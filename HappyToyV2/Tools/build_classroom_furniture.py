"""Original metric classroom furniture; run with Blender --background --python.

Blender Z is up. FBX export maps this to Unity Y. Origins sit on the floor.
No external assets or textures are used.
"""
import bpy
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Assets/Art/Finished'
SOURCE = ROOT / 'SourceArt'


def material(name, color, metal=0, rough=.7):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    node = mat.node_tree.nodes.get('Principled BSDF')
    node.inputs['Base Color'].default_value = (*color, 1)
    node.inputs['Metallic'].default_value = metal
    node.inputs['Roughness'].default_value = rough
    return mat


def box(name, position, size, mat, radius=.006):
    bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new('Soft manufactured edges', 'BEVEL')
    bevel.width = min(radius, min(size) * .3)
    bevel.segments = 3
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    return obj


def build(kind):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    oak = material('Warm aged plywood', (.27, .16, .075))
    edge = material('Exposed plywood edge', (.40, .27, .13))
    steel = material('Desaturated school enamel', (.105, .145, .135), .35, .6)
    rubber = material('Dark rubber feet', (.018, .022, .02), 0, .92)
    if kind == 'teacher-desk':
        box('Teacher desktop rim', (0, 0, .737), (1.4, .7, .036), edge, .012)
        box('Teacher writing surface', (0, 0, .7575), (1.388, .688, .005), oak, .002)
        for x in [-.635, .635]:
            for y in [-.28, .28]:
                box('Teacher desk leg', (x, y, .35), (.05, .05, .70), steel)
                box('Teacher desk foot', (x, y, .02), (.065, .065, .04), rubber)
        box('Rear modesty panel', (0, .26, .48), (1.23, .024, .42), oak)
        box('Drawer cabinet', (.41, 0, .48), (.40, .55, .43), steel)
        for z in [.34, .475, .61]:
            box('Drawer front', (.41, -.286, z), (.382, .023, .12), oak)
            for x in [.35, .47]:
                box('Drawer handle mount', (x, -.309, z), (.014, .027, .018), steel)
            box('Drawer pull', (.41, -.327, z), (.14, .016, .018), steel)
        box('Knee-space apron', (-.22, -.27, .68), (.83, .026, .075), oak)
    elif kind == 'hiding-cabinet':
        paint = material('Faded cabinet enamel', (.26, .31, .27), .15, .72)
        inset = material('Recessed cabinet panels', (.18, .23, .20), .15, .78)
        handle = material('Dull cabinet handles', (.35, .37, .32), .65, .48)
        for x in [-.55, .55]:
            box('Cabinet side panel', (x, 0, 1), (.08, .8, 2), paint, .014)
        box('Cabinet back panel', (0, .4, 1), (1.1, .08, 2), paint)
        for z in [.045, 1.955]:
            box('Cabinet end panel', (0, 0, z), (1.1, .8, .09), paint)
        for x in [-.272, .272]:
            box('Cabinet door', (x, -.402, 1), (.535, .07, 1.91), paint, .009)
            box('Recessed door field', (x, -.441, 1), (.44, .009, 1.62), inset)
            for z in [.30, .355, .41, 1.59, 1.645, 1.70]:
                box('Dark ventilation slot', (x, -.448, z), (.28, .006, .015), rubber, .002)
            gripx=x+(.17 if x<0 else -.17)
            for z in [.88, 1.03]:
                box('Handle bracket', (gripx, -.465, z), (.025, .05, .025), handle)
            box('Pull handle', (gripx, -.492, .955), (.025, .025, .18), handle)
            box('Label holder', (x, -.453, 1.36), (.17, .015, .075), handle)
            box('Old label', (x, -.463, 1.36), (.145, .005, .052), edge)
    elif kind == 'infirmary-bed':
        linen = material('Faded ivory cotton', (.58, .56, .45), 0, .93)
        blanket = material('Muted infirmary blanket', (.19, .29, .27), 0, .94)
        box('Steel mattress platform', (0, 0, .48), (.86, 1.96, .055), steel)
        box('Rounded mattress', (0, 0, .57), (.84, 1.93, .14), linen, .045)
        box('Folded blanket', (0, -.28, .653), (.85, 1.24, .036), blanket, .012)
        box('Pillow', (0, .66, .682), (.59, .39, .085), linen, .035)
        for x in [-.39, .39]:
            for y in [-.91, .91]:
                box('Bed post', (x, y, .46), (.036, .036, .88), steel)
                box('Bed foot', (x, y, .025), (.06, .06, .05), rubber)
        for y in [-.91, .91]:
            box('End rail', (0, y, .88), (.81, .035, .035), steel)
            for x in [-.23, 0, .23]:
                box('End rail spindle', (x, y, .74), (.02, .025, .26), steel)
    elif kind == 'classroom-desk':
        box('Layered plywood rim', (0, 0, .745), (1, .65, .035), edge, .012)
        box('Desk writing surface', (0, 0, .766), (.987, .637, .012), oak)
        # Open shelf, not a solid block underneath the writing surface.
        box('Book shelf', (0, .02, .57), (.82, .43, .018), steel)
        box('Shelf rear lip', (0, .23, .615), (.82, .018, .10), steel)
        for x in [-.42, .42]:
            box('Side top rail', (x, 0, .70), (.03, .52, .035), steel)
            for y in [-.25, .25]:
                box('Square steel leg', (x, y, .36), (.032, .032, .70), steel)
                box('Protective foot', (x, y, .018), (.045, .045, .036), rubber)
        box('Rear stabilizer', (0, .25, .22), (.86, .026, .026), steel)
    else:
        box('Seat plywood edge', (0, 0, .445), (.45, .45, .027), edge, .012)
        box('Seat face', (0, 0, .461), (.438, .438, .008), oak)
        box('Backrest plywood edge', (0, .20, .745), (.45, .028, .29), edge, .012)
        box('Backrest face', (0, .182, .745), (.435, .009, .274), oak)
        for x in [-.18, .18]:
            box('Backrest upright', (x, .205, .62), (.025, .025, .50), steel)
            box('Seat rail', (x, 0, .41), (.027, .40, .027), steel)
            for y in [-.18, .18]:
                box('Chair steel leg', (x, y, .21), (.027, .027, .40), steel)
                box('Chair rubber foot', (x, y, .015), (.04, .04, .03), rubber)
        box('Lower back stretcher', (0, .18, .18), (.37, .025, .025), steel)
    OUT.mkdir(parents=True, exist_ok=True)
    SOURCE.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / (kind + '.blend')))
    bpy.ops.export_scene.fbx(filepath=str(OUT / (kind + '.fbx')),
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS', add_leaf_bones=False,
        object_types={'MESH'})
    records = []
    used_materials = {mat for obj in bpy.context.scene.objects if obj.type == 'MESH' for mat in obj.data.materials}
    for mat in sorted(used_materials, key=lambda item: item.name):
        node = mat.node_tree.nodes.get('Principled BSDF')
        records.append(dict(name=mat.name, color=list(mat.diffuse_color),
            metallic=node.inputs['Metallic'].default_value,
            roughness=node.inputs['Roughness'].default_value))
    (OUT / (kind + '.materials.json')).write_text(
        json.dumps({'materials': records}, indent=2), encoding='utf-8')
    print('CLASSROOM_MODEL_READY', kind)


import sys
requested = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
for key in requested or ['classroom-desk', 'classroom-chair', 'infirmary-bed']:
    if key not in ['classroom-desk', 'classroom-chair', 'infirmary-bed', 'hiding-cabinet', 'teacher-desk']:
        raise ValueError('Unknown furniture: ' + key)
    build(key)
