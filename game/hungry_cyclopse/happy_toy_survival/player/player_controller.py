from __future__ import annotations

from ursina import Entity, Vec3, color, held_keys, mouse, time

from core.asset_loader import safe_entity
from core.config import (
    ASSETS,
    BASE_RUN_SPEED,
    BASE_WALK_SPEED,
    PLAYER_BASE_SIZE,
    STAMINA_DRAIN_PER_SECOND,
    STAMINA_MAX,
    STAMINA_RECOVER_PER_SECOND,
    radius_from_size,
    visual_scale_from_size,
)
from player.camera_controller import ThirdPersonCamera
from player.flashlight import Flashlight


class PlayerController:
    def __init__(self):
        self.size = PLAYER_BASE_SIZE
        self.stamina = STAMINA_MAX
        self.dead = False
        asset = ASSETS.characters.get("Cyclopse")
        model = str(asset.model) if asset and asset.model else "sphere"
        texture = str(asset.texture) if asset and asset.texture else None
        self.entity = safe_entity(model=model, fallback_model="sphere", texture=texture, color=color.rgb(110, 105, 130), position=(0, 0.7, 0), collider="box")
        self.camera = ThirdPersonCamera(self.entity)
        self.flashlight = Flashlight(self.entity)
        self.apply_scale()
        try:
            mouse.locked = True
        except Exception:
            pass

    @property
    def position(self) -> Vec3:
        return self.entity.position

    @property
    def radius(self) -> float:
        return radius_from_size(self.size)

    def apply_scale(self):
        scale = visual_scale_from_size(self.size)
        self.entity.scale = (scale, scale, scale)
        self.entity.y = max(0.45, scale * 0.7)

    def grow(self, amount: int = 1):
        self.size += amount
        self.apply_scale()

    def update(self, paused: bool = False):
        dt = time.dt
        if paused or self.dead:
            return
        self.camera.update(dt)
        move = Vec3(0, 0, 0)
        if held_keys["w"]:
            move += self.camera.forward_flat
        if held_keys["s"]:
            move -= self.camera.forward_flat
        if held_keys["d"]:
            move += self.camera.right_flat
        if held_keys["a"]:
            move -= self.camera.right_flat
        if move.length() > 0:
            move = move.normalized()
            wants_run = held_keys["shift"] and self.stamina > 1.0
            speed = BASE_RUN_SPEED if wants_run else BASE_WALK_SPEED
            if wants_run:
                self.stamina = max(0.0, self.stamina - STAMINA_DRAIN_PER_SECOND * dt)
            else:
                self.stamina = min(STAMINA_MAX, self.stamina + STAMINA_RECOVER_PER_SECOND * dt)
            self.entity.position += move * speed * dt
            self.entity.rotation_y = self.camera.yaw
        else:
            self.stamina = min(STAMINA_MAX, self.stamina + STAMINA_RECOVER_PER_SECOND * dt)
        self.flashlight.update(dt)
        self.flashlight.update_pickups(self.entity.position)
