from __future__ import annotations

import random

from ursina import Vec3, held_keys, invoke, time

from audio.audio_manager import AudioManager
from core.score_manager import ScoreManager
from effects.particle_effects import ParticleEffects
from effects.screen_effects import ScreenEffects
from enemies.enemy_manager import EnemyManager
from player.player_controller import PlayerController
from ui.game_over_ui import GameOverUI
from ui.hud import HUD
from ui.pause_menu import PauseMenu
from world.world_manager import WorldManager
from core.pause_manager import PauseManager


class GameManager:
    def __init__(self, app):
        self.app = app
        self.pause_manager = PauseManager()
        self.score = ScoreManager()
        self.world = WorldManager()
        self.player = PlayerController()
        self.audio = AudioManager()
        self.particles = ParticleEffects()
        self.screen_effects = ScreenEffects()
        self.enemies = EnemyManager(self.player, self.score, self.particles, self.screen_effects, self.audio)
        self.hud = HUD(self.score)
        self.game_over_ui = GameOverUI(self.score)
        self.pause_menu = PauseMenu(self.pause_manager, self.player.camera, self.restart, self.pause_manager.quit)
        self._battery_spawn_timer = 0.0
        self._game_over_shown = False

    def input(self, key: str):
        if key == "escape" and not self.player.dead:
            self.pause_manager.toggle()
        if key == "f" and not self.pause_manager.paused and not self.player.dead:
            self.player.flashlight.toggle()
        if key == "r" and self.player.dead:
            self.restart()

    def restart(self):
        from ursina import scene, destroy

        for entity in list(scene.entities):
            if entity.ignore:
                continue
            destroy(entity)
        invoke(self.app.restart_game, delay=0.02)

    def update(self):
        dt = time.dt
        paused = self.pause_manager.paused
        if not paused and not self.player.dead:
            self.score.update(dt, paused, self.player.dead)
            self.player.update(paused)
            self.world.update(self.player.position)
            self.enemies.update()
            self._update_battery_spawns(dt)
        self.hud.update()
        self.pause_menu.update()
        self.screen_effects.update()
        if self.player.dead and not self._game_over_shown:
            self._game_over_shown = True
            self.game_over_ui.show()

    def _update_battery_spawns(self, dt: float):
        self._battery_spawn_timer -= dt
        if self._battery_spawn_timer > 0 or len(self.player.flashlight.pickups) > 8:
            return
        self._battery_spawn_timer = random.uniform(8.0, 16.0)
        offset = Vec3(random.uniform(-55, 55), 0, random.uniform(-55, 55))
        if offset.length() < 25:
            offset = offset.normalized() * 25
        self.player.flashlight.spawn_pickup(self.player.position + offset)
