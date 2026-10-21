"""Blender 4.0.2: finish the preserved designated-webapp music props.

blender -b --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/SchoolLoop/finish_music_assets.py
The repository's shared entrance finisher supplies PBR conversion, seam-aware
bounded decimation, packed untouched originals and FBX/LOD reporting.
"""
from pathlib import Path
import argparse
import runpy
import sys
import bpy
import bmesh
import json
from mathutils import Matrix,Vector

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[1]
RECIPE=PROJECT/'SourceArt/CorridorEntrance/finish_entrance_assets.py'
SETTINGS={
    'grand-piano':dict(axis=2,metres=1.75,texture=2048,budgets=(100000,40000)),
    'music-stand':dict(axis=2,metres=1.16,texture=2048,budgets=(30000,4000)),
}
if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--only',choices=SETTINGS)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    finish=runpy.run_path(str(RECIPE),run_name='shared_preserved_prop_finisher')['prepare']
    finish.__globals__.update(SOURCE=SOURCE,PROJECT=PROJECT,OUTPUT=PROJECT/'Assets/Resources/SchoolLoop',SETTINGS=SETTINGS)
    original_simplify=finish.__globals__['simplify']
    extra_surfaces=[]
    active_key=None
    def music_simplify(obj,budget):
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        if obj.name.endswith('LOD1'):
            # Generated thin/nonmanifold triangles can prevent collapse. Repair
            # only the far LOD from its real surface; rebake untouched original
            # colour/ORM onto its new UVs. The near LOD stays source-atlased.
            modifier=obj.modifiers.new('Source-derived distant LOD topology repair','REMESH')
            modifier.mode='VOXEL';modifier.voxel_size=.006 if active_key=='grand-piano' else .003
            modifier.use_smooth_shade=True;modifier.use_remove_disconnected=False
            bpy.ops.object.modifier_apply(modifier=modifier.name)
            original_simplify(obj,budget)
            originals=[item for item in bpy.data.collections['Complete unchanged webapp original'].objects if item.type=='MESH']
            if len(originals)!=1:raise RuntimeError('Music atlas requires one preserved original PBR primitive')
            low,high=finish.__globals__['bounds'](originals)
            factor=SETTINGS[active_key]['metres']/(high.z-low.z)
            matrix=Matrix.Scale(factor,4)@Matrix.Translation(-Vector(((low.x+high.x)*.5,(low.y+high.y)*.5,low.z)))
            bake=runpy.run_path(str(SOURCE/'rebake_music_lod.py'),run_name='music_lod_atlas')['bake']
            extra_surfaces.append(bake(obj,originals[0],matrix,PROJECT/'Assets/Resources/SchoolLoop'/active_key,
                'CE_'+active_key+'_Distant',1024,finish.__globals__['save_pixels']))
        else:
            bm=bmesh.new();bm.from_mesh(obj.data)
            bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0006)
            bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.000001)
            bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free();obj.data.update()
            original_simplify(obj,budget)
    finish.__globals__['simplify']=music_simplify
    for key in ([args.only] if args.only else SETTINGS):
        active_key=key;extra_surfaces.clear()
        finish(key,0)
        report_path=SOURCE/key/'export.json'
        report=json.loads(report_path.read_text())
        report['materials'].extend(extra_surfaces)
        report['finishing']['distantLodRepair']='Source-derived voxel surface repair (piano 6mm, stand 3mm), bounded collapse, original colour/ORM selected-to-active rebake at 1K. Near LOD retains its source atlas.'
        for path in (report_path,PROJECT/'Assets/Resources/SchoolLoop'/key/'asset.json'):
            path.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8',newline='\n')
        bpy.data.texts.new('SOURCE - rebake_music_lod.py').write((SOURCE/'rebake_music_lod.py').read_text(encoding='utf-8'))
        bpy.data.texts.new('SOURCE - finish_music_assets.py').write(Path(__file__).read_text(encoding='utf-8'))
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/key/(key+'.blend')),compress=True)
