"""Deterministic nearest-surface PBR/normal bake for generated game meshes.

Blender 4.0.2 + numpy. Rasterize the continuous game atlas, locate the actual
untouched high source surface with BVH, and interpolate its UV/custom normals.
No lighting or current animation pose enters the bake. Source images stay 8K.
"""
import bpy
import numpy as np
from mathutils.bvhtree import BVHTree
from mathutils import Vector


def linked_image(socket,seen=None):
    seen=set() if seen is None else seen
    for link in socket.links:
        node=link.from_node
        if node in seen:continue
        seen.add(node)
        if node.type=='TEX_IMAGE' and node.image:return node.image
        for entry in node.inputs:
            found=linked_image(entry,seen)
            if found:return found


def array(collection,name,width,dtype='f4'):
    out=np.empty(len(collection)*width,dtype=dtype);collection.foreach_get(name,out)
    return out.reshape(-1,width)


def triangles(mesh):
    mesh.calc_loop_triangles()
    return array(mesh.loop_triangles,'vertices',3,'i4'),array(mesh.loop_triangles,'loops',3,'i4')


def unit(value):return value/np.maximum(np.linalg.norm(value,axis=-1,keepdims=True),1e-12)


def barycentric(p,tri):
    tri=tri.astype('f8');p=p.astype('f8')
    v0=tri[:,1]-tri[:,0];v1=tri[:,2]-tri[:,0];v2=p-tri[:,0]
    d00=np.sum(v0*v0,1);d01=np.sum(v0*v1,1);d11=np.sum(v1*v1,1)
    d20=np.sum(v2*v0,1);d21=np.sum(v2*v1,1);den=d00*d11-d01*d01
    den=np.where(abs(den)<1e-30,1e-30,den)
    v=(d11*d20-d01*d21)/den;w=(d00*d21-d01*d20)/den
    return np.column_stack((1-v-w,v,w))


def sample(image,uv):
    h,w=image.shape[:2];x=np.clip(uv[:,0]*w-.5,0,w-1);y=np.clip(uv[:,1]*h-.5,0,h-1)
    x0=x.astype('i4');y0=y.astype('i4');x1=np.minimum(x0+1,w-1);y1=np.minimum(y0+1,h-1)
    fx=(x-x0)[:,None];fy=(y-y0)[:,None]
    return ((image[y0,x0]*(1-fx)+image[y0,x1]*fx)*(1-fy)+
            (image[y1,x0]*(1-fx)+image[y1,x1]*fx)*fy)/255


