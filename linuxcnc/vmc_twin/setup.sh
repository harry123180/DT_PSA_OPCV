#!/usr/bin/env bash
# 建立 LinuxCNC 設定 vmc_twin：複製 LinuxCNC 內建的 VMC_toolchange 範例（index 刀庫版），
# 把 sim_vmc.hal（HAL 裡的模擬機台＋vismach 畫面）換成 twin_vmc.hal（網頁數位孿生），刀具表換成孿生的 10 支刀。
#
#   bash linuxcnc/vmc_twin/setup.sh [目的目錄，預設 ~/linuxcnc/configs/vmc_twin]
#   linuxcnc ~/linuxcnc/configs/vmc_twin/vmc_twin.ini
#   瀏覽器開 https://dt.qianpro.shop/vmc/?ws=ws://127.0.0.1:8765
#
# 需要：linuxcnc-uspace（2.9）、python3-websockets。
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
DST="${1:-$HOME/linuxcnc/configs/vmc_twin}"

SRC="$(find /usr/share/doc/linuxcnc /usr/share/linuxcnc /usr/local/share 2>/dev/null -type d -path '*vismach/VMC_toolchange' | head -1)"
[ -n "$SRC" ] || { echo "找不到 LinuxCNC 的 VMC_toolchange 範例（裝 linuxcnc-uspace 了嗎？）"; exit 1; }
STDGLUE="$(find /usr/share/doc/linuxcnc /usr/share/linuxcnc /usr/lib/python3/dist-packages 2>/dev/null -name stdglue.py -path '*python-stdglue*' | head -1)"
[ -n "$STDGLUE" ] || { echo "找不到 remap_lib/python-stdglue/stdglue.py"; exit 1; }
NCFILES="$(dirname "$(dirname "$(dirname "$STDGLUE")")")"     # …/nc_files

mkdir -p "$DST"
cp -r "$SRC/." "$DST/"
find "$DST" -name '*.gz' -exec gunzip -f {} \;                  # Debian 會把範例壓成 .gz
cp "$HERE/twin_vmc.hal" "$HERE/dtvmc.py" "$HERE/vmc_twin.tbl" "$HERE/vmc_twin.var" "$HERE/run_program.py" "$DST/"
cp "$REPO/python/dtlink.py" "$REPO/web/shared/cnc.py" "$DST/"
[ -f "$HERE/demo.ngc" ] && cp "$HERE/demo.ngc" "$DST/"

sed -e 's/^HALFILE *= *sim_vmc.hal/HALFILE = twin_vmc.hal/' \
    -e 's/^TOOL_TABLE *=.*/TOOL_TABLE = vmc_twin.tbl/' \
    -e 's/^PARAMETER_FILE *=.*/PARAMETER_FILE = vmc_twin.var/' \
    -e 's/^MACHINE *=.*/MACHINE = VMC digital twin (dt.qianpro.shop\/vmc)/' \
    -e "s#^PROGRAM_PREFIX *=.*#PROGRAM_PREFIX = $DST#" \
    -e "s#\.\./\.\./nc_files#$NCFILES#g" \
    "$DST/vmc_index.ini" > "$DST/vmc_twin.ini"
# 型別轉換元件 2.10 改名（conv_float_s32 → conv_real_sint、conv_s32_float → conv_sint_real）
if [ -e /usr/lib/linuxcnc/modules/conv_real_sint.so ] || [ -e /usr/lib/linuxcnc/modules/conv_real_sint ]; then
  sed -i -e 's/conv_float_s32/conv_real_sint/g; s/conv-float-s32/conv-real-sint/g'          -e 's/conv_s32_float/conv_sint_real/g; s/conv-s32-float/conv-sint-real/g' "$DST/twin_vmc.hal"
fi
# 顯示介面：預設 AXIS；DT_DISPLAY=linuxcncrsh 沒有畫面（遠端／測試用）
if [ -n "${DT_DISPLAY:-}" ]; then
  sed -i "s/^DISPLAY *=.*/DISPLAY = $DT_DISPLAY/" "$DST/vmc_twin.ini"
fi
grep -q "twin_vmc.hal" "$DST/vmc_twin.ini" || { echo "vmc_twin.ini 沒換到 HAL 檔"; exit 1; }
echo "設定建好：$DST/vmc_twin.ini"
echo "  啟動：linuxcnc $DST/vmc_twin.ini"
echo "  孿生：https://dt.qianpro.shop/vmc/?ws=ws://127.0.0.1:8765"
