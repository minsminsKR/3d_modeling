"""Bounded rear-body cleanup; preserve face, limbs, topology, UVs and weights."""
import bpy, json, math
from pathlib import Path
from mathutils import Vector

root=Path(__file__).resolve().parent.parent
out=root/'Assets/Art/CandidateShape/Cyclopse';out.mkdir(parents=True,exist_ok=True)
positions=None;report={}
def key(v):return tuple(round(c,6) for c in v.co)
def fade(value):return max(0,min(1,value))
for clip in ['Walking','Run']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root/'Assets/Art/Candidate/Cyclopse'/(clip+'.fbx')))
    obj=next(o for o in bpy.context.scene.objects if o.type=='MESH');mesh=obj.data
    original=[v.co.copy() for v in mesh.vertices]
    uv_before=[tuple(loop.uv) for loop in mesh.uv_layers.active.data]
    weights_before=[[(g.group,g.weight) for g in v.groups] for v in mesh.vertices]
    if positions is None:
        base={key(v):v.co.copy() for v in mesh.vertices}
        adjacency={k:set() for k in base}
        for edge in mesh.edges:
            a,b=[key(mesh.vertices[i]) for i in edge.vertices]
            if a!=b:adjacency[a].add(b);adjacency[b].add(a)
        current={k:v.copy() for k,v in base.items()}
        # Local Y is vertical and negative Z is rear in the imported FBX.
        influence={k:fade((-p.z-.08)/.25)*fade((p.y+.55)/.20)*fade((.72-abs(p.x))/.18) for k,p in base.items()}
        for iteration in range(10):
            updated={}
            for k,p in current.items():
                if not adjacency[k] or influence[k]==0:updated[k]=p;continue
                avg=sum((current[n] for n in adjacency[k]),Vector())/len(adjacency[k])
                target=p+(avg-p)*(.32*influence[k])
                delta=target-base[k]
                cap=.035*influence[k]
                if delta.length>cap:delta=delta.normalized()*cap
                updated[k]=base[k]+delta
            current=updated
        positions=current
    assert all(key(v) in positions for v in mesh.vertices),'Clip topology does not match'
    for v in mesh.vertices:v.co=positions[key(v)]
    mesh.update()
    assert uv_before==[tuple(loop.uv) for loop in mesh.uv_layers.active.data]
    assert weights_before==[[(g.group,g.weight) for g in v.groups] for v in mesh.vertices]
    changes=[(v.co-original[v.index]).length for v in mesh.vertices]
    report[clip]=dict(vertices=len(mesh.vertices),changed=sum(d>1e-7 for d in changes),maximum_displacement=max(changes),uv_preserved=True,weights_preserved=True)
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    start,end=rig.animation_data.action.frame_range
    bpy.context.scene.frame_start=math.floor(start);bpy.context.scene.frame_end=math.ceil(end);bpy.context.scene.frame_set(math.floor(start))
    if clip=='Walking':bpy.ops.wm.save_as_mainfile(filepath=str(root/'SourceArt/Cyclopse_candidate_shape.blend'))
    bpy.ops.export_scene.fbx(filepath=str(out/(clip+'.fbx')),object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,
        bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
(root/'Verification/cyclopse-full-clip/shape-candidate.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
