"""不開瀏覽器，用假孿生測 dtlink 的協定：manifest、控制映像、狀態映像、位元意義。

    python test_dtlink_protocol.py
"""
import asyncio
import json
import struct
import threading

import websockets

from dtlink import Twin

PORT = 8799
MANIFEST = {
    "type": "manifest", "version": 1, "root": "MAIN", "controlSize": 6, "statusSize": 9,
    "variables": [
        {"name": "MAIN.Cyl.Control", "dir": "control", "offset": 0, "size": 1, "dtype": "u8", "count": 1},
        {"name": "MAIN.Cyl.Status", "dir": "status", "offset": 0, "size": 1, "dtype": "u8", "count": 1},
        {"name": "MAIN.Drv.Control", "dir": "control", "offset": 1, "size": 1, "dtype": "u8", "count": 1},
        {"name": "MAIN.Drv.Status", "dir": "status", "offset": 1, "size": 1, "dtype": "u8", "count": 1},
        {"name": "MAIN.Drv.ControlData", "dir": "control", "offset": 2, "size": 4, "dtype": "f32", "count": 1},
        {"name": "MAIN.Drv.StatusData", "dir": "status", "offset": 2, "size": 4, "dtype": "f32", "count": 1},
        {"name": "MAIN.Sen.Status", "dir": "status", "offset": 6, "size": 1, "dtype": "u8", "count": 1},
        {"name": "MAIN.Btn.Status", "dir": "status", "offset": 7, "size": 1, "dtype": "u8", "count": 1},
        {"name": "MAIN.Lamp.Control", "dir": "control", "offset": 6 - 0, "size": 0, "dtype": "u8", "count": 0},
    ],
    "devices": [
        {"path": "MAIN.Cyl", "type": "FB_Cylinder", "name": "Cyl", "component": "Cylinder"},
        {"path": "MAIN.Drv", "type": "FB_Drive", "name": "Drv", "component": "DriveSpeed"},
        {"path": "MAIN.Sen", "type": "FB_SensorBinary", "name": "Sen", "component": "SensorBinary"},
        {"path": "MAIN.Btn", "type": "FB_Button", "name": "Btn", "component": "Button"},
    ],
}
MANIFEST["variables"].pop()  # 最後一筆只是確認 JSON 結構，不用

received = []


async def fake_twin(ready: threading.Event):
    async with websockets.connect(f"ws://127.0.0.1:{PORT}") as ws:
        await ws.send(json.dumps(MANIFEST))
        ready.set()
        status = bytearray(9)
        while True:
            msg = await asyncio.wait_for(ws.recv(), timeout=5)
            if isinstance(msg, str):
                continue
            assert msg[0] == 0x02 and len(msg) == 7, (msg[0], len(msg))
            control = msg[1:]
            received.append(bytes(control))
            # 模擬孿生：氣缸照控制到位、驅動值跟隨目標、感測器與按鈕固定為 1
            status[0] = 0b10 if control[0] & 0b10 else (0b01 if control[0] & 0b01 else 0)
            status[1] = 0
            status[2:6] = control[2:6]
            status[6] = 1
            status[7] = 1
            await ws.send(bytes([0x01]) + bytes(status))


def main():
    twin = Twin(port=PORT, verbose=False)
    ready = threading.Event()
    t = threading.Thread(target=lambda: asyncio.run(fake_twin(ready)), daemon=True)
    t.start()
    twin.wait_connected(5)

    cyl = twin.device("MAIN.Cyl")
    drv = twin.device("MAIN.Drv")
    sen = twin.device("MAIN.Sen")
    btn = twin.device("MAIN.Btn")
    assert type(cyl).__name__ == "Cylinder" and type(drv).__name__ == "Drive"

    cyl.extend()
    twin.wait_until(lambda: cyl.extended, timeout=3)
    assert not cyl.retracted
    assert twin.read("MAIN.Cyl.Control") == 0b10

    cyl.retract()
    twin.wait_until(lambda: cyl.retracted, timeout=3)
    assert twin.read("MAIN.Cyl.Control") == 0b01

    drv.target = 123.5
    twin.wait_until(lambda: abs(drv.value - 123.5) < 1e-6, timeout=3)
    raw = [r for r in received if struct.unpack_from("<f", r, 2)[0] == 123.5]
    assert raw, "控制映像裡沒有看到 123.5"

    assert sen.value is True and bool(sen)
    assert btn.pressed is True
    assert twin.find("*Drv*") == ["MAIN.Drv.Control", "MAIN.Drv.Status", "MAIN.Drv.ControlData", "MAIN.Drv.StatusData"]

    try:
        twin.write("MAIN.Sen.Status", 1)
        raise AssertionError("狀態變數應該不能寫")
    except ValueError:
        pass

    twin.stop_all()
    twin.wait_until(lambda: received and received[-1] == bytes(6), timeout=3)
    print("ALL PASS：manifest、氣缸位元、浮點目標值、感測器、按鈕、唯讀保護、stop_all")


if __name__ == "__main__":
    main()
