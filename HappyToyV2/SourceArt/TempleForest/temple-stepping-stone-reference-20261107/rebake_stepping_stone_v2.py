"""Rebake round-stone LODs from the preserved webapp original (Blender 4.0.2).

Run from any directory: blender -b -t 2 --factory-startup --python-exit-code 1
    --python <this script>
Writes only new v2 outputs and refuses to replace any previous candidate.
The original and prior candidate remain unchanged. Review in Unity is required.
"""
from pathlib import Path
import hashlib, json, math
import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
KEY = HERE.name
ORIGINAL = HERE / 'original.glb'
SOURCE_BLEND = HERE / (KEY + '.blend')
OUT = HERE / 'rebaked'
BLEND = OUT / (KEY + '-rebaked.blend')
REPORT = OUT / 'rebake-report.json'
assert bpy.app.version[:3] == (4, 0, 2)
assert ORIGINAL.is_file() and SOURCE_BLEND.is_file()
if any(p.exists() for p in (OUT, BLEND, REPORT)):
    raise RuntimeError('v2 output exists; refusing to overwrite')
def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()
original_sha = sha(ORIGINAL)
OUT.mkdir()
bpy.ops.wm.open_mainfile(filepath=str(SOURCE_BLEND), load_ui=False)
scene = bpy.context.scene
orig_col = bpy.data.collections['Complete unchanged webapp original']
game_col = bpy.data.collections['Editable source-derived game LODs']
orig = next(o for o in orig_col.objects if o.type == 'MESH')
corners = [orig.matrix_world @ Vector(c) for c in orig.bound_box]
lo = Vector(tuple(min(p[i] for p in corners) for i in range(3)))
hi = Vector(tuple(max(p[i] for p in corners) for i in range(3)))
center = Vector(((lo.x + hi.x) * .5, (lo.y + hi.y) * .5, lo.z))
norm = Matrix.Scale(1 / (hi.x - lo.x), 4) @ Matrix.Translation(-center)
orig_col.hide_render = True
game_col.hide_render = True
src = orig.copy(); src.data = orig.data.copy(); scene.collection.objects.link(src)
src.matrix_world = norm @ orig.matrix_world
src.hide_set(False); src.hide_render = False; src.hide_viewport = False
assert len(src.material_slots) == 1
source_mat = src.material_slots[0].material.copy()
src.data.materials.clear(); src.data.materials.append(source_mat)
tree = source_mat.node_tree
shader = next(n for n in tree.nodes if n.type == 'BSDF_PRINCIPLED')
output = next(n for n in tree.nodes if n.type == 'OUTPUT_MATERIAL')
em = tree.nodes.new('ShaderNodeEmission')
tree.links.new(em.outputs[0], output.inputs['Surface'])
def source_channel(name):
    inp = shader.inputs[name]
    for link in list(em.inputs['Color'].links): tree.links.remove(link)
    if inp.is_linked:
        tree.links.new(inp.links[0].from_socket, em.inputs['Color'])
    else:
        value = inp.default_value
        em.inputs['Color'].default_value = (value, value, value, 1) if isinstance(value, float) else value

scene.render.engine = 'CYCLES'; scene.cycles.device = 'CPU'; scene.cycles.samples = 1
scene.render.threads_mode = 'FIXED'; scene.render.threads = 2
scene.render.bake.use_selected_to_active = True
scene.render.bake.cage_extrusion = .03; scene.render.bake.max_ray_distance = .12
scene.render.bake.margin = 16; scene.render.bake.use_clear = True

def select_pair(target):
    bpy.ops.object.select_all(action='DESELECT'); src.select_set(True); target.select_set(True)
    bpy.context.view_layer.objects.active = target
def bake(target, mat, image):
    node = mat.node_tree.nodes.new('ShaderNodeTexImage'); node.image = image
    mat.node_tree.nodes.active = node; select_pair(target)
    bpy.ops.object.bake(type='EMIT', use_clear=True)
    mat.node_tree.nodes.remove(node)
def make_image(name, size, data=False):
    im = bpy.data.images.new(name, width=size, height=size, alpha=True, float_buffer=data)
    im.colorspace_settings.name = 'Non-Color' if data else 'sRGB'
    im.generated_color = (0, 0, 0, 1)
    return im
