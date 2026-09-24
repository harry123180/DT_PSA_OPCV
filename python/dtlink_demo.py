"""示範：用 Python 當 PLC，讓托盤在整條產線上循環。

    pip install websockets
    python dtlink_demo.py
然後開 https://dt.qianpro.shop

托盤流向（從原專案 MIL 場景的 _nextSequence 解出來）：
  上層：Lift01(上升) → Stopper03 → Index01 → Index02 → Index03 → Index04 → Index05 → Stopper04 → Lift02(下降)
  下層：Stopper01 → Stopper02 → 回到 Lift01

每一站寫成一段「步進程序」：一步一步做，每一步等條件成立才往下。
下一站「就緒」才放行，托盤不會互撞——這就是 PLC 產線控制最基本的交握（handshake）。
按 Ctrl+C 結束（會把所有輸出歸零）。
"""
import time

from dtlink import Twin

T = "MAIN.FG_Transport."


class Station:
    """一站的共用狀態：ready＝可以收下一個托盤；incoming＝上一站已經送過來了。"""

    def __init__(self, name: str):
        self.name = name
        self.ready = False
        self.incoming = False
        self.next: "Station | None" = None
        self.step = ""

    def send_to_next(self):
        self.next.ready = False
        self.next.incoming = True


# ── 步進程序：每個 yield 交出一個「條件」，排程器等它成立才繼續 ────────────
def wait(seconds: float):
    end = time.time() + seconds
    return lambda: time.time() >= end


def stopper_station(st: Station, twin: Twin):
    """擋料站：縮回擋住 → 托盤到了 → 等下一站就緒 → 伸出放行 → 等托盤離開。"""
    stopper = twin.device(f"{T}{st.name}_Y_Stopper")
    detect = twin.device(f"{T}{st.name}_B_Detect")
    exit_ = twin.device(f"{T}{st.name}_B_Exit")
    while True:
        st.step = "擋住，等托盤"
        stopper.retract()
        yield lambda: stopper.retracted
        st.ready = True
        yield lambda: detect.value
        st.ready, st.incoming = False, False
        st.step = "托盤到位，等下一站"
        yield lambda: st.next.ready
        st.send_to_next()
        st.step = "放行"
        stopper.extend()
        yield lambda: exit_.value


def index_station(st: Station, twin: Twin, work_seconds: float = 1.0):
    """分度站：擋住 → 托盤到了 → 頂升到加工位置 → 加工（這裡用等待代替）→ 放下 → 放行。"""
    stopper = twin.device(f"{T}{st.name}_Y_Stopper")
    lift = twin.device(f"{T}{st.name}_Y_Lift")
    detect = twin.device(f"{T}{st.name}_B_Detect")
    exit_ = twin.device(f"{T}{st.name}_B_Exit")
    while True:
        st.step = "擋住，等托盤"
        stopper.retract()
        lift.retract()
        yield lambda: stopper.retracted and lift.retracted
        st.ready = True
        yield lambda: detect.value
        st.ready, st.incoming = False, False
        st.step = "頂升加工"
        lift.extend()
        yield lambda: lift.extended
        yield wait(work_seconds)
        lift.retract()
        yield lambda: lift.retracted
        st.step = "加工完成，等下一站"
        yield lambda: st.next.ready
        st.send_to_next()
        st.step = "放行"
        stopper.extend()
        yield lambda: exit_.value


def lift_station(st: Station, twin: Twin, down_first: bool):
    """升降機：停在收料層 → 上一站送來時轉輸送帶收料 → 換層 → 等下一站就緒 → 送出。
    Lift01 平常在下層（縮回）、往上送；Lift02 平常在上層（伸出）、往下送。"""
    lift = twin.device(f"{T}{st.name}_Y_Lift")
    conveyor = twin.device(f"{T}{st.name}_M_Conveyor")
    detect = twin.device(f"{T}{st.name}_B_Detect")
    exit_ = twin.device(f"{T}{st.name}_B_Exit")
    at_home = (lambda: lift.retracted) if down_first else (lambda: lift.extended)
    at_other = (lambda: lift.extended) if down_first else (lambda: lift.retracted)
    go_home = lift.retract if down_first else lift.extend
    go_other = lift.extend if down_first else lift.retract
    while True:
        st.step = "回收料層"
        conveyor.stop()
        go_home()
        yield at_home
        st.ready = True
        yield lambda: st.incoming or detect.value
        st.ready, st.incoming = False, False
        st.step = "收料"
        conveyor.backward()
        yield lambda: detect.value
        conveyor.stop()
        yield wait(0.5)
        st.step = "換層"
        go_other()
        yield at_other
        st.step = "等下一站"
        yield lambda: st.next.ready
        st.send_to_next()
        st.step = "送出"
        conveyor.forward()
        yield lambda: exit_.value


# ── 排程器：輪流推進每一站（就像 PLC 每個掃描週期把所有程式跑一遍）───────────
def run(programs, stations, twin: Twin, cycle: float = 0.02):
    waiting = {p: None for p in programs}
    last_print = 0.0
    while True:
        for p in programs:
            cond = waiting[p]
            if cond is None or cond():
                waiting[p] = next(p)
        if time.time() - last_print > 2:
            last_print = time.time()
            print(" | ".join(f"{s.name}:{s.step}" for s in stations))
        time.sleep(cycle)


def main():
    twin = Twin(name="dtlink_demo")
    twin.wait_connected()
    time.sleep(0.5)

    names = ["Lift01", "Stopper03", "Index01", "Index02", "Index03", "Index04", "Index05",
             "Stopper04", "Lift02", "Stopper01", "Stopper02"]
    stations = [Station(n) for n in names]
    for a, b in zip(stations, stations[1:] + stations[:1]):
        a.next = b
    by = {s.name: s for s in stations}

    # 上下兩層主輸送帶一直轉（原專案 MIL 也是這樣：Backward）
    for name in ("Transport01_M_Conveyor", "Transport02_M_Conveyor"):
        twin.device(T + name).backward()

    programs = [
        lift_station(by["Lift01"], twin, down_first=True),
        lift_station(by["Lift02"], twin, down_first=False),
        *[stopper_station(by[n], twin) for n in ("Stopper01", "Stopper02", "Stopper03", "Stopper04")],
        *[index_station(by[n], twin) for n in ("Index01", "Index02", "Index03", "Index04", "Index05")],
    ]
    print("開始循環；Ctrl+C 結束")
    try:
        run(programs, stations, twin)
    except KeyboardInterrupt:
        pass
    finally:
        twin.stop_all()
        time.sleep(0.3)
        print("已停止，所有輸出歸零")


if __name__ == "__main__":
    main()
