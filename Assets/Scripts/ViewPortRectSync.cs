using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class ViewPortRectSync : MonoBehaviour {
    new RectTransform transform;
    Canvas rootCanvas;
    RectTransform rootCanvasTransform;
    [SerializeField] new Camera camera;
    readonly Vector3[] corners = new Vector3[4];

    void Awake() {
        if (camera == null) camera = Camera.main;
        transform = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>(true).rootCanvas;
        rootCanvasTransform = rootCanvas.GetComponent<RectTransform>();
    }

    void Update() {
        transform.GetWorldCorners(corners);
        var min = rootCanvasTransform.InverseTransformPoint(corners[0]);
        var max = rootCanvasTransform.InverseTransformPoint(corners[2]);
        var rootRect = rootCanvasTransform.rect;
        camera.rect = new Rect(
            (min.x - rootRect.x) / rootRect.width,
            (min.y - rootRect.y) / rootRect.height,
            (max.x - min.x) / rootRect.width,
            (max.y - min.y) / rootRect.height
        );
    }
}
