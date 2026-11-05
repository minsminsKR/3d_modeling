"""Finish preserved generated stone cluster as editable Blender LODs.

Blender 4.0.2; output is SourceArt only. The A100 original remains unchanged.
Review the model in the service before running this recipe, and verify shape,
seams, ground clearance and performance in Unity before any production use.
"""
from pathlib import Path
import importlib.util

here = Path(__file__).resolve().parent
recipe = here.parents[1] / 'CorridorEntrance' / 'finish_entrance_assets.py'
spec = importlib.util.spec_from_file_location('entrance_finish', recipe)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
module.SOURCE = here.parent
module.OUTPUT = here / 'game-export'
module.OUTPUT.mkdir(exist_ok=True)
# A walkable low relief, with the original unmodified model packed in the blend.
# These are candidate budgets, not approved GPU or visual quality claims.
module.SETTINGS['temple-stone-cluster'] = dict(axis=0, metres=1.8, texture=2048,
    budgets=(24000, 20000), square=True, max_height=.12)
module.prepare('temple-stone-cluster', 0)
