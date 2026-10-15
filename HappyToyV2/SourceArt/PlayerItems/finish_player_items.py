"""Blender 4.0.2: source-derived game meshes, editable hand rig and PBR maps.
Run: blender -b --python HappyToyV2/SourceArt/PlayerItems/finish_player_items.py
No GPU model generation: all base surfaces are the preserved GPU 0 webapp GLBs.
"""
import bpy, bmesh, json, pathlib, math, sys
import numpy as np
from mathutils import Vector

ROOT=pathlib.Path(__file__).resolve().parent
OUT=ROOT.parents[1]/'Assets'/'Resources'/'PlayerItems'
OUT.mkdir(parents=True,exist_ok=True)

def rig_hand(obj):
    arm=bpy.data.armatures.new('PlayerHandRig'); rig=bpy.data.objects.new('HandRig',arm)
    bpy.context.collection.objects.link(rig); bpy.context.view_layer.objects.active=rig
    rig.select_set(True); obj.select_set(False); bpy.ops.object.mode_set(mode='EDIT')
    scale=.52
    chains={'little':[(-.12,.22),(-.17,.28),(-.205,.33),(-.22,.365)],
            'ring':[(-.065,.26),(-.085,.335),(-.105,.405),(-.115,.46)],
            'middle':[(-.005,.27),(-.012,.36),(-.017,.44),(-.02,.50)],
            'index':[(.065,.245),(.08,.33),(.09,.407),(.095,.47)],
            'thumb':[(.09,.08),(.15,.145),(.20,.20),(.23,.25)]}
    wrist=arm.edit_bones.new('wrist'); wrist.head=(0,0,-.5*scale); wrist.tail=(0,0,.04*scale)
    palm=arm.edit_bones.new('palm'); palm.head=wrist.tail; palm.tail=(0,0,.245*scale); palm.parent=wrist
    segments=[]
    for name,points in chains.items():
        parent=palm
        for i,(a,b) in enumerate(zip(points,points[1:])):
            bone=arm.edit_bones.new(f'{name}_{i+1}'); bone.head=(a[0]*scale,0,a[1]*scale); bone.tail=(b[0]*scale,0,b[1]*scale); bone.parent=parent
            parent=bone; segments.append((bone.name,Vector(bone.head),Vector(bone.tail)))
    bpy.ops.object.mode_set(mode='OBJECT')
    # Heat weights diffuse smoothly across the welded anatomical surface.
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);rig.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    if not any(m.type=='ARMATURE' for m in obj.modifiers):raise RuntimeError('Automatic hand skin binding failed')
    def smooth(a,b,t):
        t=max(0,min(1,(t-a)/(b-a)));return t*t*(3-2*t)
    # Keep the fleshy palm and forearm stable when finger heat envelopes overlap.
    # Smooth transitions retain the generated knuckles and thumb webbing.
    for v in obj.data.vertices:
        x,z=v.co.x/scale,v.co.z/scale
        wrist=1-smooth(.025,.065,z)
        core=(1-smooth(.075,.12,abs(x)))*(1-smooth(.19,.26,z))
        influence=max(wrist,core)
        if influence<=0:continue
        old=[(entry.group,entry.weight) for entry in v.groups]
        for index,weight in old:obj.vertex_groups[index].add([v.index],weight*(1-influence),'REPLACE')
        for name,weight in [('wrist',influence*wrist),('palm',influence*(1-wrist))]:
            group=obj.vertex_groups[name]
            remaining=next((w*(1-influence) for index,w in old if index==group.index),0)
            group.add([v.index],remaining+weight,'REPLACE')
    rig['handedness']='left; mirror root X for right hand'
    for pose,amount in [('relaxed',.2),('flashlight_grip',1),('snapper_pinch',.7),('talisman_pinch',.5),('throw_release',0)]:
        rig.animation_data_create(); action=bpy.data.actions.new(pose); rig.animation_data.action=action
        for b in rig.pose.bones:
            if '_' not in b.name: continue
            b.rotation_mode='XYZ'; b.rotation_euler.x=amount*(.7 if b.name.startswith('thumb') else 1.05)
            b.keyframe_insert(data_path='rotation_euler',frame=1,group=b.name)
        for b in rig.pose.bones: b.rotation_euler=(0,0,0)
    rig.animation_data.action=None
    return rig

