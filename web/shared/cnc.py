"""立式加工中心：三軸插補、換刀、G-code。兩台機台共用，依孿生裡有哪台自動套用參數（PROFILES）：
- MAIN.FG_VMC：LinuxCNC VMC_toolchange 的機構，10 刀位傘式刀庫自動換刀。行程 X ±300、Y ±100、Z -400～0
- MAIN.FG_VF2：Haas VF-2（Autodesk 機台庫的模擬模型），手動換刀（操作員）。行程 X ±381、Y ±203、Z -508～0

裝置：
- M_AxisX／Y／Z：位置伺服（mm）。X、Y 移動工作台，Z 移動主軸頭（0＝最上面）
- M_Spindle：主軸（轉速 rpm，正＝順時針 M3、負＝逆時針 M4）
- M_Carousel：傘式刀庫（位置＝刀位，0＝刀位 1 對著主軸，每刀位 36°）
- Y_Arm：換刀臂（伸出＝把刀庫擺到主軸下）；Y_ToolRelease：鬆刀（伸出＝拉刀桿推下、刀把放開）；Y_CarouselLock：刀庫鎖銷
- B_Tool：主軸上的刀號（0＝沒刀）；B_Alarm：警報碼；B_CutVolume：切掉的體積（cm³）
- Q_ResetAlarm：清除警報；Q_ResetStock：換一塊新料
- Q_ToolSelect（VF-2）：手動換刀的選刀值（刀號）

座標：G-code 用工件座標（G54）—— X0 Y0＝素材中心，Z0＝素材上表面，Z 是刀尖（自動補刀長，等於永遠 G43）。
素材 160 × 120 × 40 mm（X × Y × 高）。

    from dtlink import Twin
    from cnc import VMC
    m = VMC(Twin())
    m.tool_change(1)
    m.spindle(6000)
    m.rapid(x=-70, y=-50, z=5)
    m.feed(z=-2, f=200)
    m.feed(x=70, f=800)
    m.run_gcode(open("part.nc").read())
"""
import math
import re
import time

# 每台機台：行程、換刀方式、Z=0 時主軸鼻端到素材上表面的距離（刀長補正用：機械 Z ＝ 工件 Z ＋ 刀長 － nose_to_stock）
PROFILES = {
    "MAIN.FG_VMC": dict(travel={"x": (-300.0, 300.0), "y": (-100.0, 100.0), "z": (-400.0, 0.0)},
                        changer="auto", change_z=-100.0, nose_to_stock=949.7 - 490.0),
    "MAIN.FG_VF2": dict(travel={"x": (-381.0, 381.0), "y": (-203.0, 203.0), "z": (-508.0, 0.0)},
                        changer="manual", change_z=0.0, nose_to_stock=610.0 - 110.0),
}
ROOT = "MAIN.FG_VMC"
TRAVEL = PROFILES[ROOT]["travel"]
CHANGE_Z = PROFILES[ROOT]["change_z"]          # 換刀高度（機械座標）
GAUGE_Z0 = 949.7                                # VMC：Z=0 時主軸鼻端的高度（從底座算，mm）
STOCK_TOP = 490.0                               # VMC：素材上表面的高度
STOCK = (160.0, 120.0, 40.0)
POCKETS = 10
TOOLS = {                    # 刀號: (直徑, 全長, 刃長, 名稱)；全長從刀把基準面量到刀尖
    1: (10, 100, 30, "Ø10 平銑刀"), 2: (6, 95, 20, "Ø6 平銑刀"), 3: (16, 105, 35, "Ø16 平銑刀"), 4: (8, 110, 45, "Ø8 鑽頭"),
    5: (50, 80, 8, "Ø50 面銑刀"), 6: (4, 90, 12, "Ø4 平銑刀"), 7: (12, 100, 30, "Ø12 平銑刀"), 8: (20, 100, 35, "Ø20 平銑刀"),
    9: (3, 85, 6, "Ø3 中心鑽"), 10: (25, 95, 30, "Ø25 平銑刀"),
}
ALARMS = {
    1: "刀庫鎖銷插著時旋轉刀庫", 2: "主軸有刀，換刀臂卻把有刀的刀位擺過來（撞刀）", 3: "刀具被刀庫爪抓著時 Z 軸移動（扯刀）",
    4: "鬆刀時刀具沒有被刀庫接住（刀具掉落）", 5: "主軸旋轉中鬆刀", 6: "換刀臂不在原位時主軸旋轉",
    7: "換刀臂不在原位時 Z 不在換刀高度（撞到刀庫）", 8: "換刀臂擺進時刀庫沒有對準刀位",
    10: "主軸沒轉就切入工件", 11: "切深超過刃長（刀把撞到工件）", 12: "刀尖低於工件底面（撞到治具）", 13: "主軸沒有刀，主軸鼻端撞到工件",
    14: "主軸旋轉中手動換刀",
}


