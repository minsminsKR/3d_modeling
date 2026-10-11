"""Quiet material previews of a newly generated source; no source file is saved."""
import argparse
import json
import sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('input', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--size', type=int, default=1200)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    args.output.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(args.input), merge_vertices=False)
    values = []
    for obj in bpy.context.scene.objects:
        if obj.type != 'MESH':
            continue
        coords = np.empty(len(obj.data.vertices)*3, dtype=np.float32)
        obj.data.vertices.foreach_get('co', coords)
        matrix = np.asarray(obj.matrix_world)
        values.append(coords.reshape(-1, 3)@matrix[:3, :3].T+matrix[:3, 3])
    values = np.concatenate(values)
    low, high = values.min(0), values.max(0)
    center = Vector((low+high)/2)
    height = high[2]-low[2]
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.eevee.use_gtao = True
    scene.eevee.gtao_distance = height*.1
    scene.eevee.taa_render_samples = 32
    scene.render.resolution_x = scene.render.resolution_y = args.size
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'Standard'
    if scene.world is None:
        scene.world = bpy.data.worlds.new('Source Preview World')
    scene.world.color = (.15, .15, .15)
    for name, offset, power in [('Key', (1.3, -2, 2), 100), ('Fill', (-1.4, -1, 1), 80), ('Rim', (0, 2, 2), 100)]:
        data = bpy.data.lights.new(name, 'AREA')
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = center+Vector(offset)*height
        obj.rotation_euler = (center-obj.location).to_track_quat('-Z', 'Y').to_euler()
        data.energy = power*height*height
        data.size = height*2
    data = bpy.data.cameras.new('Preview camera')
    camera = bpy.data.objects.new('Preview camera', data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    data.type = 'ORTHO'
    outputs = []
    for name, offset, target, scale in [
        ('front', (0, -3, 0), center, height*1.1),
        ('threequarter', (.9, -2, .6), center, height*1.15),
        ('side', (-3, 0, 0), center, height*1.1),
        ('face', (0, -3, .30), center+Vector((0, 0, height*.30)), height*.45)]:
        camera.location = center+Vector(offset)*height
        camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
        data.ortho_scale = scale
        path = args.output/(name+'.png')
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        outputs.append(str(path))
    (args.output/'geometry-preview.json').write_text(json.dumps({'input': args.input.name,
        'blenderBounds': [low.tolist(), high.tolist()], 'glTFConversion': '(x,-z,y)',
        'outputs': outputs}, indent=2), encoding='utf8')


if __name__ == '__main__':
    main()
