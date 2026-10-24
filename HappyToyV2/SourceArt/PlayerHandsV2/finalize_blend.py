"""Connect final game PBR maps in the editable Blender source after finish_skin.py."""
from pathlib import Path
import bpy,json,hashlib
ROOT=Path(__file__).resolve().parent
OUT=ROOT.parents[1]/'Assets/Resources/PlayerItems/hand'
obj=next(o for o in bpy.context.scene.objects if o.type=='MESH' and o.name=='hand')
mat=obj.data.materials[0];nodes=mat.node_tree.nodes;links=mat.node_tree.links
bsdf=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
for filename in ('skin-normal.png','metalrough.png'):
    image=bpy.data.images.load(str(OUT/filename),check_existing=False)
    image.colorspace_settings.name='Non-Color'
    texture=nodes.new('ShaderNodeTexImage');texture.image=image
    if filename=='skin-normal.png':
        normal=nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.55
        links.new(texture.outputs['Color'],normal.inputs['Color']);links.new(normal.outputs[0],bsdf.inputs['Normal'])
    else:
        channels=nodes.new('ShaderNodeSeparateColor');links.new(texture.outputs['Color'],channels.inputs[0])
        links.new(channels.outputs['Green'],bsdf.inputs['Roughness'])
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'hand.blend'))
report=json.loads((OUT/'asset.json').read_text(encoding='utf-8'))
report['outputSha256']['packedBlend']=hashlib.sha256((ROOT/'hand.blend').read_bytes()).hexdigest()
for path in (OUT/'asset.json',ROOT/'finish-report.json'):path.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('HAND_PACKED_PBR_PASS')