def save(im, path):
    im.file_format = 'PNG'; im.filepath_raw = str(path); im.save(); im.pack()

def interior_mask(mesh, size):
    mesh.calc_loop_triangles()
    uv = np.empty(len(mesh.loops) * 2, dtype=np.float32)
    mesh.uv_layers.active.data.foreach_get('uv', uv); uv = uv.reshape((-1, 2))
    loops = np.empty(len(mesh.loop_triangles) * 3, dtype=np.int32)
    mesh.loop_triangles.foreach_get('loops', loops); loops = loops.reshape((-1, 3))
    mask = np.zeros((size, size), dtype=bool)
    for ids in loops:
        p = uv[ids] * size
        xmin = max(0, int(np.floor(p[:, 0].min()))); xmax = min(size-1, int(np.ceil(p[:, 0].max())))
        ymin = max(0, int(np.floor(p[:, 1].min()))); ymax = min(size-1, int(np.ceil(p[:, 1].max())))
        if xmax < xmin or ymax < ymin: continue
        xx, yy = np.meshgrid(np.arange(xmin, xmax+1)+.5, np.arange(ymin, ymax+1)+.5)
        def cross(a, b): return (b[0]-a[0])*(yy-a[1])-(b[1]-a[1])*(xx-a[0])
        a, b, c = cross(p[0],p[1]), cross(p[1],p[2]), cross(p[2],p[0])
        mask[ymin:ymax+1, xmin:xmax+1] |= ((a>=0)&(b>=0)&(c>=0)) | ((a<=0)&(b<=0)&(c<=0))
    interior = mask.copy()
    for dy, dx in ((0,1),(0,-1),(1,0),(-1,0),(2,0),(-2,0),(0,2),(0,-2)):
        shifted = np.zeros_like(mask)
        ys = slice(max(0,dy), min(size,size+dy)); xs = slice(max(0,dx), min(size,size+dx))
        shifted[ys,xs] = mask[slice(max(0,-dy), min(size,size-dy)),slice(max(0,-dx), min(size,size-dx))]
        interior &= shifted
    return interior

