"""LinuxCNC 的 VMC（立式加工中心＋傘式刀庫）→ 群組 STL＋twin.json。

    python cad/vmc/make_vmc.py <輸出目錄>
    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python cad/to_fbx.py -- <輸出目錄>/stl <輸出目錄>/VMC.fbx <輸出目錄>/preview.png
    cp <輸出目錄>/VMC.fbx Unity/Assets/VMC/Models/ ; cp <輸出目錄>/twin.json Unity/Assets/VMC/

來源：LinuxCNC configs/sim/axis/vismach/VMC_toolchange（GPL-2.0-or-later，Alex Joni 2009 等）。
STL 從 GitHub 固定版本下載，不放進 repo。vmcgui 這支 vismach 腳本就是整台的運動學樹，這裡照抄：

    base ─┬─ head（Z）─┬─ dogs（主軸旋轉）
          │            └─ drawbar（拉刀桿）
          ├─ saddle（Y）── table（X）
          └─ arm（換刀臂擺動）─┬─ carousel（刀庫旋轉，10 刀位）
                                └─ lock（刀庫鎖銷）

vismach 是 Z 朝上、1 單位＝10 mm；這裡換成 mm，輸出時再轉成 CAD 慣例的 Y 朝上（to_fbx.py 預設 --up Y）。
刀具、刀把、鎖銷、拉刀桿、主軸驅動塊、治具墊塊是程式產生的（vismach 本來就用 Box／Cylinder 畫）。
"""
import json
import math
import os
import struct
import sys
import urllib.request

import numpy as np

COMMIT = "f7ccc4fc9101c54cc074eacd410374c714fac8f5"
SRC = f"https://raw.githubusercontent.com/LinuxCNC/linuxcnc/{COMMIT}/configs/sim/axis/vismach/VMC_toolchange/"
U = 10.0                                    # vismach 1 單位 = 10 mm

# ── 幾何常數（mm，vismach 座標：Z 朝上） ─────────────────────────────────
SPINDLE_XY = np.array([-1 * U, 49 * U])     # dogs／drawbar 的 Translate(-1, 49)
GAUGE_Z = (90.97 + 4) * U                   # 主軸鼻端（head.stl 底面＋Translate 4）＝刀把基準面，Z=0 時
CAROUSEL_PIVOT = np.array([19.689 * U, 43.93 * U])
ARM_IN, ARM_OUT = 5.0, -20.0                # 換刀臂角度（度），sim_vmc.hal 的 armpos
CHANGE_Z = -100.0                           # 換刀高度（toolchange.ngc 的 G53 G0 Z-100）
POCKETS = 10
SLOT0 = 16.5                                # 刀庫開口槽的角度：16.5 + 36k（從 carousel.stl 量出）
TABLE_TOP = 38.0 * U
RISER = (180.0, 140.0, 70.0)                # 治具墊塊 長×寬×高
STOCK = (160.0, 120.0, 40.0)                # 工件（素材）長×寬×高；網格由 Unity 的 Workpiece 元件即時產生

# 機台零點的擺法：X=Y=0 時工件中心在主軸正下方（vismach 原本的工作台偏在前面，主軸只碰得到後緣）
TABLE_CENTER = np.array([(-48.63 + 51.37) / 2 * U, ((39.95 + 69.95) / 2 + 8) * U])
SHIFT = SPINDLE_XY - TABLE_CENTER           # 鞍座＋工作台整組平移
TRAVEL = {"X": (-300, 300), "Y": (-100, 100), "Z": (-400, 0)}

# 刀具表：直徑、全長（從刀把基準面量到刀尖）、刃長、名稱
TOOLS = [
    (10, 100, 30, "Ø10 平銑刀"), (6, 95, 20, "Ø6 平銑刀"), (16, 105, 35, "Ø16 平銑刀"), (8, 110, 45, "Ø8 鑽頭"),
    (50, 80, 8, "Ø50 面銑刀"), (4, 90, 12, "Ø4 平銑刀"), (12, 100, 30, "Ø12 平銑刀"), (20, 100, 35, "Ø20 平銑刀"),
    (3, 85, 6, "Ø3 中心鑽"), (25, 95, 30, "Ø25 平銑刀"),
]


