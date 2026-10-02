#!/usr/bin/env python3
"""不開 GUI，用 linuxcnc Python 模組操作 LinuxCNC：等孿生連上 → 解除 E-stop → 開機 → 回原點 → 執行 G-code。

    python3 run_program.py demo.ngc            # LinuxCNC（vmc_twin.ini）已經在跑
結束碼 0＝程式跑完、沒有 LinuxCNC 錯誤、孿生沒有警報。
"""
import subprocess
import sys
import time

import linuxcnc

prog = sys.argv[1]
s, c, e = linuxcnc.stat(), linuxcnc.command(), linuxcnc.error_channel()


def pin(name):
    return subprocess.run(["halcmd", "getp", name], capture_output=True, text=True).stdout.strip()


def wait(cond, timeout, what):
    t0 = time.time()
    while time.time() - t0 < timeout:
        s.poll()
        if cond():
            return
        err = e.poll()
        if err:
            print("LinuxCNC:", err[1])
        time.sleep(0.1)
    raise SystemExit(f"逾時：{what}")


wait(lambda: pin("dtvmc.connected") == "TRUE" and pin("dtvmc.ok") == "TRUE", 300, "孿生連上（瀏覽器開 ?ws=ws://127.0.0.1:8765）")
print("孿生已連上，主軸刀號", pin("dtvmc.tool"))
c.state(linuxcnc.STATE_ESTOP_RESET)
wait(lambda: s.task_state == linuxcnc.STATE_ESTOP_RESET, 10, "解除 E-stop")
c.state(linuxcnc.STATE_ON)
wait(lambda: s.task_state == linuxcnc.STATE_ON, 10, "開機")
c.mode(linuxcnc.MODE_MANUAL)
c.wait_complete()
c.home(-1)
wait(lambda: all(s.homed[i] for i in range(3)), 120, "回原點")
print("回原點完成", [round(v, 3) for v in s.actual_position[:3]])
c.mode(linuxcnc.MODE_AUTO)
c.wait_complete()
c.program_open(prog)
c.auto(linuxcnc.AUTO_RUN, 0)
t0 = time.time()
errors = []
started = False
while time.time() - t0 < 1800:
    s.poll()
    err = e.poll()
    if err:
        print("LinuxCNC:", err[1])
        if err[0] in (linuxcnc.NML_ERROR, linuxcnc.OPERATOR_ERROR):
            errors.append(err[1])
    if s.interp_state != linuxcnc.INTERP_IDLE:
        started = True
    if started and s.interp_state == linuxcnc.INTERP_IDLE:
        break
    if s.task_state != linuxcnc.STATE_ON:
        errors.append(f"機台離開 ON 狀態（task_state={s.task_state}，孿生警報 {pin('dtvmc.alarm')}）")
        break
    time.sleep(0.2)
s.poll()
print(f"程式結束（{time.time() - t0:.0f} s），行號 {s.motion_line}，主軸上的刀 T{s.tool_in_spindle}，"
      f"孿生刀號 {pin('dtvmc.tool')}、警報 {pin('dtvmc.alarm')}")
ok = not errors and pin("dtvmc.alarm") == "0" and str(s.tool_in_spindle) == pin("dtvmc.tool")
print("PASS" if ok else f"FAIL {errors}")
sys.exit(0 if ok else 1)
