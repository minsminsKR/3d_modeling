"""Read-only Blender 4.0.2 source face/UV inventory for static encounter models."""
from pathlib import Path
import argparse
import json
import sys
import bpy
from mathutils import Vector
sys.path.insert(0, str(Path(__file__).resolve().parent))
from inspect_head_sources import source_eye_uv

PROJECT = Path(__file__).resolve().parents[2]
EYE_PIXELS = {
    # The mask has actual eye holes. Lower/upper lip pairs are averaged at
    # runtime, placing each core in its aperture rather than on porcelain.
    'LanternMask': ((275, 418), (284, 352), (511, 418), (520, 341)),
    'Mannequin': ((301, 457), (417, 431)),
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--resource-project', type=Path, default=PROJECT)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--preview-dir', type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    records = []
    for key in ('LanternMask', 'Mannequin'):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path = args.resource_project / ('Assets/Art/V1/'+key+'/'+key+'.fbx')
        bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
        scene = bpy.context.scene
        meshes = [obj for obj in scene.objects if obj.type == 'MESH']
        material = bpy.data.materials.new('Existing authored face albedo'); material.use_nodes = True
        nodes = material.node_tree.nodes; shader = nodes.get('Principled BSDF')
        texture = nodes.new('ShaderNodeTexImage'); texture.image = bpy.data.images.load(str(path.parent/'Image_0.png'))
        material.node_tree.links.new(texture.outputs['Color'], shader.inputs['Base Color'])
        shader.inputs['Roughness'].default_value = .65
        for mesh in meshes:
            mesh.data.materials.clear(); mesh.data.materials.append(material)
        points = [obj.matrix_world@vertex.co for obj in meshes for vertex in obj.data.vertices]
        lo = Vector(tuple(min(point[i] for point in points) for i in range(3)))
        hi = Vector(tuple(max(point[i] for point in points) for i in range(3)))
        height = (hi-lo).z
        centre = (lo+hi)/2
        if key == 'Mannequin': centre.z = hi.z-height*.125; crop = height*.37
        else: crop = max((hi-lo).z, (hi-lo).x)*1.2
        camera_data = bpy.data.cameras.new('Optional source face crop'); camera = bpy.data.objects.new('Optional source face crop',camera_data)
        scene.collection.objects.link(camera); scene.camera = camera
        camera.location = centre+Vector((0,-height*3,height*.025)); camera.rotation_euler = (centre-camera.location).to_track_quat('-Z','Y').to_euler()
        camera_data.type='ORTHO'; camera_data.ortho_scale=crop
        world=bpy.data.worlds.new('Source inspection studio');world.use_nodes=True;world.node_tree.nodes.get('Background').inputs[0].default_value=(.12,.12,.12,1);scene.world=world
        for at, power, size in ((centre+Vector((-height,-height, height)),1500,height), (centre+Vector((height,-height*.5,height*.2)),800,height)):
            data=bpy.data.lights.new('Source inspection area','AREA');data.energy=power;data.shape='DISK';data.size=size
            light=bpy.data.objects.new(data.name,data);scene.collection.objects.link(light);light.location=at;light.rotation_euler=(centre-at).to_track_quat('-Z','Y').to_euler()
        bpy.context.view_layer.update()
        record=dict(key=key, source=path.relative_to(args.resource_project).as_posix(), bounds=dict(min=list(lo),max=list(hi)),
            meshes=[dict(name=obj.name,vertices=len(obj.data.vertices),matrix=[list(row) for row in obj.matrix_world]) for obj in meshes])
        if key in EYE_PIXELS: record['eyeAnchors']=[source_eye_uv(scene,meshes,pixel) for pixel in EYE_PIXELS[key]]
        records.append(record)
        if args.preview_dir:
            args.preview_dir.mkdir(parents=True,exist_ok=True)
            scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=800;scene.render.resolution_y=800;scene.render.resolution_percentage=100
            scene.render.filepath=str(args.preview_dir/(key+'-head-front.png'));bpy.ops.render.render(write_still=True)
        print('STATIC_FACE_INSPECTED',key,flush=True)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(dict(blender=bpy.app.version_string,assets=records,
        scope='Read-only existing authored static model faces; no source scene or asset is saved.'),indent=2),encoding='utf-8')


if __name__=='__main__': main()
