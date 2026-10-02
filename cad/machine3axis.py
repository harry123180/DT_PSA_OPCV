"""三軸機台（工作台 X、鞍座 Y、主軸頭 Z 的立式加工中心，或同樣串接的機台）→ twin.json。
吃 assembly_groups.py 的輸出（每群一個 STL ＋ groups_report.json），群組名稱用參數指定。

    python machine3axis.py <群組目錄> --name HaasVF2 --root FG_VF2 \
        --x Table --y Saddle --z Head [--spindle Spindle] [--static Base,Column,...] \
        --travel 762,406,508 [--up Z] [--xdir -1] [--ydir 1] [--zdir 1]

- 串接：鞍座（Y）帶工作台（X），主軸頭（Z）獨立；主軸（可選）掛在主軸頭底下，DriveSpeed 驅動旋轉
- 行程 --travel X,Y,Z（mm，總長）：X、Y 以目前位置為中心（±一半），Z 從目前位置往下（0～-Z）
- 方向：機床慣例是「刀具相對工件」——X＋時工作台往 -X 移動，所以預設 --xdir -1、--ydir -1（鞍座往 -Y）、--zdir 1。
  CAD 的軸向跟機台座標不一定一樣，跑完看建置後的畫面再調
- --up：CAD 的朝上軸（Fusion 360 匯出常見 Z，SolidWorks 是 Y），會寫進 twin.json 的 to_fbx 指令提示
- 原點：各群組包圍盒中心（主軸放在包圍盒中心的 XY、上緣）；寫 pivots.json 給 to_fbx.py
接著：to_fbx.py <群組目錄> <Name>.fbx preview.png --up <up> → 放進 Unity/Assets/<Name>/ → TwinBuild。
"""
import json
import os
import sys


def arg(key, default=None):
    return sys.argv[sys.argv.index(key) + 1] if key in sys.argv else default


def main():
    src = sys.argv[1]
    report = json.load(open(os.path.join(src, "groups_report.json"), encoding="utf-8"))
    name, root = arg("--name", "Machine"), arg("--root", "FG_Machine")
    gx, gy, gz, gs = arg("--x"), arg("--y"), arg("--z"), arg("--spindle")
    up = arg("--up", "Z")
    tx, ty, tz = (float(v) for v in arg("--travel", "500,400,400").split(","))
    sx, sy, sz = float(arg("--xdir", "-1")), float(arg("--ydir", "-1")), float(arg("--zdir", "1"))
    for g in (gx, gy, gz) + ((gs,) if gs else ()):
        if g not in report:
            raise SystemExit(f"groups_report.json 沒有群組 {g}；有：{list(report)}")
    statics = [g for g in report if g not in (gx, gy, gz, gs)]

    def center(g):
        b = report[g]["bbox"]
        return [(b[0] + b[3]) / 2, (b[1] + b[4]) / 2, (b[2] + b[5]) / 2]

    # CAD 座標的軸向：朝上軸是 up，其他兩軸照順序當水平的 X、Y
    if up == "Z":
        ax = {"x": [1, 0, 0], "y": [0, 1, 0], "z": [0, 0, 1]}
    else:                                   # Y 朝上：CAD (x, y, z) 的水平是 x、-z
        ax = {"x": [1, 0, 0], "y": [0, 0, -1], "z": [0, 1, 0]}
    scale = lambda v, k: [c * k for c in v]

    pivots = {g: center(g) for g in report}
    if gs:
        b = report[gs]["bbox"]
        pivots[gs] = [(b[0] + b[3]) / 2, (b[1] + b[4]) / 2, (b[2] + b[5]) / 2]
    json.dump(pivots, open(os.path.join(src, "pivots.json"), "w"), indent=1)

    devices = [{"name": "M_AxisX", "type": "DrivePosition", "speed": 250},
               {"name": "M_AxisY", "type": "DrivePosition", "speed": 250},
               {"name": "M_AxisZ", "type": "DrivePosition", "speed": 250}]
    axes = [
        {"name": "Saddle_Y", "mesh": gy, "actor": "M_AxisY", "type": "translation", "dir": scale(ax["y"], sy), "factor": 0.001, "offset": 0},
        {"name": "Table_X", "mesh": gx, "actor": "M_AxisX", "type": "translation", "dir": scale(ax["x"], sx), "factor": 0.001, "offset": 0,
         "parent": "Saddle_Y"},
        {"name": "Head_Z", "mesh": gz, "actor": "M_AxisZ", "type": "translation", "dir": scale(ax["z"], sz), "factor": 0.001, "offset": 0},
    ]
    if gs:
        devices.append({"name": "M_Spindle", "type": "DriveSpeed", "accel": 4000})
        axes.append({"name": "Spindle", "mesh": gs, "actor": "M_Spindle", "type": "rotation", "dir": ax["z"], "factor": 6,
                     "offset": 0, "mode": "speed", "parent": "Head_Z"})
    twin = {
        "name": name, "root": root, "model": name,
        "devices": devices,
        "statics": [{"name": g, "mesh": g} for g in statics],
        "axes": axes,
        "travel": {"X": [-tx / 2, tx / 2], "Y": [-ty / 2, ty / 2], "Z": [-tz, 0]},
        "pivots": [{"mesh": g, "p": p} for g, p in pivots.items()],
    }
    if up == "Z":
        # TwinBuild 的 pivots 用 Y 朝上的 CAD 座標；Z 朝上的 CAD 由 to_fbx.py --up Z 轉，這裡跟著轉
        for p in twin["pivots"]:
            x, y, z = p["p"]
            p["p"] = [x, z, -y]
        for a in twin["axes"]:
            x, y, z = a["dir"]
            a["dir"] = [x, z, -y]
    json.dump(twin, open(os.path.join(src, "twin.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"twin.json：{len(statics)} 個固定群組 {statics}；行程 X±{tx / 2:g} Y±{ty / 2:g} Z 0～-{tz:g}")
    print(f"下一步：blender --background --python cad/to_fbx.py -- {src} {name}.fbx preview.png --up {up}")


if __name__ == "__main__":
    main()
