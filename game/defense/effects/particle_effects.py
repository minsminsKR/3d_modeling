from __future__ import annotations
import random
from ursina import Entity, Vec3, color, destroy, invoke

class ParticleEffects:
    def __init__(self):
        self.active = 0
        self.max_active = 50

    def spawn_burst(self, position: Vec3, col=color.red, scale=0.5, speed=0.25):
        if self.active >= self.max_active:
            return
        self.active += 1
        burst = Entity(model="sphere", position=position + Vec3(0, 0.5, 0), scale=scale, color=col)
        burst.animate_scale(scale * 3.0, duration=speed)
        # Fade out alpha
        fade_color = color.color(col.h, col.s, col.v, 0.0)
        burst.animate_color(fade_color, duration=speed)
        invoke(self._finish, burst, delay=speed + 0.05)

    def play_hit(self, position: Vec3):
        self.spawn_burst(position, col=color.orange, scale=0.3, speed=0.15)

    def play_death(self, position: Vec3):
        self.spawn_burst(position, col=color.rgb(255, 50, 50), scale=0.7, speed=0.3)

    def play_gate_crossing(self, position: Vec3):
        self.spawn_burst(position, col=color.cyan, scale=1.2, speed=0.4)

    def _finish(self, entity):
        destroy(entity)
        self.active = max(0, self.active - 1)
