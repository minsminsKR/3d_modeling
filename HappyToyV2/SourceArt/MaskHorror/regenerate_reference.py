"""One-command reproducible model authoring in pinned Blender 4.0.2."""
from pathlib import Path
import runpy,sys
import bpy
HERE=Path(__file__).resolve().parent
assert bpy.app.version[:3]==(4,0,2),'Use pinned Blender 4.0.2'
sys.path.insert(0,str(HERE));sys.argv=[sys.argv[0]]
for filename in ('author_manyhand_reference.py','sculpt_and_bake.py','finish_surface_bakes.py','export_body_lod.py','validate_sources.py'):
    print('MANYHAND_REGENERATE_STAGE',filename,flush=True)
    runpy.run_path(str(HERE/filename),run_name='__main__')
print('MANYHAND_REGENERATE_COMPLETE',flush=True)