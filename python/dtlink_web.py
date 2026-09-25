"""網頁版（Pyodide，在瀏覽器的 Web Worker 裡跑）的 dtlink。

學生照樣寫 `from dtlink import Twin; twin = Twin()`：啟動時 install() 把 dtlink.Twin 換成 WebTwin，
所以同一份程式在網頁和自己電腦（dtlink.py＋websockets）都能跑。

和孿生的交換走 SharedArrayBuffer（頁面主執行緒的 bridge 建立，傳進 worker 放在 self.dt）：
- dt.stat：狀態映像（孿生每個物理週期寫入）
- dt.ctrl：控制映像（這裡寫，bridge 送進孿生）
- dt.hdr：Int32Array［0 狀態序號、1 控制已變更、2 已連線、3 睡眠用］
Python 在 while 迴圈或 sleep 裡時收不到 postMessage，所以一定要共享記憶體。
"""
from __future__ import annotations

import json
import threading
import time

import js
from pyodide.ffi import to_js

import dtlink
from dtlink import Variable

HDR_SEQ, HDR_DIRTY, HDR_CONNECTED, HDR_SLEEP = 0, 1, 2, 3


def _sleep(seconds: float):
    """用 Atomics.wait 真正讓出 CPU（Pyodide 預設的 time.sleep 在 worker 裡是忙等）。"""
    ms = max(0.0, float(seconds)) * 1000.0
    if ms > 0:
        js.Atomics.wait(js.dt.hdr, HDR_SLEEP, 0, ms)


class WebTwin(dtlink.Twin):
    """瀏覽器內的孿生。參數（host、port）為了和本機版相容而保留，這裡不用。"""

    def __init__(self, host: str = "", port: int = 0, name: str = "browser", verbose: bool = True):
        self.name, self.verbose = name, verbose
        self._lock = threading.Lock()
        self.variables: dict[str, Variable] = {}
        self.device_info: list[dict] = []
        m = json.loads(str(js.dt.manifest))
        self.variables = {v["name"]: Variable(**{k: v[k] for k in ("name", "dir", "offset", "size", "dtype", "count")})
                          for v in m["variables"]}
        self.device_info = m["devices"]
        # 從目前的控制映像接著寫（上一次執行留下的輸出還在，按「停止」時頁面會歸零）
        self._control = bytearray(js.dt.ctrl.slice().to_py())
        if self.verbose:
            print(f"[dtlink] 網頁孿生：{len(self.device_info)} 個裝置")

    # ── 傳輸掛勾 ──
    def _status_bytes(self) -> bytes:
        return bytes(js.dt.stat.slice().to_py())

    def _control_bytes(self) -> bytes:
        return bytes(js.dt.ctrl.slice().to_py())

    def _sync_control(self, offset: int, size: int):
        # 暫存器表頁面可能手動改過控制映像：先讀回要改的那一段，免得用舊值蓋掉別人的修改
        chunk = js.dt.ctrl.slice(offset, offset + size).to_py()
        self._control[offset:offset + size] = bytes(chunk)

    def _control_changed(self, offset: int | None = None, size: int | None = None):
        # 只寫回改動的範圍（不整塊覆寫），手動切換的其他位元組保持不變
        if offset is None:
            offset, size = 0, len(self._control)
        js.dt.ctrl.set(to_js(memoryview(self._control)[offset:offset + size]), offset)
        js.Atomics.store(js.dt.hdr, HDR_DIRTY, 1)

    # ── 連線狀態 ──
    @property
    def connected(self) -> bool:
        return js.Atomics.load(js.dt.hdr, HDR_CONNECTED) == 1

    def wait_connected(self, timeout: float | None = None) -> bool:
        return True  # 程式是在孿生送出 manifest 之後才開始跑的

    def sleep(self, seconds: float):
        _sleep(seconds)


def _load_labels_web() -> dict:
    """網頁版：用同步請求讀網站根目錄的 devices_zh.json（worker 裡可以用同步 XHR）。"""
    try:
        from pyodide.http import open_url
        url = str(js.URL.new("devices_zh.json", js.location.href))
        return json.loads(open_url(url).read()).get("devices", {})
    except Exception:
        return {}


def install():
    """讓學生程式的 Twin() 與 time.sleep 都用網頁版。"""
    dtlink.Twin = WebTwin
    dtlink._load_labels = _load_labels_web
    time.sleep = _sleep
