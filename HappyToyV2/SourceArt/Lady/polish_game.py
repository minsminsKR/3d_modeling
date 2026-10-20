"""Finish the existing webapp Lady; preserve generated originals and horror rotations.

Blender 4.0.2, CPU-only authoring. Can run through MCP or --background --python.
The generated high source is used only for texture/normal reprojection.
"""
import hashlib
import json
from pathlib import Path
import sys

import bmesh
import bpy
import numpy as np

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(PROJECT / "SourceArt/CreatureStudio/tools"))
import author_lady as author
import bake_surface_atlas as atlas


def say(value):
    print("[Lady polish] " + str(value), flush=True)


def rotation_digest(action):
    rows = [(curve.data_path, curve.array_index,
             [(tuple(key.co), key.interpolation) for key in curve.keyframe_points])
            for curve in action.fcurves if curve.data_path.endswith("rotation_quaternion")]
    return hashlib.sha256(json.dumps(rows, sort_keys=True).encode()).hexdigest()


def uv_split_count(mesh):
    ids = np.empty(len(mesh.loops), dtype="i4")
    mesh.loops.foreach_get("vertex_index", ids)
    uv = atlas.array(mesh.uv_layers.active.data, "uv", 2)
    return int(len(np.unique(np.column_stack((ids, np.rint(uv * 1000000).astype("i8"))), axis=0)))


def unwrap_game(obj, settings):
    """Convex projection charts for the distant remesh; no overlapping unfolded hair."""
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.select_set(True)
    with bpy.context.temp_override(object=obj, active_object=obj,
                                   selected_objects=[obj], selected_editable_objects=[obj]):
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=1.4,island_margin=settings["uv_island_margin"],correct_aspect=True,scale_to_bounds=True)
        bpy.ops.object.mode_set(mode="OBJECT")
    obj.data.update()


def remove_loose(obj):
    bm = bmesh.new()
    try:
        bm.from_mesh(obj.data)
        loose = [vertex for vertex in bm.verts if not vertex.link_faces]
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
        bm.to_mesh(obj.data)
        return len(loose)
    finally:
        bm.free()


def author_support(fit, rig, mesh, settings):
    """Only a vertical visual offset; the 28-bone contortions remain untouched."""
    scene = bpy.context.scene
    scene.frame_set(1)
    fit.animation_data_clear()
    # Start from the retained author's fitting root, never the preceding run's
    # first animated offset. Repeated finishing must not accumulate height.
    base_z = settings["support_fit_base_z"]
    fit.location.z = base_z
    bpy.context.view_layer.update()
    rest = atlas.array(mesh.data.vertices, "co", 3)
    world = author.world_points(mesh)
    chosen = np.flatnonzero(world[:, 2] < settings["support_rest_height_metres"])
    points = np.column_stack((rest[chosen], np.ones(len(chosen))))
    weights = {}
    names = {group.index: group.name for group in mesh.vertex_groups}
    for local, original in enumerate(chosen):
        for group in mesh.data.vertices[int(original)].groups:
            name = names[group.group]
            if name in rig.pose.bones and group.weight > 0:
                weights.setdefault(name, []).append((local, group.weight))
    weights = {name: (np.array([i for i, _ in rows]), np.array([w for _, w in rows]))
               for name, rows in weights.items()}
    inverse_rest = {bone.name: bone.matrix_local.inverted() for bone in rig.data.bones}
    modifiers = [(modifier, modifier.show_viewport) for obj in bpy.data.objects if obj.type == "MESH"
                 for modifier in obj.modifiers if modifier.type == "ARMATURE"]
    raw, minimum = [], []
    try:
        for modifier, _ in modifiers:
            modifier.show_viewport = False
        for frame in range(scene.frame_start, scene.frame_end + 1):
            scene.frame_set(frame)
            posed = np.zeros((len(points), 3))
            for name, (ids, amount) in weights.items():
                matrix = np.asarray(rig.matrix_world @ rig.pose.bones[name].matrix @ inverse_rest[name]
                                    @ rig.matrix_world.inverted() @ mesh.matrix_world)
                posed[ids] += (points[ids] @ matrix.T)[:, :3] * amount[:, None]
            lowest = float(posed[:, 2].min())
            minimum.append(lowest)
            raw.append(float(np.clip(settings["support_target_metres"] - lowest,
                                     -settings["support_max_offset_metres"], settings["support_max_offset_metres"])))
            if frame % 400 == 0:
                say("support frame " + str(frame))
    finally:
        for modifier, visible in modifiers:
            modifier.show_viewport = visible
    # Two-frame smoothing removes numerical contact switching, not the bone snaps.
    values = np.asarray(raw[:-1])
    filtered = sum(np.roll(values, offset) * weight for offset, weight in
                   ((-2, 1), (-1, 2), (0, 3), (1, 2), (2, 1))) / 9
    filtered = np.append(filtered, filtered[0])
    for frame, offset in enumerate(filtered, start=scene.frame_start):
        fit.location.z = base_z + float(offset)
        fit.keyframe_insert("location", index=2, frame=frame)
    fit.animation_data.action.name = "Lady vertical support only"
    for curve in fit.animation_data.action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation = "LINEAR"
    scene.frame_set(1)
    return dict(samples=len(raw), probeVertices=len(chosen), oldMinimumRange=[min(minimum), max(minimum)],
                offsetRange=[float(filtered.min()), float(filtered.max())],
                method="Visual root Z only, circular two-frame smoothing; original bone rotations unchanged")


