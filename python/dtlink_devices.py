"""列出網頁孿生的所有裝置，並把完整的 manifest 存成 devices.json。

    python dtlink_devices.py            # 等網頁連上後列出
"""
import json
import sys
from collections import Counter
from pathlib import Path

from dtlink import Twin, print_devices

twin = Twin(port=int(sys.argv[1]) if len(sys.argv) > 1 else 8765)
twin.wait_connected()

types = Counter(d["type"] for d in twin.device_info)
print("\n裝置類型：", dict(types))
print_devices(twin)

out = Path(__file__).with_name("devices.json")
out.write_text(json.dumps({"devices": twin.device_info,
                           "variables": [v.__dict__ for v in twin.variables.values()]},
                          ensure_ascii=False, indent=1), encoding="utf-8")
print(f"\n已寫入 {out}")
