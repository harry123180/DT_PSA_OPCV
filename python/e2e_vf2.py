"""Haas VF-2（Fusion 機台庫模型）的端對端測試：裝置與行程、三軸移動到極限、手動換刀、切削體積、
警報 14（主軸轉著換刀）與清除、G-code 範例零件（跟加工中心同一支）、部件高亮、沒有頁面錯誤。

    python e2e_vf2.py <站台目錄>            # 本機組好的 --variant vf2 站
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
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 8173), functools.partial(Handler, directory=target))
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    base = "http://127.0.0.1:8173/"

HEAD = "from dtlink import Twin\nfrom cnc import VMC, AlarmError, TOOLS\nm = VMC(Twin())\n"

with sync_playwright() as p:
    browser = p.chromium.launch(channel="chrome", headless=True,
                                args=["--enable-unsafe-swiftshader", "--ignore-gpu-blocklist",
                                      "--host-resolver-rules=MAP dt.qianpro.shop 104.21.28.23"])
    page = browser.new_page(viewport={"width": 1600, "height": 900})
    errors, unity_logs = [], []
    page.on("pageerror", lambda e: errors.append(str(e)))
    page.on("console", lambda m: unity_logs.append(m.text) if ("[VMC]" in m.text or "[PythonLink]" in m.text) else None)
    page.goto(base, wait_until="load", timeout=120000)
    page.wait_for_function("window.dtUnity && document.getElementById('status').textContent.includes('就緒')", timeout=240000)
    time.sleep(2)
    canvas = page.locator("#unity-canvas")
    title = page.inner_text("#site-title")
    options = page.evaluate("[...document.querySelectorAll('#examples option')].map(o => o.textContent)")
    check("VF-2" in title and any("G-code" in o for o in options), f"站點標題「{title}」、範例 {options[1:]}")
    set_code = lambda code: page.evaluate("c => document.querySelector('.CodeMirror').CodeMirror.setValue(c)", code)
    console = lambda: page.inner_text("#console")

    def run(code, until, timeout=90000):
        page.click("#clear")
        set_code(code)
        page.click("#run")
        page.wait_for_function(f"document.getElementById('console').textContent.includes({until!r}) || "
                               "document.getElementById('console').querySelector('.err')", timeout=timeout)
        time.sleep(0.5)
        return console()

    def num(out, key):
        mt = re.search(rf"{key}\s*(-?[\d.]+)", out)
        return float(mt.group(1)) if mt else None

    out = run(HEAD + """print("START")
print("ROOT", m.root, "CHANGER", m.changer)
print("DEVS", len(m.twin.devices("FG_VF2")))
print("TOOL", m.tool)
print("DONE1")
""", "DONE1")
    check("ROOT MAIN.FG_VF2 CHANGER manual" in out and num(out, "DEVS") == 10 and num(out, "TOOL") == 1,
          "cnc.py 自動認出 VF-2（手動換刀）、10 個裝置、開機主軸上 T1：" + " / ".join(l for l in out.splitlines() if l[:4] in ("ROOT", "DEVS", "TOOL")))

    shot0 = HERE / "vf2_0.png"; canvas.screenshot(path=str(shot0))
    out = run(HEAD + """m.move_machine(x=381, y=-203)
m.move_machine(z=-300)
print("POS", m.machine_pos())
print("DONE2")
""", "DONE2")
    pos = re.search(r"'x': (-?[\d.]+), 'y': (-?[\d.]+), 'z': (-?[\d.]+)", out)
    check(pos and all(abs(float(v) - t) < 0.05 for v, t in zip(pos.groups(), (381, -203, -300))), "三軸到行程極限 (381, -203, -300)：" + (pos.group(0) if pos else out[-200:]))
    shot1 = HERE / "vf2_1.png"; canvas.screenshot(path=str(shot1))
    check(diff(shot0, shot1) > 0.3, f"三軸移動後畫面有變化（差異 {diff(shot0, shot1):.2f}）")
    out = run(HEAD + "try:\n    m.move_machine(x=400)\nexcept ValueError as e:\n    print('BLOCKED', e)\nm.home()\nprint('DONE2b')\n", "DONE2b")
    check("BLOCKED" in out and "381" in out, "X=400 超出行程被擋下")

    out = run(HEAD + """for t in (5, 8, 1):
    m.tool_change(t)
    print("NOW", m.tool)