collection = bpy.data.collections.new('Rebaked v2 game LODs'); scene.collection.children.link(collection)
items = []
for i in range(3):
    old = bpy.data.objects['CE_' + KEY + '_LOD' + str(i)]
    target = old.copy(); target.data = old.data.copy(); collection.objects.link(target)
    target.name = 'ROUND_REBAKED_LOD' + str(i)
    target.hide_set(False); target.hide_render = False; target.hide_viewport = False
    mat = bpy.data.materials.new(target.name + '_material'); mat.use_nodes = True
    target.data.materials.clear(); target.data.materials.append(mat)
    bpy.ops.object.select_all(action='DESELECT'); target.select_set(True); bpy.context.view_layer.objects.active = target
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.018)
    bpy.ops.object.mode_set(mode='OBJECT')

    # Independent white bake checks true projection coverage, including a negative control.
    size = 1024
    coverage = make_image('coverage_LOD' + str(i), size, True)
    source_channel('Base Color')
    for link in list(em.inputs['Color'].links): tree.links.remove(link)
    em.inputs['Color'].default_value = (1,1,1,1)
    bake(target, mat, coverage)
    mask = interior_mask(target.data, size)
    pixels = np.empty(size*size*4, dtype=np.float32); coverage.pixels.foreach_get(pixels)
    values = pixels.reshape((size,size,4))[:,:,:3].mean(axis=2)
    count = int(mask.sum()); missing = int((mask & (values < .5)).sum())
    if not count or missing / count > .001: raise RuntimeError('projection coverage failed LOD%d: %d/%d' % (i, missing, count))
    if i == 0:
        src.location.x += 10
        coverage.pixels = np.tile(np.array([0,0,0,1],dtype=np.float32),size*size)
        bake(target, mat, coverage); coverage.pixels.foreach_get(pixels)
        negative = int((mask & (pixels.reshape((size,size,4))[:,:,:3].mean(axis=2) < .5)).sum())
        src.location.x -= 10
        if negative / count < .99: raise RuntimeError('coverage negative control failed')
    bpy.data.images.remove(coverage)

    size = 2048
    color = make_image(target.name + '_baseColor', size)
    source_channel('Base Color'); bake(target, mat, color)
    color_path = OUT / ('CE_' + KEY + '_lod' + str(i) + '-baseColor.png'); save(color, color_path)
    channels = []
    for channel in ('Metallic','Roughness'):
        image = make_image(target.name + '_' + channel, size, True)
        source_channel(channel); bake(target, mat, image)
        pixels = np.empty(size*size*4, dtype=np.float32); image.pixels.foreach_get(pixels)
        channels.append(pixels.reshape((size,size,4))[:,:,0].copy())
        bpy.data.images.remove(image)
    packed = make_image(target.name + '_metallicSmoothness', size, True)
    rgba = np.zeros((size,size,4),dtype=np.float32); rgba[:,:,0] = channels[0]; rgba[:,:,3] = 1-channels[1]
    packed.pixels.foreach_set(rgba.ravel()); packed.update()
    packed_path = OUT / ('CE_' + KEY + '_lod' + str(i) + '-metallicSmoothness.png'); save(packed, packed_path)

    nodes = mat.node_tree.nodes; links = mat.node_tree.links
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    color_node = nodes.new('ShaderNodeTexImage'); color_node.image = color
    links.new(color_node.outputs['Color'], bsdf.inputs['Base Color'])
    pack_node = nodes.new('ShaderNodeTexImage'); pack_node.image = packed
    separate = nodes.new('ShaderNodeSeparateColor'); links.new(pack_node.outputs['Color'], separate.inputs['Color'])
    links.new(separate.outputs['Red'], bsdf.inputs['Metallic'])
    invert = nodes.new('ShaderNodeMath'); invert.operation = 'SUBTRACT'; invert.inputs[0].default_value = 1
    links.new(pack_node.outputs['Alpha'], invert.inputs[1]); links.new(invert.outputs[0], bsdf.inputs['Roughness'])

    bpy.ops.object.select_all(action='DESELECT'); target.select_set(True); bpy.context.view_layer.objects.active = target
    fbx = OUT / (KEY + '-rebaked-lod' + str(i) + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y', bake_anim=False, path_mode='RELATIVE')
    target.data.calc_loop_triangles()
    items.append(dict(lod=i, triangles=len(target.data.loop_triangles), uvInteriorPixels=count, projectionMissPixels=missing,
                      fbx=dict(file=fbx.name, sha256=sha(fbx)),
                      textures=dict(baseColor=dict(file=color_path.name, sha256=sha(color_path), width=size, height=size),
                                    metallicSmoothness=dict(file=packed_path.name, sha256=sha(packed_path), width=size, height=size))))
    print('REBAKED_LOD', json.dumps(items[-1]), flush=True)
    target.hide_render = True

src.hide_render = True
source_text = bpy.data.texts.new('SOURCE - rebake_stepping_stone_v2.py')
source_text.write(Path(__file__).read_text(encoding='utf-8'))
bpy.ops.file.pack_all(); bpy.ops.wm.save_as_mainfile(filepath=str(BLEND), compress=True)
assert sha(ORIGINAL) == original_sha
files = [p for p in OUT.iterdir() if p.is_file()]
result = dict(key=KEY, originalSha256=original_sha, blender=bpy.app.version_string, editableBlend=BLEND.name,
              editableSha256=sha(BLEND), recipe=Path(__file__).name,
              sourceCandidate=SOURCE_BLEND.name, cageExtrusionMetres=.03, maxRayDistanceMetres=.12,
              lods=items, negativeControlMissPixels=negative,
              hashes={p.name:sha(p) for p in files},
              notes='Each LOD has separate UV and maps. Unity must assign its matching base color and metallic-smoothness texture explicitly. Verify appearance and transitions in game; projection coverage alone does not certify visual quality.')
REPORT.write_text(json.dumps(result,indent=2)+'\n', encoding='utf-8')
print('REBAKED_V2_COMPLETE',flush=True)
