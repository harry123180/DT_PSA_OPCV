// 站點設定：六軸並聯機器人（6-PSS，由 CAD 自動轉出的數位孿生）。組站時覆蓋 web/site.js。
window.DT_SITE = {
  id: "robot",
  title: "六軸並聯機器人",
  sub: "CAD 自動轉出的數位孿生 · 六支滑座推動平台（閉鏈運動學）",
  other: { href: "../", text: "產線孿生" },
  defaultExample: "basic",
  examples: {
    basic: {
      label: "基本：平台上下、傾斜",
      code: `# 基本：用平台姿態控制，hexapod 幫你算六支滑座要去哪（逆向運動學）
from dtlink import Twin
from hexapod import Hexapod

twin = Twin()
robot = Hexapod(twin)
print("目前姿態", robot.pose())

robot.move(z=15)               # 平台上升 15 mm
print("上升後", robot.pose(), "滑座", robot.legs_mm())

robot.move(roll=8)             # 回到原高度，繞 X 傾斜 8 度
print("傾斜後", robot.pose())

robot.home()
print("回原位", robot.pose())
`,
    },
    dance: {
      label: "平台畫圓、搖擺（連續軌跡）",
      code: `# 平台在水平面畫圓，同時搖擺：follow() 給「時間 → 姿態」，連續送出、中途不停
# （move() 是點到點，每段都會起停，拿來串很多小段會一頓一頓的）
import math
from dtlink import Twin
from hexapod import Hexapod

twin = Twin()
robot = Hexapod(twin)

def circle(t):                       # t：秒；一圈 4 秒
    a = t / 4 * 2 * math.pi
    return dict(x=10 * math.cos(a), y=10 * math.sin(a), roll=5 * math.sin(a), pitch=5 * math.cos(a))

robot.follow(circle, duration=8)     # 兩圈
robot.home()
print("完成", robot.pose())
`,
    },
    legs: {
      label: "直接控制單支滑座",
      code: `# 直接控制滑座（PLC 的做法）：只動一支腳，看平台怎麼傾斜（姿態由孿生的正向運動學算出）
from dtlink import Twin

twin = Twin()
leg1 = twin.device("MAIN.FG_Robot.M_Leg1")
print(leg1.label, "行程", leg1.range)

leg1.move_to(40)
twin.sleep(0.2)                # 平台姿態是孿生下一幀才解出來的
pose = {a: round(twin.read(f"MAIN.FG_Robot.B_Pose{a}.StatusData"), 2) for a in ("X", "Y", "Z", "Roll", "Pitch", "Yaw")}
print("只推腳 1 之後的平台姿態", pose)

leg1.move_to(0)
`,
    },
    check: {
      label: "驗證：逆向 vs 正向運動學",
      code: `# 驗證：Python 用逆向運動學算滑座位置，孿生用正向運動學算回平台姿態，兩者應該一致
from dtlink import Twin
from hexapod import Hexapod

twin = Twin()
robot = Hexapod(twin)
for goal in [dict(z=10), dict(x=12), dict(y=-10), dict(roll=6), dict(pitch=-6), dict(yaw=12), dict(x=5, y=5, z=-8, yaw=-5)]:
    robot.move(**goal)
    got = robot.pose()
    err = max(abs(got[k] - goal.get(k, 0)) for k in got)
    print(goal, "→", got, "誤差", round(err, 3))
robot.home()
`,
    },
    list: {
      label: "列出所有裝置",
      code: `from dtlink import Twin, print_devices

twin = Twin()
print_devices(twin, twin.devices())
twin.print_regs("M_Leg1")
`,
    },
  },
};
