"""CPU-only Lady snapshot in a disposable GUI Blender MCP session; never saves art."""
from collections import Counter
import json
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Quaternion, Vector


def snapshot():
    project = Path(__file__).resolve().parents[2]
    source = project / "SourceArt/Lady/Lady_Editable.blend"
    names = {"Lady_Rig", "Lady_Fit", "Lady_LOD0", "Lady_LOD1", "Lady_LOD2"}
    # Do not load the million-triangle original or its viewport/scene settings.
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                area.type = "CONSOLE"
    if not names.issubset(bpy.data.objects.keys()):
        with bpy.data.libraries.load(str(source), link=False) as (available, selected):
            selected.objects = [name for name in available.objects if name in names]
        for obj in selected.objects:
            if obj is not None:
                bpy.context.scene.collection.objects.link(obj)
    rig = bpy.data.objects["Lady_Rig"]
    action = rig.animation_data.action
    deform = {bone.name for bone in rig.data.bones if bone.use_deform}
    meshes = []
    for name in sorted(names):
        obj = bpy.data.objects[name]
        if obj.type != "MESH":
            continue
        mesh = obj.data
        mesh.calc_loop_triangles()
        loop_indices = np.empty(len(mesh.loops), dtype=np.int32)
        mesh.loops.foreach_get("vertex_index", loop_indices)
        loop_uv = np.empty(len(mesh.loops) * 2, dtype=np.float32)
        mesh.uv_layers.active.data.foreach_get("uv", loop_uv)
        uv_keys = np.column_stack((loop_indices, np.rint(loop_uv.reshape(-1, 2) * 1000000).astype(np.int64)))
        uv_split_vertices = int(len(np.unique(uv_keys, axis=0)))
        groups = {group.index: group.name for group in obj.vertex_groups}
        influences = Counter()
        totals = []
        rigid_head = 0
        for vertex in mesh.vertices:
            weights = [(groups[group.group], group.weight) for group in vertex.groups
                       if groups[group.group] in deform and group.weight > 0.0001]
            influences[len(weights)] += 1
            totals.append(sum(value for _, value in weights))
            rigid_head += len(weights) == 1 and weights[0][0] == "Head"
        topology = bmesh.new()
        try:
            topology.from_mesh(mesh)
            boundary = sum(edge.is_boundary for edge in topology.edges)
            complex_edges = sum(len(edge.link_faces) > 2 for edge in topology.edges)
            loose = sum(not vertex.link_faces for vertex in topology.verts)
        finally:
            topology.free()
        bounds = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
        meshes.append(dict(name=name, vertices=len(mesh.vertices), triangles=len(mesh.loop_triangles),
                           uv_split_vertex_estimate=uv_split_vertices,
                           boundary_edges=boundary, edges_with_more_than_two_faces=complex_edges,
                           loose_vertices=loose, influences=dict(influences),
                           weight_sum_min=min(totals), weight_sum_max=max(totals),
                           rigid_head_vertices=rigid_head,
                           world_bounds_min=[min(p[i] for p in bounds) for i in range(3)],
                           world_bounds_max=[max(p[i] for p in bounds) for i in range(3)],
                           uv_layers=[layer.name for layer in mesh.uv_layers]))
    start, end = action.frame_range
    seams = []
    for bone in rig.pose.bones:
        path = bone.path_from_id("rotation_quaternion")
        curves = {curve.array_index: curve for curve in action.fcurves if curve.data_path == path}
        if len(curves) == 4:
            first = Quaternion([curves[i].evaluate(start) for i in range(4)])
            last = Quaternion([curves[i].evaluate(end) for i in range(4)])
            seams.append(dict(bone=bone.name, degrees=first.rotation_difference(last).angle * 180 / 3.141592653589793))
    return dict(source=str(source), blender_version=bpy.app.version_string,
                scope="CPU mesh/weights/topology and curve endpoints; no renders or saved model changes",
                bones=len(rig.data.bones), deform_bones=len(deform), meshes=meshes,
                action=dict(name=action.name, frames=[start, end], curves=len(action.fcurves),
                            keyframes=sum(len(curve.keyframe_points) for curve in action.fcurves),
                            loop_seams=sorted(seams, key=lambda item: item["degrees"], reverse=True)))


def material_regions():
    """Triangle-centroid samples of the raw ORM; no shading or GPU readback."""
    project = Path(__file__).resolve().parents[2]
    obj = bpy.data.objects["Lady_LOD0"]
    mesh = obj.data
    image = bpy.data.images.load(str(project / "Assets/Resources/Lady/game-orm.png"), check_existing=True)
    image.colorspace_settings.name = "Non-Color"
    pixels = np.empty(len(image.pixels), dtype=np.float32)
    image.pixels.foreach_get(pixels)
    pixels = pixels.reshape(image.size[1], image.size[0], 4)
    vertices = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.vertices.foreach_get("co", vertices)
    vertices = vertices.reshape(-1, 3)
    loops = np.empty(len(mesh.loops), dtype=np.int32)
    mesh.loops.foreach_get("vertex_index", loops)
    uv = np.empty(len(mesh.uv_layers.active.data) * 2, dtype=np.float32)
    mesh.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 3, 2).mean(axis=1)
    points = vertices[loops].reshape(-1, 3, 3).mean(axis=1)
    matrix = np.array(obj.matrix_world)
    points = points @ matrix[:3, :3].T + matrix[:3, 3]
    x = np.clip((uv[:, 0] * image.size[0]).astype(int), 0, image.size[0] - 1)
    y = np.clip((uv[:, 1] * image.size[1]).astype(int), 0, image.size[1] - 1)
    values = pixels[y, x, :3]
    masks = {"upper_head": points[:, 2] > 1.8,
             "central_robe": (points[:, 2] > .3) & (points[:, 2] < 1.5) & (np.abs(points[:, 0]) < .18),
             "feet": points[:, 2] < .20}
    regions = {name: dict(triangles=int(mask.sum()), metallic_mean=float(values[mask, 2].mean()),
                          metallic_p95=float(np.quantile(values[mask, 2], .95)),
                          roughness_mean=float(values[mask, 1].mean())) for name, mask in masks.items()}
    return dict(method="Raw linear ORM sampled at triangle UV centroids; not area weighted or visually rendered",
                ao_min=float(pixels[:, :, 0].min()), ao_max=float(pixels[:, :, 0].max()), regions=regions)


report = snapshot()
report["material_regions"] = material_regions()
destination = Path(globals().get("LADY_REPORT", Path(__file__).resolve().parents[2] / "Builds/LadyMcpReview/blender.json"))
destination.parent.mkdir(parents=True, exist_ok=True)
destination.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
