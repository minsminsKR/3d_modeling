"""Finish the preserved generated Korean stone lantern with Blender 4.0.2.

Uses the repository's source-derived asset recipe, keeping original GLB/PBR
and a packed editable blend alongside two optimized FBX variants.
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
module.SETTINGS['stone-lantern'] = dict(axis=2, metres=1.45, texture=2048,
                                        budgets=(65000, 18000))
module.prepare('stone-lantern', 0)
