"""Inspect the preserved pine mesh in background Blender; no files are changed."""
from pathlib import Path
import bpy
import bmesh

source = Path(__file__).resolve().parent / 'pine' / 'original.glb'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
for obj in bpy.context.scene.objects:
    if obj.type != 'MESH':
        continue
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    print('PINE_TOPOLOGY', obj.name, 'vertices', len(bm.verts), 'faces', len(bm.faces),
          'boundary_edges', sum(e.is_boundary for e in bm.edges),
          'non_manifold_edges', sum(not e.is_manifold for e in bm.edges), flush=True)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.00002)
    print('PINE_WELDED', 'vertices', len(bm.verts), 'faces', len(bm.faces),
          'boundary_edges', sum(e.is_boundary for e in bm.edges),
          'non_manifold_edges', sum(not e.is_manifold for e in bm.edges), flush=True)
    bm.free()
