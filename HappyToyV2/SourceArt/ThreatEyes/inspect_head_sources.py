"""Read-only Blender 4.0.2 rig/head placement inventory for the red eye visuals.

Default sources are repository-relative existing editable refinement .blend files.
--resource-project permits a read-only review staging dependency root. Optional
previews go only to the supplied outside-source directory. No .blend is saved.
"""
from pathlib import Path
import argparse
import json
import sys
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

PROJECT = Path(__file__).resolve().parents[2]
# Measured on the optional 800px front source crop, not on a guessed bone axis.
# Cyclopse's source has one painted eye: two cores flank its orbital band.
EYE_PIXELS = {
    'Cyclopse': ((354, 427), (453, 427)),
    'Uncat': ((344, 468), (444, 453)),
    'Hwacat_angry': ((378, 467), (531, 444)),
    'Baby': ((290, 396), (466, 393)),
}


def source_eye_uv(scene, meshes, pixel):
    camera = scene.camera
    rotation = camera.rotation_euler.to_quaternion()
    origin = camera.location + rotation @ Vector(((pixel[0] / 800 - .5) * camera.data.ortho_scale,
        (.5 - pixel[1] / 800) * camera.data.ortho_scale, 0))
    direction = rotation @ Vector((0, 0, -1))
    nearest = None
    for obj in meshes:
        evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get()); posed = evaluated.to_mesh()
        posed.calc_loop_triangles()
        points = [evaluated.matrix_world @ vertex.co for vertex in posed.vertices]
        indices = [tuple(triangle.vertices) for triangle in posed.loop_triangles]
        tree = BVHTree.FromPolygons(points, indices, all_triangles=True)
        point, normal, face, distance = tree.ray_cast(origin, direction)
        if point is not None and (nearest is None or distance < nearest['distance']):
            triangle = posed.loop_triangles[face]
            a, b, c = (points[i] for i in triangle.vertices)
            ab, ac, ap = b-a, c-a, point-a
            d00, d01, d11 = ab.dot(ab), ab.dot(ac), ac.dot(ac)
            denominator = d00*d11-d01*d01
            v = (d11*ap.dot(ab)-d01*ap.dot(ac))/denominator
            w = (d00*ap.dot(ac)-d01*ap.dot(ab))/denominator
            weights = (1-v-w, v, w)
            uv = sum((posed.uv_layers.active.data[loop].uv * weight for loop, weight in zip(triangle.loops, weights)), Vector((0, 0)))
            nearest = dict(distance=distance, uv=list(uv), posedWorldPoint=list(point),
                posedWorldNormal=list(normal), sourceMesh=obj.name, sourcePixel=list(pixel),
                materialSlot=triangle.material_index)
        evaluated.to_mesh_clear()
    if nearest is None: raise RuntimeError('Eye source pixel misses the posed mesh: '+str(pixel))
    return nearest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--resource-project', type=Path, default=PROJECT)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--preview-dir', type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    records = []
    for key in ('Cyclopse', 'Uncat', 'Hwacat_angry', 'Baby'):
        path = args.resource_project / ('SourceArt/EnemyRefinementV2/' + key + '-refinement-v2.blend')
        bpy.ops.wm.open_mainfile(filepath=str(path)); scene = bpy.context.scene; scene.frame_set(1)
        rig = next(obj for obj in scene.objects if obj.type == 'ARMATURE')
        head = next(bone for bone in rig.pose.bones if bone.name.endswith(':Head'))
        meshes = [obj for obj in scene.objects if obj.type == 'MESH' and obj.name != 'Proof floor']
        points = []
        for obj in meshes:
            group = obj.vertex_groups.get(head.name)
            if not group: continue
            evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get()); posed = evaluated.to_mesh()
            for vertex in obj.data.vertices:
                if any(item.group == group.index and item.weight > .30 for item in vertex.groups):
                    points.append(evaluated.matrix_world @ posed.vertices[vertex.index].co)
            evaluated.to_mesh_clear()
        lo = Vector(tuple(min(point[i] for point in points) for i in range(3)))
        hi = Vector(tuple(max(point[i] for point in points) for i in range(3)))
        head_matrix = rig.matrix_world @ head.matrix
        record = dict(key=key, source=path.relative_to(args.resource_project).as_posix(), headBone=head.name,
            actualEyeBones=[bone.name for bone in rig.pose.bones if 'eye' in bone.name.lower()],
            supportedHeadVertices=len(points), posedWorldBounds=dict(min=list(lo), max=list(hi)),
            headWorldPosition=list(head_matrix.translation), headWorldRotation=list(head_matrix.to_quaternion()),
            sourcePose='existing refinement scene frame1; no source deformation/bone/clip change')
        records.append(record)
        if True:
            centre = (lo + hi) / 2; height = max((hi - lo).z, (hi - lo).x)
            camera = scene.camera
            camera.location = centre + Vector((0, -height * 2.5, height * .02))
            camera.rotation_euler = (centre - camera.location).to_track_quat('-Z', 'Y').to_euler()
            camera.data.type = 'ORTHO'; camera.data.ortho_scale = height * 1.30
            bpy.context.view_layer.update()
            record['eyeAnchors'] = [source_eye_uv(scene, meshes, pixel) for pixel in EYE_PIXELS[key]]
        if args.preview_dir:
            args.preview_dir.mkdir(parents=True, exist_ok=True)
            scene.render.engine = 'BLENDER_EEVEE'; scene.render.resolution_x = 800; scene.render.resolution_y = 800
            scene.render.resolution_percentage = 100; scene.render.filepath = str(args.preview_dir / (key + '-head-front.png'))
            bpy.ops.render.render(write_still=True)
        print('SOURCE_HEAD_INSPECTED', key, len(points), flush=True)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(dict(blender=bpy.app.version_string, assets=records,
        scope='Read-only posed head vertices and rig inventory. Optional source studio head crops are not native Unity acceptance.'), indent=2), encoding='utf-8')


if __name__ == '__main__': main()
