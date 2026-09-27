"""6-DOF 並聯機器人（6-PSS／Hexaglide：6 支水平滾珠螺桿滑座，各接一根 210 mm 連桿到上平台）的分群與機構描述。

    python cad/parallel_robot/classify_robot.py <dump.json> <輸出目錄>

輸入是 dump.py 的結果；輸出：
  groups.json  哪些實體合成哪個網格（Leg1_Base、Leg1_Motor、Leg1_Carriage、Leg1_Screw、Rod1…、Platform）與原點
  twin.json    給 Unity TwinBuild 的機構描述（裝置、軸、自訂運動學元件），座標都是 CAD 的 mm

辨識規則（只靠幾何，零件名都是 BaseBodyN）：
  連桿＝有兩個相距最遠的球面（R5）的實體，球心＝兩端球關節；下端在滑座上、上端在平台上
  螺桿＝長條且圓柱面很多（螺紋），方向＝模組軸向
  滑座＝包住連桿下端球心的方塊；同一模組內、軸向落在滑座範圍內的零件都跟著滑座走
  馬達＝模組最外端的最大方塊；平台＝最高處的零件
"""
import json
import math
import sys
from pathlib import Path

d = json.load(open(sys.argv[1]))
out = Path(sys.argv[2])
out.mkdir(parents=True, exist_ok=True)
by_i = {s["i"]: s for s in d}


def center(s): return [(a + b) / 2 for a, b in zip(s["min"], s["max"])]
def size(s): return [b - a for a, b in zip(s["min"], s["max"])]
def sub(a, b): return [x - y for x, y in zip(a, b)]
def dot(a, b): return sum(x * y for x, y in zip(a, b))
def norm(a): return math.sqrt(dot(a, a))
def unit(a): n = norm(a); return [x / n for x in a]
def inside(p, s, pad=0.5): return all(s["min"][k] - pad <= p[k] <= s["max"][k] + pad for k in range(3))


# ── 連桿與兩端球心 ───────────────────────────────────────
rods = []
for s in d:
    cs = []
    for x in s["sph"]:
        if all(math.dist(x["c"], q) > 0.01 for q in cs):
            cs.append(x["c"])
    if len(cs) != 2:
        continue
    a, b = cs
    lo, hi = (a, b) if a[1] < b[1] else (b, a)
    rods.append(dict(i=s["i"], bottom=lo, top=hi, length=math.dist(a, b)))
assert len(rods) == 6, f"找到 {len(rods)} 根連桿，預期 6"

# ── 螺桿（模組軸向） ────────────────────────────────────
screws = [s for s in d if max(size(s)) > 100 and s["kinds"].get("CYLINDER", 0) > 60 and not s["sph"]]
assert len(screws) == 6, f"找到 {len(screws)} 支螺桿"

def line_dist(p, s):
    """點到螺桿軸線的垂直距離（水平面上）"""
    c, u = center(s), unit(s["cyl"][0]["d"])
    rel = sub(p, c)
    perp = sub(rel, [dot(rel, u) * x for x in u])
    return math.hypot(perp[0], perp[2])


legs = []
for r in rods:
    # 球心在滑座上方（球窩座墊高），所以只比水平位置
    carriage = next(s for s in d if s["vol"] > 10000 and s["max"][1] < r["bottom"][1]
                    and all(s["min"][k] - 0.5 <= r["bottom"][k] <= s["max"][k] + 0.5 for k in (0, 2)))
    cc = center(carriage)
    # 滑座可能離螺桿中點很遠，平行的另一支螺桿中心反而更近；要看到軸線的垂直距離
    screw = min(screws, key=lambda s: line_dist(cc, s))
    u = unit(screw["cyl"][0]["d"])
    if dot(u, [-cc[0], 0, -cc[2]]) < 0:   # 正方向：往機器人中心（滑座往內 → 平台升高）
        u = [-x for x in u]
    legs.append(dict(rod=r, carriage=carriage["i"], screw=screw["i"], u=u, center=cc))

