// 站點設定：三軸龍門（雙 Y 同步＋X 橫梁＋Z 升降，由 CAD 自動轉出）。組站時覆蓋 web/site.js。
window.DT_SITE = {
  id: "xyz",
  title: "三軸龍門",
  sub: "CAD 自動轉出的數位孿生 · 雙 Y 同步＋X＋Z，用 Python 當運動控制器",
  other: { href: "../", text: "產線孿生" },
  defaultExample: "basic",
  examples: {
    basic: {
      label: "基本：三軸各自移動",
      code: `# 基本：三支伺服各自移動（0＝惰輪端，行程 0～98 mm）
from dtlink import Twin

twin = Twin()
x = twin.device("MAIN.FG_Gantry3.M_AxisX")
y = twin.device("MAIN.FG_Gantry3.M_AxisY")   # 兩支 Y 由同一個伺服帶動（同步）
z = twin.device("MAIN.FG_Gantry3.M_AxisZ")

y.move_to(70); print("Y", round(y.value, 1))
x.move_to(80); print("X", round(x.value, 1))
z.move_to(60); print("Z", round(z.value, 1))
for d in (z, x, y):
    d.move_to(10)
print("回到起點附近")
`,
    },
    pick: {
      label: "取放：直線插補",
      code: `# 取放：Gantry.move() 三軸同時走直線（像 CNC 的 G01），Z 下去、上來再搬到下一格
from dtlink import Twin
from xyz import Gantry

g = Gantry(Twin())
SAFE, DOWN = 80, 15
spots = [(15, 15), (80, 15), (80, 80), (15, 80)]
g.move(z=SAFE)
for n, (px, py) in enumerate(spots, 1):
    g.move(px, py, SAFE)      # 在安全高度移過去
    g.move(z=DOWN)            # 下降
    twin_pos = g.position()
    g.move(z=SAFE)            # 上升
    print(f"第 {n} 格完成", twin_pos)
g.move(50, 50, SAFE)
`,
    },
    circle: {
      label: "XY 畫圓（連續軌跡）",
      code: `# XY 平面畫圓，Z 同時上下起伏：follow() 給「時間 → 位置」，連續送出、中途不停
import math
from dtlink import Twin
from xyz import Gantry

g = Gantry(Twin())

def path(t):                         # 一圈 4 秒
    a = t / 4 * 2 * math.pi
    return dict(x=49 + 35 * math.cos(a), y=49 + 35 * math.sin(a), z=50 + 20 * math.sin(2 * a))

g.follow(path, duration=8)           # 兩圈
print("完成", g.position())
`,
    },
    registers: {
      label: "暫存器讀寫（%QD／%ID）",
      code: `# 用暫存器位址直接寫目標位置（REAL），跟 PLC 寫 EtherCAT 伺服目標值一樣
from dtlink import Twin

twin = Twin()
twin.print_regs("M_Axis")
rows = {r["name"]: r["address"] for r in twin.reg_table("M_AxisY")}
target = rows["MAIN.FG_Gantry3.M_AxisY.ControlData"]
actual = rows["MAIN.FG_Gantry3.M_AxisY.StatusData"]
twin.write_reg(target, 45.0)
twin.wait_until(lambda: abs(twin.read_reg(actual) - 45.0) < 0.5, timeout=10)
print("Y 到位：", round(twin.read_reg(actual), 1), "（兩支 Y 一起動）")
`,
    },
    list: {
      label: "列出所有裝置",
      code: `from dtlink import Twin, print_devices

twin = Twin()
print_devices(twin, twin.devices())
print(twin.describe("MAIN.FG_Gantry3.M_AxisY"))
`,
    },
  },
};
