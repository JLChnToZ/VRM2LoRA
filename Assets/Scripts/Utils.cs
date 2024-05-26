using System.Text.RegularExpressions;
using TMPro;
using System.Text;

public static class Utils {
    static readonly Regex integerRegex = new Regex(@"^\s*([+-]?)0*(\d+)\s*$", RegexOptions.Compiled);
    static readonly Regex decimalRegex = new Regex(@"^\s*([+-]?)0*(\d+)(?:\.(\d*)0*)?\s*$", RegexOptions.Compiled);
    static readonly StringBuilder sb = new StringBuilder();

    public static void BindReformatter(this TMP_InputField inputField) {
        inputField.onEndEdit.AddListener(text => {
            Match m;
            switch (inputField.contentType) {
                case TMP_InputField.ContentType.IntegerNumber:
                    m = integerRegex.Match(text);
                    if (m.Success) {
                        sb.Clear();
                        sb.Append(m.Groups[1].Value);
                        sb.Append(m.Groups[2].Value);
                        inputField.SetTextWithoutNotify(sb.ToString());
                    }
                    break;
                case TMP_InputField.ContentType.DecimalNumber:
                    m = decimalRegex.Match(text);
                    if (m.Success) {
                        sb.Clear();
                        sb.Append(m.Groups[1].Value);
                        sb.Append(m.Groups[2].Value);
                        if (m.Groups[3].Success) {
                            sb.Append('.');
                            sb.Append(m.Groups[3].Value);
                        }
                        inputField.SetTextWithoutNotify(sb.ToString());
                    }
                    break;
            }
        });
    }
}