"""Compatibility entry point for current eight-pair reference geometry.
For the complete source/2K bake/LOD chain use regenerate_reference.py.
The superseded upright two-arm model remains recoverable in Git history.
"""
from pathlib import Path
import runpy
runpy.run_path(str(Path(__file__).with_name('author_manyhand_reference.py')),run_name='__main__')