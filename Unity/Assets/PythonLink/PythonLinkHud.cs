using UnityEngine;

namespace PythonLink
{
    /// <summary>
    /// 右下角的連線狀態。IMGUI 的預設字型在 WebGL 沒有中日韓字，所以文字用英文。
    /// </summary>
    public class PythonLinkHud : MonoBehaviour
    {
        [SerializeField] private PythonLinkClient _client;

        private GUIStyle _box;
        private GUIStyle _label;

        private void Awake()
        {
            if (_client == null) _client = FindAnyObjectByType<PythonLinkClient>();
        }

        private void OnGUI()
        {
            if (_client == null) return;
            _box ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft };
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };

            string head, detail;
            switch (_client.State)
            {
                case WsState.Open:
                    var ago = _client.LastControlTime < 0 ? "no command yet" : $"last command {Time.unscaledTime - _client.LastControlTime:0.0}s ago";
                    var who = string.IsNullOrEmpty(_client.PythonClientName) ? "" : $" ({_client.PythonClientName})";
                    head = $"<color=#3ddc84>Python connected</color>{who}";
                    detail = $"{_client.DeviceCount} devices | {ago}";
                    break;
                case WsState.Connecting:
                    head = "<color=#ffcc00>Connecting to Python...</color>";
                    detail = _client.Url;
                    break;
                default:
                    head = "<color=#ff6b6b>Python not connected</color>";
                    detail = $"run: python dtlink_demo.py  ({_client.Url})";
                    break;
            }

            const float w = 360f, h = 52f;
            var rect = new Rect(Screen.width - w - 12f, Screen.height - h - 12f, w, h);
            GUI.Box(rect, GUIContent.none, _box);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, w - 20f, 20f), head, _label);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 26f, w - 20f, 20f), detail, _label);
        }
    }
}
