"""Remove the malformed rooftop mass from the retained generated house.

Run with Blender 4.2.23:
  blender -b --threads 2 --python-exit-code 1 --python clean_roof.py
The input is the untouched prepared `TwoStoryHouse.fbx`. The measured world
coordinates and source hash intentionally bind this localized edit to that
mesh. Existing roof floor, tank, shed, walls and full source are retained.
"""
from pathlib import Path
import hashlib
import bpy
import bmesh

root = Path(__file__).resolve().parent
source = root / 'TwoStoryHouse.fbx'
expected = '8ec50188b26ad897a605a7d1eb586596a1cfdcfdf4e5d80b1decb324654ac148'
if hashlib.sha256(source.read_bytes()).hexdigest() != expected:
    raise RuntimeError('Input house changed; remeasure the roof before cleaning')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source))
bpy.context.view_layer.update()
meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
if len(meshes) != 1: raise RuntimeError('Expected one prepared house mesh')
obj = meshes[0]
bm = bmesh.new(); bm.from_mesh(obj.data)
selected = []
mass_count = 0
fragment_count = 0
for face in bm.faces:
    p = obj.matrix_world @ face.calc_center_median()
    mass = -.38 < p.x < .02 and .05 < p.y < .46 and p.z > .752
    fragment = -.20 < p.x < -.035 and -.27 < p.y < -.155 and p.z > .754
    if mass or fragment:
        selected.append(face)
        mass_count += int(mass)
        fragment_count += int(fragment)
if not 22000 <= mass_count <= 22500 or not 400 <= fragment_count <= 500:
    raise RuntimeError('Unexpected roof artifact selection; aborting')
print('ROOF_ARTIFACTS_REMOVED', mass_count, fragment_count, 'OF', len(bm.faces))
bmesh.ops.delete(bm, geom=selected, context='FACES')
bm.to_mesh(obj.data); bm.free(); obj.data.update()
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(root / 'TwoStoryHouseCleanEditable.blend'))
def export(name):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(root / name), use_selection=True,
        add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y',
        path_mode='COPY', embed_textures=True)
export('TwoStoryHouseClean.fbx')
faces = sum(len(item.data.polygons) for item in meshes)
ratio = min(1.0, 30000.0 / faces)
for item in meshes:
    bpy.context.view_layer.objects.active = item
    item.select_set(True)
    if ratio < 1.0:
        modifier = item.modifiers.new('Background face budget', 'DECIMATE')
        modifier.ratio = ratio
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    item.select_set(False)
print('ROOF_CLEAN_FAR_FACES', sum(len(item.data.polygons) for item in meshes))
export('TwoStoryHouseCleanFar.fbx')
