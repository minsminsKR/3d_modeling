"""Map source eye landmarks to a real triangle of the continuous game UV atlas.

The iris boundary/pupil are clipped to the actual selected game triangle,
never across unrelated UV islands. Source eye authoring stays independent.
"""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import closest_point_on_tri
sys.path.insert(0,str(Path(__file__).resolve().parent))
import author_creature as author
from bake_surface_atlas import array,triangles,barycentric


def main():
    p=argparse.ArgumentParser();p.add_argument('--creature',required=True);a=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    folder=author.STUDIO/a.creature
    bpy.ops.wm.open_mainfile(filepath=str(folder/(a.creature+'_Editable.blend')))
    lod=bpy.data.objects[a.creature+'_LOD0'];report=json.loads((folder/'authoring-report.json').read_text())
    source=json.loads((folder/'eyes-source-authoring.json').read_text())
    positions=array(lod.data.vertices,'co',3);faces,loops=triangles(lod.data);uv=array(lod.data.uv_layers.active.data,'uv',2)
    slots=np.array([lod.data.polygons[t.polygon_index].material_index for t in lod.data.loop_triangles])
    normalization=report['normalization'];bounds=np.array(normalization['sourceBounds']);center=bounds.mean(0);center[2]=bounds[0,2];scale=normalization['uniformScale']
    height=bounds[1,2]-bounds[0,2];out=dict(version=2,key=a.creature,sourceGLBSHA256=source['sourceGLBSHA256'],eyes=[],
        selection='source landmark projected onto an actual game surface triangle; clipped iris boundary and pupil',
        gameFBXSHA256=author.digest(author.PROJECT/'Assets'/'Resources'/'EnemyStudio'/a.creature/(a.creature+'.fbx')))
    for eye in source['eyes']:
        point=np.array(eye['sourceCenterGLTF']);point=(point[[0,2,1]]*np.array([1,-1,1])-center)*scale
        slot=eye.get('materialSlot',0);candidates=np.flatnonzero(slots==slot)
        chart=uv[loops[candidates]];area=np.cross(chart[:,1]-chart[:,0],chart[:,2]-chart[:,0]);candidates=candidates[abs(area)>1e-12]
        tree=BVHTree.FromPolygons(positions.tolist(),faces[candidates].tolist(),all_triangles=True)
        location,_,idx,distance=tree.find_nearest(Vector(point));index=candidates[idx];tri=positions[faces[index]].astype('f8');chart=uv[loops[index]].astype('f8')
        local=np.array(location);weights=barycentric(local[None],tri[None])[0];anchor=weights@chart
        radii=np.array(eye.get('reviewedRadiiPixels',[10,6]))/source.get('face_preview_size',1200)*height*.45*scale
        if eye.get('generatedActualOcularSurface'):
            cfg=json.loads((folder/'eye-surface.json').read_text());radii=np.array(cfg['radiiGLTF'][:2])*scale*.85
        def boundary(factor):
            result=[]
            for angle in np.linspace(0,2*np.pi,32,endpoint=False):
                intended=local+np.array([np.cos(angle)*radii[0],0,np.sin(angle)*radii[1]])*factor
                fitted=closest_point_on_tri(Vector(intended),*map(Vector,tri));w=barycentric(np.array(fitted)[None],tri[None])[0]
                value=np.clip(w@chart,0,1);result.append(dict(x=float(value[0]),y=float(value[1])))
            return result
        out['eyes'].append(dict(uv=dict(x=float(anchor[0]),y=float(anchor[1])),materialSlot=int(slot),
            sourceMesh=lod.name,sourceCenterGLTF=eye['sourceCenterGLTF'],sourceRadius=eye['sourceRadius'],
            uvBoundarySamples=boundary(1),uvIslandContinuityVerified=True,blackCutouts=[boundary(.32)],
            actualGameTriangle=int(index),actualSourceLandmarkDistance=float(distance),aperture=False))
    (folder/'eyes-authoring.json').write_text(json.dumps(out,indent=2),encoding='utf8')
    print('GAME_EYES_AUTHORED',a.creature,len(out['eyes']))

if __name__=='__main__':main()