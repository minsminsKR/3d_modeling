"""Blender 4.0.2: author seam-safe glow masks on ORIGINAL eye material UVs.

Read-only original/refinement meshes; no new eye geometry, albedo edits or .blend
saves. Source-visible UV triangles receive a feathered anatomical eye annulus.
Pupil/iris and empty aperture centres receive exactly zero emission.
"""
from pathlib import Path
import argparse
import json
import math
import sys
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
sys.path.insert(0,str(Path(__file__).resolve().parent))
from eye_regions import REGIONS, outer_samples
from inspect_head_sources import source_eye_uv

PROJECT=Path(__file__).resolve().parents[2]
SIZE=2048


def load_source(project,key):
    if key in ('Cyclopse','Uncat','Hwacat_angry','Baby'):
        source=project/('SourceArt/EnemyRefinementV2/'+key+'-refinement-v2.blend')
        bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene;scene.frame_set(1)
        rig=next(obj for obj in scene.objects if obj.type=='ARMATURE')
        head=next(bone for bone in rig.pose.bones if bone.name.endswith(':Head'))
        meshes=[obj for obj in scene.objects if obj.type=='MESH' and obj.name!='Proof floor']
        head_points=[]
        for obj in meshes:
            group=obj.vertex_groups.get(head.name)
            if not group: continue
            evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());posed=evaluated.to_mesh()
            head_points += [evaluated.matrix_world@posed.vertices[vertex.index].co for vertex in obj.data.vertices
                if any(item.group==group.index and item.weight>.30 for item in vertex.groups)]
            evaluated.to_mesh_clear()
        lo=Vector(tuple(min(point[i] for point in head_points) for i in range(3)))
        hi=Vector(tuple(max(point[i] for point in head_points) for i in range(3)))
        centre=(lo+hi)/2;height=max((hi-lo).z,(hi-lo).x)
        camera=scene.camera;camera.location=centre+Vector((0,-height*2.5,height*.02))
        camera.rotation_euler=(centre-camera.location).to_track_quat('-Z','Y').to_euler()
        camera.data.type='ORTHO';camera.data.ortho_scale=height*1.30
    else:
        source=project/('Assets/Art/V1/'+key+'/'+key+'.fbx')
        bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(source),use_anim=False)
        scene=bpy.context.scene;meshes=[obj for obj in scene.objects if obj.type=='MESH']
        points=[obj.matrix_world@vertex.co for obj in meshes for vertex in obj.data.vertices]
        lo=Vector(tuple(min(point[i] for point in points) for i in range(3)))
        hi=Vector(tuple(max(point[i] for point in points) for i in range(3)))
        height=(hi-lo).z;centre=(lo+hi)/2
        if key=='Mannequin': centre.z=hi.z-height*.125;crop=height*.37
        else: crop=max((hi-lo).z,(hi-lo).x)*1.2
        camera=bpy.data.objects.new('Read-only source eye measurement camera',bpy.data.cameras.new('Read-only eye camera'))
        scene.collection.objects.link(camera);scene.camera=camera;camera.location=centre+Vector((0,-height*3,height*.025))
        camera.rotation_euler=(centre-camera.location).to_track_quat('-Z','Y').to_euler()
        camera.data.type='ORTHO';camera.data.ortho_scale=crop
    bpy.context.view_layer.update()
    return scene,meshes,source


def snapshots(meshes):
    result=[]
    for obj in meshes:
        evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh();mesh.calc_loop_triangles()
        points=[evaluated.matrix_world@vertex.co for vertex in mesh.vertices]
        triangles=[dict(points=np.array([points[index] for index in triangle.vertices],dtype=np.float64),
            uv=np.array([mesh.uv_layers.active.data[index].uv[:] for index in triangle.loops],dtype=np.float64),
            slot=triangle.material_index) for triangle in mesh.loop_triangles]
        tree=BVHTree.FromPolygons(points,[tuple(triangle.vertices) for triangle in mesh.loop_triangles],all_triangles=True)
        result.append(dict(name=obj.name,points=np.array(points,dtype=np.float64),triangles=triangles,tree=tree))
        evaluated.to_mesh_clear()
    return result


