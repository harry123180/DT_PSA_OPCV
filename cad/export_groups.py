"""依 classify 的結果把 STEP 實體合併成群組網格（STL，單位 mm），並算出每個模組的運動資料（軸向、行程、螺桿中心）。"""
import cadquery as cq, json, sys, os
step, groups_json, outdir = sys.argv[1:4]
os.makedirs(outdir, exist_ok=True)
g = json.load(open(groups_json))
solids = cq.importers.importStep(step).solids().vals()
names = 'ABCDEFG'
buckets = {}
for i, (mi, kind) in g['assign'].items():
    buckets.setdefault((mi, kind), []).append(solids[int(i)])
meta = dict(modules=[])
for (mi, kind), items in sorted(buckets.items()):
    name = f"{names[mi]}_{kind.capitalize()}"
    comp = cq.Compound.makeCompound(items)
    cq.exporters.export(cq.Workplane().add(comp), os.path.join(outdir, name + '.stl'), tolerance=0.08, angularTolerance=0.25)
    print(name, len(items), 'solids')
for mi, m in enumerate(g['modules']):
    ax = m['axis']
    car = [solids[int(i)].BoundingBox() for i, (k, kind) in g['assign'].items() if k == mi and kind == 'carriage']
    lo = min([b.xmin, b.ymin, b.zmin][ax] for b in car); hi = max([b.xmax, b.ymax, b.zmax][ax] for b in car)
    # 行程：滑座群組在兩端座之間能走的距離
    ends = [solids[m['blocks'][k]].BoundingBox() for k in ('idle_end', 'motor_end')]
    inner_lo = min([e.xmax, e.ymax, e.zmax][ax] for e in ends)
    inner_hi = max([e.xmin, e.ymin, e.zmin][ax] for e in ends)
    cb = solids[m['blocks']['carriage']].BoundingBox()
    meta['modules'].append(dict(name=names[mi], axis='XYZ'[ax], sign=m['sign'], parent=None if m['parent'] is None else names[m['parent']],
        screw_center=m['screw_center'], carriage_center=[(cb.xmin+cb.xmax)/2, (cb.ymin+cb.ymax)/2, (cb.zmin+cb.zmax)/2],
        travel_minus=round(inner_lo - lo, 1), travel_plus=round(inner_hi - hi, 1)))
json.dump(meta, open(os.path.join(outdir, 'meta.json'), 'w'), indent=1)
print(json.dumps(meta, indent=1))
