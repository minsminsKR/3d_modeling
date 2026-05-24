from __future__ import annotations
from dataclasses import dataclass
from ursina import Entity, Text, color, destroy, camera


@dataclass
class HUDSnapshot:
    ally_count: int
    weapon_name: str
    kills: int
    elapsed_time: float
    level: int
    exp_ratio: float
    coins: int
    gems: int
    combo: int
    dps_label: str


class HUD:
    def __init__(self):
        self.ally_count_text = Text(text="ARMY 1", position=(-0.86, 0.45), scale=2.1, color=color.cyan)
        self.weapon_text = Text(text="Pistol", position=(-0.86, 0.39), scale=1.55, color=color.lime)
        self.kills_text = Text(text="KOs 0", position=(-0.86, 0.34), scale=1.5, color=color.orange)
        self.coin_text = Text(text="Coins 0  Gems 0", position=(-0.86, 0.29), scale=1.45, color=color.gold)
        self.level_text = Text(text="LV 1", position=(0.62, 0.45), scale=1.8, color=color.white)
        self.exp_back = Entity(model="cube", parent=camera.ui, position=(0, 0.455, 0), scale=(0.7, 0.022, 0.02), color=color.rgba32(30, 30, 40, 180))
        self.exp_fill = Entity(model="cube", parent=camera.ui, position=(-0.35, 0.455, -0.01), scale=(0.01, 0.026, 0.02), color=color.azure)
        self.combo_text = Text(text="", position=(0, 0.34), origin=(0, 0), scale=2.4, color=color.red)
        self.dps_text = Text(text="", position=(0.58, 0.39), scale=1.35, color=color.rgb32(255, 150, 80))
        self.time_text = Text(text="0.0s", position=(0.70, 0.34), scale=1.25, color=color.gray)

    def update_hud(self, snapshot: HUDSnapshot):
        self.ally_count_text.text = f"ARMY {snapshot.ally_count}"
        self.weapon_text.text = snapshot.weapon_name
        self.kills_text.text = f"KOs {snapshot.kills}"
        self.coin_text.text = f"Coins {snapshot.coins}  Gems {snapshot.gems}"
        self.level_text.text = f"LV {snapshot.level}"
        self.dps_text.text = snapshot.dps_label
        self.time_text.text = f"{snapshot.elapsed_time:.1f}s"
        fill_width = max(0.01, 0.7 * snapshot.exp_ratio)
        self.exp_fill.scale_x = fill_width
        self.exp_fill.x = -0.35 + fill_width * 0.5
        if snapshot.combo >= 3:
            self.combo_text.text = f"COMBO x{snapshot.combo}"
            self.combo_text.scale = 2.4 + min(0.9, snapshot.combo * 0.015)
        else:
            self.combo_text.text = ""

    def destroy(self):
        for entity in (
            self.ally_count_text,
            self.weapon_text,
            self.kills_text,
            self.coin_text,
            self.level_text,
            self.exp_back,
            self.exp_fill,
            self.combo_text,
            self.dps_text,
            self.time_text,
        ):
            destroy(entity)