def fetch(name, cache):
    os.makedirs(cache, exist_ok=True)
    path = os.path.join(cache, name)
    if not os.path.exists(path):
        print("下載", SRC + name)
        urllib.request.urlretrieve(SRC + name, path)
    return path


def read_ascii_stl(path):
    v = [list(map(float, l.split()[1:4])) for l in open(path) if l.strip().startswith("vertex")]
    return np.array(v).reshape(-1, 3, 3)


def to_cad(p):
    """vismach（Z 朝上）→ CAD（Y 朝上）：(x, y, z) → (x, z, -y)"""
    p = np.asarray(p, float)
    return np.stack([p[..., 0], p[..., 2], -p[..., 1]], -1)


def write_stl(path, tris):
    tris = to_cad(tris)
    with open(path, "wb") as f:
        f.write(b"vmc".ljust(80, b" "))
        f.write(struct.pack("<I", len(tris)))
        for t in tris:
            n = np.cross(t[1] - t[0], t[2] - t[0])
            n = n / (np.linalg.norm(n) or 1)
            f.write(struct.pack("<12fH", *n, *t[0], *t[1], *t[2], 0))


def box(x0, y0, z0, x1, y1, z1):
    c = np.array([[x0, y0, z0], [x1, y0, z0], [x1, y1, z0], [x0, y1, z0],
                  [x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1]])
    f = [(0, 2, 1), (0, 3, 2), (4, 5, 6), (4, 6, 7), (0, 1, 5), (0, 5, 4),
         (1, 2, 6), (1, 6, 5), (2, 3, 7), (2, 7, 6), (3, 0, 4), (3, 4, 7)]
    return np.array([[c[i], c[j], c[k]] for i, j, k in f])


def cone(cx, cy, z0, r0, z1, r1, n=32):
    """z0→z1 的截錐（含上下蓋），外法線"""
    out = []
    for i in range(n):
        a, b = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
        p0 = [cx + r0 * math.cos(a), cy + r0 * math.sin(a), z0]
        p1 = [cx + r0 * math.cos(b), cy + r0 * math.sin(b), z0]
        q0 = [cx + r1 * math.cos(a), cy + r1 * math.sin(a), z1]
        q1 = [cx + r1 * math.cos(b), cy + r1 * math.sin(b), z1]
        quad = (p0, p1, q1, q0) if z1 > z0 else (q0, q1, p1, p0)
        out += [[quad[0], quad[1], quad[2]], [quad[0], quad[2], quad[3]]]
        if r0 > 0:
            out.append([[cx, cy, z0], p1, p0] if z1 > z0 else [[cx, cy, z0], p0, p1])
        if r1 > 0:
            out.append([[cx, cy, z1], q0, q1] if z1 > z0 else [[cx, cy, z1], q1, q0])
    return np.array(out)


def tool_mesh(d, length, flute):
    """原點＝刀把基準面中心（主軸鼻端）；刀把錐柄往上、刀身往下。"""
    holder = np.concatenate([
        cone(0, 0, 0, 22, 38, 12),            # BT 錐柄（夾在主軸裡，刀庫上看得到）
        cone(0, 0, -16, 32, 0, 32),           # 法蘭（V 溝，刀庫爪抓這裡）
        cone(0, 0, -40, 20, -16, 26),         # 刀把本體
    ])
    shank = cone(0, 0, -(length - flute), d / 2, -40, min(d / 2, 18), 24)
    if "鑽" in TOOLS_NAME.get((d, length), ""):
        cutter = np.concatenate([cone(0, 0, -(length - 0.29 * d), d / 2, -(length - flute), d / 2, 24),
                                 cone(0, 0, -length, 0, -(length - 0.29 * d), d / 2, 24)])
    else:
        cutter = cone(0, 0, -length, d / 2, -(length - flute), d / 2, 24)
    return np.concatenate([holder, shank]), cutter


TOOLS_NAME = {(d, l): n for d, l, _, n in TOOLS}


