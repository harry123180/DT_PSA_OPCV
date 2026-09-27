"""通用：由「滾珠螺桿直線模組」組成的串接機台（單軸、XY、XYZ 龍門、雙 Y 同步…）的分群與機構描述。

    python cad/screw_modules.py <dump.json> <輸出目錄> [--name Gantry3] [--root FG_Gantry] [--speed 100]

輸入 dump.py 的結果；輸出 groups.json、twin.json（格式見 TwinBuild.cs）、summary.json（給網頁說明用）。
只處理軸向對齊座標軸的模組（斜的見 cad/parallel_robot/）。

辨識（只靠幾何）：
  螺桿    長條、圓柱面很多（螺紋）；方向＝模組軸向
  四大塊  中心落在螺桿軸線上（< 3 mm）的大方塊，依軸向排序：馬達｜馬達端座｜滑座｜惰輪端座
  成員    中心落在四大塊截面範圍內、軸向在模組兩端之間的零件；軸向落在滑座範圍內的跟著滑座走
  掛載    模組的端座貼著（< 3 mm）別的模組的滑座 → 整支掛在那個滑座上；
          兩端分別貼著兩個滑座（龍門橫梁架在兩支 Y 上）→ 那兩支是同步軸，共用一個伺服
  行程    滑座在兩端座內側面之間能走的距離；位置 0＝惰輪端，往馬達端為正
軸名：機器座標 X＝CAD x、Y＝CAD -z、Z＝CAD y（CAD 是 Y 朝上）；同步的兩支叫 Y1、Y2，共用 M_AxisY。
"""
import json
import math
import sys
from collections import defaultdict
from pathlib import Path

args = [a for a in sys.argv[1:] if not a.startswith("--")]
def opt(k, default):
    return sys.argv[sys.argv.index(k) + 1] if k in sys.argv else default
d = json.load(open(args[0]))
out = Path(args[1] if len(args) > 1 else ".")
out.mkdir(parents=True, exist_ok=True)
NAME, ROOT, SPEED = opt("--name", "Gantry"), opt("--root", "FG_Gantry"), float(opt("--speed", "100"))
by_i = {s["i"]: s for s in d}


def center(s): return [(a + b) / 2 for a, b in zip(s["min"], s["max"])]
def size(s): return [b - a for a, b in zip(s["min"], s["max"])]
def gap(a, b): return max(max(a["min"][k] - b["max"][k], b["min"][k] - a["max"][k]) for k in range(3))


screws = [s for s in d if s["kinds"].get("CYLINDER", 0) > 60 and max(size(s)) > 5 * sorted(size(s))[1]]
def line_blocks(sc):
    """中心在螺桿軸線上（< 3 mm）的大方塊，依軸向排序"""
    ax = max(range(3), key=lambda k: size(sc)[k])
    c = center(sc)
    near = [s for s in d if s["i"] != sc["i"] and max(abs(center(s)[k] - c[k]) for k in range(3) if k != ax) < 3
            and size(s)[ax] < 0.5 * size(sc)[ax]]
    vmax = max(s["vol"] for s in near)
    return ax, sorted((s for s in near if s["vol"] > 0.3 * vmax), key=lambda s: center(s)[ax])


# 第一輪：跟螺桿範圍重疊的三塊依序是 端座｜滑座｜端座（上一軸的滑座可能剛好在這一軸的延長線上，不能只數大塊）
first = {}
for sc in screws:
    ax, blocks = line_blocks(sc)
    inner = [b for b in blocks if b["max"][ax] > sc["min"][ax] + 0.5 and b["min"][ax] < sc["max"][ax] - 0.5]
    assert len(inner) == 3, f"螺桿 {sc['i']}：跟螺桿重疊的大塊有 {len(inner)} 個，預期 3（兩端座＋滑座）"
    first[sc["i"]] = (ax, blocks, inner)
all_carriages = {inner[1]["i"] for ax, blocks, inner in first.values()}

# 第二輪：馬達＝螺桿範圍外、不是任何模組滑座、離端座最近的大塊；它在哪一端，哪一端就是馬達端
modules = []
for sc in screws:
    ax, blocks, inner = first[sc["i"]]
    outside = [b for b in blocks if b not in inner and b["i"] not in all_carriages]
    motor = min(outside, key=lambda b: min(abs(b["min"][ax] - inner[-1]["max"][ax]), abs(inner[0]["min"][ax] - b["max"][ax])))
    if abs(motor["min"][ax] - inner[-1]["max"][ax]) < abs(inner[0]["min"][ax] - motor["max"][ax]):
        idle_end, carriage, motor_end = inner
    else:
        motor_end, carriage, idle_end = inner
    sign = 1 if center(motor)[ax] > center(idle_end)[ax] else -1       # 正方向：往馬達端
    parts = (motor, motor_end, carriage, idle_end)
    lo = [min(b["min"][k] for b in parts) - 1 for k in range(3)]
    hi = [max(b["max"][k] for b in parts) + 1 for k in range(3)]
    modules.append(dict(screw=sc["i"], axis=ax, sign=sign, box=(lo, hi), blocks=dict(
        motor=motor["i"], motor_end=motor_end["i"], carriage=carriage["i"], idle_end=idle_end["i"])))

