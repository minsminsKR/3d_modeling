"""Fine 1.5mm source-derived surface repair, preserving both generated tubes."""
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def preprocess(game):
    bpy.ops.object.select_all(action='DESELECT')
    game.select_set(True)
    bpy.context.view_layer.objects.active=game
    before=len(game.data.polygons)
    game.data.remesh_voxel_size=.0015
    game.data.remesh_voxel_adaptivity=0
    game.data.use_remesh_preserve_volume=True
    bpy.ops.object.voxel_remesh()
    game.data.update()
    tree=BVHTree.FromObject(game,bpy.context.evaluated_depsgraph_get())
    tubes=[]
    for y in (.022,-.054):
        hit=tree.ray_cast(Vector((0,y,-.1)),Vector((0,0,1)),.3)[0]
        if hit is None or hit.z>.075:
            raise RuntimeError('Source-derived retopology lost a physical fluorescent tube')
        tubes.append(dict(y=y,undersideZ=hit.z))
    gap=tree.ray_cast(Vector((0,-.015,-.1)),Vector((0,0,1)),.3)[0]
    if gap is None or gap.z<max(t['undersideZ'] for t in tubes)+.006:
        raise RuntimeError('Physical generated tubes were fused during surface repair')
    return dict(tool='Source-derived 1.5mm voxel surface repair before LOD collapse/bake',
                originalUnchanged=True,voxelMetres=.0015,preserveVolume=True,
                sourceCopyFacesBefore=before,sourceCopyFacesAfter=len(game.data.polygons),
                tubeUndersideRays=tubes,separatingReflectorZ=gap.z,
                tubeGeometry='Both actual generated glass cylinders retained; no primitive replacement')
