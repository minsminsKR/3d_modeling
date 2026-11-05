"""Finish the preserved A100-generated hall in Blender 4.0.2.

Derive two review LODs and 4K PBR outputs without altering the original GLB.
Do not promote a candidate before verifying the actual game camera and route.
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
module.SETTINGS['temple-hall-v2'] = dict(axis=0, metres=9.0, texture=4096,
                                         budgets=(160000, 85000))
module.prepare('temple-hall-v2', 0)