# 腿依方位角排序（俯視逆時針），Leg1..6
legs.sort(key=lambda g: math.atan2(g["rod"]["bottom"][2], g["rod"]["bottom"][0]))

# ── 平台 ────────────────────────────────────────────────
top_y = min(r["top"][1] for r in rods)
platform = [s["i"] for s in d if center(s)[1] > top_y]
platform_center = [0.0, top_y, 0.0]
for k in (0, 2):
    platform_center[k] = sum(r["top"][k] for r in rods) / 6

# ── 每支模組的零件 ──────────────────────────────────────
assign = {}
for i in platform:
    assign[i] = "Platform"
for n, g in enumerate(legs, 1):
    assign[g["rod"]["i"]] = f"Rod{n}"
def axial_of(s, g):
    return dot(sub(center(s), center(by_i[g["screw"]])), g["u"])


def aligned(g):
    return max(abs(x) for x in g["u"]) > 0.999   # 軸向對齊座標軸：包圍盒在軸向上的長度才準


# 六支模組是同一個零件：在對齊座標軸的模組上量滑座長度、端座位置、行程，再套用到全部
ref = next(g for g in legs if aligned(g))
ref_members = []
for s in d:
    rel = sub(center(s), center(by_i[ref["screw"]]))
    ax = dot(rel, ref["u"])
    perp = sub(rel, [ax * x for x in ref["u"]])
    if math.hypot(perp[0], perp[2]) < 12 and abs(perp[1]) < 30:
        ref_members.append((s, ax))
k = max(range(3), key=lambda k: abs(ref["u"][k]))
def extent(s): return (s["max"][k] - s["min"][k]) / 2
car = by_i[ref["carriage"]]
car_half = extent(car)
car_ax = axial_of(car, ref)
ends = sorted((ax, s) for s, ax in ref_members if s["vol"] > 10000 and s["i"] != car["i"] and abs(ax) < 100)
outer_ax, outer_s = max((e for e in ends if e[0] < car_ax), key=lambda e: e[0])
inner_ax, inner_s = min((e for e in ends if e[0] > car_ax), key=lambda e: e[0])
outer_face = outer_ax + extent(outer_s)
inner_face = inner_ax - extent(inner_s)
HOME = round(car_ax - car_half - outer_face, 2)                 # 0＝貼著外側（馬達端）端座
TRAVEL = round(inner_face - outer_face - 2 * car_half, 2)
print(f"模組量測（Leg 參考）：滑座半長 {car_half}，外端座面 {outer_face:.1f}，內端座面 {inner_face:.1f} → home {HOME}，行程 {TRAVEL}")

for n, g in enumerate(legs, 1):
    u, sc = g["u"], center(by_i[g["screw"]])
    cax = axial_of(by_i[g["carriage"]], g)
    members = []
    for s in d:
        if s["i"] in assign:
            continue
        rel = sub(center(s), sc)
        ax = dot(rel, u)
        perp = sub(rel, [ax * x for x in u])
        if math.hypot(perp[0], perp[2]) < 16 and abs(perp[1]) < 30 and -130 < ax < 100:
            members.append((s, ax))
    motor = min((s for s, a in members if s["vol"] > 20000), key=lambda t: axial_of(t, g))   # 最外端的大方塊
    motor_ax = axial_of(motor, g)
    for s, ax in members:
        if s["i"] == g["screw"]:
            kind = "Screw"
        elif max(size(s)) > 100:
            kind = "Base"                                  # 導桿
        elif abs(ax - cax) <= car_half + 1:
            kind = "Carriage"
        elif s["i"] == motor["i"] or ax < motor_ax - 5:
            kind = "Motor"                                 # 馬達本體與尾蓋
        else:
            kind = "Base"
        assign[s["i"]] = f"Leg{n}_{kind}"
    # 同一個零件：端座軸向位置應該跟參考模組一樣（行程才能共用）
    ends_here = sorted(round(a, 1) for s, a in members if s["vol"] > 10000 and s["i"] != g["carriage"] and s["i"] != motor["i"])
    assert abs(round(cax - car_ax, 1)) < 0.2, f"Leg{n} 滑座位置跟參考模組不同：{cax} vs {car_ax}"
    g["home"], g["travel"] = HOME, TRAVEL
    print(f"Leg{n}: 端座軸向 {ends_here}")

