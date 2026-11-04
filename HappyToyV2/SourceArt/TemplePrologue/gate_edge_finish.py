"""Bridge the cut boundary of a source-derived gate leaf for review.

The original front and rear remain intact. Each generated strip follows the
nearest existing boundary segments, and is part of the same animated mesh.
"""
import bmesh
from mathutils import Vector


def finish_edges(leaf, half_width, bottom, top, coefficients):
    bm = bmesh.new()
    bm.from_mesh(leaf.data)
    uv_layer = bm.loops.layers.uv.active
    if not uv_layer:
        raise RuntimeError('Gate leaf has no UV atlas')
    offset = leaf.location.copy()
    result = []
    outer = -half_width if leaf.name.startswith('Left') else half_width
    for axis, plane, normal in (
        (0, 0, Vector((1 if outer < 0 else -1, 0, 0))),
        (0, outer, Vector((-1 if outer < 0 else 1, 0, 0))),
        (2, bottom, Vector((0, 0, -1))),
        (2, top, Vector((0, 0, 1))),
    ):
        along = 2 if axis == 0 else 0
        segments = [[], []]
        for edge in bm.edges:
            if len(edge.link_faces) != 1:
                continue
            a, b = (v.co + offset for v in edge.verts)
            if (abs(a[axis]-plane) > 1e-4 or abs(b[axis]-plane) > 1e-4 or
                    abs(a[along]-b[along]) < 1e-7 or (a.y < -1.5) != (b.y < -1.5)):
                continue
            segments[0 if a.y < -1.5 else 1].append((a.copy(), b.copy()))
        if not all(segments):
            raise RuntimeError('Missing front or rear boundary on ' + leaf.name)
        positions = sorted(set(round(v[along], 6) for side in segments for pair in side for v in pair))
        max_gap = 0.0
        count = 0
        for start, end in zip(positions, positions[1:]):
            if end-start < 1e-6:
                continue
            mid = (start+end)*.5
            chosen = []
            for index, side in enumerate(segments):
                ranked = []
                for a, b in side:
                    low, high = sorted((a[along], b[along]))
                    distance = max(low-mid, 0, mid-high)
                    t = min(1, max(0, (mid-a[along])/(b[along]-a[along])))
                    y = a.y + (b.y-a.y)*t
                    ranked.append((distance, -y if index == 0 else y, a, b))
                distance, _, a, b = min(ranked, key=lambda item: item[:2])
                max_gap = max(max_gap, distance)
                if distance > 1e-5:
                    points = [v for pair in side for v in pair]
                    lower = [v for v in points if v[along] <= mid]
                    upper = [v for v in points if v[along] >= mid]
                    if lower and upper:
                        a = max(lower, key=lambda p: p[along])
                        b = min(upper, key=lambda p: p[along])
                chosen.append((a, b))
            def point(pair, position):
                a, b = pair
                t = min(1, max(0, (position-a[along])/(b[along]-a[along])))
                p = a.lerp(b, t)
                p[along] = position
                p[axis] = plane
                return p
            front_a, front_b = (point(chosen[0], p) for p in (start, end))
            back_a, back_b = (point(chosen[1], p) for p in (start, end))
            if min(back_a.y-front_a.y, back_b.y-front_b.y) <= .05:
                raise RuntimeError('Unexpected edge depth on ' + leaf.name)
            coords = [front_a, back_a, back_b, front_b]
            if (coords[1]-coords[0]).cross(coords[2]-coords[0]).dot(normal) < 0:
                coords.reverse()
            face = bm.faces.new([bm.verts.new(p-offset) for p in coords])
            for loop, p in zip(face.loops, coords):
                # The generated atlas is irregular at the cut itself. Sample
                # an interior strip of the existing planar wood instead.
                sample_x = outer*.5 if axis == 0 else p.x
                sample_z = p.z if axis == 0 else (bottom+top)*.5
                u = coefficients[0][0]*sample_x + coefficients[1][0]*sample_z + coefficients[2][0]
                v = coefficients[0][1]*sample_x + coefficients[1][1]*sample_z + coefficients[2][1]
                if axis == 0: u += (p.y-front_a.y)*.0169
                else: v += (p.y-front_a.y)*.0169
                loop[uv_layer].uv = (u, v)
            count += 1
        if max_gap > .25:
            raise RuntimeError('Boundary has an unexpectedly large missing interval: ' + str((leaf.name, axis, plane, max_gap)))
        result.append(dict(axis=axis, plane=plane, strips=count, maxBoundaryGap=max_gap))
    bm.to_mesh(leaf.data)
    bm.free()
    leaf.data.update()
    return result
