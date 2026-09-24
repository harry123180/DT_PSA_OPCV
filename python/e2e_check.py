"""端對端檢查：網頁孿生連上後，實際驅動一支氣缸伸縮，確認狀態回報正確。

    python e2e_check.py [port]
結束碼 0＝通過。會把裝置清單寫到 devices.json。
"""
import json
import sys
import time
from collections import Counter
from pathlib import Path

from dtlink import Twin

port = int(sys.argv[1]) if len(sys.argv) > 1 else 8765
twin = Twin(port=port, name="e2e-check")
twin.wait_connected(timeout=180)
time.sleep(1.0)  # 等第一筆狀態映像

Path(__file__).with_name("devices.json").write_text(
    json.dumps({"devices": twin.device_info, "variables": [v.__dict__ for v in twin.variables.values()]},
               ensure_ascii=False, indent=1), encoding="utf-8")
print("裝置類型：", dict(Counter(d["type"] for d in twin.device_info)))

cylinders = twin.devices(type="FB_Cylinder")
assert cylinders, "manifest 裡沒有氣缸"

ok = 0
for cyl in cylinders[:3]:
    start = "extended" if cyl.extended else ("retracted" if cyl.retracted else "middle")
    try:
        cyl.extend()
        twin.wait_until(lambda: cyl.extended, timeout=8)
        t_ext = time.time()
        cyl.retract()
        twin.wait_until(lambda: cyl.retracted, timeout=8)
        print(f"PASS {cyl.path}（原本 {start}）：伸出→到位、縮回→到位")
        ok += 1
    except TimeoutError:
        print(f"FAIL {cyl.path}：control={twin.read(cyl.path + '.Control')} status={twin.read(cyl.path + '.Status')}")
    finally:
        cyl.release()

print(f"{ok}/{min(3, len(cylinders))} 支氣缸通過")
sys.exit(0 if ok else 1)
