"""Haas VF-2（Autodesk Fusion 機台庫的模擬模型）→ 群組 STL＋twin.json。

    python cad/haas_vf2/make_vf2.py fetch  <輸出目錄>     # 從 cam.autodesk.com 下載 haas vf-2.f3d／.mch（公開，不用登入）
    python cad/haas_vf2/make_vf2.py fusion <輸出目錄>     # 裝一次性 Fusion add-in → 開 Fusion → 等它匯出 STEP → 關 Fusion、移除 add-in
    python cad/haas_vf2/make_vf2.py build  <輸出目錄>     # STEP → 群組 STL（Y 朝上）＋刀具／治具／素材＋twin.json
    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python cad/to_fbx.py -- <輸出目錄>/stl <輸出目錄>/HaasVF2.fbx <輸出目錄>/preview.png
    cp <輸出目錄>/HaasVF2.fbx Unity/Assets/HaasVF2/Models/ ; cp <輸出目錄>/twin.json Unity/Assets/HaasVF2/

.f3d 是 Fusion 專有格式（ShapeManager 實體），只有 Fusion 打得開，所以要經過 Fusion 匯出一次 STEP。
機台模型的元件已經照運動學拆好：Static、X-Axis、Y-Axis、Z-Axis、Spindle（.mch 的 kinematics 寫明 Y 帶 X、Z 帶主軸）。
Fusion 匯出是 Z 朝上、mm；工作台面在 z=0，主軸鼻端在 z=610（.mch 的 head attach_frame）。
模型沒有刀庫：孿生用手動換刀（Q_ToolSelect＝刀號，主軸停著時換上）。

模型來源與授權：Autodesk Fusion Machine Library（https://cam.autodesk.com/machineslist）的機台模擬模型，
給 Fusion 使用者做機台模擬用；原檔與轉出的 STL／FBX 都不進 repo。公開部署前先確認授權。
"""
import json
import os
import shutil
import struct
import subprocess
import sys
import time
import urllib.parse
import urllib.request

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "vmc"))
import make_vmc as V  # noqa: E402  box、cone、tool_mesh、write_stl、to_cad、刀具表

LIB = "https://cam.autodesk.com/machines/download.php?name="
F3D, MCH = "haas vf-2.f3d", "haas vf-2.mch"
STEP = "haas_vf2.step"
GAUGE = np.array([0.0, 0.0, 610.0])          # 主軸鼻端（Z=0 時），.mch head attach_frame
RISER = (180.0, 140.0, 70.0)
STOCK = (160.0, 120.0, 40.0)
TRAVEL = {"X": [-381, 381], "Y": [-203, 203], "Z": [-508, 0]}
RULES = {                                     # 元件路徑 → 群組（assembly_groups.py 的規則格式）
    "Spindle": ["/Spindle$"],
    "Head": ["/Z-Axis"],
    "Table": ["/X-Axis$"],
    "Saddle": ["/Y-Axis$"],
    "Pendant": ["/Static#3$"],
    "Enclosure": ["/Static#2$", "/Static#[456]$"],
    "Base": ["/Static#1$"],
}
COLORS = {"Base": (0.82, 0.83, 0.84), "Enclosure": (0.86, 0.87, 0.88), "Pendant": (0.18, 0.18, 0.2),
          "Head": (0.25, 0.27, 0.3), "Spindle": (0.7, 0.71, 0.73), "Table": (0.56, 0.58, 0.61), "Saddle": (0.4, 0.42, 0.45),
          "Riser": (0.25, 0.35, 0.55), "Stock": (0.8, 0.82, 0.85)}


def fetch(out):
    src = os.path.join(out, "src")
    os.makedirs(src, exist_ok=True)
    for name in (F3D, MCH):
        path = os.path.join(src, name)
        if not os.path.exists(path):
            print("下載", name)
            req = urllib.request.Request(LIB + urllib.parse.quote(name), headers={"User-Agent": "Mozilla/5.0"})
            with urllib.request.urlopen(req) as r, open(path, "wb") as f:
                f.write(r.read())
    kin = json.load(open(os.path.join(src, MCH), encoding="utf-8"))["kinematics"]["default"]["parts"]
    print("運動學：", [(p.get("id"), p.get("direction"), p.get("min"), p.get("max")) for p in kin if p.get("type") == "linear"])


