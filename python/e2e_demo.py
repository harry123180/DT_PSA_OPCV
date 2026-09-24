"""實測 dtlink_demo：網頁孿生 + 步進程序跑一段時間，統計各站放行次數。

    python e2e_demo.py [秒數] [網址]
"""
import sys
import threading
import time
from collections import Counter
from pathlib import Path

from playwright.sync_api import sync_playwright

import dtlink_demo as demo
from dtlink import Twin
from e2e_browser import serve_local

HERE = Path(__file__).parent
SECONDS = int(sys.argv[1]) if len(sys.argv) > 1 else 90
URL = sys.argv[2] if len(sys.argv) > 2 else None


def main():
    url = URL or serve_local()
    twin = Twin(name="e2e-demo", verbose=False)
    releases = Counter()
    stations_ref = {}

    def control():
        twin.wait_connected(180)
        time.sleep(0.5)
        names = ["Lift01", "Stopper03", "Index01", "Index02", "Index03", "Index04", "Index05",
                 "Stopper04", "Lift02", "Stopper01", "Stopper02"]
        stations = [demo.Station(n) for n in names]
        for a, b in zip(stations, stations[1:] + stations[:1]):
            a.next = b
        by = {s.name: s for s in stations}
        stations_ref.update(by)
        for name in ("Transport01_M_Conveyor", "Transport02_M_Conveyor"):
            twin.device(demo.T + name).backward()
        programs = [
            demo.lift_station(by["Lift01"], twin, down_first=True),
            demo.lift_station(by["Lift02"], twin, down_first=False),
            *[demo.stopper_station(by[n], twin) for n in ("Stopper01", "Stopper02", "Stopper03", "Stopper04")],
            *[demo.index_station(by[n], twin) for n in ("Index01", "Index02", "Index03", "Index04", "Index05")],
        ]
        waiting = {p: None for p in programs}
        last = {s.name: "" for s in stations}
        end = time.time() + SECONDS
        while time.time() < end:
            for p in programs:
                c = waiting[p]
                if c is None or c():
                    waiting[p] = next(p)
            for s in stations:
                if s.step != last[s.name] and s.step in ("放行", "送出"):
                    releases[s.name] += 1
                last[s.name] = s.step
            time.sleep(0.02)
        twin.stop_all()

    t = threading.Thread(target=control, daemon=True)
    with sync_playwright() as p:
        browser = p.chromium.launch(channel="chrome", headless=True,
                                    args=["--enable-unsafe-swiftshader", "--ignore-gpu-blocklist",
                                          "--disable-features=LocalNetworkAccessChecks"])
        page = browser.new_page(viewport={"width": 1600, "height": 900})
        page.goto(url, wait_until="load", timeout=120000)
        t.start()
        for i in range(1, 4):
            time.sleep(SECONDS / 3 + (5 if i == 1 else 0))
            page.screenshot(path=str(HERE / f"e2e_demo_{i}.png"))
        t.join(timeout=30)
        browser.close()

    print("各站放行次數：", dict(releases))
    print("最後步驟：", {n: s.step for n, s in stations_ref.items()})
    moved = sum(releases.values())
    print("PASS" if moved >= 5 else "FAIL", f"共放行 {moved} 次")
    sys.exit(0 if moved >= 5 else 1)


if __name__ == "__main__":
    main()
