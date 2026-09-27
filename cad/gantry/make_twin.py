"""雙軸直線模組：classify_gantry.py 的分群結果 → 標準的 groups.json 與 twin.json（給 export_groups.py、TwinBuild）。

    python cad/gantry/make_twin.py <dump.json> <classify 輸出.json> <輸出目錄>

模組 A（沒有 parent）是水平 X 軸，模組 B 掛在 A 的滑座上，是垂直 Z 軸。
位置 0＝滑座在惰輪端，往馬達端為正（跟第一版一致，e2e_gantry.py 與網頁範例都照這個）。
"""
import json
import math
import sys
from pathlib import Path

d = {s["i"]: s for s in json.load(open(sys.argv[1]))}
g = json.load(open(sys.argv[2]))
out = Path(sys.argv[3])
out.mkdir(parents=True, exist_ok=True)
LETTER = "ABCDEFG"


def center(i): return [(a + b) / 2 for a, b in zip(d[i]["min"], d[i]["max"])]


groups, pivots = {}, {}
for i, (mi, kind) in g["assign"].items():
    groups.setdefault(f"{LETTER[mi]}_{kind.capitalize()}", []).append(int(i))

twin = dict(name="LinearGantry", root="FG_Gantry", model="LinearGantry", devices=[], statics=[], axes=[], free=[])
role = {}   # 模組 → 軸名（沒有 parent 的是 X，掛在它上面的是 Z）
for mi, m in enumerate(g["modules"]):
    role[mi] = "X" if m["parent"] is None else "Z"
for mi, m in enumerate(g["modules"]):
    L, ax, name = LETTER[mi], m["axis"], role[mi]
    u = [0.0, 0.0, 0.0]
    u[ax] = float(m["sign"])                       # 往馬達端
    car = [int(i) for i, (k, kind) in g["assign"].items() if k == mi and kind == "carriage"]
    lo = min(d[i]["min"][ax] for i in car) * m["sign"]
    hi = max(d[i]["max"][ax] for i in car) * m["sign"]
    lo, hi = min(lo, hi), max(lo, hi)
    ends = [d[m["blocks"][k]] for k in ("idle_end", "motor_end")]
    faces = sorted(v * m["sign"] for e in ends for v in (e["min"][ax], e["max"][ax]))
    idle_inner = faces[1]                            # 惰輪端座的內側面（沿正方向）
    motor_inner = faces[2]
    travel_minus = idle_inner - lo                   # 負值：滑座離惰輪端還有多遠
    travel = round(motor_inner - idle_inner - (hi - lo), 1)
    pivots[f"{L}_Carriage"] = center(m["blocks"]["carriage"])
    for k in ("Screw", "Base", "Motor"):
        pivots[f"{L}_{k}"] = m["screw_center"]
    parent = None
    if m["parent"] is not None:
        parent = f"Module_{name}"
        twin["statics"].append(dict(name=parent, parent=f"Axis{role[m['parent']]}_Carriage"))   # 群組節點
    twin["devices"].append(dict(name=f"M_Axis{name}", type="DrivePosition", speed=100, mesh=f"{L}_Motor", parent=parent))
    twin["statics"].append(dict(name=f"Base_{name}", mesh=f"{L}_Base", parent=parent))
    twin["axes"].append(dict(name=f"Axis{name}_Screw", mesh=f"{L}_Screw", actor=f"M_Axis{name}", type="rotation",
                             dir=u, factor=36.0, offset=0, parent=parent))
    twin["axes"].append(dict(name=f"Axis{name}_Carriage", mesh=f"{L}_Carriage", actor=f"M_Axis{name}", type="translation",
                             dir=u, factor=0.001, offset=round(travel_minus, 1), parent=parent))
    print(f"模組 {L}（{name} 軸）：方向 {u}，0＝惰輪端，offset {travel_minus:.1f}，行程 0～{travel} mm")
twin["pivots"] = [dict(mesh=k, p=v) for k, v in pivots.items()]
json.dump(dict(groups=groups, pivots=pivots), open(out / "groups.json", "w"), indent=1)
json.dump(twin, open(out / "twin.json", "w"), indent=1)
print({k: len(v) for k, v in sorted(groups.items())})
