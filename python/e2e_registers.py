"""暫存器表端對端測試：registers.html（另一分頁）手動切換 ↔ 孿生 ↔ 網頁 Python 的 read_reg/write_reg。

    python e2e_registers.py <站台目錄>
    python e2e_registers.py https://dt.qianpro.shop/
"""
import functools
import http.server
import sys
import threading
import time
from pathlib import Path

from playwright.sync_api import sync_playwright

HERE = Path(__file__).parent
import os
target = sys.argv[1]
# 情境：NOTAP=1 模擬瀏覽器快取了沒有 DTTap 監聽點的舊版 Unity；ORDER=reg-first 先開暫存器表再開產線
NOTAP = os.environ.get("NOTAP") == "1"
REG_FIRST = os.environ.get("ORDER") == "reg-first"
print(f"情境：{'舊版 Unity（無監聽點）' if NOTAP else '新版'}、{'先開暫存器表' if REG_FIRST else '先開產線'}")
results = []


def check(cond, msg):
    results.append(bool(cond))
    print(("PASS " if cond else "FAIL ") + msg, flush=True)


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
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 8162), functools.partial(Handler, directory=target))
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    base = "http://127.0.0.1:8162/"

DEV = "MAIN.FG_Transport.Stopper01_Y_Stopper"
DEV2 = "MAIN.FG_Transport.Stopper02_Y_Stopper"

