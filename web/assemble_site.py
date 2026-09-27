"""把網站組進一個目錄：Unity 建置＋網頁（編輯器、橋接器）＋Pyodide＋Python 函式庫。

    python web/assemble_site.py <輸出目錄> <pyodide 目錄> [--variant gantry]

--variant：別的機台（web/variants/<名稱>/：site.js、devices_zh.json 覆蓋預設，Unity 建置用 Build/<名稱>/WebGL）。

輸出目錄的內容就是 dt.qianpro.shop 的根目錄。
"""
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
args = [a for a in sys.argv[1:] if not a.startswith("--")]
variant = sys.argv[sys.argv.index("--variant") + 1] if "--variant" in sys.argv else ""
if variant:
    args.remove(variant)
out = Path(args[0]).resolve()
pyodide = Path(args[1]).resolve()
variant_dir = ROOT / "web" / "variants" / variant if variant else None

if out.exists():
    shutil.rmtree(out)
out.mkdir(parents=True)

build = ROOT / "Build" / variant.capitalize() / "WebGL" if variant else ROOT / "Build" / "WebGL"
for name in ("Build", "StreamingAssets", "TemplateData"):
    if (build / name).exists():
        shutil.copytree(build / name, out / name)

for f in (ROOT / "web").iterdir():
    if f.name in ("assemble_site.py", "build_device_info.py", "variants") or f.name.startswith("_"):
        continue
    (shutil.copytree if f.is_dir() else shutil.copy2)(f, out / f.name)

if variant_dir:
    for f in variant_dir.iterdir():
        if f.is_file():
            shutil.copy2(f, out / f.name)

keep = {"pyodide.mjs", "pyodide.js", "pyodide.asm.mjs", "pyodide.asm.wasm", "python_stdlib.zip",
        "pyodide-lock.json", "package.json"}
(out / "pyodide").mkdir()
for f in pyodide.iterdir():
    if f.name in keep:
        shutil.copy2(f, out / "pyodide" / f.name)

(out / "python").mkdir()
for f in ("dtlink.py", "dtlink_web.py", "dtlink_demo.py", "dtlink_devices.py", "README.md"):
    shutil.copy2(ROOT / "python" / f, out / "python" / f)
shutil.copy2(out / "devices_zh.json", out / "python" / "devices_zh.json")  # 本機 dtlink.py 讀中文名稱用
# 站點自帶的 Python 模組（variants/<名稱>/python/*.py）：放進 python/，清單寫進 modules.txt 讓網頁 worker 載入
if variant_dir and (variant_dir / "python").is_dir():
    mods = sorted(f.name for f in (variant_dir / "python").glob("*.py"))
    for name in mods:
        shutil.copy2(variant_dir / "python" / name, out / "python" / name)
    (out / "python" / "modules.txt").write_text(chr(10).join(mods) + chr(10), encoding="utf-8")

# 版本號：Cloudflare 會把 js 的快取時間改寫成數小時，檔名不變就會拿到舊版；每個網址都帶 ?v=版本
import time as _t
build = _t.strftime("%Y%m%d%H%M%S")
for name in ("index.html", "registers.html", "app.js", "site.js"):
    f = out / name
    f.write_text(f.read_text(encoding="utf-8").replace("__BUILD__", build), encoding="utf-8")
(out / "version.txt").write_text(build + chr(10), encoding="utf-8")

total = sum(p.stat().st_size for p in out.rglob("*") if p.is_file())
print(f"組好 {out}（{total / 1e6:.1f} MB，版本 {build}）")
