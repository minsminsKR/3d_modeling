from __future__ import annotations

from ursina import Entity, Vec3, color, destroy, invoke


class ParticleEffects:
    def __init__(self):
        self.active = 0
        self.max_active = 30

    def eat(self, position: Vec3):
        if self.active >= self.max_active:
            return
        self.active += 1
        burst = Entity(model="sphere", position=position + Vec3(0, 1.0, 0), scale=0.7, color=color.rgba(210, 70, 80, 150))
        burst.animate_scale(3.0, duration=0.28)
        burst.animate_color(color.rgba(210, 70, 80, 0), duration=0.28)
        invoke(self._finish, burst, delay=0.32)

    def _finish(self, entity):
        destroy(entity)
        self.active = max(0, self.active - 1)
