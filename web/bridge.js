// 孿生 ↔ 瀏覽器 Python 的橋接器（頁面主執行緒）。
//
// Unity 的 PythonLinkWS.jslib 遇到網址 "page:" 時呼叫 DTPageBridge.createSocket()，
// 拿到一個長得像 WebSocket 的物件：Unity 送出的 manifest／狀態映像進到這裡，
// 控制映像由這裡送回 Unity。資料放在 SharedArrayBuffer，Web Worker 裡的 Python 直接讀寫
// （Python 在 while 迴圈或 sleep 裡時收不到 postMessage，所以不能靠訊息傳遞）。
//
// SharedArrayBuffer 版面：Int32 表頭 4 格［0 狀態序號、1 控制已變更、2 已連線、3 睡眠用］，
// 接著控制映像（controlSize）、狀態映像（statusSize）。
(function () {
  "use strict";
  const HDR_SEQ = 0, HDR_DIRTY = 1, HDR_CONN = 2, HDR_SLEEP = 3;
  const HEADER_BYTES = 16;
  const RESEND_MS = 200;

  const B = {
    socket: null,
    manifestText: null,
    manifest: null,
    sab: null,
    hdr: null,
    ctrl: null,
    stat: null,
    listeners: [],
    _pump: null,
    _lastSent: 0,

    createSocket() {
      const s = {
        binaryType: "arraybuffer",
        readyState: 0,
        onopen: null, onclose: null, onerror: null, onmessage: null,
        send(data) { B._fromUnity(data); },
        close() { B._closed(s); },
      };
      B.socket = s;
      setTimeout(() => { s.readyState = 1; if (s.onopen) s.onopen(); }, 0);
      return s;
    },

    onReady(fn) {
      B.listeners.push(fn);
      if (B.manifest) fn(B);
    },

    get connected() { return !!(B.hdr && Atomics.load(B.hdr, HDR_CONN) === 1); },

    /** 所有控制輸出歸零（按「停止」時呼叫）。 */
    stopAll() {
      if (!B.ctrl) return;
      B.ctrl.fill(0);
      Atomics.store(B.hdr, HDR_DIRTY, 1);
    },

    /** 叫醒正在 time.sleep() 的 Python（停止時用，讓中斷馬上生效）。 */
    wakeSleepers() {
      if (B.hdr) Atomics.notify(B.hdr, HDR_SLEEP);
    },

    _fromUnity(data) {
      if (typeof data === "string") {
        let m;
        try { m = JSON.parse(data); } catch (e) { return; }
        if (m.type === "manifest") B._setup(m, data);
        return;
      }
      const a = data instanceof Uint8Array ? data : new Uint8Array(data);
      if (a[0] === 0x01 && B.stat && a.length - 1 === B.stat.length) {
        B.stat.set(a.subarray(1));
        Atomics.add(B.hdr, HDR_SEQ, 1);
      }
    },

    _setup(m, text) {
      const c = m.controlSize, s = m.statusSize;
      const sameLayout = B.manifest && B.manifest.controlSize === c && B.manifest.statusSize === s;
      if (!sameLayout) {
        B.sab = new SharedArrayBuffer(HEADER_BYTES + c + s);
        B.hdr = new Int32Array(B.sab, 0, 4);
        B.ctrl = new Uint8Array(B.sab, HEADER_BYTES, c);
        B.stat = new Uint8Array(B.sab, HEADER_BYTES + c, s);
      }
      B.manifest = m;
      B.manifestText = text;
      Atomics.store(B.hdr, HDR_CONN, 1);
      Atomics.store(B.hdr, HDR_DIRTY, 1);
      B._toUnityText(JSON.stringify({ type: "hello", name: "browser editor" }));
      if (!B._pump) B._pump = setInterval(B._pumpControl, 20);
      for (const fn of B.listeners) { try { fn(B); } catch (e) { console.error(e); } }
    },

    _pumpControl() {
      const s = B.socket;
      if (!s || s.readyState !== 1 || !B.ctrl || !s.onmessage) return;
      const now = performance.now();
      const dirty = Atomics.exchange(B.hdr, HDR_DIRTY, 0) === 1;
      if (!dirty && now - B._lastSent < RESEND_MS) return;
      const frame = new Uint8Array(1 + B.ctrl.length);
      frame[0] = 0x02;
      frame.set(B.ctrl, 1);
      s.onmessage({ data: frame.buffer });
      B._lastSent = now;
    },

    _toUnityText(text) {
      const s = B.socket;
      if (s && s.onmessage) s.onmessage({ data: text });
    },

    _closed(s) {
      if (B.socket !== s) return;
      s.readyState = 3;
      if (B.hdr) Atomics.store(B.hdr, HDR_CONN, 0);
    },
  };

  window.DTPageBridge = B;

  // ── 暫存器表中心：把過程映像廣播給 registers.html（另一個分頁），並接收手動寫入 ──
  // jslib 在 manifest／狀態／控制經過時呼叫 DTTap，網頁 Python 與本機 Python 兩種模式都看得到。
  // 手動寫入只在網頁 Python 模式生效（控制映像在 SharedArrayBuffer）；本機模式由 Python 每 0.2 秒整塊重送，只能看。
  const localMode = !!new URLSearchParams(location.search).get("ws");
  const hub = {
    manifestText: null,
    ctrl: null,
    stat: null,
    lastBroadcast: 0,
    changed: false,
    channel: ("BroadcastChannel" in window) ? new BroadcastChannel("dt-registers") : null,
  };

  window.DTTap = {
    text(t) {
      if (t.indexOf('"manifest"') < 0) return;
      hub.manifestText = t;
      hub.sendManifest();
    },
    status(frame) {
      if (frame[0] !== 0x01) return;
      hub.stat = frame.slice(1);
      hub.changed = true;
    },
    control(frame) {
      if (frame[0] !== 0x02) return;
      hub.ctrl = frame.slice(1);
      hub.changed = true;
    },
  };

  hub.writable = () => !localMode && !!B.ctrl;
  hub.sendManifest = () => {
    if (!hub.channel || !hub.manifestText) return;
    hub.channel.postMessage({ t: "manifest", text: hub.manifestText, mode: localMode ? "local" : "page", writable: hub.writable() });
  };
  hub.sendImage = (force) => {
    if (!hub.channel || !hub.manifestText) return;
    const now = performance.now();
    if (!force && !hub.changed && now - hub.lastBroadcast < 1000) return;
    // 網頁模式直接讀 SharedArrayBuffer（包含手動切換、還沒送進孿生的最新值）
    const ctrl = (!localMode && B.ctrl) ? B.ctrl.slice() : (hub.ctrl || new Uint8Array(0));
    const stat = hub.stat || new Uint8Array(0);
    hub.channel.postMessage({ t: "image", ctrl, stat, writable: hub.writable(), ts: Date.now() });
    hub.changed = false;
    hub.lastBroadcast = now;
  };

  if (hub.channel) {
    hub.channel.onmessage = (e) => {
      const m = e.data || {};
      if (m.t === "hello") { hub.sendManifest(); hub.sendImage(true); return; }
      if (!hub.writable()) {
        if (m.t === "write" || m.t === "zero") hub.channel.postMessage({ t: "readonly" });
        return;
      }
      if (m.t === "write" && Array.isArray(m.bytes)) {
        const off = m.offset | 0;
        if (off < 0 || off + m.bytes.length > B.ctrl.length) return;
        B.ctrl.set(m.bytes, off);
        Atomics.store(B.hdr, 1, 1);          // HDR_DIRTY：下一個 20 ms 送進孿生
        hub.changed = true;
        hub.sendImage(true);
      } else if (m.t === "zero") {
        B.stopAll();
        hub.changed = true;
        hub.sendImage(true);
      }
    };
    setInterval(() => hub.sendImage(false), 100);
    window.addEventListener("pagehide", () => hub.channel.postMessage({ t: "bye" }));
  }
})();
