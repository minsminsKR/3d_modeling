"""Actual exported-tube geometry, per-LOD atlas emission and casing isolation."""
from pathlib import Path
import json
import bpy,bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

source=Path(__file__).resolve().parent;project=source.parents[2]
destination=project/'Assets/Resources/SchoolRenewal/fluorescent-fixture'
report=json.loads((destination/'asset.json').read_text(encoding='utf-8'))
output=project/'Builds/ModelingDownloads/SchoolRenewal/fluorescent-fixture'
results=[]
for index,lod in enumerate(report['lods']):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(destination/lod['file']))
    obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    bm=bmesh.new();bm.from_mesh(obj.data);bm.transform(obj.matrix_world)
    tree=BVHTree.FromBMesh(bm)
    entry=report['materials'][index]
    image=bpy.data.images.load(str(destination/entry['emission']),check_existing=False)
    image.colorspace_settings.name='Non-Color'
    pixels=np.empty(image.size[0]*image.size[1]*4,dtype=np.float32)
    image.pixels.foreach_get(pixels)
    def sample(x,y):
        hit,normal,face,distance=tree.ray_cast(Vector((x,y,-.1)),Vector((0,0,1)),.4)
        if hit is None:raise RuntimeError('Actual generated lamp geometry missing '+str((index,x,y)))
        polygon=obj.data.polygons[face]
        if len(polygon.vertices)!=3:raise RuntimeError('Game FBX must contain explicit triangles')
        verts=[obj.matrix_world@obj.data.vertices[v].co for v in polygon.vertices]
        uv=[obj.data.uv_layers.active.data[k].uv for k in polygon.loop_indices]
        mapped=barycentric_transform(hit,*verts,*(Vector((v.x,v.y,0)) for v in uv))
        sx=min(image.size[0]-1,max(0,int(mapped.x*image.size[0])))
        sy=min(image.size[1]-1,max(0,int(mapped.y*image.size[1])))
        value=float(pixels[(sy*image.size[0]+sx)*4])
        return dict(x=x,y=y,surfaceZ=hit.z,emission=value)
    tubes=[sample(x,y) for x in (-.3,0,.3) for y in (.022,-.054)]
    casing=[sample(0,y) for y in (-.015,.112,-.112)]
    if any(t['emission']<.2 for t in tubes):raise RuntimeError('A physical generated tube lost its baked emission '+str((index,tubes)))
    if any(c['emission']>.08 for c in casing):raise RuntimeError('Generated metal casing glows '+str((index,casing)))
    if casing[0]['surfaceZ']<max(t['surfaceZ'] for t in tubes)+.006:raise RuntimeError('Export fused the two physical tubes')
    material=bpy.data.materials.new('Actual generated PBR and isolated tube emission');material.use_nodes=True
    shader=material.node_tree.nodes.get('Principled BSDF')
    base=material.node_tree.nodes.new('ShaderNodeTexImage');base.image=bpy.data.images.load(str(destination/entry['baseColor']))
    material.node_tree.links.new(base.outputs['Color'],shader.inputs['Base Color'])
    emission=material.node_tree.nodes.new('ShaderNodeTexImage');emission.image=image
    material.node_tree.links.new(emission.outputs['Color'],shader.inputs['Emission Color'])
    shader.inputs['Emission Strength'].default_value=2
    shader.inputs['Roughness'].default_value=.65
    obj.data.materials.clear();obj.data.materials.append(material)
    scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE'
    scene.render.resolution_x=1600;scene.render.resolution_y=700;scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new('Neutral inspection');scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.08,.08,.08,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.3
    lamp=bpy.data.lights.new('Soft material inspection','AREA');lamp.energy=40;lamp.size=2
    light=bpy.data.objects.new(lamp.name,lamp);scene.collection.objects.link(light);light.location=(0,-.6,-1)
    light.rotation_euler=(Vector((0,0,.08))-light.location).to_track_quat('-Z','Y').to_euler()
    camera=bpy.data.cameras.new('Generated tube emission review');camera.type='ORTHO';camera.ortho_scale=1.4
    view=bpy.data.objects.new(camera.name,camera);scene.collection.objects.link(view);view.location=(0,-.45,-1.4)
    view.rotation_euler=(Vector((0,0,.08))-view.location).to_track_quat('-Z','Y').to_euler();scene.camera=view
    scene.render.filepath=str(output/('lamp-emission-review-lod'+str(index)+'.png'));bpy.ops.render.render(write_still=True)
    results.append(dict(lod=index,triangles=sum(len(p.vertices)-2 for p in obj.data.polygons),tubeSamples=tubes,casingSamples=casing))
    bm.free()
(source/'validation.json').write_text(json.dumps(dict(actualExports=results),indent=2)+'\n',encoding='utf-8')
print('SCHOOL_GENERATED_TUBE_MASK_AND_CASING_PASS',json.dumps(results),flush=True)
