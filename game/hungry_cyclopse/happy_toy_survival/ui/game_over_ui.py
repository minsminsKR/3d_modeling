from __future__ import annotations

from ursina import Entity, Text, color


class GameOverUI:
    def __init__(self, score_manager):
        self.score = score_manager
        self.root = Entity(enabled=False)
        self.title = Text("YOU WERE CONSUMED", parent=self.root, y=0.12, scale=2.1, origin=(0, 0), color=color.rgb(235, 55, 65))
        self.final = Text("", parent=self.root, y=-0.02, scale=1.15, origin=(0, 0), color=color.white)
        self.restart = Text("Press R to Restart", parent=self.root, y=-0.16, scale=1.0, origin=(0, 0), color=color.light_gray)

    def show(self):
        self.root.enabled = True
        self.final.text = f"Final Score: {self.score.score}\nSurvival Time: {int(self.score.survival_time)}"
