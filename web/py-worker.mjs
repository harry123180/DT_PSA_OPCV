// 學生的 Python 在這個 Web Worker 裡跑（Pyodide）。放在 worker 是為了：
// 學生寫 while True / time.sleep 時，3D 畫面（主執行緒）照樣順暢，而且可以中斷。
import { loadPyodide } from "./pyodide/pyodide.mjs";

let pyodide = null;
let interruptBuffer = null;
const post = (type, extra = {}) => self.postMessage({ type, ...extra });

const BOOT = `
import sys, traceback
sys.path.insert(0, "/home/pyodide")
import dtlink_web
dtlink_web.install()

def run_student(code):
    # 每次執行一個全新的命名空間；__name__ == "__main__" 讓範例的 main() 會跑
    g = {"__name__": "__main__", "__file__": "student.py"}
    try:
        exec(compile(code, "student.py", "exec"), g)
        return True
    except KeyboardInterrupt:
        print("[已停止]", file=sys.stderr)
        return False
    except SystemExit:
        return True
    except BaseException:
        # 只印學生程式那段的 traceback，不印 Pyodide 內部
        tb = traceback.format_exc().splitlines()
        keep = [l for l in tb if "_pyodide" not in l and "<exec>" not in l]
        print("\\n".join(keep), file=sys.stderr)
        return False
`;

async function init(msg) {
  self.dt = {
    hdr: new Int32Array(msg.sab, 0, 4),
    ctrl: new Uint8Array(msg.sab, 16, msg.controlSize),
    stat: new Uint8Array(msg.sab, 16 + msg.controlSize, msg.statusSize),
    manifest: msg.manifest,
  };
  if (!pyodide) {
    post("status", { text: "載入 Python（第一次約 5–10 秒）…" });
    pyodide = await loadPyodide({ indexURL: new URL("./pyodide/", import.meta.url).href });
    pyodide.setStdout({ batched: (text) => post("out", { text }) });
    pyodide.setStderr({ batched: (text) => post("err", { text }) });
    interruptBuffer = new Int32Array(new SharedArrayBuffer(4));
    pyodide.setInterruptBuffer(interruptBuffer);
    for (const f of ["dtlink.py", "dtlink_web.py", "dtlink_demo.py"]) {
      // 沿用 worker 自己網址上的 ?v=版本，Python 檔跟著網站版本走
      const r = await fetch(new URL(`./python/${f}${new URL(import.meta.url).search}`, import.meta.url));
      pyodide.FS.writeFile(`/home/pyodide/${f}`, await r.text());
    }
    pyodide.runPython(BOOT);
  }
  post("ready", { interrupt: interruptBuffer.buffer, version: pyodide.version });
}

function run(code) {
  interruptBuffer[0] = 0;
  post("running");
  const t0 = performance.now();
  let ok = false;
  try {
    ok = pyodide.globals.get("run_student")(code);
  } catch (e) {
    post("err", { text: String(e) });
  }
  post("done", { ok, seconds: (performance.now() - t0) / 1000 });
}

self.onmessage = async (e) => {
  const msg = e.data;
  try {
    if (msg.type === "init") await init(msg);
    else if (msg.type === "run") run(msg.code);
  } catch (err) {
    post("err", { text: String(err && err.stack || err) });
    post("done", { ok: false, seconds: 0 });
  }
};
