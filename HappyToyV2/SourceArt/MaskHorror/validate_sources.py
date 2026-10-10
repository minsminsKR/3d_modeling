"""Read-only independent Blender validation of the editable eight-pair package."""
from pathlib import Path
import hashlib,json
import bpy,bmesh
import numpy as np
HERE=Path(__file__).resolve().parent;PROJECT=HERE.parents[1]
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
manifest=json.loads((HERE/'model-manifest.json').read_text(encoding='utf-8'))
assert bpy.app.version[:3]==(4,0,2) and manifest['blender']=='4.0.2'
assert manifest['schema']==2 and manifest['runtime']['armPairs']==8
assert manifest['reference']['sha256']=='0667ecacad32d2904108bc26f79616732babcf66f4d825253a3720c2d7190295'
assert 'Private' in manifest['reference']['provenance'] and len(manifest['assets'])==2
for script in manifest.get('authoringScripts',[]):
    p=PROJECT/script['path'];normalized=p.read_text(encoding='utf-8').replace('\r\n','\n').encode('utf-8')
    assert hashlib.sha256(normalized).hexdigest()==script['sha256NormalizedLf'],'Authoring source hash: '+str(p)
for item in manifest['assets']:
    for key,hash_key in [('source','sourceSha256'),('fbx','fbxSha256')]:
        p=PROJECT/item[key];assert p.is_file(),p
        assert digest(p)==item[hash_key],str(p)+' hash'
        if key=='source':assert p.stat().st_size<100*1024*1024,'GitHub regular-file limit'
    bpy.ops.wm.open_mainfile(filepath=str(PROJECT/item['source']))
    objects=list(bpy.context.scene.objects);assert all(o.type in {'MESH','EMPTY'} for o in objects)
    meshes=[o for o in objects if o.type=='MESH' and not o.name.startswith(('SCULPT HIGH |','LOD1 |'))]
    assert {o.name for o in meshes}==set(item['meshParts'])
    triangles=0
    for obj in meshes:
        assert obj.data.uv_layers and obj.data.vertices,'Empty topology/UV: '+obj.name
        assert obj.data.materials and all(m and m.use_nodes for m in obj.data.materials)
        assert all(len(p.vertices)<=4 for p in obj.data.polygons),'FBX tangent topology: '+obj.name
        obj.data.calc_loop_triangles();triangles+=len(obj.data.loop_triangles)
    assert triangles==item['triangles'] and triangles<170000,(item['key'],triangles)
    uv_areas={}
    for obj in meshes:
        uv=obj.data.uv_layers.active.data;slot=obj.data.materials[0].name
        for poly in obj.data.polygons:
            q=[uv[i].uv for i in poly.loop_indices]
            area=abs(sum(q[i].x*q[(i+1)%len(q)].y-q[(i+1)%len(q)].x*q[i].y for i in range(len(q)))/2)
            uv_areas[slot]=uv_areas.get(slot,0)+area
    main_surface='MW_skin' if item['key']=='wraith-body' else 'MW_mask'
    assert uv_areas[main_surface]>.25,'Real skin/face texel density is required, not merely a 2K image size'
    if 'uvAreaByMaterial' in item:
        assert all(abs(uv_areas[k]-v)<.00001 for k,v in item['uvAreaByMaterial'].items())
    points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    for i,axis in enumerate((0,2,1)):
        values=[p[axis]*(1 if i<2 else -1) for p in points]
        assert abs(min(values)-item['boundsMin'][i])<.00001
        assert abs(max(values)-item['boundsMax'][i])<.00001
    for image in bpy.data.images:
        if not image.filepath:continue
        p=Path(bpy.path.abspath(image.filepath)).resolve()
        assert p.is_file() and image.filepath.startswith('//'),image.filepath
        assert p.is_relative_to((PROJECT/'Assets/Resources/MaskHorror/Textures').resolve()),p
    high=[o for o in objects if o.type=='MESH' and o.name.startswith('SCULPT HIGH |')]
    assert len(high)==item['highSculptParts'] and all(any(m.type=='MULTIRES' for m in o.modifiers) for o in high)
    assert any(m.sculpt_levels==2 for o in high for m in o.modifiers if m.type=='MULTIRES')
    assert all(m.name.startswith('SCULPT procedural source | ') for o in high for m in o.data.materials)
    for texture in item['textures']:
        p=PROJECT/texture['path'];assert p.is_file() and digest(p)==texture['sha256'],p
        image=bpy.data.images.load(str(p),check_existing=False)
        assert tuple(image.size)==(2048,2048) and texture['width']==2048 and texture['height']==2048
        if texture['kind']=='metallic-smoothness':
            image.colorspace_settings.name='Non-Color';values=np.empty(2048*2048*4,np.float32);image.pixels.foreach_get(values)
            values=values.reshape((-1,4));assert values[:,0].max()>.74,'Bell metallic must survive atlas packing'
            assert values[:,3].max()>.75 and values[:,3].min()<.65,'Actual varied roughness missing'
    names={o.name for o in objects}
    if item['key']=='wraith-body':
        for index in range(8):
            assert 'Segment%02d'%index in names
            for side in 'LR':
                suffix='%02d%s'%(index,side)
                assert 'ArmSwing'+suffix in names and 'HandContact'+suffix in names
                assert 'Human five-finger arm '+suffix in names
        assert {'HeadSocket','HeadFrontTarget'}<=names
        socket=bpy.data.objects['HeadSocket'].matrix_world.translation
        assert abs(socket.x)<.01 and -.26<-socket.y<0,'Head must lead original root rather than bbox center'
        assert item['boundsMax'][2]>2.9 and item['boundsMin'][2]<-.5,'Long body must trail from front root'
        lod=item['lod1'];assert digest(PROJECT/lod['fbx'])==lod['fbxSha256']
        assert lod['triangles']<item['triangles']*.5
        assert len([o for o in meshes if o.name.startswith('Human five-finger arm ')])==16
        by_name={o.name[len('SCULPT HIGH | '):]:o for o in high}
        for obj in meshes:
            if obj.data.materials[0].name!='MW_skin':continue
            assert obj.get('anatomicalCurvatureVersion')==1,'Fused construction planes must be relaxed'
            sculpt=by_name[obj.name]
            assert len(sculpt.data.vertices)==len(obj.data.vertices)
            assert all((a.co-b.co).length<.000001 for a,b in zip(obj.data.vertices,sculpt.data.vertices)),'Low/high base must match repaired geometry'
    else:
        shell=next(o for o in meshes if o.name=='Aged human smiling mask shell')
        assert len(shell.data.vertices)>10000,'Genuine volumetric human face missing'
        bm=bmesh.new();bm.from_mesh(shell.data)
        assert bm.calc_volume(signed=True)>0,'Mask surface normals must point outward'
        assert all(e.is_manifold for e in bm.edges),'Physical aperture thickness must be closed'
        bm.free()
        assert {'FaceJoint','FaceFront','FaceRear','Human nose tip landmark','Nose rear landmark'}<=names
        eyes=[o for o in meshes if o.name.startswith('Black eye slit ')]
        assert len(eyes)==2
        for eye in eyes:
            p=[eye.matrix_world@v.co for v in eye.data.vertices]
            width=max(v.x for v in p)-min(v.x for v in p);height=max(v.z for v in p)-min(v.z for v in p)
            assert width>height*2.5 and height<.045
            mat=eye.data.materials[0];assert mat.name=='MW_eye'
            bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
            assert bsdf.inputs['Emission Strength'].default_value==0 and bsdf.inputs['Metallic'].default_value==0
        assert len([o for o in meshes if o.name.startswith('Yellowed human tooth ')])==10
        assert any(o.name.startswith('Wet hair strands ') for o in meshes)
        assert any(o.name.startswith('Hollow black bells ') for o in meshes)
    print('MANYHAND_SOURCE_VALIDATION_PASS',item['key'],'triangles='+str(triangles),
          'bounds='+str((item['boundsMin'],item['boundsMax'])),'mainSurfaceUVArea='+str(uv_areas[main_surface]),
          'sourceMiB=%.2f'%((PROJECT/item['source']).stat().st_size/(1024*1024)),
          'sourceSHA='+item['sourceSha256'],'fbxSHA='+item['fbxSha256'],flush=True)
print('MANYHAND_EDITABLE_PACKAGE_PASS',flush=True)
