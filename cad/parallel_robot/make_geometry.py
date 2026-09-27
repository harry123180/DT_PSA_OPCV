"""twin.json → web/variants/robot/python/hexapod_geometry.py（Python 逆向運動學用的幾何）。

    python cad/parallel_robot/make_geometry.py Unity/Assets/ParallelRobot/twin.json web/variants/robot/python/hexapod_geometry.py
"""
import json
import sys

t = json.load(open(sys.argv[1]))
c = t["custom"]


def robot(p):
    return [round(p[0], 6), round(-p[2], 6) + 0.0, round(p[1], 6)]   # CAD（Y 朝上）→ 機器人（Z 朝上）


lines = ['"""並聯機器人的幾何（由 CAD 自動產生：cad/parallel_robot/make_geometry.py 讀 twin.json）。',
         "",
         "座標是機器人座標系（mm，Z 朝上）：機器人 (x, y, z) = CAD (x, -z, y)。",
         "bottom：滑座上的球心（位置 0 時）；top：平台上的球心（原始姿態）；u：滑座正方向（往內）；range：滑座可動範圍。",
         '"""',
         f"ROD_LENGTH = {c['rodLength']}",
         f"PLATFORM_CENTER = {robot(c['platformCenter'])}",
         "LEGS = ["]
for l in c["legs"]:
    lines.append(f"    dict(bottom={robot(l['bottom'])}, top={robot(l['top'])}, u={robot(l['u'])}, range={l['range']}),")
lines.append("]")
open(sys.argv[2], "w", encoding="utf-8").write("\n".join(lines) + "\n")
print("寫入", sys.argv[2])
