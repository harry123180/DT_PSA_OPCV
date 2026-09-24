using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PythonLink
{
    public enum WsState
    {
        Closed = 0,
        Connecting = 1,
        Open = 2,
    }

    /// <summary>最小的 WebSocket 介面：輪詢式收訊，主執行緒呼叫。</summary>
    public interface IWsTransport
    {
        WsState State { get; }
        void Connect(string url);
        void Close();
        void SendBinary(byte[] data, int length);
        void SendText(string text);
        /// <summary>取最新一筆二進位訊息（較舊的直接丟掉：Python 每次送的是完整映像）。</summary>
        bool TryTakeLatestBinary(out byte[] data);
        bool TryTakeText(out string text);
    }

    public static class WsTransportFactory
    {
        public static IWsTransport Create()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WsTransportBrowser();
#else
            return new WsTransportDotNet();
#endif
        }

        /// <summary>網址列的查詢參數（例如 ?ws=ws://127.0.0.1:9000）；非網頁版回空字串。</summary>
        public static string QueryParam(string name)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return WsTransportBrowser.PLWS_QueryParam(name) ?? "";
#else
            return "";
#endif
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>瀏覽器版：實作在 Plugins/WebGL/PythonLinkWS.jslib。</summary>
    public class WsTransportBrowser : IWsTransport
    {
        [DllImport("__Internal")] private static extern void PLWS_Connect(string url);
        [DllImport("__Internal")] private static extern int PLWS_State();
        [DllImport("__Internal")] private static extern void PLWS_Close();
        [DllImport("__Internal")] private static extern void PLWS_SendBinary(byte[] data, int length);
        [DllImport("__Internal")] private static extern void PLWS_SendText(string text);
        [DllImport("__Internal")] private static extern int PLWS_PollBinary(byte[] buffer, int maxLength);
        [DllImport("__Internal")] private static extern string PLWS_PollText();
        [DllImport("__Internal")] public static extern string PLWS_QueryParam(string name);

        private byte[] _rx = new byte[4096];

        public WsState State => (WsState)PLWS_State();
        public void Connect(string url) => PLWS_Connect(url);
        public void Close() => PLWS_Close();
        public void SendBinary(byte[] data, int length) => PLWS_SendBinary(data, length);
        public void SendText(string text) => PLWS_SendText(text);

        public bool TryTakeLatestBinary(out byte[] data)
        {
            data = null;
            var n = PLWS_PollBinary(_rx, _rx.Length);
            if (n < 0) return false;
            if (n > _rx.Length)
            {
                // 緩衝不夠：放大後下一筆再收（這一筆已被 JS 端取走，Python 會持續重送）
                _rx = new byte[n * 2];
                return false;
            }
            data = new byte[n];
            Buffer.BlockCopy(_rx, 0, data, 0, n);
            return true;
        }

        public bool TryTakeText(out string text)
        {
            text = PLWS_PollText();
            return !string.IsNullOrEmpty(text);
        }
    }
#endif

    /// <summary>Editor／桌面版：用 .NET ClientWebSocket，收訊在背景工作、主執行緒輪詢。</summary>
    public class WsTransportDotNet : IWsTransport
    {
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<byte[]> _bin = new();
        private readonly ConcurrentQueue<string> _txt = new();
        private volatile int _state;

        public WsState State => (WsState)_state;

        public void Connect(string url)
        {
            Close();
            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();
            _state = (int)WsState.Connecting;
            _ = RunAsync(_ws, new Uri(url), _cts.Token);
        }

        private async Task RunAsync(ClientWebSocket ws, Uri uri, CancellationToken ct)
        {
            try
            {
                await ws.ConnectAsync(uri, ct);
                _state = (int)WsState.Open;
                var buffer = new byte[65536];
                var message = new System.IO.MemoryStream();
                while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    var r = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    message.Write(buffer, 0, r.Count);
                    if (!r.EndOfMessage) continue;
                    var bytes = message.ToArray();
                    message.SetLength(0);
                    if (r.MessageType == WebSocketMessageType.Text) _txt.Enqueue(Encoding.UTF8.GetString(bytes));
                    else _bin.Enqueue(bytes);
                }
            }
            catch (Exception)
            {
                // 連不上或斷線：狀態改成 Closed，由上層定時重連
            }
            finally
            {
                _state = (int)WsState.Closed;
            }
        }

        public void Close()
        {
            try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
            try { _ws?.Abort(); } catch (Exception) { }
            _ws = null;
            _state = (int)WsState.Closed;
        }

        public void SendBinary(byte[] data, int length)
        {
            if (_state != (int)WsState.Open || _ws == null) return;
            var copy = new byte[length];
            Buffer.BlockCopy(data, 0, copy, 0, length);
            Send(copy, WebSocketMessageType.Binary);
        }

        public void SendText(string text)
        {
            if (_state != (int)WsState.Open || _ws == null) return;
            Send(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text);
        }

        // ClientWebSocket 同時只能有一個 SendAsync，排隊送出
        private Task _sendChain = Task.CompletedTask;
        private readonly object _sendLock = new();

        private void Send(byte[] payload, WebSocketMessageType type)
        {
            var ws = _ws;
            lock (_sendLock)
            {
                _sendChain = _sendChain.ContinueWith(async _ =>
                {
                    try { await ws.SendAsync(new ArraySegment<byte>(payload), type, true, CancellationToken.None); }
                    catch (Exception) { }
                }).Unwrap();
            }
        }

        public bool TryTakeLatestBinary(out byte[] data)
        {
            data = null;
            while (_bin.TryDequeue(out var item)) data = item;
            return data != null;
        }

        public bool TryTakeText(out string text) => _txt.TryDequeue(out text);
    }
}
