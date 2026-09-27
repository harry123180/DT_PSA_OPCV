"""6-DOF 並聯機器人（6-PSS／Hexaglide）：逆向運動學與平台運動控制。

PLC 只控制 6 支滑座伺服（M_Leg1～6，位置 mm，0＝CAD 原始姿態，往內為正）；平台姿態由孿生的正向運動學算出，
回報在 B_PoseX／Y／Z（mm）、B_PoseRoll／Pitch／Yaw（度）。這個模組做反方向：給平台姿態 → 算 6 個滑座位置，
並插值成連續動作，就像真正的運動控制器。

    from dtlink import Twin
    from hexapod import Hexapod
    robot = Hexapod(Twin())
    robot.move(z=20)                  # 平台上升 20 mm
    robot.move(roll=8, duration=1.5)  # 繞 X 傾斜 8 度
    print(robot.pose())

姿態座標（機器人座標系，Z 朝上）：x、y、z 是平台相對原始位置的位移（mm）；roll、pitch、yaw 是繞 X、Y、Z 的角度（度），
旋轉順序 R = Rz(yaw)·Ry(pitch)·Rx(roll)，繞平台中心轉。
"""
import math

from hexapod_geometry import LEGS, PLATFORM_CENTER, ROD_LENGTH

AXES = ("x", "y", "z", "roll", "pitch", "yaw")


def _rot(roll, pitch, yaw):
    cr, sr = math.cos(math.radians(roll)), math.sin(math.radians(roll))
    cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
    cy, sy = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    return [[cy * cp, cy * sp * sr - sy * cr, cy * sp * cr + sy * sr],
            [sy * cp, sy * sp * sr + cy * cr, sy * sp * cr - cy * sr],
            [-sp, cp * sr, cp * cr]]


def ik(x=0.0, y=0.0, z=0.0, roll=0.0, pitch=0.0, yaw=0.0):
    """平台姿態 → 6 個滑座位置（mm）。到不了（連桿不夠長）或超出滑座行程會丟 ValueError。"""
    r = _rot(roll, pitch, yaw)
    c = PLATFORM_CENTER
    out = []
    for n, leg in enumerate(LEGS, 1):
        local = [leg["top"][k] - c[k] for k in range(3)]
        top = [c[k] + (x, y, z)[k] + sum(r[k][m] * local[m] for m in range(3)) for k in range(3)]
        d = [top[k] - leg["bottom"][k] for k in range(3)]
        du = sum(d[k] * leg["u"][k] for k in range(3))
        disc = du * du - sum(v * v for v in d) + ROD_LENGTH ** 2
        if disc < 0:
            raise ValueError(f"腳 {n}：連桿長度不夠，平台到不了這個姿態")
        s = du - math.sqrt(disc)          # 兩個解裡跟原始姿態同一支（原始姿態 s＝0）
        lo, hi = leg["range"]
        if not lo - 1e-6 <= s <= hi + 1e-6:
            raise ValueError(f"腳 {n}：需要滑座到 {s:.1f} mm，超出行程 {lo:g}～{hi:g} mm")
        out.append(s)
    return out


class Hexapod:
    def __init__(self, twin, root="MAIN.FG_Robot"):
        self.twin = twin
        self.legs = [twin.device(f"{root}.M_Leg{n}") for n in range(1, 7)]
        self._pose = [f"{root}.B_Pose{a}" for a in ("X", "Y", "Z", "Roll", "Pitch", "Yaw")]

    def pose(self) -> dict:
        """孿生算出來的平台姿態（正向運動學）。"""
        return {a: round(self.twin.read(p + ".StatusData"), 2) for a, p in zip(AXES, self._pose)}

    def legs_mm(self) -> list:
        return [round(leg.value, 2) for leg in self.legs]

    def move(self, x=0.0, y=0.0, z=0.0, roll=0.0, pitch=0.0, yaw=0.0, duration=None, speed=30.0):
        """平台從目前姿態直線插值到目標姿態（每 20 ms 送一次 6 個滑座目標），走完等到位。

        duration：秒；省略時依滑座最大位移除以 speed（mm/s）估算。整段路徑先全部檢查過，
        中途有任何一點到不了或超行程就不動、直接丟 ValueError。"""
        start = self.pose()
        goal = dict(x=x, y=y, z=z, roll=roll, pitch=pitch, yaw=yaw)
        end = ik(**goal)
        if duration is None:
            now = self.legs_mm()
            duration = max(0.2, max(abs(e - n) for e, n in zip(end, now)) / speed)
        steps = max(1, int(duration / 0.02))
        path = []
        for i in range(1, steps + 1):
            t = i / steps
            t = t * t * (3 - 2 * t)       # 平滑起停
            path.append(ik(**{a: start[a] + (goal[a] - start[a]) * t for a in AXES}))
        for targets in path:
            for leg, s in zip(self.legs, targets):
                leg.target = s
            self.twin.sleep(0.02)
        self.twin.wait_until(lambda: all(abs(leg.value - s) < 0.05 for leg, s in zip(self.legs, end)), timeout=5)
        self.settle()

    def settle(self, timeout=2.0):
        """等平台姿態穩定：滑座位置先回報，平台姿態是孿生下一幀才解出來的，到位當下讀會晚一幀。"""
        last = None
        for _ in range(int(timeout / 0.05)):
            now = self.pose()
            if now == last:
                return now
            last = now
            self.twin.sleep(0.05)
        return last

    def home(self, duration=None):
        self.move(duration=duration)
