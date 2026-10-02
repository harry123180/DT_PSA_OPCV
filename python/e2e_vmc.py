"""立式加工中心（LinuxCNC VMC 機構）的端對端測試：三軸移動、自動換刀（刀號交接）、切削體積、
警報（沒轉就下刀、沒接住就鬆刀）與清除、G-code 範例零件、部件高亮、沒有頁面錯誤。

    python e2e_vmc.py <站台目錄>            # 本機組好的 --variant vmc 站
    python e2e_vmc.py https://dt.qianpro.shop/vmc/
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
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 8171), functools.partial(Handler, directory=target))
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    base = "http://127.0.0.1:8171/"

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
    check(title.startswith("立式加工中心") and any("G-code" in o for o in options) and any("換刀" in o for o in options),
          f"站點標題「{title}」、範例 {options[1:]}")

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

    # 1. 裝置清單
    out = run("""from dtlink import Twin, print_devices
twin = Twin()
ds = twin.devices()
print_devices(twin, ds)
print("COUNT", len(ds))
print("TOOL", twin.read("MAIN.FG_VMC.B_Tool.StatusData"))
print("DONE1")
""", "DONE1")
    names = sorted(set(re.findall(r"MAIN\.FG_VMC\.(\w+)", out)))
    check(len(names) == 13 and "換刀臂" in out and num(out, "TOOL") == 1, f"13 個裝置 {names}、中文名稱、開機主軸上是 T1（{num(out, 'TOOL')}）")
    if num(out, "COUNT") != 13:
        print("   裝置清單：", out.replace(chr(10), " | ")[:600])

    # 2. 三軸移動
    shot0 = HERE / "vmc_0.png"; canvas.screenshot(path=str(shot0))
    out = run(HEAD + """m.move_machine(x=200)
m.move_machine(y=-80)
m.move_machine(z=-200)
print("POS", m.machine_pos())
print("DONE2")
""", "DONE2")
    pos = re.search(r"'x': (-?[\d.]+), 'y': (-?[\d.]+), 'z': (-?[\d.]+)", out)
    check(pos and all(abs(float(v) - t) < 0.05 for v, t in zip(pos.groups(), (200, -80, -200))), "三軸到位 (200, -80, -200)：" + (pos.group(0) if pos else out[-200:]))
    shot1 = HERE / "vmc_1.png"; canvas.screenshot(path=str(shot1))
    dv = diff(shot0, shot1)
    check(dv > 0.3, f"三軸移動後畫面有變化（差異 {dv:.2f}）")
    run(HEAD + "m.home()\nprint('DONE2b')\n", "DONE2b")

    # 3. 自動換刀
    out = run(HEAD + """for t in (5, 3, 1):
    m.tool_change(t)
    print("NOW", m.tool)
print("DONE3")
""", "DONE3", timeout=180000)
    nows = [int(float(v)) for v in re.findall(r"NOW (\d+)", out)]
    check(nows == [5, 3, 1], f"換刀 T5 → T3 → T1，B_Tool 依序回報 {nows}")
    handovers = [l for l in unity_logs if "放回刀位" in l or "主軸夾住" in l]
    check(len(handovers) >= 6, f"孿生記錄刀具交接 {len(handovers)} 次：{[h[h.find('[VMC]'):][:30] for h in handovers[-3:]]}")

    # 換刀臂擺進時的截圖：刀庫在主軸下
    out = run(HEAD + """m.move_machine(z=-100)
m.arm.extend()
m.twin.wait_until(lambda: m.arm.extended, timeout=6)
print("DONE3b")
""", "DONE3b")
    shot_arm = HERE / "vmc_arm.png"; canvas.screenshot(path=str(shot_arm))
    check(diff(shot0, shot_arm) > 0.3, f"換刀臂擺進畫面有變化（差異 {diff(shot0, shot_arm):.2f}）")
    out = run(HEAD + "m.arm.retract()\nm.twin.wait_until(lambda: m.arm.retracted, timeout=6)\nm.move_machine(z=0)\nprint('A', m.alarm)\nprint('DONE3c')\n", "DONE3c")
    check(num(out, "A") == 0, "主軸有刀、Z 在換刀高度時擺動換刀臂沒有警報")

    # 4. 切削：Ø10 銑一條 160 mm 長、2 mm 深的溝
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
    shot_cut = HERE / "vmc_cut.png"; canvas.screenshot(path=str(shot_cut))

    # 5. 警報：主軸沒轉就下刀
    out = run(HEAD + """m.rapid(x=0, y=30, z=3)
