import bpy, json, math, hashlib, os
from pathlib import Path
from mathutils import Vector

PROJECT=Path(__file__).resolve().parents[2]
OUT=Path(__file__).resolve().parent / 'work'
DATA=json.loads((OUT/'baseline-audit.json').read_text(encoding='utf-8'))
CANDIDATE=OUT/'proposed/Assets/Resources/EnemyRefinement'
CANDIDATE.mkdir(parents=True,exist_ok=True)
REPORT=[]

def evaluated_points(ob):
    ev=ob.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
    points=[ev.matrix_world@v.co for v in mesh.vertices];ev.to_mesh_clear();return points

def array_hash(values):
    return hashlib.sha256(json.dumps(values,separators=(',',':')).encode()).hexdigest()

for spec in DATA:
    key=spec['key'];bpy.ops.wm.open_mainfile(filepath=str(OUT/(key+'-baseline.blend')))
    scene=bpy.context.scene;model=next(ob for ob in scene.objects if ob.type=='MESH' and ob.name!='Proof floor')
    rig=next(ob for ob in scene.objects if ob.type=='ARMATURE');mesh=model.data
    # Keep topology, UV corners, skin weights, rest positions and clip curves exactly.
    # UV seams are not holes. No weld, remesh, subdivision or bone alteration is used.
    original_positions=[list(v.co) for v in mesh.vertices]
    original_uv=[list(uv.uv) for uv in mesh.uv_layers.active.data]
    original_weights=[[(g.group,g.weight) for g in v.groups] for v in mesh.vertices]
    original_polygons=[list(p.vertices) for p in mesh.polygons]
    phases=[1+p*(max(a.frame_range.y for a in bpy.data.actions)-1) for p in [0,.125,.25,.375,.5,.625,.75,.875,1]]
    original_motion=[]
    for f in phases:scene.frame_set(int(f),subframe=f%1);original_motion.append(evaluated_points(model))
    scene.frame_set(1)
    for polygon in mesh.polygons:polygon.use_smooth=True
    # Area-weighted normals reduce scanned triangular facets. A seam may share a
    # normal only with coincident geometry, matching skin weights and a <60deg crease.
    normal_sums=[Vector((0,0,0)) for _ in mesh.vertices]
    materials=[set() for _ in mesh.vertices]
    for p in mesh.polygons:
        weighted=p.normal*p.area
        for v in p.vertices:normal_sums[v]+=weighted;materials[v].add(p.material_index)
    normals=[n.normalized() if n.length>1e-12 else Vector((0,0,1)) for n in normal_sums]
    clusters={}
    for v in mesh.vertices:
        pos=tuple(round(c,6) for c in v.co)
        weights=tuple((g.group,round(g.weight,5)) for g in v.groups)
        clusters.setdefault((pos,weights,tuple(sorted(materials[v.index]))),[]).append(v.index)
    joined=0
    for group in clusters.values():
        if len(group)<2:continue
        # A complete-link normal cone preserves mouth/eyelid/cloth hard boundaries.
        if any(normals[i].dot(normals[j])<.5 for i in group for j in group):continue
        common=sum((normal_sums[i] for i in group),Vector()).normalized()
        for i in group:normals[i]=common
        joined+=len(group)
    mesh.normals_split_custom_set([normals[loop.vertex_index] for loop in mesh.loops])
    mesh.use_auto_smooth=True;mesh.auto_smooth_angle=math.pi

    # Bake object-space ceramic/fibre relief into the existing UV. It follows the
    # surface continuously across UV islands, rather than adding a screen overlay.
    material=mesh.materials[0];nodes=material.node_tree.nodes;links=material.node_tree.links
    bs=nodes.get('Principled BSDF')
    coords=nodes.new('ShaderNodeTexCoord')
    noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=115 if key=='Baby' else 70
    noise.inputs['Detail'].default_value=3;noise.inputs['Roughness'].default_value=.68
    links.new(coords.outputs['Generated'],noise.inputs['Vector'])
    coarse=nodes.new('ShaderNodeTexNoise');coarse.inputs['Scale'].default_value=11;coarse.inputs['Detail'].default_value=2
    links.new(coords.outputs['Generated'],coarse.inputs['Vector'])
    mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.6
    links.new(noise.outputs['Fac'],mix.inputs[1]);links.new(coarse.outputs['Fac'],mix.inputs[2])
    rough=nodes.new('ShaderNodeMapRange');rough.inputs['To Min'].default_value=.57;rough.inputs['To Max'].default_value=.91
    links.new(coarse.outputs['Fac'],rough.inputs['Value']);links.new(rough.outputs['Result'],bs.inputs['Roughness'])
    bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.08
    local_height=max(v.co.z for v in mesh.vertices)-min(v.co.z for v in mesh.vertices)
    bump.inputs['Distance'].default_value=local_height*.0005
    links.new(mix.outputs[0],bump.inputs['Height']);links.new(bump.outputs['Normal'],bs.inputs['Normal'])
    # UV evidence is geometric and does not rely on the proof renderer.
    assert original_positions==[list(v.co) for v in mesh.vertices]
    assert original_uv==[list(uv.uv) for uv in mesh.uv_layers.active.data]
    assert original_weights==[[(g.group,g.weight) for g in v.groups] for v in mesh.vertices]
    assert original_polygons==[list(p.vertices) for p in mesh.polygons]
    max_motion_error=0
    for f,old in zip(phases,original_motion):
        scene.frame_set(int(f),subframe=f%1);new=evaluated_points(model)
        max_motion_error=max(max_motion_error,max((a-b).length for a,b in zip(old,new)))
    assert max_motion_error<1e-10
    scene.frame_set(1)
    scene.render.filepath=str(OUT/(key+'-refined.png'));bpy.ops.render.render(write_still=True)
    # Normal and smoothness maps use the established atlas; only the new resource
    # variant is imported with >4 skin influences, preserving all source weights.
    scene.render.engine='CYCLES';scene.cycles.samples=8;scene.cycles.device='CPU';scene.render.bake.margin=12
    baked_normal=bpy.data.images.new(key+' tangent normal',width=1024,height=1024,alpha=False)
    baked_normal.colorspace_settings.name='Non-Color'
    target=nodes.new('ShaderNodeTexImage');target.image=baked_normal;nodes.active=target
    bpy.ops.object.select_all(action='DESELECT');model.select_set(True);bpy.context.view_layer.objects.active=model
    scene.render.bake.use_selected_to_active=False
    bpy.ops.object.bake(type='NORMAL');baked_normal.filepath_raw=str(CANDIDATE/(key+'-normal.png'));baked_normal.file_format='PNG';baked_normal.save()
    baked_rough=bpy.data.images.new(key+' roughness bake',width=1024,height=1024,alpha=False);baked_rough.colorspace_settings.name='Non-Color'
    target.image=baked_rough;nodes.active=target;bpy.ops.object.bake(type='ROUGHNESS')
    pixels=list(baked_rough.pixels);packed=bpy.data.images.new(key+' metallic smoothness',width=1024,height=1024,alpha=True)
    packed.colorspace_settings.name='Non-Color'
    output=[]
    for at in range(0,len(pixels),4):output.extend((0,0,0,max(.04,min(.43,1-pixels[at]))))
    packed.pixels.foreach_set(output);packed.filepath_raw=str(CANDIDATE/(key+'-metallic-smoothness.png'));packed.file_format='PNG';packed.save()
    scene.render.engine='BLENDER_EEVEE'
    # A real clip contact sheet uses six evaluated skeletal poses with identical framing.
    for index,phase in enumerate([0,.2,.4,.6,.8,1]):
        f=1+phase*(max(a.frame_range.y for a in bpy.data.actions)-1);scene.frame_set(int(f),subframe=f%1)
        scene.render.filepath=str(OUT/(key+'-motion-'+str(index)+'.png'));bpy.ops.render.render(write_still=True)
    scene.frame_set(1)
    # Export only the original mesh and rig. Proof camera/floor/lights never enter Unity.
    bpy.ops.object.select_all(action='DESELECT');model.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
    fbx=CANDIDATE/(key+'-v2.fbx')
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'ARMATURE','MESH'},
        add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,
        mesh_smooth_type='OFF',use_tspace=True,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
    source_art=OUT/'proposed/SourceArt/EnemyRefinementV2';source_art.mkdir(parents=True,exist_ok=True)
    for image in list(bpy.data.images):
        if image.users==0:bpy.data.images.remove(image)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(source_art/(key+'-refinement-v2.blend')))
    item={'key':key,'source':spec['source'],'sourceSHA256':spec['sourceSHA256'],'vertices':len(mesh.vertices),
        'triangles':sum(len(p.vertices)-2 for p in mesh.polygons),'smoothedCompatibleSeamVertices':joined,
        'positionsSHA256':array_hash(original_positions),'uvSHA256':array_hash(original_uv),'weightsSHA256':array_hash(original_weights),
        'topologySHA256':array_hash(original_polygons),'originalClipPhases':len(phases),'maximumDeformationChange':max_motion_error,
        'candidateFBXSHA256':hashlib.sha256(fbx.read_bytes()).hexdigest(),'modelingChanges':'area-weighted smooth normals with skin/material/crease-safe seam continuity; UV-baked fibre/ceramic relief; spatially varying roughness; full source skin influences on candidate importer'}
    REPORT.append(item);(OUT/'refinement-report.json').write_text(json.dumps(REPORT,indent=2),encoding='utf-8')
    print('ENEMY_REFINEMENT_READY',key,flush=True)
print('ENEMY_REFINEMENT_COMPLETE',flush=True)
