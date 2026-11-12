"""Repair the measured ground-floor window on the retained 125k-face house.

Run with Blender 4.2.23 in background from any working directory. The original
generated GLB and the prior house FBX remain untouched. Runtime PBR is supplied
by the existing Unity house material.
"""
from pathlib import Path
import hashlib
import bpy

root = Path(__file__).resolve().parent
expected = {
    'CompactHouse.fbx': 'f3129568e98569ad5291c47689763b217d0707e42ad2e3db13064c799a2c1416',
    'albedo.png': '92a027e5ee70e483081fdbf628e86e507862db33b9b2a775dfe3e35766288ae1',
}
for name, digest in expected.items():
    if hashlib.sha256((root / name).read_bytes()).hexdigest() != digest:
        raise RuntimeError('House input changed; remeasure the ground window')

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root / 'CompactHouse.fbx'))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
if len(meshes) != 1 or not 120000 <= len(meshes[0].data.polygons) <= 130000:
    raise RuntimeError('The reviewed house mesh is not present')
source = meshes[0]
verts = [v.co for v in source.data.vertices if -.485 < v.co.x < -.44
         and -.26 < v.co.y < -.09 and -.28 < v.co.z < -.13]
if len(verts) < 30:
    raise RuntimeError('The measured ground window region no longer matches')

# Reference the retained original atlas in the editable copy. The final Unity
# renderer uses the original house PBR material, including metallic/roughness.
atlas = bpy.data.images.load(str(root / 'albedo.png'), check_existing=True)
atlas.pack()
for mat in source.data.materials:
    if not mat.use_nodes: continue
    shader = next((node for node in mat.node_tree.nodes if node.type == 'BSDF_PRINCIPLED'), None)
    if not shader: continue
    image = mat.node_tree.nodes.new('ShaderNodeTexImage')
    image.image = atlas
    mat.node_tree.links.new(image.outputs['Color'], shader.inputs['Base Color'])

# The camera-measured near window contains intersecting folded surfaces and
# has broken atlas fragments. Place one plane in its interior, leaving the
# existing outside frame intact. Sample a clean region of the same glass atlas.
points = [(-.4735, -.235, -.272), (-.4735, -.110, -.272),
          (-.4735, -.110, -.145), (-.4735, -.235, -.145)]
mesh = bpy.data.meshes.new('Ground-floor window repaired surface')
mesh.from_pydata(points, [], [(0, 3, 2, 1)])
mesh.update()
uv = mesh.uv_layers.new(name='Original glass atlas')
coordinates = [(.594, .456), (.610, .456), (.610, .477), (.594, .477)]
for loop in mesh.loops:
    uv.data[loop.index].uv = coordinates[loop.vertex_index]
if source.data.materials: mesh.materials.append(source.data.materials[0])
window = bpy.data.objects.new('Ground-floor window repaired surface', mesh)
bpy.context.scene.collection.objects.link(window)
window.matrix_world = source.matrix_world.copy()

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(root / 'CompactHouseGroundWindowEditable.blend'))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(root / 'CompactHouseGroundWindow.fbx'),
    use_selection=True, add_leaf_bones=False, bake_anim=False,
    axis_forward='-Z', axis_up='Y', path_mode='AUTO', embed_textures=False)
print('GROUND_WINDOW_REPAIR', len(source.data.polygons), len(mesh.polygons), flush=True)
