"""機台列表頁：四張卡片、縮圖載得到、篩選、點「進入」進得去機台而且 3D 載得起來、機台頁的「所有機台」回得來、
舊網址（?ws=、/registers.html）轉到產線、手機寬度沒有水平捲動。

    python e2e_gallery.py https://dt.qianpro.shop/
"""
import sys
import time

from playwright.sync_api import sync_playwright

base = sys.argv[1].rstrip("/") + "/"
results = []


def check(cond, msg):
    results.append(bool(cond))
    print(("PASS " if cond else "FAIL ") + msg, flush=True)


with sync_playwright() as p:
    b = p.chromium.launch(channel="chrome", headless=True, args=["--enable-unsafe-swiftshader", "--ignore-gpu-blocklist",
                                                                   "--host-resolver-rules=MAP dt.qianpro.shop 104.21.28.23"])
    pg = b.new_page(viewport={"width": 1366, "height": 900})
    errs = []
    pg.on("pageerror", lambda e: errs.append(str(e)))
    pg.goto(base, wait_until="networkidle")
    titles = pg.evaluate("[...document.querySelectorAll('.card h2')].map(h => h.textContent)")
    check(len(titles) == 4, f"四張機台卡片：{titles}")
    loaded = pg.evaluate("[...document.querySelectorAll('.card img')].map(i => i.complete && i.naturalWidth > 0)")
    check(all(loaded) and len(loaded) == 4, f"縮圖都載得到 {loaded}")
    pg.click("button[data-f=sync]")
    shown = pg.evaluate("[...document.querySelectorAll('.card')].filter(c => !c.hidden).map(c => c.querySelector('h2').textContent)")
    check(shown == ["三軸龍門"], f"篩選「同步軸」只剩 {shown}")
    pg.click("button[data-f=all]")
    pg.screenshot(path="e2e_gallery_desktop.png", full_page=True)

    pg.click(".card:nth-child(4) .btn.primary")
    pg.wait_for_url("**/robot/", timeout=20000)
    pg.wait_for_function("window.dtUnity && document.getElementById('status').textContent.includes('就緒')", timeout=240000)
    check(pg.inner_text("#site-title") == "六軸並聯機器人", "點「進入」→ 機器人頁，3D 與 Python 都就緒")
    pg.click("#other-site")
    pg.wait_for_url(base, timeout=20000)
    check(pg.locator(".card").count() == 4, "機台頁的「所有機台」回到列表")

    pg.goto(base + "?ws=ws://127.0.0.1:8765")
    pg.wait_for_url(lambda u: "/line/?ws=" in u, wait_until="commit", timeout=15000)   # 產線頁要載 Unity，不等 load
    check("/line/?ws=" in pg.url, f"舊的本機模式網址轉到產線：{pg.url}")
    pg.goto(base + "registers.html", wait_until="commit")
    check(pg.url.rstrip("/").endswith("/line/registers.html"), f"舊的暫存器表網址轉到產線：{pg.url}")

    ph = b.new_page(viewport={"width": 390, "height": 844})
    ph.goto(base, wait_until="networkidle")
    over = ph.evaluate("document.documentElement.scrollWidth > innerWidth")
    check(not over, "手機寬度（390）沒有水平捲動")
    ph.screenshot(path="e2e_gallery_phone.png", full_page=True)
    check(not errs, f"沒有頁面錯誤 {errs[:2]}")
    b.close()
print("ALL PASS" if all(results) else "有失敗項目")
sys.exit(0 if all(results) else 1)
