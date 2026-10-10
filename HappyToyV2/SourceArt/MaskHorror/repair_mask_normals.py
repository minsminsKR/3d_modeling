"""Legacy entry point: current generator authors outward normals; audit only."""
from pathlib import Path
import runpy
runpy.run_path(str(Path(__file__).with_name('validate_sources.py')),run_name='__main__')