with sync_playwright() as p:
    browser = p.chromium.launch(channel="chrome", headless=True,
                                args=["--enable-unsafe-swiftshader", "--ignore-gpu-blocklist",
                                      "--host-resolver-rules=MAP dt.qianpro.shop 104.21.28.23"])
    ctx = browser.new_context(viewport={"width": 1600, "height": 900})
    errors = []
    if REG_FIRST:
        reg = ctx.new_page()
        reg.on("pageerror", lambda e: errors.append("reg: " + str(e)))
        reg.goto(base + "registers.html", wait_until="load")
        check("未開啟" in reg.inner_text("#conn") or "等待" in reg.inner_text("#conn"), "產線未開時暫存器表顯示未連線")
    main = ctx.new_page()
    if NOTAP:
        # 讓 bridge.js 的 window.DTTap 設不上去：等同 Unity 建置裡沒有監聽點
        main.add_init_script("Object.defineProperty(window, 'DTTap', { get(){ return undefined; }, set(v){}, configurable: false });")
    main.on("pageerror", lambda e: errors.append("main: " + str(e)))
    main.goto(base, wait_until="load", timeout=120000)
    main.wait_for_function("document.getElementById('status').textContent.includes('就緒')", timeout=180000)

    if not REG_FIRST:
        reg = ctx.new_page()
        reg.on("pageerror", lambda e: errors.append("reg: " + str(e)))
        reg.goto(base + "registers.html", wait_until="load")
    reg.wait_for_function("document.getElementById('conn').textContent.includes('可手動切換')", timeout=30000)
    n_rows = reg.evaluate("document.querySelectorAll('tr').length")
    check(n_rows > 100, f"暫存器表建好：{n_rows} 列，連線狀態「{reg.inner_text('#conn')}」")

    def section(dev):
        return reg.locator("section.dev", has=reg.locator(".path", has_text=dev)).first

    def bit(dev, var, label):
        row = section(dev).locator("tr", has=reg.locator("td.var", has_text=var)).first
        return row.locator(".bit", has_text=label).first

    ext_q = bit(DEV, "Control", "伸出")
    ext_i = bit(DEV, "Status", "伸出端")
    ret_i = bit(DEV, "Status", "縮回端")

    # 1. 手動切換：點 %Q 伸出 → 機台回報伸出端
    reg.fill("#q", "Stopper01_Y_Stopper")
    reg.dispatch_event("#q", "input")
    ext_q.click()
    reg.wait_for_function(
        """() => [...document.querySelectorAll('section.dev')].find(s => s.querySelector('.path').textContent.endsWith('Stopper01_Y_Stopper'))
                 .querySelectorAll('tr')[1].querySelector('.bit:nth-child(2)').classList.contains('on')""", timeout=8000)
    check("on" in ext_i.get_attribute("class"), "手動點 %Q「伸出」→ %I「伸出端」亮（機台真的動了）")
    addr = section(DEV).locator("td.addr").first.inner_text()
    reg.screenshot(path=str(HERE / "e2e_registers.png"))

    # 2. 網頁 Python 用暫存器讀寫
    code = f'''
from dtlink import Twin
twin = Twin(verbose=False)
row = [r for r in twin.reg_table("{DEV}") if r["name"].endswith(".Control")][0]
status = [r for r in twin.reg_table("{DEV}") if r["name"].endswith(".Status")][0]
q, i = row["address"], status["address"]
qx = q.replace("QB", "QX"); ix = i.replace("IB", "IX")
print("ADDR", q, i)
print("READ_EXT", twin.read_reg(ix + ".1"))
twin.write_reg(qx + ".1", False)
twin.write_reg(qx + ".0", True)
twin.wait_until(lambda: twin.read_reg(ix + ".0"), timeout=8)
print("RETRACTED_OK", twin.read_reg(ix + ".0"))
'''
    main.evaluate("c => document.querySelector('.CodeMirror').CodeMirror.setValue(c)", code)
    main.click("#run")
    main.wait_for_function("document.getElementById('console').textContent.includes('RETRACTED_OK') || "
                           "document.getElementById('console').textContent.includes('Error')", timeout=30000)
    out = main.inner_text("#console")
    check(f"ADDR {addr}" in out, f"Python reg_table 的位址與暫存器表一致（{addr}）")
    check("READ_EXT True" in out, "Python read_reg 讀到手動伸出後的伸出端＝True")
    check("RETRACTED_OK True" in out, "Python write_reg 縮回 → 縮回端到位")
    reg.wait_for_function(
        """() => [...document.querySelectorAll('section.dev')].find(s => s.querySelector('.path').textContent.endsWith('Stopper01_Y_Stopper'))
                 .querySelectorAll('tr')[1].querySelector('.bit:nth-child(1)').classList.contains('on')""", timeout=8000)
    check("on" in ret_i.get_attribute("class"), "暫存器表即時顯示 Python 造成的變化（縮回端亮）")
    main.wait_for_function("!document.getElementById('run').disabled", timeout=10000)

    # 3. Python 一直寫 Stopper02 時，手動切換的 Stopper01 不會被蓋掉
    code2 = f'''
from dtlink import Twin
import time
twin = Twin(verbose=False)
s2 = twin.device("{DEV2}")
end = time.time() + 8
k = 0
while time.time() < end:
    (s2.extend if k % 2 == 0 else s2.retract)()
    k += 1
    time.sleep(0.1)
print("LOOP_DONE", k)
'''
    main.evaluate("c => document.querySelector('.CodeMirror').CodeMirror.setValue(c)", code2)
    main.click("#run")
    time.sleep(2)
    reg.fill("#q", "Stopper01_Y_Stopper")
    reg.dispatch_event("#q", "input")
    before = main.evaluate(f"(() => {{ const m = window.DTPageBridge.manifest; const v = m.variables.find(v => v.name === '{DEV}.Control'); return window.DTPageBridge.ctrl[v.offset]; }})()")
    ext_q.click()   # 手動：伸出（在 Python 迴圈進行中）
    main.wait_for_function("document.getElementById('console').textContent.includes('LOOP_DONE')", timeout=30000)
    after = main.evaluate(f"(() => {{ const m = window.DTPageBridge.manifest; const v = m.variables.find(v => v.name === '{DEV}.Control'); return window.DTPageBridge.ctrl[v.offset]; }})()")
    check(after & 0b10, f"Python 寫別的暫存器 8 秒後，手動切換的 Stopper01 伸出位元仍在（{before}→{after}）")

    # 4. 全部輸出歸零
    reg.once("dialog", lambda d: d.dismiss())
    reg.click("#zero")
    time.sleep(0.8)
    zero = main.evaluate("Array.from(window.DTPageBridge.ctrl).every(v => v === 0)")
    check(zero, "暫存器表「全部輸出歸零」有效")
    check(not errors, f"沒有頁面錯誤 {errors[:2]}")
    browser.close()

print("ALL PASS" if all(results) else "有失敗項目")
sys.exit(0 if all(results) else 1)
