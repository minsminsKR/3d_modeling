from __future__ import annotations

from ursina import Text, color


class HUD:
    def __init__(self, score_manager):
        self.score = score_manager
        self.text = Text(text="", position=(-0.86, 0.46), scale=1.25, color=color.rgb(230, 225, 215), background=True)

    def update(self):
        self.text.text = f"Score: {self.score.score}\nTime: {int(self.score.survival_time)}"
