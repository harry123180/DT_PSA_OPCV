"""從原專案的 Context.json（Unity 產生的官方裝置說明）做出中文裝置資料 web/devices_zh.json。

    python web/build_device_info.py            # 只翻譯還沒翻過的
    python web/build_device_info.py --force    # 全部重翻

每個裝置：短名稱（label）、所屬群組、以及 Function／Signal／Role／Caution 的中文。
翻譯用內網 Gemma4（OpenAI 相容 API）；結果存檔進 repo，網站建置時不需要 LLM。
"""
import json
import re
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Unity/Assets/StreamingAssets/VC_Demo_1_Beckhoff_1_Context.json"
OUT = ROOT / "web/devices_zh.json"
API = "http://100.87.1.14:8003/v1/chat/completions"
MODEL = "gemma-4-31B-IT-nvfp4"
FIELDS = ("Function", "Signal", "Role", "Caution")

GLOSSARY = """術語（一律照這個翻）：pallet＝托盤；workpiece／payload／part＝工件；stopper＝擋料器；
index unit／index station＝分度站；lift＝升降機；conveyor＝輸送帶；accumulation＝堆積；cylinder＝氣缸；
double solenoid＝雙電磁閥；single solenoid＝單電磁閥；limit／end position＝到位；EXTENDED／extended＝伸出；
retracted＝縮回；sensor＝感測器；gripper＝夾爪；gate＝閘門；bunker＝料倉；cap＝瓶蓋；slot＝槽位；
laser mark＝雷射打標；reader＝讀碼器；functional group（FG）＝功能群組；station＝站；handover＝交接；
safety door＝安全門；interlock＝互鎖；emergency stop／E-Stop＝緊急停止；signal tower＝三色燈；
operator panel＝操作面板；NIO＝不良品；axis＝軸；twin＝孿生；PLC、Unity、EtherCAT 保留英文。"""

GROUP_ZH = {
    "FG_System": "系統（安全與面板）",
    "FG_Transport": "輸送系統",
    "FG_01": "第 1 站：讀碼與雷射打標",
    "FG_02": "第 2 站：光學檢測",
    "FG_03": "第 3 站：翻轉移載",
    "FG_04": "第 4 站：壓入與到位檢查",
    "FG_05": "第 5 站：上蓋",
}


def tidy(label: str) -> str:
    """統一短名稱：模型有時寫「功能群組 03」「系統功能群組」。"""
    label = re.sub(r"功能群組\s*0?(\d)", lambda m: f"第 {m.group(1)} 站", label)
    return label.replace("主輸送帶功能群組", "輸送系統").replace("系統功能群組", "系統")


def collect():
    d = json.loads(SRC.read_text(encoding="utf-8"))
    items = {}

    def walk(n, ancestors):
        e = {x["key"]: x["value"] for x in n.get("entries", [])}
        path = e.get("oc.plcPath")
        if path:
            group = next((a for a in ancestors if a.startswith("FG_")), n["name"] if n["name"].startswith("FG_") else "")
            items[path] = {
                "name": n["name"],
                "scenePath": n.get("scenePath", ""),
                "type": e.get("oc.deviceType", ""),
                "group": group,
                "module": next((a for a in reversed(ancestors) if re.match(r"^(Lift|Index|Stopper|Transport)\d+$", a)), ""),
                "en": {k: e[k] for k in FIELDS if e.get(k)},
            }
        for c in n.get("children", []):
            walk(c, ancestors + [n["name"]])

    walk(d["root"], [])
    return items


def ask(batch: dict) -> dict:
    prompt = f"""把下面工業自動化產線裝置的英文說明翻成繁體中文（台灣用語），並替每個裝置取一個短名稱。
{GLOSSARY}

規則：
- label：給學生看的短名稱，12 個字以內，格式像「分度站 3 · 擋料氣缸」「第 1 站 · 夾爪」「升降機 1 · 輸送帶」「擋料站 2 · 到位感測器」。
  依裝置路徑判斷：Y_ 開頭通常是氣缸，B_ 是感測器，M_ 是馬達，SS_ 是安全開關；IndexNN＝分度站 N、StopperNN＝擋料站 N、LiftNN＝升降機 N、TransportNN＝主輸送帶 N。
- 其他欄位（function、signal、role、caution）忠實翻譯，不要自己加內容；沒有的欄位就不要輸出。
- 只輸出 JSON，格式：{{"<路徑>": {{"label": "...", "function": "...", "signal": "...", "role": "...", "caution": "..."}}}}

裝置：
{json.dumps({p: {"type": v["type"], **v["en"]} for p, v in batch.items()}, ensure_ascii=False, indent=1)}"""
    body = {"model": MODEL, "temperature": 0.1, "max_tokens": 6000,
            "messages": [{"role": "user", "content": prompt}]}
    req = urllib.request.Request(API, data=json.dumps(body).encode("utf-8"), headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=300) as r:
        text = json.loads(r.read())["choices"][0]["message"]["content"]
    m = re.search(r"\{.*\}", text, re.S)
    return json.loads(m.group(0))


def main():
    force = "--force" in sys.argv
    items = collect()
    old = json.loads(OUT.read_text(encoding="utf-8")) if OUT.exists() and not force else {"devices": {}}
    done = old.get("devices", {})
    todo = [p for p in items if p not in done or not done[p].get("label")]
    print(f"{len(items)} 個裝置，要翻 {len(todo)} 個")
    for i in range(0, len(todo), 8):
        batch = {p: items[p] for p in todo[i:i + 8]}
        for attempt in range(3):
            try:
                zh = ask(batch)
                break
            except Exception as exc:  # noqa: BLE001
                print("  重試：", exc)
        else:
            raise SystemExit("翻譯失敗")
        for p, v in batch.items():
            done[p] = {**{k: v[k] for k in ("name", "type", "group", "module", "scenePath")},
                       "groupLabel": GROUP_ZH.get(v["group"], v["group"]), "en": v["en"], **zh.get(p, {})}
            if done[p].get("label"):  # 模型有時寫「功能群組 03」，統一成「第 3 站」
                done[p]["label"] = tidy(done[p]["label"])
        print(f"  {min(i + 8, len(todo))}/{len(todo)}", flush=True)
    OUT.write_text(json.dumps({"source": SRC.name, "groups": GROUP_ZH, "devices": done}, ensure_ascii=False, indent=1),
                   encoding="utf-8")
    print("寫入", OUT)


if __name__ == "__main__":
    main()
