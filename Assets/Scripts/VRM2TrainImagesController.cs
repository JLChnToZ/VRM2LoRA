using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Mathematics;
using TMPro;
using Cysharp.Threading.Tasks;
using UniVRM10;
using UniVRM10.Migration;
using UniGLTF;
using UniGLTF.Extensions.VRMC_vrm;
using SimpleFileBrowser;

using UnityRandom = Unity.Mathematics.Random;

public class VRM2TrainImagesController : MonoBehaviour {
    static readonly float[] StandingMuscles = new[] {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // Spine to Neck (1-12)
        0, 0, 0, 0, 0, 0, 0, 0, 0, // Head & Face (13-21)
        0.6F, 0, 0, 1, 0, 0, 0, 0, // Left Foot (22-29)
        0.6F, 0, 0, 1, 0, 0, 0, 0, // Right Foot (30-37)
        0.1F, 0, -0.6F, 0.1F, 0.1F, 0.9F, -0.1F, 0, 0, // Left Hand (38-46)
        0.1F, 0, -0.6F, 0.1F, 0.1F, 0.9F, -0.1F, 0, 0, // Right Hand (47-55)
        0, 0, 0, 0, // Left Thumb (56-59)
        0, 0, 0, 0, // Left Index (60-63)
        0, 0, 0, 0, // Left Middle (64-67)
        0, 0, 0, 0, // Left Ring (68-71)
        0, 0, 0, 0, // Left Little (72-75)
        0, 0, 0, 0, // Right Thumb (76-79)
        0, 0, 0, 0, // Right Index (80-83)
        0, 0, 0, 0, // Right Middle (84-87)
        0, 0, 0, 0, // Right Ring (88-91)
        0, 0, 0, 0, // Right Little (92-95)
    };
    static readonly Regex fileNameSanitizer = new Regex(@"[^\w\d\-_]", RegexOptions.Compiled);

    [SerializeField] Button loadButton, generatePoseButton, generateFaceButton, cancelButon;
    [SerializeField] TMP_Text vrmInfoText, facialCountText;
    [SerializeField] TMP_InputField countInput, delayInput;
    [SerializeField] GameObject generateButtonContainer;
    [SerializeField] RectTransform generateProgressBar;
    [SerializeField] new Camera camera;
    HumanPoseHandler vrmPoseHandler;
    Vrm10Instance vrmInstance;
    HumanPose pose;
    Transform cameraTransform;
    CancellationTokenSource cancelTokenSource;
    GenerateMode generateMode = GenerateMode.FullBody;
    HashSet<Transform> fullBodyBones = new HashSet<Transform>(), headShotBones = new HashSet<Transform>();
    string modelName;
    int expressionCounter;
    UnityRandom random;

    static void Noop() {}

    void Awake() {
        if (camera == null) camera = Camera.main;
        cameraTransform = camera.transform;
        loadButton.onClick.AddListener(LoadModelFromDialog);
        generatePoseButton.onClick.AddListener(StartGeneratePoses);
        generateFaceButton.onClick.AddListener(StartGenerateFaces);
        cancelButon.onClick.AddListener(CancelGenerate);
        countInput.BindReformatter();
        delayInput.BindReformatter();
    }

    void OnVRMMetaData(Texture2D thumbnail, Meta newMeta, Vrm0Meta oldMeta) {
        if (newMeta != null) {
            if (vrmInfoText != null)
                vrmInfoText.text = $"{newMeta.Name} V{newMeta.Version}\n{newMeta.CreditNotation}";
            modelName = newMeta.Name;
        } else if (oldMeta != null) {
            if (vrmInfoText != null)
                vrmInfoText.text = $"{oldMeta.title} V{oldMeta.version}\n{oldMeta.author} {oldMeta.contactInformation}";
            modelName = oldMeta.title;
        } else {
            if (vrmInfoText != null)
                vrmInfoText.text = "";
            modelName = "";
        }
    }

    void LoadModelFromDialog() {
        FileBrowser.SetFilters(true, new FileBrowser.Filter("VRM", ".vrm"));
        FileBrowser.SetDefaultFilter("VRM");
        FileBrowser.ShowLoadDialog(
            LoadModel,
            Noop,
            FileBrowser.PickMode.Files,
            title: "Load VRM Avatar",
            loadButtonText: "Open"
        );
    }

    void LoadModel(string[] paths) {
        if (paths.Length > 0) LoadModel(paths[0]).Forget();
    }

