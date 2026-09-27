"""六軸並聯機器人（CAD 自動轉出）端對端測試：Python 逆向運動學送滑座位置 → 孿生正向運動學回報平台姿態，兩者要一致；
畫面要真的動；行程保護；單支腳推動時平台會傾斜；高亮。

    python e2e_robot.py <站台目錄>
    python e2e_robot.py https://dt.qianpro.shop/robot/
"""
import functools
import http.server
import re
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
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 8166), functools.partial(Handler, directory=target))
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    base = "http://127.0.0.1:8166/"

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
    check(page.inner_text("#site-title") == "六軸並聯機器人", "站點標題")

    def run(code, timeout=120000):
        page.click("#clear")
        page.evaluate("c => document.querySelector('.CodeMirror').CodeMirror.setValue(c)", code)
        page.click("#run")
        page.wait_for_function("/完成|已結束/.test(document.getElementById('status').textContent)", timeout=timeout)
        time.sleep(0.3)
        bad = page.evaluate("!!document.getElementById('console').querySelector('.err')")
        return page.inner_text("#console"), bad

    shot0 = HERE / "robot_0.png"; canvas.screenshot(path=str(shot0))
    out, bad = run("""from dtlink import Twin
from hexapod import Hexapod
robot = Hexapod(Twin())
print("POSE0", robot.pose())
print("LEGS0", robot.legs_mm())
""")
    check(not bad and "POSE0" in out, "開場姿態：" + next((l for l in out.splitlines() if l.startswith("POSE0")), out[-200:]))

    # 逆向 vs 正向：每個姿態誤差要很小（位置 mm、角度 度）
    out, bad = run("""from dtlink import Twin
from hexapod import Hexapod
robot = Hexapod(Twin())
for goal in [dict(z=12), dict(x=12), dict(y=-10), dict(roll=6), dict(pitch=-6), dict(yaw=12), dict(x=5, y=5, z=-8, yaw=-5)]:
    robot.move(**goal)
    got = robot.pose()
    err = max(abs(got[k] - goal.get(k, 0)) for k in got)
    print("CHK", goal, got, "ERR", round(err, 3))
""", timeout=180000)
    errs = [float(m) for m in re.findall(r"ERR ([\d.]+)", out)]
    for line in out.splitlines():
        if line.startswith("CHK"):
            print("   ", line[:170])
    check(not bad and len(errs) == 7 and max(errs) < 0.05, f"逆向 vs 正向運動學 7 個姿態，最大誤差 {max(errs) if errs else '無'}")

    run("""from dtlink import Twin
from hexapod import Hexapod
Hexapod(Twin()).move(z=15, roll=8)
""")
    shot1 = HERE / "robot_1.png"; canvas.screenshot(path=str(shot1))
    check(diff(shot0, shot1) > 0.5, f"平台上升並傾斜後畫面有變化（差異 {diff(shot0, shot1):.2f}）")

    out, bad = run("""from dtlink import Twin
from hexapod import Hexapod
robot = Hexapod(Twin())
try:
    robot.move(z=60)
except ValueError as e:
    print("BLOCKED", e)
""")
    check("BLOCKED" in out, "到不了的姿態（z=60）在動之前就被擋下：" + next((l for l in out.splitlines() if "BLOCKED" in l), "")[:80])

    # 只推一支腳：平台應該傾斜（roll 或 pitch 不為 0）
    out, bad = run("""from dtlink import Twin
from hexapod import Hexapod
twin = Twin(); robot = Hexapod(twin)
robot.home()
twin.device("MAIN.FG_Robot.M_Leg1").move_to(30)
print("ONE", robot.settle())
""")
    m = re.search(r"'roll': (-?[\d.]+), 'pitch': (-?[\d.]+)", out)
    tilt = max(abs(float(m.group(1))), abs(float(m.group(2)))) if m else 0
    check(tilt > 1, f"只推腳 1 到 30 mm，平台傾斜 {tilt:.1f} 度（正向運動學）")
    shot2 = HERE / "robot_2.png"; canvas.screenshot(path=str(shot2))

    page.evaluate("window.DTHighlight('highlight', 'MAIN.FG_Robot.M_Leg1')")
    time.sleep(1.2)
    hl = HERE / "robot_hl.png"; canvas.screenshot(path=str(hl))
    page.evaluate("window.DTHighlight('clear', '')")
    check(orange(hl) > 200, f"高亮腳 1（{orange(hl)} 個橘色像素）")
    run("""from dtlink import Twin
from hexapod import Hexapod
Hexapod(Twin()).home()
""")
    warn = [l for l in unity_logs if "到不了" in l]
    check(not warn, f"孿生端沒有解不出的姿態 {warn[:1]}")
    check(not errors, f"沒有頁面錯誤 {errors[:2]}")
    page.screenshot(path=str(HERE / "e2e_robot_page.png"))
    browser.close()

print("ALL PASS" if all(results) else "有失敗項目")
sys.exit(0 if all(results) else 1)
