from __future__ import annotations

from ursina import Entity, PointLight, Vec3, color, destroy

from core.config import FLASHLIGHT_DRAIN_PER_SECOND, FLASHLIGHT_MAX_BATTERY, FLASHLIGHT_PICKUP_AMOUNT


class Flashlight:
    def __init__(self, parent):
        self.parent = parent
        self.enabled = True
        self.battery = FLASHLIGHT_MAX_BATTERY
        self.light = PointLight(parent=parent, position=(0, 2.0, 1.2), color=color.rgba(215, 205, 170, 255))
        self.light.range = 24
        self.pickups: list[Entity] = []

    def toggle(self):
        if self.battery <= 0:
            self.enabled = False
        else:
            self.enabled = not self.enabled
        self.light.enabled = self.enabled

    def update(self, dt: float):
        if self.enabled and self.battery > 0:
            self.battery = max(0.0, self.battery - FLASHLIGHT_DRAIN_PER_SECOND * dt)
            if self.battery <= 0:
                self.enabled = False
                self.light.enabled = False

    def add_battery(self):
        self.battery = min(FLASHLIGHT_MAX_BATTERY, self.battery + FLASHLIGHT_PICKUP_AMOUNT)

    def spawn_pickup(self, position: Vec3):
        pickup = Entity(model="cube", color=color.rgb(215, 210, 95), position=position + Vec3(0, 0.65, 0), scale=(0.55, 0.25, 0.55), collider="box")
        self.pickups.append(pickup)

    def update_pickups(self, player_position: Vec3):
        for pickup in list(self.pickups):
            pickup.rotation_y += 80 * 0.016
            if (pickup.position - player_position).length() < 2.0:
                self.add_battery()
                self.pickups.remove(pickup)
                destroy(pickup)