    async UniTaskVoid LoadModel(string path, CancellationToken cancelToken = default) {
        UnloadModel();
        vrmInstance = await Vrm10.LoadPathAsync(path,
            controlRigGenerationOption: ControlRigGenerationOption.None,
            vrmMetaInformationCallback: OnVRMMetaData,
            ct: cancelToken
        );
        vrmInstance.UpdateType = Vrm10Instance.UpdateTypes.None;
        var gltfInstance = vrmInstance.GetComponent<RuntimeGltfInstance>();
        gltfInstance.EnableUpdateWhenOffscreen();
        var humanoid = vrmInstance.Humanoid;
        Avatar avatar = null;
        if (vrmInstance.TryGetComponent(out Animator anim)) avatar = anim.avatar;
        if (avatar == null) avatar = humanoid.CreateAvatar();
        vrmPoseHandler = new HumanPoseHandler(avatar, vrmInstance.transform);
        var headBone = humanoid.Head;
        if (headBone != null) headShotBones.Add(headBone);
        var neckBone = humanoid.Neck;
        if (neckBone != null) headShotBones.Add(neckBone);
        var leftEye = humanoid.LeftEye;
        if (leftEye != null) headShotBones.Add(leftEye);
        var rightEye = humanoid.RightEye;
        if (rightEye != null) headShotBones.Add(rightEye);
        var jaw = humanoid.Jaw;
        if (jaw != null) headShotBones.Add(jaw);
        foreach (var smr in vrmInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            foreach (var bone in smr.bones) {
                if (bone == null || !fullBodyBones.Add(bone)) continue;
                if (bone.parent == headBone) headShotBones.Add(bone);
            }
        facialCountText.text = vrmInstance.Runtime.Expression.ExpressionKeys.Count.ToString();
        await UniTask.Yield();
        ResetPose();
    }

    void UnloadModel() {
        if (vrmInfoText != null) vrmInfoText.text = "";
        modelName = "";
        if (vrmInstance != null) {
            Destroy(vrmInstance.gameObject);
            vrmInstance = null;
        }
        if (vrmPoseHandler != null) {
            vrmPoseHandler.Dispose();
            vrmPoseHandler = null;
        }
        fullBodyBones.Clear();
        headShotBones.Clear();
        facialCountText.text = "-";
    }

    void CancelGenerate() {
        if (cancelTokenSource != null) cancelTokenSource.Cancel();
    }

    void StartGeneratePoses() {
        generateMode = GenerateMode.FullBody;
        StartGenerate();
    }

    void StartGenerateFaces() {
        generateMode = GenerateMode.FacialExpression;
        expressionCounter = -1;
        StartGenerate();
    }

    void StartGenerate() {
        if (cancelTokenSource != null) cancelTokenSource.Cancel();
        FileBrowser.ShowSaveDialog(
            GenerateAndSaveImages,
            Noop,
            FileBrowser.PickMode.Folders,
            title: "Save Images",
            saveButtonText: "Save"
        );
    }

    void GenerateAndSaveImages(string[] paths) {
        if (paths.Length == 0) return;
        cancelTokenSource = new CancellationTokenSource();
        generateButtonContainer.SetActive(false);
        cancelButon.gameObject.SetActive(true);
        GenerateAndSaveImages(paths[0], cancelTokenSource.Token).Forget();
    }

    async UniTaskVoid GenerateAndSaveImages(string dirPath, CancellationToken cancelToken = default) {
        if (!int.TryParse(countInput.text, out var count) || count <= 0) return;
        if (!float.TryParse(delayInput.text, out var delay)) delay = 0;
        generateProgressBar.anchorMax = new Vector2(0, 1);
        random = new UnityRandom(unchecked((uint)DateTime.Now.Ticks));
        RenderTexture rt = null;
        var modelNameForFile = string.IsNullOrEmpty(modelName) ? "VRMModel" : fileNameSanitizer.Replace(modelName, "_");
        try {
            rt = RenderTexture.GetTemporary(1024, 1024, 24);
            for (int i = 0; i < count; i++) {
                if (cancelToken.IsCancellationRequested) break;
                await UniTask.SwitchToMainThread();
                switch (generateMode) {
                    case GenerateMode.FullBody: RandomizePose(); break;
                    case GenerateMode.FacialExpression: RandomizeFacial(); break;
                }
                if (delay <= 0) await UniTask.Yield();
                else await UniTask.Delay((int)(delay * 1000));
                var filepath = Path.Combine(dirPath, $"{modelNameForFile}_{generateMode}_{i:D4}.png");
                var oldRT = camera.targetTexture;
                // var oldActiveRT = RenderTexture.active;
                try {
                    // RenderTexture.active = rt;
                    camera.targetTexture = rt;
                    camera.Render();
                } finally {
                    // RenderTexture.active = oldActiveRT;
                    camera.targetTexture = oldRT;
                }
                try {
                    var format = SystemInfo.GetCompatibleFormat(rt.graphicsFormat, FormatUsage.ReadPixels);
                    var asyncGPUReadback = await AsyncGPUReadback.Request(rt, 0, format);
                    var pngData = ImageConversion.EncodeNativeArrayToPNG(
                        asyncGPUReadback.GetData<byte>(),
                        format, (uint)rt.width, (uint)rt.height
                    );
                    await File.WriteAllBytesAsync(filepath, pngData.ToArray()).ConfigureAwait(false);
                } catch (Exception ex) {
                    Debug.LogException(ex);
                }
                generateProgressBar.anchorMax = new Vector2((float)(i + 1) / count, 1);
            }
        } finally {
            cancelTokenSource = null;
            await UniTask.SwitchToMainThread();
            if (rt != null) RenderTexture.ReleaseTemporary(rt);
            ResetPose();
            SetFacial(-1);
            generateButtonContainer.SetActive(true);
            cancelButon.gameObject.SetActive(false);
        }
    }

    void ResetPose() {
        if (vrmPoseHandler == null) return;
        vrmPoseHandler.GetHumanPose(ref pose);
        Array.Copy(StandingMuscles, pose.muscles, StandingMuscles.Length);
        vrmPoseHandler.SetHumanPose(ref pose);
        PlaceCameraFullBody(0);
    }

    void RandomizePose() {
        if (vrmPoseHandler == null) return;
        vrmPoseHandler.GetHumanPose(ref pose);
        for (int i = 0; i < pose.muscles.Length; i++)
            pose.muscles[i] = random.NextFloat(-0.7F, 0.7F);
        // Eyes special treatment: look at same direction
        pose.muscles[17] = pose.muscles[15]; // Up-Down
        pose.muscles[18] = -pose.muscles[16]; // In-Out
        vrmPoseHandler.SetHumanPose(ref pose);
        SetFacial(-1);
        PlaceCameraFullBody(random.NextFloat2(new float2(-30F, -90F), new float2(45F, 90F)));
    }

    void RandomizeFacial() {
        if (vrmInstance == null) return;
        ResetPose();
        var keys = vrmInstance.Runtime.Expression.ExpressionKeys;
        if (keys.Count > 0) {
            SetFacial(expressionCounter);
            expressionCounter = (expressionCounter + 1) % (keys.Count + 1);
        }
        PlaceCameraHeadShot(random.NextFloat2(new float2(-30F, -30F), new float2(30F, 30F)));
    }

    void SetFacial(int expressionIndex) {
        if (vrmInstance == null) return;
        var runtime = vrmInstance.Runtime;
        var runtimeExperssion = runtime.Expression;
        var keys = runtimeExperssion.ExpressionKeys;
        if (keys.Count <= 0) return;
        for (int i = 0; i < keys.Count; i++)
            runtimeExperssion.SetWeight(keys[i], i == expressionIndex ? 1 : 0);
        runtime.Process();
    }

    void PlaceCameraFullBody(float2 yawPitch) {
        if (vrmInstance == null) return;
        float3 min = float.MaxValue, max = float.MinValue;
        foreach (var bone in fullBodyBones)
            CalculacteBounds(ref min, ref max, bone);
        PlaceCamera((min + max) / 2, yawPitch, CalculateCameraDistance(min, max, 0.75F));
    }

    void PlaceCameraHeadShot(float2 yawPitch) {
        if (vrmInstance == null) return;
        float3 min = float.MaxValue, max = float.MinValue;
        foreach (var bone in headShotBones)
            CalculacteBounds(ref min, ref max, bone);
        PlaceCamera((min + max) / 2, yawPitch, CalculateCameraDistance(min, max, 1F));
    }

    static void CalculacteBounds(ref float3 min, ref float3 max, Transform bone) {
        if (bone == null) return;
        var pos = bone.position;
        min = math.min(min, pos);
        max = math.max(max, pos);
    }

    float CalculateCameraDistance(float3 min, float3 max, float scale) {
        return math.distance(min, max) / math.tan(math.radians(camera.fieldOfView) / 2) * scale;
    }

    void PlaceCamera(float3 target, float2 yawPitch, float distance) {
        var cameraPosition = target + math.mul(quaternion.Euler(new float3(math.radians(yawPitch), 0)), new float3(0, 0, distance));
        var cameraRotation = quaternion.LookRotationSafe(target - cameraPosition, new float3(0, 1, 0));
        cameraTransform.SetPositionAndRotation(cameraPosition, cameraRotation);
    }

    enum GenerateMode {
        FullBody,
        FacialExpression,
    }
}
