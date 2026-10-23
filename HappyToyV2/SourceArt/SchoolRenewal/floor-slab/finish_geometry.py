from pathlib import Path
import runpy
def preprocess(near):
    return runpy.run_path(str(Path(__file__).resolve().parents[1] / 'architecture_geometry.py'))['preprocess'](near, 'floor-slab')
