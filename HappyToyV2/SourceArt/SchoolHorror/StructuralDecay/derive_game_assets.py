"""Blender 4.0.2: retain scan silhouettes/UVs and derive bounded game rubble relief.

blender -b --factory-startup --python-exit-code 1 --python <this file>
Original acquired .blend files/maps stay byte-identical. No model synthesis is used.
"""
from pathlib import Path
import hashlib
import json
import math
import uuid

import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[2]
OUT=PROJECT/'Assets/Resources/SchoolHorror/Structural'
NS=uuid.UUID('0648ae4e-fcb2-4323-8bd8-d314a6ca1ac5')
assert bpy.app.version[:3]==(4,0,2), 'Pinned Blender 4.0.2 is required'
OUT.mkdir(parents=True,exist_ok=True)


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def metadata(path,folder=False,normal=False,linear=False,fbx=False):
    destination=Path(str(path)+'.meta')
    if destination.exists():return
    guid=uuid.uuid5(NS,path.relative_to(PROJECT).as_posix()).hex
    if folder:
        text=f'fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    else:
        template=PROJECT/('Assets/Resources/DoorDebris/geometry.fbx.meta' if fbx else 'Assets/Resources/GraphicsPbr/paper-aged/albedo.png.meta')
        text=template.read_text(encoding='utf-8')
        old=text.split('guid: ',1)[1].splitlines()[0];text=text.replace(old,guid,1)
        if not fbx:
            text=text.replace('aniso: 16','aniso: 8')
            if normal:text=text.replace('textureType: 0','textureType: 1')
            if linear or normal:text=text.replace('sRGBTexture: 1','sRGBTexture: 0')
    destination.write_text(text,encoding='utf-8',newline='\n')


def source(asset,suffix):
    directory=HERE/'Originals'/asset/'textures'
    matches=list(directory.glob(asset+'_'+suffix+'_*'))
    assert len(matches)==1,(asset,suffix,matches)
    return matches[0]


def load(path,linear=False):
    image=bpy.data.images.load(str(path),check_existing=False)
    image.colorspace_settings.name='Non-Color' if linear else 'sRGB'
    return image


def save_image(image,name,linear=False):
    image.scale(2048,2048)
    values=np.empty(2048*2048*4,dtype=np.float32);image.pixels.foreach_get(values)
    game=bpy.data.images.new(name,2048,2048,alpha=False,float_buffer=False)
    game.colorspace_settings.name='Non-Color' if linear else 'sRGB'
    game.pixels.foreach_set(values);game.filepath_raw=str(OUT/(name+'.png'));game.file_format='PNG'
    game.save();metadata(OUT/(name+'.png'),normal=name.endswith('-normal'),linear=linear)
    return game


def maps(asset,prefix):
    albedo=save_image(load(source(asset,'diff')),prefix+'-albedo')
    normal=save_image(load(source(asset,'nor_gl'),True),prefix+'-normal',True)
    ao=save_image(load(source(asset,'ao'),True),prefix+'-ao',True)
    rough=load(source(asset,'rough'),True);rough.scale(2048,2048)
    values=np.empty(2048*2048*4,dtype=np.float32);rough.pixels.foreach_get(values)
    packed=np.zeros((2048*2048,4),dtype=np.float32);packed[:,3]=1-np.clip(values.reshape(-1,4)[:,0],0,1)
    image=bpy.data.images.new(prefix+' metallic-smoothness',2048,2048,alpha=True,float_buffer=False)
    image.colorspace_settings.name='Non-Color';image.pixels.foreach_set(packed.ravel())
    image.filepath_raw=str(OUT/(prefix+'-metallic-smoothness.png'));image.file_format='PNG';image.save()
    metadata(OUT/(prefix+'-metallic-smoothness.png'),linear=True)
    return albedo,normal,rough,ao


def material(name,images):
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links;shader=nodes.get('Principled BSDF')
    for image,socket in zip(images[:3],('Base Color','Normal','Roughness')):
        texture=nodes.new('ShaderNodeTexImage');texture.image=image
        if socket=='Normal':
            normal=nodes.new('ShaderNodeNormalMap');links.new(texture.outputs['Color'],normal.inputs['Color']);links.new(normal.outputs['Normal'],shader.inputs['Normal'])
        else:links.new(texture.outputs['Color'],shader.inputs[socket])
    return mat


