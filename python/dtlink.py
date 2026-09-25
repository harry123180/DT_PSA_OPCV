"""dtlink —— 用 Python 控制網頁版的 DT_PSA_OPCV 數位孿生。

在自己的電腦執行 Python：這支程式開一個本機 WebSocket 伺服器（預設 ws://127.0.0.1:8765），
瀏覽器裡的孿生會自動連過來。之後你的 Python 程式就扮演 PLC：
寫「控制」（Control：氣缸伸出、輸送帶轉動、燈亮）、讀「狀態」（Status：到位、感測器、按鈕）。

    from dtlink import Twin
    twin = Twin()                     # 開伺服器
    twin.wait_connected()             # 等網頁連上
    for d in twin.devices("Cylinder"):
        print(d)
    cyl = twin.device("MAIN.FG_System....")
    cyl.extend()
    twin.wait_until(lambda: cyl.extended, timeout=5)

需要：pip install websockets
協定說明見 README 或 Unity/Assets/PythonLink/PythonLinkClient.cs。
"""
from __future__ import annotations

import asyncio
import fnmatch
import json
import struct
import threading
import time
from dataclasses import dataclass
from typing import Callable, Iterable

try:
    import websockets
except ImportError:  # 網頁版（Pyodide）用不到 websockets
    websockets = None

_REG_RE = __import__("re").compile(r"^%([QI])([XBWD])?(\d+)(?:\.([0-7]))?$")
FRAME_STATUS = 0x01
FRAME_CONTROL = 0x02
_FMT = {"u8": "B", "i8": "b", "u16": "H", "i16": "h", "u32": "I", "i32": "i",
        "u64": "Q", "i64": "q", "f32": "f", "f64": "d"}


@dataclass
class Variable:
    name: str
    dir: str        # "control"（Python 寫）或 "status"（Python 讀）
    offset: int
    size: int
    dtype: str
    count: int

    @property
    def fmt(self) -> str:
        return "<" + (_FMT[self.dtype] * self.count)


