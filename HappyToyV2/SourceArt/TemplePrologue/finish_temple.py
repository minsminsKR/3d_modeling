"""Finish the preserved temple GLB with the shared entrance asset recipe.

Run with Blender 4.0.2 in background from any working directory. Outputs are
kept in SourceArt until a Unity importer has validated materials and scale.
"""
from pathlib import Path
import importlib.util

here = Path(__file__).resolve().parent
recipe = here.parent / 'CorridorEntrance' / 'finish_entrance_assets.py'
spec = importlib.util.spec_from_file_location('entrance_finish', recipe)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
module.SOURCE = here
module.OUTPUT = here / 'game-export'
module.OUTPUT.mkdir(exist_ok=True)
module.SETTINGS['temple'] = dict(axis=0, metres=9.0, texture=4096, budgets=(100000,))
module.prepare('temple', 0)
