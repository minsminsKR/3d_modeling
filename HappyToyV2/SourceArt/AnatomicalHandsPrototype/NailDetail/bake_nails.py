"""Conform the fitted CC0 nail surfaces and bake color/mask to the hand UV.

Run in Blender 4.2.23 background mode with ``-- <work-directory>``.
No runtime asset or existing authoring blend is modified by this script.
"""
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = Path(__file__).resolve().parent
WORK = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
bpy.ops.wm.open_mainfile(filepath=str(HERE.parent / 'hand-rig-prototype-v7.blend'))
hand = bpy.data.objects['Blender anatomical hand candidate']
rig = next(obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE')
rig.animation_data.action = None
for bone in rig.pose.bones:
    bone.matrix_basis.identity()
bpy.context.view_layer.update()
data = json.loads((WORK / 'fitted-nails.json').read_text(encoding='utf8'))
neighbors = [set() for _ in data['verts']]
for face in data['faces']:
    for i in face:
        neighbors[i].update(j for j in face if j != i)
unseen = set(range(len(neighbors)))
components = []
while unseen:
    pending = [unseen.pop()]
    component = set(pending)
    while pending:
        more = neighbors[pending.pop()] & unseen
        unseen -= more
        component.update(more)
        pending.extend(more)
    components.append(sorted(component))
if len(components) != 5:
    raise RuntimeError(f'Expected five fingernail components, got {len(components)}')
components.sort(key=lambda c: sum(data['verts'][i][0] for i in c) / len(c), reverse=True)
regions = [(.088, .136, .0035, .0043), (.0525, .210, .0045, .006),
           (.0185, .224, .0045, .006), (-.012, .215, .0045, .0055),
           (-.049, .188, .004, .005)]
for part_index, (part, (cx, cz, rx, rz)) in enumerate(zip(components, regions)):
    # The thumb surface axis differs from the bone axis. Cross-sections of the
    # dorsal fingertip determine this center and basis in the neutral hand.
    if part_index == 0:
        cx, cz, rx, rz = .0830, .1339, .0045, .0050
    xs = [data['verts'][i][0] for i in part]
    zs = [data['verts'][i][2] for i in part]
    # Orthogonalize the source thumb before sizing; normalizing x/z bounds
    # independently skews the original diagonal nail into a narrow wedge.
    if part_index == 0:
        width_samples = [.96241665 * data['verts'][i][0] - .27157724 * data['verts'][i][2] for i in part]
        length_samples = [.27157724 * data['verts'][i][0] + .96241665 * data['verts'][i][2] for i in part]
    for i in part:
        vertex = data['verts'][i]
        u = (2 * (vertex[0] - min(xs)) / (max(xs) - min(xs)) - 1) * rx
        v = (2 * (vertex[2] - min(zs)) / (max(zs) - min(zs)) - 1) * rz
        if part_index == 0:
            width = .96241665 * vertex[0] - .27157724 * vertex[2]
            length = .27157724 * vertex[0] + .96241665 * vertex[2]
            u = (2 * (width - min(width_samples)) / (max(width_samples) - min(width_samples)) - 1) * rx
            v = (2 * (length - min(length_samples)) / (max(length_samples) - min(length_samples)) - 1) * rz
            vertex[0] = cx + .727 * u + .686 * v
            vertex[2] = cz - .3511 * u + .9363 * v
        else:
            vertex[0] = cx + u
            vertex[2] = cz + v
bvh = BVHTree.FromPolygons([v.co for v in hand.data.vertices],
                           [list(face.vertices) for face in hand.data.polygons])
vertices = []
for vertex in data['verts']:
    hit, normal, _, _ = bvh.ray_cast(Vector((vertex[0], .10, vertex[2])), Vector((0, -1, 0)), .20)
    if hit is None:
        raise RuntimeError('Fitted nail vertex missed the hand surface')
    vertices.append(tuple(hit + normal * .0003))
faces, coords = [], []
for face, uv in zip(data['faces'], data['uv']):
    a, b, c = (Vector(vertices[i]) for i in face[:3])
    if (b - a).cross(c - a).y > .000000001:
        faces.append(face)
        coords.append(uv)
mesh = bpy.data.meshes.new('Fitted CC0 nail surfaces')
mesh.from_pydata(vertices, [], faces)
mesh.update()
nails = bpy.data.objects.new('Fitted CC0 nail surfaces', mesh)
bpy.context.scene.collection.objects.link(nails)
uv = mesh.uv_layers.new(name='NewSkinUV')
for face, values in zip(mesh.polygons, coords):
    for index, value in zip(face.loop_indices, values):
        uv.data[index].uv = value
material = bpy.data.materials.new('Nail bake source')
material.use_nodes = True
nodes, links = material.node_tree.nodes, material.node_tree.links
nodes.clear()
tex = nodes.new('ShaderNodeTexImage')
tex.image = bpy.data.images.load(str(HERE / 'Source' / 'Nails_01.png'))
uv_node = nodes.new('ShaderNodeUVMap')
uv_node.uv_map = 'NewSkinUV'
links.new(uv_node.outputs['UV'], tex.inputs['Vector'])
emission = nodes.new('ShaderNodeEmission')
links.new(tex.outputs['Color'], emission.inputs[0])
output = nodes.new('ShaderNodeOutputMaterial')
links.new(emission.outputs[0], output.inputs['Surface'])
mesh.materials.append(material)
hand.data.uv_layers.active = hand.data.uv_layers['NewSkinUV']
skin_nodes = hand.data.materials[0].node_tree.nodes
image = bpy.data.images.new('Nail bake target', 2048, 2048, alpha=True)
target = skin_nodes.new('ShaderNodeTexImage')
target.image = image
skin_nodes.active = target
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 1
scene.render.threads_mode = 'FIXED'
scene.render.threads = 2
scene.render.bake.margin = 2
scene.render.bake.use_clear = True
bpy.ops.object.select_all(action='DESELECT')
nails.select_set(True)
hand.select_set(True)
bpy.context.view_layer.objects.active = hand
for name in ('color', 'mask'):
    if name == 'mask':
        for link in list(emission.inputs[0].links):
            links.remove(link)
        emission.inputs[0].default_value = (1, 1, 1, 1)
    bpy.ops.object.bake(type='EMIT', use_selected_to_active=True,
                        cage_extrusion=.003, max_ray_distance=.006)
    image.filepath_raw = str(WORK / f'bake-{name}.png')
    image.file_format = 'PNG'
    image.save()
    print('NAIL_BAKE', name)
