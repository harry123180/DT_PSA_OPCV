# LinuxCNC 控制網頁上的加工中心孿生

真的 CNC 控制器（LinuxCNC）驅動網頁上的立式加工中心孿生 <https://dt.qianpro.shop/vmc/>：
LinuxCNC 做 G-code 解譯、路徑規劃、加減速和 M6 換刀程式，孿生扮演機台，把感測器回授給 LinuxCNC。
這就是虛擬試運轉：控制程式不改，只是機台從實體換成孿生。

```
 G-code ─▶ LinuxCNC（toolchange.ngc、運動控制）
              │ HAL：x/y/z、主軸轉速、刀庫位置、換刀臂／鬆刀／鎖銷電磁閥
              ▼
        dtvmc.py（HAL 元件＋WebSocket 伺服器 :8765）◀──▶ 瀏覽器裡的孿生（?ws=ws://127.0.0.1:8765）
              ▲
              │ HAL：換刀臂進／出、已鬆刀／已夾刀、已上鎖（toolchange.ngc 的 M66 在等這些）
              │      孿生警報 → E-stop；解除 E-stop（F1）→ 清除孿生警報
```

注意：LinuxCNC 換刀前的 `M19` 主軸定位會讓 HAL 裡的主軸慢慢轉，定位中不把轉速送給孿生（孿生不模擬主軸角度，否則會誤判成「換刀臂不在原位時主軸旋轉」）。

機構與換刀程式來自 LinuxCNC 的範例 `configs/sim/axis/vismach/VMC_toolchange`；原本由 `sim_vmc.hal` 在 HAL 裡模擬的機台
換成 `twin_vmc.hal`。刀庫馬達＋index／pulse 編碼器仍在 HAL 裡（毫秒級脈波，網路取樣不到），位置鏡射給孿生。

## 安裝（Debian 12 bookworm／Ubuntu＋LinuxCNC 2.9）

```bash
sudo apt install linuxcnc-uspace python3-websockets      # Debian 12 官方套件庫就有
bash linuxcnc/vmc_twin/setup.sh                           # 建 ~/linuxcnc/configs/vmc_twin
linuxcnc ~/linuxcnc/configs/vmc_twin/vmc_twin.ini
```

Windows 用 WSL2（Debian）或 Docker 都可以：WSL2 的 localhost 會轉給 Windows，瀏覽器直接連 `ws://127.0.0.1:8765`。
沒有畫面的環境用 `DT_DISPLAY=linuxcncrsh bash setup.sh`，再用 `run_program.py` 操作。

Docker（沒有畫面，repo 根目錄執行）：

```bash
docker build -t dt-linuxcnc -f linuxcnc/vmc_twin/Dockerfile .        # 慢的話加 --build-arg MIRROR=ftp.tw.debian.org
docker run --rm -it -p 8765:8765 --cap-add=IPC_OWNER --cap-add=IPC_LOCK --cap-add=SYS_NICE --name lcnc dt-linuxcnc
# 瀏覽器開 https://dt.qianpro.shop/vmc/?ws=ws://127.0.0.1:8765，然後：
docker exec -it lcnc su cnc -c "cd ~/linuxcnc/configs/vmc_twin && python3 run_program.py demo.ngc"
```

`--cap-add=IPC_OWNER` 不能省：`rtapi_app` 是 setuid root，要打開一般使用者建立的 HAL 共享記憶體，Docker 預設把這個權限拿掉了。

## 使用

1. 啟動 LinuxCNC，終端機會看到 `[dtvmc]` 的訊息
2. 瀏覽器開 <https://dt.qianpro.shop/vmc/?ws=ws://127.0.0.1:8765>，右下角變成 **Python connected**
   - 孿生一連上，dtvmc 會先把主軸上的刀卸回刀庫（LinuxCNC 開機時主軸是空的）
   - 孿生沒連上之前，LinuxCNC 解除不了 E-stop
3. 解除 E-stop（F1）、開機（F2）、回原點（Home All）
4. 開 `demo.ngc`（面銑、口袋、啄鑽、圓槽，4 次換刀）→ 執行

孿生撞刀、扯刀、沒轉就切削……都會讓 LinuxCNC 急停；按 F1 解除 E-stop 時順便清除孿生的警報。

## 座標與刀具

- G54：X0 Y0＝素材中心，Z0＝素材上表面（`vmc_twin.var` 裡 Z 偏移 -459.7）
- 刀具表 `vmc_twin.tbl`：T1～T10 在刀位 1～10，Z＝刀長（從主軸鼻端量到刀尖）。換刀後要下 `G43 Hn`
- 同一支程式在網頁的 Python 編輯器裡用 `VMC.run_gcode()` 也能跑（網頁版自動補刀長，G43 會被忽略）

## 檔案

| 檔案 | 做什麼 |
|---|---|
| `twin_vmc.hal` | 取代 `sim_vmc.hal`：電磁閥命令送孿生、極限開關讀孿生、E-stop 鏈接上孿生警報 |
| `dtvmc.py` | HAL 使用者空間元件 `dtvmc`＋dtlink WebSocket 伺服器（環境變數 `DT_HOST`、`DT_PORT`） |
| `vmc_twin.tbl`、`vmc_twin.var` | 刀具表、G54 偏移 |
| `demo.ngc` | 示範零件（跟網頁的 G-code 範例同一支） |
| `setup.sh` | 從系統裝好的 LinuxCNC 範例複製設定、換上上面這些檔案（2.9 與 2.10 的元件名稱差異自動處理） |
| `run_program.py` | 不開 GUI：等孿生連上 → 解除 E-stop → 開機 → 回原點 → 執行 G-code，回報 LinuxCNC 與孿生的刀號、警報 |
| `Dockerfile` | Debian 12＋LinuxCNC 2.9＋這套設定，沒有畫面 |

整合測試（Windows 這端開瀏覽器、容器裡跑 LinuxCNC）：`python python/e2e_linuxcnc.py <加工中心站台目錄>`。
