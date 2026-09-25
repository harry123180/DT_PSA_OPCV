"""部件對應測試：暫存器表的中文名稱、滑鼠移上去 3D 高亮、「定位」讓相機飛過去、3D 點選回報到暫存器表。

    python e2e_inspect.py <站台目錄>
    python e2e_inspect.py https://dt.qianpro.shop/
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
DEV = "MAIN.FG_Transport.Index03_Y_Stopper"


def check(cond, msg):
    results.append(bool(cond))
    print(("PASS " if cond else "FAIL ") + msg, flush=True)


def diff(a, b):
    x, y = Image.open(a).convert("L"), Image.open(b).convert("L")
    h = ImageChops.difference(x, y).histogram()
    return sum(i * n for i, n in enumerate(h)) / (x.size[0] * x.size[1])


def orange(path):
    """高亮色（橘）像素數：R 高、G 中、B 低。場景本身幾乎沒有這種顏色。"""
    img = Image.open(path).convert("RGB")
    return sum(1 for r, g, b in img.get_flattened_data() if r > 150 and r - g > 25 and g - b > 20 and r - b > 60)


def orange_center(path):
    img = Image.open(path).convert("RGB")
    w = img.size[0]
    pts = [(i % w, i // w) for i, (r, g, b) in enumerate(img.get_flattened_data())
           if r > 150 and r - g > 25 and g - b > 20 and r - b > 60]
    if not pts:
        return img.size[0] / 2, img.size[1] / 2
    xs, ys = sorted(p[0] for p in pts), sorted(p[1] for p in pts)
    return xs[len(xs) // 2], ys[len(ys) // 2]


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
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 8164), functools.partial(Handler, directory=target))
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    base = "http://127.0.0.1:8164/"

with sync_playwright() as p:
    browser = p.chromium.launch(channel="chrome", headless=True,
                                args=["--enable-unsafe-swiftshader", "--ignore-gpu-blocklist",
                                      "--host-resolver-rules=MAP dt.qianpro.shop 104.21.28.23"])
    ctx = browser.new_context(viewport={"width": 1600, "height": 900})
    errors = []
    main = ctx.new_page()
    main.on("pageerror", lambda e: errors.append("main: " + str(e)))
    unity_logs = []
    main.on("console", lambda m: unity_logs.append(m.text) if "[PythonLink]" in m.text else None)
    main.goto(base, wait_until="load", timeout=120000)
    main.wait_for_function("window.dtUnity && document.getElementById('status').textContent.includes('就緒')", timeout=180000)
    time.sleep(2)
    canvas = main.locator("#unity-canvas")

    reg = ctx.new_page()
    reg.on("pageerror", lambda e: errors.append("reg: " + str(e)))
    reg.goto(base + "registers.html", wait_until="load")
    reg.wait_for_function("document.getElementById('conn').textContent.includes('可手動切換')", timeout=30000)
    reg.wait_for_selector(f"section.dev[data-path='{DEV}']", timeout=20000)
    label = reg.inner_text(f"section.dev[data-path='{DEV}'] .label")
    check("分度站 3" in label and "擋料" in label, f"暫存器表顯示中文名稱：「{label}」")
    reg.click(f"section.dev[data-path='{DEV}'] .more")
    about = reg.inner_text(f"section.dev[data-path='{DEV}'] .about")
    check("功能" in about and "托盤" in about, "「說明」展開官方說明（中文）")
    reg.fill("#q", "擋料氣缸")
    reg.dispatch_event("#q", "input")
    visible = reg.evaluate("[...document.querySelectorAll('section.dev')].filter(s => s.style.display !== 'none').length")
    check(visible >= 5, f"中文搜尋「擋料氣缸」找到 {visible} 個裝置")
    reg.fill("#q", "")
    reg.dispatch_event("#q", "input")

    # 定位：相機飛到部件並高亮（橘色）
    before = HERE / "insp_0.png"; canvas.screenshot(path=str(before))
    reg.click(f"section.dev[data-path='{DEV}'] .focus")
    time.sleep(2.5)
    focused = HERE / "insp_1.png"; canvas.screenshot(path=str(focused))
    check(diff(before, focused) > 5, f"「定位」後相機飛到部件（差異 {diff(before, focused):.1f}）")
    lit = orange(focused)
    check(lit > 300, f"定位的部件被橘色高亮（{lit} 個橘色像素）")

    # 暫存器表 hover 另一個裝置 → 高亮換過去，原本的橘色消失
    reg.hover(f"section.dev[data-path='MAIN.FG_Transport.Lift01_Y_Lift'] .label")
    time.sleep(1.2)
    moved = HERE / "insp_2.png"; canvas.screenshot(path=str(moved))
    check(orange(moved) < lit * 0.3, f"hover 另一個裝置 → 原部件高亮移除（橘色 {lit} → {orange(moved)}）")
    main.evaluate(f"window.DTHighlight('highlight', '{DEV}')")   # 等同 hover 回來
    time.sleep(1.2)
    back = HERE / "insp_3.png"; canvas.screenshot(path=str(back))
    check(orange(back) > 300, f"hover 回來 → 再次高亮（{orange(back)} 個橘色像素）")

    # 3D 點選：定位出口感測器，在它亮橘色的位置點下去 → 說明卡＋暫存器表都指向它
    DEV2 = "MAIN.FG_Transport.Index04_B_Exit"
    reg.click(f"section.dev[data-path='{DEV2}'] .focus")
    time.sleep(2.5)
    spot = HERE / "insp_4.png"; canvas.screenshot(path=str(spot))
    cx, cy = orange_center(spot)
    main.evaluate("window.DTHighlight('clear', '')")
    time.sleep(0.5)
    box = canvas.bounding_box()
    main.mouse.click(box["x"] + cx, box["y"] + cy)
    time.sleep(1.5)
    card_visible = main.evaluate("!document.getElementById('pick-card').hidden")
    card_label = main.inner_text("#pick-card .label") if card_visible else ""
    card_path = main.inner_text("#pick-card .path") if card_visible else ""
    note = reg.inner_text("#picked-note")
    check(card_visible and card_path == DEV2, f"3D 點選橘色的零件 → 說明卡：{card_label}（{card_path}）")
    check(note.startswith("3D 點選") and "出口感測器" in note, f"3D 點選 → 暫存器表同步：{note}")
    picked_lit = HERE / "insp_5.png"; canvas.screenshot(path=str(picked_lit))
    check(orange(picked_lit) > 100, f"點選後零件高亮（{orange(picked_lit)} 個橘色像素）")
    main.evaluate("window.DTHighlight('highlight', 'Project/FG_Transport/Transport02')")   # 整段模組（場景路徑）
    time.sleep(1.2)
    module_lit = HERE / "insp_6.png"; canvas.screenshot(path=str(module_lit))
    check(orange(module_lit) > 3000, f"模組（主輸送帶 2）整段高亮（{orange(module_lit)} 個橘色像素）")
    main.screenshot(path=str(HERE / "e2e_inspect_main.png"))
    reg.screenshot(path=str(HERE / "e2e_inspect_reg.png"))
    for line in [l for l in unity_logs if 'map ' not in l][-8:]:
        print("   unity:", line[:160])
    check(not errors, f"沒有頁面錯誤 {errors[:2]}")
    browser.close()

print("ALL PASS" if all(results) else "有失敗項目")
sys.exit(0 if all(results) else 1)
