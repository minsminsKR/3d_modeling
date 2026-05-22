from __future__ import annotations
from ursina import Text, color, Vec2, destroy

class HUD:
    def __init__(self):
        self.ally_count_text = Text(
            text="Soldiers: 1",
            position=(-0.85, 0.45),
            scale=1.8,
            color=color.cyan
        )
        self.weapon_text = Text(
            text="Weapon: Pistol",
            position=(-0.85, 0.40),
            scale=1.6,
            color=color.lime
        )
        self.kills_text = Text(
            text="Kills: 0",
            position=(-0.85, 0.35),
            scale=1.6,
            color=color.orange
        )
        self.time_text = Text(
            text="Time: 0.0s",
            position=(-0.85, 0.30),
            scale=1.6,
            color=color.white
        )

    def update_hud(self, ally_count: int, weapon_name: str, kills: int, elapsed_time: float):
        self.ally_count_text.text = f"Soldiers: {ally_count}"
        self.weapon_text.text = f"Weapon: {weapon_name}"
        self.kills_text.text = f"Kills: {kills}"
        self.time_text.text = f"Time: {elapsed_time:.1f}s"

    def destroy(self):
        destroy(self.ally_count_text)
        destroy(self.weapon_text)
        destroy(self.kills_text)
        destroy(self.time_text)
