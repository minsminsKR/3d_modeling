from __future__ import annotations
import math
import random
from ursina import Entity, Vec3, color, destroy

from core.asset_loader import safe_entity
from core.config import ASSETS

class Ally:
    def __init__(self, index: int, parent_manager):
        self.index = index
        self.manager = parent_manager
        self.offset = Vec3(0, 0, 0)
        self.update_offset()
        
        # Load asset for ally
        asset = ASSETS.characters.get("Uncat")
        model = str(asset.model) if asset and asset.model else "cube"
        texture = str(asset.texture) if asset and asset.texture else None
        
        self.entity = safe_entity(
            model=model,
            fallback_model="cube",
            texture=texture,
            color=color.rgb(100, 180, 255),
            scale=(0.7, 0.7, 0.7),
            collider=None
        )
        self.entity.y = 0.35
        self.shoot_timer = random.uniform(0, 0.3) # Stagger shots

    def update_offset(self):
        # V-shape or Grid formation behind leader
        if self.index == 0:
            self.offset = Vec3(0, 0, 0)
            return
        
        row = (self.index - 1) // 4 + 1
        col = (self.index - 1) % 4
        # Spread layout
        x_offset = (col - 1.5) * 0.8
        z_offset = -row * 1.0
        self.offset = Vec3(x_offset, 0, z_offset)

    def update(self, leader_pos: Vec3, weapon_system, dt: float):
        # Target position
        target_pos = leader_pos + self.offset
        # Lerp towards target
        self.entity.position = self.entity.position + (target_pos - self.entity.position) * min(1.0, dt * 8.0)
        # Look forward
        self.entity.rotation_y = 0
        
        # Shoot automatically
        self.shoot_timer += dt
        wp = weapon_system.current_weapon
        if self.shoot_timer >= wp.fire_rate:
            self.shoot_timer = 0
            # Shoot forward from ally position
            weapon_system.shoot(self.entity.position + Vec3(0, 0.3, 0.4))

    def destroy(self):
        destroy(self.entity)

class AllyManager:
    def __init__(self, weapon_system):
        self.weapon_system = weapon_system
        self.allies: list[Ally] = []

    def set_ally_count(self, count: int, leader_pos: Vec3):
        # Make sure we have exactly count allies
        current_count = len(self.allies)
        if count == current_count:
            return
            
        if count > current_count:
            # Spawn new allies
            for i in range(current_count, count):
                ally = Ally(i, self)
                # Spawn near leader
                ally.entity.position = leader_pos + Vec3(random.uniform(-2, 2), 0, random.uniform(-3, -1))
                self.allies.append(ally)
        else:
            # Remove extra allies
            for i in range(current_count - 1, count - 1, -1):
                self.allies[i].destroy()
                self.allies.pop(i)
                
        # Re-index all allies and recalculate offsets
        for idx, ally in enumerate(self.allies):
            ally.index = idx
            ally.update_offset()

    def add_allies(self, amount: int, leader_pos: Vec3):
        new_count = len(self.allies) + amount
        self.set_ally_count(new_count, leader_pos)

    def multiply_allies(self, multiplier: float, leader_pos: Vec3):
        new_count = int(len(self.allies) * multiplier)
        if new_count < 1:
            new_count = 1
        self.set_ally_count(new_count, leader_pos)

    def remove_ally(self, index: int):
        if 0 <= index < len(self.allies):
            self.allies[index].destroy()
            self.allies.pop(index)
            # Re-index
            for idx, ally in enumerate(self.allies):
                ally.index = idx
                ally.update_offset()

    def update(self, leader_pos: Vec3, dt: float):
        for ally in self.allies:
            ally.update(leader_pos, self.weapon_system, dt)

    def clear(self):
        for ally in self.allies:
            ally.destroy()
        self.allies.clear()
