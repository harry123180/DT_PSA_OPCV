# LinuxCNC 2.9（Debian 12 官方套件）＋網頁孿生橋接，沒有畫面（DISPLAY = linuxcncrsh）。
#
#   docker build -t dt-linuxcnc -f linuxcnc/vmc_twin/Dockerfile .          # 在 repo 根目錄
#   docker run --rm -it -p 8765:8765 --cap-add=IPC_OWNER --cap-add=IPC_LOCK --cap-add=SYS_NICE --name lcnc dt-linuxcnc
#   瀏覽器開 https://dt.qianpro.shop/vmc/?ws=ws://127.0.0.1:8765
#   docker exec -it lcnc su cnc -c "cd ~/linuxcnc/configs/vmc_twin && python3 run_program.py demo.ngc"
#
# --cap-add=IPC_OWNER：rtapi_app 是 setuid root，要能打開一般使用者建立的 HAL 共享記憶體（Docker 預設拿掉這個權限）。
FROM debian:bookworm
ARG MIRROR=deb.debian.org
RUN sed -i "s#deb.debian.org#${MIRROR}#g" /etc/apt/sources.list.d/debian.sources \
 && apt-get update \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends linuxcnc-uspace python3-websockets procps \
 && rm -rf /var/lib/apt/lists/*
RUN useradd -m cnc
COPY linuxcnc/vmc_twin /repo/linuxcnc/vmc_twin
COPY python/dtlink.py /repo/python/dtlink.py
COPY web/shared/cnc.py /repo/web/shared/cnc.py
RUN chown -R cnc /repo && su cnc -c "DT_DISPLAY=linuxcncrsh bash /repo/linuxcnc/vmc_twin/setup.sh" \
 && cp /repo/linuxcnc/vmc_twin/run_program.py /home/cnc/linuxcnc/configs/vmc_twin/ && chown -R cnc /home/cnc/linuxcnc
EXPOSE 8765
CMD ["su", "cnc", "-c", "cd ~/linuxcnc/configs/vmc_twin && linuxcnc vmc_twin.ini"]
