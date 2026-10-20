"""CPU Cycles preview of the actual edited Lady, with no game/player/UI input.

Run Blender --background --threads 2 --python this.py -- --source ... --output ...
"""
import argparse
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--frames',type=int,default=64)
    parser.add_argument('--samples',type=int,default=16)
    parser.add_argument('--width',type=int,default=432)
    parser.add_argument('--height',type=int,default=640)
    parser.add_argument('--start-frame',type=int,default=13)
    parser.add_argument('--lod',type=int,choices=(0,1,2),default=0)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    bpy.ops.wm.open_mainfile(filepath=str(args.source.resolve()),load_ui=False)
    scene=bpy.context.scene
    model_name='Lady_LOD'+str(args.lod)
    for obj in scene.objects:
        if obj.type=='MESH':obj.hide_render=obj.name!=model_name
        elif obj.type=='LIGHT':obj.hide_render=True
    model=bpy.data.objects[model_name];model.hide_set(False);model.hide_render=False
    # The pre-polish editable file stored its normal map without a shader link;
    # Unity did use that map. Link the retained packed map for a fair comparison.
    for material in model.data.materials:
        nodes=material.node_tree.nodes;links=material.node_tree.links
        bsdf=next(node for node in nodes if node.type=='BSDF_PRINCIPLED')
        if not bsdf.inputs['Normal'].is_linked:
            normal_image=next((image for image in bpy.data.images if Path(bpy.path.abspath(image.filepath)).name=='normal.png'),None)
            if normal_image:
                normal_image.colorspace_settings.name='Non-Color'
                texture=nodes.new('ShaderNodeTexImage');texture.image=normal_image
                normal=nodes.new('ShaderNodeNormalMap');links.new(texture.outputs['Color'],normal.inputs['Color']);links.new(normal.outputs['Normal'],bsdf.inputs['Normal'])
    rig=bpy.data.objects['Lady_Rig'];rig.data.pose_position='POSE'
    for modifier in model.modifiers:
        if modifier.type=='ARMATURE':modifier.show_render=True
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=args.samples
    scene.cycles.use_denoising=True;scene.cycles.denoiser='OPENIMAGEDENOISE';scene.cycles.max_bounces=2
    scene.cycles.use_adaptive_sampling=True;scene.cycles.adaptive_threshold=.12;scene.cycles.adaptive_min_samples=4
    scene.cycles.diffuse_bounces=1;scene.cycles.glossy_bounces=1
    scene.render.threads_mode='FIXED';scene.render.threads=2
    scene.render.resolution_x=args.width;scene.render.resolution_y=args.height;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGB'
    scene.render.film_transparent=False;scene.render.use_persistent_data=True
    scene.world.use_nodes=True
    background=next(node for node in scene.world.node_tree.nodes if node.type=='BACKGROUND')
    background.inputs['Color'].default_value=(.055,.065,.08,1);background.inputs['Strength'].default_value=.25
    scene.view_settings.view_transform='Standard';scene.view_settings.look='Medium High Contrast'
    def aim(obj,point):obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()
    camera_data=bpy.data.cameras.new('CPU motion camera');camera=bpy.data.objects.new(camera_data.name,camera_data)
    scene.collection.objects.link(camera);scene.camera=camera;camera_data.type='ORTHO';camera_data.ortho_scale=2.72
    camera.location=(.65,-4,1.22);aim(camera,(0,0,1.05))
    for name,at,power,size,color in [('Key',(1,-3,3.2),280,2.4,(.9,.95,1)),('Rim',(-1.5,1.5,2.7),180,2,(.45,.62,1))]:
        data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;data.color=color
        light=bpy.data.objects.new(name,data);scene.collection.objects.link(light);light.location=at;aim(light,(0,0,1.1))
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,0))
    floor=bpy.context.object;floor.name='Temporary review floor'
    material=bpy.data.materials.new('CPU floor');material.use_nodes=True
    bsdf=next(node for node in material.node_tree.nodes if node.type=='BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value=(.028,.032,.04,1);bsdf.inputs['Roughness'].default_value=1
    floor.data.materials.append(material)
    args.output.mkdir(parents=True,exist_ok=True)
    for index in range(args.frames):
        # Every third real 60 Hz source frame: 20fps preview at true speed.
        frame=args.start_frame+index*3
        scene.frame_set(frame)
        scene.render.filepath=str(args.output/f'{index:03d}.png')
        bpy.ops.render.render(write_still=True)
        print(f'LADY_CPU_FRAME {index+1}/{args.frames}',flush=True)
    (args.output/'render.json').write_text(json.dumps(dict(source=str(args.source),frames=args.frames,
        sourceFrames=[args.start_frame,args.start_frame+(args.frames-1)*3],fps=20,engine='Cycles CPU',samples=args.samples,
        lod=args.lod,scope='Actual editable game LOD and authored action, review lighting; no live gameplay/FPS claim'),indent=2))


if __name__=='__main__':main()
