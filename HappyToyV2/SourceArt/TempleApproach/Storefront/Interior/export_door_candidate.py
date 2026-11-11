"""Export a scratch doorway candidate with the production FBX settings.

Input: Storefront-door-candidate.blend produced by cut_door_candidate.py.
Output: this work folder only. Never overwrites any tracked Unity or authoring
asset. Markers require verification after Unity import; no gameplay claim.
"""
from pathlib import Path
import bpy
from mathutils import Matrix, Vector

work = Path(__file__).resolve().parent
holder = bpy.data.objects['Old Korean neighborhood storefront']
for name, point in {
    'ShopDoorLeftFoot': (-.275, -.242, 0),
    'ShopDoorRightFoot': (-.028, -.242, 0),
    'ShopDoorTop': (-.1515, -.242, .465),
    'ShopInteriorRear': (-.1515, .303, 0),
}.items():
    marker = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(marker)
    marker.parent = holder
    marker.matrix_world = Matrix.Translation(Vector(point))
    marker.empty_display_size = .02

bpy.context.view_layer.update()
for name in ('ShopDoorLeftFoot', 'ShopDoorRightFoot', 'ShopDoorTop', 'ShopInteriorRear'):
    print('MARKER', name, tuple(round(v, 6) for v in bpy.data.objects[name].matrix_world.translation), flush=True)

bpy.ops.wm.save_as_mainfile(filepath=str(work / 'Storefront-door-candidate-with-markers.blend'))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(work / 'Storefront-door-candidate.fbx'), use_selection=True,
    add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', path_mode='COPY', embed_textures=True)