def projected(scene,points):
    camera=scene.camera;inverse=np.array(camera.matrix_world.inverted(),dtype=np.float64)
    homogeneous=np.column_stack((points,np.ones(len(points))))
    local=homogeneous@inverse.T
    return np.column_stack((400+local[:,0]*800/camera.data.ortho_scale,400-local[:,1]*800/camera.data.ortho_scale))


def mask_value(region,x,y):
    if region['kind']=='ellipseAnnulus':
        angle=math.radians(region['rotation']);c,s=math.cos(angle),math.sin(angle)
        dx=x-region['centre'][0];dy=y-region['centre'][1];rx=c*dx+s*dy;ry=-s*dx+c*dy
        outer=np.sqrt((rx/region['outer'][0])**2+(ry/region['outer'][1])**2)
        inner=np.sqrt((rx/region['clear'][0])**2+(ry/region['clear'][1])**2)
        # Exact zero through the protected centre. Soft edges lie entirely in
        # the existing annular surface, never over the source iris/pupil.
        outside=(1-outer)*min(region['outer']);inside=(inner-1)*min(region['clear'])
        a=np.clip(outside/region['feather'],0,1);b=np.clip(inside/region['feather'],0,1)
        a=a*a*(3-2*a);b=b*b*(3-2*b)
        t=np.clip(inside/np.maximum(inside+outside,.0000001),0,1)
        radial=np.sin(t*math.pi)**1.5
        theta=np.arctan2(ry/region['outer'][1],rx/region['outer'][0])
        phase=-.24 if 'right' in region['name'] else .24
        arc=np.clip(.65+.26*np.sin(theta+phase)-.10*np.cos(theta*2+.7),.24,1)
        return a*b*radial*arc*region['peak']
    boundary=region['boundary'];distance=np.full_like(x,np.inf,dtype=np.float64)
    for index,a in enumerate(boundary):
        b=boundary[(index+1)%len(boundary)];vx=b[0]-a[0];vy=b[1]-a[1]
        t=np.clip(((x-a[0])*vx+(y-a[1])*vy)/(vx*vx+vy*vy),0,1)
        distance=np.minimum(distance,np.sqrt((x-a[0]-t*vx)**2+(y-a[1]-t*vy)**2))
    edge=np.clip((region['lipWidth']-distance)/region['feather'],0,1)
    edge=edge*edge*(3-2*edge)
    radial=np.exp(-((distance/(region['lipWidth']*.66))**2))
    theta=np.arctan2(y-region['centre'][1],x-region['centre'][0])
    arc=np.clip(.72+.20*np.sin(theta+.25)-.08*np.cos(theta*2-.4),.35,1)
    return edge*radial*arc*region['peak']


def visible(scene,trees,point,pixel,body_height):
    rotation=scene.camera.rotation_euler.to_quaternion()
    origin=scene.camera.location+rotation@Vector(((pixel[0]/800-.5)*scene.camera.data.ortho_scale,
        (.5-pixel[1]/800)*scene.camera.data.ortho_scale,0))
    direction=rotation@Vector((0,0,-1));nearest=None
    for tree in trees:
        hit,normal,index,distance=tree.ray_cast(origin,direction)
        if hit is not None and (nearest is None or distance<nearest[1]): nearest=(hit,distance)
    return nearest is not None and (nearest[0]-Vector(point)).length<body_height*.0002


