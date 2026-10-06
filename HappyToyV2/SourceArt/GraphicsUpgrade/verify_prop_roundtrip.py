import bpy,json,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent
expected=json.loads((ROOT/'props-model-manifest.json').read_text())
results=[]
for entry in expected['assets']:
    key=entry['key']
    source=ROOT/'Props'/(key+'.blend')
    bpy.ops.wm.open_mainfile(filepath=str(source))
    meshes=[x for x in bpy.context.scene.objects if x.type=='MESH']
    assert len(meshes)==1,(key,len(meshes))
    packed=[image for image in bpy.data.images if image.type=='IMAGE' and image.source=='FILE']
    assert all(image.packed_file for image in packed),(key,'unpacked image')
    source_mesh=meshes[0].data;source_mesh.calc_loop_triangles()
    zero_area=sum(t.area<1e-15 for t in source_mesh.loop_triangles)
    assert zero_area==0,(key,zero_area)
    assert len(source_mesh.uv_layers)==1
    assert all(math.isfinite(c) and -.001<=c<=1.001 for p in source_mesh.uv_layers.active.data for c in p.uv)
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.outliner.orphans_purge(do_local_ids=True,do_linked_ids=False,do_recursive=True)
    path=ROOT.parents[1]/'Assets/Resources/GraphicsUpgrade/Props'/(key+'.fbx')
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
    meshes=[x for x in bpy.context.scene.objects if x.type=='MESH']
    assert len(meshes)==1,(key,'FBX mesh count')
    obj=meshes[0];obj.data.calc_loop_triangles()
    points=[obj.matrix_world@v.co for v in obj.data.vertices]
    points=[Vector((v.x,v.z,-v.y)) for v in points]
    minimum=[min(v[i] for v in points) for i in range(3)];maximum=[max(v[i] for v in points) for i in range(3)]
    error=max(abs(minimum[i]-entry['boundsMin'][i]) for i in range(3))
    error=max(error,max(abs(maximum[i]-entry['boundsMax'][i]) for i in range(3)))
    assert error<.00001,(key,error)
    assert len(obj.data.loop_triangles)==entry['triangles'],(key,len(obj.data.loop_triangles),entry['triangles'])
    result={'key':key,'status':'PASS','roundtripBoundsErrorMetres':error,'triangles':len(obj.data.loop_triangles),
            'sourcePackedImages':len(packed),'sourceZeroAreaTriangles':zero_area,'uvLayers':len(obj.data.uv_layers),
            'materials':[m.name for m in obj.data.materials]}
    results.append(result);print('PROP_ROUNDTRIP '+json.dumps(result),flush=True)
(ROOT/'fbx-roundtrip-proof.json').write_text(json.dumps({'scope':'Blender source/FBX geometry/UV/units/packed images only; Unity importer/runtime still require root tests','blender':bpy.app.version_string,'assets':results},indent=2),encoding='utf-8')
