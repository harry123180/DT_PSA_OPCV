"""三軸龍門（雙 Y 同步＋X＋Z，由 CAD 自動轉出）的端對端測試：三支伺服位置回讀、畫面真的有動、
行程保護、Y 同步（兩支一起亮、一起動）、直線插補範例。

    python e2e_gantry.py <站台目錄>            # 本機組好的 --variant gantry 站
    python e2e_gantry.py https://dt.qianpro.shop/xyz/
"""
import functools
import re
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
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 8169), functools.partial(Handler, directory=target))
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    base = "http://127.0.0.1:8169/"

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
    check(title == "三軸龍門" and any("取放" in o for o in options), f"站點標題「{title}」、範例 {options[1:]}")

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

    shot0 = HERE / "xyz_0.png"; canvas.screenshot(path=str(shot0))
    out = run("""from dtlink import Twin, print_devices
twin = Twin()
print_devices(twin, twin.devices())
for a in "XYZ":
    d = twin.device(f"MAIN.FG_Gantry3.M_Axis{a}")
    print("RANGE", a, d.range)
print("DONE0")
""", "DONE0")
    check(out.count("RANGE") == 3 and "(0, 98)" in out and "雙馬達同步" in out, "列出三支伺服（中文名稱、行程 0～98 mm）")

    shots = {}
    for i, (axis, pos) in enumerate([("Y", 90), ("X", 85), ("Z", 90)], 1):
        out = run(f"""from dtlink import Twin
twin = Twin()
d = twin.device("MAIN.FG_Gantry3.M_Axis{axis}")
d.move_to({pos}); print("{axis}", round(d.value, 1))
print("DONE{i}")
""", f"DONE{i}")
        check(f"{axis} {pos}.0" in out, f"{axis} 軸 move_to({pos}) 到位且回讀 {pos}.0")
        shots[axis] = HERE / f"xyz_{i}.png"; canvas.screenshot(path=str(shots[axis]))
    prev = shot0
    for axis in "YXZ":
        dv = diff(prev, shots[axis]); prev = shots[axis]
        check(dv > 0.3, f"{axis} 軸移動後畫面有變化（差異 {dv:.2f}）")

    out = run("""from dtlink import Twin
from xyz import Gantry
g = Gantry(Twin())
try:
    g.move(50, 120, 50)
except ValueError as e:
    print("BLOCKED", e)
g.move(20, 30, 40)
print("POS", g.position())
print("DONE5")
""", "DONE5")
    check("BLOCKED" in out and "Y 軸" in out, "直線插補超出行程（Y=120）被擋下")
    pos = re.search(r"'x': (-?[\d.]+), 'y': (-?[\d.]+), 'z': (-?[\d.]+)", out)
    ok = pos and all(abs(float(v) - t) < 0.1 for v, t in zip(pos.groups(), (20, 30, 40)))
    check(ok, "Gantry.move(20, 30, 40) 三軸同時到位：" + next((l for l in out.splitlines() if l.startswith("POS")), out[-160:]))

    page.evaluate("window.DTHighlight('highlight', 'MAIN.FG_Gantry3.M_AxisY')")
    time.sleep(1.2)
    hy = HERE / "xyz_hy.png"; canvas.screenshot(path=str(hy))
    page.evaluate("window.DTHighlight('highlight', 'MAIN.FG_Gantry3.M_AxisX')")
    time.sleep(1.2)
    hx = HERE / "xyz_hx.png"; canvas.screenshot(path=str(hx))
    page.evaluate("window.DTHighlight('clear', '')")
    check(orange(hy) > 1.5 * orange(hx) > 300, f"高亮 Y 軸（兩支）{orange(hy)}、X 軸 {orange(hx)} 個橘色像素")
    for line in [l for l in unity_logs if "map " in l or "highlight" in l][-5:]:
        print("   unity:", line[:200])
    page.screenshot(path=str(HERE / "e2e_xyz_page.png"))
    check(not errors, f"沒有頁面錯誤 {errors[:2]}")
    browser.close()

print("ALL PASS" if all(results) else "有失敗項目")
sys.exit(0 if all(results) else 1)
