from __future__ import annotations
import math
import random
from ursina import Vec3, color, destroy

from core.asset_loader import safe_entity



class FormationPlanner:
    def __init__(self):
        self.cached_count = -1
        self.offsets: list[Vec3] = []

    def build(self, count: int) -> list[Vec3]:
        if count == self.cached_count:
            return self.offsets
        self.cached_count = count
        self.offsets = []
        if count <= 0:
            return self.offsets

        columns = min(18, max(3, int(math.sqrt(count) * 1.9)))
        spacing = max(0.48, 0.92 - min(0.36, count * 0.0018))
        row_gap = spacing * 0.95

        for index in range(count):
            row = index // columns
            col = index % columns
            row_count = min(columns, count - row * columns)
            width = (row_count - 1) * spacing
            x = col * spacing - width * 0.5
            z = -1.15 - row * row_gap
            wave = math.sin(index * 0.73) * 0.07
            self.offsets.append(Vec3(x + wave, 0, z))
        return self.offsets


class Ally:
    def __init__(self, index: int):
        self.index = index
        self.offset = Vec3(0, 0, 0)
        self.entity = safe_entity(
            model="cube",
            fallback_model="cube",
            color=color.rgb32(95, 185, 255),
            scale=(0.62, 0.62, 0.62),
            collider=None,
        )
        self.entity.y = 0.31
        self.shoot_timer = random.uniform(0.0, 0.35)
        self.update_accumulator = random.uniform(0.0, 0.05)
        self.last_target = Vec3(0, 0, 0)

    def update_visual(self, leader_pos: Vec3, weapon_system, dt: float, quality: int, progression=None):
        target_pos = leader_pos + self.offset
        target_pos.x = max(-6.1, min(6.1, target_pos.x))
        lerp_speed = 10.0 if quality == 0 else 5.0
        self.entity.position = self.entity.position + (target_pos - self.entity.position) * min(1.0, dt * lerp_speed)
        self.entity.rotation_y = math.sin((self.index + leader_pos.z) * 0.18) * 2.0

        self.shoot_timer += dt
        wp = weapon_system.current_weapon
        cadence = wp.effective_fire_rate(weapon_system)
        if quality == 2:
            cadence *= 2.6
        elif quality == 1:
            cadence *= 1.45
        if self.shoot_timer >= cadence:
            self.shoot_timer = random.uniform(0.0, cadence * 0.18)
            weapon_system.shoot(self.entity.position + Vec3(0, 0.28, 0.42))

    def destroy(self):
        destroy(self.entity)


class AllyManager:
    def __init__(self, weapon_system, max_allies: int = 250):
        self.weapon_system = weapon_system
        self.allies: list[Ally] = []
        self.max_allies = max_allies
        self.formation = FormationPlanner()
        self._quality_cursor = 0

    def set_ally_count(self, count: int, leader_pos: Vec3):
        count = max(0, min(self.max_allies, count))
        current_count = len(self.allies)
        if count > current_count:
            for i in range(current_count, count):
                ally = Ally(i)
                ally.entity.position = leader_pos + Vec3(random.uniform(-2.0, 2.0), 0, random.uniform(-3.5, -0.8))
                self.allies.append(ally)
        elif count < current_count:
            for i in range(current_count - 1, count - 1, -1):
                self.allies[i].destroy()
                self.allies.pop(i)
        self._refresh_offsets()

    def _refresh_offsets(self):
        offsets = self.formation.build(len(self.allies))
        for idx, ally in enumerate(self.allies):
            ally.index = idx
            ally.offset = offsets[idx]
            ally.entity.scale = (0.58, 0.58, 0.58) if len(self.allies) > 120 else (0.64, 0.64, 0.64)

    def add_allies(self, amount: int, leader_pos: Vec3):
        self.set_ally_count(len(self.allies) + amount, leader_pos)

    def multiply_allies(self, multiplier: float, leader_pos: Vec3):
        crowd_count = len(self.allies) + 1
        new_crowd = int(crowd_count * multiplier)
        self.set_ally_count(max(0, new_crowd - 1), leader_pos)

    def remove_ally(self, index: int):
        if 0 <= index < len(self.allies):
            self.allies[index].destroy()
            self.allies.pop(index)
            self._refresh_offsets()

    def update(self, leader_pos: Vec3, dt: float, progression=None):
        if progression:
            self.max_allies = progression.ally_cap
        total = len(self.allies)
        for idx, ally in enumerate(self.allies):
            if idx < 72:
                quality = 0
                step = 1
            elif idx < 150:
                quality = 1
                step = 2
            else:
                quality = 2
                step = 4
            if step > 1 and (idx + self._quality_cursor) % step != 0:
                ally.shoot_timer += dt
                continue
            ally.update_visual(leader_pos, self.weapon_system, dt * step, quality, progression)
        self._quality_cursor = (self._quality_cursor + 1) % 8

    def clear(self):
        for ally in self.allies:
            ally.destroy()
        self.allies.clear()
        self.formation.cached_count = -1
