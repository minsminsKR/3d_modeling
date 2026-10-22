"""Blender 4.0.2: derive editable LODs/PBR from the designated-webapp original.

Run from the repository root with Blender --background --factory-startup
--threads 2 --python-exit-code 1 --python <this-file>. No substitute base mesh.
"""
from pathlib import Path
import runpy
import bpy
import bmesh

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[1]
KEY = 'retractable-gate'
SETTINGS = {KEY: dict(axis=2, metres=2.55, texture=2048, budgets=(100000, 65000))}

if __name__ == '__main__':
    if bpy.app.version[:3] != (4, 0, 2):
        raise RuntimeError('Use the documented pinned Blender 4.0.2 authoring build')
    recipe = PROJECT / 'SourceArt/CorridorEntrance/finish_entrance_assets.py'
    prepare = runpy.run_path(str(recipe), run_name='preserved_gate_finisher')['prepare']
    prepare.__globals__.update(SOURCE=SOURCE, PROJECT=PROJECT,
        OUTPUT=PROJECT / 'Assets/Resources/SchoolRetractableGate', SETTINGS=SETTINGS)
    original_simplify = prepare.__globals__['simplify']
    def simplify_gate(obj, budget):
        # Generated seam duplicates and zero-area slivers can prevent bounded collapse.
        # Repair only copied game meshes; per-loop atlas UVs and the original stay intact.
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.0006)
        bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=.000001)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()
        original_simplify(obj, budget)
    prepare.__globals__['simplify'] = simplify_gate
    prepare(KEY, 0)
    print('SCHOOL_RETRACTABLE_GATE_DERIVATION_PASS', flush=True)
