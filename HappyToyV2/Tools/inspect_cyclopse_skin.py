"""Read-only FBX geometry/weight diagnostics through Blender's importer."""
import bpy, json, math
from pathlib import Path
from collections import Counter

root=Path(__file__).resolve().parent.parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/Art/V1/Cyclopse/Walking.fbx'))
report={'meshes':[], 'rigs':[]}
for obj in bpy.context.scene.objects:
    if obj.type=='ARMATURE':
        report['rigs'].append({'name':obj.name,'scale':list(obj.scale),'bones':[
            {'name':b.name,'head':list(b.head_local),'tail':list(b.tail_local)} for b in obj.data.bones]})
    if obj.type!='MESH':continue
    mesh=obj.data;groups={g.index:g.name for g in obj.vertex_groups};hist=Counter();unweighted=[];dominant=Counter()
    for v in mesh.vertices:
        weights=[(g.weight,groups[g.group]) for g in v.groups if g.weight>1e-6];hist[len(weights)]+=1
        if not weights:unweighted.append(v.index)
        else:dominant[max(weights)[1]]+=1
    rest=[obj.matrix_world@v.co for v in mesh.vertices]
    edges=[((rest[e.vertices[0]]-rest[e.vertices[1]]).length,e.index) for e in mesh.edges]
    rest_lengths={idx:length for length,idx in edges}
    coincident={}
    for v in mesh.vertices:coincident.setdefault(tuple(round(c,7) for c in v.co),[]).append(v.index)
    seam_differences=[]
    for indices in coincident.values():
        if len(indices)<2:continue
        maps=[{g.group:g.weight for g in mesh.vertices[i].groups} for i in indices]
        diff=max(sum(abs(maps[0].get(k,0)-w.get(k,0)) for k in set(maps[0])|set(w)) for w in maps[1:])
        if diff>.01:seam_differences.append({'vertices':indices,'difference':diff})
    report['seam_differences']=seam_differences[:20];report['seam_difference_count']=len(seam_differences)
    report['worst_edge_vertices']=[{'vertex':i,'position':list(mesh.vertices[i].co),'weights':{groups[g.group]:g.weight for g in mesh.vertices[i].groups}} for i in mesh.edges[9030].vertices]
    ratios=[]
    for frame in (1,8,16,24):
        bpy.context.scene.frame_set(frame)
        evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());em=evaluated.to_mesh()
        points=[evaluated.matrix_world@v.co for v in em.vertices]
        stretch=sorted([((points[e.vertices[0]]-points[e.vertices[1]]).length/max(rest_lengths[e.index],1e-6),e.index) for e in em.edges],reverse=True)
        ratios.append({'frame':frame,'top_stretch':stretch[:12],'over3':sum(r>3 for r,i in stretch)})
        evaluated.to_mesh_clear()
    report['meshes'].append({'name':obj.name,'vertices':len(mesh.vertices),'faces':len(mesh.polygons),'scale':list(obj.scale),
        'weight_histogram':dict(hist),'unweighted':unweighted[:20],'unweighted_count':len(unweighted),'dominant':dict(dominant),
        'longest_edges':sorted(edges,reverse=True)[:12],'posed_edge_ratios':ratios})
out=root/'Verification/cyclopse-skin-inspection';out.mkdir(parents=True,exist_ok=True)
(out/'blender.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report['meshes'],indent=2))
