"""Blender 4.0.2: derive game meshes from retained webapp originals.
Remove dense atlas constraints before collapse, then bake original colour,
roughness and normals onto the new UVs. No substitute base mesh is created.
blender --background --factory-startup --python-exit-code 1 --python prepare_game_props.py
"""
from pathlib import Path
import argparse,hashlib,json,math,sys,shutil
import bpy,bmesh
import numpy as np
from mathutils import Vector
SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[1]
SETTINGS={'package':(.112,1800),'pellet':(.028,1200)}
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def select(target,source=None):
    bpy.ops.object.select_all(action='DESELECT')
    if source:source.hide_set(False);source.hide_render=False;source.select_set(True)
    target.select_set(True);bpy.context.view_layer.objects.active=target

def apply_package_print(obj,destination):
    # Reproject the clear original imagegen reference onto the two broad carton
    # faces. This local UV finishing prevents image-to-3D text reconstruction
    # from turning the Korean name into illegible glyphs. Side PBR stays baked.
    reference=SOURCE/'package-reference.png';filename='SN_package_print-baseColor.png'
    shutil.copy2(reference,destination/filename)
    image=bpy.data.images.load(str(destination/filename),check_existing=True);image.colorspace_settings.name='sRGB';image.pack()
    material=bpy.data.materials.get('SN_package_print') or bpy.data.materials.new('SN_package_print');material.use_nodes=True
    nodes=material.node_tree.nodes;shader=nodes.get('Principled BSDF');shader.inputs['Metallic'].default_value=0;shader.inputs['Roughness'].default_value=.9
    texture=nodes.get('Reference carton printing') or nodes.new('ShaderNodeTexImage');texture.name='Reference carton printing';texture.image=image;material.node_tree.links.new(texture.outputs['Color'],shader.inputs['Base Color'])
    slots=[m.name if m else '' for m in obj.data.materials]
    if material.name not in slots:obj.data.materials.append(material)
    index=list(obj.data.materials).index(material)
    points=[obj.matrix_world@Vector(c) for c in obj.bound_box]
    low=Vector(tuple(min(v[i] for v in points) for i in range(3)));high=Vector(tuple(max(v[i] for v in points) for i in range(3)))
    normals=obj.matrix_world.to_3x3().inverted().transposed();uv=obj.data.uv_layers.active.data
    width,height=image.size
    TL=Vector((210/width,1-192/height));TR=Vector((900/width,1-251/height));BL=Vector((215/width,1-1092/height));BR=Vector((900/width,1-1208/height))
    for polygon in obj.data.polygons:
        normal=(normals@polygon.normal).normalized()
        if abs(normal.x)<.85:continue
        polygon.material_index=index
        for loop_index in polygon.loop_indices:
            point=obj.matrix_world@obj.data.vertices[obj.data.loops[loop_index].vertex_index].co
            u=(point.y-low.y)/max(1e-6,high.y-low.y)
            if normal.x<0:u=1-u
            v=(point.z-low.z)/max(1e-6,high.z-low.z);u=max(0,min(1,u));v=max(0,min(1,v))
            uv[loop_index].uv=((1-u)*BL+u*BR)*(1-v)+((1-u)*TL+u*TR)*v
    return dict(name=material.name,roughness=.9,baseColor=filename)

