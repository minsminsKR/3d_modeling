from __future__ import annotations
from ursina import Entity, Vec3, color, destroy, time, application, camera

from core.events import EventBus
from core.progression import RunProgression
from player.player_controller import PlayerController
from allies.ally_manager import AllyManager
from enemies.enemy_manager import EnemyManager
from weapons.weapon_system import WeaponSystem
from gates.gate_manager import GateManager, GateSpec
from ui.hud import HUD, HUDSnapshot
from ui.pause_menu import PauseMenu
from ui.game_over_ui import GameOverUI
from ui.upgrade_ui import UpgradeUI
from effects.particle_effects import ParticleEffects
from audio.audio_manager import AudioManager
from rewards.reward_manager import RewardManager
from world.segment_manager import SegmentManager


class GameManager:
    def __init__(self, app):
        self.app = app
        self.paused = False
        self.upgrade_paused = False
        self.game_over = False
        self.kills = 0
        self.elapsed_time = 0.0
        self.slowmo_timer = 0.0
        self.normal_time_scale = 1.0

        self.events = EventBus()
        self.progression = RunProgression()
        self.particles = ParticleEffects()
        self.audio = AudioManager(event_bus=self.events)
        self.weapons = WeaponSystem(self.events)
        self.weapons.set_run_modifiers(self.progression)

        self.player = PlayerController(self.weapons)
        self.allies = AllyManager(self.weapons, self.progression.ally_cap)
        self.enemies = EnemyManager()
        self.gates = GateManager()
        self.rewards = RewardManager()
        self.world = SegmentManager()
        self.allies.add_allies(2, Vec3(0, 0, 0))
        self.setup_world()

        self.hud = HUD()
        self.pause_menu = PauseMenu(self.toggle_pause, self.restart, self.quit_game)
        self.game_over_ui = GameOverUI(self.restart)
        self.upgrade_ui = UpgradeUI(self.apply_upgrade)
        self.events.on("weapon_upgraded", self.on_weapon_upgraded)

    def setup_world(self):
        from ursina import DirectionalLight, AmbientLight
        self.rails = [
            Entity(model="cube", color=color.rgba32(0, 210, 255, 120), scale=(0.16, 0.25, 900), position=(-6.5, 0.45, 330)),
            Entity(model="cube", color=color.rgba32(0, 210, 255, 120), scale=(0.16, 0.25, 900), position=(6.5, 0.45, 330)),
        ]
        self.light = DirectionalLight(y=10, z=-5, rotation=(45, -45, 0))
        self.ambient = AmbientLight(color=color.rgba32(80, 90, 110, 120))

    def update_road_tiling(self):
        player_z = self.player.position.z
        self.world.update(player_z)
        for rail in self.rails:
            if player_z > rail.z:
                rail.z += 420

    def toggle_pause(self):
        if self.game_over or self.upgrade_paused:
            return
        self.paused = not self.paused
        if self.paused:
            self.pause_menu.show()
        else:
            self.pause_menu.hide()

    def update(self):
        dt = time.dt
        if self.game_over:
            return
        if self.paused or self.upgrade_paused:
            return

        if self.slowmo_timer > 0:
            self.slowmo_timer -= dt
            application.time_scale = 0.42
        else:
            application.time_scale = self.normal_time_scale

        self.elapsed_time += dt
        self.progression.update(dt)
        self.player.update(False, dt)
        self.allies.update(self.player.position, dt, self.progression)
        self.weapons.update_weapon(len(self.allies.allies) + 1, self.kills)
        self.weapons.bullet_pool.update(dt)

        difficulty = self.elapsed_time * 0.045 + self.enemies.wave_count * 0.22
        hit_gate = self.gates.update(self.player.position, difficulty, dt)
        if hit_gate:
            self.apply_gate(hit_gate)

        killed_enemies, player_died = self.enemies.update(
            self.player, self.weapons.bullet_pool, self.allies, self.particles, self.audio, dt, self.progression
        )
        for enemy in killed_enemies:
            self.kills += enemy.points
            self.progression.add_kill_reward(enemy.points)
            self.rewards.spawn_enemy_rewards(enemy.position, enemy.points)
            self.audio.play_death()

        self.rewards.update(self.player.position, self.progression, self.particles, dt)
        self.particles.update(dt)
        self.update_road_tiling()
        self.update_pressure_visuals()
        self.update_hud()

        if self.progression.pending_upgrade:
            self.show_upgrade_choice()
        if player_died:
            self.trigger_game_over()

    def apply_gate(self, spec: GateSpec):
        if spec.gate_type == "add":
            self.allies.add_allies(int(spec.value), self.player.position)
        elif spec.gate_type == "mult":
            self.allies.multiply_allies(int(spec.value), self.player.position)
        elif spec.gate_type == "fire_rate":
            self.progression.apply_upgrade("fire_rate", weapon_system=self.weapons, player=self.player)
        elif spec.gate_type == "damage":
            self.progression.apply_upgrade("damage", weapon_system=self.weapons, player=self.player)
        elif spec.gate_type == "spread":
            self.progression.apply_upgrade("spread", weapon_system=self.weapons, player=self.player)
        elif spec.gate_type == "random_weapon":
            self.weapons.force_random_weapon()
        self.particles.play_gate_crossing(Vec3(self.player.position.x, 1.0, self.player.position.z), spec.dramatic)
        self.audio.play_gate()
        if spec.dramatic:
            self.slowmo_timer = 0.22
            camera.fov = 68

    def on_weapon_upgraded(self, **_):
        self.slowmo_timer = max(self.slowmo_timer, 0.12)
        self.particles.camera_shake(0.045, 0.14)

    def show_upgrade_choice(self):
        self.upgrade_paused = True
        application.time_scale = 0.0
        self.upgrade_ui.show(self.progression.roll_options())

    def apply_upgrade(self, key: str):
        application.time_scale = self.normal_time_scale
        self.progression.apply_upgrade(
            key,
            ally_manager=self.allies,
            player_pos=self.player.position,
            player=self.player,
            weapon_system=self.weapons,
        )
        self.particles.play_gate_crossing(self.player.position + Vec3(0, 1.0, 0), True)
        self.audio.play_gate()
        self.upgrade_paused = False

    def update_pressure_visuals(self):
        pressure = min(1.0, self.enemies.active_count() / 55.0)
        camera.fov = camera.fov + ((75 + pressure * 8) - camera.fov) * min(1.0, time.dt * 2.0)

    def update_hud(self):
        weapon = self.weapons.current_weapon
        fire_rate = weapon.effective_fire_rate(self.weapons)
        dps = int((weapon.damage * self.weapons.damage_mult * weapon.projectile_count) / max(0.03, fire_rate))
        self.hud.update_hud(
            HUDSnapshot(
                ally_count=len(self.allies.allies) + 1,
                weapon_name=weapon.name,
                kills=self.kills,
                elapsed_time=self.elapsed_time,
                level=self.progression.level,
                exp_ratio=self.progression.exp_ratio,
                coins=self.progression.coins,
                gems=self.progression.gems,
                combo=self.progression.combo,
                dps_label=f"DPS {dps}",
            )
        )

    def input(self, key: str):
        if key == "escape" and not self.game_over:
            self.toggle_pause()
        if key == "r" and self.game_over:
            self.restart()

    def trigger_game_over(self):
        self.game_over = True
        application.time_scale = self.normal_time_scale
        self.audio.play_death()
        self.game_over_ui.show(self.kills, self.elapsed_time)

    def restart(self):
        application.time_scale = self.normal_time_scale
        self.allies.clear()
        self.enemies.clear()
        self.gates.clear()
        self.rewards.clear()
        self.weapons.clear()
        self.world.clear()
        self.world = SegmentManager()
        self.player.destroy()
        self.progression = RunProgression()
        self.weapons.set_run_modifiers(self.progression)
        self.kills = 0
        self.elapsed_time = 0.0
        self.slowmo_timer = 0.0
        self.game_over = False
        self.paused = False
        self.upgrade_paused = False
        self.pause_menu.hide()
        self.game_over_ui.hide()
        self.upgrade_ui.hide()
        self.player = PlayerController(self.weapons)
        self.allies.add_allies(2, self.player.position)
        for rail in self.rails:
            rail.z = 330

    def quit_game(self):
        application.quit()

    def destroy_all(self):
        application.time_scale = self.normal_time_scale
        self.player.destroy()
        self.allies.clear()
        self.enemies.destroy()
        self.gates.clear()
        self.rewards.destroy()
        self.weapons.destroy()
        self.world.clear()
        self.hud.destroy()
        self.pause_menu.destroy()
        self.game_over_ui.destroy()
        self.upgrade_ui.destroy()
        for rail in self.rails:
            destroy(rail)
        destroy(self.light)
        destroy(self.ambient)
