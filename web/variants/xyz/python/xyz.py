"""三軸龍門：直角座標的直線插補與連續軌跡。

各軸是獨立的位置伺服（M_AxisX、M_AxisY、M_AxisZ，0～98 mm，Y 是雙馬達同步）。各軸各自用最高速度走，
斜線會變成「先走完短的那軸」的折線；這裡把路徑切成 20 ms 一步同時送三軸，走出真正的直線（像 CNC 的 G01）。

    from dtlink import Twin
    from xyz import Gantry
    g = Gantry(Twin())
    g.move(80, 60, 20)                 # 直線走到 (80, 60, 20)
    g.follow(lambda t: dict(x=..., y=..., z=...), duration=6)   # 連續軌跡，中途不停
"""
import math

AXES = ("x", "y", "z")


class Gantry:
    def __init__(self, twin, root="MAIN.FG_Gantry3"):
        self.twin = twin
        self.axes = {a: twin.device(f"{root}.M_Axis{a.upper()}") for a in AXES}

    def position(self) -> dict:
        return {a: round(d.value, 2) for a, d in self.axes.items()}

    def _check(self, p):
        for a in AXES:
            lo, hi = self.axes[a].range or (-math.inf, math.inf)
            if not lo - 1e-6 <= p[a] <= hi + 1e-6:
                raise ValueError(f"{a.upper()} 軸 {p[a]:.1f} mm 超出行程 {lo:g}～{hi:g} mm")

    def _send(self, p):
        for a in AXES:
            self.axes[a].target = p[a]

    def _wait(self, p):
        self.twin.wait_until(lambda: all(abs(self.axes[a].value - p[a]) < 0.01 for a in AXES), timeout=10)

    def move(self, x=None, y=None, z=None, speed=40.0):
        """直線插補到 (x, y, z)（沒給的軸不動），速度 mm/s，平滑起停，走完等到位。"""
        start = self.position()
        goal = {a: start[a] if v is None else float(v) for a, v in zip(AXES, (x, y, z))}
        self._check(goal)
        dist = math.sqrt(sum((goal[a] - start[a]) ** 2 for a in AXES))
        steps = max(1, int(dist / speed / 0.02))
        for i in range(1, steps + 1):
            t = i / steps
            t = t * t * (3 - 2 * t)
            self._send({a: start[a] + (goal[a] - start[a]) * t for a in AXES})
            self.twin.sleep(0.02)
        self._wait(goal)

    def follow(self, path_at, duration, rate=50):
        """連續軌跡：path_at(t) 回傳 t 秒時的位置 dict（缺的軸維持目前位置），以 rate Hz 連續送，中途不停。
        整條先檢查行程；起點離目前位置太遠會先 move() 過去。"""
        now = self.position()
        pts = []
        for i in range(int(duration * rate) + 1):
            p = dict(now, **{k: float(v) for k, v in path_at(i / rate).items()})
            self._check(p)
            pts.append(p)
        if max(abs(pts[0][a] - now[a]) for a in AXES) > 0.5:
            self.move(**pts[0])
        for p in pts:
            self._send(p)
            self.twin.sleep(1 / rate)
        self._wait(pts[-1])

    def home(self):
        self.move(0, 0, 0)
