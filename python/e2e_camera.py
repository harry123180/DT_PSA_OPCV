"""檢查網頁孿生的視角操作：右鍵拖曳旋轉、滾輪縮放、中鍵平移，比對前後畫面差異。

    python e2e_camera.py [網址]
"""
import sys
import time
from pathlib import Path

from PIL import Image, ImageChops
from playwright.sync_api import sync_playwright

HERE = Path(__file__).parent
URL = sys.argv[1] if len(sys.argv) > 1 else "https://dt.qianpro.shop/?ws=ws://127.0.0.1:9999"


def diff(a: Path, b: Path) -> float:
    x, y = Image.open(a).convert("L"), Image.open(b).convert("L")
    h = ImageChops.difference(x, y).histogram()
    return sum(i * n for i, n in enumerate(h)) / (x.size[0] * x.size[1])


with sync_playwright() as p:
    browser = p.chromium.launch(channel="chrome", headless=True,
                                args=["--enable-unsafe-swiftshader", "--ignore-gpu-blocklist",
                                      "--host-resolver-rules=MAP dt.qianpro.shop 104.21.28.23", "--disable-features=LocalNetworkAccessChecks"])
    page = browser.new_page(viewport={"width": 1600, "height": 900})
    logs = []
    page.on("console", lambda m: logs.append(m.text))
    page.goto(URL, wait_until="load", timeout=120000)
    canvas = page.locator("#unity-canvas")
    canvas.wait_for(timeout=60000)
    time.sleep(12)
    box = canvas.bounding_box()
    cx, cy = box["x"] + box["width"] / 2, box["y"] + box["height"] / 2

    def shot(name):
        path = HERE / f"cam_{name}.png"
        canvas.screenshot(path=str(path))
        return path

    base = shot("0_base")
    page.mouse.click(cx, cy)            # 先點一下讓畫布取得焦點
    time.sleep(0.5)
    page.mouse.move(cx, cy)
    page.mouse.down(button="right")
    for i in range(1, 21):
        page.mouse.move(cx + i * 12, cy + i * 3)
        time.sleep(0.03)
    page.mouse.up(button="right")
    time.sleep(0.8)
    rot = shot("1_rotate")

    page.mouse.move(cx, cy)
    for _ in range(6):
        page.mouse.wheel(0, -240)
        time.sleep(0.08)
    time.sleep(0.8)
    zoom = shot("2_zoom")

    page.mouse.move(cx, cy)
    page.mouse.down(button="middle")
    for i in range(1, 21):
        page.mouse.move(cx - i * 10, cy)
        time.sleep(0.03)
    page.mouse.up(button="middle")
    time.sleep(0.8)
    pan = shot("3_pan")

    # 觸控板組合：Ctrl＋左鍵拖曳旋轉、Shift＋左鍵拖曳平移
    def modifier_drag(key, dx, dy):
        page.mouse.move(cx, cy)
        page.keyboard.down(key)
        page.mouse.down(button="left")
        for i in range(1, 21):
            page.mouse.move(cx + i * dx, cy + i * dy)
            time.sleep(0.03)
        page.mouse.up(button="left")
        page.keyboard.up(key)
        time.sleep(0.8)

    modifier_drag("Control", 12, 2)
    t_rot = shot("4_ctrl_rotate")
    modifier_drag("Shift", -10, 0)
    t_pan = shot("5_shift_pan")
    browser.close()

print(f"右鍵旋轉 差異 {diff(base, rot):.2f}")
print(f"滾輪縮放 差異 {diff(rot, zoom):.2f}")
print(f"中鍵平移 差異 {diff(zoom, pan):.2f}")
print(f"觸控板 Ctrl＋左鍵旋轉 差異 {diff(pan, t_rot):.2f}")
print(f"觸控板 Shift＋左鍵平移 差異 {diff(t_rot, t_pan):.2f}")
print("（差異 < 1 幾乎等於沒動）")
for line in logs:
    if "rror" in line or "xception" in line:
        print("console:", line[:200])
