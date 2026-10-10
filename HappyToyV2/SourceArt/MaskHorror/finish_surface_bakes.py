"""Finalize genuine tangent normals without replacing valid sculpt detail.
All diffuse/AO/roughness/metallic baking belongs to sculpt_and_bake.py.
This changes only pure-black missed ray/background texels to a neutral normal;
source surfaces, UVs, actual metal values and roughness remain intact.
"""
from pathlib import Path
import hashlib,json,sys
import bpy
import numpy as np
HERE=Path(__file__).resolve().parent;PROJECT=HERE.parents[1]
sys.path.insert(0,str(HERE))
from sculpt_and_bake import shader_sculpt
p=HERE/'model-manifest.json';manifest=json.loads(p.read_text(encoding='utf-8'))
for record in manifest['assets']:
    bpy.ops.wm.open_mainfile(filepath=str(PROJECT/record['source']))
    areas={}
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH' or obj.name.startswith(('SCULPT HIGH |','LOD1 |')):continue
        uv=obj.data.uv_layers.active.data;slot=obj.data.materials[0].name
        for poly in obj.data.polygons:
            q=[uv[i].uv for i in poly.loop_indices]
            area=abs(sum(q[i].x*q[(i+1)%len(q)].y-q[(i+1)%len(q)].x*q[i].y for i in range(len(q)))/2)
            areas[slot]=areas.get(slot,0)+area
    record['uvAreaByMaterial']=areas
    record['uvAllocation']='Dedicated skin 72%-width / face 74%-width region; hair, cord, bells, teeth and eyes separately packed'
    # Retain coherent editable procedural high materials after a body-only
    # surface refresh; low atlas previews are deliberately left connected.
    for mat in {m for obj in bpy.context.scene.objects if obj.name.startswith('SCULPT HIGH |') for m in obj.data.materials if m}:
        shader_sculpt(mat)
    bpy.ops.file.make_paths_relative();bpy.ops.wm.save_as_mainfile(filepath=str(PROJECT/record['source']),compress=True)
    record['sourceSha256']=hashlib.sha256((PROJECT/record['source']).read_bytes()).hexdigest()
    normal_path=PROJECT/next(t['path'] for t in record['textures'] if t['kind']=='normal')
    normal=bpy.data.images.load(str(normal_path),check_existing=False);normal.colorspace_settings.name='Non-Color'
    values=np.empty(2048*2048*4,dtype=np.float32);normal.pixels.foreach_get(values);values=values.reshape((-1,4))
    misses=values[:,:3].sum(axis=1)<.025;values[misses,:3]=(.5,.5,1);values[misses,3]=1
    normal.pixels.foreach_set(values.ravel());normal.filepath_raw=str(normal_path);normal.file_format='PNG';normal.save()
    for texture in record['textures']:texture['sha256']=hashlib.sha256((PROJECT/texture['path']).read_bytes()).hexdigest()
    record['normalMissPolicy']='Only pure-black ray/background misses neutralized; all valid selected-high-to-low sculpt normals retained'
    print('MANYHAND_NORMAL_FINISH_PASS',record['key'],int(misses.sum()),flush=True)
manifest['materialContract']={
    'maps':'Actual 2048-square Cycles atlases: sRGB diffuse; linear tangent normal/AO/roughness; packed R=real metallic, A=1-roughness',
    'MW_eye':'Two narrow black rough nonmetal nonemissive slit surfaces. BindAuthoredStaticEyes(eyes,false) preserves black eyes.',
    'MW_bell':'Hollow black-bronze bell geometry with source metallic .78 retained in packed atlas',
    'skin':'Original procedural dark mottled brown-grey skin, voxel anatomy and actual multires scar/pore displacement',
    'reference':'Private user reference SHA/proportions only; image neither redistributed nor licensed CC0'}
manifest['authoringScripts']=[dict(path=p.relative_to(PROJECT).as_posix(),
    sha256NormalizedLf=hashlib.sha256(p.read_text(encoding='utf-8').replace('\r\n','\n').encode('utf-8')).hexdigest())
    for p in sorted(HERE.glob('*.py'))]
p.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
