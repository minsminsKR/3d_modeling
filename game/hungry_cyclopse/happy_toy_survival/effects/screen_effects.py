from __future__ import annotations

from ursina import Entity, color, camera, time


class ScreenEffects:
    def __init__(self):
        self.overlay = Entity(parent=camera.ui, model="quad", scale=(2, 2), color=color.rgba(0, 0, 0, 0), z=1)
        self.shake_timer = 0.0

    def death(self):
        self.shake_timer = 0.55
        self.overlay.color = color.rgba(0, 0, 0, 185)

    def update(self):
        if self.shake_timer > 0:
            self.shake_timer -= time.dt
            camera.x += (0.5 - time.time() % 1) * 0.018