def prepare(key):
    height,budget=SETTINGS[key];original=SOURCE/(key+'-original.glb')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(original))
    originals=[o for o in bpy.context.scene.objects if o.type=='MESH']
    if len(originals)!=1:raise ValueError('Review unexpected multi-mesh webapp output: '+key)
    raw=originals[0];original_faces=len(raw.data.polygons)
    original_collection=bpy.data.collections.new('Complete webapp original');bpy.context.scene.collection.children.link(original_collection)
    for collection in list(raw.users_collection):collection.objects.unlink(raw)
    original_collection.objects.link(raw)
    game_collection=bpy.data.collections.new('Unity game LOD');bpy.context.scene.collection.children.link(game_collection)
    obj=raw.copy();obj.data=raw.data.copy();obj.parent=None;obj.matrix_world=raw.matrix_world.copy();obj.name='SN_'+key;game_collection.objects.link(obj)
    points=[obj.matrix_world@Vector(c) for c in obj.bound_box]
    low=Vector(tuple(min(p[i] for p in points) for i in range(3)));high=Vector(tuple(max(p[i] for p in points) for i in range(3)))
    factor=height/max(.00001,high.z-low.z);centre=Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z))
    obj.location=(obj.location-centre)*factor;obj.scale*=factor
    select(obj);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    # This temporary normalized high-poly copy is the bake source. The complete
    # original stays byte-identical in GLB and separately retained in the blend.
    highpoly=obj.copy();highpoly.data=obj.data.copy();highpoly.name='Temporary original surface bake source';game_collection.objects.link(highpoly)
    for layer in list(obj.data.uv_layers):obj.data.uv_layers.remove(layer)
    # Generated surfaces have many disconnected micro-patches. Reconstruct a
    # connected surface FROM this original geometry before collapse; original
    # appearance is transferred by the selected-to-active surface bakes below.
    shell=obj.modifiers.new('Close thin reconstructed paper surface patches','SOLIDIFY')
    shell.thickness=height/80;shell.offset=0;shell.use_even_offset=False;shell.thickness_clamp=0
    bpy.ops.object.modifier_apply(modifier=shell.name)
    remesh=obj.modifiers.new('Connected surface from original webapp geometry','REMESH')
    remesh.mode='VOXEL';remesh.voxel_size=height/200;remesh.use_smooth_shade=True;remesh.use_remove_disconnected=False
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    if key=='package':
        # Complete the closed carton silhouette using the generated carton's own
        # surface hull. No hand-authored box or substitute base is introduced.
        bm=bmesh.new();bm.from_mesh(obj.data)
        hull=bmesh.ops.convex_hull(bm,input=list(bm.verts),use_existing_faces=False)
        hull_faces={v for v in hull['geom'] if isinstance(v,bmesh.types.BMFace)}
        bmesh.ops.delete(bm,geom=[f for f in bm.faces if f not in hull_faces],context='FACES_ONLY')
        bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
        bmesh.ops.triangulate(bm,faces=list(bm.faces));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bm.to_mesh(obj.data);bm.free()
    if key=='pellet':
        # Discard tiny disconnected reconstruction specks, keeping the wrapper
        # and every substantial paper/tail component. The raw original remains.
        bm=bmesh.new();bm.from_mesh(obj.data);groups=[]
        for vertex in bm.verts:vertex.tag=False
        for vertex in bm.verts:
            if vertex.tag:continue
            group=[];pending=[vertex];vertex.tag=True
            while pending:
                current=pending.pop();group.append(current)
                for edge in current.link_edges:
                    other=edge.other_vert(current)
                    if not other.tag:other.tag=True;pending.append(other)
            groups.append(group)
        sizes=sorted((len(group) for group in groups),reverse=True)
        minimum=max(64,int(sizes[0]*.005));specks=[v for group in groups if len(group)<minimum for v in group]
        print('ISLANDS',key,'largest',sizes[:10],'removed vertices',len(specks),flush=True)
        bmesh.ops.delete(bm,geom=specks,context='VERTS');bm.to_mesh(obj.data);bm.free()
    for attempt in range(4):
        if len(obj.data.polygons)<=budget:break
        modifier=obj.modifiers.new('Derived collapse without original UV island constraints','DECIMATE');modifier.ratio=min(1,budget*.98/max(1,sum(len(p.vertices)-2 for p in obj.data.polygons)));modifier.use_collapse_triangulate=True;modifier.delimit=set()
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        print('GAME_MESH',key,attempt,len(obj.data.polygons),flush=True)
    if len(obj.data.polygons)>budget:raise ValueError('Game triangle budget not met: '+str(len(obj.data.polygons)))
    bpy.context.view_layer.update()
    check_points=[obj.matrix_world@Vector(c) for c in obj.bound_box]
    extent=Vector(tuple(max(v[i] for v in check_points)-min(v[i] for v in check_points) for i in range(3)))
    if not height*.85<=extent.z<=height*1.15 or extent.x>(high.x-low.x)+height*.12 or extent.y>(high.y-low.y)+height*.12:
        raise ValueError('Derived shape bounds diverged from original: '+str(tuple(extent)))
    obj.data.use_auto_smooth=True;obj.data.auto_smooth_angle=math.radians(35)
    for polygon in obj.data.polygons:polygon.use_smooth=True
    select(obj);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')
    material=bpy.data.materials.new('SN_'+key+'_0');material.use_nodes=True;nodes=material.node_tree.nodes;links=material.node_tree.links
    shader=nodes.get('Principled BSDF');shader.inputs['Metallic'].default_value=0;shader.inputs['Roughness'].default_value=.9
    obj.data.materials.clear();obj.data.materials.append(material)
    for polygon in obj.data.polygons:polygon.material_index=0
    target=nodes.new('ShaderNodeTexImage');nodes.active=target
    destination=PROJECT/'Assets/Resources/Snappers'/key;destination.mkdir(parents=True,exist_ok=True)
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=8
    scene.render.bake.use_selected_to_active=True;scene.render.bake.cage_extrusion=height*.025;scene.render.bake.max_ray_distance=height*.08
    # Hide the unnormalized archival original, never the selected bake source.
    original_collection.hide_render=True;original_collection.hide_viewport=True
    results={};images={}
    for label,bake_type in [('baseColor','DIFFUSE'),('roughness','ROUGHNESS'),('normal','NORMAL')]:
        image=bpy.data.images.new('SN_'+key+'_'+label,width=2048,height=2048,alpha=True)
        image.colorspace_settings.name='sRGB' if label=='baseColor' else 'Non-Color'
        target.image=image;nodes.active=target;select(obj,highpoly)
        print('BAKE',key,label,flush=True)
        if bake_type=='DIFFUSE':bpy.ops.object.bake(type=bake_type,pass_filter={'COLOR'},margin=12)
        else:bpy.ops.object.bake(type=bake_type,margin=12)
        image.file_format='PNG';image.filepath_raw=str(destination/(material.name+'-'+label+'.png'));image.save();image.pack();images[label]=image;results[label]=Path(image.filepath_raw).name
    # Complete source reconstruction gaps with the surrounding physical paper
    # colour. Empty normal-bake pixels identify ray misses, not printed black ink.
    n=np.array(images['normal'].pixels[:],dtype=np.float32).reshape((-1,4))
    missing=np.all(n[:,:3]<1e-6,axis=1)|(n[:,3]<.01)
    if np.any(missing):
        c=np.array(images['baseColor'].pixels[:],dtype=np.float32).reshape((-1,4))
        paper=np.median(c[~missing,:3],axis=0) if np.any(~missing) else np.array([.7,.55,.02])
        for label in ['baseColor','roughness','normal']:
            image=images[label];pixels=np.array(image.pixels[:],dtype=np.float32).reshape((-1,4))
            pixels[missing,:3]=paper if label=='baseColor' else (.9,.9,.9) if label=='roughness' else (.5,.5,1)
            pixels[missing,3]=1;image.pixels.foreach_set(pixels.ravel());image.save();image.pack()
    # Unity's standard Lit packed map: R=metallic=0, A=smoothness=1-roughness.
    pixels=np.array(images['roughness'].pixels[:],dtype=np.float32).reshape((-1,4));pixels[:,3]=1-pixels[:,0];pixels[:,:3]=0
    packed=bpy.data.images.new('SN_'+key+'_metallicSmoothness',width=2048,height=2048,alpha=True);packed.colorspace_settings.name='Non-Color';packed.pixels.foreach_set(pixels.ravel());packed.file_format='PNG';packed.filepath_raw=str(destination/(material.name+'-metallicSmoothness.png'));packed.save();packed.pack();results['metallicSmoothness']=Path(packed.filepath_raw).name
    # Keep the roughness bake inside the editable source; the Unity packed map is
    # its reproducible derivative, so the standalone gray PNG need not be shipped.
    (destination/results['roughness']).unlink();results.pop('roughness')
    target.image=images['baseColor'];links.new(target.outputs['Color'],shader.inputs['Base Color'])
    rough=nodes.new('ShaderNodeTexImage');rough.image=images['roughness'];links.new(rough.outputs['Color'],shader.inputs['Roughness'])
    normal=nodes.new('ShaderNodeTexImage');normal.image=images['normal'];normal_map=nodes.new('ShaderNodeNormalMap');links.new(normal.outputs['Color'],normal_map.inputs['Color']);links.new(normal_map.outputs['Normal'],shader.inputs['Normal'])
    bpy.data.objects.remove(highpoly,do_unlink=True)
    for image in bpy.data.images:
        if image.source=='FILE' and not image.packed_file:image.pack()
    materials=[dict(name=material.name,roughness=.9,**results)]
    if key=='package':materials.append(apply_package_print(obj,destination))
    bpy.context.preferences.filepaths.save_version=0;select(obj)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(key+'.blend')))
    fbx=destination/'geometry.fbx';bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='RELATIVE',use_mesh_modifiers=True)
    report=dict(key=key,tool='Blender 4.0.2',original=original.name,originalSha256=sha(original),editable=key+'.blend',heightMetres=height,targetTriangles=budget,originalFaces=original_faces,gameFaces=len(obj.data.polygons),fbx=fbx.relative_to(PROJECT).as_posix(),materials=materials,textures='Original full PBR embedded in GLB and packed blend; original surfaces rebaked to new 2048px game UV atlas (colour, roughness, tangent normals)',bounds=dict(width=(high.x-low.x)*factor,depth=(high.y-low.y)*factor,height=height))
    if key=='package':report['printFinishing']='Broad carton faces reproject the legible package-reference.png front panel; side/edge PBR and original generated GLB retained.'
    (destination/'asset.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');(SOURCE/(key+'-export.json')).write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');print('GAME_PROP '+json.dumps(report),flush=True)
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--only',choices=SETTINGS);args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    for key in ([args.only] if args.only else SETTINGS):prepare(key)
