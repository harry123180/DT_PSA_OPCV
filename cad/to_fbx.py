"""Blender：把群組 STL（mm）組成一個給 Unity 的 FBX。

    blender --background --python to_fbx.py -- <STL 目錄> <輸出.fbx> <預覽.png> [--up Y|Z] [--max-tris 40000]

- 單位 mm → m 並 apply
- 軸向：--up 是 CAD 的朝上軸（SolidWorks 預設 Y，Creo／Inventor 常是 Z）。一律轉成 Blender 的 Z 朝上，
  再用預設軸向匯出；Unity 讀進來就是 Y 朝上
- 原點：pivots.json 指定的點（CAD 座標 mm）；沒指定的放在包圍盒中心
- 材質依名稱：含 Motor → 深色；含 Screw／Rod → 鋼色；其他 → 鋁色。FBX 帶的是 Principled BSDF 底色
  STL 目錄裡有 colors.json（{"網格名": [r, g, b]}）時，列在裡面的網格改用指定顏色
- --max-tris：單一物件超過就用 Decimate（collapse）減到這個數（預設 40000）
- 印出每個物件的三角面數、尺寸（公尺）、原點，並渲一張預覽圖：跑得過不代表長得對，要看圖
"""
import json
import os
import sys

import bpy
import mathutils

argv = sys.argv[sys.argv.index("--") + 1:]
src, out_fbx, out_png = argv[:3]
up = argv[argv.index("--up") + 1] if "--up" in argv else "Y"
max_tris = int(argv[argv.index("--max-tris") + 1]) if "--max-tris" in argv else 40000
pivots = json.load(open(os.path.join(src, "pivots.json"))) if os.path.exists(os.path.join(src, "pivots.json")) else {}
colors = json.load(open(os.path.join(src, "colors.json"))) if os.path.exists(os.path.join(src, "colors.json")) else {}

bpy.ops.wm.read_factory_settings(use_empty=True)


def cad_to_blender(p):
    """CAD 座標（mm）→ Blender 座標（m），與 STL 匯入的軸向轉換一致"""
    x, y, z = p
    return mathutils.Vector((x, -z, y) if up == "Y" else (x, y, z)) * 0.001


def material(name, rgb, metallic, roughness):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    m.diffuse_color = (*rgb, 1)
    return m


mats = dict(body=material("Aluminium", (0.72, 0.74, 0.77), 0.3, 0.45),
            motor=material("Motor", (0.12, 0.13, 0.15), 0.2, 0.5),
            steel=material("Steel", (0.45, 0.47, 0.5), 0.6, 0.35))
lo = mathutils.Vector((1e9, 1e9, 1e9)); hi = -lo
for f in sorted(os.listdir(src)):
    if not f.endswith(".stl"):
        continue
    name = f[:-4]
    if up == "Y":
        bpy.ops.wm.stl_import(filepath=os.path.join(src, f), forward_axis="NEGATIVE_Z", up_axis="Y")
    else:
        bpy.ops.wm.stl_import(filepath=os.path.join(src, f))
    ob = bpy.context.selected_objects[0]
    ob.name = ob.data.name = name
    ob.scale = (0.001, 0.001, 0.001)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if name in pivots:
        bpy.context.scene.cursor.location = cad_to_blender(pivots[name])
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    else:
        bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
    tris0 = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    if tris0 > max_tris:
        mod = ob.modifiers.new("decimate", "DECIMATE")
        mod.ratio = max_tris / tris0
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.shade_auto_smooth(angle=0.6)
    kind = "motor" if "Motor" in name else "steel" if ("Screw" in name or "Rod" in name) else "body"
    if name in colors:
        rgb = tuple(colors[name])
        key = "rgb_%.2f_%.2f_%.2f" % rgb
        if key not in mats:
            mats[key] = material(key, rgb, 0.3, 0.5)
        kind = key
    ob.data.materials.append(mats[kind])
    for v in ob.bound_box:
        w = ob.matrix_world @ mathutils.Vector(v)
        lo = mathutils.Vector(map(min, lo, w)); hi = mathutils.Vector(map(max, hi, w))
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    print(f"OBJ {name}: tris={tris} size_m={[round(v, 3) for v in ob.dimensions]} origin={[round(v, 4) for v in ob.location]}")

bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=False, object_types={"MESH"}, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True, mesh_smooth_type="FACE")
print("FBX", out_fbx, os.path.getsize(out_fbx), "bounds_m", [round(v, 3) for v in lo], [round(v, 3) for v in hi])

scene = bpy.context.scene
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scene.collection.objects.link(cam); scene.camera = cam
mid, ext = (lo + hi) / 2, (hi - lo).length
cam.location = mid + mathutils.Vector((1.1, -1.3, 0.8)).normalized() * ext * 1.6
cam.rotation_euler = (mid - cam.location).to_track_quat("-Z", "Y").to_euler()
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); scene.collection.objects.link(sun)
sun.rotation_euler = (0.9, 0.3, 0.8)
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.color_type = "MATERIAL"
scene.render.resolution_x, scene.render.resolution_y = 1000, 800
scene.render.filepath = os.path.abspath(out_png)
bpy.ops.render.render(write_still=True)
