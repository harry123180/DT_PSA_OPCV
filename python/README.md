# 用 Python 控制網頁版產線孿生

網頁版：<https://dt.qianpro.shop>（瀏覽器直接開，不用安裝 Unity）

網頁裡的孿生會連到**你自己電腦**上的 Python。你的 Python 程式就是這條產線的 PLC：
寫「控制」讓氣缸伸縮、輸送帶轉動、燈亮；讀「狀態」知道氣缸到位沒、感測器有沒有偵測到托盤。

## 三步驟開始

```bash
pip install websockets
python dtlink_demo.py          # 開本機伺服器 ws://127.0.0.1:8765，等網頁連上
```

再用 Chrome 開 <https://dt.qianpro.shop>。右下角顯示 **Python connected** 就接上了。

> Chrome 第一次可能會跳出「允許這個網站存取區域網路上的裝置」，請按允許——
> 那是網頁要連到你電腦上的 Python（127.0.0.1），資料不會離開你的電腦。

## 寫自己的控制程式

```python
from dtlink import Twin, print_devices

twin = Twin()                 # 開伺服器
twin.wait_connected()         # 等網頁連上

print_devices(twin, twin.devices(type="FB_Cylinder"))   # 看有哪些氣缸

cyl = twin.devices("Stopper", type="FB_Cylinder")[0]
cyl.extend()                                       # 伸出
twin.wait_until(lambda: cyl.extended, timeout=5)   # 等到位（逾時會丟 TimeoutError）
cyl.retract()
```

| 類型 | Python 物件 | 控制 | 狀態 |
|---|---|---|---|
| `FB_Cylinder` 氣缸 | `Cylinder` | `extend()` `retract()` `release()` | `extended` `retracted` |
| `FB_SensorBinary` 感測器 | `Sensor` | — | `value` |
| `FB_Drive` 驅動 | `Drive` | `forward()` `backward()` `stop()`、`target = 速度或位置` | `value` `active` |
| `FB_Button` 按鈕 | `Button` | `light(True)` 按鈕燈 | `pressed` |
| `FB_Lamp` 燈 | `Lamp` | `on()` `off()` | — |
| `FB_Lock` 安全門鎖 | `Lock` | `lock()` `unlock()` | `closed` `locked` |
| `FB_Switch` 選擇開關 | `Switch` | — | `position` |

其他類型可以直接讀寫變數：`twin.read("MAIN....Status")`、`twin.write("MAIN....Control", 1)`、
`twin.find("*Stopper*")` 用萬用字元找變數名稱。

## 常見問題

- **右下角一直是 Python not connected**：Python 有沒有在跑？埠號被占用時可以改用
  `Twin(port=9000)`，網頁網址加上 `?ws=ws://127.0.0.1:9000`。
- **網頁重新整理後**：孿生會自動重連，Python 會把目前的控制輸出重送一次，機台接著跑。
- **想讓全部停下來**：`twin.stop_all()` 把所有控制輸出歸零。

## 這跟 PLC 版有什麼不同

原專案的 PLC 版（TwinCAT／Siemens）中間還有一層「模擬單元」與模擬的現場匯流排，PLC 程式看到的是端子。
網頁版把那一層拿掉，Python 直接讀寫每個裝置的 Control／Status——跟原專案 MIL 模式驅動裝置是同一層。
適合學控制邏輯與產線流程；要驗證真的 PLC 程式，請用原專案的 Windows 版接 TwinCAT。

協定細節：`Unity/Assets/PythonLink/PythonLinkClient.cs` 開頭的註解。
