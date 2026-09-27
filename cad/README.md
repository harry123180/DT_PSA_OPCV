# CAD → 數位孿生

把廠商給的 STEP 組立檔，變成網頁上可以用 Python／PLC 控制的數位孿生（Unity WebGL＋Open Commissioning）。

| 機台 | CAD | 機構 | 網址 |
|---|---|---|---|
| 雙軸直線模組 | linear-module-3 `Configuration 1.STEP`（170 實體） | 串接：Z 軸整支掛在 X 軸滑座上 | <https://dt.qianpro.shop/gantry/> |
| 六軸並聯機器人 | 6-dof-parallel-robot-1（199 實體） | 閉鏈：6 支滑座經 210 mm 連桿推動平台（6-PSS） | <https://dt.qianpro.shop/robot/> |
| 三軸龍門 | linear-actuator-28 `Configuration 5.STEP`（208 實體） | 串接＋同步：兩支 Y 同步、X 橫梁架在兩個 Y 滑座上、Z 在 X 滑座上 | <https://dt.qianpro.shop/xyz/> |

CAD 原檔與轉出的 FBX 不放進 repo（第三方來源，授權不明），依下面流程重新產生。

## 流程

```bash
python cad/dump.py <組立.STEP> dump.json                                  # 每個實體的體積、包圍盒、圓柱面軸線、球心
python cad/screw_modules.py dump.json out/ --name <Model> --root FG_<Name>  # 滾珠螺桿模組機台：通用分群 → groups.json、twin.json
#   其他機構：python cad/<機台>/classify_*.py dump.json out/（例：cad/parallel_robot/）
python cad/export_groups.py <組立.STEP> out/groups.json out/stl --tol 0.2  # 每群一個 STL
blender --background --python cad/to_fbx.py -- out/stl out/<Model>.fbx out/preview.png   # Y 朝上、公尺、原點、材質
cp out/<Model>.fbx Unity/Assets/<Name>/Models/ ; cp out/twin.json Unity/Assets/<Name>/
Unity.exe -batchmode -quit -buildTarget WebGL -projectPath Unity \
  -executeMethod PythonLink.Editor.TwinBuild.BuildFromCommandLine -twin Assets/<Name>/twin.json -out ../Build/<Variant>/WebGL
python web/assemble_site.py <站台目錄> <pyodide 目錄> --variant <variant>
python python/e2e_<variant>.py <站台目錄>
```

- 雙軸模組、三軸龍門：`cad/screw_modules.py`（自動找模組、掛載、同步軸、行程）
- 並聯機器人：`cad/parallel_robot/classify_robot.py`；Python 逆向運動學的幾何用 `cad/parallel_robot/make_geometry.py` 從 twin.json 產生

## 分工

| 檔案 | 通用？ | 做什麼 |
|---|---|---|
| `cad/dump.py`、`export_groups.py`、`to_fbx.py` | 通用 | 讀 STEP、依 groups.json 輸出網格、轉 FBX |
| `cad/screw_modules.py` | 滾珠螺桿模組機台通用 | 找模組、分群（接觸擴散找滑座零件）、掛載與同步軸、行程；最後檢查滑座零件都在兩端座之間 |
| `cad/<機台>/classify_*.py` | 其他機構自己寫 | 只靠幾何辨識零件（名稱都是 `BaseBodyN`），分群、量行程與關節座標 |
| `Unity/Assets/PythonLink/Editor/TwinBuild.cs` | 通用 | 讀 twin.json 建 OC 場景：裝置 → Axis → 模型，串接用 parent，自動偵測 CAD→Unity 軸向 |
| `Unity/Assets/PythonLink/ParallelRobot6.cs` | 閉鏈範本 | 每幀正向運動學（牛頓法，double）擺平台與連桿，姿態寫進 SensorAnalog |
| `web/variants/<variant>/` | 每台自己寫 | site.js（標題、範例）、devices_zh.json（中文說明、行程）、python/（例如逆向運動學） |

twin.json 欄位見 `TwinBuild.cs` 開頭的註解。

## 限制

- 分群規則要依機構寫；會動的零件在 CAD 裡必須是獨立實體
- 模擬沒有力、沒有極限開關：超行程只在 Python 端擋（`move_to()`、`hexapod.move()`）
- 螺桿導程、伺服速度這類 CAD 裡沒有的規格是假設值（導程 10 mm/轉）
