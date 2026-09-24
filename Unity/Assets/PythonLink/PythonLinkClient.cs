using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using OC.Communication;
using UnityEngine;

namespace PythonLink
{
    /// <summary>
    /// 取代 TcAdsClient 的閘道：孿生改由本機 Python 驅動。
    ///
    /// 協定（ws://127.0.0.1:8765，孿生是 WebSocket 用戶端、Python 是伺服器）：
    /// 1. 連上後孿生先送一則文字訊息 manifest（JSON）：控制／狀態映像的大小、每個變數的位置與型別、裝置清單。
    /// 2. 之後每個物理週期（預設 50 Hz），狀態有變或每 0.5 秒送一次二進位 0x01 + 狀態映像（Status）。
    /// 3. Python 隨時送二進位 0x02 + 完整控制映像（Control）；孿生在下一個物理週期套用最新一筆。
    /// 全部 little-endian。斷線時裝置回到未連線狀態，孿生每 2 秒重連一次。
    /// </summary>
    public class PythonLinkClient : Client
    {
        public const byte FrameStatus = 0x01;
        public const byte FrameControl = 0x02;

        [Header("Python Link")]
        [SerializeField] private string _url = "ws://127.0.0.1:8765";
        [SerializeField] private float _reconnectInterval = 2f;
        [SerializeField] private float _heartbeatInterval = 0.5f;

        private readonly PythonLinkBuffer _buffer = new();
        private IWsTransport _ws;
        private bool _built;
        private bool _wasOpen;
        private float _nextConnect;
        private float _lastSent;
        private byte[] _txFrame = System.Array.Empty<byte>();
        private byte[] _lastStatus = System.Array.Empty<byte>();

        public override IClientBuffer Buffer => _buffer;
        public string Url => _url;
        public WsState State => _ws?.State ?? WsState.Closed;
        public int DeviceCount { get; private set; }
        public int ControlSize => _buffer.InputBytes.Length;
        public int StatusSize => _buffer.OutputBytes.Length;
        public float LastControlTime { get; private set; } = -1f;
        public string PythonClientName { get; private set; } = "";

        private void Awake()
        {
            var fromQuery = WsTransportFactory.QueryParam("ws");
            if (!string.IsNullOrEmpty(fromQuery)) _url = fromQuery;
            _ws = WsTransportFactory.Create();
        }

        private void Update()
        {
            if (_ws == null) return;
            var state = _ws.State;

            if (state == WsState.Closed)
            {
                if (_wasOpen) OnClosed();
                if (Time.unscaledTime >= _nextConnect)
                {
                    _nextConnect = Time.unscaledTime + _reconnectInterval;
                    _ws.Connect(_url);
                }
                return;
            }

            if (state == WsState.Open && !_wasOpen) OnOpened();

            while (_ws.TryTakeText(out var text)) HandleText(text);
        }

        private void OnOpened()
        {
            _wasOpen = true;
            EnsureBuffer();
            _ws.SendText(_buffer.ManifestJson(_rootName));
            _lastStatus = System.Array.Empty<byte>();
            _buffer.SetConnected(true);
            _isConnected.Value = true;
            Debug.Log($"[PythonLink] connected {_url}: {DeviceCount} devices, control {ControlSize} B, status {StatusSize} B");
        }

        private void OnClosed()
        {
            _wasOpen = false;
            _buffer.SetConnected(false);
            _isConnected.Value = false;
            PythonClientName = "";
            System.Array.Clear(_buffer.InputBytes, 0, _buffer.InputBytes.Length);
            Debug.Log("[PythonLink] disconnected");
        }

        private void HandleText(string text)
        {
            // Python 可以送 {"type":"hello","name":"..."}；其餘文字訊息目前忽略
            if (text.Contains("\"hello\""))
            {
                var hello = JsonUtility.FromJson<Hello>(text);
                PythonClientName = hello?.name ?? "python";
            }
        }

        [System.Serializable]
        private class Hello
        {
            public string type;
            public string name;
        }

        /// <summary>第一次連線時依場景裡的 Link 排出映像版面（場景不變，之後重連沿用）。</summary>
        private void EnsureBuffer()
        {
            if (_built) return;
            var links = new List<Link>();
            var system = typeof(Client).GetField("_link", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(this) as Link;
            if (system != null) links.Add(system);
            foreach (var component in GetComponentsInChildren<ILink>(true))
            {
                if (component.Link != null && component.Link.Enable) links.Add(component.Link);
            }
            _buffer.Build(links);
            DeviceCount = links.Count;
            _txFrame = new byte[_buffer.OutputBytes.Length + 1];
            _txFrame[0] = FrameStatus;
            _built = true;
        }

        public override void BeforeFixedUpdate()
        {
            if (_isConnected.Value && _ws != null && _ws.TryTakeLatestBinary(out var frame))
            {
                if (frame.Length == _buffer.InputBytes.Length + 1 && frame[0] == FrameControl)
                {
                    System.Buffer.BlockCopy(frame, 1, _buffer.InputBytes, 0, _buffer.InputBytes.Length);
                    LastControlTime = Time.unscaledTime;
                }
            }
            base.BeforeFixedUpdate();
        }

        public override void AfterFixedUpdate()
        {
            base.AfterFixedUpdate();
            if (!_isConnected.Value || _ws == null || _ws.State != WsState.Open) return;

            var status = _buffer.OutputBytes;
            var changed = _lastStatus.Length != status.Length || !SameBytes(_lastStatus, status);
            if (!changed && Time.unscaledTime - _lastSent < _heartbeatInterval) return;

            System.Buffer.BlockCopy(status, 0, _txFrame, 1, status.Length);
            _ws.SendBinary(_txFrame, _txFrame.Length);
            if (_lastStatus.Length != status.Length) _lastStatus = new byte[status.Length];
            System.Buffer.BlockCopy(status, 0, _lastStatus, 0, status.Length);
            _lastSent = Time.unscaledTime;
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            for (var i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        public override void Connect()
        {
            _nextConnect = 0f;
            if (_ws == null) _ws = WsTransportFactory.Create();
        }

        public override void Disconnect()
        {
            _ws?.Close();
            if (_wasOpen) OnClosed();
        }

        private void OnDestroy() => _ws?.Close();

        public override XElement GetAsset() =>
            new("PythonLinkClient", new XAttribute("Name", name), new XAttribute("Url", _url));

        public override void SetAsset(XElement xElement)
        {
            var url = xElement?.Attribute("Url")?.Value;
            if (!string.IsNullOrEmpty(url)) _url = url;
        }
    }
}
