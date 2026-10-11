"""Rebuild the generated two-orbit face as one actual central orbit in Blender 4.0.2.

Canonical hook: apply_surgery(mesh) AFTER source-order weights are bound and BEFORE
normalization/LOD/baking. Original GLB and PBR images are never overwritten.
Donor eyelid/socket geometry and its separate UV corners are retained, translated
and enlarged. The eyeball surface becomes a convex corneal dome. Both previous
orbit surfaces are removed. A triangulated skin annulus joins the new socket to
the existing face; it is part of the same mesh, not a foreground eye overlay.

All added vertices are rigidly Head-weighted. Existing deform/UV layers survive
outside the surgical region. The feature group can retain extra eye detail during
game decimation. Returned eyeAuthoring rasterizes actual source UV triangles,
never a polygon bridging UV seams or the old right eye.

Standalone quiet inspection:
blender --background --factory-startup --python tools/cyclopse_eye_surgery.py --
  --output /path/to/ignored/work/Cyclopse-one-eye
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector
from mathutils.geometry import delaunay_2d_cdt

HERE = Path(__file__).resolve().parent
CONFIG = HERE.parent / 'Cyclopse' / 'eye-surgery.json'


def _source(world):
    return Vector((world.x, world.z, -world.y))


def _blender(source):
    return Vector((source[0], -source[2], source[1]))


def _ellipse(point, center, radii):
    return ((point[0]-center[0])/radii[0])**2+((point[1]-center[1])/radii[1])**2 < 1


def _inside(point, polygon):
    inside = False
    x, y = point
    for index, a in enumerate(polygon):
        b = polygon[(index+1) % len(polygon)]
        if (a[1] > y) != (b[1] > y) and x < (b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]:
            inside = not inside
    return inside


def _area(polygon):
    return .5*sum(a[0]*polygon[(index+1) % len(polygon)][1]-polygon[(index+1) % len(polygon)][0]*a[1]
                  for index, a in enumerate(polygon))


def _loops(edges, coordinate):
    remaining = set(edges)
    loops, open_chains = [], []
    while remaining:
        edge = next(iter(remaining)); start, current = edge.verts
        chain = [start]; remaining.remove(edge)
        previous = start
        for _ in range(len(edges)+2):
            if current == start:
                if len(chain) >= 3: loops.append(chain)
                break
            chain.append(current)
            choices = [candidate for candidate in current.link_edges if candidate in remaining]
            if not choices:
                open_chains.append(chain); break
            if len(choices) > 1:
                # Keep the least sharp turn if two component boundaries touch.
                forward = coordinate(current)-coordinate(previous)
                choices.sort(key=lambda candidate: forward.normalized().dot((coordinate(candidate.other_vert(current))-coordinate(current)).normalized()), reverse=True)
            edge = choices[0]; remaining.remove(edge); previous, current = current, edge.other_vert(current)
        else:
            raise RuntimeError('Surgical boundary walk did not terminate')
    return loops, open_chains


def apply_surgery(mesh, config_path=None):
    config = json.loads(Path(config_path or CONFIG).read_text(encoding='utf8'))
    if mesh.type != 'MESH' or not mesh.data.uv_layers.active:
        raise ValueError('Surgery needs the original textured source mesh')
    if mesh.get('CyclopseOneEyeSurgery'):
        raise ValueError('Do not apply the surgery twice to a derived mesh')
    matrix = mesh.matrix_world.copy(); inverse = matrix.inverted()
    before_vertices, before_faces = len(mesh.data.vertices), len(mesh.data.polygons)
    data = np.empty(before_vertices*3, dtype=np.float32); mesh.data.vertices.foreach_get('co', data)
    local = data.reshape(-1,3); affine = np.asarray(matrix)
    world = local@affine[:3,:3].T+affine[:3,3]
    source = np.c_[world[:,0],world[:,2],-world[:,1]]
    expected = np.asarray(config['sourceBounds'])
    actual = np.stack((source.min(0),source.max(0)))
    if np.max(np.abs(actual-expected)) > .0001:
        raise ValueError('Surgery hook must run in raw source coordinates before normalization: '+str(actual.tolist()))
    roi = np.where((source[:,0] > -.10) & (source[:,0] < .085) &
                   (source[:,1] > .325) & (source[:,1] < .482) & (source[:,2] > .065))[0]
    if len(roi) < 1000:
        raise ValueError('Reviewed real face region is absent')
    bm = bmesh.new(); bm.from_mesh(mesh.data); bm.verts.ensure_lookup_table()
    deform = bm.verts.layers.deform.verify()
    uv_layers = list(bm.loops.layers.uv.values()); active_uv = bm.loops.layers.uv.active
    head = mesh.vertex_groups.get(config['headGroup']) or mesh.vertex_groups.new(name=config['headGroup'])
    feature = mesh.vertex_groups.get(config['featureGroup']) or mesh.vertex_groups.new(name=config['featureGroup'])
    roi_set = set(int(index) for index in roi)
    witnesses = [(bm.verts[index], bm.verts[index].co.copy(), dict(bm.verts[index][deform]))
                 for index in range(0, before_vertices, max(1,before_vertices//1024)) if index not in roi_set]
    roi_vertices = [bm.verts[int(index)] for index in roi]
    welded = bmesh.ops.remove_doubles(bm, verts=roi_vertices, dist=config['weldDistance'])
    # Welding only UV-split copies preserves per-loop UV corners and removes
    # artificial seam gaps before walking actual geometric cut boundaries.
    valid_roi = [vertex for vertex in roi_vertices if vertex.is_valid]
    candidates = {face for vertex in valid_roi for face in vertex.link_faces}
    coordinate_cache = {}

    def point(vertex):
        if vertex not in coordinate_cache: coordinate_cache[vertex] = _source(matrix@vertex.co)
        return coordinate_cache[vertex]

    def centroid(face):
        return sum((point(vertex) for vertex in face.verts), Vector())/len(face.verts)

    donor_center = config['donorCenterXY']; donor_radii = config['donorSocketRadii']
    donor = {face for face in candidates if (center := centroid(face))[2] > config['frontDepthMinimum'] and _ellipse(center, donor_center, donor_radii)}
    if len(donor) < 50:
        bm.free(); raise ValueError('The actual donor eye/socket surface was not found')
    donor_vertices = sorted({vertex for face in donor for vertex in face.verts}, key=lambda vertex: vertex.index)
    donor_ids = {vertex:index for index,vertex in enumerate(donor_vertices)}
    donor_points = [point(vertex).copy() for vertex in donor_vertices]
    donor_faces = []
    for face in donor:
        donor_faces.append({'ids':[donor_ids[loop.vert] for loop in face.loops], 'material':face.material_index,
                            'uv':[[list(loop[layer].uv) for loop in face.loops] for layer in uv_layers]})
    donor_edges = [edge for face in donor for edge in face.edges if sum(link in donor for link in edge.link_faces) == 1]
    donor_loops, donor_open = _loops(set(donor_edges),point)
    if not donor_loops:
        bm.free(); raise ValueError('Donor socket has no closed actual perimeter')
    donor_boundary = max(donor_loops,key=lambda loop:abs(_area([point(vertex)[:2] for vertex in loop])))
    if donor_open:
        print('[Cyclopse surgery] Donor has '+str(len(donor_open))+' small open seam chains',flush=True)
    donor_reference_depth = float(np.median([point(vertex)[2] for vertex in donor_boundary]))
    target = config['targetCenter']; scale = config['socketScale']
    moved = []
    for old in donor_points:
        value = Vector((target[0]+(old[0]-donor_center[0])*scale,
                        target[1]+(old[1]-donor_center[1])*scale,
                        target[2]+(old[2]-donor_reference_depth)*config['depthScale']))
        r = math.sqrt(((old[0]-donor_center[0])/config['donorLensRadii'][0])**2+
                      ((old[1]-donor_center[1])/config['donorLensRadii'][1])**2)
        if r < 1:
            value[2] = target[2]+.002+config['lensDomeDepth']*math.sqrt(max(0,1-r*r))
        elif r < 1.22:
            blend = (1.22-r)/.22
            value[2] = value[2]*(1-blend)+(target[2]+.002)*blend
        moved.append(value)
    nose = config['noseProtection']
    removed = set()
    for face in candidates:
        center = centroid(face)
        if center[2] <= config['frontDepthMinimum']: continue
        cut = any(_ellipse(center,orbit['center'],orbit['radii']) for orbit in config['oldOrbits']) or _ellipse(center,target[:2],config['targetCutRadii'])
        protect = center[2] > nose['frontDepthMinimum'] and _ellipse(center,nose['center'],nose['radii'])
        if cut and not protect: removed.add(face)
    if len(removed) < len(donor)*2:
        bm.free(); raise ValueError('Both old orbits were not selected for actual geometry excision')
    outside_edges = {edge for face in removed for edge in face.edges if any(link not in removed for link in edge.link_faces)}
    boundary_loops, open_chains = _loops(outside_edges,point)
    if not boundary_loops:
        bm.free(); raise ValueError('Excision has no closed face perimeter; refuse an overlay fallback')
    boundary = max(boundary_loops,key=lambda loop:abs(_area([point(vertex)[:2] for vertex in loop])))
    outer_xy = [tuple(point(vertex)[:2]) for vertex in boundary]
    inner_ids = [donor_ids[vertex] for vertex in donor_boundary]
    inner_xy = [tuple(moved[index][:2]) for index in inner_ids]
    if not _inside(target[:2],outer_xy):
        bm.free(); raise ValueError('Actual face cut does not surround the central eye')
    if not all(_inside(point,outer_xy) for point in inner_xy):
        missed = [point for point in inner_xy if not _inside(point,outer_xy)]
        detail = {'missed':missed[:12], 'outerBounds':[[min(value[0] for value in outer_xy),min(value[1] for value in outer_xy)],
                  [max(value[0] for value in outer_xy),max(value[1] for value in outer_xy)]],
                  'outerArea':_area(outer_xy),'innerArea':_area(inner_xy),'outerLoops':len(boundary_loops)}
        bm.free(); raise ValueError('New socket perimeter does not fit the excised face: '+json.dumps(detail))
    removed_count = len(removed)
    bmesh.ops.delete(bm,geom=list(removed),context='FACES')
    new_eye = []
    for value in moved:
        vertex = bm.verts.new(inverse@_blender(value));vertex[deform][head.index] = 1;vertex[deform][feature.index] = 1;new_eye.append(vertex)
    eye_faces = []
    for copied in donor_faces:
        face = bm.faces.new([new_eye[index] for index in copied['ids']]);face.material_index=copied['material'];face.smooth=True
        for layer_index,layer in enumerate(uv_layers):
            for loop,uv in zip(face.loops,copied['uv'][layer_index]):loop[layer].uv=uv
        eye_faces.append(face)
    inner_vertices = [new_eye[index] for index in inner_ids]
    vertices2d = [Vector(xy) for xy in outer_xy+inner_xy]
    fixed_vertices = boundary+inner_vertices
    perimeter = len(boundary); inner_start = perimeter
    constraints = [(index,(index+1)%perimeter) for index in range(perimeter)]
    constraints += [(inner_start+index,inner_start+(index+1)%len(inner_xy)) for index in range(len(inner_xy))]
    step = config['skinGridStep']
    xmin,xmax = min(x for x,y in outer_xy),max(x for x,y in outer_xy)
    ymin,ymax = min(y for x,y in outer_xy),max(y for x,y in outer_xy)
    for x in np.arange(xmin+step,xmax,step):
        for y in np.arange(ymin+step,ymax,step):
            if _inside((x,y),outer_xy) and not _inside((x,y),inner_xy):vertices2d.append(Vector((float(x),float(y))))
    coords,edges,triangles,original_vertices,_,_ = delaunay_2d_cdt(vertices2d,constraints,[],0,1e-9,True)
    surface_points = [point(vertex) for vertex in boundary]+[moved[index] for index in inner_ids]
    sample_xy = np.asarray([value[:2] for value in surface_points]);sample_z=np.asarray([value[2] for value in surface_points])
    # Quadratic head curvature plus local boundary residuals. Unlike a flat
    # cover, both new lid and original cheek/forehead edges meet their real depths.
    origin=np.asarray(target[:2]);xy=sample_xy-origin
    design=np.c_[np.ones(len(xy)),xy[:,0],xy[:,1],xy[:,0]**2,xy[:,0]*xy[:,1],xy[:,1]**2]
    coefficients=np.linalg.lstsq(design,sample_z,rcond=None)[0];residual=sample_z-design@coefficients
    output_vertices=[];new_skin=[]
    for coord,original in zip(coords,original_vertices):
        fixed=[index for index in original if index<len(fixed_vertices)]
        if fixed:
            vertex=fixed_vertices[fixed[0]]
        else:
            relative=np.asarray(coord)-origin
            base=np.asarray([1,relative[0],relative[1],relative[0]**2,relative[0]*relative[1],relative[1]**2])@coefficients
            distance=((sample_xy-np.asarray(coord))**2).sum(1)
            weights=1/np.maximum(distance,1e-10)
            depth=float(base+(residual*weights).sum()/weights.sum())
            vertex=bm.verts.new(inverse@_blender((coord[0],coord[1],depth)));vertex[deform][head.index]=1
            new_skin.append(vertex)
        output_vertices.append(vertex)
    uv_center=config['skinUVCenter'];uv_half=config['skinUVHalfWidth'];skin_faces=[]
    for indices in triangles:
        center=sum((coords[index] for index in indices),Vector((0,0)))/len(indices)
        if not _inside(center,outer_xy) or _inside(center,inner_xy):continue
        face=bm.faces.new([output_vertices[index] for index in indices]);face.material_index=0;face.smooth=True
        for loop,index in zip(face.loops,indices):
            xy=coords[index]
            uv=(uv_center[0]+((xy[0]-xmin)/(xmax-xmin)*2-1)*uv_half,
                uv_center[1]+((xy[1]-ymin)/(ymax-ymin)*2-1)*uv_half)
            for layer in uv_layers:loop[layer].uv=uv
        skin_faces.append(face)
    if not skin_faces:
        bm.free();raise ValueError('Skin annulus triangulation is empty')
    # Check the main annulus joins both true perimeters, rather than layering
    # another model over still-present old eye surfaces.
    new_eye_set=set(eye_faces);new_skin_set=set(skin_faces)
    unjoined_inner=sum(not any(face in new_skin_set for face in edge.link_faces)
                       for face in eye_faces for edge in face.edges
                       if sum(link in new_eye_set for link in edge.link_faces)==1)
    unjoined_outer=sum(not any(face in new_skin_set for face in edge.link_faces) for edge in outside_edges if edge.is_valid)
    if unjoined_inner or unjoined_outer:
        bm.free();raise ValueError('Unjoined surgical border: inner='+str(unjoined_inner)+' outer='+str(unjoined_outer))
    for vertex,co,weights in witnesses:
        if not vertex.is_valid or (vertex.co-co).length > 1e-9 or dict(vertex[deform])!=weights:
            bm.free();raise AssertionError('Surgery modified body positions or skin weights outside the reviewed face')

    # Author the single actual iris from its own triangles. Split UV charts
    # remain separate; a pupil cutout is black, as is every body/old-right-eye UV.
    iris_triangles=[];black_triangles=[];iris_points=[]
    for copied,face in zip(donor_faces,eye_faces):
        original_center=sum((donor_points[index] for index in copied['ids']),Vector())/len(copied['ids'])
        iris=_ellipse(original_center,donor_center,config['donorIrisRadii'])
        pupil=_ellipse(original_center,donor_center,config['donorPupilRadii'])
        corners=[{'x':float(loop[active_uv].uv.x),'y':float(loop[active_uv].uv.y)} for loop in face.loops]
        if len(corners)!=3:raise ValueError('Original eye donor must have triangulated physical surfaces')
        if iris:
            iris_triangles.append(corners);iris_points.extend(zip([donor_points[index] for index in copied['ids']],corners))
        if pupil:black_triangles.append(corners)
    if len(iris_triangles)<8:bm.free();raise ValueError('No actual central iris UV triangles')
    center_entry=min(iris_points,key=lambda pair:(pair[0].x-donor_center[0])**2+(pair[0].y-donor_center[1])**2)
    boundary_uv=[]
    for angle in np.linspace(0,math.tau,8,endpoint=False):
        target_point=(donor_center[0]+config['donorIrisRadii'][0]*math.cos(angle),donor_center[1]+config['donorIrisRadii'][1]*math.sin(angle))
        sample=min(iris_points,key=lambda pair:(pair[0].x-target_point[0])**2+(pair[0].y-target_point[1])**2)
        boundary_uv.append(sample[1])
    eye_authoring={'version':2,'key':'Cyclopse','sourceGLBSHA256':config['sourceSHA256'],
                  'provenance':'Actual surgically relocated single iris; old two-orbit surfaces removed; independent UV triangle raster',
                  'eyes':[{'uv':center_entry[1],'materialSlot':0,'sourceMesh':'Cyclopse_LOD0',
                           'uvBoundarySamples':boundary_uv,'rasterTriangles':iris_triangles,'blackTriangles':black_triangles,
                           'sourceCenterGLTF':[target[0],target[1],target[2]+.002+config['lensDomeDepth']],
                           'sourceRadius':max(config['donorIrisRadii'])*scale}]}
    bm.normal_update();bm.to_mesh(mesh.data);bm.free();mesh.data.update()
    mesh['CyclopseOneEyeSurgery']=1;mesh['CentralEyeSourceGLTF']=target
    report={'status':'PASS','sourceSHA256':config['sourceSHA256'],'sourceVertices':before_vertices,'sourceFaces':before_faces,
            'correctedVertices':len(mesh.data.vertices),'correctedFaces':len(mesh.data.polygons),
            'removedActualOldOrbitFaces':removed_count,'relocatedActualEyeFaces':len(donor_faces),'skinBridgeFaces':len(skin_faces),
            'onePhysicalOrbit':True,'convexEyeballDepth':config['lensDomeDepth'],'targetCenterGLTF':target,
            'donorReferenceDepth':donor_reference_depth,'headRigidNewVertices':len(new_eye)+len(new_skin),
            'unchangedBodyWitnesses':len(witnesses),'joinedInnerBorder':unjoined_inner==0,'joinedOuterBorder':unjoined_outer==0,
            'sourceTexturesUnmodified':True,'oldOrbitBoundaryComponents':len(boundary_loops),'openBoundaryChains':len(open_chains),
            'eyeAuthoring':eye_authoring}
    return report


def _render(output):
    from mathutils import Vector
    scene=bpy.context.scene;scene.audio_volume=0;scene.render.engine='BLENDER_EEVEE';scene.eevee.use_gtao=True;scene.eevee.gtao_distance=.06;scene.eevee.taa_render_samples=48
    scene.render.resolution_x=scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.view_settings.view_transform='Standard'
    if not scene.world:scene.world=bpy.data.worlds.new('Surgical inspection world')
    scene.world.color=(.15,.15,.15)
    center=Vector((-.008,-.13,.402))
    for name,offset,power in [('Key',(1.3,-2,2),100),('Fill',(-1.4,-1,1),80),('Rim',(0,2,2),100)]:
        data=bpy.data.lights.new(name,'AREA');obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj)
        obj.location=Vector((0,0,0))+Vector(offset);obj.rotation_euler=(center-obj.location).to_track_quat('-Z','Y').to_euler();data.energy=power;data.size=2
    data=bpy.data.cameras.new('Actual corrected high inspection');camera=bpy.data.objects.new('Inspection camera',data);scene.collection.objects.link(camera);scene.camera=camera;data.type='ORTHO'
    for name,offset,scale in [('face',(0,-2,0),.30),('side',(-2,-.25,.04),.34),('threequarter',(.9,-2,.25),.34),('front',(0,-2,-.38),1.10)]:
        target=center if name!='front' else Vector((0,0,0))
        camera.location=target+Vector(offset);camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();data.ortho_scale=scale
        scene.render.filepath=str(output/(name+'.png'));bpy.ops.render.render(write_still=True)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--source',type=Path,default=HERE.parent/'Cyclopse/source-original.glb');parser.add_argument('--output',type=Path,required=True);parser.add_argument('--no-render',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    config=json.loads(CONFIG.read_text(encoding='utf8'))
    if hashlib.sha256(args.source.read_bytes()).hexdigest()!=config['sourceSHA256']:raise ValueError('Canonical source GLB hash differs')
    args.output.mkdir(parents=True,exist_ok=True);bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.scene.audio_volume=0;bpy.ops.import_scene.gltf(filepath=str(args.source),merge_vertices=False)
    meshes=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
    if len(meshes)!=1:raise ValueError('Reviewed surgery source must be one actual body mesh')
    mesh=meshes[0];mesh.name='Cyclopse_CorrectedHigh';report=apply_surgery(mesh)
    (args.output/'surgery-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    (args.output/'eyes-surgery-authoring.json').write_text(json.dumps(report['eyeAuthoring'],indent=2),encoding='utf8')
    bpy.ops.wm.save_as_mainfile(filepath=str(args.output/'Cyclopse_CorrectedHigh.blend'))
    bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);bpy.context.view_layer.objects.active=mesh
    bpy.ops.export_scene.gltf(filepath=str(args.output/'Cyclopse_CorrectedHigh.glb'),export_format='GLB',use_selection=True,export_animations=False)
    if not args.no_render:_render(args.output)
    print('[Cyclopse surgery] PASS '+str(report['removedActualOldOrbitFaces'])+' old orbit faces excised; joined single physical socket',flush=True)


if __name__=='__main__':main()
