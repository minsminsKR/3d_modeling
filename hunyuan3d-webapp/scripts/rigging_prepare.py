"""Build a bounded analysis mesh; never modify the original generated asset."""
import json
import os
import sys
from pathlib import Path
from rigging_process_guard import install_process_guard

install_process_guard()
import bpy

request = json.loads(Path(sys.argv[1]).read_text())
source = request['source']
output = request['analysis_mesh']
budget = request['analysis_faces']
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source)
collection = bpy.data.collections.get('glTF_not_exported')
if collection:
    for obj in list(collection.objects): bpy.data.objects.remove(obj,do_unlink=True)
meshes = [obj for obj in bpy.context.scene.objects if obj.type=='MESH']
before = sum(len(obj.data.polygons) for obj in meshes)
if before == 0: raise RuntimeError('모델에서 메시를 찾지 못했습니다.')
if before > budget:
    for obj in meshes:
        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new('RigAnalysisDecimate','DECIMATE')
        modifier.ratio = max(.001, budget/before)
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
after = sum(len(obj.data.polygons) for obj in meshes)
bpy.ops.export_scene.gltf(filepath=output,export_format='GLB')
Path(request['output_dir'],'preparation.json').write_text(json.dumps({'source_faces':before,'analysis_faces':after,'face_budget':budget}))
print('Analysis mesh prepared',before,'->',after,flush=True)
# Embedded bpy 4.2's interpreter teardown segfaults on this host after a
# successful export. Check the completed file, then exit the isolated helper
# without invoking Blender's faulty global destructors.
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from rigging_artifacts import inspect_glb
inspect_glb(output,require_skin=False)
sys.stdout.flush();sys.stderr.flush()
os._exit(0)
