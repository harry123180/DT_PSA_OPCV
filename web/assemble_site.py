"""把網站組進一個目錄：Unity 建置＋網頁（編輯器、橋接器）＋Pyodide＋Python 函式庫。

    python web/assemble_site.py <輸出目錄> <pyodide 目錄>

輸出目錄的內容就是 dt.qianpro.shop 的根目錄。
"""
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
out = Path(sys.argv[1]).resolve()
pyodide = Path(sys.argv[2]).resolve()

if out.exists():
    shutil.rmtree(out)
out.mkdir(parents=True)

build = ROOT / "Build" / "WebGL"
for name in ("Build", "StreamingAssets", "TemplateData"):
    if (build / name).exists():
        shutil.copytree(build / name, out / name)

for f in (ROOT / "web").iterdir():
    if f.name in ("assemble_site.py",) or f.name.startswith("_"):
        continue
    (shutil.copytree if f.is_dir() else shutil.copy2)(f, out / f.name)

keep = {"pyodide.mjs", "pyodide.js", "pyodide.asm.mjs", "pyodide.asm.wasm", "python_stdlib.zip",
        "pyodide-lock.json", "package.json"}
(out / "pyodide").mkdir()
for f in pyodide.iterdir():
    if f.name in keep:
        shutil.copy2(f, out / "pyodide" / f.name)

(out / "python").mkdir()
for f in ("dtlink.py", "dtlink_web.py", "dtlink_demo.py", "dtlink_devices.py", "README.md"):
    shutil.copy2(ROOT / "python" / f, out / "python" / f)

# 版本號：Cloudflare 會把 js 的快取時間改寫成數小時，檔名不變就會拿到舊版；每個網址都帶 ?v=版本
import time as _t
build = _t.strftime("%Y%m%d%H%M%S")
for name in ("index.html", "registers.html", "app.js"):
    f = out / name
    f.write_text(f.read_text(encoding="utf-8").replace("__BUILD__", build), encoding="utf-8")
(out / "version.txt").write_text(build + chr(10), encoding="utf-8")

total = sum(p.stat().st_size for p in out.rglob("*") if p.is_file())
print(f"組好 {out}（{total / 1e6:.1f} MB，版本 {build}）")
