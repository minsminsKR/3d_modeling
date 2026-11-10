"""Measure thumb silhouette sections in the neutral authoring mesh.

Run in Blender background mode. Widths are dorsal projection measurements, not
3D scans or proof that the nail stays centered in every animated pose.
"""
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

source = Path(__file__).resolve().parent.parent / 'hand-rig-prototype-v7.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
hand = bpy.data.objects['Blender anatomical hand candidate']
surface = BVHTree.FromPolygons([vertex.co for vertex in hand.data.vertices],
                               [list(face.vertices) for face in hand.data.polygons])
# Center and perpendicular/longitudinal directions used by bake_nails.py.
for along in (-.005, 0, .005):
    x = .0830 + .686 * along
    z = .1339 + .727 * along
    samples = []
    for step in range(-200, 201):
        across = step * .0001
        origin = Vector((x + .727 * across, .1, z - .686 * across))
        hit, _, _, _ = surface.ray_cast(origin, Vector((0, -1, 0)), .2)
        if hit is not None:
            samples.append(across)
    if not samples:
        raise RuntimeError(f'No thumb surface at longitudinal offset {along}')
    low, high = min(samples), max(samples)
    print(f'along={along:.4f} width=[{low:.4f},{high:.4f}] center_offset={(low + high) / 2:.5f} m')