try:
    m.feed(z=-2, f=100)
    print("NOALARM")
except AlarmError as e:
    print("ALARM", m.alarm, e)
m.reset_alarm()
print("AFTER", m.alarm)
m.rapid(z=30)
print("DONE5")
""", "DONE5")
    check("ALARM 10" in out and num(out, "AFTER") == 0, "主軸沒轉就下刀 → 警報 10，reset_alarm() 後清除")

    # 6. 警報：沒有接住就鬆刀 → 刀掉落；清除後刀回刀庫、可以再換上
    out = run(HEAD + """m.move_machine(z=0)
m.release.extend()
m.twin.wait_until(lambda: m.release.extended, timeout=3)
m.twin.sleep(0.3)
print("ALARM", m.alarm, "TOOL", m.tool)
m.release.retract()
m.twin.wait_until(lambda: m.release.retracted, timeout=3)
m.reset_alarm()
print("AFTER", m.alarm)
m.tool_change(1)
print("TOOL2", m.tool)
print("DONE6")
""", "DONE6", timeout=120000)
    check("ALARM 4 TOOL 0" in out and num(out, "AFTER") == 0 and num(out, "TOOL2") == 1,
          "Z=0 鬆刀 → 警報 4、刀掉落；清除後刀回刀庫，重新換上 T1：" + " / ".join(l for l in out.splitlines() if l[:5] in ("ALARM", "AFTER", "TOOL2")))

    # 7. G-code 範例零件（預設範例，加速 6 倍）
    page.select_option("#examples", label=next(o for o in options if "G-code" in o))
    code = page.evaluate("document.querySelector('.CodeMirror').CodeMirror.getValue()").replace("m.speedup = 3", "m.speedup = 6")
    t0 = time.time()
    out = run(code, "切削體積 ", timeout=400000)
    gvol = re.search(r"切削體積 ([\d.]+) cm³（|切削體積 ([\d.]+) cm", out)
    final = re.search(r"主軸上的刀：T(\d+)，切削體積 ([\d.]+)", out)
    check(final and final.group(1) == "2" and float(final.group(2)) > 25 and "換刀 T" in out and ".err" not in out,
          f"G-code 範例跑完（{time.time() - t0:.0f} s）：" + (final.group(0) if final else out[-300:]))
    shot_part = HERE / "vmc_part.png"; canvas.screenshot(path=str(shot_part))

    # 8. 部件高亮
    page.evaluate("window.DTHighlight('highlight', 'MAIN.FG_VMC.M_AxisX')")
    time.sleep(1.2)
    hx = HERE / "vmc_hx.png"; canvas.screenshot(path=str(hx))
    page.evaluate("window.DTHighlight('highlight', 'MAIN.FG_VMC.Y_CarouselLock')")
    time.sleep(1.2)
    hl = HERE / "vmc_hl.png"; canvas.screenshot(path=str(hl))
    page.evaluate("window.DTHighlight('clear', '')")
    check(orange(hx) > 2 * orange(hl) and orange(hx) > 500, f"高亮 X 軸（工作台）{orange(hx)}、鎖銷 {orange(hl)} 個橘色像素")

    page.screenshot(path=str(HERE / "e2e_vmc_page.png"))
    bad = [l for l in unity_logs if "Exception" in l or "NullReference" in l]
    check(not errors and not bad, f"沒有頁面錯誤 {errors[:2]} {bad[:2]}")
    browser.close()

print("ALL PASS" if all(results) else "有失敗項目")
sys.exit(0 if all(results) else 1)
