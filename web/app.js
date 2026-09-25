// 頁面：載入 Unity 孿生、管理 Python worker、編輯器與輸出。
(function () {
  "use strict";
  const B = window.DTPageBridge;
  const params = new URLSearchParams(location.search);
  const localMode = !!params.get("ws");
  const $ = (id) => document.getElementById(id);
  const STORE_KEY = "dt.editor.code";

  // ── 範例 ────────────────────────────────────────────────
  const EXAMPLES = {
    basic: `# 基本：讓一支擋料器伸出、縮回
from dtlink import Twin

twin = Twin()
stopper = twin.device("MAIN.FG_Transport.Stopper01_Y_Stopper")
print(stopper)

stopper.extend()                                      # 伸出
twin.wait_until(lambda: stopper.extended, timeout=5)  # 等到位
print("伸出到位")

twin.sleep(1)
stopper.retract()                                     # 縮回
twin.wait_until(lambda: stopper.retracted, timeout=5)
print("縮回到位")
`,
    list: `# 列出所有裝置（類型、元件、路徑）
from dtlink import Twin, print_devices
from collections import Counter

twin = Twin()
print(Counter(d.type for d in twin.devices()))
print_devices(twin)
`,
    registers: `# 暫存器讀寫：用 %Q（輸出）／%I（輸入）位址操作，跟 PLC 的過程映像一樣
# 位址對照可以開右上角「暫存器表」頁面查，那裡也能手動切換輸出
from dtlink import Twin

twin = Twin()
twin.print_regs("Stopper01_Y_Stopper")          # 印出這支擋料器的暫存器位址與目前值

row = [r for r in twin.reg_table("Stopper01_Y_Stopper") if r["name"].endswith(".Control")][0]
q = row["address"].replace("QB", "QX")          # 例如 %QB76 → %QX76（位元存取）
status = [r for r in twin.reg_table("Stopper01_Y_Stopper") if r["name"].endswith(".Status")][0]["address"].replace("IB", "IX")  # 狀態在輸入區，例如 %IX80

twin.write_reg(q + ".0", False)
twin.write_reg(q + ".1", True)                   # bit1 = 伸出
twin.wait_until(lambda: twin.read_reg(status + ".1"), timeout=5)   # 輸入 bit1 = 伸出端
print(q + ".1 → 伸出，", status + ".1 =", twin.read_reg(status + ".1"))

twin.sleep(1)
twin.write_reg(q + ".1", False)
twin.write_reg(q + ".0", True)                   # bit0 = 縮回
twin.wait_until(lambda: twin.read_reg(status + ".0"), timeout=5)
print(q + ".0 → 縮回，", status + ".0 =", twin.read_reg(status + ".0"))
`,
    sensor: `# 讀感測器：主輸送帶轉起來，監看托盤經過 Stopper01 的偵測感測器
from dtlink import Twin
import time

twin = Twin()
T = "MAIN.FG_Transport."
for name in ("Transport01_M_Conveyor", "Transport02_M_Conveyor"):
    twin.device(T + name).backward()

detect = twin.device(T + "Stopper01_B_Detect")
stopper = twin.device(T + "Stopper01_Y_Stopper")
stopper.retract()          # 擋住托盤

last = None
end = time.time() + 30
while time.time() < end:   # 監看 30 秒
    if detect.value != last:
        last = detect.value
        print(f"{time.strftime('%H:%M:%S')}  偵測到托盤：{last}")
        if last:           # 托盤到了：等 1 秒再放行
            time.sleep(1)
            stopper.extend()
            twin.wait_until(lambda: not detect.value, timeout=10)
            stopper.retract()
    time.sleep(0.05)
print("結束")
`,
  };

  // ── 編輯器 ──────────────────────────────────────────────
  const editor = CodeMirror.fromTextArea($("editor"), {
    mode: "python", lineNumbers: true, indentUnit: 4, tabSize: 4, matchBrackets: true,
    extraKeys: {
      "Ctrl-Enter": () => run(), "Cmd-Enter": () => run(),
      "Ctrl-/": "toggleComment", "Cmd-/": "toggleComment",
      Tab: (cm) => cm.somethingSelected() ? cm.indentSelection("add") : cm.replaceSelection("    ", "end"),
    },
  });
  let saved = null;
  try { saved = localStorage.getItem(STORE_KEY); } catch (e) {}
  editor.setValue(saved || EXAMPLES.basic);
  editor.on("change", () => { try { localStorage.setItem(STORE_KEY, editor.getValue()); } catch (e) {} });

  // ── 輸出 ────────────────────────────────────────────────
  const out = $("console");
  function log(text, cls) {
    const span = document.createElement("span");
    if (cls) span.className = cls;
    span.textContent = text.endsWith("\n") ? text : text + "\n";
    out.appendChild(span);
    while (out.childNodes.length > 3000) out.removeChild(out.firstChild);
    out.scrollTop = out.scrollHeight;
  }
  function setStatus(text, running) {
    $("status").textContent = text;
    $("status").className = "status" + (running ? " running" : "");
  }

  // ── 模式 ────────────────────────────────────────────────
  if (localMode) {
    $("mode").textContent = "本機 Python 模式";
    $("mode").classList.add("local");
    $("mode-switch").textContent = "改用網頁編輯器";
    $("mode-switch").href = location.pathname;
  } else {
    $("mode").textContent = "網頁 Python";
    $("mode-switch").textContent = "改連本機 Python";
    $("mode-switch").href = "?ws=ws://127.0.0.1:8765";
  }

  // ── Python worker ───────────────────────────────────────
  let worker = null, workerReady = false, interruptView = null, running = false, pendingCode = null, killTimer = null;

  function startWorker() {
    workerReady = false;
    worker = new Worker("py-worker.mjs", { type: "module" });
    worker.onmessage = onWorkerMessage;
    worker.onerror = (e) => log("[Python worker 錯誤] " + (e.message || e), "err");
    if (B.manifest) sendInit();
  }

  function sendInit() {
    worker.postMessage({
      type: "init", sab: B.sab, manifest: B.manifestText,
      controlSize: B.manifest.controlSize, statusSize: B.manifest.statusSize,
    });
  }

  function onWorkerMessage(e) {
    const m = e.data;
    switch (m.type) {
      case "status": setStatus(m.text); log(m.text, "sys"); break;
      case "ready":
        workerReady = true;
        interruptView = new Int32Array(m.interrupt);
        setStatus("就緒（Python " + m.version + "）");
        if (pendingCode !== null) { const c = pendingCode; pendingCode = null; runNow(c); }
        break;
      case "out": log(m.text); break;
      case "err": log(m.text, "err"); break;
      case "running": running = true; setStatus("執行中…", true); $("run").disabled = true; break;
      case "done":
        running = false;
        clearTimeout(killTimer);
        $("run").disabled = false;
        setStatus(m.ok ? `完成（${m.seconds.toFixed(1)} 秒）` : "已結束");
        break;
    }
  }

  function run() {
    if (localMode) { log("目前是本機 Python 模式：請在自己電腦執行 python 程式，或點右上「改用網頁編輯器」。", "sys"); return; }
    if (running) return;
    const code = editor.getValue();
    log("── 執行 " + new Date().toLocaleTimeString() + " ──", "sys");
    if (!B.manifest) { log("產線還在載入，載好會自動開始執行。", "sys"); pendingCode = code; return; }
    if (!workerReady) { pendingCode = code; return; }
    runNow(code);
  }

  function runNow(code) { worker.postMessage({ type: "run", code }); }

  function stop() {
    pendingCode = null;
    if (!running) { B.stopAll(); log("所有輸出已歸零", "sys"); return; }
    if (interruptView) interruptView[0] = 2;   // 送 KeyboardInterrupt
    B.wakeSleepers();                          // 叫醒 time.sleep 裡的 Python
    killTimer = setTimeout(() => {             // 兩秒還沒停（例如卡在純運算）就強制結束
      if (!running) return;
      worker.terminate();
      running = false;
      $("run").disabled = false;
      log("[強制結束]", "err");
      B.stopAll();
      startWorker();
    }, 2000);
    setTimeout(() => B.stopAll(), 300);
  }

  $("run").onclick = run;
  $("stop").onclick = stop;
  $("clear").onclick = () => { out.textContent = ""; };
  $("download").onclick = () => {
    const blob = new Blob([editor.getValue()], { type: "text/x-python" });
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = "my_control.py";
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
  };
  $("examples").onchange = async (e) => {
    const key = e.target.value;
    e.target.value = "";
    if (!key) return;
    let code = EXAMPLES[key];
    if (key === "demo") code = await (await fetch("python/dtlink_demo.py", { cache: "no-cache" })).text();
    editor.setValue(code);
    log("已載入範例，按「執行」或 Ctrl+Enter。", "sys");
  };

  if (!localMode) {
    if (!window.crossOriginIsolated || typeof SharedArrayBuffer === "undefined") {
      log("這個瀏覽器不支援網頁 Python 需要的共享記憶體。請用最新版 Chrome、Edge 或 Firefox。", "err");
      $("run").disabled = true;
    } else {
      startWorker();
      B.onReady(() => { if (worker) sendInit(); });
      setStatus("等產線載入…");
    }
  } else {
    $("run").disabled = true;
    setStatus("本機 Python 模式");
    log("本機 Python 模式：在自己電腦執行\n  pip install websockets\n  python dtlink_demo.py\n右下角出現 Python connected 就接上了。", "sys");
  }

  // ── Unity 孿生 ──────────────────────────────────────────
  const canvas = $("unity-canvas");
  const script = document.createElement("script");
  script.src = "Build/WebGL.loader.js";
  script.onload = () => {
    createUnityInstance(canvas, {
      arguments: [],
      dataUrl: "Build/WebGL.data.unityweb",
      frameworkUrl: "Build/WebGL.framework.js.unityweb",
      codeUrl: "Build/WebGL.wasm.unityweb",
      streamingAssetsUrl: "StreamingAssets",
      companyName: "Preliy",
      productName: "DT PSA OPCV (Python Link)",
      productVersion: "0.1.0",
    }, (p) => { $("progress-bar").style.width = Math.round(p * 100) + "%"; })
      .then(() => { $("loading").style.display = "none"; })
      .catch((err) => { $("loading").textContent = "產線載入失敗：" + err; });
  };
  document.body.appendChild(script);
  // 在編輯器打字時不要讓 Unity 吃掉按鍵（WASD 等）
  canvas.addEventListener("mousedown", () => canvas.focus());
})();
