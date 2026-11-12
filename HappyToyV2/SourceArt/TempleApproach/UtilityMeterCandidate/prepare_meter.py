"""Rebuild editable and staged reduced wall meter from the retained A100 GLB.
Blender 4.2.23: blender -b --threads 2 --python-exit-code 1 --python prepare_meter.py
"""
from pathlib import Path
import hashlib
import bpy
from mathutils import Vector
root = Path(__file__).resolve().parent
source = root / "model.glb"
if hashlib.sha256(source.read_bytes()).hexdigest() != "26f61ddb461d05969158daea11e225c9081f2fec06f244dbc90340dfa9d298d0":
    raise RuntimeError("Unexpected A100 source GLB")
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if not meshes: raise RuntimeError("Meter mesh missing")
bpy.context.view_layer.update()
corners = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
lo = Vector(tuple(min(point[i] for point in corners) for i in range(3)))
hi = Vector(tuple(max(point[i] for point in corners) for i in range(3)))
height = hi.z - lo.z
if height < .001: raise RuntimeError("Invalid meter dimensions")
holder = bpy.data.objects.new("Aged residential electricity meter", None)
bpy.context.scene.collection.objects.link(holder)
for obj in [o for o in bpy.context.scene.objects if o != holder and o.parent is None]: obj.parent = holder
holder.scale = (1 / height,) * 3
holder.location = (-((lo.x + hi.x) / 2) / height, -((lo.y + hi.y) / 2) / height, -lo.z / height)
albedo = None
for mat in bpy.data.materials:
    if not mat.use_nodes: continue
    for node in mat.node_tree.nodes:
        if node.type != "BSDF_PRINCIPLED": continue
        socket = node.inputs.get("Base Color")
        if socket and socket.is_linked:
            tex = socket.links[0].from_node
            if tex.type == "TEX_IMAGE" and tex.image and tex.image.packed_file:
                albedo = bytes(tex.image.packed_file.data)
if not albedo: raise RuntimeError("Meter original albedo missing")
(root / "albedo.png").write_bytes(albedo)
for obj in meshes:
    if not obj.data.uv_layers: raise RuntimeError("Meter UV missing")
faces = sum(len(obj.data.polygons) for obj in meshes)
bpy.ops.file.pack_all()
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(root / "MeterEditable.blend"))
for target in (250000, 80000, 45000):
    current = sum(len(obj.data.polygons) for obj in meshes)
    ratio = min(1., target / max(1, current))
    for obj in meshes:
        bpy.context.view_layer.objects.active = obj; obj.select_set(True)
        if ratio < 1:
            modifier = obj.modifiers.new("Game face budget", "DECIMATE"); modifier.ratio = ratio
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=str(root / "Meter.fbx"),use_selection=True,add_leaf_bones=False,
    bake_anim=False,axis_forward="-Z",axis_up="Y",path_mode="AUTO",embed_textures=False)
print("METER_RESULT", faces, sum(len(obj.data.polygons) for obj in meshes), "bounds",tuple(lo),tuple(hi),flush=True)
