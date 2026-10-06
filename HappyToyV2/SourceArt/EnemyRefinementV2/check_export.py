import bpy,json
from pathlib import Path
from mathutils import Vector,kdtree
ROOT=Path(__file__).resolve().parent / 'work';PROJECT=Path(__file__).resolve().parents[2]
report=json.loads((ROOT/'refinement-report.json').read_text())
def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
    rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    for r in rigs:r.data.pose_position='REST'
    bpy.context.view_layer.update();graph=bpy.context.evaluated_depsgraph_get()
    geometry=[];uv=[];bones=[];bonepaths=[]
    for r in rigs:
        for b in r.data.bones:
            p=b;names=[]
            while p:names.append(p.name);p=p.parent
            bones.append(b.name);bonepaths.append('/'.join(reversed(names)))
    for ob in bpy.context.scene.objects:
        if ob.type!='MESH':continue
        ev=ob.evaluated_get(graph);m=ev.to_mesh()
        points=[ev.matrix_world@v.co for v in m.vertices];geometry.extend(points)
        for poly in m.polygons:
            for l in poly.loop_indices:
                p=points[m.loops[l].vertex_index];t=m.uv_layers.active.data[l].uv
                uv.append((p.copy(),t.copy()))
        ev.to_mesh_clear()
    return geometry,uv,sorted(bones),sorted(bonepaths)
results=[]
for item in report:
    original=load(PROJECT/item['source'])
    candidate=load(ROOT/'proposed/Assets/Resources/EnemyRefinement'/(item['key']+'-v2.fbx'))
    kd=kdtree.KDTree(len(original[0]))
    for i,p in enumerate(original[0]):kd.insert(p,i)
    kd.balance();error=max(kd.find(p)[2] for p in candidate[0])
    uvTree=kdtree.KDTree(len(original[1]))
    for index,(point,uv) in enumerate(original[1]):uvTree.insert(point,index)
    uvTree.balance();uvError=0
    for point,uv in candidate[1]:
        near=uvTree.find_range(point,1e-7)
        assert near,'Export lost a source UV corner position'
        uvError=max(uvError,min((uv-original[1][index][1]).length for _,index,_ in near))
    result={'key':item['key'],'originalVertexCount':len(original[0]),'candidateVertexCount':len(candidate[0]),
        'maximumRestSurfaceError':error,'maximumUVCornerError':uvError,
        'uvTriangleCornerCountEqual':len(original[1])==len(candidate[1]),
        'boneNamesEqual':original[2]==candidate[2],'boneHierarchyEqual':original[3]==candidate[3]}
    results.append(result)
    assert error<1e-6,result
    assert uvError<1e-6 and result['uvTriangleCornerCountEqual'] and result['boneNamesEqual'] and result['boneHierarchyEqual'],result
(ROOT/'fbx-roundtrip-results.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
print('ENEMY_FBX_ROUNDTRIP_PASS',json.dumps(results),flush=True)
