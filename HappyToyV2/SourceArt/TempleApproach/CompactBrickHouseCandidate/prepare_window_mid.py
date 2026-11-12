"""Derive the static mid-range LOD from the already prepared 125k-face FBX.

Run with Blender 4.2.23: blender -b --threads 2 --python-exit-code 1
--python prepare_window_mid.py. Original editable source stays untouched.
"""
from pathlib import Path
import hashlib
import bpy

root = Path(__file__).resolve().parent
expected = {
    'model.glb': '21837403eff1a02510620e5ae211aad9786a55c00ab63de9bce2e939fe0cc6a2',
    'albedo.png': '92a027e5ee70e483081fdbf628e86e507862db33b9b2a775dfe3e35766288ae1',
}
for name, digest in expected.items():
    if hashlib.sha256((root / name).read_bytes()).hexdigest() != digest:
        raise RuntimeError('House source changed; review the measured window surface')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root / 'CompactHouse.fbx'))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
faces = sum(len(obj.data.polygons) for obj in meshes)
if not meshes or not faces:
    raise RuntimeError('Source house has no geometry')
ratio = min(1.0, 100000.0 / faces)
for obj in meshes:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    if ratio < 1.0:
        modifier = obj.modifiers.new('Mid-range face budget', 'DECIMATE')
        modifier.ratio = ratio
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
print('MID_COMPACT_HOUSE_FACES', faces, sum(len(obj.data.polygons) for obj in meshes))
# The source's outside window comprises overlapping, irregular triangles.
# A narrow plane in front of that measured window provides a stable surface,
# using the same atlas UVs rather than covering the wall or window frame.
from mathutils import Vector
window_samples = []
for obj in meshes:
    mesh = obj.data
    mesh.calc_loop_triangles()
    if not mesh.uv_layers.active:
        raise RuntimeError('House atlas is missing')
    atlas = mesh.uv_layers.active.data
    for triangle in mesh.loop_triangles:
        uv_centre = sum((atlas[loop].uv for loop in triangle.loops), Vector((0, 0))) / 3
        if 587 / 1600 <= uv_centre.x <= 667 / 1600 and 1 - 292 / 1600 <= uv_centre.y <= 1 - 233 / 1600:
            window_samples.extend(obj.matrix_world @ mesh.vertices[index].co for index in triangle.vertices)
if len(window_samples) < 60 or any(not (-.10 < point.x < .17 and -.43 < point.y < -.39 and .54 < point.z < .74)
                                   for point in window_samples):
    raise RuntimeError('Measured window no longer matches this house mesh')
window_mesh = bpy.data.meshes.new('Measured window surface')
x0, x1 = -.0684, .1341
z0, z1 = .5635, .7122
y = -.4165
points = [(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)]
window_mesh.from_pydata(points, [], [(0, 1, 2, 3)])
window_mesh.update()
uv = window_mesh.uv_layers.new(name='Original window atlas')
for polygon in window_mesh.polygons:
    for loop in polygon.loop_indices:
        x, _, z = points[window_mesh.loops[loop].vertex_index]
        uv.data[loop].uv = (.24604242*x + .00108995*z + .38308082,
                            .00111561*x - .24638702*z + .99301628)
window = bpy.data.objects.new('Stable window surface', window_mesh)
bpy.context.scene.collection.objects.link(window)
# The original window has a narrow central frame. Preserve its legibility
# over the stable surface using a dark, non-emissive atlas sample.
rail_mesh = bpy.data.meshes.new('Window central rail')
centre = (x0 + x1) * .5
rail_points = [(centre - .003, y - .001, z0), (centre + .003, y - .001, z0),
               (centre + .003, y - .001, z1), (centre - .003, y - .001, z1)]
rail_mesh.from_pydata(rail_points, [], [(0, 1, 2, 3)]); rail_mesh.update()
rail_uv = rail_mesh.uv_layers.new(name='Dark frame atlas')
for loop in rail_mesh.loops:
    rail_uv.data[loop.index].uv = (.5, .9)
rail = bpy.data.objects.new('Window central rail', rail_mesh)
bpy.context.scene.collection.objects.link(rail)
bpy.context.preferences.filepaths.save_version = 0
for image in bpy.data.images:
    if image.source == 'FILE' and not image.packed_file:
        candidate = Path(bpy.path.abspath(image.filepath))
        if candidate.is_file():
            image.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(root / 'CompactHouseWindowEditable.blend'))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(root / 'CompactHouseMidWindow.fbx'),
    use_selection=True, add_leaf_bones=False, bake_anim=False,
    axis_forward='-Z', axis_up='Y', path_mode='AUTO', embed_textures=False)
