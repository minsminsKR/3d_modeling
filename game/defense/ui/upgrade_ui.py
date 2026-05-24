from __future__ import annotations
from ursina import Entity, Text, Button, color, destroy, camera


class UpgradeUI:
    def __init__(self, on_pick):
        self.on_pick = on_pick
        self.root = Entity(parent=camera.ui, enabled=False)
        self.title = Text(text="LEVEL UP", parent=self.root, position=(0, 0.25), origin=(0, 0), scale=3.0, color=color.gold)
        self.buttons: list[Button] = []

    def show(self, options):
        self.clear_buttons()
        self.root.enabled = True
        x_positions = [-0.42, 0.0, 0.42]
        for idx, option in enumerate(options):
            btn = Button(
                text=f"{option.title}\n{option.description}",
                parent=self.root,
                position=(x_positions[idx], -0.04),
                scale=(0.34, 0.20),
                color=color.rgb32(35, 45, 65),
                highlight_color=color.rgb32(70, 90, 130),
                pressed_color=color.rgb32(110, 130, 180),
            )
            btn.text_entity.scale = 1.0
            btn.on_click = lambda key=option.key: self.pick(key)
            self.buttons.append(btn)

    def pick(self, key: str):
        self.hide()
        self.on_pick(key)

    def hide(self):
        self.root.enabled = False
        self.clear_buttons()

    def clear_buttons(self):
        for btn in self.buttons:
            destroy(btn)
        self.buttons.clear()

    def destroy(self):
        self.clear_buttons()
        destroy(self.title)
        destroy(self.root)
