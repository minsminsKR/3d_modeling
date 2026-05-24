from __future__ import annotations
from ursina import Entity, Text, Button, color, destroy, application

class PauseMenu:
    def __init__(self, resume_callback, restart_callback, quit_callback):
        self.resume_callback = resume_callback
        self.restart_callback = restart_callback
        self.quit_callback = quit_callback

        self.background = Entity(
            parent=camera.ui,
            model="quad",
            color=color.rgba32(0, 0, 0, 180),
            scale=(2, 2),
            enabled=False,
            z=-1
        )
        
        self.title = Text(
            text="PAUSED",
            parent=self.background,
            position=(0, 0.2),
            scale=3,
            color=color.white,
            origin=(0, 0)
        )
        
        self.resume_btn = Button(
            text="Resume",
            parent=self.background,
            position=(0, 0.05),
            scale=(0.3, 0.06),
            color=color.azure,
            on_click=self.resume_callback
        )
        
        self.restart_btn = Button(
            text="Restart",
            parent=self.background,
            position=(0, -0.05),
            scale=(0.3, 0.06),
            color=color.orange,
            on_click=self.restart_callback
        )
        
        self.quit_btn = Button(
            text="Quit",
            parent=self.background,
            position=(0, -0.15),
            scale=(0.3, 0.06),
            color=color.red,
            on_click=self.quit_callback
        )

    def show(self):
        self.background.enabled = True

    def hide(self):
        self.background.enabled = False

    def destroy(self):
        destroy(self.title)
        destroy(self.resume_btn)
        destroy(self.restart_btn)
        destroy(self.quit_btn)
        destroy(self.background)

# Import camera globally
from ursina import camera
