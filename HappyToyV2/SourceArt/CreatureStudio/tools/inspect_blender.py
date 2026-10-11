"""Describe how Blender imports a generated GLB without changing it."""
import argparse,json,sys
from pathlib import Path
import bpy
parser=argparse.ArgumentParser();parser.add_argument('input',type=Path);args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(args.input),merge_vertices=False)
print('IMPORTED_OBJECTS',json.dumps([{'name':o.name,'type':o.type,'parent':o.parent.name if o.parent else None,
    'vertices':len(o.data.vertices)if o.type=='MESH'else None,'bones':len(o.data.bones)if o.type=='ARMATURE'else None,
    'modifiers':[(m.type,m.object.name if m.type=='ARMATURE'and m.object else None)for m in o.modifiers]}for o in bpy.context.scene.objects]),flush=True)
