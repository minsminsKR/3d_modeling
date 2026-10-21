"""Blender 4.0.2: refresh embedded standalone recipe texts in preserved sources.

This changes no model, UV, material, original PBR or exported game asset.
Use after a common recipe's final documentation/other-asset settings change.
"""
from pathlib import Path
import bpy

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[1]
for key in ('grand-piano','music-stand'):
    path=SOURCE/key/(key+'.blend')
    bpy.ops.wm.open_mainfile(filepath=str(path))
    for recipe in (SOURCE/'finish_music_assets.py',SOURCE/'rebake_music_lod.py',PROJECT/'SourceArt/CorridorEntrance/finish_entrance_assets.py'):
        name='SOURCE - '+recipe.name
        block=bpy.data.texts.get(name) or bpy.data.texts.new(name)
        block.clear();block.write(recipe.read_text(encoding='utf-8'))
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
    print('SCHOOL_EMBEDDED_RECIPES_UPDATED '+key,flush=True)
