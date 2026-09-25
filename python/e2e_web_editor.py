"""網頁 Python 編輯器端對端測試（Pyodide in Web Worker ↔ 孿生）。

    python e2e_web_editor.py <站台目錄>          # 本機靜態伺服器（帶 COOP/COEP）
    python e2e_web_editor.py https://dt.qianpro.shop/
"""
import functools
import http.server
import sys
import threading
import time
from pathlib import Path

from playwright.sync_api import sync_playwright

HERE = Path(__file__).parent
target = sys.argv[1]
results = []


def check(cond, msg):
    results.append((bool(cond), msg))
    print(("PASS " if cond else "FAIL ") + msg, flush=True)


class Handler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {**http.server.SimpleHTTPRequestHandler.extensions_map,
                      ".wasm": "application/wasm", ".js": "application/javascript", ".mjs": "application/javascript"}

    def end_headers(self):
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        self.send_header("Cache-Control", "no-cache")
        super().end_headers()

    def log_message(self, *a):
        pass


if target.startswith("http"):
    url = target
else:
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 8161), functools.partial(Handler, directory=target))
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    url = "http://127.0.0.1:8161/"

with sync_playwright() as p:
    browser = p.chromium.launch(channel="chrome", headless=True,
                                args=["--enable-unsafe-swiftshader", "--ignore-gpu-blocklist",
                                      "--host-resolver-rules=MAP dt.qianpro.shop 104.21.28.23"])
    page = browser.new_page(viewport={"width": 1600, "height": 900})
    errors = []
    page.on("pageerror", lambda e: errors.append(str(e)))
    page.goto(url, wait_until="load", timeout=120000)

    check(page.evaluate("window.crossOriginIsolated"), "頁面已跨來源隔離（SharedArrayBuffer 可用）")
    page.wait_for_function("document.getElementById('status').textContent.includes('就緒')", timeout=180000)
    check(True, "Pyodide 載入完成：" + page.inner_text("#status"))
    console = lambda: page.inner_text("#console")
    set_code = lambda code: page.evaluate("c => document.querySelector('.CodeMirror').CodeMirror.setValue(c)", code)
    wait_done = lambda t: page.wait_for_function("!document.getElementById('run').disabled", timeout=t)

    # 1. 基本範例
    page.select_option("#examples", "basic")
    page.click("#run")
    page.wait_for_function("document.getElementById('console').textContent.includes('縮回到位') || "
                           "document.getElementById('console').textContent.includes('Error')", timeout=60000)
    check("伸出到位" in console() and "縮回到位" in console(), "基本範例：擋料器伸出到位、縮回到位")
    wait_done(10000)

    # 2. 在編輯器打字不會被 Unity 吃掉
    set_code("")
    page.click(".CodeMirror")
    page.keyboard.type("print('wasd ok')")
    typed = page.evaluate("document.querySelector('.CodeMirror').CodeMirror.getValue()")
    check(typed == "print('wasd ok')", f"編輯器打字正常（WASD 沒被場景攔截）：{typed!r}")
    page.keyboard.press("Control+Enter")
    page.wait_for_function("document.getElementById('console').textContent.includes('wasd ok')", timeout=20000)
    check(True, "Ctrl+Enter 執行")

    # 3. 整條線循環 30 秒後停止
    page.select_option("#examples", "demo")
    time.sleep(1)
    page.click("#run")
    time.sleep(30)
    text = console()
    moved = sum(text.count(k) for k in ("收料", "換層", "頂升加工", "放行", "送出"))
    check(moved >= 10, f"整條線循環：30 秒內步驟變化 {moved} 次")
    page.screenshot(path=str(HERE / "e2e_web_editor.png"))
    page.click("#stop")
    wait_done(8000)
    time.sleep(0.6)
    check("已停止" in console() or "已停止，所有輸出歸零" in console(), "停止：程式收到中斷")
    zero = page.evaluate("Array.from(window.DTPageBridge.ctrl).every(v => v === 0)")
    check(zero, "停止後所有控制輸出歸零")
    check(not errors, f"沒有頁面錯誤 {errors[:2]}")
    browser.close()

ok = all(r for r, _ in results)
print("ALL PASS" if ok else "有失敗項目")
sys.exit(0 if ok else 1)
