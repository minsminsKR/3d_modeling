"""Inspect the existing ritual-floor FBX layers, without modifying assets.

Run with Blender 4.0.2 in background, e.g.:
  blender -b -t 2 --python-exit-code 1 --python HappyToyV2/Tools/quality/probe_temple_floor_geometry.py
The scale conversion in the report applies to the former 2 mm bed. This is
an asset-space sample, not a GPU depth-buffer or final-screen measurement.
"""
import json, math
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root=Path(__file__).resolve().parents[2] / 'Assets/Resources/CorridorEntrance'
# The Unity prefab bounds include both LOD transforms. Read LOD1 first so
# the LOD0 "bedUp" cutoff below uses that same union instead of LOD0 alone.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root / 'ritual-floor-lod1.fbx'))
far=[o for o in bpy.data.objects if o.type=='MESH']
if len(far)!=1: raise RuntimeError('Unexpected far mesh count')
far_heights=[(far[0].matrix_world @ v.co).z for v in far[0].data.vertices]
far_min,far_max=min(far_heights),max(far_heights)
for level in (0,1):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root / ('ritual-floor-lod%d.fbx' % level)))
    objects=[o for o in bpy.data.objects if o.type=='MESH']
    if len(objects)!=1: raise RuntimeError('Unexpected mesh count')
    obj=objects[0]; mesh=obj.data; mesh.calc_loop_triangles()
    verts=[obj.matrix_world @ v.co for v in mesh.vertices]
    faces=[tuple(t.vertices) for t in mesh.loop_triangles]
    lo=Vector(tuple(min(v[i] for v in verts) for i in range(3)))
    hi=Vector(tuple(max(v[i] for v in verts) for i in range(3)))
    counts={'up':0,'down':0,'side':0,'upperUp':0,'upperDown':0,'upperSide':0}
    mesh.calc_normals_split()
    up_loops=0; inverted_loops=0; horizontal_loops=0
    normal_matrix=obj.matrix_world.to_3x3().inverted().transposed()
    for tri in mesh.loop_triangles:
        a,b,c=(verts[i] for i in tri.vertices)
        geometric=(b-a).cross(c-a)
        if geometric.length < 1e-12: continue
        geometric.normalize()
        if geometric.z < .25: continue
        for loop_index in tri.loops:
            normal=normal_matrix @ mesh.loops[loop_index].normal
            if normal.length < 1e-8: continue
            normal.normalize();up_loops+=1
            if normal.dot(geometric) < 0: inverted_loops+=1
            if normal.z < .15:horizontal_loops+=1
    for a,b,c in faces:
        normal=(verts[b]-verts[a]).cross(verts[c]-verts[a]); length=normal.length
        if length<1e-12: continue
        vertical=normal.z/length; kind='up' if vertical>.25 else 'down' if vertical<-.25 else 'side'
        counts[kind]+=1
        if (verts[a].z+verts[b].z+verts[c].z)/3 > (lo.z+hi.z)*.5:
            counts['upper'+kind.capitalize()]+=1
    for condition in ('all','up','upperUp','lowerUp','bedUp'):
      selected=[]
      for a,b,c in faces:
        normal=(verts[b]-verts[a]).cross(verts[c]-verts[a]);length=normal.length
        if length<1e-12: continue
        if condition != 'all' and normal.z/length<=.25: continue
        if condition=='upperUp' and (verts[a].z+verts[b].z+verts[c].z)/3<=(lo.z+hi.z)*.5:continue
        if condition=='lowerUp' and (verts[a].z+verts[b].z+verts[c].z)/3>(lo.z+hi.z)*.5:continue
        if condition=='bedUp' and (verts[a].z+verts[b].z+verts[c].z)/3>(min(lo.z,far_min)+max(hi.z,far_max))*.5:continue
        selected.append((a,b,c))
      tree=BVHTree.FromPolygons(verts,selected,all_triangles=True)
      depths=[]; hit_counts=[]; limit=64
      step=max((hi.z-lo.z)*1e-6,1e-8)
      for iy in range(limit):
       for ix in range(limit):
        x=lo.x+(hi.x-lo.x)*(ix+.5)/limit; y=lo.y+(hi.y-lo.y)*(iy+.5)/limit
        position=Vector((x,y,hi.z+step*2)); hits=[]
        for k in range(32):
          point,normal,index,distance=tree.ray_cast(position,Vector((0,0,-1)))
          if point is None or point.z<lo.z-step: break
          hits.append(point.z);position=point-Vector((0,0,step))
        hit_counts.append(len(hits))
        for a,b in zip(hits,hits[1:]): depths.append((a-b)/(hi.z-lo.z)*.002)
      depths.sort()
      def percentile(p): return depths[min(len(depths)-1,round((len(depths)-1)*p))] if depths else None
      print('FLOOR_PROBE',json.dumps(dict(lod=level,condition=condition,triangles=len(selected),boundsMin=list(lo),boundsMax=list(hi),
        direction=counts,upFaceLoopNormals=up_loops,invertedFaceLoopNormals=inverted_loops,sideOrDownVertexNormals=horizontal_loops,sampleRays=len(hit_counts),raysWithoutHit=sum(n==0 for n in hit_counts),raysWithTwoOrMore=sum(n>=2 for n in hit_counts),
        maxHits=max(hit_counts),adjacentBedGapMetersP50=percentile(.5),adjacentBedGapMetersP95=percentile(.95),
        gapsUnderPointOneMillimetre=sum(d<.0001 for d in depths),adjacentPairs=len(depths))),flush=True)