def make_mask(scene,sources,regions,body_height):
    mask=np.zeros((SIZE,SIZE),dtype=np.float32)
    trees=[source['tree'] for source in sources];slots=set();painted=0
    ranges=[]
    for region in regions:
        samples=np.array(outer_samples(region));padding=region.get('lipWidth',0)+2
        ranges.append((samples.min(axis=0)-padding,samples.max(axis=0)+padding))
    for source in sources:
        for triangle in source['triangles']:
            points=triangle['points'];screen=projected(scene,points)
            if not any(np.all(screen.max(axis=0)>=lo) and np.all(screen.min(axis=0)<=hi) for lo,hi in ranges): continue
            uv=triangle['uv'];a,b,c=uv*SIZE-.5
            ab=b-a;ac=c-a;det=ab[0]*ac[1]-ac[0]*ab[1]
            if abs(det)<1e-10: continue
            lo=np.maximum(np.floor(np.minimum(np.minimum(a,b),c)).astype(int),0)
            hi=np.minimum(np.ceil(np.maximum(np.maximum(a,b),c)).astype(int),SIZE-1)
            if np.any(hi<lo): continue
            xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1),np.arange(lo[1],hi[1]+1))
            apx=xx-a[0];apy=yy-a[1]
            v=(apx*ac[1]-ac[0]*apy)/det;w=(ab[0]*apy-apx*ab[1])/det;u=1-v-w
            inside=(u>=0)&(v>=0)&(w>=0)
            if not inside.any(): continue
            xi=xx[inside];yi=yy[inside];weights=np.column_stack((u[inside],v[inside],w[inside]))
            xy=weights@screen
            values=np.maximum.reduce([mask_value(region,xy[:,0],xy[:,1]) for region in regions])
            active=values>.001
            if not active.any(): continue
            xi=xi[active];yi=yi[active];weights=weights[active];xy=xy[active];values=values[active]
            world=weights@points
            for index in range(len(values)):
                if not visible(scene,trees,world[index],xy[index],body_height): continue
                mask[yi[index],xi[index]]=max(mask[yi[index],xi[index]],values[index]);painted+=1;slots.add(triangle['slot'])
    if slots!={0}: raise RuntimeError('Only the original face material slot0 may emit: '+repr(slots))
    if painted==0: raise RuntimeError('Anatomical emission mask is empty')
    return mask,painted


def vector(values): return dict(zip(('x','y','z')[:len(values)],map(float,values)))


def hit_or_lip(scene,meshes,pixel,centre):
    candidates=[pixel]
    delta=Vector(pixel)-Vector(centre)
    if delta.length>0:
        delta.normalize();candidates += [Vector(pixel)+delta*step for step in (2,4,6,8)]
    for candidate in candidates:
        try: return source_eye_uv(scene,meshes,candidate)
        except RuntimeError: pass
    return None


def profile_eye(scene,meshes,region,height):
    hit=source_eye_uv(scene,meshes,region.get('anchor',region['centre']))
    boundary=[];world=[]
    for pixel in outer_samples(region):
        sample=hit_or_lip(scene,meshes,pixel,region['centre'])
        if sample is None: continue
        if sample['materialSlot']!=0: raise RuntimeError('Eye boundary leaves original face slot0')
        boundary.append(vector(sample['uv']));world.append(Vector(sample['posedWorldPoint']))
    if len(boundary)<8: raise RuntimeError('Too few real eye boundary UV samples')
    if region['kind']=='apertureLip': centre=sum(world,Vector())/len(world)
    else: centre=Vector(hit['posedWorldPoint'])
    radius=max((point-centre).length for point in world)
    eye=dict(name=region['name'],uv=vector(hit['uv']),sourceMesh=hit['sourceMesh'],materialSlot=hit['materialSlot'],
        uvBoundarySamples=boundary,sourceWorldRadius=radius,sourceWorldHeight=height,
        sourceCentreWorld=vector(centre),sourceAnchorWorld=vector(hit['posedWorldPoint']),
        sourceWorldNormal=vector(hit['posedWorldNormal']),screenCentre=vector(region['centre']),
        shape=region['kind'],preservedFeature=region['preservedFeature'],featherPixels=region['feather'],
        centreHasSurface=region['kind']!='apertureLip',aperture=region['kind']=='apertureLip',
        emissionFalloff='smooth radial ridge with low-frequency arc modulation',maskPeak=region['peak'])
    if region['kind']=='ellipseAnnulus':
        eye.update(screenOuterRadii=vector(region['outer']),screenClearRadii=vector(region['clear']),
            screenRotationDegrees=region['rotation'])
        angle=math.radians(region['rotation']);c,s=math.cos(angle),math.sin(angle)
        axes=[]
        for delta in ((region['outer'][0]*c,region['outer'][0]*s),(-region['outer'][1]*s,region['outer'][1]*c)):
            at=(region['centre'][0]+delta[0],region['centre'][1]+delta[1]);sample=source_eye_uv(scene,meshes,at)
            axes.append(vector(Vector(sample['uv'])-Vector(hit['uv'])))
        eye['uvAxisX']=axes[0];eye['uvAxisY']=axes[1]
    else: eye.update(screenLipBoundary=[vector(point) for point in region['boundary']],lipWidthPixels=region['lipWidth'])
    return eye


