"""依 STEP 的組立樹（元件名稱）分群 → 每群一個 STL。給「零件名稱有意義」的 CAD 用，例如 Fusion 360 機台庫
匯出的機台：元件已經照運動軸拆好、取好名字（X、Y、Z、Spindle、Table…），不必再靠幾何猜。

    python assembly_groups.py <組立.STEP> --list                       # 印出組立樹：路徑、實體數、包圍盒（mm）
    python assembly_groups.py <組立.STEP> rules.json <輸出目錄> [--tol 0.3]

rules.json：{"群組名": ["路徑規則", ...], ...}
  規則是 Python 正規表示式，比對「元件路徑」（例 "VF-2/Machine/Y Axis/X Axis/Table"，大小寫不分）。
  同一個元件裡有多個實體時，路徑後面加 #序號（例 "VF-2/Static#2"），可以只挑其中一個。
  一個實體屬於第一個規則比對到它路徑的群組（依 rules.json 的順序）；"*" 群組收其他全部。
  沒被任何群組收的實體會列出來，並以非 0 結束（會動的零件漏掉比多一塊靜止件嚴重）。

輸出：<輸出目錄>/<群組>.stl（mm，原本的 CAD 座標）、groups_report.json（每群的實體數、包圍盒、來源路徑）。
接下來跟其他機台一樣：to_fbx.py → twin.json → TwinBuild。
"""
import json
import os
import re
import sys

from OCP.BRepBndLib import BRepBndLib
from OCP.BRepMesh import BRepMesh_IncrementalMesh
from OCP.Bnd import Bnd_Box
from OCP.IFSelect import IFSelect_RetDone
from OCP.STEPCAFControl import STEPCAFControl_Reader
from OCP.StlAPI import StlAPI_Writer
from OCP.TCollection import TCollection_ExtendedString
from OCP.TDF import TDF_Label, TDF_LabelSequence
from OCP.TDataStd import TDataStd_Name
from OCP.TDocStd import TDocStd_Document
from OCP.TopAbs import TopAbs_SHAPE, TopAbs_SHELL, TopAbs_SOLID
from OCP.TopExp import TopExp_Explorer
from OCP.TopLoc import TopLoc_Location
from OCP.TopoDS import TopoDS, TopoDS_Compound
from OCP.BRep import BRep_Builder
from OCP.XCAFDoc import XCAFDoc_DocumentTool, XCAFDoc_ShapeTool


def label_name(label):
    attr = TDataStd_Name()
    if label.FindAttribute(TDataStd_Name.GetID_s(), attr):
        return attr.Get().ToExtString()
    return "?"


def read(step):
    doc = TDocStd_Document(TCollection_ExtendedString("XmlOcaf"))
    reader = STEPCAFControl_Reader()
    reader.SetNameMode(True)
    if reader.ReadFile(step) != IFSelect_RetDone:
        raise SystemExit(f"讀不了 {step}")
    reader.Transfer(doc)
    return doc, XCAFDoc_DocumentTool.ShapeTool_s(doc.Main())   # doc 要留著，否則被回收後 tool 變空的


def walk(tool, label, path, loc, out):
    """展開組立樹：每個實體 → (路徑, 已套用位置的 solid)"""
    name = label_name(label)
    if XCAFDoc_ShapeTool.IsReference_s(label):
        ref = TDF_Label()
        XCAFDoc_ShapeTool.GetReferredShape_s(label, ref)
        inst_loc = loc.Multiplied(XCAFDoc_ShapeTool.GetLocation_s(label))
        # 名稱放在被引用的原型（元件）上，實例常只有編號或「名稱:1」：優先用原型名稱
        proto = label_name(ref)
        use = proto if proto not in ("?", "") else name
        walk(tool, ref, path + [use], inst_loc, out)
        return
    if XCAFDoc_ShapeTool.IsAssembly_s(label):
        comps = TDF_LabelSequence()
        XCAFDoc_ShapeTool.GetComponents_s(label, comps)
        for i in range(1, comps.Length() + 1):
            walk(tool, comps.Value(i), path, loc, out)
        return
    shape = XCAFDoc_ShapeTool.GetShape_s(label).Moved(loc)
    key = "/".join(p for p in path if p and p != "?") or name
    n = 0
    items = []
    for kind in (TopAbs_SOLID, TopAbs_SHELL):       # 沒有封閉實體時收殼面（Fusion 的曲面實體匯出成 shell）
        exp = TopExp_Explorer(shape, kind, TopAbs_SOLID if kind == TopAbs_SHELL else TopAbs_SHAPE)
        while exp.More():
            items.append(exp.Current())
            exp.Next()
    for k, sh in enumerate(items):
        out.append((key if len(items) == 1 else f"{key}#{k + 1}", sh))   # 同一元件多個實體：路徑加 #序號
        n += 1
    if n == 0 and not shape.IsNull():
        out.append((key, shape))


def solids_of(step):
    doc, tool = read(step)
    roots = TDF_LabelSequence()
    tool.GetFreeShapes(roots)
    out = []
    for i in range(1, roots.Length() + 1):
        r = roots.Value(i)
        walk(tool, r, [label_name(r)], TopLoc_Location(), out)
    return out


def bbox(shapes):
    b = Bnd_Box()
    for s in shapes:
        BRepBndLib.Add_s(s, b)
    if b.IsVoid():
        return None
    x0, y0, z0, x1, y1, z1 = b.Get()
    return [round(v, 1) for v in (x0, y0, z0, x1, y1, z1)]


def compound(shapes):
    c = TopoDS_Compound()
    bld = BRep_Builder()
    bld.MakeCompound(c)
    for s in shapes:
        bld.Add(c, s)
    return c


def main():
    step = sys.argv[1]
    items = solids_of(step)
    if "--list" in sys.argv:
        paths = {}
        for p, s in items:
            paths.setdefault(p, []).append(s)
        print(f"{len(items)} 個實體，{len(paths)} 個元件路徑")
        for p, ss in sorted(paths.items()):
            print(f"  {len(ss):4d}  {p}   bbox {bbox(ss)}")
        return
    rules = json.load(open(sys.argv[2], encoding="utf-8"))
    outdir = sys.argv[3]
    tol = float(sys.argv[sys.argv.index("--tol") + 1]) if "--tol" in sys.argv else 0.3
    os.makedirs(outdir, exist_ok=True)
    compiled = [(g, [re.compile(r, re.I) for r in rs if r != "*"], "*" in rs) for g, rs in rules.items()]
    groups, sources, left = {}, {}, []
    for p, s in items:
        for g, pats, catch_all in compiled:
            if catch_all or any(pt.search(p) for pt in pats):
                groups.setdefault(g, []).append(s)
                sources.setdefault(g, set()).add(p)
                break
        else:
            left.append(p)
    report = {}
    for g, ss in groups.items():
        c = compound(ss)
        BRepMesh_IncrementalMesh(c, tol, False, min(0.6, tol * 3), True)
        StlAPI_Writer().Write(c, os.path.join(outdir, g + ".stl"))
        report[g] = {"solids": len(ss), "bbox": bbox(ss), "paths": sorted(sources[g])}
        print(f"{g}: {len(ss)} 個實體 bbox {report[g]['bbox']}")
    json.dump(report, open(os.path.join(outdir, "groups_report.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    if left:
        print(f"未分群 {len(left)} 個實體：", sorted(set(left))[:20])
        sys.exit(1)


if __name__ == "__main__":
    main()
