"""CPU-only topology experiment; no source or game asset is written."""
from pathlib import Path
import bmesh
import bpy

source = Path(__file__).resolve().parent / 'pine' / 'original.glb'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
mesh = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
bpy.context.view_layer.objects.active = mesh
mesh.select_set(True)
bm = bmesh.new()
bm.from_mesh(mesh.data)
bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.00002)
bad = [e for e in bm.edges if len(e.link_faces) > 2]
print('SPLIT_NONMANIFOLD', len(bad), flush=True)
bmesh.ops.split_edges(bm, edges=bad)
bm.to_mesh(mesh.data)
bm.free()
modifier = mesh.modifiers.new('Experiment', 'DECIMATE')
modifier.ratio = .075
bpy.ops.object.modifier_apply(modifier=modifier.name)
print('DECIMATED_FACES', len(mesh.data.polygons), flush=True)
for cycle in range(3):
    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    edges = [e for e in bm.edges if len(e.link_faces) > 2]
    bmesh.ops.split_edges(bm, edges=edges)
    bm.to_mesh(mesh.data)
    bm.free()
    modifier = mesh.modifiers.new('Repeat experiment', 'DECIMATE')
    modifier.ratio = min(1, 70000 / len(mesh.data.polygons))
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    print('DECIMATED_PASS', cycle, 'split', len(edges), 'faces', len(mesh.data.polygons), flush=True)
bm = bmesh.new()
bm.from_mesh(mesh.data)
remaining = set(bm.faces)
sizes = []
while remaining:
    seed = remaining.pop()
    stack = [seed]
    count = 0
    while stack:
        face = stack.pop(); count += 1
        for edge in face.edges:
            for other in edge.link_faces:
                if other in remaining:
                    remaining.remove(other); stack.append(other)
    sizes.append(count)
sizes.sort(reverse=True)
print('COMPONENTS', len(sizes), 'top', sizes[:20], 'small_faces', sum(x for x in sizes if x < 50), flush=True)
bm.free()
