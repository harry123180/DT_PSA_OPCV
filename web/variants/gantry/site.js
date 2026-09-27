// 站點設定：雙軸直線模組（由 CAD 自動轉成的數位孿生）。組站時覆蓋 web/site.js。
window.DT_SITE = {
  id: "gantry",
  title: "雙軸直線模組",
  sub: "CAD 自動轉出的數位孿生 · 用 Python 控制兩支伺服",
  other: { href: "../", text: "所有機台" },
  defaultExample: "basic",
  examples: {
    basic: {
      label: "基本：X、Z 移到指定位置",
      code: `# 基本：兩支伺服移到指定位置（單位 mm，0＝惰輪端）
from dtlink import Twin

twin = Twin()
x = twin.device("MAIN.FG_Gantry.M_AxisX")
z = twin.device("MAIN.FG_Gantry.M_AxisZ")
print(x.label, "行程", x.range)
print(z.label, "行程", z.range)

x.move_to(300)                 # 等到位才往下
print("X 到位：", round(x.value, 1))
z.move_to(250)
print("Z 到位：", round(z.value, 1))

x.move_to(50)
z.move_to(20)
print("回到起點附近")
`,
    },
    rectangle: {
      label: "走矩形路徑",
      code: `# 走矩形：X、Z 輪流動，重複三圈
from dtlink import Twin

twin = Twin()
x = twin.device("MAIN.FG_Gantry.M_AxisX")
z = twin.device("MAIN.FG_Gantry.M_AxisZ")

corners = [(60, 60), (350, 60), (350, 350), (60, 350)]
for lap in range(3):
    for cx, cz in corners:
        x.move_to(cx)
        z.move_to(cz)
    print(f"第 {lap + 1} 圈完成")
`,
    },
    together: {
      label: "兩軸同時動（不等待）",
      code: `# 兩軸同時動：move_to(wait=False) 送出目標就回來，再一起等到位
from dtlink import Twin

twin = Twin()
x = twin.device("MAIN.FG_Gantry.M_AxisX")
z = twin.device("MAIN.FG_Gantry.M_AxisZ")

for tx, tz in [(380, 380), (30, 200), (200, 30)]:
    x.move_to(tx, wait=False)
    z.move_to(tz, wait=False)
    twin.wait_until(lambda: not x.active and not z.active and abs(x.value - tx) < 0.5 and abs(z.value - tz) < 0.5, timeout=10)
    print(f"到 ({tx}, {tz})")
`,
    },
    registers: {
      label: "暫存器讀寫（%QD／%ID）",
      code: `# 用暫存器位址直接寫目標位置（REAL），跟 PLC 寫 EtherCAT 伺服的目標值一樣
from dtlink import Twin

twin = Twin()
twin.print_regs("M_Axis")          # 看位址對照

for row in twin.reg_table("M_AxisX"):
    if row["name"].endswith("ControlData"):
        target = row["address"]
    if row["name"].endswith("StatusData"):
        actual = row["address"]
print("X 目標位址", target, "目前位置位址", actual)

twin.write_reg(target, 250.0)
twin.wait_until(lambda: abs(twin.read_reg(actual) - 250.0) < 0.5, timeout=10)
print("到位：", round(twin.read_reg(actual), 1))
`,
    },
    list: {
      label: "列出所有裝置",
      code: `from dtlink import Twin, print_devices

twin = Twin()
print_devices(twin, twin.devices())
print(twin.describe("MAIN.FG_Gantry.M_AxisX"))
`,
    },
  },
};