def write_png(path,mask):
    path.parent.mkdir(parents=True,exist_ok=True)
    image=bpy.data.images.new('Original-surface eye annulus emission',width=SIZE,height=SIZE,alpha=False,float_buffer=False)
    image.colorspace_settings.name='Non-Color'
    rgba=np.ones((SIZE,SIZE,4),dtype=np.float32);rgba[:,:,:3]=mask[:,:,None]
    image.pixels.foreach_set(rgba.ravel());image.filepath_raw=str(path);image.file_format='PNG';image.save()
    bpy.data.images.remove(image)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--resource-project',type=Path,default=PROJECT)
    parser.add_argument('--output-project',type=Path,default=PROJECT)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    records=[]
    for key,regions in REGIONS.items():
        scene,meshes,source=load_source(args.resource_project,key);surfaces=snapshots(meshes)
        points=np.concatenate([item['points'] for item in surfaces]);height=float(points[:,2].max()-points[:,2].min())
        mask,painted=make_mask(scene,surfaces,regions,height)
        profile=dict(version=2,key=key,source=source.relative_to(args.resource_project).as_posix(),
            maskResource='ThreatEyes/'+key+'-emission',materialSlot=0,
            eyes=[profile_eye(scene,meshes,region,height) for region in regions])
        directory=args.output_project/'Assets/Resources/ThreatEyes';directory.mkdir(parents=True,exist_ok=True)
        (directory/(key+'-profile.json')).write_text(json.dumps(profile,indent=2),encoding='utf-8')
        write_png(directory/(key+'-emission.png'),mask)
        records.append(dict(key=key,eyeCount=len(regions),source=profile['source'],materialSlots=[0],
            nonzeroTexels=int((mask>0).sum()),strongTexels=int((mask>.5).sum()),paintedSamples=painted,
            textureDimensions=[SIZE,SIZE],preservesOriginalBaseMap=True,addsGeometry=False,
            maximumScalar=float(mask.max()),meanPositiveScalar=float(mask[mask>0].mean()),
            flatWhiteTexels=int((mask>.95).sum()),
            centreMaskValues=[float(mask[min(SIZE-1,int(eye['uv']['y']*SIZE)),min(SIZE-1,int(eye['uv']['x']*SIZE))])
                if eye['centreHasSurface'] else None for eye in profile['eyes']]))
        print('ORIGINAL_SURFACE_EYE_MASK',key,len(regions),'eyes',int((mask>0).sum()),'texels',flush=True)
    destination=args.output_project/'SourceArt/ThreatEyes/emission-mask-manifest.json'
    destination.parent.mkdir(parents=True,exist_ok=True)
    destination.write_text(json.dumps(dict(version=2,blender=bpy.app.version_string,assets=records,
        scope='Read-only source mesh UV rasterization. Original pupils/iris/eyeballs/apertures are preserved. Native Unity review is separate.'),indent=2),encoding='utf-8')


if __name__=='__main__': main()
