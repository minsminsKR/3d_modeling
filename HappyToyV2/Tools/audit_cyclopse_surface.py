"""Read-only surface/UV inspection. No repair or atlas rewriting."""
import bpy
import json
from pathlib import Path
from collections import Counter

root = Path(__file__).resolve().parent.parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/Art/Candidate/Cyclopse/Walking.fbx'))
obj = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
mesh = obj.data
mesh.calc_loop_triangles()
uv = mesh.uv_layers.active
image = bpy.data.images.load(str(root/'Assets/Art/V1/Cyclopse/model_textured.jpg'))
pixels = list(image.pixels)
width, height = image.size
edges = Counter()
welded_edges = Counter()
positions = [tuple(round(c,6) for c in v.co) for v in mesh.vertices]
gray = []
zero_uv = []
outside = []
for tri in mesh.loop_triangles:
    coords = [uv.data[i].uv.copy() for i in tri.loops]
    cross = (coords[1].x-coords[0].x)*(coords[2].y-coords[0].y)-(coords[1].y-coords[0].y)*(coords[2].x-coords[0].x)
    if abs(cross) < 1e-10:
        zero_uv.append(tri.polygon_index)
    if any(min(p)<0 or max(p)>1 for p in coords):
        outside.append(tri.polygon_index)
    center = sum(coords, coords[0]*0)/3
    x = min(width-1, max(0, int(center.x*width)))
    y = min(height-1, max(0, int(center.y*height)))
    rgb = pixels[(y*width+x)*4:(y*width+x)*4+3]
    if max(rgb)-min(rgb)<.065 and sum(rgb)/3>.1:
        centroid = sum((mesh.vertices[i].co for i in tri.vertices), mesh.vertices[0].co*0)/3
        gray.append(dict(face=tri.polygon_index, position=list(centroid), uv=list(center), color=rgb, area=tri.area))
    for a,b in zip(tri.vertices, tri.vertices[1:]+tri.vertices[:1]):
        edges[tuple(sorted((a,b)))]+=1
        welded_edges[tuple(sorted((positions[a],positions[b])))]+=1
report = dict(vertices=len(mesh.vertices),triangles=len(mesh.loop_triangles),
    boundary_edges=sum(n==1 for n in edges.values()),nonmanifold_edges=sum(n>2 for n in edges.values()),
    zero_uv_faces=sorted(set(zero_uv)),outside_uv_faces=sorted(set(outside)),
    gray_texture_triangles=gray,gray_area=sum(row['area'] for row in gray),
    total_area=sum(t.area for t in mesh.loop_triangles))
report['position_welded_boundary_edges']=sum(n==1 for n in welded_edges.values())
report['position_welded_nonmanifold_edges']=sum(n>2 for n in welded_edges.values())
report['zero_uv_area']=sum(t.area for t in mesh.loop_triangles if t.polygon_index in set(zero_uv))
out=root/'Verification/cyclopse-full-clip/surface.json'
out.write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({k:(len(v) if isinstance(v,list) else v) for k,v in report.items()},indent=2))
