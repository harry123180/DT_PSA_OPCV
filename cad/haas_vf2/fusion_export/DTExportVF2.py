# Fusion 360 add-in（一次性）：匯入 Autodesk 機台庫的模擬模型（.f3d），輸出元件樹（tree.json）、STEP 與每個元件的 STL。
# make_vf2.py fusion 會把這個資料夾複製到 Fusion 的 AddIns、寫好 config.json，Fusion 啟動 20 秒後自動執行，完成時寫 DONE。
# 跑完記得移除（make_vf2.py fusion --remove），否則每次開 Fusion 都會再跑一次。
import adsk.core, adsk.fusion, traceback, threading, json, os, time

HERE = os.path.dirname(os.path.abspath(__file__))
CFG = json.load(open(os.path.join(HERE, "config.json"), encoding="utf-8"))
OUT, F3D = CFG["out"], CFG["f3d"]
EVENT_ID = "DTExportVF2_Go"
handlers = []
app = adsk.core.Application.get()


def log(msg):
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, "log.txt"), "a", encoding="utf-8") as f:
        f.write(time.strftime("%H:%M:%S ") + str(msg) + "\n")


def safe(f, default=None):
    try:
        return f()
    except Exception:
        return default


def bbox(b):
    return [round(v * 10, 2) for v in (b.minPoint.x, b.minPoint.y, b.minPoint.z, b.maxPoint.x, b.maxPoint.y, b.maxPoint.z)]  # cm → mm


def body_info(b):
    return {"name": b.name, "volume_cm3": safe(lambda: round(b.volume, 3)), "bbox": safe(lambda: bbox(b.boundingBox)),
            "visible": safe(lambda: b.isVisible), "solid": safe(lambda: b.isSolid)}


def mesh_info(m):
    return {"name": m.name, "bbox": safe(lambda: bbox(m.boundingBox)), "visible": safe(lambda: m.isVisible),
            "triangles": safe(lambda: m.displayMesh.triangleCount)}


def occ_info(occ):
    comp = occ.component
    return {"name": occ.name, "path": occ.fullPathName, "component": comp.name,
            "bodies": [body_info(b) for b in comp.bRepBodies], "meshes": [mesh_info(m) for m in comp.meshBodies],
            "children": [occ_info(o) for o in occ.childOccurrences]}


def export_stl(design, geom, name, done):
    try:
        em = design.exportManager
        path = os.path.join(OUT, "stl", "".join(c if c.isalnum() or c in "-_." else "_" for c in name) + ".stl")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        o = em.createSTLExportOptions(geom, path)
        o.sendToPrintUtility = False
        o.meshRefinement = adsk.fusion.MeshRefinementSettings.MeshRefinementMedium
        done.append([name, os.path.basename(path), bool(em.execute(o))])
    except Exception as e:
        done.append([name, None, str(e)[:200]])


def work():
    log("start")
    doc = app.importManager.importToNewDocument(app.importManager.createFusionArchiveImportOptions(F3D))
    log("imported " + doc.name)
    design = adsk.fusion.Design.cast(doc.products.itemByProductType("DesignProductType"))
    root = design.rootComponent
    tree = {"root": root.name, "rootBodies": [body_info(b) for b in root.bRepBodies],
            "rootMeshes": [mesh_info(m) for m in root.meshBodies], "occurrences": [occ_info(o) for o in root.occurrences],
            "products": [p.productType for p in doc.products]}
    with open(os.path.join(OUT, "tree.json"), "w", encoding="utf-8") as f:
        json.dump(tree, f, ensure_ascii=False, indent=1)
    done = []
    for o in root.allOccurrences:
        export_stl(design, o, o.fullPathName.replace("+", "__"), done)
    with open(os.path.join(OUT, "stl_index.json"), "w", encoding="utf-8") as f:
        json.dump(done, f, ensure_ascii=False, indent=1)
    em = design.exportManager
    log("step export " + str(em.execute(em.createSTEPExportOptions(os.path.join(OUT, CFG.get("step", "model.step")), root))))
    safe(lambda: doc.close(False))
    with open(os.path.join(OUT, "DONE"), "w") as f:
        f.write("ok")
    log("done")


class GoHandler(adsk.core.CustomEventHandler):
    def notify(self, args):
        try:
            work()
        except Exception:
            log("ERROR " + traceback.format_exc())
            with open(os.path.join(OUT, "DONE"), "w") as f:
                f.write("error")


def run(context):
    try:
        log("run")
        ev = app.registerCustomEvent(EVENT_ID)
        h = GoHandler()
        ev.add(h)
        handlers.append(h)

        def later():
            time.sleep(20)
            app.fireCustomEvent(EVENT_ID, "")
        threading.Thread(target=later, daemon=True).start()
    except Exception:
        log("ERROR run " + traceback.format_exc())


def stop(context):
    safe(lambda: app.unregisterCustomEvent(EVENT_ID))
