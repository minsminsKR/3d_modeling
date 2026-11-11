"""Compare scratch doorway FBX against the currently imported game FBX."""
from pathlib import Path
import bpy
from mathutils import Vector

work = Path(__file__).resolve().parent
repo = next(p for p in (work, *work.parents) if (p / 'HappyToyV2/Assets').is_dir())
paths = [repo / 'HappyToyV2/Assets/Resources/AlleyStorefront/geometry.fbx',
         work / 'Storefront-door-candidate.fbx']
for path in paths:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    bpy.context.view_layer.update()
    objects=[o for o in bpy.context.scene.objects if o.type == 'MESH']
    points=[o.matrix_world @ Vector(c) for o in objects for c in o.bound_box]
    lo=[min(p[i] for p in points) for i in range(3)]
    hi=[max(p[i] for p in points) for i in range(3)]
    print('FBX', path.name, 'bounds', [round(x,7) for x in lo], [round(x,7) for x in hi],
          'faces',sum(len(o.data.polygons) for o in objects),
          'verts',sum(len(o.data.vertices) for o in objects),
          'uv',[(o.name,[u.name for u in o.data.uv_layers]) for o in objects],flush=True)
    for o in bpy.context.scene.objects:
        if o.name.startswith('ShopDoor') or o.name == 'ShopInteriorRear':
            print('IMPORTED_MARKER',o.name,tuple(round(v,7) for v in o.matrix_world.translation),flush=True)
