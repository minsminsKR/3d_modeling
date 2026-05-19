from __future__ import annotations

import math
import random
from dataclasses import dataclass, field
from pathlib import Path


ROOT_DIR = Path(__file__).resolve().parents[1]
ASSET_ROOT = Path(r"E:\AI\3d_modeling\game\happy_toy\assets")


PLAYER_BASE_SIZE = 5
BASE_WALK_SPEED = 8.0
BASE_RUN_SPEED = 13.0
GIANT_SPEED = BASE_RUN_SPEED - 0.18

ENEMY_TARGET_COUNT = 20
SPAWN_MIN_DISTANCE = 30.0
SPAWN_MAX_DISTANCE = 80.0
CHUNK_SIZE = 48
CHUNK_RADIUS = 2
WORLD_SEED = 7331

FLASHLIGHT_MAX_BATTERY = 100.0
FLASHLIGHT_DRAIN_PER_SECOND = 4.5
FLASHLIGHT_PICKUP_AMOUNT = 35.0
STAMINA_MAX = 100.0
STAMINA_DRAIN_PER_SECOND = 24.0
STAMINA_RECOVER_PER_SECOND = 18.0


@dataclass
class CharacterAsset:
    model: Path | None = None
    texture: Path | None = None
    animation_name: str = ""
    fallback_model: str = "cube"


@dataclass
class AssetCatalog:
    characters: dict[str, CharacterAsset] = field(default_factory=dict)
    props: list[Path] = field(default_factory=list)
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

    if ASSET_ROOT.exists():
        catalog.props = sorted(
            [p for p in ASSET_ROOT.rglob("*.glb") if "prop" in str(p).lower() or "textures" in str(p).lower()]
        )
        for texture_name in ("floor", "wall", "ceiling"):
            found = _find_first(ASSET_ROOT, ["textures", texture_name], (".jpg", ".jpeg", ".png"))
            if found:
                catalog.textures[texture_name] = found
    return catalog


ASSETS = scan_assets()


def visual_scale_from_size(size: float) -> float:
    return max(0.08, size / PLAYER_BASE_SIZE)


def radius_from_size(size: float) -> float:
    return max(0.65, visual_scale_from_size(size) * 0.85)


def choose_weighted(options: list[tuple[str, float]]) -> str:
    total = sum(weight for _, weight in options)
    roll = random.uniform(0, total)
    cursor = 0.0
    for value, weight in options:
        cursor += weight
        if roll <= cursor:
            return value
    return options[-1][0]


def random_ring_position(origin, min_distance=SPAWN_MIN_DISTANCE, max_distance=SPAWN_MAX_DISTANCE):
    angle = random.uniform(0.0, math.tau)
    distance = random.uniform(min_distance, max_distance)
    return origin + type(origin)(math.cos(angle) * distance, 0, math.sin(angle) * distance)