def pocket_angle(p):
    """刀位 p（1～10）在刀庫上的角度（刀庫值 0、換刀臂角度 0 時，vismach 座標，繞刀庫中心）"""
    return SLOT0 + 36 * 4 - 36 * (p - 1)


def rot(v, deg):
    a = math.radians(deg)
    return np.array([v[0] * math.cos(a) - v[1] * math.sin(a), v[0] * math.sin(a) + v[1] * math.cos(a)])


def main():
    out = sys.argv[1]
    cache, stl = os.path.join(out, "src"), os.path.join(out, "stl")
    os.makedirs(stl, exist_ok=True)
    meshes, pivots = {}, {}

    def load(name, dx=0, dy=0, dz=0):
        t = read_ascii_stl(fetch(name + ".stl", cache)) * U
        return t + np.array([dx, dy, dz])

    sx, sy = SHIFT
    meshes["Base"] = load("base")
    meshes["Head"] = load("head", dz=4 * U)
    meshes["Saddle"] = load("saddle", sx, 7 * U + sy)
    meshes["Table"] = load("table", sx, 8 * U + sy)
    meshes["Arm"] = load("arm")
    meshes["Carousel"] = load("carousel")
    cx, cy = SPINDLE_XY
    meshes["SpindleDogs"] = box(cx - 60, cy - 30, 940, cx + 60, cy + 30, 1000)
    meshes["Drawbar"] = cone(cx, cy, 1200, 30, 1250, 30)
    meshes["LockPin"] = box(180, 420, 850, 200, 480, 900)
    rx, ry, rz = RISER
    meshes["Riser"] = box(cx - rx / 2, cy - ry / 2, TABLE_TOP, cx + rx / 2, cy + ry / 2, TABLE_TOP + rz)
    sx_, sy_, sz_ = STOCK            # 素材：只用來帶材質（Unity 的 Workpiece 元件自己產生網格），預覽圖上也看得到
    meshes["Stock"] = box(cx - sx_ / 2, cy - sy_ / 2, TABLE_TOP + rz, cx + sx_ / 2, cy + sy_ / 2, TABLE_TOP + rz + sz_)

    # 刀位：換刀臂在「進」、刀庫值 0 時，刀位 1 正對主軸。刀位中心半徑＝刀庫中心到主軸的距離（擺進去之後）
    spindle_in_arm = rot(SPINDLE_XY, -ARM_IN)                     # 主軸位置換到換刀臂座標（臂角 0）
    vec = spindle_in_arm - CAROUSEL_PIVOT
    pocket_r = float(np.hypot(*vec))
    align = math.degrees(math.atan2(vec[1], vec[0])) - pocket_angle(1)   # 刀庫要轉多少度，槽才正對主軸
    pocket_z = GAUGE_Z + CHANGE_Z                                  # Z＝換刀高度時主軸鼻端的高度
    pockets = []                                                   # 網格烘焙時的座標：臂在「出」、刀庫還沒轉
    for p in range(1, POCKETS + 1):
        a = math.radians(pocket_angle(p))
        xy = CAROUSEL_PIVOT + pocket_r * np.array([math.cos(a), math.sin(a)])
        pockets.append([*rot(xy, ARM_OUT), pocket_z])
    # 刀具一開始的位置：T1 在主軸上，T2～T10 在刀位 2～10（刀位 1 空著，等 T1 換回來）
    # 刀具網格放在臂角「出」、刀庫值 0 的實際位置
    tools = []
    for i, (d, length, flute, name) in enumerate(TOOLS, 1):
        holder, cutter = tool_mesh(d, length, flute)
        if i == 1:
            at = np.array([cx, cy, GAUGE_Z])
        else:
            at = np.array(pockets[i - 1])
        meshes[f"Tool{i}_Holder"] = holder + at
        meshes[f"Tool{i}_Cutter"] = cutter + at
        pivots[f"Tool{i}_Holder"] = pivots[f"Tool{i}_Cutter"] = at.tolist()
        tools.append({"name": f"T{i}", "label": name, "diameter": d, "length": length, "flute": flute, "inSpindle": i == 1,
                      "holder": f"Tool{i}_Holder", "cutter": f"Tool{i}_Cutter", "pocket": i})

    # 原點：旋轉件放在轉軸上
    pivots["Arm"] = [0, 0, 850]
    pivots["Carousel"] = [*CAROUSEL_PIVOT, 835]
    pivots["LockPin"] = [190, 450, 875]
    pivots["SpindleDogs"] = [cx, cy, 970]
    pivots["Drawbar"] = [cx, cy, 1225]
    pivots["Head"] = [cx, cy, GAUGE_Z]
    pivots["Riser"] = [cx, cy, TABLE_TOP + rz]
    pivots["Table"] = [cx, cy, TABLE_TOP]
    pivots["Saddle"] = [cx, cy, 315]
    pivots["Base"] = [0, 0, 0]

    # 換刀臂擺到「出」：臂、刀庫、鎖銷的網格放在「出」的位置（機台開機時的狀態）
    for n in ("Arm", "Carousel", "LockPin"):
        t = meshes[n].copy()
        xy = t[..., :2].reshape(-1, 2)
        t[..., :2] = np.array([rot(p, ARM_OUT) for p in xy]).reshape(t[..., :2].shape)
        meshes[n] = t
        pv = np.array(pivots[n])
        pivots[n] = [*rot(pv[:2], ARM_OUT), pv[2]]

    total = 0
    for n, t in meshes.items():
        write_stl(os.path.join(stl, n + ".stl"), t)
        total += len(t)
    json.dump({k: to_cad(v).tolist() for k, v in pivots.items()}, open(os.path.join(stl, "pivots.json"), "w"), indent=1)
    colors = {"Base": (0.80, 0.82, 0.80), "Head": (0.22, 0.24, 0.27), "Saddle": (0.33, 0.52, 0.58), "Table": (0.60, 0.62, 0.64),
              "Arm": (0.30, 0.32, 0.35), "Carousel": (0.85, 0.45, 0.10), "Riser": (0.25, 0.35, 0.55), "LockPin": (0.95, 0.80, 0.10),
              "Drawbar": (0.90, 0.40, 0.60), "SpindleDogs": (0.90, 0.90, 0.90), "Stock": (0.80, 0.82, 0.85)}
    for i in range(1, len(TOOLS) + 1):
        colors[f"Tool{i}_Holder"] = (0.16, 0.16, 0.18)
        colors[f"Tool{i}_Cutter"] = (0.55, 0.57, 0.6)
    json.dump(colors, open(os.path.join(stl, "colors.json"), "w"), indent=1)
    print(f"{len(meshes)} 個網格，共 {total} 面；刀位半徑 {pocket_r:.1f} mm，對位修正 {align:+.2f}°，"
          f"平移鞍座／工作台 {SHIFT.round(1)} mm")

    def cad(p):
        return [round(float(v), 3) for v in to_cad(p)]

    def dirc(d):
        return [float(v) for v in to_cad(d)]

    twin = {
        "name": "VMC", "root": "FG_VMC", "model": "VMC",
        "devices": [
            {"name": "M_AxisX", "type": "DrivePosition", "speed": 250},
            {"name": "M_AxisY", "type": "DrivePosition", "speed": 250},
            {"name": "M_AxisZ", "type": "DrivePosition", "speed": 250},
            {"name": "M_Spindle", "type": "DriveSpeed", "accel": 4000},
            {"name": "M_Carousel", "type": "DrivePosition", "speed": 2},
            {"name": "Y_Arm", "type": "Cylinder", "time": 2.0},
            {"name": "Y_ToolRelease", "type": "Cylinder", "time": 0.4},
            {"name": "Y_CarouselLock", "type": "Cylinder", "time": 0.3},
            {"name": "Q_ResetStock", "type": "Lamp"},
            {"name": "Q_ResetAlarm", "type": "Lamp"},
            {"name": "B_Tool", "type": "SensorAnalog"},
            {"name": "B_Alarm", "type": "SensorAnalog"},
            {"name": "B_CutVolume", "type": "SensorAnalog"},
        ],
        "statics": [
            {"name": "Machine_Base", "mesh": "Base"},
            {"name": "Fixture", "mesh": "Riser", "parent": "Table_X"},
        ],
        "axes": [
            # 鞍座帶工作台前後（Y），工作台左右（X）；方向照 vismach：X＋→工作台往 +x、Y＋→鞍座往 +y
            {"name": "Saddle_Y", "mesh": "Saddle", "actor": "M_AxisY", "type": "translation", "dir": dirc([0, 1, 0]),
             "factor": 0.001, "offset": 0},
            {"name": "Table_X", "mesh": "Table", "actor": "M_AxisX", "type": "translation", "dir": dirc([1, 0, 0]),
             "factor": 0.001, "offset": 0, "parent": "Saddle_Y"},
            {"name": "Head_Z", "mesh": "Head", "actor": "M_AxisZ", "type": "translation", "dir": dirc([0, 0, 1]),
             "factor": 0.001, "offset": 0},
            {"name": "Spindle", "mesh": "SpindleDogs", "actor": "M_Spindle", "type": "rotation", "dir": dirc([0, 0, 1]),
             "factor": 6, "offset": 0, "mode": "speed", "parent": "Head_Z"},
            {"name": "Drawbar", "mesh": "Drawbar", "actor": "Y_ToolRelease", "type": "translation", "dir": dirc([0, 0, -1]),
             "factor": 0.0005, "offset": 0, "parent": "Head_Z"},
            # 換刀臂：氣缸 0～100 → -20°～+5°；刀庫：每刀位 36°。
            # Unity 是左手座標：繞「朝上」轉正角度，從上往下看是順時針。vismach 的正角度是逆時針，所以 dir 朝下
            {"name": "Arm", "mesh": "Arm", "actor": "Y_Arm", "type": "rotation", "dir": dirc([0, 0, -1]),
             "factor": 0.25, "offset": 0},
            {"name": "Carousel", "mesh": "Carousel", "actor": "M_Carousel", "type": "rotation", "dir": dirc([0, 0, -1]),
             "factor": 36, "offset": round(align / 36, 5), "parent": "Arm"},
            {"name": "LockPin", "mesh": "LockPin", "actor": "Y_CarouselLock", "type": "translation", "dir": dirc([*rot([0, 1], ARM_OUT), 0]),
             "factor": 0.0005, "offset": 0, "parent": "Arm"},
        ],
        "custom": {
            "type": "PythonLink.VmcToolChanger",
            "spindleGauge": cad([cx, cy, GAUGE_Z]),
            "pocketList": [{"p": cad(p)} for p in pockets],
            "toolList": tools,
            "stockSize": list(STOCK), "stockOrigin": cad([cx, cy, TABLE_TOP + rz]), "stockCell": 1.5,
            "changeZ": CHANGE_Z,
            "spindle": "M_Spindle", "carousel": "M_Carousel", "zAxis": "M_AxisZ",
            "arm": "Y_Arm", "release": "Y_ToolRelease", "lockPin": "Y_CarouselLock",
            "resetStock": "Q_ResetStock", "resetAlarm": "Q_ResetAlarm",
            "toolSensor": "B_Tool", "alarmSensor": "B_Alarm", "volumeSensor": "B_CutVolume",
            "fixture": "Fixture", "spindleNode": "Spindle", "carouselNode": "Carousel", "stockMaterialMesh": "Stock",
        },
        "travel": TRAVEL,
        "pivots": [{"mesh": n, "p": to_cad(p).tolist()} for n, p in pivots.items()
                   if n in ("Head", "Table", "Saddle", "Carousel", "Arm", "Riser")],
    }
    # 臂角 0 → 實際開機是「出」(-20°)：Arm axis 的值＝(氣缸 0～100 + offset)×factor，網格已經畫在 -20°，
    # 所以 offset 0 → 角度 0 → 擺出狀態；氣缸 100 → +25° → 擺進
    json.dump(twin, open(os.path.join(out, "twin.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print("twin.json 寫好")


if __name__ == "__main__":
    main()