class Twin:
    """一個網頁孿生的連線。同時只接受一個瀏覽器分頁，新的連線會取代舊的。"""

    def __init__(self, host: str = "127.0.0.1", port: int = 8765, name: str = "python", verbose: bool = True):
        if websockets is None:
            raise SystemExit("缺少套件：請先執行  pip install websockets")
        self.host, self.port, self.name, self.verbose = host, port, name, verbose
        self.variables: dict[str, Variable] = {}
        self.device_info: list[dict] = []
        self._control = bytearray()
        self._status = b""
        self._status_time = 0.0
        self._lock = threading.Lock()
        self._dirty = threading.Event()
        self._connected = threading.Event()
        self._ws = None
        self._loop = asyncio.new_event_loop()
        self._started = threading.Event()
        self._thread = threading.Thread(target=self._run, name="dtlink", daemon=True)
        self._thread.start()
        self._started.wait(5)
        if self.verbose:
            print(f"[dtlink] 等待網頁孿生連線：ws://{host}:{port}")

    # ── 連線 ──────────────────────────────────────────────
    def _run(self):
        asyncio.set_event_loop(self._loop)
        self._loop.run_until_complete(self._serve())

    async def _serve(self):
        async with websockets.serve(self._handler, self.host, self.port, max_size=None):
            self._started.set()
            await asyncio.gather(self._sender(), asyncio.Future())

    async def _handler(self, ws):
        if self._ws is not None:
            try:
                await self._ws.close()
            except Exception:
                pass
        self._ws = ws
        try:
            async for msg in ws:
                if isinstance(msg, str):
                    self._on_text(msg)
                    await ws.send(json.dumps({"type": "hello", "name": self.name}))
                elif msg and msg[0] == FRAME_STATUS:
                    with self._lock:
                        self._status = bytes(msg[1:])
                        self._status_time = time.time()
        except websockets.ConnectionClosed:
            pass
        finally:
            if self._ws is ws:
                self._ws = None
                self._connected.clear()
                if self.verbose:
                    print("[dtlink] 網頁孿生已斷線（重新整理網頁或等它自動重連）")

    def _on_text(self, text: str):
        m = json.loads(text)
        if m.get("type") != "manifest":
            return
        variables = {v["name"]: Variable(**{k: v[k] for k in ("name", "dir", "offset", "size", "dtype", "count")})
                     for v in m["variables"]}
        with self._lock:
            # 重連且版面相同：保留目前的控制輸出，機台接著原本的狀態跑
            if len(self._control) != m["controlSize"] or variables.keys() != self.variables.keys():
                self._control = bytearray(m["controlSize"])
            self.variables = variables
            self.device_info = m["devices"]
            self._status = bytes(m["statusSize"])
        self._connected.set()
        self._dirty.set()
        if self.verbose:
            print(f"[dtlink] 已連線：{len(self.device_info)} 個裝置，控制 {m['controlSize']} B、狀態 {m['statusSize']} B")

    async def _sender(self):
        """控制映像有變就在 20 ms 內送出；沒變也每 0.2 秒重送一次，網頁重整後馬上接得上。"""
        last = 0.0
        while True:
            await asyncio.sleep(0.02)
            ws = self._ws
            if ws is None or not self._connected.is_set():
                continue
            now = time.time()
            if not self._dirty.is_set() and now - last < 0.2:
                continue
            self._dirty.clear()
            with self._lock:
                frame = bytes([FRAME_CONTROL]) + bytes(self._control)
            try:
                await ws.send(frame)
                last = now
            except Exception:
                pass

    @property
    def connected(self) -> bool:
        return self._connected.is_set()

    def wait_connected(self, timeout: float | None = None) -> bool:
        ok = self._connected.wait(timeout)
        if not ok:
            raise TimeoutError("網頁孿生沒有連上：確認網頁已開啟，且網址參數 ws= 與這裡的埠號一致")
        return ok

    # ── 讀寫變數 ──────────────────────────────────────────
    def _var(self, name: str) -> Variable:
        try:
            return self.variables[name]
        except KeyError:
            raise KeyError(f"找不到變數 {name}；用 twin.find('*關鍵字*') 查名稱") from None

    def read(self, name: str):
        v = self._var(name)
        with self._lock:
            buf = self._status_bytes() if v.dir == "status" else self._control_bytes()
            values = struct.unpack_from(v.fmt, buf, v.offset)
        return values[0] if v.count == 1 else list(values)

    def write(self, name: str, value):
        v = self._var(name)
        if v.dir != "control":
            raise ValueError(f"{name} 是狀態（孿生回報的），不能寫；控制變數名稱結尾是 .Control 或 .ControlData")
        values = value if isinstance(value, (list, tuple)) else [value]
        with self._lock:
            self._sync_control(v.offset, v.size)
            struct.pack_into(v.fmt, self._control, v.offset, *values)
        self._control_changed(v.offset, v.size)

    # ── 傳輸相關的掛勾：網頁版（dtlink_web）覆寫 ──
    def _status_bytes(self) -> bytes:
        return self._status

    def _control_bytes(self):
        return self._control

    def _sync_control(self, offset: int, size: int):
        """改控制映像之前先同步那一段（網頁版：暫存器表可能手動改過）。本機版控制映像只有 Python 在寫，不用同步。"""

    def _control_changed(self, offset: int | None = None, size: int | None = None):
        self._dirty.set()

    def read_bit(self, name: str, bit: int) -> bool:
        return bool((int(self.read(name)) >> bit) & 1)

    def write_bit(self, name: str, bit: int, value: bool):
        v = self._var(name)
        with self._lock:
            self._sync_control(v.offset, v.size)
            (cur,) = struct.unpack_from(v.fmt, self._control, v.offset)
            cur = (cur | (1 << bit)) if value else (cur & ~(1 << bit))
            struct.pack_into(v.fmt, self._control, v.offset, cur)
        self._control_changed(v.offset, v.size)

    # ── 暫存器（過程映像，IEC 61131-3 位址）──────────────
    # %Q＝輸出區（控制映像，Python 寫）、%I＝輸入區（狀態映像，孿生回報）
    # 寬度：X＝位元（%QX5.1）、B＝位元組、W＝字（2）、D＝雙字（4；該位置是浮點變數時當 REAL）
    def _parse_reg(self, address: str):
        m = _REG_RE.match(address.strip().upper())
        if not m:
            raise ValueError(f"暫存器位址格式不對：{address}（例：%QX5.1、%IB12、%QD20）")
        area, width, byte, bit = m.group(1), m.group(2) or "X", int(m.group(3)), m.group(4)
        if width == "X" and bit is None:
            raise ValueError(f"位元位址要寫成 %{area}X位元組.位元，例如 %{area}X{byte}.0")
        if width != "X" and bit is not None:
            raise ValueError(f"{address}：只有 X（位元）可以帶 .位元")
        size = {"X": 1, "B": 1, "W": 2, "D": 4}[width]
        limit = len(self._control) if area == "Q" else len(self._status_bytes())
        if byte + size > limit:
            raise ValueError(f"{address} 超出範圍：%{area} 區只有 {limit} 個位元組")
        return area, width, byte, (int(bit) if bit is not None else None), size

    def _reg_is_real(self, area: str, byte: int) -> bool:
        d = "control" if area == "Q" else "status"
        return any(v.dir == d and v.dtype == "f32" and v.offset <= byte < v.offset + v.size and (byte - v.offset) % 4 == 0
                   for v in self.variables.values())

    def read_reg(self, address: str):
        """讀暫存器：%IX12.1 → bool、%IB12 → int、%QD20 → float（REAL）或 int。"""
        area, width, byte, bit, size = self._parse_reg(address)
        with self._lock:
            buf = self._control_bytes() if area == "Q" else self._status_bytes()
            if width == "X":
                return bool((buf[byte] >> bit) & 1)
            if width == "D" and self._reg_is_real(area, byte):
                return struct.unpack_from("<f", buf, byte)[0]
            return int.from_bytes(bytes(buf[byte:byte + size]), "little")

    def write_reg(self, address: str, value):
        """寫暫存器（只能寫 %Q 輸出區）：write_reg("%QX5.1", True)、write_reg("%QB5", 2)、write_reg("%QD20", 1.5)。"""
        area, width, byte, bit, size = self._parse_reg(address)
        if area != "Q":
            raise ValueError(f"{address} 是輸入區（%I，機台回報的），只能讀；輸出區是 %Q")
        with self._lock:
            self._sync_control(byte, size)
            if width == "X":
                cur = self._control[byte]
                self._control[byte] = (cur | (1 << bit)) if value else (cur & ~(1 << bit))
            elif width == "D" and self._reg_is_real(area, byte):
                struct.pack_into("<f", self._control, byte, float(value))
            else:
                self._control[byte:byte + size] = int(value).to_bytes(size, "little", signed=int(value) < 0)
        self._control_changed(byte, size)

    def reg_table(self, keyword: str = "") -> list[dict]:
        """暫存器對照表：位址、EtherCAT 風格位移量、變數名稱、型別、目前值。"""
        rows = []
        for v in self.variables.values():
            if keyword and keyword.lower() not in v.name.lower():
                continue
            area = "Q" if v.dir == "control" else "I"
            width = "D" if v.dtype in ("f32", "u32", "i32") else ("W" if v.size == 2 else "B")
            rows.append({"address": f"%{area}{width}{v.offset}", "offset": f"0x{v.offset:04X}", "name": v.name,
                         "dtype": v.dtype, "size": v.size, "value": self.read(v.name)})
        return sorted(rows, key=lambda r: (r["address"][1], int(r["offset"], 16)))

    def print_regs(self, keyword: str = ""):
        for r in self.reg_table(keyword):
            val = f"{r['value']:.3f}" if isinstance(r["value"], float) else (
                f"{r['value']:3d}  0b{r['value']:08b}" if isinstance(r["value"], int) and r["size"] == 1 else r["value"])
            print(f"  {r['address']:8s} {r['offset']}  {r['dtype']:4s} {val!s:>16s}  {r['name']}")

    def find(self, pattern: str) -> list[str]:
        """用萬用字元找變數名稱，例如 twin.find('*Stopper*')。"""
        return [n for n in self.variables if fnmatch.fnmatch(n, pattern)]

    # ── 裝置 ──────────────────────────────────────────────
    def devices(self, keyword: str = "", type: str = "") -> list["Device"]:
        """列出裝置；keyword 比對路徑（不分大小寫），type 比對 FB 類型（例如 'FB_Cylinder'）。"""
        out = []
        for d in self.device_info:
            if keyword and keyword.lower() not in d["path"].lower():
                continue
            if type and d["type"] != type:
                continue
            out.append(make_device(self, d))
        return out

    def device(self, path: str) -> "Device":
        for d in self.device_info:
            if d["path"] == path:
                return make_device(self, d)
        close = [d["path"] for d in self.device_info if path.lower() in d["path"].lower()]
        hint = f"；相近的有：{close[:5]}" if close else ""
        raise KeyError(f"找不到裝置 {path}{hint}")

    # ── 時間 ──────────────────────────────────────────────
    def sleep(self, seconds: float):
        time.sleep(seconds)

    def wait_until(self, condition: Callable[[], bool], timeout: float = 10.0, poll: float = 0.02) -> bool:
        """等條件成立；逾時丟 TimeoutError（控制程式卡住時比無限等待容易除錯）。"""
        end = time.time() + timeout
        while time.time() < end:
            if condition():
                return True
            time.sleep(poll)
        raise TimeoutError(f"等待逾時（{timeout} 秒）")

    def stop_all(self):
        """把所有控制輸出歸零（所有氣缸、馬達停止輸出）。"""
        with self._lock:
            self._control[:] = bytes(len(self._control))
        self._control_changed(0, len(self._control))


