#!/usr/bin/env python3
"""LinuxCNC ↔ 網頁孿生（dt.qianpro.shop/vmc/）的橋接：HAL 使用者空間元件 dtvmc。

LinuxCNC 是控制器（G-code 解譯、路徑規劃、加減速、M6 換刀程式 toolchange.ngc），孿生是機台：
    LinuxCNC 的命令 → 孿生           x/y/z 位置、主軸轉速、刀庫位置、換刀臂／鬆刀／刀庫鎖銷的電磁閥
    孿生的感測器   → LinuxCNC        換刀臂進／出、已鬆刀／已夾刀、已上鎖（toolchange.ngc 的 M66 等的就是這些）
    孿生的警報     → LinuxCNC E-stop  撞刀、扯刀、沒轉就切削……孿生一發警報，LinuxCNC 就急停
    LinuxCNC 要求解除 E-stop（F1）    → 清除孿生的警報

刀庫馬達與編碼器（index／pulse）留在 HAL 裡模擬（毫秒級的脈波，50 Hz 的網路取樣不到），這裡把位置鏡射給孿生。

    loadusr -Wn dtvmc python3 dtvmc.py          # twin_vmc.hal 裡
環境變數：DT_HOST（預設 0.0.0.0）、DT_PORT（預設 8765）、DTLINK_PATH（dtlink.py 所在目錄，預設這支程式旁邊）
"""
import os
import subprocess
import sys
import time

import hal

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.environ.get("DTLINK_PATH", HERE))
from dtlink import Twin  # noqa: E402

ROOT = "MAIN.FG_VMC."
CAR_OFFSET = float(os.environ.get("DT_CAROUSEL_OFFSET", "0"))   # HAL car-pos → 孿生刀庫值（孿生 0＝刀位 1 對主軸）

c = hal.component("dtvmc")
for n in ("x", "y", "z", "spindle-vel", "car-pos"):
    c.newpin(n, hal.HAL_FLOAT, hal.HAL_IN)
for n in ("arm-act", "tool-release", "car-lock", "reset-request", "orient"):
    c.newpin(n, hal.HAL_BIT, hal.HAL_IN)
for n in ("arm-in", "arm-out", "tool-released", "tool-locked", "car-locked", "ok", "connected"):
    c.newpin(n, hal.HAL_BIT, hal.HAL_OUT)
c.newpin("alarm", hal.HAL_S32, hal.HAL_OUT)
c.newpin("tool", hal.HAL_S32, hal.HAL_OUT)
c.newpin("cut-volume", hal.HAL_FLOAT, hal.HAL_OUT)       # 孿生回報的切削體積（cm³）
c.ready()

twin = Twin(host=os.environ.get("DT_HOST", "0.0.0.0"), port=int(os.environ.get("DT_PORT", "8765")), name="linuxcnc")


def log(*a):
    print("[dtvmc]", *a, flush=True)


def home_carousel():
    """開機刀庫歸零：LinuxCNC 2.9 的 carousel 元件（index 編碼）第一次動作只做歸零就回報 ready，
    不會轉到要求的刀位——第一次換刀會拿錯刀（原版 vismach 模擬沒畫刀具所以看不出來）。
    像實機開機一樣先歸零一次：暫時接手 enable／pocket-number，要刀位 1，等 ready，再接回 LinuxCNC。"""
    h = lambda *a: subprocess.run(["halcmd", *a], capture_output=True, text=True).stdout.strip()
    if h("getp", "carousel.0.ready") == "" or h("getp", "motion.digital-out-00") == "TRUE":
        return                                      # 還沒載入刀庫元件，或 LinuxCNC 正在用刀庫
    h("unlinkp", "carousel.0.enable")
    h("unlinkp", "carousel.0.pocket-number")
    h("setp", "carousel.0.pocket-number", "1")
    h("setp", "carousel.0.enable", "1")
    t0 = time.time()
    while h("getp", "carousel.0.ready") != "TRUE" and time.time() - t0 < 15:
        time.sleep(0.1)
    h("setp", "carousel.0.enable", "0")
    h("net", "car-enable", "carousel.0.enable")
    h("net", "car-pos-s32", "carousel.0.pocket-number")
    log("刀庫歸零：刀位", h("getp", "carousel.0.current-position"), "，car-pos", h("getp", "carpos.out"))


