"""Render actual Unity CPU-baked OBJ hand poses in an owned background Blender.

No GPU, native player, live Blender scene, or Unity render is used. Run with
Blender 4.0.2 --background --threads 2 --python this_file -- --manifest ... .
"""
from __future__ import annotations
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys
import time

import bpy
from mathutils import Vector


def sha(path):
    digest = hashlib.sha256()
    with open(path, "rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def camera_vector(values):
    # Unity camera +Z forward/+Y up -> Blender +Y forward/+Z up.
    return (values[0], values[2], values[1])


def image(path, colour=True):
    result = bpy.data.images.load(str(path), check_existing=True)
    result.colorspace_settings.name = "sRGB" if colour else "Non-Color"
    return result


def material(record):
    result = bpy.data.materials.new(record["name"])
    result.use_nodes = True
    nodes, links = result.node_tree.nodes, result.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    tint = record.get("tint", {"r": 1, "g": 1, "b": 1, "a": 1})
    colour = (tint["r"], tint["g"], tint["b"], tint["a"])
    coord = nodes.new("ShaderNodeTexCoord")
    scale, offset = record.get("uvScale", {"x": 1, "y": 1}), record.get("uvOffset", {"x": 0, "y": 0})
    multiply = nodes.new("ShaderNodeVectorMath"); multiply.operation = "MULTIPLY"
    multiply.inputs[1].default_value = (scale["x"], scale["y"], 1)
    links.new(coord.outputs["UV"], multiply.inputs[0])
    add = nodes.new("ShaderNodeVectorMath"); add.operation = "ADD"
    add.inputs[1].default_value = (offset["x"], offset["y"], 0)
    links.new(multiply.outputs["Vector"], add.inputs[0])

    def texture(path, colour=True):
        node = nodes.new("ShaderNodeTexImage"); node.image = image(path, colour)
        links.new(add.outputs["Vector"], node.inputs["Vector"])
        return node

    albedo = texture(record["albedo"]) if record.get("albedo") else None
    tint_node = nodes.new("ShaderNodeMixRGB"); tint_node.blend_type = "MULTIPLY"
    tint_node.inputs[0].default_value = 1; tint_node.inputs[2].default_value = colour
    if albedo: links.new(albedo.outputs["Color"], tint_node.inputs[1])
    else: tint_node.inputs[1].default_value = (1, 1, 1, 1)
    if record.get("flame"):
        emission, transparent, mix = nodes.new("ShaderNodeEmission"), nodes.new("ShaderNodeBsdfTransparent"), nodes.new("ShaderNodeMixShader")
        links.new(tint_node.outputs["Color"], emission.inputs["Color"])
        emission.inputs["Strength"].default_value = 2
        alpha = nodes.new("ShaderNodeMath"); alpha.operation = "MULTIPLY"
        alpha.inputs[1].default_value = record.get("opacity", 1) * colour[3]
        if albedo: links.new(albedo.outputs["Alpha"], alpha.inputs[0])
        else: alpha.inputs[0].default_value = 1
        links.new(alpha.outputs[0], mix.inputs[0]); links.new(transparent.outputs[0], mix.inputs[1])
        links.new(emission.outputs[0], mix.inputs[2]); links.new(mix.outputs[0], output.inputs["Surface"])
    else:
        principled = nodes.new("ShaderNodeBsdfPrincipled")
        colour_output = tint_node.outputs["Color"]
        if record.get("monochrome", 0) > 0:
            gray = nodes.new("ShaderNodeRGBToBW"); links.new(colour_output, gray.inputs["Color"])
            mix_gray = nodes.new("ShaderNodeMixRGB"); mix_gray.inputs[0].default_value = record["monochrome"]
            links.new(colour_output, mix_gray.inputs[1]); links.new(gray.outputs[0], mix_gray.inputs[2]); colour_output = mix_gray.outputs[0]
        links.new(colour_output, principled.inputs["Base Color"])
        principled.inputs["Metallic"].default_value = record.get("metallic", 0)
        principled.inputs["Roughness"].default_value = 1 - record.get("smoothness", .35)
        if record.get("normal"):
            normal = nodes.new("ShaderNodeNormalMap")
            normal.inputs["Strength"].default_value = record.get("normalScale", 1)
            links.new(texture(record["normal"], False).outputs["Color"], normal.inputs["Color"])
            links.new(normal.outputs["Normal"], principled.inputs["Normal"])
        if record.get("metalRough"):
            packed = texture(record["metalRough"], False)
            channels = nodes.new("ShaderNodeSeparateColor"); links.new(packed.outputs["Color"], channels.inputs["Color"])
            if record.get("pbrLayout", 0) > .5:
                smooth = nodes.new("ShaderNodeMath"); smooth.operation = "MULTIPLY"
                smooth.inputs[1].default_value = record.get("smoothness", .45)
                links.new(packed.outputs["Alpha"], smooth.inputs[0])
                inverse = nodes.new("ShaderNodeMath"); inverse.operation = "SUBTRACT"; inverse.inputs[0].default_value = 1
                links.new(smooth.outputs[0], inverse.inputs[1]); links.new(inverse.outputs[0], principled.inputs["Roughness"])
                links.new(channels.outputs["Red"], principled.inputs["Metallic"])
            else:
                links.new(channels.outputs["Green"], principled.inputs["Roughness"])
                links.new(channels.outputs["Blue"], principled.inputs["Metallic"])
        links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    return result


def load_obj(path, records):
    positions, uv, normals, faces, face_materials = [], [], [], [], []
    indices = {record["name"]: i for i, record in enumerate(records)}
    selected = 0
    with open(path, "r", encoding="utf-8-sig") as source:
        for line in source:
            parts = line.split()
            if not parts: continue
            if parts[0] == "v": positions.append(camera_vector(tuple(map(float, parts[1:4]))))
            elif parts[0] == "vt": uv.append(tuple(map(float, parts[1:3])))
            elif parts[0] == "vn": normals.append(camera_vector(tuple(map(float, parts[1:4]))))
            elif parts[0] == "usemtl": selected = indices[parts[1]]
            elif parts[0] == "f":
                row = [int(part.split("/")[0]) - 1 for part in parts[1:]]
                # Camera-axis conversion is a reflection; retain outward normals.
                faces.append((row[0], row[2], row[1])); face_materials.append(selected)
    if len(positions) != len(uv) or len(positions) != len(normals):
        raise ValueError("Exporter must retain one UV/normal per explicit OBJ vertex")
    mesh = bpy.data.meshes.new(path.stem); mesh.from_pydata(positions, [], faces); mesh.update()
    obj = bpy.data.objects.new(path.stem, mesh); bpy.context.collection.objects.link(obj)
    for record in records: mesh.materials.append(material(record))
    layer = mesh.uv_layers.new(name="Actual Unity UV")
    for polygon, index in zip(mesh.polygons, face_materials):
        polygon.material_index = index; polygon.use_smooth = True
        for loop in polygon.loop_indices: layer.data[loop].uv = uv[mesh.loops[loop].vertex_index]
    if hasattr(mesh, "use_auto_smooth"): mesh.use_auto_smooth = True
    mesh.normals_split_custom_set_from_vertices(normals)
    return obj


def lamp(name, location, energy, size, target=(0, .5, -.15), colour=(1, 1, 1)):
    data = bpy.data.lights.new(name, "AREA"); data.energy = energy; data.shape = "DISK"; data.size = size; data.color = colour
    obj = bpy.data.objects.new(name, data); bpy.context.collection.objects.link(obj); obj.location = location
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_frame(directory, frame, output, width, samples):
    for obj in list(bpy.data.objects): bpy.data.objects.remove(obj, do_unlink=True)
    # This owned background process renders several large baked skins. Removing
    # scene objects alone leaves their meshes/materials allocated between frames.
    for collection in (bpy.data.meshes, bpy.data.materials, bpy.data.lights, bpy.data.cameras):
        for block in list(collection):
            if block.users == 0: collection.remove(block)
    obj_path = directory / frame["obj"]
    if sha(obj_path) != frame["objSha256"]: raise ValueError("OBJ changed after Unity export: " + str(obj_path))
    load_obj(obj_path, frame["materials"])
    scene = bpy.context.scene; scene.render.engine = "CYCLES"; scene.cycles.device = "CPU"; scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.threads_mode = "FIXED"; scene.render.threads = 2
    scene.render.resolution_x = width; scene.render.resolution_y = round(width / frame["aspect"]); scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"; scene.render.image_settings.color_mode = "RGBA"; scene.render.film_transparent = True
    scene.view_settings.view_transform = "Standard"; scene.view_settings.look = "None"
    scene.view_settings.exposure = 0; scene.view_settings.gamma = 1
    if not scene.world: scene.world = bpy.data.worlds.new("CPU reference ambient")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.05, .055, .065, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .55
    data = bpy.data.cameras.new("Exact Unity vertical FOV")
    camera = bpy.data.objects.new("CPU review camera", data); bpy.context.collection.objects.link(camera)
    camera.location = (0, 0, 0); camera.rotation_euler = (math.pi / 2, 0, 0)
    data.sensor_fit = "VERTICAL"; data.sensor_height = 24
    data.lens = 12 / math.tan(math.radians(frame["cameraFov"]) / 2)
    data.clip_start = .01; data.clip_end = 3; data.dof.use_dof = False; scene.camera = camera
    lamp("Soft reference key", (-.65, -.2, .85), 65, 1.1, colour=(1, .97, .91))
    lamp("Soft reference fill", (.7, -.1, .25), 22, 1.4, colour=(.85, .91, 1))
    destination = output / (frame["name"] + ".png"); scene.render.filepath = str(destination)
    began = time.time(); bpy.ops.render.render(write_still=True)
    return {"name": frame["name"], "path": str(destination), "sha256": sha(destination),
            "objSha256": frame["objSha256"], "seconds": round(time.time() - began, 3),
            "vertices": frame["vertices"], "triangles": frame["triangles"],
            "centralRectOccupiedTriangles": frame["centralRectOccupiedTriangles"], "aspect": frame["aspect"], "cameraFov": frame["cameraFov"]}


def main():
    if not bpy.app.background: raise RuntimeError("Only run in an owned --background Blender; never clear a live user's scene")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True, type=Path); parser.add_argument("--output", type=Path)
    parser.add_argument("--frames", nargs="*"); parser.add_argument("--width", type=int, default=640); parser.add_argument("--samples", type=int, default=16)
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = parser.parse_args(argv)
    if not 320 <= args.width <= 1280 or not 1 <= args.samples <= 64: raise ValueError("Use bounded CPU review dimensions/samples")
    source = args.manifest.resolve(); manifest = json.loads(source.read_text(encoding="utf-8-sig"))
    if manifest["status"] != "CPU_GEOMETRY_EXPORT_PASS": raise ValueError("A successful current Unity export is required")
    for entry in manifest["inputs"]:
        if sha(entry["path"]) != entry["sha256"]: raise ValueError("Source changed after Unity export: " + entry["path"])
    output = (args.output or source.parent / "cpu-stills").resolve(); output.mkdir(parents=True, exist_ok=True)
    frames = [frame for frame in manifest["frames"] if not args.frames or frame["name"] in args.frames]
    if args.frames and len(frames) != len(set(args.frames)): raise ValueError("Requested frame is absent from the export")
    for frame in frames:
        if (output / (frame["name"] + ".png")).exists(): raise ValueError("Use a fresh output or unrendered frame selection")
    report = {"status": "RUNNING", "scope": "Blender CPU reference stills of actual Unity-baked poses; not native shader/gameplay/FPS validation",
              "blender": bpy.app.version_string, "device": "CPU", "threads": 2, "samples": args.samples,
              "renderScript": str(Path(__file__).resolve()), "renderScriptSha256": sha(Path(__file__).resolve()),
              "sourceManifest": str(source), "sourceManifestSha256": sha(source), "frames": [], "limitations": manifest["limitations"]}
    report_path = output / "render-report.json"
    try:
        for frame in frames:
            report["frames"].append(render_frame(source.parent, frame, output, args.width, args.samples))
            report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        report["status"] = "CPU_REFERENCE_RENDER_PASS"
    except Exception as error:
        report["status"] = "FAIL"; report["error"] = repr(error); raise
    finally: report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("HAPPYTOY_HAND_CPU_RENDER_PASS " + str(output))


if __name__ == "__main__": main()
