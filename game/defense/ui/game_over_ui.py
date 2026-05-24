from __future__ import annotations
from ursina import Entity, Text, Button, color, destroy, camera

class GameOverUI:
    def __init__(self, restart_callback):
        self.restart_callback = restart_callback

        self.background = Entity(
            parent=camera.ui,
            model="quad",
            color=color.rgba32(30, 0, 0, 220),
            scale=(2, 2),
            enabled=False,
            z=-1
        )
        
        self.title = Text(
            text="YOU DIED",
            parent=self.background,
            position=(0, 0.25),
            scale=4.5,
            color=color.red,
            origin=(0, 0)
        )
        
        self.stats_text = Text(
            text="Kills: 0\nTime: 0.0s",
            parent=self.background,
            position=(0, 0.05),
            scale=2.0,
            color=color.white,
            origin=(0, 0)
        )
        
        self.restart_btn = Button(
            text="Restart (R)",
            parent=self.background,
            position=(0, -0.15),
            scale=(0.3, 0.08),
            color=color.rgb32(200, 50, 50),
            on_click=self.restart_callback
        )

    def show(self, kills: int, elapsed_time: float):
        self.stats_text.text = f"Kills: {kills}\nTime: {elapsed_time:.1f}s"
        self.background.enabled = True

    def hide(self):
        self.background.enabled = False

    def destroy(self):
        destroy(self.title)
        destroy(self.stats_text)
        destroy(self.restart_btn)
        destroy(self.background)
