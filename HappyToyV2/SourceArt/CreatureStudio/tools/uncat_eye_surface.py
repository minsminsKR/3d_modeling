"""Create the missing narrow Uncat ocular surface with actual reviewed eye UVs.

The generated source's opposite orbit is filled by opaque hair, with no visible
ocular surface. This explicit little convex lens and dark rim use the existing
exposed eye's PBR atlas, a distinct material slot, and rigid Head ownership. The
raw GLB is preserved. Reapply after game decimation so the small eye is retained.
"""
import json
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector


def apply_surface(mesh, config_path=None, normalization=None):
    folder = Path(__file__).resolve().parent.parent/'Uncat'
    config = json.loads(Path(config_path or folder/'eye-surface.json').read_text(encoding='utf8'))
    author_path = folder/'eyes-authoring.json'
    authoring = json.loads(author_path.read_text(encoding='utf8'))
    source_eye = authoring['eyes'][0]
    head_group = mesh.vertex_groups.get('mixamorig:Head')
    if not head_group:
        raise ValueError('The actual new ocular surface needs rigid Head binding')
    if len(mesh.data.materials) < 2:
        material = mesh.data.materials[0].copy()
        material.name = 'Uncat_OcularSurface'
        mesh.data.materials.append(material)
    data = bmesh.new()
    data.from_mesh(mesh.data)
    deform = data.verts.layers.deform.verify()
    uv_layer = data.loops.layers.uv.active
    # The generator owns slot 1 only. Running after decimation replaces its
    # reduced fragments with the exact small reviewed ocular surface.
    previous = [face for face in data.faces if face.material_index == 1]
    if previous:
        bmesh.ops.delete(data, geom=previous, context='FACES')
    loose = [vertex for vertex in data.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(data, geom=loose, context='VERTS')
    center = np.array(config['centerGLTF'], dtype=float)
    radius = np.array(config['radiiGLTF'], dtype=float)
    segments = config['segments']
    def transformed(point):
        value = np.array([point[0], -point[2], point[1]])
        if normalization:
            bounds = np.array(normalization['sourceBounds'])
            shift = (bounds[0]+bounds[1])/2
            shift[2] = bounds[0, 2]
            value = (value-shift)*normalization['uniformScale']
        return Vector(value)
    def vertex(point):
        value = data.verts.new(transformed(point))
        value[deform][head_group.index] = 1
        return value
    hub = vertex(center+np.array([0, 0, config['convexDepthGLTF']]))
    inner, outer = [], []
    for index in range(segments):
        angle = 2*np.pi*index/segments
        offset = np.array([np.cos(angle)*radius[0], np.sin(angle)*radius[1], 0])
        inner.append(vertex(center+offset))
        outer.append(vertex(center+offset*np.array([1.05, 1.32, 1])+np.array([0, 0, -.00025])))
    # Use corners of a real reviewed exposed-eye triangle, never unrelated body
    # UVs. Material-slot-aware runtime anchors distinguish the reused atlas.
    actual_uv = source_eye['rasterTriangles'][config['sourceTriangleUVIndex']]
    iris_uv = [Vector((point['x'], point['y'])) for point in actual_uv]
    dark_uv = Vector(config['darkRimUV'])
    for index in range(segments):
        following = (index+1)%segments
        face = data.faces.new((hub, inner[index], inner[following]))
        face.material_index = 1
        for loop, uv in zip(face.loops, iris_uv):
            loop[uv_layer].uv = uv
        face.smooth = True
        for triple in ((inner[index], outer[index], outer[following]),
                       (inner[index], outer[following], inner[following])):
            rim = data.faces.new(triple)
            rim.material_index = 1
            for loop in rim.loops:
                loop[uv_layer].uv = dark_uv
            rim.smooth = True
    data.to_mesh(mesh.data)
    data.free()
    mesh.data.update()
    if mesh.data.validate(clean_customdata=True):
        raise ValueError('Generated ocular surface changed at topology validation')
    # Keep one source authoring record for each actual surface. Both rasterize
    # real iris atlas corners, but the clone has its own actual material slot.
    clone = {key: value for key, value in source_eye.items() if key != 'sourceTriangleIndices'}
    clone.update(materialSlot=1, sourceCenterGLTF=center.tolist(), sourceRadius=float(np.linalg.norm(radius)),
                 sourceTriangleCount=segments, generatedActualOcularSurface=True,
                 uv={'x':float(sum(point['x'] for point in actual_uv)/3),
                     'y':float(sum(point['y'] for point in actual_uv)/3)},
                 rasterTriangles=[actual_uv]*segments, blackTriangles=[])
    source_eye['materialSlot'] = 0
    authoring['eyes'] = [source_eye, clone]
    authoring['newEyeSurfaceGenerator'] = 'tools/uncat_eye_surface.py'
    authoring['newEyeSurfaceLandmarks'] = 'eye-surface.json'
    author_path.write_text(json.dumps(authoring, indent=2), encoding='utf8')
    return {'centerGLTF': center.tolist(), 'sourceIrisAtlasReused': True, 'materialSlot': 1,
            'lensFaces': segments, 'darkRimFaces': segments*2, 'rigidBone': 'mixamorig:Head',
            'originalGLBModified': False, 'gameSmallEyePreservedAfterDecimation': bool(normalization)}
