from __future__ import annotations
import math
import random
from dataclasses import dataclass, field
from pathlib import Path

# Dynamically locate the assets directory in game/happy_toy/assets
ROOT_DIR = Path(__file__).resolve().parents[1]
# E:\AI\3d_modeling-antigravity\game\defense\core\config.py -> parents[3] is E:\AI\3d_modeling-antigravity
PROJECT_ROOT = Path(__file__).resolve().parents[3]
ASSET_ROOT = PROJECT_ROOT / "game" / "happy_toy" / "assets"

PLAYER_BASE_SIZE = 1.0
BASE_WALK_SPEED = 6.0
PLAYER_AUTO_FORWARD_SPEED = 4.0
PLAYER_LEFT_RIGHT_SPEED = 7.0
MAP_BOUNDS_X = 6.0

@dataclass
class CharacterAsset:
    model: Path | None = None
    texture: Path | None = None
    animation_name: str = ""
    fallback_model: str = "cube"

@dataclass
class AssetCatalog:
    characters: dict[str, CharacterAsset] = field(default_factory=dict)
    textures: dict[str, Path] = field(default_factory=dict)

def _find_first(root: Path, contains: list[str], extensions: tuple[str, ...]) -> Path | None:
    if not root.exists():
        return None
    lowered = [c.lower() for c in contains]
    candidates: list[Path] = []
    for path in root.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in extensions:
            continue
        haystack = str(path).lower()
        if all(part in haystack for part in lowered):
            candidates.append(path)
    if not candidates:
        return None
    return sorted(candidates, key=lambda p: (len(str(p)), str(p)))[0]

def _find_texture(character_name: str) -> Path | None:
    return _find_first(ASSET_ROOT, ["characters", character_name, "model_textured"], (".jpg", ".jpeg", ".png"))

def scan_assets() -> AssetCatalog:
    catalog = AssetCatalog()
    character_specs = {
        "Cyclopse": ("Run", "sphere"),
        "Hwacat": ("Hip Hop Dancing", "cube"),
        "Uncat": ("Run", "cube"),
        "Hwacat_angry": ("Zombie Run", "cube"),
    }
    for name, (animation, fallback) in character_specs.items():
        catalog.characters[name] = CharacterAsset(
            model=_find_first(ASSET_ROOT, ["characters", name, animation], (".fbx",)),
            texture=_find_texture(name),
            animation_name=animation,
            fallback_model=fallback,
        )
    return catalog

ASSETS = scan_assets()