# ── 各類裝置的包裝：位元意義取自 Open Commissioning 的元件原始碼 ──────────
class Device:
    def __init__(self, twin: Twin, info: dict):
        self.twin, self.path, self.type = twin, info["path"], info["type"]
        self.component, self.name = info.get("component", ""), info.get("name", "")

    def _has(self, suffix: str) -> bool:
        return f"{self.path}.{suffix}" in self.twin.variables

    @property
    def control(self) -> int:
        return self.twin.read(f"{self.path}.Control")

    @property
    def status(self) -> int:
        return self.twin.read(f"{self.path}.Status")

    def __repr__(self) -> str:
        comp = f" {self.component}" if self.component else ""
        return f"<{self.type}{comp} {self.path}>"


class Cylinder(Device):
    """氣缸：Control bit0＝縮回（minus）、bit1＝伸出（plus）；Status bit0／bit1＝到達縮回／伸出端。"""

    def extend(self):
        self.twin.write(f"{self.path}.Control", 0b10)

    def retract(self):
        self.twin.write(f"{self.path}.Control", 0b01)

    def release(self):
        self.twin.write(f"{self.path}.Control", 0)

    @property
    def extended(self) -> bool:
        return self.twin.read_bit(f"{self.path}.Status", 1)

    @property
    def retracted(self) -> bool:
        return self.twin.read_bit(f"{self.path}.Status", 0)