# ── 零件歸屬 ────────────────────────────────────────────
# 滑座群組：從滑座方塊沿「互相接觸」的零件擴散（軸承、螺帽、螺絲…都鎖在滑座上）；
# 碰到導桿、螺桿、任何模組的端座／馬達／別的滑座就停。只看「軸向在滑塊範圍內」會漏掉前後凸出的軸承。
stop = set()
for m in modules:
    stop.add(m["screw"])
    stop.update(m["blocks"].values())
def long_part(s, m): return size(s)[m["axis"]] > 0.5 * size(by_i[m["screw"]])[m["axis"]]
assign = {}
for mi, m in enumerate(modules):
    assign[m["screw"]] = (mi, "Screw")
    for k, i in m["blocks"].items():
        assign[i] = (mi, {"motor": "Motor", "carriage": "Carriage"}.get(k, "Base"))
for mi, m in enumerate(modules):
    lo, hi = m["box"]
    ax = m["axis"]
    # 跟著滑座走的零件，軸向中心一定在兩端座內側面之間（不然一動就撞端座）；
    # 包圍盒接觸只是近似，電纜這類彎管的包圍盒很大，沒有這條限制會一路連到馬達端
    ends = sorted(v for k in ("idle_end", "motor_end") for v in (by_i[m["blocks"][k]]["min"][ax], by_i[m["blocks"][k]]["max"][ax]))
    inner_lo, inner_hi = ends[1], ends[2]
    todo, seen = [m["blocks"]["carriage"]], {m["blocks"]["carriage"]}
    while todo:
        cur = by_i[todo.pop()]
        for s in d:
            i = s["i"]
            if i in seen or i in stop or i in assign or long_part(s, m):
                continue
            if not all(lo[k] - 5 <= center(s)[k] <= hi[k] + 5 for k in range(3)):
                continue
            if not inner_lo < center(s)[ax] < inner_hi:
                continue
            if gap(s, cur) < 0.05:
                seen.add(i)
                assign[i] = (mi, "Carriage")
                todo.append(i)
for s in d:
    if s["i"] in assign:
        continue
    p = center(s)
    for mi, m in enumerate(modules):
        lo, hi = m["box"]
        if not all(lo[k] <= p[k] <= hi[k] for k in range(3)):
            continue
        mot = by_i[m["blocks"]["motor"]]
        if not long_part(s, m) and (p[m["axis"]] - center(mot)[m["axis"]]) * m["sign"] > 0:
            kind = "Motor"                                            # 比馬達更外面：尾蓋
        else:
            kind = "Base"                                             # 導桿、端座上的零件
        assign[s["i"]] = (mi, kind)
        break

# ── 掛載與同步 ──────────────────────────────────────────
parents = defaultdict(set)
for mi, m in enumerate(modules):
    for end in ("idle_end", "motor_end"):
        for mj, n in enumerate(modules):
            if mj != mi and gap(by_i[m["blocks"][end]], by_i[n["blocks"]["carriage"]]) < 3:
                parents[mi].add(mj)
sync = []                       # 同步群：被同一支模組兩端同時架著的模組
for mi, ps in parents.items():
    if len(ps) > 1:
        sync.append(sorted(ps))
drive_of = {mi: mi for mi in range(len(modules))}
for group in sync:
    for mj in group:
        drive_of[mj] = group[0]
parent = {mi: (min(ps) if ps else None) for mi, ps in ((mi, parents.get(mi, set())) for mi in range(len(modules)))}

# 其他零件（落在四大塊範圍外：尾蓋、電纜、側板、轉接螺絲…）：跟它碰到的已分類零件同一群，反覆傳播；
# 直接丟給「最近的滑座」會把馬達端的尾蓋、電纜也變成會動的
while True:
    progress = False
    for s in d:
        if s["i"] in assign:
            continue
        touch = [assign[t["i"]] for t in d if t["i"] in assign and gap(s, t) < 0.05]
        if touch:
            # 同時碰到固定件與滑座（轉接螺絲）：跟滑座走；否則取最多的
            kinds = [x for x in touch if x[1] == "Carriage"] or touch
            assign[s["i"]] = max(set(kinds), key=kinds.count)
            progress = True
    if not progress:
        break
for s in d:
    if s["i"] not in assign:
        best = min((t for t in d if t["i"] in assign), key=lambda t: gap(s, t))
        assign[s["i"]] = assign[best["i"]]

# 檢查：滑座群組的每個零件一定在兩端座之間，否則一動就穿過端座——通常是把馬達端的零件分錯了
for mi, m in enumerate(modules):
    ax = m["axis"]
    ends = sorted(v for k in ("idle_end", "motor_end") for v in (by_i[m["blocks"][k]]["min"][ax], by_i[m["blocks"][k]]["max"][ax]))
    for i, (k, kind) in assign.items():
        if k == mi and kind == "Carriage":
            assert ends[1] - 0.5 <= center(by_i[i])[ax] <= ends[2] + 0.5, f"實體 {i} 分在滑座，但在端座外面（{center(by_i[i])[ax]:.1f}）"

