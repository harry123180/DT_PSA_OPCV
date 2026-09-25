// 暫存器表：透過 BroadcastChannel("dt-registers") 跟產線分頁（bridge.js 的暫存器中心）溝通。
(function () {
  "use strict";
  const $ = (id) => document.getElementById(id);
  const ch = new BroadcastChannel("dt-registers");

  // 位元意義：取自 Open Commissioning 元件原始碼（與 dtlink.py 的裝置包裝一致）
  const BITS = {
    "FB_Cylinder.Control": ["縮回", "伸出"],
    "FB_Cylinder.Status": ["縮回端", "伸出端"],
    "FB_SensorBinary.Status": ["偵測"],
    "FB_Drive.Control": ["正轉", "反轉"],
    "FB_Drive.Status": [null, null, null, null, null, null, "動作中", "bit7"],
    "FB_Button.Control": ["按鈕燈"],
    "FB_Button.Status": ["按下"],
    "FB_Lamp.Control": ["亮"],
    "FB_Lock.Control": ["上鎖"],
    "FB_Lock.Status": ["門已關", "已上鎖"],
    "FB_Switch.Status": ["段 1", "段 2", "段 3", "段 4", "段 5", "段 6", "段 7", "段 8"],
    "FB_DeviceByte.ControlData": ["b0", "b1", "b2", "b3", "b4", "b5", "b6", "b7"],
    "FB_DeviceByte.StatusData": ["b0", "b1", "b2", "b3", "b4", "b5", "b6", "b7"],
    "FB_Lock.ControlData": ["b0", "b1", "b2", "b3", "b4", "b5", "b6", "b7"],
    "FB_Lock.StatusData": ["b0", "b1", "b2", "b3", "b4", "b5", "b6", "b7"],
  };
  const DEFAULT_BITS = ["b0", "b1", "b2", "b3", "b4", "b5", "b6", "b7"];

  let manifest = null, writable = false, mode = "page";
  let ctrl = new Uint8Array(0), stat = new Uint8Array(0);
  let lastImage = 0;
  let mainHidden = false;
  const rows = [];          // { v, area, el: {val, bits[]}, dev }
  const devEls = [];        // { el, dev, rows }

  function address(v) {
    const area = v.dir === "control" ? "Q" : "I";
    let w = "B";
    if (v.dtype === "f32" || v.dtype === "u32" || v.dtype === "i32") w = "D";
    else if (v.size === 2) w = "W";
    const a = `%${area}${w}${v.offset}`;
    return v.count > 1 ? `${a}…${w}${v.offset + v.size - 4}` : a;
  }

  function readValue(v) {
    const buf = v.dir === "control" ? ctrl : stat;
    if (buf.length < v.offset + v.size) return null;
    const dv = new DataView(buf.buffer, buf.byteOffset + v.offset, v.size);
    switch (v.dtype) {
      case "u8": return dv.getUint8(0);
      case "i8": return dv.getInt8(0);
      case "u16": return dv.getUint16(0, true);
      case "i16": return dv.getInt16(0, true);
      case "u32": return dv.getUint32(0, true);
      case "i32": return dv.getInt32(0, true);
      case "f32": return v.count > 1 ? Array.from({ length: v.count }, (_, i) => dv.getFloat32(i * 4, true)) : dv.getFloat32(0, true);
      case "f64": return dv.getFloat64(0, true);
      default: return dv.getUint8(0);
    }
  }

  function fmt(v, x) {
    if (x === null) return "—";
    if (Array.isArray(x)) return x.slice(0, 3).map((n) => n.toFixed(1)).join(", ") + (x.length > 3 ? "…" : "");
    if (v.dtype.startsWith("f")) return x.toFixed(3);
    if (v.size === 1) return `${x}  0x${x.toString(16).padStart(2, "0").toUpperCase()}`;
    return String(x);
  }

  function write(offset, bytes) {
    if (!writable) { alert(mode === "local" ? "本機 Python 模式下暫存器表只能看：控制輸出由你電腦上的 Python 決定。" : "產線還沒連上。"); return; }
    ch.postMessage({ t: "write", offset, bytes: Array.from(bytes) });
  }

  function build() {
    const list = $("list");
    list.textContent = "";
    rows.length = 0; devEls.length = 0;
    const types = new Set();
    for (const dev of manifest.devices) {
      const vars = manifest.variables.filter((v) => v.name.startsWith(dev.path + "."));
      if (!vars.length) continue;
      types.add(dev.type);
      const el = document.createElement("section");
      el.className = "dev";
      el.innerHTML = `<div class="head"><span class="path"></span><span class="type"></span></div><table><tbody></tbody></table>`;
      el.querySelector(".path").textContent = dev.path;
      el.querySelector(".type").textContent = `${dev.type}${dev.component ? " · " + dev.component : ""}`;
      const tbody = el.querySelector("tbody");
      const devRows = [];
      // 輸出在前、輸入在後
      vars.sort((a, b) => (a.dir === b.dir ? a.offset - b.offset : a.dir === "control" ? -1 : 1));
      for (const v of vars) {
        const area = v.dir === "control" ? "Q" : "I";
        const suffix = v.name.slice(dev.path.length + 1);
        const tr = document.createElement("tr");
        tr.innerHTML = `<td class="addr ${area.toLowerCase()}"></td><td class="off"></td><td class="var"></td><td class="typ"></td><td class="val"></td><td class="ctl"></td>`;
        tr.children[0].textContent = address(v);
        tr.children[1].textContent = "0x" + v.offset.toString(16).padStart(4, "0").toUpperCase();
        tr.children[2].textContent = suffix;
        tr.children[3].textContent = v.dtype + (v.count > 1 ? `×${v.count}` : "");
        const row = { v, area, dev, val: tr.children[4], bits: [], input: null, last: undefined };
        const ctl = tr.children[5];
        if (v.size === 1 && v.count === 1) {
          const labels = BITS[`${dev.type}.${suffix}`] || DEFAULT_BITS;
          const box = document.createElement("div");
          box.className = "bits";
          labels.forEach((label, bit) => {
            if (!label) return;
            const b = document.createElement("span");
            b.className = "bit " + area.toLowerCase();
            b.innerHTML = `<span class="led"></span><span></span>`;
            b.lastChild.textContent = `${bit}:${label}`;
            b.title = `${address(v).replace(/%(.)B/, "%$1X")}.${bit}`;
            if (area === "Q") b.onclick = () => {
              const cur = readValue(v) || 0;
              write(v.offset, [cur ^ (1 << bit)]);
            };
            box.appendChild(b);
            row.bits.push({ bit, el: b });
          });
          ctl.appendChild(box);
        } else if (area === "Q" && v.dtype === "f32" && v.count === 1) {
          const inp = document.createElement("input");
          inp.type = "number"; inp.step = "any"; inp.className = "num"; inp.placeholder = "輸入後 Enter";
          inp.onkeydown = (e) => {
            if (e.key !== "Enter") return;
            const b = new Uint8Array(4);
            new DataView(b.buffer).setFloat32(0, parseFloat(inp.value) || 0, true);
            write(v.offset, b);
          };
          ctl.appendChild(inp);
          row.input = inp;
        }
        tbody.appendChild(tr);
        rows.push(row); devRows.push(row);
      }
      list.appendChild(el);
      devEls.push({ el, dev, rows: devRows });
    }
    const sel = $("type");
    sel.innerHTML = '<option value="">全部類型</option>';
    [...types].sort().forEach((t) => { const o = document.createElement("option"); o.value = o.textContent = t; sel.appendChild(o); });
    applyFilter();
    refresh(true);
  }

  function refresh(initial) {
    for (const r of rows) {
      const x = readValue(r.v);
      const key = Array.isArray(x) ? x.join(",") : x;
      if (key === r.last) continue;
      r.val.textContent = fmt(r.v, x);
      if (!initial && r.last !== undefined) { r.val.classList.remove("flash"); void r.val.offsetWidth; r.val.classList.add("flash"); }
      r.last = key;
      for (const b of r.bits) b.el.classList.toggle("on", !!(x & (1 << b.bit)));
    }
    if ($("changed").checked) applyFilter();
  }

  function applyFilter() {
    const q = $("q").value.trim().toLowerCase();
    const type = $("type").value, area = $("area").value, nz = $("changed").checked;
    for (const d of devEls) {
      let any = false;
      for (const r of d.rows) {
        const tr = r.val.parentElement;
        let show = (!area || r.area === area) && (!nz || (r.last !== 0 && r.last !== undefined && r.last !== "0"));
        if (q) show = show && (d.dev.path.toLowerCase().includes(q) || address(r.v).toLowerCase().includes(q) || r.v.name.toLowerCase().includes(q));
        tr.style.display = show ? "" : "none";
        any = any || show;
      }
      d.el.style.display = any && (!type || d.dev.type === type) ? "" : "none";
    }
  }

  function setConn() {
    const c = $("conn");
    const alive = manifest && performance.now() - lastImage < 2500;
    if (!alive) { c.textContent = "產線分頁未開啟"; c.className = "conn"; }
    else if (mainHidden) { c.textContent = "產線分頁在背景：瀏覽器暫停了 3D 模擬，請把兩個視窗並排"; c.className = "conn ro"; }
    else if (!writable) { c.textContent = mode === "local" ? "已連線（本機 Python 模式，只能看）" : "已連線（唯讀）"; c.className = "conn ro"; }
    else { c.textContent = "已連線，可手動切換"; c.className = "conn ok"; }
    document.body.classList.toggle("ro", !writable);
  }

  ch.onmessage = (e) => {
    const m = e.data || {};
    if (m.t === "manifest") {
      const same = manifest && manifest.controlSize === JSON.parse(m.text).controlSize;
      mode = m.mode; writable = !!m.writable;
      if (!same) { manifest = JSON.parse(m.text); build(); }
    } else if (m.t === "image") {
      ctrl = m.ctrl; stat = m.stat; writable = !!m.writable; lastImage = performance.now(); mainHidden = !!m.hidden;
      if (manifest) refresh(false);
    } else if (m.t === "readonly") {
      writable = false;
    } else if (m.t === "bye") {
      lastImage = 0;
    }
    setConn();
  };

  ["q", "type", "area", "changed"].forEach((id) => $(id).addEventListener("input", applyFilter));
  $("zero").onclick = () => { if (writable) ch.postMessage({ t: "zero" }); else write(0, []); };
  ch.postMessage({ t: "hello" });
  // 主動輪詢：產線分頁的計時器在背景會被瀏覽器放慢，但訊息處理不會，所以由這邊問、那邊馬上回
  setInterval(() => ch.postMessage({ t: manifest ? "poll" : "hello" }), 300);
  setInterval(setConn, 500);
})();
