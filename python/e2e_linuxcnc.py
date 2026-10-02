"""LinuxCNC ↔ 網頁孿生的整合測試：瀏覽器開加工中心孿生（?ws=ws://127.0.0.1:8765），連到 Docker 裡 LinuxCNC 的 dtvmc 橋接，
在容器裡用 run_program.py 讓 LinuxCNC 解除 E-stop、回原點、跑 demo.ngc（4 次換刀），檢查：
LinuxCNC 程式跑完沒有錯誤、孿生主軸上的刀＝LinuxCNC 的 tool_in_spindle、孿生沒有警報、真的切掉了料、畫面有變化。
最後故意讓孿生撞機，確認 LinuxCNC 會 E-stop。

    python e2e_linuxcnc.py <站台目錄>          # web/assemble_site.py --variant vmc 組好的站
前提：容器 lcnc（-p 8765:8765）裡 LinuxCNC 已用 vmc_twin.ini 啟動（linuxcnc/vmc_twin/README.md）。
"""
import functools
import http.server
import os
import re
import subprocess
import sys
import threading
import time
from pathlib import Path

from PIL import Image, ImageChops
from playwright.sync_api import sync_playwright

HERE = Path(__file__).parent
site = sys.argv[1]
CONTAINER = os.environ.get("LCNC_CONTAINER", "lcnc")
CFG = "/home/cnc/linuxcnc/configs/vmc_twin"
results = []
env = dict(os.environ, MSYS_NO_PATHCONV="1")


def check(cond, msg):
    results.append(bool(cond))
    print(("PASS " if cond else "FAIL ") + msg, flush=True)


def sh(cmd, timeout=60):
    r = subprocess.run(["docker", "exec", CONTAINER, "su", "cnc", "-c", cmd], capture_output=True, text=True,
                       encoding="utf-8", errors="replace", timeout=timeout, env=env)
    return (r.stdout + r.stderr).strip()


def pin(name):
    return sh(f"halcmd getp {name}")


def diff(a, b):
    x, y = Image.open(a).convert("L"), Image.open(b).convert("L")
    h = ImageChops.difference(x, y).histogram()
    return sum(i * n for i, n in enumerate(h)) / (x.size[0] * x.size[1])


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


httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 8172), functools.partial(Handler, directory=site))
threading.Thread(target=httpd.serve_forever, daemon=True).start()

check("dtvmc.x" in sh("halcmd show pin dtvmc"), "容器裡的 LinuxCNC 已載入 dtvmc（HAL 元件）")

with sync_playwright() as p:
    browser = p.chromium.launch(channel="chrome", headless=True, args=["--enable-unsafe-swiftshader", "--ignore-gpu-blocklist"])
    page = browser.new_page(viewport={"width": 1600, "height": 900})
    errors, logs = [], []
    page.on("pageerror", lambda e: errors.append(str(e)))
    page.on("console", lambda m: logs.append(m.text) if "[VMC]" in m.text else None)
    page.goto("http://127.0.0.1:8172/?ws=ws://127.0.0.1:8765", wait_until="load", timeout=120000)
    page.wait_for_function("document.body.innerText.includes('Python connected')", timeout=240000)
    t0 = time.time()
    while pin("dtvmc.ok") != "TRUE" and time.time() - t0 < 120:
        time.sleep(1)
    check(pin("dtvmc.connected") == "TRUE" and pin("dtvmc.ok") == "TRUE" and pin("dtvmc.tool") == "0",
          f"孿生連上 LinuxCNC，橋接先卸刀（主軸刀號 {pin('dtvmc.tool')}）、沒有警報")
    canvas = page.locator("#unity-canvas")
    shot0 = HERE / "lcnc_0.png"; canvas.screenshot(path=str(shot0))

    out = sh(f"cd {CFG} && python3 run_program.py demo.ngc", timeout=1500)
    print("   " + "\n   ".join(l for l in out.splitlines() if l.strip())[-1500:])
    shot1 = HERE / "lcnc_1.png"; canvas.screenshot(path=str(shot1))
    vol = float(pin("dtvmc.cut-volume") or 0)
    check("PASS" in out.splitlines()[-1], "LinuxCNC 跑完 demo.ngc：沒有錯誤、孿生沒有警報、孿生主軸上的刀＝LinuxCNC 的 tool_in_spindle")
    swaps = [l for l in logs if "主軸夾住" in l]
    check(len(swaps) >= 4, f"孿生記錄 LinuxCNC 的換刀 {len(swaps)} 次：{[s[s.find('T'):][:20] for s in swaps]}")
    check(vol > 25, f"孿生切掉 {vol} cm³（網頁版同一支程式約 34.5）")
    check(diff(shot0, shot1) > 0.3, f"畫面有變化（差異 {diff(shot0, shot1):.2f}）")

    # 孿生撞機 → LinuxCNC E-stop：MDI 主軸停著直接下刀切入素材
    sh(f"cd {CFG} && python3 -c \"import linuxcnc,time; c=linuxcnc.command(); s=linuxcnc.stat(); "
       "c.mode(linuxcnc.MODE_MDI); c.wait_complete(); c.mdi('M5'); c.wait_complete(); "
       "c.mdi('G0 X30 Y30 Z5'); c.wait_complete(30); c.mdi('G1 Z-3 F300'); c.wait_complete(30); time.sleep(1); s.poll(); "
       "print('TASK', s.task_state)\"", timeout=120)
    time.sleep(1)
    st = sh("python3 -c \"import linuxcnc; s=linuxcnc.stat(); s.poll(); print(s.task_state)\"")
    check(pin("dtvmc.alarm") == "10" and st.strip() == "1", f"沒轉就下刀 → 孿生警報 {pin('dtvmc.alarm')} → LinuxCNC E-stop（task_state={st}）")
    sh("python3 -c \"import linuxcnc,time; c=linuxcnc.command(); c.state(linuxcnc.STATE_ESTOP_RESET); time.sleep(2)\"")
    time.sleep(1)
    check(pin("dtvmc.alarm") == "0", f"LinuxCNC 解除 E-stop → 孿生警報清除（{pin('dtvmc.alarm')}）")
    page.screenshot(path=str(HERE / "e2e_linuxcnc_page.png"))
    check(not errors, f"沒有頁面錯誤 {errors[:2]}")
    browser.close()

print("ALL PASS" if all(results) else "有失敗項目")
sys.exit(0 if all(results) else 1)