print("DONE3")
""", "DONE3", timeout=120000)
    nows = [int(v) for v in re.findall(r"NOW (\d+)", out)]
    check(nows == [5, 8, 1] and len([l for l in unity_logs if "手動換刀" in l]) >= 3, f"手動換刀 T5 → T8 → T1，B_Tool 回報 {nows}")

    out = run(HEAD + """m.new_stock()
m.speedup = 2
m.spindle(5000)
m.rapid(x=-90, y=0, z=5)
m.feed(z=-2, f=300)
m.feed(x=90, f=1200)
m.rapid(z=20)
m.spindle(0)
print("VOL", m.cut_volume)
print("A", m.alarm)
print("DONE4")
""", "DONE4")
    vol = num(out, "VOL")
    check(vol is not None and 2.6 < vol < 3.8 and num(out, "A") == 0, f"切削體積 {vol} cm³（理論 3.2），沒有警報")
    shot_cut = HERE / "vf2_cut.png"; canvas.screenshot(path=str(shot_cut))

    out = run(HEAD + """m.spindle(3000)
m.tool_select.target = 4
m.twin.sleep(0.6)
print("ALARM", m.alarm, "TOOL", m.tool)
m.spindle(0)
m.reset_alarm()
m.twin.wait_until(lambda: m.tool == 4, timeout=3)
print("AFTER", m.alarm, "TOOL2", m.tool)
m.tool_change(1)
print("DONE5")
""", "DONE5")
    check("ALARM 14 TOOL 1" in out and "AFTER 0 TOOL2 4" in out, "主軸轉著換刀 → 警報 14、不換；停主軸、清警報後換上 T4")

    page.select_option("#examples", label=next(o for o in options if "G-code" in o))
    code = page.evaluate("document.querySelector('.CodeMirror').CodeMirror.getValue()").replace("m.speedup = 3", "m.speedup = 6")
    t0 = time.time()
    out = run(code, "切削體積 ", timeout=400000)
    final = re.search(r"主軸上的刀：T(\d+)，切削體積 ([\d.]+)", out)
    check(final and final.group(1) == "2" and float(final.group(2)) > 25 and "（手動）" in out,
          f"G-code 範例跑完（{time.time() - t0:.0f} s）：" + (final.group(0) if final else out[-300:]))
    shot_part = HERE / "vf2_part.png"; canvas.screenshot(path=str(shot_part))

    page.evaluate("window.DTHighlight('highlight', 'MAIN.FG_VF2.M_AxisX')")
    time.sleep(1.2)
    hx = HERE / "vf2_hx.png"; canvas.screenshot(path=str(hx))
    page.evaluate("window.DTHighlight('highlight', 'MAIN.FG_VF2.M_Spindle')")
    time.sleep(1.2)
    hs = HERE / "vf2_hs.png"; canvas.screenshot(path=str(hs))
    page.evaluate("window.DTHighlight('clear', '')")
    check(orange(hx) > 500 and orange(hs) > 50, f"高亮 X 軸（工作台）{orange(hx)}、主軸 {orange(hs)} 個橘色像素")

    page.screenshot(path=str(HERE / "e2e_vf2_page.png"))
    bad = [l for l in unity_logs if "Exception" in l or "NullReference" in l]
    check(not errors and not bad, f"沒有頁面錯誤 {errors[:2]} {bad[:2]}")
    browser.close()

print("ALL PASS" if all(results) else "有失敗項目")
sys.exit(0 if all(results) else 1)
