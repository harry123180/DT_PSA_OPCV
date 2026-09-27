"""依 groups.json 把 STEP 實體合併成網格群組，每群一個 STL（單位 mm）。

    python export_groups.py <組立.STEP> <groups.json> <輸出目錄> [--tol 0.15]

--tol：網格化的弦高誤差（mm），角度誤差跟著放寬。小零件多的機台（螺絲、螺紋）用 0.15～0.3 才不會面數爆掉；
WebGL 整台最好在 50 萬面以內。

groups.json：{"groups": {"網格名": [實體序號, ...]}, "pivots": {"網格名": [x, y, z]}}
實體序號＝dump.py 的 i（兩者都用 cadquery 讀同一個 STEP，順序一致）。
"""
import json
import os
import sys

import cadquery as cq

step, groups_json, outdir = sys.argv[1:4]
tol = float(sys.argv[sys.argv.index("--tol") + 1]) if "--tol" in sys.argv else 0.15
os.makedirs(outdir, exist_ok=True)
g = json.load(open(groups_json))
solids = cq.importers.importStep(step).solids().vals()
used = set()
for name, ids in sorted(g["groups"].items()):
    comp = cq.Compound.makeCompound([solids[i] for i in ids])
    cq.exporters.export(cq.Workplane().add(comp), os.path.join(outdir, name + ".stl"),
                        tolerance=tol, angularTolerance=min(0.6, tol * 3))
    used.update(ids)
    print(f"{name}: {len(ids)} solids")
json.dump(g.get("pivots", {}), open(os.path.join(outdir, "pivots.json"), "w"), indent=1)
left = sorted(set(range(len(solids))) - used)
print("沒有分到群組的實體：", left if left else "無")
