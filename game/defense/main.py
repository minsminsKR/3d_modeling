from __future__ import annotations
from pathlib import Path
import sys
from ursina import Ursina, color, window

PACKAGE_DIR = Path(__file__).resolve().parent
if str(PACKAGE_DIR) not in sys.path:
    sys.path.insert(0, str(PACKAGE_DIR))

from core.game_manager import GameManager

app = None
manager = None

def restart_game():
    global manager
    manager = GameManager(app)

def update():
    if manager:
        manager.update()

def input(key):
    if manager:
        manager.input(key)

if __name__ == "__main__":
    app = Ursina(title="Mob Shooter Defense", borderless=False)
    app.restart_game = restart_game
    window.color = color.rgb(10, 10, 15)
    window.exit_button.visible = False
    window.fps_counter.enabled = True
    restart_game()
    app.run()
