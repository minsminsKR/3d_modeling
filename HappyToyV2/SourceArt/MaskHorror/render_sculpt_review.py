"""Compatibility entry point for the current anatomical socket studio."""
from pathlib import Path
import runpy
runpy.run_path(str(Path(__file__).with_name('render_reference_review.py')),run_name='__main__')