# ── 命名 ────────────────────────────────────────────────
ROLE = {0: "X", 2: "Y", 1: "Z"}             # CAD 軸 → 機器軸（CAD Y 朝上）
role = {mi: ROLE[m["axis"]] for mi, m in enumerate(modules)}
for group in sync:
    for n, mj in enumerate(group, 1):
        role[mj] = f"{ROLE[modules[mj]['axis']]}{n}"
drive_name = {mi: "M_Axis" + ROLE[modules[drive_of[mi]]["axis"]] if any(mi in g for g in sync) else "M_Axis" + role[mi]
              for mi in range(len(modules))}
LET = "ABCDEFGH"

groups, pivots = defaultdict(list), {}
for i, (mi, kind) in assign.items():
    groups[f"{LET[mi]}_{kind}"].append(i)
twin = dict(name=NAME, root=ROOT, model=NAME, devices=[], statics=[], axes=[], free=[])
summary = []
made_devices = {}
order = sorted(range(len(modules)), key=lambda mi: (parent[mi] is not None, mi))
for mi in order:
    m, L, r = modules[mi], LET[mi], role[mi]
    ax = m["axis"]
    u = [0.0, 0.0, 0.0]
    u[ax] = float(m["sign"])
    car = [i for i, (k, kind) in assign.items() if k == mi and kind == "Carriage"]
    own = [i for i in car if all(m["box"][0][k] <= center(by_i[i])[k] <= m["box"][1][k] for k in range(3))]
    lo = min(min(by_i[i]["min"][ax] * m["sign"], by_i[i]["max"][ax] * m["sign"]) for i in own)
    hi = max(max(by_i[i]["min"][ax] * m["sign"], by_i[i]["max"][ax] * m["sign"]) for i in own)
    faces = sorted(v * m["sign"] for k in ("idle_end", "motor_end") for v in (by_i[m["blocks"][k]]["min"][ax], by_i[m["blocks"][k]]["max"][ax]))
    idle_inner, motor_inner = faces[1], faces[2]
    offset = round(idle_inner - lo, 2)
    travel = round(motor_inner - idle_inner - (hi - lo), 2)
    pivots[f"{L}_Carriage"] = center(by_i[m["blocks"]["carriage"]])
    for k in ("Screw", "Base", "Motor"):
        pivots[f"{L}_{k}"] = center(by_i[m["screw"]])
    grp = None
    if parent[mi] is not None:
        grp = f"Module_{r}"
        twin["statics"].append(dict(name=grp, parent=f"Axis{role[parent[mi]]}_Carriage"))
    dn = drive_name[mi]
    if dn not in made_devices:
        dev = dict(name=dn, type="DrivePosition", speed=SPEED, mesh=f"{L}_Motor", parent=grp, meshes=[])
        made_devices[dn] = dev
        twin["devices"].append(dev)
        summary.append(dict(device=dn, modules=[r], axis=ROLE[ax], travel=travel, parent=role.get(parent[mi]) if parent[mi] is not None else None))
    else:
        made_devices[dn]["meshes"].append(f"{L}_Motor")                 # 同步軸的另一顆馬達
        next(x for x in summary if x["device"] == dn)["modules"].append(r)
    twin["statics"].append(dict(name=f"Base_{r}", mesh=f"{L}_Base", parent=grp))
    twin["axes"].append(dict(name=f"Axis{r}_Screw", mesh=f"{L}_Screw", actor=dn, type="rotation", dir=u, factor=36.0, offset=0, parent=grp))
    twin["axes"].append(dict(name=f"Axis{r}_Carriage", mesh=f"{L}_Carriage", actor=dn, type="translation", dir=u, factor=0.001, offset=offset, parent=grp))
    kinds = defaultdict(int)
    for i, (k, kind) in assign.items():
        if k == mi:
            kinds[kind] += 1
    print(f"模組 {L}（{r}）：CAD 軸 {'xyz'[ax]}{'+' if m['sign'] > 0 else '-'}，伺服 {dn}，掛在 {role[parent[mi]] if parent[mi] is not None else '地面'}，"
          f"行程 0～{travel} mm（CAD 位置 {-offset:g}），零件 {dict(kinds)}")
twin["pivots"] = [dict(mesh=k, p=v) for k, v in pivots.items()]
json.dump(dict(groups=groups, pivots=pivots), open(out / "groups.json", "w"), indent=1)
json.dump(twin, open(out / "twin.json", "w"), indent=1)
json.dump(summary, open(out / "summary.json", "w"), indent=1, ensure_ascii=False)
print("同步軸：", [[role[m] for m in g] for g in sync] or "無", "；實體", len(assign), "/", len(d))
