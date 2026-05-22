from __future__ import annotations
from ursina import Entity, Vec3, color, held_keys, camera

from core.asset_loader import safe_entity
from core.config import (
    ASSETS,
    PLAYER_BASE_SIZE,
    PLAYER_AUTO_FORWARD_SPEED,
    PLAYER_LEFT_RIGHT_SPEED,
    MAP_BOUNDS_X
)

class PlayerController:
    def __init__(self, weapon_system):
        self.weapon_system = weapon_system
        self.dead = False
        self.shoot_timer = 0.0
        
        # Load player model
        asset = ASSETS.characters.get("Cyclopse")
        model = str(asset.model) if asset and asset.model else "sphere"
        texture = str(asset.texture) if asset and asset.texture else None
        
        self.entity = safe_entity(
            model=model,
            fallback_model="sphere",
            texture=texture,
            color=color.rgb(100, 200, 100),
            position=(0, 0.5, 0),
            collider="box"
        )
        self.apply_scale()
        self.setup_camera()

    @property
    def position(self) -> Vec3:
        return self.entity.position

    @property
    def scale(self) -> Vec3:
        return self.entity.scale

    def apply_scale(self):
        self.entity.scale = (PLAYER_BASE_SIZE, PLAYER_BASE_SIZE, PLAYER_BASE_SIZE)
        self.entity.y = PLAYER_BASE_SIZE * 0.5

    def setup_camera(self):
        camera.fov = 75
        self.update_camera(0.016)

    def update_camera(self, dt: float):
        # Position camera behind and above the player, looking slightly ahead
        target_cam_pos = self.entity.position + Vec3(0, 11, -9)
        camera.position = camera.position + (target_cam_pos - camera.position) * min(1.0, dt * 10.0)
        camera.look_at(self.entity.position + Vec3(0, 0, 3.5))

    def update(self, paused: bool, dt: float):
        if self.dead or paused:
            return
            
        # Left/right controls
        move_x = 0.0
        if held_keys["a"] or held_keys["left arrow"]:
            move_x = -1.0
        elif held_keys["d"] or held_keys["right arrow"]:
            move_x = 1.0
            
        # Auto forward movement + User left/right
        new_x = self.entity.x + move_x * PLAYER_LEFT_RIGHT_SPEED * dt
        new_x = max(-MAP_BOUNDS_X, min(MAP_BOUNDS_X, new_x))
        new_z = self.entity.z + PLAYER_AUTO_FORWARD_SPEED * dt
        
        self.entity.position = Vec3(new_x, self.entity.y, new_z)
        
        # Keep character looking straight ahead (+Z)
        self.entity.rotation = Vec3(0, 0, 0)
        
        # Update camera
        self.update_camera(dt)
        
        # Auto shooting
        self.shoot_timer += dt
        wp = self.weapon_system.current_weapon
        if self.shoot_timer >= wp.fire_rate:
            self.shoot_timer = 0.0
            # Shoot bullet forward from player position
            self.weapon_system.shoot(self.entity.position + Vec3(0, 0.4, 0.5))

    def destroy(self):
        from ursina import destroy
        destroy(self.entity)
