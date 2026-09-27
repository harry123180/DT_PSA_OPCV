#!/usr/bin/env bash
# 組整站並部署到 qianproserver:~/services/dt-twin（nginx 容器 dt-twin-web，127.0.0.1:8150，cloudflared → dt.qianpro.shop）
#   bash deploy/deploy.sh <pyodide 目錄>
# 前提：各機台的 Unity 建置已在 Build/<Variant>/WebGL（產線在 Build/WebGL）。
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PYODIDE="$1"
WORK="$(mktemp -d)"
python "$ROOT/web/assemble_all.py" "$WORK/html" "$PYODIDE"
cp "$ROOT/deploy/nginx.conf" "$ROOT/deploy/headers.conf" "$ROOT/deploy/docker-compose.yml" "$WORK/"
tar czf "$WORK/dt-twin.tgz" -C "$WORK" html nginx.conf headers.conf docker-compose.yml
scp -q "$WORK/dt-twin.tgz" qianproserver:~/services/dt-twin.tgz
ssh qianproserver "cd ~/services/dt-twin && rm -rf html && tar xzf ../dt-twin.tgz && docker compose up -d --force-recreate 2>&1 | tail -1 && cat html/version.txt"
rm -rf "$WORK"
