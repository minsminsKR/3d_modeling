"""Check source-preserving tree candidate geometry and FBX handoff.

Blender 4.0.2 CPU check. Does not certify Unity import, camera appearance,
visible LOD transition, collision, or frame rate.
"""
import hashlib
import json
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

root = Path(__file__).resolve().parent
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
source_hash = 'c944ed7981599293a40a1d9e9613fd8bc35f654bad1018edcb21897663b6eaf6'
if sha(root / 'original.glb') != source_hash:
    raise RuntimeError('Original generated GLB checksum changed')
blend = root / 'wide-zelkova-safe.blend'
fbx = root / 'safe-export' / 'wide-zelkova-safe-lod0.fbx'
bpy.ops.wm.open_mainfile(filepath=str(blend))
source = next(obj for obj in bpy.data.collections['Complete unchanged webapp original'].objects if obj.type == 'MESH')
safe = bpy.data.objects['CE_wide-zelkova-safe_LOD0']


def arrays(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    mesh.calc_normals_split()
    positions = np.empty(len(mesh.vertices) * 3, np.float32)
    mesh.vertices.foreach_get('co', positions)
    positions = positions.reshape(-1, 3)
    matrix = np.asarray(obj.matrix_world, dtype=np.float32)
    positions = positions @ matrix[:3, :3].T + matrix[:3, 3]
    indices = np.empty(len(mesh.loop_triangles) * 3, np.int32)
    mesh.loop_triangles.foreach_get('vertices', indices)
    triangles = positions[indices.reshape(-1, 3)]
    normals = np.empty(len(mesh.loops) * 3, np.float32)
    mesh.loops.foreach_get('normal', normals)
    normals = normals.reshape(-1, 3) @ matrix[:3, :3].T
    lengths = np.linalg.norm(normals, axis=1)
    normals /= np.maximum(lengths[:, None], 1e-20)
    if not np.isfinite(triangles).all() or not np.isfinite(normals).all():
        raise RuntimeError('Nonfinite geometry')
    if not mesh.uv_layers.active:
        raise RuntimeError('Source atlas missing')
    return triangles, normals, lengths


source_tri, source_normals, source_lengths = arrays(source)
safe_tri, safe_normals, safe_lengths = arrays(safe)
lo = source_tri.reshape(-1, 3).min(axis=0)
hi = source_tri.reshape(-1, 3).max(axis=0)
centre = np.asarray(((lo[0] + hi[0]) * .5, (lo[1] + hi[1]) * .5, lo[2]), np.float32)
source_tri = (source_tri - centre) * (8 / (hi[2] - lo[2]))
if source_tri.shape != safe_tri.shape or source_normals.shape != safe_normals.shape:
    raise RuntimeError('Source and safe topology no longer correspond')
delta = np.linalg.norm(source_tri - safe_tri, axis=2)
valid = (source_lengths > 1e-6) & (safe_lengths > 1e-6)
dot = np.clip(np.einsum('ij,ij->i', source_normals[valid], safe_normals[valid]), -1, 1)
if float(delta.max()) > .0001 or int((safe_lengths < 1e-6).sum()) != 0:
    raise RuntimeError('Safe candidate altered source positions or has zero normals')
if int((dot < .99).sum()) > 1000:
    raise RuntimeError('Normal repair affected more than its immediate local area')


def geometry(triangles):
    a, b, c = triangles[:, 0], triangles[:, 1], triangles[:, 2]
    edges = np.stack((np.linalg.norm(a - b, axis=1), np.linalg.norm(b - c, axis=1),
                      np.linalg.norm(c - a, axis=1)), axis=1).max(axis=1)
    area = np.linalg.norm(np.cross(b - a, c - a), axis=1) * .5
    return dict(triangles=len(triangles), area=float(area.sum(dtype=np.float64)),
                maxEdge=float(edges.max()), overHalfMetre=int((edges > .5).sum()))


source_geometry = geometry(source_tri)
safe_geometry = geometry(safe_tri)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(fbx))
models = [obj for obj in bpy.data.objects if obj.type == 'MESH']
if len(models) != 1:
    raise RuntimeError('Expected one mesh on FBX reimport')
export_tri, _, export_lengths = arrays(models[0])
export_geometry = geometry(export_tri)
if export_geometry['triangles'] != safe_geometry['triangles'] or int((export_lengths < 1e-6).sum()) != 0:
    raise RuntimeError('FBX import lost triangles or contains zero normals')
if abs(export_geometry['area'] / safe_geometry['area'] - 1) > .005:
    raise RuntimeError('FBX import changed source surface area by more than 0.5%')
if export_geometry['overHalfMetre'] != 0:
    raise RuntimeError('Unexpected oversized source triangle after FBX import')
proof = dict(scope='Full source-correspondence and Blender FBX reimport, not a Unity/performance approval',
    sourceSha256=source_hash, blendSha256=sha(blend), fbxSha256=sha(fbx), source=source_geometry,
    editable=safe_geometry, reimport=export_geometry, maxPositionErrorMetres=float(delta.max()),
    sourceZeroNormalLoops=int((source_lengths < 1e-6).sum()), safeZeroNormalLoops=int((safe_lengths < 1e-6).sum()),
    normalsDotBelow099=int((dot < .99).sum()), normalsDotP01=float(np.percentile(dot, 1)))
(root / 'safe-surface-proof.json').write_text(json.dumps(proof, indent=2) + '\n', encoding='utf-8')
print('WIDE_SAFE_SURFACE', json.dumps(proof), flush=True)
