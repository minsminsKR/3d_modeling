"""Finish the preserved generated Korean incense vessel in Blender 4.0.2.

The ash vessel does not contain sticks; the game supplies them through its
existing interaction. Original GLB/PBR stay untouched in the packed blend.
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
module.SETTINGS['incense-vessel'] = dict(axis=0, metres=.44, texture=2048,
                                         budgets=(65000, 20000))
module.prepare('incense-vessel', 0)
