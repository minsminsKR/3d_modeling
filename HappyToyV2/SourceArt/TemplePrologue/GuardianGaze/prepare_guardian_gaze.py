"""Rebuild the prologue-only head rig from the preserved corridor LODs.

Blender 4.0.2: blender -b --factory-startup --python-exit-code 1 --python
HappyToyV2/SourceArt/TemplePrologue/GuardianGaze/prepare_guardian_gaze.py
Original model, textures and corridor prefabs are not modified.
"""
from pathlib import Path
import bpy

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[2]
ORIGINAL = PROJECT / 'SourceArt/CorridorEntrance/guardian-statue/guardian-statue.blend'
OUTPUT = PROJECT / 'Assets/Resources/TemplePrologue'
TEXTURE = PROJECT / 'Assets/Resources/CorridorEntrance/guardian-statue/CE_guardian-statue_0-baseColor.png'

bpy.ops.wm.open_mainfile(filepath=str(ORIGINAL))
keep = [bpy.data.objects['CE_guardian-statue_LOD0'], bpy.data.objects['CE_guardian-statue_LOD1']]
for obj in list(bpy.data.objects):
    if obj not in keep: bpy.data.objects.remove(obj, do_unlink=True)
# The editable rig references the already preserved game texture; no external
# path, packed workstation dependency or duplicate generated original is needed.
mat = bpy.data.materials.new('Existing guardian stone'); mat.use_nodes = True
shader = mat.node_tree.nodes.get('Principled BSDF')
image = bpy.data.images.load(str(TEXTURE), check_existing=False)
image.filepath = bpy.path.relpath(str(TEXTURE), start=str(HERE))
node = mat.node_tree.nodes.new('ShaderNodeTexImage'); node.image = image
mat.node_tree.links.new(node.outputs['Color'], shader.inputs['Base Color'])
for obj in keep:
    obj.hide_set(False); obj.hide_viewport = False; obj.hide_render = False
    obj.data.materials.clear(); obj.data.materials.append(mat)

bpy.ops.object.select_all(action='DESELECT')
bpy.ops.object.armature_add(); arm = bpy.context.object; arm.name = 'Guardian gaze rig'
bpy.ops.object.mode_set(mode='EDIT')
body = arm.data.edit_bones[0]; body.name = 'Body'; body.head = (0,0,0); body.tail = (0,0,1)
head = arm.data.edit_bones.new('GuardianHead'); head.head = (0,0,2.25); head.tail = (0,0,2.65); head.parent = body
bpy.ops.object.mode_set(mode='OBJECT')

def smooth(a,b,v):
    t=max(0,min(1,(v-a)/(b-a)))
    return t*t*(3-2*t)

def staff_vertices(mesh):
    # Above the lower neck, the forward-held staff separates topologically
    # from the head. Keep that connected island rigid with the body.
    active={v.index for v in mesh.vertices if v.co.z>=2.20}
    parents={i:i for i in active}
    def find(i):
        while parents[i]!=i:
            parents[i]=parents[parents[i]]; i=parents[i]
        return i
    for edge in mesh.edges:
        a,b=edge.vertices
        if a in active and b in active:
            ra,rb=find(a),find(b)
            if ra!=rb: parents[rb]=ra
    groups={}
    for i in active: groups.setdefault(find(i),[]).append(i)
    protected=set()
    for ids in groups.values():
        points=[mesh.vertices[i].co for i in ids]
        if len(ids)>=12 and min(p.x for p in points)>.10 and max(p.y for p in points)<-.27 and max(p.z for p in points)<2.45:
            protected.update(ids)
    if not protected: raise RuntimeError('Cannot identify the raised staff on '+mesh.name)
    return protected

for obj in keep:
    protected=staff_vertices(obj.data)
    body_group=obj.vertex_groups.new(name='Body'); head_group=obj.vertex_groups.new(name='GuardianHead')
    for vertex in obj.data.vertices:
        weight=0 if vertex.index in protected else smooth(2.20,2.38,vertex.co.z)
        if weight>0.00001: head_group.add([vertex.index],weight,'REPLACE')
        if weight<.99999: body_group.add([vertex.index],1-weight,'REPLACE')
    obj.parent=arm
    modifier=obj.modifiers.new('Head and rigid body','ARMATURE'); modifier.object=arm
    print(obj.name, 'protected staff vertices:',len(protected))

bpy.ops.outliner.orphans_purge(do_recursive=True)
OUTPUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE / 'guardian-gaze.blend'), compress=True)
for i,obj in enumerate(keep):
    bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); obj.select_set(True)
    bpy.context.view_layer.objects.active=arm
    bpy.ops.export_scene.fbx(filepath=str(OUTPUT / ('guardian-gaze-lod%d.fbx'%i)),
        use_selection=True, object_types={'MESH','ARMATURE'}, axis_forward='-Z', axis_up='Y',
        bake_anim=False, add_leaf_bones=False, path_mode='RELATIVE', use_mesh_modifiers=True)
print('GUARDIAN_GAZE_AUTHORING_PASS')
