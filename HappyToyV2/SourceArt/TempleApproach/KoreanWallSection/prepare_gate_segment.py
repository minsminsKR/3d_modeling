"""Reproduce the gate and masonry surround of the previously generated Korean wall.

Run from Blender 4.2.23 with --background --python-exit-code 1 --python this_file.
The original imported FBX, generation source and material remain unchanged.
"""
from pathlib import Path
import hashlib
import bpy
import bmesh
from mathutils import Matrix

root = Path(__file__).resolve().parents[3]
source = root / 'Assets/Resources/AlleyKoreanWall/geometry.fbx'
expected = '682638bc6d7ca5715ca3fa3796fe86d86722ab99db0afcee72f7dfe78dab691b'
actual = hashlib.sha256(source.read_bytes()).hexdigest()
if actual != expected:
    raise RuntimeError('Original wall FBX does not match the reviewed input: ' + actual)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source))
objects = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
if len(objects) != 1:
    raise RuntimeError('Unexpected original wall mesh count')
obj = objects[0]
bpy.context.view_layer.update()
obj.data.transform(obj.matrix_world)
obj.parent = None
obj.matrix_world = Matrix.Identity(4)
bm = bmesh.new(); bm.from_mesh(obj.data)
for x, clear_inner in [(.05, True), (1.12, False)]:
    bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
        dist=1e-6, plane_co=(x, 0, 0), plane_no=(1, 0, 0),
        clear_inner=clear_inner, clear_outer=not clear_inner)
bmesh.ops.triangulate(bm, faces=list(bm.faces))
for vertex in bm.verts:
    vertex.co.x -= .585
    vertex.co.z += .00723
bm.to_mesh(obj.data); bm.free(); obj.data.update()
if not (30000 < len(obj.data.polygons) < 40000) or len(obj.data.uv_layers) != 1:
    raise RuntimeError('Unexpected section mesh/UV structure')
obj.name = 'Korean gate and masonry section'
for other in list(bpy.context.scene.objects):
    if other != obj:
        bpy.data.objects.remove(other, do_unlink=True)
bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.file.pack_all()
bpy.context.preferences.filepaths.save_version = 0
editable = Path(__file__).resolve().parent / 'GateSectionEditable.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(editable))
target = root / 'Assets/Resources/AlleyKoreanWall/gate-segment.fbx'
bpy.ops.export_scene.fbx(filepath=str(target), use_selection=True,
    add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y',
    path_mode='AUTO', embed_textures=False)
print('GATE_SECTION', len(obj.data.vertices), len(obj.data.polygons), 'UV', len(obj.data.uv_layers), 'SHA256',
      hashlib.sha256(target.read_bytes()).hexdigest(), flush=True)

