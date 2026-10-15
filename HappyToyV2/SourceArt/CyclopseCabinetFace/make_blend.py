"""Blender 4.0.2 editable facial finishing, after Unity import/preview.
Run from HappyToyV2 with --background --factory-startup --python this script.
"""
from pathlib import Path
import json
import bpy
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
folder=root/'Builds/CyclopseCabinetFace/authoring'
face=json.loads((folder/'face.json').read_text())
mouth=json.loads((folder/'mouth.json').read_text())
vec=lambda p:(p['x'],p['y'],p['z'])
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
def obj(name,vs,triangles):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vs,[],[triangles[i:i+3] for i in range(0,len(triangles),3)]);mesh.update()
    ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob)
    for p in mesh.polygons:p.use_smooth=True
    return ob
skin=obj('Existing webapp Cyclopse - cabinet facial finishing',[vec(p) for p in face['neutral']],face['triangles'])
skin.hide_render=True;skin.hide_viewport=True
skin.shape_key_add(name='Basis');key=skin.shape_key_add(name='Cabinet Gape')
for point,p in zip(key.data,face['opened']):point.co=vec(p)
uv=skin.data.uv_layers.new(name='Original Albedo UV')
for poly in skin.data.polygons:
    for li in poly.loop_indices:
        p=face['uv'][skin.data.loops[li].vertex_index];uv.data[li].uv=(p['x'],p['y'])
mat=bpy.data.materials.new('Original Cyclopse albedo - packed');mat.use_nodes=True
image=bpy.data.images.load(str(root/'Assets/Resources/EnemyStudio/Cyclopse/albedo.png'));image.pack()
nodes=mat.node_tree.nodes;tex=nodes.new('ShaderNodeTexImage');tex.image=image
mat.node_tree.links.new(tex.outputs['Color'],nodes.get('Principled BSDF').inputs['Base Color']);skin.data.materials.append(mat)
centre=Vector(vec(face['mouthCentre']))
names=['Ivory enamel','Stained enamel','Gum rim','Recessed mouth cavity','Tongue']
colors=[(.65,.51,.29,1),(.42,.29,.13,1),(.15,.036,.04,1),(.009,.0018,.003,1),(.19,.045,.065,1)]
for part,name,color in zip(mouth['parts'],names,colors):
    ob=obj(name,[Vector(vec(p))+centre for p in mouth['vertices']],part['triangles'])
    layer=ob.data.uv_layers.new(name='Enamel detail UV')
    for poly in ob.data.polygons:
        for li in poly.loop_indices:
            q=mouth['uv'][ob.data.loops[li].vertex_index];layer.data[li].uv=(q['x'],q['y'])
    # Remove vertices belonging to the other material parts, retaining editable
    # tooth/gum/cavity topology without duplicating unused authoring geometry.
    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='DESELECT');bpy.ops.mesh.select_loose();bpy.ops.mesh.delete(type='VERT');bpy.ops.object.mode_set(mode='OBJECT');ob.select_set(False)
    m=bpy.data.materials.new(name);m.diffuse_color=color;m.use_nodes=True;m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=color
    if 'enamel' in name.lower():
        tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(root/'SourceArt/CyclopseExpression/chase-face-generated-original.png'));tex.image.pack();m.node_tree.links.new(tex.outputs['Color'],m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    ob.data.materials.append(m)

patch=json.loads((folder/'patch.json').read_text())
ob=obj('Lower face resurfacing - editable gape',[Vector(vec(p))+centre for p in patch['neutral']],patch['triangles'])
ob.shape_key_add(name='Basis');gape=ob.shape_key_add(name='Cabinet Gape')
for point,p in zip(gape.data,patch['opened']):point.co=Vector(vec(p))+centre
gape.value=1
uv=ob.data.uv_layers.new(name='Generated chase projection');mouthuv=ob.data.uv_layers.new(name='Mouth aperture')
for poly in ob.data.polygons:
    for li in poly.loop_indices:
        vi=ob.data.loops[li].vertex_index;p=patch['uv'][vi];q=patch['mouthUV'][vi]
        uv.data[li].uv=(p['x'],p['y']);mouthuv.data[li].uv=(q['x'],q['y'])
m=bpy.data.materials.new('Approved chase face with real mouth aperture');m.use_nodes=True;m.blend_method='CLIP';m.use_screen_refraction=True
n=m.node_tree.nodes;links=m.node_tree.links;bsdf=n.get('Principled BSDF');tex=n.new('ShaderNodeTexImage')
tex.image=bpy.data.images.load(str(root/'SourceArt/CyclopseCabinetFace/closed-lips-generated-original.png'));tex.image.pack()
uvnode=n.new('ShaderNodeUVMap');uvnode.uv_map='Generated chase projection';links.new(uvnode.outputs['UV'],tex.inputs['Vector']);links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
mouthnode=n.new('ShaderNodeUVMap');mouthnode.uv_map='Mouth aperture';length=n.new('ShaderNodeVectorMath');length.operation='LENGTH';links.new(mouthnode.outputs['UV'],length.inputs[0])
cut=n.new('ShaderNodeMath');cut.operation='GREATER_THAN';cut.inputs[1].default_value=1;links.new(length.outputs['Value'],cut.inputs[0]);links.new(cut.outputs[0],bsdf.inputs['Alpha'])
ob.data.materials.append(m)

text=bpy.data.texts.new('README - reproducible facial source');text.write('Original creature source and rig remain in SourceArt/CreatureStudio/Cyclopse. This file provides editable neutral/gape shape key and separate anatomical mouth components. Unity importer uses gape-profile.json plus the existing chase sculpt; see README.md for regeneration. Axes here deliberately match Unity authoring JSON (Y up).\n')
key.value=1
bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).parent/'Cyclopse_CabinetFace_Editable.blend'))
print('CYCLOPSE_CABINET_BLEND_PASS')
