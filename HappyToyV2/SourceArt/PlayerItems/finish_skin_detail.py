"""Compatibility entry point for the current photographic hand finish.
The historical A100 original remains here; the active hand is PlayerHandsV2.
"""
from pathlib import Path
import runpy

runpy.run_path(str(Path(__file__).resolve().parent.parent / "PlayerHandsV2/finish_skin.py"), run_name="__main__")
