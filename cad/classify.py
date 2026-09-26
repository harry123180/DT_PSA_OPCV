"""把直線模組組立（多個 BaseBody）自動分成：每個模組的 固定座／馬達／滑座／螺桿，並判斷模組之間的掛載關係。
只靠幾何：兩支 640 導桿＋一支 605 螺桿定義一個模組；四個大塊依體積辨識（滑座、馬達端座、惰輪端座、馬達）。"""
import json, sys
d = json.load(open(sys.argv[1]))
V_ROD, V_SCREW = 201046, 100819
V_CARRIAGE, V_MOTOR_END, V_IDLE_END, V_MOTOR = 262160, 258635, 264995, 212896
def near(v, t): return abs(v - t) < 0.01 * t
def center(s): return [(a + b) / 2 for a, b in zip(s['min'], s['max'])]
def size(s): return [b - a for a, b in zip(s['min'], s['max'])]
def contains(box, p, pad=0):
    return all(box[0][k] - pad <= p[k] <= box[1][k] + pad for k in range(3))

screws = [s for s in d if near(s['vol'], V_SCREW)]
modules = []
for sc in screws:
    ax = max(range(3), key=lambda k: size(sc)[k])
    c = center(sc)
    def same_line(s, tol=40):   # 截面中心離螺桿軸夠近（導桿在螺桿兩側約 42 mm，用較寬的容差）
        cc = center(s)
        return all(abs(cc[k] - c[k]) < tol for k in range(3) if k != ax)
    rods = [s for s in d if near(s['vol'], V_ROD) and same_line(s, 60) and max(range(3), key=lambda k: size(s)[k]) == ax]
    blocks = {}
    for name, vol in [('carriage', V_CARRIAGE), ('motor_end', V_MOTOR_END), ('idle_end', V_IDLE_END), ('motor', V_MOTOR)]:
        cand = [s for s in d if near(s['vol'], vol) and same_line(s)]
        # 同一條線上最靠近螺桿範圍的那個
        cand.sort(key=lambda s: abs(center(s)[ax] - c[ax]))
        blocks[name] = cand[0]
    lo = [min(s['min'][k] for s in [sc] + rods + list(blocks.values())) for k in range(3)]
    hi = [max(s['max'][k] for s in [sc] + rods + list(blocks.values())) for k in range(3)]
    sign = 1 if center(blocks['motor'])[ax] > center(blocks['idle_end'])[ax] else -1   # 正方向：往馬達端
    modules.append(dict(screw=sc['i'], rods=[r['i'] for r in rods], blocks={k: v['i'] for k, v in blocks.items()},
                        axis=ax, sign=sign, box=[lo, hi], screw_center=c))

by_i = {s['i']: s for s in d}
# 每個實體歸屬：先認大件，其餘看落在哪個模組的包圍盒，再細分滑座／螺桿／固定
assign = {}
for mi, m in enumerate(modules):
    assign[m['screw']] = (mi, 'screw')
    for r in m['rods']: assign[r] = (mi, 'base')
    for k, i in m['blocks'].items():
        assign[i] = (mi, {'carriage': 'carriage', 'motor': 'motor'}.get(k, 'base'))
for s in d:
    if s['i'] in assign: continue
    p = center(s)
    hits = [mi for mi, m in enumerate(modules) if contains(m['box'], p)]
    if not hits:
        assign[s['i']] = (None, 'extra'); continue
    mi = hits[0] if len(hits) == 1 else min(hits, key=lambda k: max(size(by_i[modules[k]['screw']])))
    m = modules[mi]; ax = m['axis']
    car = by_i[m['blocks']['carriage']]
    cc = center(car)
    sc = m['screw_center']
    radial = max(abs(p[k] - sc[k]) for k in range(3) if k != ax)
    if abs(p[ax] - cc[ax]) < 70:
        kind = 'carriage'
    elif radial < 14 and size(s)[ax] < 30 and abs(p[ax] - center(by_i[m['blocks']['motor_end']])[ax]) < 40 and s['vol'] > 2000:
        kind = 'screw'      # 聯軸器，跟螺桿一起轉
    else:
        kind = 'base'
    assign[s['i']] = (mi, kind)

# 掛載：模組 B 的惰輪端座若貼著模組 A 的滑座，B 整支掛在 A 的滑座上
for mi, m in enumerate(modules):
    m['parent'] = None
    idle = by_i[m['blocks']['idle_end']]
    for mj, n in enumerate(modules):
        if mj == mi: continue
        car = by_i[n['blocks']['carriage']]
        gap = max(max(idle['min'][k] - car['max'][k], car['min'][k] - idle['max'][k]) for k in range(3))
        if gap < 3: m['parent'] = mj
# 其他沒歸屬的（轉接板、螺絲）：掛到它碰到的滑座
for i, (mi, kind) in list(assign.items()):
    if kind != 'extra': continue
    p = center(by_i[i])
    best = min(range(len(modules)), key=lambda k: sum((p[a] - center(by_i[modules[k]['blocks']['carriage']])[a]) ** 2 for a in range(3)))
    assign[i] = (best, 'carriage')

out = dict(modules=modules, assign={str(k): v for k, v in assign.items()})
json.dump(out, open(sys.argv[2], 'w'), indent=1)
from collections import Counter
for mi, m in enumerate(modules):
    cnt = Counter(kind for (k, kind) in assign.values() if k == mi)
    print(f"module {mi}: axis={'XYZ'[m['axis']]}{'+' if m['sign']>0 else '-'} parent={m['parent']} blocks={m['blocks']} parts={dict(cnt)}")
print('unassigned', [i for i, v in assign.items() if v[0] is None])
