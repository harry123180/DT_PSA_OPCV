// 瀏覽器端的 WebSocket：Unity 以輪詢方式取訊息（每個 FixedUpdate 一次），不用 SendMessage 回呼。
// 二進位只保留最新一筆：Python 每次送的是完整的控制映像，舊的沒有意義。
var PythonLinkWS = {
  $plws: { socket: null, state: 0, bin: null, txt: [] },

  PLWS_Connect: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    if (plws.socket) {
      try { plws.socket.onclose = null; plws.socket.close(); } catch (e) {}
    }
    plws.bin = null;
    plws.txt = [];
    plws.state = 1;
    var s;
    try {
      s = new WebSocket(url);
    } catch (e) {
      console.warn("[PythonLink] 無法建立 WebSocket：" + e);
      plws.state = 0;
      return;
    }
    s.binaryType = "arraybuffer";
    s.onopen = function () { plws.state = 2; console.log("[PythonLink] 已連線 " + url); };
    s.onclose = function () { plws.state = 0; };
    s.onerror = function () { plws.state = 0; };
    s.onmessage = function (ev) {
      if (typeof ev.data === "string") {
        plws.txt.push(ev.data);
        if (plws.txt.length > 64) plws.txt.shift();
      } else {
        plws.bin = new Uint8Array(ev.data);
      }
    };
    plws.socket = s;
  },

  PLWS_State: function () {
    return plws.state;
  },

  PLWS_Close: function () {
    if (plws.socket) {
      try { plws.socket.onclose = null; plws.socket.close(); } catch (e) {}
    }
    plws.socket = null;
    plws.state = 0;
  },

  PLWS_SendBinary: function (ptr, length) {
    if (plws.state !== 2 || !plws.socket) return;
    plws.socket.send(HEAPU8.slice(ptr, ptr + length));
  },

  PLWS_SendText: function (strPtr) {
    if (plws.state !== 2 || !plws.socket) return;
    plws.socket.send(UTF8ToString(strPtr));
  },

  // 回傳訊息長度；-1＝沒有新訊息。長度大於 maxLength 時只回長度、不複製（C# 端會放大緩衝）
  PLWS_PollBinary: function (ptr, maxLength) {
    var a = plws.bin;
    if (!a) return -1;
    plws.bin = null;
    if (a.length <= maxLength) HEAPU8.set(a, ptr);
    return a.length;
  },

  PLWS_PollText: function () {
    if (!plws.txt.length) return 0;
    var s = plws.txt.shift();
    var n = lengthBytesUTF8(s) + 1;
    var b = _malloc(n);
    stringToUTF8(s, b, n);
    return b;
  },

  PLWS_QueryParam: function (namePtr) {
    var v = "";
    try { v = new URLSearchParams(window.location.search).get(UTF8ToString(namePtr)) || ""; } catch (e) {}
    var n = lengthBytesUTF8(v) + 1;
    var b = _malloc(n);
    stringToUTF8(v, b, n);
    return b;
  }
};

autoAddDeps(PythonLinkWS, "$plws");
mergeInto(LibraryManager.library, PythonLinkWS);
