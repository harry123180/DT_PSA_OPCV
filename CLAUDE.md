# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

@_workflow/CLAUDE.md

---

## Branch `webgl-python`: the web twin and Python Link

Everything above is the upstream project (virtual commissioning over TwinCAT/ADS). This branch adds
a separate layer that runs the twin **in a browser** and lets **Python act as the PLC**, served at
<https://dt.qianpro.shop>. It touches none of the knowledge-base machinery above; the two layers
meet only in the Unity project.

**Python replaces the simulation unit, not the control PLC.** `PythonLinkClient` is an OC `Client`
that stands in for `TcAdsClient`: Python reads/writes each device's `Control`/`Status` directly —
the same layer the MIL scene drives — with no fieldbus or terminals in between. Protocol is in the
header comment of `Unity/Assets/PythonLink/PythonLinkClient.cs` (manifest JSON, then binary
`0x01`+status / `0x02`+control frames, little-endian).

| Path | Role |
|---|---|
| `Unity/Assets/PythonLink/` | Runtime: `PythonLinkClient`, WebSocket transport (`Plugins/WebGL/PythonLinkWS.jslib`), HUD, part highlight/inspect, `ParallelRobot6` (closed-chain kinematics) |
| `Unity/Assets/PythonLink/Editor/PythonLinkBuild.cs` | **Line**: copies `VC_Demo_1_Beckhoff_1.unity` → `VC_Demo_1_Python.unity`, swaps the client, builds WebGL to `Build/WebGL` |
| `Unity/Assets/PythonLink/Editor/TwinBuild.cs` | **CAD machines**: builds an OC scene from a `twin.json` (schema in its header comment) and builds WebGL |
| `python/dtlink*.py` | The student-facing library (`Twin`, `Cylinder`, `Drive`, `read_reg`/`write_reg`…). `dtlink.py` is the local WebSocket server; `dtlink_web.py` is the same API inside Pyodide |
| `web/` | Page: `app.js`, `bridge.js` (Unity ↔ worker over `SharedArrayBuffer`), `py-worker.mjs` (Pyodide), `registers.*` (process-image page), `gallery/` (machine list) |
| `web/variants/<variant>/` | Per-machine overrides: `site.js`, `devices_zh.json`, optional `python/*.py` modules |
| `cad/` | STEP → grouped STL → FBX → `twin.json` pipeline; read `cad/README.md` (also the `cad-to-twin` skill) |
| `deploy/` | nginx container on `qianproserver` behind Cloudflare Tunnel |

Two connection modes, same Python API: default `page:` (Pyodide in a Web Worker on the same page,
via `bridge.js`) or `?ws=ws://127.0.0.1:8765` (local `python python/dtlink_demo.py`).

### Commands

```bash
# Unity WebGL builds (batch mode; output lands in Build/, which is gitignored)
Unity.exe -batchmode -quit -buildTarget WebGL -projectPath Unity -executeMethod PythonLink.Editor.PythonLinkBuild.BuildWebGL
Unity.exe -batchmode -quit -buildTarget WebGL -projectPath Unity \
  -executeMethod PythonLink.Editor.TwinBuild.BuildFromCommandLine -twin Assets/<Name>/twin.json -out ../Build/<Variant>/WebGL

# Assemble a site (one machine, or the whole dt.qianpro.shop) — needs a Pyodide distribution dir
python web/assemble_site.py <out> <pyodide-dir> [--variant gantry|xyz|robot]
python web/assemble_all.py  <out> <pyodide-dir>

# Deploy the whole site to qianproserver
bash deploy/deploy.sh <pyodide-dir>

# Tests
python python/test_dtlink_protocol.py          # protocol unit test against a fake twin, no browser
python python/e2e_<name>.py <site-dir|URL>     # Playwright end-to-end per page (xyz, robot, gantry, registers, gallery, web_editor, inspect…)
```

### Things that bite

- **Variant name → build dir is `capitalize()`d**: `--variant xyz` reads `Build/Xyz/WebGL`; the
  line (no variant) reads `Build/WebGL`. Adding a machine = a `SITES` entry in
  `web/assemble_all.py` + a card in `web/gallery/index.html` + a thumbnail (`python/make_thumbs.py`).
- **The site needs cross-origin isolation** (`deploy/headers.conf`: COOP/COEP) or
  `SharedArrayBuffer` — and therefore the in-page Python — does not exist. A plain static server
  will load the 3D but not run Python.
- **`__BUILD__` placeholders** in `index.html`/`registers.html`/`app.js`/`site.js` are replaced at
  assembly with a timestamp, because Cloudflare rewrites JS cache lifetimes. Keep new assets behind
  `?v=__BUILD__`.
- **Static batching is off for WebGL** on purpose (part picking/highlight needs readable per-part
  meshes). Don't re-enable it in `ApplyPlayerSettings`.
- **Background tabs pause the simulation**, which is why the register table opens as a separate
  window rather than a tab.
- **CAD sources and generated FBX/scenes are gitignored** (third-party, unclear licence):
  `cad/*.STEP`, `Unity/Assets/<Machine>/Models/*.fbx`, `Unity/Assets/<Machine>/VC_*.unity`.
  Regenerate them with the `cad/` pipeline; only `twin.json` and code are tracked.
- **`web/devices_zh.json` is generated** from the line's `*_Context.json` by
  `web/build_device_info.py` (LLM translation on the internal network) and committed; the site
  build does not call an LLM.
- Overtravel is enforced only in Python (`move_to()`, `hexapod.move()`) — the simulation has no
  limit switches or forces. Lead/speed figures not in the CAD are assumptions (10 mm/rev).
