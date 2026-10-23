"""Repair only the generated shoe-cubby copy at3mm, retaining16 real cavities."""
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def preprocess(game):
    bpy.ops.object.select_all(action='DESELECT');game.select_set(True)
    bpy.context.view_layer.objects.active=game
    before=len(game.data.polygons)
    game.data.remesh_voxel_size=.003
    game.data.remesh_voxel_adaptivity=0
    game.data.use_remesh_preserve_volume=True
    bpy.ops.object.voxel_remesh();game.data.update()
    tree=BVHTree.FromObject(game,bpy.context.evaluated_depsgraph_get())
    openings=[]
    for column,x in enumerate((-.575,-.187,.187,.575)):
        for row,z in enumerate((.145,.40,.65,.91)):
            origin=Vector((x,.7,z))
            front=tree.ray_cast(origin,Vector((0,-1,0)),.7)[0]
            if front is not None:
                raise RuntimeError('Generated shoe cubby opening filled at '+str((column,row)))
            back=tree.ray_cast(origin,Vector((0,-1,0)),1.1)[0]
            if back is None or not -.25<back.y<-.01:
                raise RuntimeError('Generated shoe cubby back panel lost at '+str((column,row)))
            openings.append(dict(column=column,row=row,x=x,height=z,backY=back.y))
    return dict(tool='Source-derived3mm voxel retopology before collapse/bake',
                originalUnchanged=True,voxelMetres=.003,preserveVolume=True,
                sourceCopyFacesBefore=before,sourceCopyFacesAfter=len(game.data.polygons),
                cavityRays=openings,openFront='Blender+Y after180degree Z normalization')
