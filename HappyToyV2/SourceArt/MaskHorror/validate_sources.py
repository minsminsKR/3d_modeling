"""Read-only Blender checks for the complete editable wraith source package."""
from pathlib import Path
import hashlib
import json
import bmesh
import bpy

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[1]
manifest=json.loads((HERE/'model-manifest.json').read_text(encoding='utf-8'))
assert manifest['blender']=='4.0.2'
assert len(manifest['assets'])==2
for item in manifest['assets']:
    for key,hash_key in [('source','sourceSha256'),('fbx','fbxSha256')]:
        p=PROJECT/item[key]
        assert p.is_file(),p
        assert hashlib.sha256(p.read_bytes()).hexdigest()==item[hash_key],p
    bpy.ops.wm.open_mainfile(filepath=str(PROJECT/item['source']))
    objects=list(bpy.context.scene.objects)
    assert all(obj.type in {'MESH','EMPTY'} for obj in objects)
    meshes=[obj for obj in objects if obj.type=='MESH' and not obj.name.startswith(('SCULPT HIGH |','LOD1 |'))]
    triangles=0
    for obj in meshes:
        assert obj.data.uv_layers,'Missing measured UV: '+obj.name
        assert len(obj.data.materials)>0
        assert len(obj.data.vertices)>0
        assert all(len(p.vertices)<=4 for p in obj.data.polygons),'FBX tangent topology: '+obj.name
        obj.data.calc_loop_triangles();triangles+=len(obj.data.loop_triangles)
    assert triangles==item['triangles'] and triangles<=60000
    points=[obj.matrix_world@v.co for obj in meshes for v in obj.data.vertices]
    for i,axis in enumerate((0,2,1)):
        values=[p[axis]*(1 if i<2 else -1) for p in points]
        assert abs(min(values)-item['boundsMin'][i])<.00001
        assert abs(max(values)-item['boundsMax'][i])<.00001
    for image in bpy.data.images:
        if not image.filepath:continue
        p=Path(bpy.path.abspath(image.filepath)).resolve()
        assert p.is_file(),p
        assert image.filepath.startswith('//'),image.filepath
        assert p.is_relative_to((PROJECT/'Assets/Resources/GraphicsPbr').resolve()) or p.is_relative_to((PROJECT/'Assets/Resources/MaskHorror/Textures').resolve()),p
    high=[obj for obj in objects if obj.type=='MESH' and obj.name.startswith('SCULPT HIGH |')]
    assert len(high)==item['highSculptParts']
    assert all(any(mod.type=='MULTIRES' for mod in obj.modifiers) for obj in high)
    for texture in item['textures']:
        p=PROJECT/texture['path'];assert p.is_file()
        assert hashlib.sha256(p.read_bytes()).hexdigest()==texture['sha256']
        assert texture['width']==2048 and texture['height']==2048
    if item['key']=='wraith-body':
        names={obj.name for obj in objects}
        assert all(name in names for name in ('ArmSwingL','ArmSwingR','LegSwingL','LegSwingR'))
        assert item['boundsMax'][0]-item['boundsMin'][0]>2.3
        lod=item['lod1'];assert (PROJECT/lod['fbx']).is_file()
        assert hashlib.sha256((PROJECT/lod['fbx']).read_bytes()).hexdigest()==lod['fbxSha256']
        assert lod['triangles']<item['triangles']*.5
    else:
        shell=next(obj for obj in meshes if obj.name.startswith('Cracked porcelain shell'))
        assert len(shell.data.vertices)>3000,'Genuine curved shell missing'
        bm=bmesh.new();bm.from_mesh(shell.data)
        assert bm.calc_volume(signed=True)>0,'Closed mask shell normals must point outward'
        assert all(edge.is_manifold for edge in bm.edges),'Shell aperture walls must retain physical thickness'
        bm.free()
    print('MASK_HORROR_SOURCE_PASS',item['key'],triangles,flush=True)
