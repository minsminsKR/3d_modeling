"""Repair only the generated leaf copy at3mm; retain recessed wired glass."""
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def preprocess(game):
    bpy.ops.object.select_all(action='DESELECT');game.select_set(True)
    bpy.context.view_layer.objects.active=game
    before=len(game.data.polygons)
    game.data.remesh_voxel_size=.003;game.data.remesh_voxel_adaptivity=0
    game.data.use_remesh_preserve_volume=True;bpy.ops.object.voxel_remesh();game.data.update()
    points=[Vector(p) for p in game.bound_box]
    size=[max(v[a] for v in points)-min(v[a] for v in points) for a in range(3)]
    if not (1.0<size[0]<1.1 and .30<size[1]<.40 and 2.40<size[2]<2.48):
        raise RuntimeError('Generated door leaf/hardware silhouette changed during surface repair')
    tree=BVHTree.FromObject(game,bpy.context.evaluated_depsgraph_get())
    probes=[]
    for x,z in ((0,.4),(0,1.2),(0,2.25),(-.25,1.75)):
        hit=tree.ray_cast(Vector((x,.5,z)),Vector((0,-1,0)),1)[0]
        if hit is None:raise RuntimeError('Generated closed door lost a physical surface '+str((x,z)))
        probes.append(dict(x=x,height=z,surfaceY=hit.y))
    return dict(tool='Source-derived3mm voxel surface repair before LOD/PBR bake',
                originalUnchanged=True,voxelMetres=.003,preserveVolume=True,
                sourceCopyFacesBefore=before,sourceCopyFacesAfter=len(game.data.polygons),
                dimensionsMetres=size,closedLeafSurfaceRays=probes,
                glazing='Original opaque dirty wired glass appearance retained for the inspect-only locked door',
                frame='This generated model is a bare leaf with hardware, not an architectural jamb/frame')
