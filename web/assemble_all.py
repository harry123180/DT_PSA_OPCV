"""組整個 dt.qianpro.shop：根目錄是機台列表頁，每台機器一個子目錄。

    python web/assemble_all.py <輸出目錄> <pyodide 目錄> [--with-private]

新增機台：在 SITES 加一行（子目錄、variant），並在 web/gallery/index.html 的 MACHINES 加一張卡片、
web/gallery/img/ 放縮圖（python/make_thumbs.py 產生）。
"""
import shutil
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SITES = [("line", ""), ("gantry", "gantry"), ("xyz", "xyz"), ("robot", "robot"), ("vmc", "vmc"), ("vf2", "vf2")]   # 子目錄、variant（空＝產線）
# 第三方模型、授權還沒確認公開散布的機台：只在 --with-private 時組進去（列表頁不放卡片，網址知道才進得去）
PRIVATE_SITES = []   # 例：還沒確認授權的第三方模型

args = [a for a in sys.argv[1:] if not a.startswith("--")]
out = Path(args[0]).resolve()
pyodide = args[1]
if "--with-private" in sys.argv:
    SITES = SITES + PRIVATE_SITES
if out.exists():
    shutil.rmtree(out)
out.mkdir(parents=True)

for sub, variant in SITES:
    cmd = [sys.executable, str(ROOT / "web" / "assemble_site.py"), str(out / sub), pyodide]
    if variant:
        cmd += ["--variant", variant]
    print(subprocess.run(cmd, check=True, capture_output=True, text=True, encoding="utf-8").stdout.strip())

build = time.strftime("%Y%m%d%H%M%S")
shutil.copytree(ROOT / "web" / "gallery" / "img", out / "img")
page = (ROOT / "web" / "gallery" / "index.html").read_text(encoding="utf-8").replace("__BUILD__", build)
(out / "index.html").write_text(page, encoding="utf-8")
(out / "version.txt").write_text(build + chr(10), encoding="utf-8")
total = sum(p.stat().st_size for p in out.rglob("*") if p.is_file())
print(f"整站 {out}（{total / 1e6:.0f} MB，列表頁版本 {build}）")
