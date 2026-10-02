// 站點設定：立式加工中心（LinuxCNC VMC_toolchange 的機構）。組站時覆蓋 web/site.js。
window.DT_SITE = {
  id: "vmc",
  title: "立式加工中心（CNC）",
  sub: "三軸＋主軸＋10 刀位傘式刀庫 · 寫 G-code 或 Python 當控制器，真的把素材切出形狀 · 也能接 LinuxCNC（?ws=ws://127.0.0.1:8765）",
  other: { href: "../", text: "所有機台" },
  defaultExample: "gcode",
  examples: {
    gcode: {
      label: "G-code：面銑＋口袋＋鑽孔＋圓槽",
      code: `# 用 G-code 加工一個零件：4 次換刀、面銑、方形口袋、4 個孔、圓形槽
# 座標：X0 Y0＝素材中心，Z0＝素材上表面（160 × 120 × 40 mm），Z 是刀尖
# 網頁版自動補刀長；G43 Hn 是給 LinuxCNC 用的（同一支程式可以直接拿去 LinuxCNC 跑）
from dtlink import Twin
from cnc import VMC

m = VMC(Twin())
m.speedup = 3          # 模擬加速 3 倍（只影響時間，路徑一樣）
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
      code: `# 基本：用工件座標移動刀尖、開主軸、在素材上銑一條溝
from dtlink import Twin
from cnc import VMC, tool_table

m = VMC(Twin())
tool_table()
print("主軸上的刀：T%d" % m.tool, "目前刀尖位置：", m.position())

m.spindle(5000)                     # 主軸 5000 rpm 順時針（M3 S5000）
m.rapid(x=-90, y=0, z=5)            # G0：快速移到素材左邊外面、上方 5 mm
m.feed(z=-2, f=300)                 # G1：下刀到 Z-2（素材外面下刀，不會撞）
m.feed(x=90, f=800)                 # 橫越整塊素材：銑出一條 Ø10 的溝
m.rapid(z=20)
m.spindle(0)
print("機械座標：", m.machine_pos())
print("切削體積 %.2f cm³（理論 160 × 10 × 2 / 1000 = 3.2）" % m.cut_volume)
`,
    },
    toolchange: {
      label: "自動換刀（M6）",
      code: `# 自動換刀：tool_change() 照 LinuxCNC 的 toolchange.ngc 動作
# 卸刀（刀庫轉到空刀位 → Z 下到 -100 → 換刀臂擺進 → 鬆刀 → Z 上升）
# 裝刀（刀庫轉到新刀 → Z 下降套住刀把 → 夾刀 → 換刀臂擺回）
from dtlink import Twin
from cnc import VMC, TOOLS

m = VMC(Twin())
for t in (5, 3, 8, 1):
    m.tool_change(t)
    d, length, flute, name = TOOLS[t]
    print(f"  主軸上：T{m.tool} {name}，刀長 {length} mm")
`,
    },
    manual: {
      label: "逐步換刀（自己寫順序）",
      code: `# 不用 tool_change()，自己一步一步控制換刀（像在寫 PLC 的換刀程序）
# 每一步都等感測器確認才做下一步；順序錯了孿生會發警報（例如沒對準就鬆刀 → 刀掉下去）
from dtlink import Twin

twin = Twin()
R = "MAIN.FG_VMC."
z = twin.device(R + "M_AxisZ")
arm = twin.device(R + "Y_Arm")
release = twin.device(R + "Y_ToolRelease")
lock = twin.device(R + "Y_CarouselLock")
car = twin.device(R + "M_Carousel")
spindle = twin.device(R + "M_Spindle")
tool = lambda: int(twin.read(R + "B_Tool.StatusData"))
alarm = lambda: int(twin.read(R + "B_Alarm.StatusData"))

def step(text, ok, timeout=8):
    print("•", text)
    if not twin.wait_until(lambda: ok() or alarm(), timeout=timeout) or alarm():
        raise SystemExit(f"停在「{text}」，警報 {alarm()}")

spindle.target = 0;                      step("主軸停止", lambda: abs(spindle.value) < 1)
z.target = 0;                            step("Z 回到 0", lambda: abs(z.value) < 0.01)
print("主軸上是 T%d，要把它放回刀位 %d" % (tool(), tool()))
lock.retract();                          step("拔刀庫鎖銷", lambda: lock.retracted)
car.target = tool() - 1;                 step("刀庫轉到空刀位", lambda: abs(car.value - car.target) < 1e-3)
lock.extend();                           step("插鎖銷", lambda: lock.extended)
z.target = -100;                         step("Z 下到換刀高度 -100", lambda: abs(z.value + 100) < 0.01)
arm.extend();                            step("換刀臂擺進（刀庫爪抓住刀把）", lambda: arm.extended)
release.extend();                        step("鬆刀", lambda: release.extended)
z.target = 0;                            step("Z 上升，刀留在刀庫", lambda: abs(z.value) < 0.01)
print("主軸上：T%d" % tool())
lock.retract();                          step("拔鎖銷", lambda: lock.retracted)
car.target = 6;                          step("刀庫轉到刀位 7（T7）", lambda: abs(car.value - 6) < 1e-3)
lock.extend();                           step("插鎖銷", lambda: lock.extended)
z.target = -100;                         step("Z 下降套住 T7 的刀把", lambda: abs(z.value + 100) < 0.01)
release.retract();                       step("夾刀", lambda: release.retracted)
arm.retract();                           step("換刀臂擺回", lambda: arm.retracted)
z.target = 0;                            step("Z 回到 0", lambda: abs(z.value) < 0.01)
print("完成，主軸上：T%d" % tool())
`,
    },
    alarm: {
      label: "警報：主軸沒轉就下刀",
      code: `# 故意犯錯：主軸沒轉就切入工件 → 警報 10。看警報怎麼讀、怎麼清除
from dtlink import Twin
from cnc import VMC, AlarmError

m = VMC(Twin())
if m.tool == 0:
    m.tool_change(1)
m.spindle(0)
m.rapid(x=0, y=0, z=3)
try:
    m.feed(z=-2, f=100)          # 主軸沒轉！
except AlarmError as e:
    print("被擋下來了：", e)
print("B_Alarm =", m.alarm)
m.reset_alarm()
print("清除後 B_Alarm =", m.alarm)
m.rapid(z=30)
`,
    },
    registers: {
      label: "暫存器讀寫（%QD／%ID）",
      code: `# 用暫存器位址直接寫：跟 PLC 寫 EtherCAT 伺服目標值、讀感測器一樣
from dtlink import Twin

twin = Twin()
twin.print_regs("M_Axis")
rows = {r["name"]: r["address"] for r in twin.reg_table("FG_VMC")}
x_target = rows["MAIN.FG_VMC.M_AxisX.ControlData"]
x_actual = rows["MAIN.FG_VMC.M_AxisX.StatusData"]
twin.write_reg(x_target, 150.0)
twin.wait_until(lambda: abs(twin.read_reg(x_actual) - 150.0) < 0.1, timeout=10)
print("X 到位：", round(twin.read_reg(x_actual), 2))
twin.write_reg(x_target, 0.0)
print("主軸上的刀號（%ID）：", twin.read_reg(rows["MAIN.FG_VMC.B_Tool.StatusData"]))
`,
    },
    list: {
      label: "列出所有裝置",
      code: `from dtlink import Twin, print_devices

twin = Twin()
print_devices(twin, twin.devices())
print(twin.describe("MAIN.FG_VMC.Y_Arm"))
print(twin.describe("MAIN.FG_VMC.B_Alarm"))
`,
    },
  },
};