class Sensor(Device):
    """二元感測器：Status bit0。"""

    @property
    def value(self) -> bool:
        return self.twin.read_bit(f"{self.path}.Status", 0)

    def __bool__(self) -> bool:
        return self.value


class Drive(Device):
    """驅動：DriveSimple 用 Control bit0／bit1＝正轉／反轉；
    DriveSpeed／DrivePosition 用 ControlData（目標速度或位置，float），StatusData 回報目前值。"""

    def forward(self):
        self.twin.write(f"{self.path}.Control", 0b01)

    def backward(self):
        self.twin.write(f"{self.path}.Control", 0b10)

    def stop(self):
        self.twin.write(f"{self.path}.Control", 0)
        if self._has("ControlData") and self.component == "DriveSpeed":
            self.twin.write(f"{self.path}.ControlData", 0.0)

    @property
    def target(self) -> float:
        return self.twin.read(f"{self.path}.ControlData")

    @target.setter
    def target(self, value: float):
        self.twin.write(f"{self.path}.ControlData", float(value))

    @property
    def value(self) -> float:
        return self.twin.read(f"{self.path}.StatusData")

    @property
    def active(self) -> bool:
        return self.twin.read_bit(f"{self.path}.Status", 6)


class Button(Device):
    """按鈕：Status bit0＝按下；Control bit0＝按鈕燈（回授）。"""

    @property
    def pressed(self) -> bool:
        return self.twin.read_bit(f"{self.path}.Status", 0)

    def light(self, on: bool = True):
        self.twin.write_bit(f"{self.path}.Control", 0, on)


class Lamp(Device):
    """燈：Control bit0。"""

    def on(self):
        self.twin.write_bit(f"{self.path}.Control", 0, True)

    def off(self):
        self.twin.write_bit(f"{self.path}.Control", 0, False)


class Lock(Device):
    """安全門鎖：Control bit0＝上鎖；Status bit0＝門已關、bit1＝已上鎖。"""

    def lock(self):
        self.twin.write_bit(f"{self.path}.Control", 0, True)

    def unlock(self):
        self.twin.write_bit(f"{self.path}.Control", 0, False)

    @property
    def closed(self) -> bool:
        return self.twin.read_bit(f"{self.path}.Status", 0)

    @property
    def locked(self) -> bool:
        return self.twin.read_bit(f"{self.path}.Status", 1)


class Switch(Device):
    """選擇開關：Status 的第 n 個位元代表切在第 n+1 段（0＝第 0 段）。"""

    @property
    def position(self) -> int:
        s = self.status
        return s.bit_length()


_TYPES = {
    "FB_Cylinder": Cylinder,
    "FB_SensorBinary": Sensor,
    "FB_Drive": Drive,
    "FB_Button": Button,
    "FB_Lamp": Lamp,
    "FB_Lock": Lock,
    "FB_Switch": Switch,
}


def make_device(twin: Twin, info: dict) -> Device:
    return _TYPES.get(info["type"], Device)(twin, info)


def print_devices(twin: Twin, items: Iterable[Device] | None = None):
    for d in items if items is not None else twin.devices():
        print(f"  {d.type:16s} {d.component:14s} {d.path}")