missing = [s["i"] for s in d if s["i"] not in assign]
print("未分群：", missing)

# ── groups.json ─────────────────────────────────────────
groups, pivots = {}, {}
for i, name in assign.items():
    groups.setdefault(name, []).append(i)
for n, g in enumerate(legs, 1):
    pivots[f"Leg{n}_Carriage"] = center(by_i[g["carriage"]])
    pivots[f"Leg{n}_Screw"] = center(by_i[g["screw"]])
    pivots[f"Leg{n}_Motor"] = pivots[f"Leg{n}_Base"] = center(by_i[g["screw"]])
    pivots[f"Rod{n}"] = g["rod"]["bottom"]
pivots["Platform"] = platform_center
json.dump(dict(groups=groups, pivots=pivots), open(out / "groups.json", "w"), indent=1)

# ── twin.json（Unity TwinBuild 讀） ─────────────────────
L = legs[0]["rod"]["length"]
twin = dict(
    name="ParallelRobot", root="FG_Robot", model="ParallelRobot",
    devices=[], statics=[], axes=[], free=[],
    custom=dict(type="PythonLink.ParallelRobot6", platform="Platform", rodLength=round(L, 3),
                platformCenter=platform_center, legs=[], pose=[]),
)
for n, g in enumerate(legs, 1):
    twin["devices"].append(dict(name=f"M_Leg{n}", type="DrivePosition", speed=400, mesh=f"Leg{n}_Motor"))
    twin["statics"].append(dict(name=f"Base_Leg{n}", mesh=f"Leg{n}_Base"))
    twin["axes"].append(dict(name=f"Leg{n}_Carriage", mesh=f"Leg{n}_Carriage", actor=f"M_Leg{n}", type="translation",
                             dir=g["u"], factor=0.001, offset=0))   # 0＝CAD 原始位置（平台置中），往內為正
    twin["axes"].append(dict(name=f"Leg{n}_Screw", mesh=f"Leg{n}_Screw", actor=f"M_Leg{n}", type="rotation",
                             dir=g["u"], factor=36.0, offset=0))
    twin["free"].append(dict(name=f"Rod{n}", mesh=f"Rod{n}"))
    twin["custom"]["legs"].append(dict(carriage=f"Leg{n}_Carriage", rod=f"Rod{n}", bottom=g["rod"]["bottom"],
                                       top=g["rod"]["top"], u=g["u"], home=g["home"], travel=g["travel"],
                                       range=[-g["home"], round(g["travel"] - g["home"], 2)]))
twin["free"].append(dict(name="Platform", mesh="Platform"))
twin["pivots"] = [dict(mesh=k, p=v) for k, v in pivots.items()]   # Unity 端用來確認 CAD→Unity 的座標對應
for axis in ("X", "Y", "Z", "Roll", "Pitch", "Yaw"):
    name = f"B_Pose{axis}"
    twin["devices"].append(dict(name=name, type="SensorAnalog"))
    twin["custom"]["pose"].append(name)
json.dump(twin, open(out / "twin.json", "w"), indent=1)

for n, g in enumerate(legs, 1):
    kinds = {}
    for i, name in assign.items():
        if name.startswith(f"Leg{n}_"):
            k = name.split("_")[1]; kinds[k] = kinds.get(k, 0) + 1
    print(f"Leg{n}: u={[round(x, 3) for x in g['u']]} home={g['home']} travel={g['travel']} parts={kinds}")
print("Platform", len(platform), "parts; rod length", round(L, 3), "; platform center", platform_center)
