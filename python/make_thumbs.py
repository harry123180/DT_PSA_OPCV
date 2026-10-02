"""機台列表頁的縮圖：打開各站，等 3D 載入完成，截 3D 畫面裁掉工具列與狀態框，存成 web/gallery/img/<站>.jpg。

    python make_thumbs.py <組好的整站目錄> [機台…]   # web/assemble_all.py 的輸出；沒給機台就全部重拍
"""
import functools
import http.server
import io
import sys
import threading
import time
from pathlib import Path

from PIL import Image
from playwright.sync_api import sync_playwright

site = sys.argv[1]
OUT = Path(__file__).resolve().parent.parent / "web" / "gallery" / "img"


class Handler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {**http.server.SimpleHTTPRequestHandler.extensions_map,
                      ".wasm": "application/wasm", ".js": "application/javascript", ".mjs": "application/javascript"}

    def end_headers(self):
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        super().end_headers()

    def log_message(self, *a):
        pass


FILL = {"line"}                   # 填滿畫面的機台，不做置中構圖
UI = [(0.0, 0.0, 0.05, 1.0),      # 左側工具列
      (0.33, 0.0, 0.67, 0.07),    # 上方計時器
      (0.0, 0.9, 0.6, 1.0),       # 左下提示
      (0.6, 0.88, 1.0, 1.0)]      # 右下 Python 狀態框


def row_bg(img):
    """每一列的背景色（背景是上下漸層）：取該列最右邊的像素"""
    px = img.load()
    return [px[img.width - 3, y] for y in range(img.height)]


def mask_ui(img):
    """把 Unity 介面元素塗成背景色，免得被當成機台"""
    img = img.copy()
    px, bg = img.load(), row_bg(img)
    w, h = img.size
    for x0, y0, x1, y1 in UI:
        for y in range(int(y0 * h), int(y1 * h)):
            for x in range(int(x0 * w), int(x1 * w)):
                px[x, y] = bg[y]
    return img


def machine_box(img):
    """跟背景色差很多的像素範圍"""
    px, bg = img.load(), row_bg(img)
    w, h = img.size
    xs, ys = [], []
    for y in range(0, h, 2):
        c = bg[y]
        for x in range(0, w, 2):
            r, g, b = px[x, y]
            if abs(r - c[0]) + abs(g - c[1]) + abs(b - c[2]) > 60:
                xs.append(x); ys.append(y)
    return (min(xs), min(ys), max(xs), max(ys)) if xs else None


def touches_edge(img):
    """機台碰到畫面邊緣或介面元素（被切掉或被擋住）"""
    b = machine_box(img)
    if b is None:
        return False
    w, h = img.size
    m = 0.02 * min(w, h)
    if b[0] < m or b[1] < m or b[2] > w - m or b[3] > h - m:
        return True
    # 介面區塗掉的部分會讓範圍剛好停在介面區邊上：靠近（m 以內）也算被擋住
    return any(b[0] < x1 * w + m and b[2] > x0 * w - m and b[1] < y1 * h + m and b[3] > y0 * h - m for x0, y0, x1, y1 in UI)


def frame(img, pad=0.1, aspect=8 / 5):
    """機台置中、四周留白、8:5；超出畫面的部分用背景色補（背景是上下漸層，逐列補色）"""
    b = machine_box(img)
    if b is None:
        return img
    x0, y0, x1, y1 = b
    cw, ch = (x1 - x0) * (1 + 2 * pad), (y1 - y0) * (1 + 2 * pad)
    if cw / ch < aspect:
        cw = ch * aspect
    else:
        ch = cw / aspect
    left, top = round((x0 + x1) / 2 - cw / 2), round((y0 + y1) / 2 - ch / 2)
    out = Image.new("RGB", (round(cw), round(ch)))
    bg = row_bg(img)
    po = out.load()
    for y in range(out.height):
        c = bg[min(max(top + y, 0), img.height - 1)]
        for x in range(out.width):
            po[x, y] = c
    out.paste(img, (-left, -top))
    return out


httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 8170), functools.partial(Handler, directory=site))
threading.Thread(target=httpd.serve_forever, daemon=True).start()
with sync_playwright() as p:
    browser = p.chromium.launch(channel="chrome", headless=True, args=["--enable-unsafe-swiftshader", "--ignore-gpu-blocklist"])
    for name in sys.argv[2:] or ("line", "gantry", "xyz", "robot", "vmc", "vf2"):
        page = browser.new_page(viewport={"width": 1600, "height": 900})
        page.goto(f"http://127.0.0.1:8170/{name}/", wait_until="load", timeout=120000)
        page.wait_for_function("window.dtUnity && document.getElementById('status').textContent.includes('就緒')", timeout=240000)
        time.sleep(3)
        canvas = page.locator("#unity-canvas")
        box = canvas.bounding_box()
        page.mouse.click(box["x"] + box["width"] * 0.12, box["y"] + box["height"] * 0.15)   # 空白處點一下：3D 畫面取得焦點，滾輪才有作用
        page.mouse.move(box["x"] + box["width"] / 2, box["y"] + box["height"] / 2)
        if name in FILL:
            # 產線本來就填滿畫面：直接裁掉介面元素的邊
            img = Image.open(io.BytesIO(canvas.screenshot())).convert("RGB")
            w, h = img.size
            img = img.crop((int(w * 0.06), int(h * 0.08), int(w * 0.97), int(h * 0.86)))
        else:
            for _ in range(10):
                img = mask_ui(Image.open(io.BytesIO(canvas.screenshot())).convert("RGB"))
                if not touches_edge(img):
                    break
                page.mouse.wheel(0, 360)      # 機台碰到邊或介面：拉遠一點再截
                time.sleep(1.5)
            print("  ", name, "拉遠", _, "次")
            img = frame(img)
        tw, th = 800, 500
        scale = max(tw / img.width, th / img.height)
        img = img.resize((round(img.width * scale), round(img.height * scale)), Image.LANCZOS)
        left, top = (img.width - tw) // 2, (img.height - th) // 2
        img.crop((left, top, left + tw, top + th)).save(OUT / f"{name}.jpg", quality=82)
        print(name, (OUT / f"{name}.jpg").stat().st_size // 1024, "KB")
        page.close()
    browser.close()