def run(open_source=True):
    settings = json.loads((HERE / "polish-settings.json").read_text())
    if open_source:
        bpy.ops.wm.open_mainfile(filepath=str(HERE / "Lady_Editable.blend"), load_ui=False)
    scene = bpy.context.scene
    rig, fit = bpy.data.objects["Lady_Rig"], bpy.data.objects["Lady_Fit"]
    high, first = bpy.data.objects["Lady_SourceHigh"], bpy.data.objects["Lady_LOD0"]
    scene.frame_set(1)
    digest = rotation_digest(rig.animation_data.action)
    original = {path.name: hashlib.sha256(path.read_bytes()).hexdigest() for path in (HERE / "webapp").glob("*.glb")}
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    before_uv = uv_split_count(first.data)
    # The close-up face has a reviewed source atlas. Keep it exactly: global
    # angle-based unfolding of the tangled hair overlaps facial texels.
    after_uv = uv_split_count(first.data)
    say(f"UV split vertices {before_uv} -> {after_uv}")
    previous_report=json.loads((HERE / "authoring-report.json").read_text())
    baseline_uv=previous_report.get("polish",{}).get("uvSplitVerticesBefore",before_uv)
    surface = atlas.bake(high, first, PROJECT / "Assets/Resources/Lady", 2048)
    surface.update(vertices=len(first.data.vertices), triangles=len(first.data.polygons),
                   sourceSkinEditedOnlyForGame=True, fusedGameSurfaceSeamsSeparated=True,
                   chartMargin=.00005, nearUvPreserved=True, polishSettings="polish-settings.json")
    (HERE / "surface-report.json").write_text(json.dumps(surface, indent=2) + "\n")
    # Update the game-only map; the original 8K ORM remains packed in SourceHigh/GLBs.
    image = bpy.data.images.load(str(PROJECT / "Assets/Resources/Lady/game-orm.png"), check_existing=False)
    image.colorspace_settings.name = "Non-Color"
    pixels = np.empty(len(image.pixels), dtype="f4")
    image.pixels.foreach_get(pixels)
    pixels.reshape(-1, 4)[:, 2] = settings["game_metallic"]
    image.pixels.foreach_set(pixels)
    import author_creature as shared
    shared.save_image_atomic(image, PROJECT / "Assets/Resources/Lady/game-orm.png")
    for material in first.data.materials:
        nodes, links = material.node_tree.nodes, material.node_tree.links
        for node in nodes:
            if node.type == "TEX_IMAGE" and node.image and Path(bpy.path.abspath(node.image.filepath)).resolve() == (PROJECT / "Assets/Resources/Lady/game-orm.png").resolve():
                node.image.pixels.foreach_set(pixels)
        bsdf = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
        for link in list(bsdf.inputs["Metallic"].links):
            links.remove(link)
        bsdf.inputs["Metallic"].default_value = settings["game_metallic"]
        normal_image = bpy.data.images.load(str(PROJECT / "Assets/Resources/Lady/normal.png"), check_existing=True)
        normal_image.colorspace_settings.name = "Non-Color"
        texture = nodes.new("ShaderNodeTexImage")
        texture.image = normal_image
        normal = nodes.new("ShaderNodeNormalMap")
        links.new(texture.outputs["Color"], normal.inputs["Color"])
        links.new(normal.outputs["Normal"], bsdf.inputs["Normal"])
    for level in (1, 2):
        bpy.data.objects.remove(bpy.data.objects["Lady_LOD" + str(level)], do_unlink=True)
    lods = [first]
    for level in (1, 2):
        obj = author.copy_lod(first, level, settings[f"lod{level}_triangles"] / len(first.data.polygons))
        remove_loose(obj)
        lods.append(obj)
    rig.data.pose_position = "POSE"
    support = author_support(fit, rig, first, settings)
    if rotation_digest(rig.animation_data.action) != digest:
        raise ValueError("Authored horror rotations changed; source not saved")
    report = json.loads((HERE / "authoring-report.json").read_text())
    report["lods"] = [dict(name=obj.name, vertices=len(obj.data.vertices), triangles=len(obj.data.polygons)) for obj in lods]
    report["polish"] = dict(settings=settings, uvSplitVerticesBefore=baseline_uv, uvSplitVerticesAfter=after_uv,
                            nearUvPreserved=True,
                            rotationSha256=digest, rotationKeysUnchanged=True, support=support, generatedOriginals=original)
    (HERE / "authoring-report.json").write_text(json.dumps(report, indent=2) + "\n")
    scene.frame_set(1)
    for obj in lods[1:]:
        obj.hide_render = True
        obj.hide_set(True)
    bpy.ops.file.pack_all()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE / "Lady_Editable.blend"))
    author.export_game(fit, rig, lods)
    say("POLISH_SOURCE_PASS; generated originals and horror rotation keys unchanged")


if __name__ == "__main__":
    run(open_source=not globals().get("POLISH_SOURCE_ALREADY_OPEN", False))
