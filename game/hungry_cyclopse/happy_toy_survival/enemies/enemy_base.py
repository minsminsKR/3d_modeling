from __future__ import annotations

import random

from ursina import Entity, Vec3, color, destroy, time

from core.asset_loader import safe_entity
from core.config import ASSETS, GIANT_SPEED, radius_from_size, visual_scale_from_size
from enemies.enemy_ai import EnemyAI


class Enemy:
    def __init__(self, kind: str, size: int, position: Vec3):
        self.kind = kind
        self.size = size
        self.radius = radius_from_size(size)
        self.is_giant = kind == "Giant Cyclopse"
        self.speed = self._speed_for_kind(kind)
        self.vision_range = 65.0
        self.give_up_range = 95.0
        self.active = True
        self.entity = self._make_entity(position)
        self.ai = EnemyAI(self)
        self.knockback = Vec3(0, 0, 0)

    def _speed_for_kind(self, kind: str) -> float:
        if kind == "Giant Cyclopse":
            return GIANT_SPEED
        if kind == "Hwacat_angry":
            return 7.4
        if kind == "Uncat":
            return 6.6
        return 5.4

    def _make_entity(self, position: Vec3) -> Entity:
        asset_key = "Cyclopse" if self.is_giant else self.kind
        asset = ASSETS.characters.get(asset_key)
        model = str(asset.model) if asset and asset.model else ("sphere" if self.is_giant else "cube")
        texture = str(asset.texture) if asset and asset.texture else None
        tint = color.rgb(115, 80, 95) if self.is_giant else color.rgb(100, 92, 86)
        entity = safe_entity(model=model, fallback_model=("sphere" if self.is_giant else "cube"), texture=texture, position=position, color=tint, collider=None)
        scale = visual_scale_from_size(self.size)
        entity.scale = (scale, scale, scale)
        entity.y = max(0.4, scale * 0.55)
        return entity

    def reset(self, kind: str, size: int, position: Vec3):
        self.kind = kind
        self.size = size
        self.radius = radius_from_size(size)
        self.is_giant = kind == "Giant Cyclopse"
        self.speed = self._speed_for_kind(kind)
        self.entity.position = position
        self.entity.enabled = True
        self.active = True
        scale = visual_scale_from_size(size)
        self.entity.scale = (scale, scale, scale)
        self.ai = EnemyAI(self)

    def update(self, player):
        if not self.active:
            return
        dt = time.dt
        direction = self.ai.choose_direction(player, self.is_giant)
        self.entity.position += direction * self.speed * dt
        if self.knockback.length() > 0.01:
            self.entity.position += self.knockback * dt
            self.knockback *= 0.86
        if direction.length() > 0:
            self.entity.look_at(self.entity.position + direction)
            self.entity.rotation_x = 0
            self.entity.rotation_z = 0
        if not self.is_giant:
            self.entity.rotation_y += random.uniform(-8, 8) * dt

    def apply_knockback(self, away: Vec3, force: float = 10.0):
        self.knockback = away.normalized() * force if away.length() else Vec3(random.uniform(-1, 1), 0, random.uniform(-1, 1)) * force

    def disable(self):
        self.active = False
        self.entity.enabled = False

    def destroy(self):
        destroy(self.entity)
