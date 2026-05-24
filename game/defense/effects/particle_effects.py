from __future__ import annotations
import random
from ursina import Entity, Vec3, color, destroy, invoke, camera


class ParticleEffects:
    def __init__(self):
        self.active = 0
        self.max_active = 90
        self.shake_timer = 0.0
        self.shake_strength = 0.0
        self._last_shake_offset = Vec3(0, 0, 0)

    def update(self, dt: float):
        if self._last_shake_offset.length() > 0:
            camera.position -= self._last_shake_offset
            self._last_shake_offset = Vec3(0, 0, 0)
        if self.shake_timer <= 0:
            return
        self.shake_timer -= dt
        strength = self.shake_strength * max(0.0, self.shake_timer)
        self._last_shake_offset = Vec3(random.uniform(-strength, strength), random.uniform(-strength, strength), 0)
        camera.position += self._last_shake_offset

    def camera_shake(self, strength: float = 0.06, duration: float = 0.12):
        self.shake_strength = max(self.shake_strength, strength)
        self.shake_timer = max(self.shake_timer, duration)

    def spawn_burst(self, position: Vec3, col=color.red, scale=0.5, speed=0.25, pieces: int = 1):
        if self.active >= self.max_active:
            return
        for _ in range(min(pieces, self.max_active - self.active)):
            self.active += 1
            burst = Entity(
                model=random.choice(["sphere", "cube"]),
                position=position + Vec3(random.uniform(-0.15, 0.15), 0.45, random.uniform(-0.15, 0.15)),
                scale=scale * random.uniform(0.55, 1.0),
                color=col,
            )
            burst.animate_position(burst.position + Vec3(random.uniform(-1.0, 1.0), random.uniform(0.4, 1.1), random.uniform(-1.0, 1.0)), duration=speed)
            burst.animate_scale(scale * 2.8, duration=speed)
            fade_color = color.Color(col.r, col.g, col.b, 0.0)
            burst.animate_color(fade_color, duration=speed)
            invoke(self._finish, burst, delay=speed + 0.04)

    def play_hit(self, position: Vec3, combo: int = 0):
        self.spawn_burst(position, col=color.orange, scale=0.22 + min(0.16, combo * 0.004), speed=0.14, pieces=1)
        if combo >= 15:
            self.camera_shake(0.018, 0.06)

    def play_death(self, position: Vec3, points: int = 1):
        self.spawn_burst(position, col=color.rgb32(255, 55, 50), scale=0.45 + min(0.35, points * 0.06), speed=0.28, pieces=min(5, 1 + points // 2))
        self.camera_shake(0.028 + min(0.04, points * 0.004), 0.10)

    def play_gate_crossing(self, position: Vec3, dramatic: bool = False):
        self.spawn_burst(position, col=color.cyan if not dramatic else color.magenta, scale=1.0 if not dramatic else 1.55, speed=0.38, pieces=6 if dramatic else 3)
        self.camera_shake(0.06 if dramatic else 0.035, 0.16)

    def play_pickup(self, position: Vec3, kind: str):
        self.spawn_burst(position, col=color.gold if kind == "coin" else color.azure, scale=0.18, speed=0.12, pieces=1)

    def _finish(self, entity):
        destroy(entity)
        self.active = max(0, self.active - 1)
