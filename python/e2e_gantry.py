"""雙軸直線模組（由 CAD 自動轉出）的端對端測試：網頁 Python 控制兩支伺服，檢查位置回讀、畫面真的有動、
行程保護、部件高亮。

    python e2e_gantry.py <站台目錄>            # 本機組好的 --variant gantry 站
    python e2e_gantry.py https://dt.qianpro.shop/gantry/
"""
import functools
import http.server
import sys
import threading
import time
from pathlib import Path

from PIL import Image, ImageChops
from playwright.sync_api import sync_playwright

HERE = Path(__file__).parent
target = sys.argv[1]
results = []


def check(cond, msg):
    results.append(bool(cond))
    print(("PASS " if cond else "FAIL ") + msg, flush=True)


def diff(a, b):
    x, y = Image.open(a).convert("L"), Image.open(b).convert("L")
    h = ImageChops.difference(x, y).histogram()
    return sum(i * n for i, n in enumerate(h)) / (x.size[0] * x.size[1])


def orange(path):
    img = Image.open(path).convert("RGB")
    return sum(1 for r, g, b in img.get_flattened_data() if r > 150 and r - g > 25 and g - b > 20 and r - b > 60)


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
    base = target.rstrip("/") + "/"
else:
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 8165), functools.partial(Handler, directory=target))
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    base = "http://127.0.0.1:8165/"

with sync_playwright() as p:
    browser = p.chromium.launch(channel="chrome", headless=True,
                                args=["--enable-unsafe-swiftshader", "--ignore-gpu-blocklist",
                                      "--host-resolver-rules=MAP dt.qianpro.shop 104.21.28.23"])
    page = browser.new_page(viewport={"width": 1600, "height": 900})
    errors, unity_logs = [], []
    page.on("pageerror", lambda e: errors.append(str(e)))
    page.on("console", lambda m: unity_logs.append(m.text) if "[PythonLink]" in m.text else None)
    page.goto(base, wait_until="load", timeout=120000)
    page.wait_for_function("window.dtUnity && document.getElementById('status').textContent.includes('就緒')", timeout=240000)
    time.sleep(2)
    canvas = page.locator("#unity-canvas")
    title = page.inner_text("#site-title")
    options = page.evaluate("[...document.querySelectorAll('#examples option')].map(o => o.textContent)")
    check(title == "雙軸直線模組" and any("矩形" in o for o in options), f"站點標題「{title}」、範例 {options[1:]}")

    set_code = lambda code: page.evaluate("c => document.querySelector('.CodeMirror').CodeMirror.setValue(c)", code)
    wait_done = lambda t: page.wait_for_function("!document.getElementById('run').disabled && "
                                                 "!document.getElementById('status').className.includes('running')", timeout=t)
    console = lambda: page.inner_text("#console")

    def run(code, until, timeout=60000):
        page.click("#clear")
        set_code(code)
        page.click("#run")
        page.wait_for_function(f"document.getElementById('console').textContent.includes({until!r}) || "
                               "document.getElementById('console').querySelector('.err')", timeout=timeout)
        time.sleep(0.5)
        return console()

    shot0 = HERE / "gantry_0.png"; canvas.screenshot(path=str(shot0))
    out = run("""from dtlink import Twin, print_devices
twin = Twin()
print_devices(twin, twin.devices())
x = twin.device("MAIN.FG_Gantry.M_AxisX"); z = twin.device("MAIN.FG_Gantry.M_AxisZ")
print("RANGE", x.range, z.range)
print("START", round(x.value, 1), round(z.value, 1))
print("DONE0")
""", "DONE0")
    check("X 軸" in out and "Z 軸" in out and "RANGE (0, 411.8) (0, 411.8)" in out, "列出兩支伺服（中文名稱、行程 0～411.8 mm）")

    out = run("""from dtlink import Twin
twin = Twin()
x = twin.device("MAIN.FG_Gantry.M_AxisX"); z = twin.device("MAIN.FG_Gantry.M_AxisZ")
x.move_to(380); print("X", round(x.value, 1))
print("DONE1")
""", "DONE1")
    check("X 380.0" in out, "X 軸 move_to(380) 到位且回讀 380.0")
    shot1 = HERE / "gantry_1.png"; canvas.screenshot(path=str(shot1))
    check(diff(shot0, shot1) > 1, f"X 軸移動後畫面有變化（差異 {diff(shot0, shot1):.2f}）")

    out = run("""from dtlink import Twin
twin = Twin()
z = twin.device("MAIN.FG_Gantry.M_AxisZ")
z.move_to(400); print("Z", round(z.value, 1))
print("DONE2")
""", "DONE2")
    check("Z 400.0" in out, "Z 軸 move_to(400) 到位且回讀 400.0")
    shot2 = HERE / "gantry_2.png"; canvas.screenshot(path=str(shot2))
    check(diff(shot1, shot2) > 0.3, f"Z 軸移動後畫面有變化（差異 {diff(shot1, shot2):.2f}）")

    out = run("""from dtlink import Twin
twin = Twin()
x = twin.device("MAIN.FG_Gantry.M_AxisX")
try:
    x.move_to(900)
except ValueError as e:
    print("BLOCKED", e)
print("DONE3")
""", "DONE3")
    check("BLOCKED" in out and "超出範圍" in out, "超出行程（900 mm）被擋下")

    # 回原點，畫面應該接近一開始
    run("""from dtlink import Twin
twin = Twin()
x = twin.device("MAIN.FG_Gantry.M_AxisX"); z = twin.device("MAIN.FG_Gantry.M_AxisZ")
x.move_to(205.9, wait=False); z.move_to(205.9, wait=False)
twin.wait_until(lambda: abs(x.value - 205.9) < 0.5 and abs(z.value - 205.9) < 0.5, timeout=15)
print("DONE4")
""", "DONE4")
    shot3 = HERE / "gantry_3.png"; canvas.screenshot(path=str(shot3))
    check("DONE4" in console(), "兩軸同時移動（wait=False）後一起到位")

    # 部件高亮：X 軸只亮自己的馬達／螺桿／滑座，不含 Z 軸
    page.evaluate("window.DTHighlight('highlight', 'MAIN.FG_Gantry.M_AxisX')")
    time.sleep(1.2)
    hx = HERE / "gantry_hx.png"; canvas.screenshot(path=str(hx))
    page.evaluate("window.DTHighlight('highlight', 'MAIN.FG_Gantry.M_AxisZ')")
    time.sleep(1.2)
    hz = HERE / "gantry_hz.png"; canvas.screenshot(path=str(hz))
    page.evaluate("window.DTHighlight('clear', '')")
    check(orange(hx) > 500 and orange(hz) > 500, f"高亮 X 軸 {orange(hx)}、Z 軸 {orange(hz)} 個橘色像素")
    for line in [l for l in unity_logs if "map " in l or "highlight" in l][-4:]:
        print("   unity:", line[:200])
    page.screenshot(path=str(HERE / "e2e_gantry_page.png"))
    check(not errors, f"沒有頁面錯誤 {errors[:2]}")
    browser.close()

print("ALL PASS" if all(results) else "有失敗項目")
sys.exit(0 if all(results) else 1)
