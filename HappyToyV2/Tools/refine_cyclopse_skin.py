"""Refine V1 skin-weight discontinuities geometrically; preserve topology, UVs and source files."""
import bpy, json, math
from mathutils.kdtree import KDTree
from pathlib import Path

root=Path(__file__).resolve().parent.parent
source=root/'Assets/Art/V1/Cyclopse'
out=root/'Assets/Art/Refined/Cyclopse';out.mkdir(parents=True,exist_ok=True)
reports=root/'Verification/cyclopse-skin-refinement';reports.mkdir(parents=True,exist_ok=True)
stored_weights=None;report={}

def key(v):return tuple(round(c,6) for c in v.co)
def measure(obj):
    mesh=obj.data;rest=[obj.matrix_world@v.co for v in mesh.vertices]
    lengths=[(rest[e.vertices[0]]-rest[e.vertices[1]]).length for e in mesh.edges]
    maximum=0;bad=set();counts=[]
    for frame in (1,8,16,24):
        bpy.context.scene.frame_set(frame)
        ev=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());em=ev.to_mesh();points=[ev.matrix_world@v.co for v in em.vertices];count=0
        for e in em.edges:
            a,b=e.vertices;r=(points[a]-points[b]).length/max(lengths[e.index],1e-6)
            maximum=max(maximum,r)
            if r>3:bad.update((a,b));count+=1
        counts.append(count);ev.to_mesh_clear()
    return {'max_edge_stretch':maximum,'edges_over_3x_by_frame':counts},bad

for clip in ('Walking','Run'):
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(source/(clip+'.fbx')))
    obj=next(o for o in bpy.context.scene.objects if o.type=='MESH');mesh=obj.data
    before,bad=measure(obj);groups={g.index:g.name for g in obj.vertex_groups}
    if stored_weights is None:
        weights=[{groups[g.group]:g.weight for g in v.groups} for v in mesh.vertices]
        affected=set(range(len(mesh.vertices)))
        # Geometric Gaussian smoothing avoids topology-density bias and discontinuities at tiny edges.
        tree=KDTree(len(mesh.vertices))
        for v in mesh.vertices:tree.insert(v.co,v.index)
        tree.balance();sigma=.055;updated=[]
        for v in mesh.vertices:
            avg={};total=0
            for co,j,distance in tree.find_range(v.co,sigma*3):
                influence=math.exp(-distance*distance/(2*sigma*sigma));total+=influence
                for name,w in weights[j].items():avg[name]=avg.get(name,0)+w*influence
            updated.append({name:w/total for name,w in avg.items()})
        weights=updated
        stored_weights={key(v):weights[v.index] for v in mesh.vertices}
        report['affected_vertices']=len(affected);report['total_vertices']=len(mesh.vertices)
    assert all(key(v) in stored_weights for v in mesh.vertices),'Source clip meshes differ; do not transfer weights blindly'
    for g in obj.vertex_groups:g.remove(list(range(len(mesh.vertices))))
    for v in mesh.vertices:
        # Keep the smooth blend: a hard top-four truncation introduces new discontinuities.
        weights=[(name,w) for name,w in stored_weights[key(v)].items() if w>1e-6];total=sum(w for _,w in weights)
        for name,w in weights:
            group=obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
            group.add([v.index],w/total,'REPLACE')
    obj.data.update();bpy.context.view_layer.update();after,_=measure(obj)
    report[clip]={'before':before,'after':after}
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    start,end=rig.animation_data.action.frame_range
    bpy.context.scene.frame_start=math.floor(start);bpy.context.scene.frame_end=math.ceil(end)
    report[clip]['export_frame_range']=[start,end];report[clip]['fps']=bpy.context.scene.render.fps
    bpy.context.scene.frame_set(math.floor(start))
    if clip=='Walking':
        bpy.ops.wm.save_as_mainfile(filepath=str(root/'SourceArt/Cyclopse_refined.blend'))
    bpy.ops.export_scene.fbx(filepath=str(out/(clip+'.fbx')),use_selection=False,object_types={'ARMATURE','MESH'},add_leaf_bones=False,
        bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
(reports/'weights.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report,indent=2))
