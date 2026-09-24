"""用 Playwright 開網頁孿生，同時執行 e2e_check.py，驗證「網頁 ↔ 本機 Python」整條路。

    python e2e_browser.py                         # 測本機 Build/WebGL（自動開靜態伺服器）
    python e2e_browser.py https://dt.qianpro.shop # 測正式網址
"""
import functools
import http.server
import subprocess
import sys
import threading
import time
from pathlib import Path

from playwright.sync_api import sync_playwright

HERE = Path(__file__).parent
BUILD = HERE.parent / "Build" / "WebGL"
PORT_HTTP = 8160
PORT_WS = 8765


def serve_local() -> str:
    http.server.SimpleHTTPRequestHandler.extensions_map.update({".wasm": "application/wasm", ".js": "application/javascript"})
    handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory=str(BUILD))
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", PORT_HTTP), handler)
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    return f"http://127.0.0.1:{PORT_HTTP}/"


def main():
    url = sys.argv[1] if len(sys.argv) > 1 else serve_local()
    check = subprocess.Popen([sys.executable, str(HERE / "e2e_check.py"), str(PORT_WS)],
                             cwd=HERE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8")
    logs: list[str] = []
    with sync_playwright() as p:
        browser = p.chromium.launch(
            channel="chrome", headless=True,
            args=["--enable-unsafe-swiftshader", "--ignore-gpu-blocklist",
                  "--disable-features=LocalNetworkAccessChecks,PrivateNetworkAccessRespectPreflightResults"],
        )
        page = browser.new_page(viewport={"width": 1600, "height": 900})
        page.on("console", lambda m: logs.append(f"[{m.type}] {m.text}"))
        page.on("pageerror", lambda e: logs.append(f"[pageerror] {e}"))
        t0 = time.time()
        page.goto(url, wait_until="load", timeout=120000)
        try:
            out, _ = check.communicate(timeout=300)
        except subprocess.TimeoutExpired:
            check.kill()
            out, _ = check.communicate()
        page.wait_for_timeout(1500)
        shot = HERE / "e2e_screenshot.png"
        page.screenshot(path=str(shot))
        browser.close()

    print("── e2e_check.py ──")
    print(out)
    print("── 瀏覽器 console（PythonLink／錯誤）──")
    for line in logs:
        if "PythonLink" in line or "error" in line.lower() or "exception" in line.lower():
            print(line[:300])
    print(f"耗時 {time.time() - t0:.0f} 秒，截圖 {shot}")
    sys.exit(check.returncode)


if __name__ == "__main__":
    main()