def export(obj,name):
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    path=OUT/(name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',
        apply_unit_scale=True,bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
    metadata(path,fbx=True)
    obj.data.calc_loop_triangles()
    return {'path':path.relative_to(PROJECT).as_posix(),'sha256':sha(path),'triangles':len(obj.data.loop_triangles),'vertices':len(obj.data.vertices)}


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.scene.unit_settings.system='METRIC'
    metadata(OUT,True)
    concrete=maps('concrete_debris','debris');stone=maps('namaqualand_stones_01','aggregate')
    concrete_mat=material('Acquired photographed concrete debris',concrete)
    stone_mat=material('Acquired quartz aggregate scan',stone)
    original=HERE/'Originals/namaqualand_stones_01/namaqualand_stones_01_4k.blend'
    with bpy.data.libraries.load(str(original),link=False) as (src,dst):
        dst.objects=[name for name in src.objects if name.endswith('_LOD0')]
    outputs=[];models=[]
    for index,obj in enumerate(sorted(dst.objects,key=lambda o:o.name)):
        bpy.context.scene.collection.objects.link(obj);obj.hide_viewport=False;obj.hide_render=False
        bpy.context.view_layer.update();obj.data=obj.data.copy();obj.data.transform(obj.matrix_world);obj.matrix_world=Matrix.Identity(4)
        vertices=[v.co.copy() for v in obj.data.vertices]
        minimum=Vector(tuple(min(v[i] for v in vertices) for i in range(3)));maximum=Vector(tuple(max(v[i] for v in vertices) for i in range(3)))
        offset=Vector(((minimum.x+maximum.x)/2,(minimum.y+maximum.y)/2,minimum.z))
        for v in obj.data.vertices:v.co-=offset
        obj.name='aggregate-'+chr(97+index)
        bpy.context.view_layer.objects.active=obj
        tri=obj.modifiers.new('Preserve original photographed triangulation','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
        original_triangles=len(obj.data.polygons)
        if original_triangles>1500:
            decimate=obj.modifiers.new('Game scan target 1500 triangles','DECIMATE');decimate.ratio=1500/original_triangles
            decimate.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=decimate.name)
        obj.data.materials.clear();obj.data.materials.append(stone_mat)
        for polygon in obj.data.polygons:polygon.use_smooth=True
        result=export(obj,obj.name);result['originalTriangles']=original_triangles
        outputs.append(result);models.append(obj)
    height=load(source('concrete_debris','disp'),True);width,height_px=height.size
    samples=np.empty(width*height_px*4,dtype=np.float32);height.pixels.foreach_get(samples)
    samples=samples.reshape(height_px,width,4)[:,:,0]
    for index in range(3):
        nx,ny=24,60;vertices=[];uv=[];faces=[]
        for row in range(ny+1):
            y=row/ny;edge=math.sin(math.pi*y)**.45
            width_here=.56*(.76+.24*math.sin(y*math.pi*3+index*.9))*edge+.025
            for col in range(nx+1):
                x=col/nx;u=(.08+index*.30+(x-.5)*width_here/2)%1;v=(.03+index*.17+y*.87)%1
                elevation=float(samples[int(v*(height_px-1)),int(u*(width-1))])
                rim=(math.sin(math.pi*x)*math.sin(math.pi*y))**.45
                z=.002+elevation*.075*rim
                vertices.append(((x-.5)*width_here,(y-.5)*1.74,z));uv.append((u,v))
                if col<nx and row<ny:
                    a=row*(nx+1)+col;faces.extend(((a,a+1,a+nx+2),(a,a+nx+2,a+nx+1)))
        mesh=bpy.data.meshes.new('Photographed debris relief '+str(index));mesh.from_pydata(vertices,[],faces);mesh.update()
        layer=mesh.uv_layers.new(name='Preserved scan crop')
        for polygon in mesh.polygons:
            polygon.use_smooth=True
            for loop in polygon.loop_indices:layer.data[loop].uv=uv[mesh.loops[loop].vertex_index]
        obj=bpy.data.objects.new('scan-rubble-'+chr(97+index),mesh);bpy.context.scene.collection.objects.link(obj)
        obj.data.materials.append(concrete_mat);outputs.append(export(obj,obj.name));models.append(obj)
    for i,obj in enumerate(models):obj.location.x=(i%4)*.85;obj.location.y=(i//4)*2.15
    # Keep only the actual edited game assemblies/material images in this packed
    # handoff; immutable full-resolution scenes/maps are preserved beside it.
    for mat in list(bpy.data.materials):
        if mat not in (concrete_mat,stone_mat):bpy.data.materials.remove(mat,do_unlink=True)
    for image in list(bpy.data.images):
        if image.users==0:bpy.data.images.remove(image)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'school-structural-decay.blend'),compress=True)
    report={'blender':bpy.app.version_string,'script':Path(__file__).name,'scriptSha256':sha(Path(__file__)),
        'method':'Original LOD0 stone scans decimated with original UVs; concrete rubble relief samples the acquired photographic heightfield with authored tapered footprint. No synthesized base model.',
        'heightfieldScaleMetres':.075,'gameMapResolution':2048,'models':outputs,
        'gameMaps':[{'path':p.relative_to(PROJECT).as_posix(),'sha256':sha(p)} for p in sorted(OUT.glob('*.png'))],
        'editableBlendSha256':sha(HERE/'school-structural-decay.blend')}
    (HERE/'game-derivation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('SCHOOL_STRUCTURAL_ASSETS_DERIVED',len(outputs),'models')


if __name__=='__main__':main()
