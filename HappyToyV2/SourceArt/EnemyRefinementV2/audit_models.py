import bpy, bmesh, json, math, hashlib, os
from pathlib import Path
from mathutils import Vector

PROJECT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent / 'work'
OUT.mkdir(parents=True, exist_ok=True)
ASSETS = [
    ('Cyclopse','Assets/Art/CandidateShapeSkin/Cyclopse/Walking.fbx','Assets/Art/V1/Cyclopse/model_textured.jpg',2.15),
    ('Uncat','Assets/Art/V1/Uncat/Walking.fbx','Assets/Art/V1/Uncat/model_textured.jpg',1.9),
    ('Hwacat_angry','Assets/Art/V1/Hwacat_angry/Zombie Run.fbx','Assets/Art/V1/Hwacat_angry/model_textured.jpg',1.72),
    ('Baby','Assets/Art/V1/Baby/Zombie Crawl.fbx','Assets/Art/V1/Baby/model_textured.jpg',1.05)
]

def bounds(meshes):
    points = []
    graph=bpy.context.evaluated_depsgraph_get()
    for ob in meshes:
        ev=ob.evaluated_get(graph); mesh=ev.to_mesh()
        points.extend(ev.matrix_world @ v.co for v in mesh.vertices)
        ev.to_mesh_clear()
    return Vector(tuple(min(p[i] for p in points) for i in range(3))), Vector(tuple(max(p[i] for p in points) for i in range(3)))

def light(name,position,power,size):
    data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size
    ob=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(ob); ob.location=position
    return ob

report=[]
for key, source, texture, unity_height in ASSETS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.import_scene.fbx(filepath=str(PROJECT/source),use_anim=True)
    scene=bpy.context.scene; scene.frame_set(1)
    meshes=[o for o in scene.objects if o.type=='MESH']; rigs=[o for o in scene.objects if o.type=='ARMATURE']
    entry={'key':key,'source':source,'sourceSHA256':hashlib.sha256((PROJECT/source).read_bytes()).hexdigest(),
        'unityHeight':unity_height,'meshCount':len(meshes),'bones':[b.name for rig in rigs for b in rig.data.bones],
        'actions':[{ 'name':a.name, 'range':list(a.frame_range)} for a in bpy.data.actions],'meshes':[]}
    for ob in meshes:
        bm=bmesh.new();bm.from_mesh(ob.data)
        weight_sums=[sum(g.weight for g in v.groups) for v in ob.data.vertices]
        influences=[len(v.groups) for v in ob.data.vertices]
        entry['meshes'].append({'name':ob.name,'vertices':len(ob.data.vertices),'triangles':sum(len(p.vertices)-2 for p in ob.data.polygons),
            'looseVertices':sum(1 for v in bm.verts if not v.link_edges),'boundaryEdges':sum(1 for e in bm.edges if e.is_boundary),
            'nonmanifoldEdges':sum(1 for e in bm.edges if not e.is_manifold),'zeroAreaFaces':sum(1 for f in bm.faces if f.calc_area()<1e-14),
            'uvLayers':[uv.name for uv in ob.data.uv_layers],'influencesMax':max(influences,default=0),
            'overFourWeights':sum(1 for count in influences if count>4),'unweightedVertices':sum(1 for s in weight_sums if s<1e-7),
            'weightSumMin':min(weight_sums,default=0),'weightSumMax':max(weight_sums,default=0)})
        bm.free()
    lo,hi=bounds(meshes); center=(lo+hi)/2; height=(hi-lo).z
    entry['boundsFirstFrame']={'min':list(lo),'max':list(hi),'size':list(hi-lo)}
    # Real rig evaluation at eight clip phases catches deformation/size changes.
    end=max((a.frame_range.y for a in bpy.data.actions),default=30)
    entry['animationBounds']=[]
    for phase in [0,.125,.25,.375,.5,.625,.75,.875,1]:
        frame=1+phase*(end-1);scene.frame_set(int(frame),subframe=frame%1)
        low,high=bounds(meshes); entry['animationBounds'].append({'frame':frame,'minimumZ':low.z,'size':list(high-low)})
    scene.frame_set(1)
    texture_image=bpy.data.images.load(str(PROJECT/texture),check_existing=True)
    material=bpy.data.materials.new(key+' baseline diffuse');material.use_nodes=True
    bs=material.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.82
    tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=texture_image
    material.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    for ob in meshes:
        ob.data.materials.clear();ob.data.materials.append(material)
        for poly in ob.data.polygons:poly.material_index=0
    scene.render.engine='BLENDER_EEVEE';scene.eevee.use_gtao=True;scene.eevee.gtao_distance=height*.16
    scene.eevee.use_soft_shadows=True;scene.render.resolution_x=640;scene.render.resolution_y=720;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX';scene.world=bpy.data.worlds.new('Proof world');scene.world.use_nodes=True
    scene.world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.08,.08,.085,1)
    scene.world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.4
    camera_data=bpy.data.cameras.new('Proof camera');camera=bpy.data.objects.new('Proof camera',camera_data);scene.collection.objects.link(camera)
    camera.location=center+Vector((height*.95,-height*2.3,height*.28));camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
    camera_data.type='ORTHO';camera_data.ortho_scale=height*1.32
    camera_data.clip_start=height*.003;camera_data.clip_end=height*40;scene.camera=camera
    keylight=light('Soft key',center+Vector((height,-height, height*1.8)),850*height**2,height*1.2)
    keylight.data.shadow_buffer_clip_start=height*.001
    keylight.rotation_euler=(center-keylight.location).to_track_quat('-Z','Y').to_euler()
    rim=light('Rim',center+Vector((-height,height,height)),1000*height**2,height)
    rim.data.shadow_buffer_clip_start=height*.001
    rim.rotation_euler=(center-rim.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.mesh.primitive_plane_add(size=height*8,location=(center.x,center.y,lo.z-.001*height))
    floor=bpy.context.object;floor.name='Proof floor';floor_mat=bpy.data.materials.new('neutral proof floor');floor_mat.diffuse_color=(.07,.07,.065,1);floor.data.materials.append(floor_mat)
    scene.render.filepath=str(OUT/(key+'-baseline.png'));bpy.ops.render.render(write_still=True)
    # Keep an inspectable artist source containing real imported rig/actions and proof setup.
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(key+'-baseline.blend')))
    report.append(entry)
    (OUT/'baseline-audit.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print('ENEMY_BASELINE_READY',key,flush=True)
print('ENEMY_AUDIT_COMPLETE',flush=True)
