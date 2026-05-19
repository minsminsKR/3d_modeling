from __future__ import annotations

from ursina import application, mouse


class PauseManager:
    def __init__(self):
        self.paused = False

    def toggle(self):
        self.paused = not self.paused
        try:
            mouse.locked = not self.paused
        except Exception:
            pass

    def resume(self):
        self.paused = False
        try:
            mouse.locked = True
        except Exception:
            pass

    def quit(self):
        application.quit()
