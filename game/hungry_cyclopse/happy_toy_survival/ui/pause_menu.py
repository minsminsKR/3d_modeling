from __future__ import annotations

from ursina import Button, Entity, Slider, Text, color, mouse


class PauseMenu:
    def __init__(self, pause_manager, camera_controller, on_restart, on_quit):
        self.pause_manager = pause_manager
        self.camera_controller = camera_controller
        self.root = Entity(enabled=False)
        Text("PAUSED", parent=self.root, y=0.26, scale=2.0, origin=(0, 0), color=color.white)
        Button("Resume", parent=self.root, y=0.08, scale=(0.28, 0.07), on_click=self.pause_manager.resume)
        Button("Restart", parent=self.root, y=-0.03, scale=(0.28, 0.07), on_click=on_restart)
        Text("Mouse Sensitivity", parent=self.root, y=-0.14, x=-0.16, scale=0.8, color=color.light_gray)
        self.slider = Slider(35, 160, default=self.camera_controller.sensitivity, parent=self.root, y=-0.20, scale=0.45)
        Button("Quit", parent=self.root, y=-0.34, scale=(0.28, 0.07), color=color.rgb(90, 35, 40), on_click=on_quit)

    def update(self):
        self.root.enabled = self.pause_manager.paused
        self.camera_controller.sensitivity = self.slider.value
        try:
            mouse.locked = not self.pause_manager.paused
        except Exception:
            pass