class AlarmError(RuntimeError):
    pass


class VMC:
    def __init__(self, twin, root=None, rate=50):
        if root is None:                                # 自動找孿生裡是哪台機台
            root = next((r for r in PROFILES if f"{r}.M_AxisX.ControlData" in twin.variables), ROOT)
        self.twin, self.root, self.rate = twin, root, rate
        prof = PROFILES.get(root, PROFILES[ROOT])
        self.travel, self.changer = prof["travel"], prof["changer"]
        self.change_z, self.nose_to_stock = prof["change_z"], prof["nose_to_stock"]
        d = lambda n: twin.device(f"{root}.{n}")
        self.axes = {"x": d("M_AxisX"), "y": d("M_AxisY"), "z": d("M_AxisZ")}
        self.spindle_drive = d("M_Spindle")
        if self.changer == "auto":
            self.carousel = d("M_Carousel")
            self.arm, self.release, self.lock = d("Y_Arm"), d("Y_ToolRelease"), d("Y_CarouselLock")
        else:
            self.tool_select = d("Q_ToolSelect")
        self.speedup = 1.0          # 模擬加速：進給速度 × speedup（只影響模擬時間，不影響路徑）
        self.accel = 1500.0         # mm/s²
        self.rapid_speed = 6000.0   # G0 速度 mm/min
        self.f = 500.0              # 目前進給 mm/min
        self.verbose = True

    # ── 狀態 ─────────────────────────────────────────────────────────
    def _read(self, name):
        return self.twin.read(f"{self.root}.{name}.StatusData")

    @property
    def tool(self) -> int:
        """主軸上的刀號（0＝沒刀）"""
        return int(round(self._read("B_Tool")))

    @property
    def alarm(self) -> int:
        return int(round(self._read("B_Alarm")))

    @property
    def cut_volume(self) -> float:
        """到目前切掉的體積（cm³）"""
        return self._read("B_CutVolume")

    @property
    def rpm(self) -> float:
        return self.spindle_drive.value

    def check(self):
        a = self.alarm
        if a:
            raise AlarmError(f"警報 {a}：{ALARMS.get(a, '未知')}（m.reset_alarm() 清除）")

    def machine_pos(self) -> dict:
        """機械座標（各軸伺服的回報值）"""
        return {a: round(d.value, 3) for a, d in self.axes.items()}

    def tool_length(self, t=None) -> float:
        t = self.tool if t is None else t
        return TOOLS[t][1] if t else 0.0

    def to_machine(self, x, y, z, t=None) -> dict:
        """工件座標（刀尖）→ 機械座標"""
        return {"x": x, "y": y, "z": z + self.tool_length(t) - self.nose_to_stock}

    def to_work(self, mp=None, t=None) -> dict:
        mp = mp or self.machine_pos()
        return {"x": mp["x"], "y": mp["y"], "z": round(mp["z"] - self.tool_length(t) + self.nose_to_stock, 3)}

    def position(self) -> dict:
        """目前刀尖的工件座標"""
        return self.to_work()

    # ── 低階：機械座標的運動 ─────────────────────────────────────────
    def _check_travel(self, p):
        for a in "xyz":
            lo, hi = self.travel[a]
            if not lo - 1e-6 <= p[a] <= hi + 1e-6:
                raise ValueError(f"{a.upper()} 軸要到 {p[a]:.2f}（機械座標），超出行程 {lo:g}～{hi:g}")

    def _send(self, p):
        for a in "xyz":
            self.axes[a].target = p[a]

    def _stream(self, points):
        """以 rate Hz 連續送出機械座標點（中途不停），最後等到位。"""
        dt = 1.0 / self.rate
        t0 = time.time()
        for i, p in enumerate(points):
            self._send(p)
            if i % 5 == 0:
                self.check()
            wait = t0 + (i + 1) * dt - time.time()
            if wait > 0:
                self.twin.sleep(wait)
        if points:
            last = points[-1]
            self.twin.wait_until(lambda: all(abs(self.axes[a].value - last[a]) < 0.02 for a in "xyz") or self.alarm, timeout=20)
            self.check()

    def _profile(self, length, speed):
        """梯形速度曲線：回傳每個取樣時刻已走的距離（0～length）"""
        if length < 1e-9:
            return [length]
        v = max(speed, 1e-3)
        a = self.accel * self.speedup ** 2
        ta = v / a
        if v * ta > length:            # 三角形：達不到設定速度
            ta = math.sqrt(length / a)
            v = a * ta
            tc = 0.0
        else:
            tc = (length - v * ta) / v
        total = 2 * ta + tc
        n = max(1, int(math.ceil(total * self.rate)))
        out = []
        for i in range(1, n + 1):
            t = total * i / n
            if t < ta:
                s = 0.5 * a * t * t
            elif t < ta + tc:
                s = 0.5 * a * ta * ta + v * (t - ta)
            else:
                td = total - t
                s = length - 0.5 * a * td * td
            out.append(min(length, max(0.0, s)))
        return out

    def _path(self, start, end, speed_mm_min, arc=None):
        """start→end 的機械座標點（直線或圓弧），速度 mm/min"""
        speed = speed_mm_min / 60.0 * self.speedup
        if arc is None:
            d = {a: end[a] - start[a] for a in "xyz"}
            length = math.sqrt(sum(v * v for v in d.values()))
            pts = [{a: start[a] + d[a] * (s / length if length else 1) for a in "xyz"} for s in self._profile(length, speed)]
        else:
            cx, cy, cw = arc
            r = math.hypot(start["x"] - cx, start["y"] - cy)
            a0 = math.atan2(start["y"] - cy, start["x"] - cx)
            a1 = math.atan2(end["y"] - cy, end["x"] - cx)
            sweep = a1 - a0
            if cw and sweep >= -1e-9:
                sweep -= 2 * math.pi
            if not cw and sweep <= 1e-9:
                sweep += 2 * math.pi
            dz = end["z"] - start["z"]
            length = math.hypot(abs(sweep) * r, dz)
            pts = []
            for s in self._profile(length, speed):
                k = s / length if length else 1
                ang = a0 + sweep * k
                pts.append({"x": cx + r * math.cos(ang), "y": cy + r * math.sin(ang), "z": start["z"] + dz * k})
            pts[-1] = dict(end)
        for p in pts:
            self._check_travel(p)
        return pts

    def move_machine(self, x=None, y=None, z=None, f=None):
        """機械座標直線移動（沒給的軸不動）；f＝mm/min，預設快速"""
        start = self.machine_pos()
        end = {a: start[a] if v is None else float(v) for a, v in zip("xyz", (x, y, z))}
        self._stream(self._path(start, end, f or self.rapid_speed))

    # ── 工件座標的運動（刀尖） ────────────────────────────────────────
    def _target(self, x, y, z):
        w = self.position()
        return self.to_machine(w["x"] if x is None else x, w["y"] if y is None else y, w["z"] if z is None else z)

    def rapid(self, x=None, y=None, z=None):
        """G0：快速移動到工件座標（刀尖）"""
        self._stream(self._path(self.machine_pos(), self._target(x, y, z), self.rapid_speed))

    def feed(self, x=None, y=None, z=None, f=None):
        """G1：以進給 f（mm/min）直線切削"""
        if f:
            self.f = f
        self._stream(self._path(self.machine_pos(), self._target(x, y, z), self.f))

    def arc(self, x, y, i=None, j=None, r=None, z=None, cw=True, f=None):
        """G2（cw=True）／G3：XY 平面圓弧到 (x, y)，圓心＝起點＋(i, j)，或給半徑 r"""
        if f:
            self.f = f
        w = self.position()
        if r is not None:
            i, j = _center_from_radius(w["x"], w["y"], x, y, r, cw)
        cx, cy = w["x"] + (i or 0), w["y"] + (j or 0)
        end = self._target(x, y, z)
        self._stream(self._path(self.machine_pos(), end, self.f, arc=(cx, cy, cw)))

    def spindle(self, rpm, wait=True):
        """主軸轉速：正＝順時針（M3），負＝逆時針（M4），0＝停（M5）；預設等到轉速穩定"""
        self.spindle_drive.target = float(rpm)
        if wait:
            self.twin.wait_until(lambda: abs(self.rpm - rpm) <= max(5.0, abs(rpm) * 0.01), timeout=10)

    def home(self):
        """Z 先回到最上面，再回 X0 Y0"""
        self.move_machine(z=0)
        self.move_machine(x=0, y=0)

    # ── 換刀（照 LinuxCNC VMC_toolchange 的 toolchange.ngc） ─────────
    def _cyl(self, dev, extend, what, timeout=6):
        (dev.extend if extend else dev.retract)()
        ok = self.twin.wait_until(lambda: (dev.extended if extend else dev.retracted) or self.alarm, timeout=timeout)
        self.check()
        if not ok:
            raise TimeoutError(f"{what} 逾時")

    def _carousel_to(self, pocket):
        """刀庫轉到刀位 pocket（1～10），走最短方向"""
        v = self.carousel.value
        k = pocket - 1
        target = v + ((k - v + POCKETS / 2) % POCKETS) - POCKETS / 2
        target = round(target)
        self.carousel.target = target
        self.twin.wait_until(lambda: abs(self.carousel.value - target) < 1e-3 or self.alarm, timeout=15)
        self.check()

    def _select_pocket(self, pocket):
        """解鎖 → 刀庫轉到刀位 → 上鎖（toolchange.ngc 的 M65 P1／M68／M64 P1）"""
        if abs(self.carousel.value - round(self.carousel.value)) < 1e-3 and \
                int(round(self.carousel.value)) % POCKETS == pocket - 1 and self.lock.extended:
            return
        self._cyl(self.lock, False, "刀庫解鎖")
        self._carousel_to(pocket)
        self._cyl(self.lock, True, "刀庫上鎖")

    def tool_change(self, t: int):
        """M6 Tt：換到 t 號刀（0＝卸刀）。VMC：固定刀位，t 號刀永遠放在刀位 t；VF-2：手動換刀（操作員）。"""
        if t == self.tool:
            return
        if t and t not in TOOLS:
            raise ValueError(f"刀具表沒有 T{t}")
        if self.verbose:
            print(f"換刀 T{self.tool} → T{t}" + ("（手動）" if self.changer == "manual" else ""))
        if self.changer == "manual":                   # 主軸停、Z 回到最上面，操作員換刀
            self.spindle(0)
            self.move_machine(z=0)
            self.tool_select.target = float(t)
            self.twin.wait_until(lambda: self.tool == t or self.alarm, timeout=5)
            self.check()
            return
        self.spindle(0)
        self.move_machine(z=0)
        if self.tool:                                  # 卸刀：空刀位轉到主軸這邊，主軸下到換刀高度，刀庫擺進來接住
            self._select_pocket(self.tool)
            self.move_machine(z=self.change_z)
            self._cyl(self.arm, True, "換刀臂擺進")
            self._cyl(self.release, True, "鬆刀")
            self.move_machine(z=0)                     # 刀留在刀庫上
        else:
            self._cyl(self.release, True, "鬆刀")
        if t:                                          # 裝刀：刀庫轉到 t 號刀、擺進來，主軸（鬆刀狀態）下去套住刀把
            self._select_pocket(t)
            if not self.arm.extended:
                self._cyl(self.arm, True, "換刀臂擺進")
            self.move_machine(z=self.change_z)
        self._cyl(self.release, False, "夾刀")
        self._cyl(self.arm, False, "換刀臂擺出")
        self.move_machine(z=0)
        if self.tool != t:
            raise RuntimeError(f"換刀失敗：主軸上是 T{self.tool}，不是 T{t}")

    # ── 重置 ─────────────────────────────────────────────────────────
    def _pulse(self, name, done=None):
        """送一個上升緣：on → 等孿生確認（或至少 0.5 秒，畫面幀率低時才收得到）→ off"""
        lamp = self.twin.device(f"{self.root}.{name}")
        lamp.on()
        self.twin.sleep(0.2)
        if done:
            self.twin.wait_until(done, timeout=5)
        self.twin.sleep(0.3)
        lamp.off()
        self.twin.sleep(0.2)

    def reset_alarm(self):
        """清除警報（掉落的刀放回刀庫）"""
        self._pulse("Q_ResetAlarm", lambda: self.alarm == 0)

    def new_stock(self):
        """換一塊新的素材"""
        self._pulse("Q_ResetStock", lambda: self.cut_volume == 0)

    def stop(self):
        """主軸停、各軸停在目前位置"""
        self.spindle_drive.target = 0.0
        p = self.machine_pos()
        self._send(p)

    # ── G-code ──────────────────────────────────────────────────────
    def run_gcode(self, program: str, verbose=None):
        """執行 G-code（工件座標、mm）。支援：
        G0 G1 G2 G3（XY 平面，I J 或 R）G4 P（秒）G17 G20/G21 G28 G53 G54 G80 G81 G83 G90 G91 G98 G99
        M0 M1 M2 M3 M4 M5 M6 M8 M9 M30、T、S、F、N 行號、( ) 和 ; 註解"""
        verbose = self.verbose if verbose is None else verbose
        st = dict(motion=0, absolute=True, inch=False, cycle=None, retract_r=True, s=0, t_next=None)
        for lineno, raw in enumerate(program.splitlines(), 1):
            line = re.sub(r"\(.*?\)", "", raw).split(";")[0].strip().upper()
            if not line or line.startswith("%"):
                continue
            words = re.findall(r"([A-Z])\s*([-+]?\d*\.?\d+)", line)
            if not words:
                continue
            try:
                self._exec_block(words, st)
            except (AlarmError, ValueError, TimeoutError, RuntimeError) as e:
                raise type(e)(f"第 {lineno} 行「{raw.strip()}」：{e}") from None
            if st.get("end"):
                break
        if verbose:
            print("程式結束，切削體積", round(self.cut_volume, 2), "cm³")

    def _exec_block(self, words, st):
        g = [float(v) for k, v in words if k == "G"]
        m = [int(float(v)) for k, v in words if k == "M"]
        vals = {k: float(v) for k, v in words if k not in "GM"}
        scale = 25.4 if st["inch"] else 1.0
        for code in g:
            if code in (0, 1, 2, 3):
                st["motion"] = int(code)
                st["cycle"] = None
            elif code == 20:
                st["inch"] = True; scale = 25.4
            elif code == 21:
                st["inch"] = False; scale = 1.0
            elif code == 90:
                st["absolute"] = True
            elif code == 91:
                st["absolute"] = False
            elif code in (81, 83):
                st["cycle"] = int(code)
            elif code == 80:
                st["cycle"] = None
            elif code == 98:
                st["retract_r"] = False
            elif code == 99:
                st["retract_r"] = True
        if "F" in vals:
            self.f = vals["F"] * scale
        if "S" in vals:
            st["s"] = vals["S"]
        if "T" in vals:
            st["t_next"] = int(vals["T"])
        # M 碼（M6 在移動前、M3/M4 在移動前、M5 在移動後是常見慣例；這裡都在移動前）
        for code in m:
            if code == 6:
                self.tool_change(st["t_next"] if st["t_next"] is not None else 0)
            elif code == 3:
                self.spindle(abs(st["s"]))
            elif code == 4:
                self.spindle(-abs(st["s"]))
            elif code == 5:
                self.spindle(0)
            elif code in (2, 30):
                self.spindle(0)
                self.move_machine(z=0)
                st["end"] = True
                return
        if 4 in g:
            self.twin.sleep(vals.get("P", 0))
            return
        if 28 in g:
            self.move_machine(z=0)
            self.move_machine(x=0, y=0)
            return
        axes = {k.lower(): vals[k] * scale for k in "XYZ" if k in vals}
        if 53 in g:                                     # 機械座標，只在這一行有效
            if axes:
                self.move_machine(**axes, f=None if st["motion"] == 0 else self.f)
            return
        if not axes and "R" not in vals:
            return
        cur = self.position()
        tgt = {a: (axes[a] if st["absolute"] else cur[a] + axes[a]) if a in axes else cur[a] for a in "xyz"}
        if st["cycle"]:
            self._drill_cycle(st, cur, tgt, vals, scale)
            return
        mo = st["motion"]
        if mo == 0:
            self.rapid(**tgt)
        elif mo == 1:
            self.feed(**tgt)
        else:
            if "R" in vals:
                self.arc(tgt["x"], tgt["y"], r=vals["R"] * scale, z=tgt["z"], cw=mo == 2)
            else:
                self.arc(tgt["x"], tgt["y"], i=vals.get("I", 0) * scale, j=vals.get("J", 0) * scale, z=tgt["z"], cw=mo == 2)

    def _drill_cycle(self, st, cur, tgt, vals, scale):
        """G81 鑽孔／G83 啄鑽：R 平面、Z 孔底、Q 每次啄鑽深度"""
        r = vals.get("R", st.get("r", cur["z"]) / scale) * scale
        st["r"] = r
        depth = tgt["z"] if "Z" in vals else st.get("depth", cur["z"])
        st["depth"] = depth
        init_z = cur["z"]
        self.rapid(x=tgt["x"], y=tgt["y"])
        self.rapid(z=r)
        if st["cycle"] == 81:
            self.feed(z=depth)
        else:
            q = vals.get("Q", st.get("q", 2.0)) * scale
            st["q"] = q
            z = r
            while z > depth + 1e-6:
                z = max(depth, z - q)
                self.feed(z=z)
                self.rapid(z=r)
                if z > depth + 1e-6:
                    self.rapid(z=z + 0.5)
        self.rapid(z=r if st["retract_r"] else max(init_z, r))


def _center_from_radius(x0, y0, x1, y1, r, cw):
    """G2/G3 R 格式：由半徑求圓心偏移 (i, j)。R 負＝走大於 180° 的弧"""
    dx, dy = x1 - x0, y1 - y0
    d = math.hypot(dx, dy)
    if d < 1e-9 or abs(r) < d / 2 - 1e-6:
        raise ValueError(f"R{r} 的圓弧到不了終點（弦長 {d:.3f}）")
    h = math.sqrt(max(0.0, r * r - d * d / 4))
    mx, my = dx / 2, dy / 2
    sign = 1 if (cw ^ (r < 0)) else -1
    return mx + sign * h * dy / d, my - sign * h * dx / d


def tool_table():
    """印出刀具表"""
    for t, (d, length, flute, name) in TOOLS.items():
        print(f"T{t:<2} {name:10s} 直徑 {d:>4} mm  全長 {length} mm  刃長 {flute} mm")