def fusion(out, remove=False):
    addins = os.path.join(os.environ["APPDATA"], "Autodesk", "Autodesk Fusion 360", "API", "AddIns", "DTExportVF2")
    if remove:
        shutil.rmtree(addins, ignore_errors=True)
        print("已移除 add-in")
        return
    dst = os.path.join(out, "fusion")
    shutil.rmtree(dst, ignore_errors=True)
    shutil.rmtree(addins, ignore_errors=True)
    shutil.copytree(os.path.join(HERE, "fusion_export"), addins)
    json.dump({"out": os.path.abspath(dst), "f3d": os.path.abspath(os.path.join(out, "src", F3D)), "step": STEP},
              open(os.path.join(addins, "config.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    launcher = next((os.path.join(r, "FusionLauncher.exe") for r, _, fs in
                     os.walk(os.path.join(os.environ["LOCALAPPDATA"], "Autodesk", "webdeploy", "production")) if "FusionLauncher.exe" in fs), None)
    if not launcher:
        raise SystemExit("找不到 Fusion（FusionLauncher.exe）；add-in 已裝好，自己開 Fusion 也會執行")
    print("開 Fusion：", launcher, "（有「Recovered Documents」對話框時按 Close，add-in 才會繼續）")
    subprocess.Popen([launcher])
    t0 = time.time()
    while not os.path.exists(os.path.join(dst, "DONE")):
        if time.time() - t0 > 900:
            raise SystemExit("等 Fusion 匯出逾時；看 " + os.path.join(dst, "log.txt"))
        time.sleep(3)
    print(open(os.path.join(dst, "log.txt"), encoding="utf-8").read())
    shutil.rmtree(addins, ignore_errors=True)
    print("已移除 add-in；Fusion 還開著，請自己關（裡面沒有要存的文件）")


def read_stl(path):
    data = open(path, "rb").read()
    if data[:5] == b"solid" and b"facet" in data[:400]:
        v = [list(map(float, l.split()[1:4])) for l in data.decode("ascii", "ignore").splitlines() if l.strip().startswith("vertex")]
        return np.array(v).reshape(-1, 3, 3)
    n = struct.unpack("<I", data[80:84])[0]
    arr = np.frombuffer(data[84:84 + n * 50], dtype=np.dtype([("n", "<3f4"), ("v", "<9f4"), ("a", "<u2")]))
    return arr["v"].reshape(-1, 3, 3).astype(float)


def build(out):
    import assembly_groups as AG
    from OCP.BRepMesh import BRepMesh_IncrementalMesh
    from OCP.StlAPI import StlAPI_Writer

    step = os.path.join(out, "fusion", STEP)
    if not os.path.exists(step):
        raise SystemExit(f"沒有 {step}：先跑 fetch、fusion")
    stl = os.path.join(out, "stl")
    raw = os.path.join(out, "raw")
    shutil.rmtree(stl, ignore_errors=True)
    os.makedirs(stl)
    os.makedirs(raw, exist_ok=True)
    import re
    compiled = [(g, [re.compile(r, re.I) for r in rs]) for g, rs in RULES.items()]
    groups = {}
    for path, shape in AG.solids_of(step):
        g = next((g for g, pats in compiled if any(pt.search(path) for pt in pats)), None)
        if g is None:
            raise SystemExit(f"沒有規則收 {path}")
        groups.setdefault(g, []).append(shape)
    meshes, pivots, report = {}, {}, {}
    for g, shapes in groups.items():
        c = AG.compound(shapes)
        BRepMesh_IncrementalMesh(c, 0.5, False, 0.5, True)
        p = os.path.join(raw, g + ".stl")
        StlAPI_Writer().Write(c, p)
        # Fusion 的曲面實體（Z-Axis、外罩）法線方向不一致：Blender 雙面渲染看不出來，Unity 只畫正面會缺一塊 → 正反面都放
        # 反向那份沿法線往內偏 0.2 mm：頂點完全相同的話 Blender 匯入 STL 會把它當重複面合併掉
        t = read_stl(p)
        n = np.cross(t[:, 1] - t[:, 0], t[:, 2] - t[:, 0])
        n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)
        meshes[g] = np.concatenate([t, t[:, ::-1] - 0.2 * n[:, None, :]])
        report[g] = {"shapes": len(shapes), "bbox": AG.bbox(shapes)}
        print(f"{g}: {len(shapes)} 個實體，{len(meshes[g])} 面，bbox {report[g]['bbox']}")

    rx, ry, rz = RISER
    sx, sy, sz = STOCK
    meshes["Riser"] = V.box(-rx / 2, -ry / 2, 0, rx / 2, ry / 2, rz)
    meshes["Stock"] = V.box(-sx / 2, -sy / 2, rz, sx / 2, sy / 2, rz + sz)
    tools = []
    for i, (d, length, flute, name) in enumerate(V.TOOLS, 1):
        holder, cutter = V.tool_mesh(d, length, flute)
        meshes[f"Tool{i}_Holder"], meshes[f"Tool{i}_Cutter"] = holder + GAUGE, cutter + GAUGE
        pivots[f"Tool{i}_Holder"] = pivots[f"Tool{i}_Cutter"] = GAUGE.tolist()
        tools.append({"name": f"T{i}", "label": name, "diameter": d, "length": length, "flute": flute, "inSpindle": i == 1,
                      "holder": f"Tool{i}_Holder", "cutter": f"Tool{i}_Cutter", "pocket": i})
    bb = lambda g: report[g]["bbox"]
    center = lambda b: [(b[0] + b[3]) / 2, (b[1] + b[4]) / 2, (b[2] + b[5]) / 2]
    pivots.update({"Base": [0, 0, 0], "Enclosure": [0, 0, 0], "Pendant": center(bb("Pendant")),
                   "Head": GAUGE.tolist(), "Spindle": [0, 0, (bb("Spindle")[2] + bb("Spindle")[5]) / 2],
                   "Table": [0, 0, 0], "Saddle": center(bb("Saddle")), "Riser": [0, 0, rz], "Stock": [0, 0, rz + sz]})
    colors = dict(COLORS)
    for i in range(1, len(V.TOOLS) + 1):
        colors[f"Tool{i}_Holder"], colors[f"Tool{i}_Cutter"] = (0.16, 0.16, 0.18), (0.55, 0.57, 0.6)
    for n, t in meshes.items():
        V.write_stl(os.path.join(stl, n + ".stl"), t)
    json.dump({k: V.to_cad(v).tolist() for k, v in pivots.items()}, open(os.path.join(stl, "pivots.json"), "w"), indent=1)
    json.dump(colors, open(os.path.join(stl, "colors.json"), "w"), indent=1)

    cad = lambda p: [round(float(v), 3) for v in V.to_cad(p)]
    dirc = lambda d: [float(v) for v in V.to_cad(d)]
    twin = {
        "name": "HaasVF2", "root": "FG_VF2", "model": "HaasVF2",
        "devices": [
            {"name": "M_AxisX", "type": "DrivePosition", "speed": 300},
            {"name": "M_AxisY", "type": "DrivePosition", "speed": 300},
            {"name": "M_AxisZ", "type": "DrivePosition", "speed": 300},
            {"name": "M_Spindle", "type": "DriveSpeed", "accel": 4000},
            {"name": "Q_ToolSelect", "type": "DrivePosition", "speed": 1000},
            {"name": "Q_ResetStock", "type": "Lamp"},
            {"name": "Q_ResetAlarm", "type": "Lamp"},
            {"name": "B_Tool", "type": "SensorAnalog"},
            {"name": "B_Alarm", "type": "SensorAnalog"},
            {"name": "B_CutVolume", "type": "SensorAnalog"},
        ],
        "statics": [
            {"name": "Machine_Base", "mesh": "Base"},
            {"name": "Enclosure", "mesh": "Enclosure"},
            {"name": "Pendant", "mesh": "Pendant"},
            {"name": "Fixture", "mesh": "Riser", "parent": "Table_X"},
        ],
        # .mch：Y 軸方向 (0,-1,0)、X 軸方向 (-1,0,0)（工作台動）、Z 軸 (0,0,1)（主軸頭動）
        "axes": [
            {"name": "Saddle_Y", "mesh": "Saddle", "actor": "M_AxisY", "type": "translation", "dir": dirc([0, -1, 0]), "factor": 0.001, "offset": 0},
            {"name": "Table_X", "mesh": "Table", "actor": "M_AxisX", "type": "translation", "dir": dirc([-1, 0, 0]), "factor": 0.001, "offset": 0,
             "parent": "Saddle_Y"},
            {"name": "Head_Z", "mesh": "Head", "actor": "M_AxisZ", "type": "translation", "dir": dirc([0, 0, 1]), "factor": 0.001, "offset": 0},
            {"name": "Spindle", "mesh": "Spindle", "actor": "M_Spindle", "type": "rotation", "dir": dirc([0, 0, 1]), "factor": 6, "offset": 0,
             "mode": "speed", "parent": "Head_Z"},
        ],
        "custom": {
            "type": "PythonLink.VmcToolChanger",
            "spindleGauge": cad(GAUGE), "pocketList": [], "toolList": tools,
            "stockSize": list(STOCK), "stockOrigin": cad([0, 0, rz]), "stockCell": 1.5, "changeZ": 0,
            "spindle": "M_Spindle", "carousel": "", "zAxis": "M_AxisZ", "arm": "", "release": "", "lockPin": "",
            "manualSelect": "Q_ToolSelect", "resetStock": "Q_ResetStock", "resetAlarm": "Q_ResetAlarm",
            "toolSensor": "B_Tool", "alarmSensor": "B_Alarm", "volumeSensor": "B_CutVolume",
            "fixture": "Fixture", "spindleNode": "Spindle", "carouselNode": "", "stockMaterialMesh": "Stock",
        },
        "travel": TRAVEL,
        "camera": [20, 145],          # 正面（操作面板那側，Fusion 的 -y）在 Unity 的 +z：鏡頭從前面斜看
        "pivots": [{"mesh": n, "p": V.to_cad(p).tolist()} for n, p in pivots.items() if n in ("Head", "Table", "Saddle", "Spindle", "Riser", "Pendant")],
    }
    json.dump(twin, open(os.path.join(out, "twin.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"{len(meshes)} 個網格、twin.json 寫好")


if __name__ == "__main__":
    cmd, out = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 and not sys.argv[2].startswith("--") else None
    if cmd == "fetch":
        fetch(out)
    elif cmd == "fusion":
        fusion(out, remove="--remove" in sys.argv)
    elif cmd == "build":
        build(out)
    else:
        raise SystemExit(__doc__)