_homed = False


def prepare():
    """孿生剛連上：（第一次）刀庫歸零；清警報、把主軸上的刀卸回刀庫（LinuxCNC 開機時主軸上沒有刀），Z 回 0"""
    global _homed
    if not _homed:
        home_carousel()
        _homed = True
    sys.path.insert(0, HERE)
    from cnc import VMC
    m = VMC(twin)
    m.verbose = False
    if m.alarm:
        m.reset_alarm()
    m.move_machine(z=0)
    if m.tool:
        log(f"孿生主軸上有 T{m.tool}，先卸回刀庫（LinuxCNC 開機時主軸是空的）")
        m.tool_change(0)
    m.move_machine(x=0, y=0, z=0)
    log("孿生就緒：主軸無刀、刀庫刀位", int(round(m.carousel.value)) % 10 + 1)


def run():
    names = ("M_AxisX", "M_AxisY", "M_AxisZ", "M_Spindle", "M_Carousel", "Y_Arm", "Y_ToolRelease", "Y_CarouselLock")
    dev = {}
    last_cmd = {}
    ready = False
    last_enable = False
    while True:
        try:
            if not twin.connected:
                if ready:
                    log("孿生斷線")
                ready = False
                c["connected"] = c["ok"] = False
                for n in ("arm-in", "arm-out", "tool-released", "tool-locked", "car-locked"):
                    c[n] = False
                time.sleep(0.2)
                continue
            if not ready:
                c["connected"] = True
                dev = {n: twin.device(ROOT + n) for n in names}     # 裝置清單要等孿生送來 manifest 才有
                prepare()
                last_cmd.clear()
                ready = True
            # 命令 → 孿生
            dev["M_AxisX"].target = c["x"]
            dev["M_AxisY"].target = c["y"]
            dev["M_AxisZ"].target = c["z"]
            # M19 主軸定位（換刀前）時，HAL 的位置迴路會讓主軸慢慢轉；孿生不模擬主軸角度，定位中送 0
            dev["M_Spindle"].target = 0.0 if c["orient"] else c["spindle-vel"]
            dev["M_Carousel"].target = c["car-pos"] + CAR_OFFSET
            for pin, name in (("arm-act", "Y_Arm"), ("tool-release", "Y_ToolRelease"), ("car-lock", "Y_CarouselLock")):
                v = bool(c[pin])
                if last_cmd.get(pin) != v:
                    (dev[name].extend if v else dev[name].retract)()
                    last_cmd[pin] = v
            # 孿生的感測器 → LinuxCNC
            c["arm-in"], c["arm-out"] = dev["Y_Arm"].extended, dev["Y_Arm"].retracted
            c["tool-released"], c["tool-locked"] = dev["Y_ToolRelease"].extended, dev["Y_ToolRelease"].retracted
            c["car-locked"] = dev["Y_CarouselLock"].extended
            alarm = int(round(twin.read(ROOT + "B_Alarm.StatusData")))
            if alarm and c["alarm"] != alarm:
                log(f"孿生警報 {alarm} → LinuxCNC E-stop")
            c["alarm"] = alarm
            c["tool"] = int(round(twin.read(ROOT + "B_Tool.StatusData")))
            c["cut-volume"] = twin.read(ROOT + "B_CutVolume.StatusData")
            c["ok"] = alarm == 0
            # LinuxCNC 要求解除 E-stop（iocontrol.user-request-enable，HAL 裡拉寬成 0.5 秒）→ 清除孿生警報
            enable = bool(c["reset-request"])
            if enable and not last_enable and alarm:
                lamp = twin.device(ROOT + "Q_ResetAlarm")
                lamp.on(); time.sleep(0.5); lamp.off()
                log("E-stop 解除 → 清除孿生警報")
            last_enable = enable
            time.sleep(0.02)
        except KeyboardInterrupt:
            raise SystemExit(0)
        except Exception as e:                       # 孿生在換頁、重連時讀寫會失敗：等它回來
            log("錯誤：", e)
            ready = False
            time.sleep(0.5)


run()
