// 跨來源隔離的備援：伺服器已經送 COOP／COEP，但有些瀏覽器擴充功能會把 COEP 拿掉，
// 頁面就沒有 SharedArrayBuffer（網頁 Python 用它跟孿生共享記憶體）。
// 這個 Service Worker 替同一個站的每個回應補上這三個標頭；擴充功能改的是網路回應，改不到這裡產生的回應。
// 由 index.html／registers.html 在 crossOriginIsolated 為 false 時註冊（作法同 coi-serviceworker）。
self.addEventListener("install", () => self.skipWaiting());
self.addEventListener("activate", (e) => e.waitUntil(self.clients.claim()));

self.addEventListener("fetch", (e) => {
  const req = e.request;
  // 跨來源隔離只看「頁面」與「Worker 腳本」的標頭；其他資源一律不攔截。
  // 攔截 Unity 的建置檔會打壞它的快取機制（第二次載入用 IndexedDB＋條件式請求，經過這裡就讀到空資料，
  // 「Cannot read properties of undefined (reading 'subarray')」）
  if (req.mode !== "navigate" && !["worker", "sharedworker"].includes(req.destination)) return;
  if (req.cache === "only-if-cached" && req.mode !== "same-origin") return;
  e.respondWith(
    fetch(req).then((res) => {
      if (res.status === 0) return res;                 // opaque（跨來源 no-cors）不能改
      const h = new Headers(res.headers);
      h.set("Cross-Origin-Embedder-Policy", "require-corp");
      h.set("Cross-Origin-Opener-Policy", "same-origin");
      h.set("Cross-Origin-Resource-Policy", "same-origin");
      return new Response(res.body, { status: res.status, statusText: res.statusText, headers: h });
    })
  );
});
