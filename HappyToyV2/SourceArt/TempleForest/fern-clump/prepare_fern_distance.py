"""Derive a source-projected distant review LOD; no Unity asset is modified.

Blender 4.0.2 and fast-simplification 0.1.12. The base/source shape-key and
near mesh are preserved. Visual appearance and runtime cost require review.
"""
import hashlib, json
from pathlib import Path
import bpy
import numpy as np
import fast_simplification
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform, closest_point_on_tri

root=Path(__file__).resolve().parent
blend=root/'fern-clump.blend'; output=root/'game-export/fern-clump-lod1.fbx'
report_path=root/'export.json'; report=json.loads(report_path.read_text(encoding='utf-8'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
if sha(root/'original.glb')!=report['originalSha256']: raise RuntimeError('Fern original changed')
bpy.ops.wm.open_mainfile(filepath=str(blend));bpy.context.preferences.filepaths.save_version=0
near=bpy.data.objects['CE_fern-clump_LOD0'];mesh=near.data;mesh.calc_loop_triangles()
previous=bpy.data.objects.get('CE_fern-clump_LOD1')
if previous: bpy.data.objects.remove(previous,do_unlink=True)
vertices=np.empty(len(mesh.vertices)*3,dtype=np.float32);mesh.vertices.foreach_get('co',vertices);vertices=vertices.reshape(-1,3)
faces=np.empty(len(mesh.loop_triangles)*3,dtype=np.int32);mesh.loop_triangles.foreach_get('vertices',faces);faces=faces.reshape(-1,3)
if not mesh.uv_layers.active or not np.isfinite(vertices).all(): raise RuntimeError('Near LOD geometry or UV invalid')
reduced,triangles=fast_simplification.simplify(vertices,faces,target_count=35000,agg=5)
if len(triangles)<1000 or len(triangles)>45000 or not np.isfinite(reduced).all():
    raise RuntimeError('Distant fern exceeds review budget or is invalid: '+str(len(triangles)))
source_vertices=[Vector(v) for v in vertices];fern=BVHTree.FromPolygons(source_vertices,faces.tolist(),all_triangles=True)
atlas=mesh.uv_layers.active.data;uv=np.empty((len(triangles)*3,2),dtype=np.float32);projection=[]
for i,face in enumerate(triangles):
    points=[Vector(reduced[j]) for j in face];_,_,index,distance=fern.find_nearest(sum(points,Vector())/3)
    if index is None:raise RuntimeError('Could not map distant triangle to source')
    tri=mesh.loop_triangles[index];a,b,c=(source_vertices[j] for j in tri.vertices)
    ta,tb,tc=(Vector((*atlas[j].uv,0)) for j in tri.loops);projection.append(distance)
    for corner,point in enumerate(points):
        mapped=barycentric_transform(closest_point_on_tri(point,a,b,c),a,b,c,ta,tb,tc)
        uv[3*i+corner]=(mapped.x,mapped.y)
if not np.isfinite(uv).all():raise RuntimeError('Distant UV invalid')
# Preserve the shared coordinate system: snapping an isolated lowest fragment moved every frond 17 mm.
ground=0.0
raw=bpy.data.meshes.new('Fern clump QEM normals');raw.from_pydata(reduced.tolist(),[],triangles.tolist());raw.update()
for p in raw.polygons:p.use_smooth=True
raw.calc_normals_split();normals=np.empty(len(raw.loops)*3,dtype=np.float32);raw.loops.foreach_get('normal',normals);normals=normals.reshape(-1,3)
# Remove only exactly equal ordered position/UV/normal faces. Split remaining
# coincident geometry so Blender's FBX reimport does not merge distinct UVs.
qem_count=len(triangles);seen=set();keep=[]
for i,face in enumerate(triangles):
    corners=tuple(tuple(reduced[v])+tuple(uv[3*i+j])+tuple(normals[3*i+j]) for j,v in enumerate(face))
    signature=min(corners,corners[1:]+corners[:1],corners[2:]+corners[:2])
    if signature not in seen:seen.add(signature);keep.append(i)
triangles=triangles[keep];uv=uv.reshape(-1,3,2)[keep].reshape(-1,2);normals=normals.reshape(-1,3,3)[keep].reshape(-1,3)
split=triangles.copy();positions=reduced.tolist();seen=set();isolated=0
for i,face in enumerate(triangles):
    key=tuple(sorted(int(v) for v in face))
    if key in seen:
        isolated+=1;start=len(positions);positions.extend(reduced[face].tolist());split[i]=(start,start+1,start+2)
    else:seen.add(key)
data=bpy.data.meshes.new('Fern clump distant source-projected mesh');data.from_pydata(positions,[],split.tolist());data.update()
data.uv_layers.new(name='Projected source atlas').data.foreach_set('uv',uv.ravel())
for face in data.polygons:face.use_smooth=True
data.use_auto_smooth=True;data.normals_split_custom_set(normals.tolist());data.calc_normals_split()
encoded=[l.normal.copy() for l in data.loops];invalid={i for i,n in enumerate(encoded) if n.length<1e-6}
if len(invalid)>100:raise RuntimeError('Too many zero normals in distant fern')
for face in data.polygons:
    for i in face.loop_indices:
        if i in invalid:
            if face.normal.length<1e-6:raise RuntimeError('Cannot repair a degenerate face')
            encoded[i]=face.normal.copy()
if invalid:data.normals_split_custom_set(encoded);data.calc_normals_split()
if any(l.normal.length<1e-6 for l in data.loops):raise RuntimeError('Distant normal repair failed')
for material in mesh.materials:data.materials.append(material)
lod=bpy.data.objects.new('CE_fern-clump_LOD1',data);bpy.data.collections['Editable source-derived game LODs'].objects.link(lod)
lod.matrix_world=near.matrix_world.copy()
bpy.ops.object.select_all(action='DESELECT');lod.select_set(True);bpy.context.view_layer.objects.active=lod
bpy.ops.export_scene.fbx(filepath=str(output),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='STRIP',use_mesh_modifiers=True)
lod.hide_render=True;lod.hide_set(True)
text=bpy.data.texts.get('SOURCE - prepare_fern_distance.py') or bpy.data.texts.new('SOURCE - prepare_fern_distance.py')
text.clear();text.write(Path(__file__).read_text(encoding='utf-8'))
bpy.ops.wm.save_as_mainfile(filepath=str(blend),compress=True)
entry=dict(file=output.name,triangles=len(triangles),vertices=len(positions),sha256=sha(output),qemTriangles=qem_count,
           identicalFacesRemoved=qem_count-len(triangles),coincidentFacesIsolated=isolated,zeroNormalsRepaired=len(invalid),
           projectedCentroidDistanceP95=float(np.percentile(projection,95)),groundOffset=ground,
           simplifier='fast-simplification 0.1.12',targetTriangles=35000,aggressiveness=5)
report['lods']=report['lods'][:1]+[entry]
for path in (report_path,root/'game-export/fern-clump/asset.json'):
    path.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('FERN_LOD1',json.dumps(entry),flush=True)
