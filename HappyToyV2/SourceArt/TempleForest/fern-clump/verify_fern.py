"""Independent CPU surface and FBX reimport checks for the review tree.

Blender 4.0.2. Samples geometry; it does not approve game-camera appearance.
"""
import hashlib, json, math
from pathlib import Path
import bpy
from mathutils.bvhtree import BVHTree
from mathutils import Vector, Matrix

root = Path(__file__).resolve().parent
report = json.loads((root/'export.json').read_text(encoding='utf-8'))
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
if sha(root/'original.glb') != report['originalSha256']:
    raise RuntimeError('Generated source hash changed')
blend = root/'fern-clump.blend'
bpy.ops.wm.open_mainfile(filepath=str(blend))
full = bpy.data.objects['geometry_0']
corners=[full.matrix_world@Vector(x) for x in full.bound_box]
lo=Vector(tuple(min(x[i] for x in corners) for i in range(3)));hi=Vector(tuple(max(x[i] for x in corners) for i in range(3)))
centre=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z)); transform=Matrix.Scale(1/(hi.x-lo.x),4)@Matrix.Translation(-centre)@full.matrix_world
points=[transform@full.data.vertices[i].co for i in range(0,len(full.data.vertices),173)]
low=[p for p in points if p.z<0.1];canopy=[p for p in points if p.z>=0.1]
def measure(obj):
    m=obj.data; m.calc_loop_triangles();m.calc_normals_split()
    if not m.uv_layers.active or len(m.uv_layers.active.data) != len(m.loops):
        raise RuntimeError('Candidate atlas missing')
    if any(not math.isfinite(value) for loop in m.uv_layers.active.data for value in loop.uv):
        raise RuntimeError('Candidate UV is non-finite')
    verts=[obj.matrix_world @ v.co for v in m.vertices]
    faces=[tuple(t.vertices) for t in m.loop_triangles]
    tree=BVHTree.FromPolygons(verts,faces,all_triangles=True)
    def distances(samples):
        ds=sorted(tree.find_nearest(p)[3] for p in samples)
        if not all(math.isfinite(d) for d in ds): raise RuntimeError('Nonfinite surface distance')
        return dict(samples=len(ds),p95=ds[math.ceil((len(ds)-1)*.95)],maximum=ds[-1])
    area=sum((verts[b]-verts[a]).cross(verts[c]-verts[a]).length*.5 for a,b,c in faces)
    return dict(triangles=len(faces),vertices=len(verts),area=area,
                zeroNormalLoops=sum(loop.normal.length<1e-6 for loop in m.loops),
                allSurfaceDistance=distances(points),lowDistance=distances(low),
                frondDistance=distances(canopy))
editable=[]
for i,entry in enumerate(report['lods']):
    obj=bpy.data.objects['CE_fern-clump_LOD'+str(i)];m=measure(obj)
    if m['triangles'] != entry['triangles']:raise RuntimeError('Editable triangle count differs from report')
    editable.append(m)
exported=[]
for i,entry in enumerate(report['lods']):
    path=root/'game-export'/entry['file']
    if sha(path)!=entry['sha256']:raise RuntimeError('FBX hash differs: '+path.name)
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path))
    models=[o for o in bpy.data.objects if o.type=='MESH']
    if len(models)!=1:raise RuntimeError('Expected one review FBX mesh')
    result=measure(models[0])
    result['areaRelativeLoss']=(editable[i]['area']-result['area'])/editable[i]['area']
    result['triangleLoss']=editable[i]['triangles']-result['triangles']
    exported.append(result)
proof=dict(scope='CPU source sampling and Blender FBX reimport only; no visual, Unity or performance approval',
           sourceSha256=report['originalSha256'],editableSha256=sha(blend),
           exportSha256=[entry['sha256'] for entry in report['lods']],
           editable=editable,exported=exported)
(root/'surface-proof.json').write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8')
print('FERN_SURFACE',json.dumps(proof),flush=True)
