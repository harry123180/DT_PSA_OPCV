"""Blender：把群組 STL（mm）組成一個 FBX 給 Unity。
- 單位換成公尺並 apply
- 原點：螺桿放在螺桿軸心、滑座放在滑塊中心，其他放在所屬模組的螺桿軸心（方便 Unity 端當作掛點）
- CAD（SolidWorks）是 Y 朝上：匯入時轉成 Blender 的 Z 朝上（x, y, z）→（x, -z, y），再用預設軸向匯出，Unity 讀進來就是 Y 朝上"""
import bpy, json, os, sys, mathutils
argv = sys.argv[sys.argv.index('--') + 1:]
src, out_fbx, out_png = argv
meta = json.load(open(os.path.join(src, 'meta.json')))
bpy.ops.wm.read_factory_settings(use_empty=True)
mods = {m['name']: m for m in meta['modules']}
def material(name, rgb, metallic, roughness):
    # FBX 帶出去的是 Principled BSDF 的底色；diffuse_color 只影響 Blender 視窗
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    m.diffuse_color = (*rgb, 1)
    return m
mat_body = material('Aluminium', (0.72, 0.74, 0.77), 0.3, 0.45)
mat_motor = material('Motor', (0.12, 0.13, 0.15), 0.2, 0.5)
mat_screw = material('Steel', (0.45, 0.47, 0.5), 0.6, 0.35)
for f in sorted(os.listdir(src)):
    if not f.endswith('.stl'): continue
    name = f[:-4]
    bpy.ops.wm.stl_import(filepath=os.path.join(src, f), forward_axis='NEGATIVE_Z', up_axis='Y')
    ob = bpy.context.selected_objects[0]
    ob.name = ob.data.name = name
    ob.scale = (0.001, 0.001, 0.001)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    m = mods[name.split('_')[0]]
    kind = name.split('_', 1)[1]
    pivot = m['carriage_center'] if kind == 'Carriage' else m['screw_center']
    bpy.context.scene.cursor.location = mathutils.Vector((pivot[0], -pivot[2], pivot[1])) * 0.001
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.shade_auto_smooth(angle=0.6)
    ob.data.materials.append(mat_motor if kind == 'Motor' else mat_screw if kind == 'Screw' else mat_body)
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    dims = [round(v, 3) for v in ob.dimensions]
    print(f"OBJ {name}: tris={tris} size_m={dims} origin={[round(v, 4) for v in ob.location]}")
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=False, object_types={'MESH'},
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True,
                         mesh_smooth_type='FACE')
print('FBX', out_fbx, os.path.getsize(out_fbx))
# 預覽圖：從斜上方看，Y 是上
scene = bpy.context.scene
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); scene.collection.objects.link(cam); scene.camera = cam
cam.location = (1.4, -1.3, 0.9)
direction = mathutils.Vector((0.36, -0.06, 0.33)) - cam.location
cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); scene.collection.objects.link(sun)
sun.rotation_euler = (0.9, 0.3, 0.8)
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.color_type = 'MATERIAL'
scene.render.resolution_x, scene.render.resolution_y = 1000, 800
scene.render.filepath = out_png
bpy.ops.render.render(write_still=True)
