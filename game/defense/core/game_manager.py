from __future__ import annotations
from ursina import Entity, Vec3, color, destroy, time, application

from player.player_controller import PlayerController
from allies.ally_manager import AllyManager
from enemies.enemy_manager import EnemyManager
from weapons.weapon_system import WeaponSystem
from gates.gate_manager import GateManager
from ui.hud import HUD
from ui.pause_menu import PauseMenu
from ui.game_over_ui import GameOverUI
from effects.particle_effects import ParticleEffects
from audio.audio_manager import AudioManager

class GameManager:
    def __init__(self, app):
        self.app = app
        self.paused = False
        self.game_over = False
        self.kills = 0
        self.elapsed_time = 0.0

        # Subsystems
        self.particles = ParticleEffects()
        self.audio = AudioManager()
        self.weapons = WeaponSystem()
        
        # Player & Allies
        self.player = PlayerController(self.weapons)
        self.allies = AllyManager(self.weapons)
        
        # World elements
        self.enemies = EnemyManager()
        self.gates = GateManager()
        self.setup_world()

        # UI
        self.hud = HUD()
        self.pause_menu = PauseMenu(self.toggle_pause, self.restart, self.quit_game)
        self.game_over_ui = GameOverUI(self.restart)

    def setup_world(self):
        # Create infinite tiling roads
        self.road_length = 150.0
        self.roads = [
            Entity(model="cube", color=color.rgb(25, 25, 30), scale=(13.0, 0.1, self.road_length), position=(0, -0.05, self.road_length / 2 - 20)),
            Entity(model="cube", color=color.rgb(20, 20, 25), scale=(13.0, 0.1, self.road_length), position=(0, -0.05, self.road_length / 2 - 20 + self.road_length))
        ]
        
        # Add side rails for a cool neon grid aesthetic
        self.rails = [
            Entity(model="cube", color=color.cyan, scale=(0.3, 0.6, self.road_length * 2), position=(-6.5, 0.25, self.road_length - 20)),
            Entity(model="cube", color=color.cyan, scale=(0.3, 0.6, self.road_length * 2), position=(6.5, 0.25, self.road_length - 20))
        ]

        # Lighting
        from ursina import DirectionalLight
        self.light = DirectionalLight(y=10, z=-5, rotation=(45, -45, 0))

    def update_road_tiling(self):
        player_z = self.player.position.z
        for road in self.roads:
            # If player passed the midpoint of this road block, move it forward
            if player_z > road.z + self.road_length / 2:
                road.z += self.road_length * 2

        # Move side rails forward in huge chunks as well
        for rail in self.rails:
            if player_z > rail.z:
                rail.z += self.road_length * 2

    def toggle_pause(self):
        if self.game_over:
            return
        self.paused = not self.paused
        if self.paused:
            self.pause_menu.show()
        else:
            self.pause_menu.hide()

    def update(self):
        dt = time.dt
        if self.game_over:
            # Read restart command on Game Over screen
            return

        if self.paused:
            return

        # Increment timer
        self.elapsed_time += dt

        # Update player controls and forward movement
        self.player.update(self.paused, dt)

        # Update allies formation and automatic shooting
        self.allies.update(self.player.position, dt)

        # Update weapon tier based on total soldiers and kills
        self.weapons.update_weapon(len(self.allies.allies) + 1, self.kills)

        # Update active bullets (from weapon pool)
        for bullet in self.weapons.bullet_pool.pool:
            if bullet.active:
                bullet.update()

        # Update gates and check collisions
        self.gates.update(self.player.position, self.allies, self.particles, self.audio)

        # Update enemies, bullet collisions, and death conditions
        new_kills, player_died = self.enemies.update(
            self.player, self.weapons.bullet_pool, self.allies, self.particles, self.audio, dt
        )
        self.kills += new_kills

        # Handle infinite road tiling
        self.update_road_tiling()

        # Update HUD UI
        self.hud.update_hud(
            len(self.allies.allies) + 1,
            self.weapons.current_weapon.name,
            self.kills,
            self.elapsed_time
        )

        # Death trigger
        if player_died:
            self.trigger_game_over()

    def input(self, key: str):
        if key == "escape" and not self.game_over:
            self.toggle_pause()
        if key == "r" and self.game_over:
            self.restart()

    def trigger_game_over(self):
        self.game_over = True
        self.audio.play_death()
        self.game_over_ui.show(self.kills, self.elapsed_time)

    def restart(self):
        # Destroy and cleanup all runtime entities
        self.allies.clear()
        self.enemies.clear()
        self.gates.clear()
        self.weapons.bullet_pool.clear()
        self.player.destroy()

        # Reset states
        self.kills = 0
        self.elapsed_time = 0.0
        self.game_over = False
        self.paused = False

        # Hide menus
        self.pause_menu.hide()
        self.game_over_ui.hide()

        # Re-create player and reset world position
        self.player = PlayerController(self.weapons)
        
        # Reset road positioning
        for i, road in enumerate(self.roads):
            road.position = (0, -0.05, self.road_length / 2 - 20 + i * self.road_length)
        for rail in self.rails:
            rail.position = (rail.x, 0.25, self.road_length - 20)

    def quit_game(self):
        application.quit()

    def destroy_all(self):
        # Final cleanup if app closes
        self.player.destroy()
        self.allies.clear()
        self.enemies.clear()
        self.gates.clear()
        self.hud.destroy()
        self.pause_menu.destroy()
        self.game_over_ui.destroy()
        for road in self.roads:
            destroy(road)
        for rail in self.rails:
            destroy(rail)
        destroy(self.light)
