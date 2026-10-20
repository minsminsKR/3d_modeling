"""Rebuild only Lady's distant game LOD from her retained generated surface."""
import json
from pathlib import Path
import sys

import bpy
import numpy as np

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[1]
sys.path.insert(0,str(HERE))
from polish_game import unwrap_game,remove_loose,rotation_digest
import author_lady as author
import bake_surface_atlas as atlas
import author_creature as shared
from refine_game import finish


def run():
    settings=json.loads((HERE/'polish-settings.json').read_text())
    scene=bpy.context.scene;scene.frame_set(1)
    rig=bpy.data.objects['Lady_Rig'];fit=bpy.data.objects['Lady_Fit']
    high=bpy.data.objects['Lady_SourceHigh'];first=bpy.data.objects['Lady_LOD0']
    digest=rotation_digest(rig.animation_data.action)
    rig.data.pose_position='REST';bpy.context.view_layer.update()
    bpy.data.objects.remove(bpy.data.objects['Lady_LOD2'],do_unlink=True)
    far=first.copy();far.data=first.data.copy();bpy.context.scene.collection.objects.link(far)
    far.name='Lady_LOD2';far.data.name='Lady distant remeshed surface'
    for modifier in list(far.modifiers):far.modifiers.remove(modifier)
    for group in list(far.vertex_groups):far.vertex_groups.remove(group)
    bpy.ops.object.select_all(action='DESELECT');far.hide_set(False);far.select_set(True)
    bpy.context.view_layer.objects.active=far
    far.data.remesh_voxel_size=settings['far_voxel_size_metres']/abs(far.matrix_world.to_scale().x)
    far.data.remesh_voxel_adaptivity=0
    with bpy.context.temp_override(object=far,active_object=far,selected_objects=[far],selected_editable_objects=[far]):
        bpy.ops.object.voxel_remesh()
        far.data.calc_loop_triangles()
        decimate=far.modifiers.new('Distant silhouette budget','DECIMATE')
        decimate.ratio=min(1,settings['lod2_triangles']/len(far.data.loop_triangles))
        decimate.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=decimate.name)
    if not far.data.uv_layers:far.data.uv_layers.new(name='Far atlas')
    modifier=far.modifiers.new('Original generated Lady rig','ARMATURE');modifier.object=rig
    finish(high,far,rig,repaint=False,write_report=False)
    remove_loose(far);unwrap_game(far,settings)
    output=PROJECT/'Assets/Resources/Lady/Far';output.mkdir(parents=True,exist_ok=True)
    report=atlas.bake(high,far,output,settings['far_atlas_size'])
    image=bpy.data.images.load(str(output/'game-orm.png'),check_existing=False)
    image.colorspace_settings.name='Non-Color';pixels=np.empty(len(image.pixels),dtype='f4');image.pixels.foreach_get(pixels)
    pixels.reshape(-1,4)[:,2]=0;image.pixels.foreach_set(pixels);shared.save_image_atomic(image,output/'game-orm.png')
    for material in far.data.materials:
        nodes=material.node_tree.nodes;links=material.node_tree.links
        for node in nodes:
            if node.type=='TEX_IMAGE' and node.image and Path(bpy.path.abspath(node.image.filepath)).resolve()==(output/'game-orm.png').resolve():
                node.image.pixels.foreach_set(pixels)
        bsdf=next(node for node in nodes if node.type=='BSDF_PRINCIPLED')
        for link in list(bsdf.inputs['Metallic'].links):links.remove(link)
        bsdf.inputs['Metallic'].default_value=0
        texture=nodes.new('ShaderNodeTexImage');texture.image=bpy.data.images.load(str(output/'normal.png'),check_existing=True)
        texture.image.colorspace_settings.name='Non-Color'
        normal=nodes.new('ShaderNodeNormalMap');links.new(texture.outputs['Color'],normal.inputs['Color']);links.new(normal.outputs['Normal'],bsdf.inputs['Normal'])
    rig.data.pose_position='POSE';scene.frame_set(1)
    if rotation_digest(rig.animation_data.action)!=digest:raise RuntimeError('Horror rotation keys changed')
    lods=[first,bpy.data.objects['Lady_LOD1'],far]
    report.update(method=report['method']+'; 2cm distant-only remesh fills sub-pixel tunnels',
                  vertices=len(far.data.vertices),triangles=len(far.data.polygons),originalRigPreserved=True)
    if report['triangles']>20000:raise RuntimeError('Far mesh budget did not improve')
    (HERE/'far-surface-report.json').write_text(json.dumps(report,indent=2)+'\n')
    summary=json.loads((HERE/'authoring-report.json').read_text())
    summary['lods']=[dict(name=obj.name,vertices=len(obj.data.vertices),triangles=len(obj.data.polygons)) for obj in lods]
    summary['polish']['farSurfaceReport']='far-surface-report.json'
    (HERE/'authoring-report.json').write_text(json.dumps(summary,indent=2)+'\n')
    for obj in lods[1:]:obj.hide_render=True;obj.hide_set(True)
    bpy.ops.file.pack_all();bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Lady_Editable.blend'))
    author.export_game(fit,rig,lods)
    print('LADY_FAR_POLISH_PASS '+json.dumps(summary['lods']),flush=True)


if __name__=='__main__':run()