def bake(high,lod,output,size):
    import author_creature as author
    rig=next(m.object for m in lod.modifiers if m.type=='ARMATURE');rig.data.pose_position='REST'
    hp=array(high.data.vertices,'co',3);ht,hl=triangles(high.data)
    high.data.calc_normals_split();hn=array(high.data.loops,'normal',3);hu=array(high.data.uv_layers.active.data,'uv',2)
    lp=array(lod.data.vertices,'co',3);lt,ll=triangles(lod.data)
    lod.data.calc_normals_split();lod.data.calc_tangents(uvmap=lod.data.uv_layers.active.name)
    ln=array(lod.data.loops,'normal',3);loop_tangents=array(lod.data.loops,'tangent',3)
    loop_signs=array(lod.data.loops,'bitangent_sign',1).ravel();lu=array(lod.data.uv_layers.active.data,'uv',2)
    print('[Creature Studio] Building source surface BVH',flush=True)
    tree=BVHTree.FromPolygons(hp.tolist(),ht.tolist(),all_triangles=True)
    materials=np.array([high.data.polygons[t.polygon_index].material_index for t in high.data.loop_triangles],dtype='i4')
    textures={};palette=[]
    def texture(image):
        if image is None:return None
        if image.name not in textures:
            values=np.empty(len(image.pixels),dtype='f4');image.pixels.foreach_get(values)
            textures[image.name]=np.clip(values.reshape(image.size[1],image.size[0],4)*255,0,255).astype('u1')
        return textures[image.name]
    for material in high.data.materials:
        bsdf=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        color=texture(linked_image(bsdf.inputs['Base Color']))
        packed=texture(linked_image(bsdf.inputs['Metallic']) or linked_image(bsdf.inputs['Roughness']))
        palette.append((color,packed,float(bsdf.inputs['Metallic'].default_value),float(bsdf.inputs['Roughness'].default_value)))
    pixel_chunks=[];point_chunks=[];normal_chunks=[];tangent_chunks=[];sign_chunks=[]
    for ids,loops in zip(lt,ll):
        uv=lu[loops];p=lp[ids];normals=ln[loops]
        low=np.maximum(np.floor(uv.min(0)*size-.5).astype('i4'),0)
        upper=np.minimum(np.ceil(uv.max(0)*size-.5).astype('i4'),size-1)
        if np.any(upper<low):continue
        yy,xx=np.mgrid[low[1]:upper[1]+1,low[0]:upper[0]+1]
        q=np.column_stack(((xx.ravel()+.5)/size,(yy.ravel()+.5)/size))
        a=uv[1]-uv[0];b=uv[2]-uv[0];det=a[0]*b[1]-a[1]*b[0]
        if abs(det)<1e-12:continue
        delta=q-uv[0];v=(delta[:,0]*b[1]-delta[:,1]*b[0])/det;w=(a[0]*delta[:,1]-a[1]*delta[:,0])/det
        weights=np.column_stack((1-v-w,v,w));inside=(weights>=-1e-6).all(1)
        if not inside.any():continue
        weights=weights[inside];coords=yy.ravel()[inside]*size+xx.ravel()[inside]
        tangent=((p[1]-p[0])*b[1]-(p[2]-p[0])*a[1])/det
        pixel_chunks.append(coords);point_chunks.append(weights@p);normal_chunks.append(unit(weights@normals))
        tangent_chunks.append(weights@loop_tangents[loops]);sign_chunks.append(np.sign(weights@loop_signs[loops]))
    pixels=np.concatenate(pixel_chunks);points=np.concatenate(point_chunks);normals=np.concatenate(normal_chunks)
    tangents=np.concatenate(tangent_chunks);signs=np.concatenate(sign_chunks)
    print(f'[Creature Studio] Mapping {len(points)} real atlas texels to source',flush=True)
    nearest=np.empty_like(points);indices=np.empty(len(points),dtype='i4');distance=np.empty(len(points),dtype='f4')
    for i,p in enumerate(points):
        found=tree.find_nearest(Vector(p));nearest[i]=found[0];indices[i]=found[2];distance[i]=found[3]
    weights=np.clip(barycentric(nearest,hp[ht[indices]]),0,1)
    weights/=np.maximum(weights.sum(1,keepdims=True),1e-12)
    src_uv=np.sum(hu[hl[indices]]*weights[:,:,None],axis=1)
    source_normals=unit(np.sum(hn[hl[indices]]*weights[:,:,None],axis=1))
    source_normals*=np.where(np.sum(source_normals*normals,axis=1)<0,-1,1)[:,None]
    tangents=unit(tangents-normals*np.sum(tangents*normals,axis=1,keepdims=True))
    bitangents=unit(np.cross(normals,tangents))*signs[:,None]
    tangent_normal=np.column_stack((np.sum(source_normals*tangents,1),np.sum(source_normals*bitangents,1),np.sum(source_normals*normals,1)))
    color=np.zeros((size*size,4),dtype='f4');orm=np.ones_like(color);normal=np.ones_like(color)
    color[:,3]=1;orm[:,1]=.8;orm[:,2]=0;normal[:,:3]=(.5,.5,1)
    material_ids=materials[indices]
    for slot,(base,packed,metal,rough) in enumerate(palette):
        selected=material_ids==slot;locations=pixels[selected]
        if base is None:raise ValueError('Generated source lacks base-color texture')
        color[locations,:3]=sample(base,src_uv[selected])[:,:3]
        if packed is not None:
            values=sample(packed,src_uv[selected]);orm[locations,1]=values[:,1];orm[locations,2]=values[:,2]
        else:orm[locations,1]=rough;orm[locations,2]=metal
    normal[pixels,:3]=np.clip(tangent_normal*.5+.5,0,1)
    # Expand real chart colors into the gutter without changing chart geometry.
    coverage=np.zeros(size*size,dtype=bool);coverage[pixels]=True;coverage=coverage.reshape(size,size)
    buffers=[color.reshape(size,size,4),orm.reshape(size,size,4),normal.reshape(size,size,4)]
    for _ in range(6):
        old=coverage.copy()
        for dy,dx in [(1,0),(-1,0),(0,1),(0,-1)]:
            shifted=np.roll(old,(dy,dx),(0,1));fill=shifted&~coverage
            for data in buffers:data[fill]=np.roll(data,(dy,dx),(0,1))[fill]
            coverage|=fill
    names=['albedo.png','game-orm.png','normal.png'];images=[]
    for name,data in zip(names,buffers):
        image=bpy.data.images.new(lod.name+'_'+name,width=size,height=size,alpha=True)
        image.colorspace_settings.name='Non-Color';image.pixels.foreach_set(data.ravel())
        author.save_image_atomic(image,output/name)
        image=bpy.data.images.load(str(output/name),check_existing=False)
        image.colorspace_settings.name='sRGB' if name=='albedo.png' else 'Non-Color';images.append(image)
    for slot in range(len(lod.data.materials)):
        material=bpy.data.materials.new(lod.name+'_SurfacePBR'+str(slot));material.use_nodes=True
        nodes=material.node_tree.nodes;links=material.node_tree.links;bsdf=nodes.get('Principled BSDF')
        albedo=nodes.new('ShaderNodeTexImage');albedo.image=images[0];links.new(albedo.outputs['Color'],bsdf.inputs['Base Color'])
        packed=nodes.new('ShaderNodeTexImage');packed.image=images[1];separate=nodes.new('ShaderNodeSeparateColor')
        links.new(packed.outputs['Color'],separate.inputs['Color']);links.new(separate.outputs['Green'],bsdf.inputs['Roughness']);links.new(separate.outputs['Blue'],bsdf.inputs['Metallic'])
        lod.data.materials[slot]=material
    rig.data.pose_position='POSE'
    return dict(method='actual source BVH nearest surface, barycentric UV/PBR/custom-normal transfer to continuous game atlas',
        size=[size,size],atlasSurfaceSamples=len(points),sourceDistanceP99=float(np.percentile(distance,99)),
        sourceDistanceMax=float(distance.max()),originalSourceUVAnd8KUnchanged=True,
        normal=dict(method='actual interpolated source surface normals in new game tangent frame',size=[size,size]))