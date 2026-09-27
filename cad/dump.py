"""列出 STEP 裡每個實體的幾何特徵，給分群與找關節用。

    python dump.py <組立.STEP> <輸出.json>

每筆：i（實體序號，後面各步都用它）、vol（mm^3）、min/max（包圍盒，mm）、faces、kinds（面型統計）、
cyl（最大的幾個圓柱面：軸心點、方向、半徑）、sph（球面：球心、半徑）。
球面＝球關節中心；最長的圓柱面方向＝桿件、螺桿、導桿的軸向。
"""
import json
import sys
import time

import cadquery as cq
from OCP.BRepAdaptor import BRepAdaptor_Surface

t0 = time.time()
solids = cq.importers.importStep(sys.argv[1]).solids().vals()
out = []
for i, s in enumerate(solids):
    bb = s.BoundingBox()
    kinds, cyl, sph = {}, [], []
    for f in s.Faces():
        k = f.geomType()
        kinds[k] = kinds.get(k, 0) + 1
        ad = BRepAdaptor_Surface(f.wrapped) if k in ("CYLINDER", "SPHERE") else None
        if k == "CYLINDER":
            c = ad.Cylinder()
            loc, d = c.Location(), c.Axis().Direction()
            cyl.append(dict(p=[round(loc.X(), 3), round(loc.Y(), 3), round(loc.Z(), 3)],
                            d=[round(d.X(), 4), round(d.Y(), 4), round(d.Z(), 4)],
                            r=round(c.Radius(), 3), area=round(f.Area(), 1)))
        elif k == "SPHERE":
            c = ad.Sphere()
            loc = c.Location()
            sph.append(dict(c=[round(loc.X(), 3), round(loc.Y(), 3), round(loc.Z(), 3)], r=round(c.Radius(), 3)))
    cyl.sort(key=lambda c: -c["area"])
    out.append(dict(i=i, vol=round(s.Volume(), 1),
                    min=[round(bb.xmin, 2), round(bb.ymin, 2), round(bb.zmin, 2)],
                    max=[round(bb.xmax, 2), round(bb.ymax, 2), round(bb.zmax, 2)],
                    faces=sum(kinds.values()), kinds=kinds, cyl=cyl[:4], sph=sph))
json.dump(out, open(sys.argv[2], "w"), indent=0)
print(f"{len(solids)} solids, {time.time() - t0:.0f} s")
