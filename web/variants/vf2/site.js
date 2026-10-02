// 站點設定：Haas VF-2（Autodesk Fusion 機台庫的模擬模型，Fusion 匯出 → 元件分群）。組站時覆蓋 web/site.js。
window.DT_SITE = {
  id: "vf2",
  title: "Haas VF-2 立式加工中心",
  sub: "Fusion 360 機台庫的 VF-2 模型 · X 762 × Y 406 × Z 508 mm · 寫 G-code 或 Python 加工",
  other: { href: "../", text: "所有機台" },
  defaultExample: "gcode",
  examples: {
    gcode: {
      label: "G-code：面銑＋口袋＋鑽孔＋圓槽",
      code: `# 用 G-code 加工一個零件（跟立式加工中心、LinuxCNC 用的是同一支程式）
# VF-2 這個模型沒有刀庫：M6 是手動換刀（主軸停、Z 回到最上面、換上刀）
# 座標：X0 Y0＝素材中心，Z0＝素材上表面（160 × 120 × 40 mm），Z 是刀尖（自動補刀長）
from dtlink import Twin
from cnc import VMC

m = VMC(Twin())        # 自動認出這台是 VF-2
m.speedup = 3
m.new_stock()

m.run_gcode("""
%
(O1000 DT 示範零件)
G21 G90 G17
(── 1. 面銑：Ø50 面銑刀，切掉 1 mm ──)
T5 M6
G43 H5
S3000 M3
G0 X-110 Y-40 Z5
G1 Z-1 F300
G1 X110 F1500
G1 Y0
G1 X-110
G1 Y40
G1 X110
G0 Z5
(── 2. 方形口袋 50 × 40，深 6 mm：Ø10 平銑刀，分兩層 ──)
T1 M6
G43 H1
S6000 M3
G0 X-20 Y-15 Z2
G1 Z-3.5 F200
G1 X20 F900
G1 Y-9
G1 X-20
G1 Y-3
G1 X20
G1 Y3
G1 X-20
G1 Y9
G1 X20
G1 Y15
G1 X-20
G1 Y-15
G1 Z-7 F200
G1 X20 F900
G1 Y15
G1 X-20
G1 Y-15
G0 Z5
(── 3. 四個孔：Ø8 鑽頭，G83 啄鑽 ──)
T4 M6
G43 H4
S2500 M3
G0 Z10
G98 G83 X-60 Y-45 Z-15 R2 Q5 F200
X60
Y45
X-60
G80
(── 4. 圓槽 R45，深 2 mm：Ø6 平銑刀，G2 整圓 ──)
T2 M6
G43 H2
S8000 M3
G0 X45 Y0 Z2
G1 Z-2 F150
G2 X45 Y0 I-45 J0 F600
G0 Z10
M30
%
""")
print("主軸上的刀：T%d，切削體積 %.1f cm³" % (m.tool, m.cut_volume))
`,
    },
    basic: {
      label: "基本：移動與主軸",
      code: `# 基本：工件座標移動刀尖、開主軸、銑一條溝
from dtlink import Twin
from cnc import VMC

m = VMC(Twin())
print("機台：", m.root, "行程：", m.travel)
print("主軸上的刀：T%d，刀尖位置：" % m.tool, m.position())
m.spindle(5000)
m.rapid(x=-90, y=0, z=5)
m.feed(z=-2, f=300)
m.feed(x=90, f=800)
m.rapid(z=50)
m.spindle(0)
print("切削體積 %.2f cm³（理論 3.2）" % m.cut_volume)
m.move_machine(x=381, y=203)          # 走到行程極限看看工作台
m.move_machine(x=-381, y=-203)
m.home()
`,
    },
    toolchange: {
      label: "手動換刀",
      code: `# VF-2 模型沒有刀庫：tool_change() 停主軸、Z 回到最上面，再寫 Q_ToolSelect 換刀
from dtlink import Twin
from cnc import VMC, TOOLS

m = VMC(Twin())
for t in (5, 8, 3, 1):
    m.tool_change(t)
    d, length, flute, name = TOOLS[t]
    print(f"  主軸上：T{m.tool} {name}，刀長 {length} mm")
`,
    },
    alarm: {
      label: "警報：主軸轉著換刀",
      code: `# 故意犯錯：主軸還在轉就直接寫選刀值 → 警報 14
from dtlink import Twin
from cnc import VMC

m = VMC(Twin())
m.spindle(3000)
m.tool_select.target = 4             # 不經過 tool_change()，主軸沒停
m.twin.sleep(0.5)
print("B_Alarm =", m.alarm, "（14＝主軸旋轉中手動換刀）")
m.spindle(0)
m.reset_alarm()
m.twin.wait_until(lambda: m.tool == 4, timeout=3)
print("警報清除、主軸停了 → 換上 T%d" % m.tool)
`,
    },
    list: {
      label: "列出所有裝置",
      code: `from dtlink import Twin, print_devices

twin = Twin()
print_devices(twin, twin.devices())
print(twin.describe("MAIN.FG_VF2.Q_ToolSelect"))
`,
    },
  },
};
