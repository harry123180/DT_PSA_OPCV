# CAD → 數位孿生：直線模組範例

把廠商給的 STEP 組立檔，變成網頁上可以用 Python 控制的數位孿生。
範例是 `linear-module-3`（SolidWorks 匯出，AP214）的 `Configuration 1.STEP`：兩支相同的滾珠螺桿直線模組組成 T 形，
水平軸（X）的滑座上立著垂直軸（Z）。網址：<https://dt.qianpro.shop/gantry/>

STEP 檔很大（40～130 MB），不放進 repo。

## 流程

```bash
S=工作目錄
python cad/dump.py        "$S/Configuration 1.STEP" $S/c1.json          # 每個實體的包圍盒、體積（約 1 分鐘）
python cad/classify.py    $S/c1.json $S/c1_groups.json                  # 自動分群
python cad/export_groups.py "$S/Configuration 1.STEP" $S/c1_groups.json $S/c1_out   # 每群一個 STL＋meta.json（行程）
blender --background --python cad/to_fbx.py -- $S/c1_out $S/c1_out/LinearGantry.fbx $S/c1_out/preview.png
cp $S/c1_out/LinearGantry.fbx       Unity/Assets/LinearGantry/Models/
cp $S/c1_out/meta.json              Unity/Assets/LinearGantry/Models/LinearGantry.motion.json
Unity.exe -batchmode -quit -buildTarget WebGL -projectPath Unity -executeMethod PythonLink.Editor.GantryBuild.BuildWebGL
python web/assemble_site.py <站台目錄> <pyodide 目錄> --variant gantry
python python/e2e_gantry.py <站台目錄>
```

Blender 路徑：`C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`。CadQuery 用本機 python。

## 每一步做什麼

| 步驟 | 做法 | 為什麼 |
|---|---|---|
| 分群 `classify.py` | STEP 裡零件名稱都是 `BaseBody1..N`，沒有意義，只能靠幾何：一支 605 mm 螺桿＋兩支 640 mm 導桿定義一個模組；四個大塊依體積認出滑座、馬達端座、惰輪端座、馬達；其他零件看落在哪個模組、軸向是否在滑座範圍內 | 會動的零件（滑座、軸承、螺帽與它們的螺絲）一定要跟固定件分開 |
| 掛載關係 | 模組 B 的惰輪端座貼著模組 A 的滑座 → B 整支掛在 A 的滑座上 | A 動時 B 要跟著走 |
| 行程 `export_groups.py` | 滑座群組在兩端座之間的空間：本例 ±205.9 mm，也就是 0～411.8 mm | 超出會撞端座 |
| 轉 FBX `to_fbx.py` | mm → m；SolidWorks 是 Y 朝上，匯入時轉成 Blender Z 朝上，再用預設軸向匯出；原點放在螺桿軸心／滑座中心 | 進 Unity 後 Y 朝上、尺寸正確、旋轉件繞自己的軸 |
| 建場景 `GantryBuild.cs` | 依 OC 慣例「邏輯元件 → Axis → 模型」：`M_AxisX`、`M_AxisZ` 是位置伺服（DrivePosition，FB_Drive）；各自帶一個直線 Axis（滑座）與一個旋轉 Axis（螺桿）；Z 模組放在 X 滑座的 Axis 底下 | Axis 的正方向依馬達實際位置判斷（FBX 會把 X 左右翻轉） |
| 網頁 | `web/variants/gantry/`：site.js（標題、範例）、devices_zh.json（中文名稱、行程 `range`） | Python 的 `move_to()` 依 `range` 擋超出行程 |

## 換成別的機台時要改的地方

- `classify.py` 的體積常數與「一個模組＝一支螺桿＋兩支導桿」只適用這種模組；別的機構（氣缸、轉盤、皮帶）要改辨識規則，
  或直接手寫一份「哪些實體屬於哪一群」的對照表。
- 螺桿導程（`GantryBuild.ScrewLead`，預設 10 mm/轉）只影響螺桿轉的視覺速度，拿到規格再改。
- 模擬沒有真實的力與極限開關：超出行程不會被擋住（Python 端會擋），要做原點復歸、極限感測器得另外加 SensorBinary。