def prepare(key):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ROOT/'Originals'/f'{key}.glb'))
    objs=[o for o in bpy.context.scene.objects if o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]; bpy.ops.object.join(); obj=bpy.context.object
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    obj.name=key; obj.data.name=key+'_surface'
    folder=OUT/key;folder.mkdir(parents=True,exist_ok=True)
    for i,mat in enumerate(obj.data.materials):
        mat.name=f'{key}_surface_{i}'
        for node in mat.node_tree.nodes:
            if node.type!='TEX_IMAGE' or not node.image:continue
            im=node.image
            # GLB packs roughness/metallic in one linear image; preserve it too.
            suffix='albedo' if im.colorspace_settings.name=='sRGB' else 'metalrough'
            im.filepath_raw=str(folder/f'{suffix}.png'); im.file_format='PNG'; im.save()
    height={'hand':.52,'incense':.24,'talisman':.20,'flashlight':.24}[key]
    axis=0 if key=='flashlight' else 2
    coords=np.array([v.co[:] for v in obj.data.vertices]); factor=height/(coords[:,axis].max()-coords[:,axis].min())
    obj.scale=(factor,)*3; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    # Mesh remains upright in Blender Z; FBX converts to Unity Y.
    if key=='flashlight':
        obj.rotation_mode='XYZ';obj.rotation_euler.y=math.pi/2; bpy.ops.object.transform_apply(location=False,rotation=True,scale=False)
    centre=(min(v.co.z for v in obj.data.vertices)+max(v.co.z for v in obj.data.vertices))/2
    for v in obj.data.vertices: v.co.z-=centre
    # GLB repeats vertices at UV/material seams. Weld geometry while retaining
    # per-loop UVs, so simplification preserves continuous skin and paper normals.
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000002)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
    if obj.data.has_custom_normals:bpy.ops.mesh.customdata_custom_splitnormals_clear()
    target={'hand':36000,'incense':8500,'talisman':9000,'flashlight':18000}[key]
    dec=obj.modifiers.new('Source surface game LOD','DECIMATE');dec.ratio=min(1,target/len(obj.data.polygons))
    bpy.ops.object.modifier_apply(modifier=dec.name)
    for p in obj.data.polygons:p.use_smooth=True
    rig=rig_hand(obj) if key=='hand' else None
    if key=='talisman':
        # Preserve UVs on five separately editable, slightly slanted torn strips.
        for i in range(5):
            frag=obj.copy();frag.data=obj.data.copy();bpy.context.collection.objects.link(frag);frag.name=f'fragment_{i}'
            bm=bmesh.new();bm.from_mesh(frag.data)
            for j,clear_inner,clear_outer in [(i,True,False),(i+1,False,True)]:
                if j==0 or j==5:continue
                bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,
                    plane_co=(0,0,-.1+j*.04),plane_no=Vector((.13*(-1 if j%2 else 1),0,1)).normalized(),
                    clear_inner=clear_inner,clear_outer=clear_outer)
            bm.to_mesh(frag.data);bm.free()
            bpy.ops.object.select_all(action='DESELECT');frag.select_set(True);bpy.context.view_layer.objects.active=frag
            bpy.ops.export_scene.fbx(filepath=str(folder/f'fragment_{i}.fbx'),use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
            frag.hide_render=True;frag.hide_set(True)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True)
    if rig:rig.select_set(True);bpy.context.view_layer.objects.active=rig
    else:bpy.context.view_layer.objects.active=obj
    bpy.ops.export_scene.fbx(filepath=str(folder/'geometry.fbx'),use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/f'{key}.blend'))
    report={'original':f'Originals/{key}.glb','heightMetres':height,'vertices':len(obj.data.vertices),'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),
            'rigged':bool(rig),'baseColor':'albedo.png','metallicRoughness':'metalrough.png','materialNames':[m.name for m in obj.data.materials]}
    (folder/'asset.json').write_text(json.dumps(report,indent=2)+'\n')
    print('PREPARED',key,report)

keys=('hand','incense','talisman','flashlight')
requested=next((x.split('=',1)[1] for x in sys.argv if x.startswith('--only=')),None)
if requested and requested not in keys:raise ValueError('Unknown player item: '+requested)
for key in ((requested,) if requested else keys):prepare(key)
