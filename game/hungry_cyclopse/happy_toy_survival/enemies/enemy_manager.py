from __future__ import annotations

import math
import random

from ursina import Vec3, camera, time

from core.config import ENEMY_TARGET_COUNT, SPAWN_MAX_DISTANCE, SPAWN_MIN_DISTANCE, choose_weighted, random_ring_position
from enemies.enemy_base import Enemy


class EnemyManager:
    def __init__(self, player, score_manager, particles, screen_effects, audio):
        self.player = player
        self.score = score_manager
        self.particles = particles
        self.screen_effects = screen_effects
        self.audio = audio
        self.enemies: list[Enemy] = []
        self.pool: list[Enemy] = []
        self.next_spawn_time = 0.0
        self.giant_target_count = 1
        self.giant_growth_timer = 0.0

    def _spawn_position(self) -> Vec3:
        for _ in range(24):
            pos = random_ring_position(self.player.position, SPAWN_MIN_DISTANCE, SPAWN_MAX_DISTANCE)
            to_pos = (pos - camera.world_position).normalized()
            in_front = camera.forward.dot(to_pos) > 0.35
            if not in_front:
                return pos
        return random_ring_position(self.player.position, SPAWN_MAX_DISTANCE * 0.7, SPAWN_MAX_DISTANCE)

    def _pick_enemy_kind_size(self):
        size = self.player.size
        if size >= 60:
            return "Giant Cyclopse", 80
        if size >= 35:
            kind = choose_weighted([("Hwacat", 0.5), ("Uncat", 0.3), ("Hwacat_angry", 0.2)])
        elif size >= 15:
            kind = random.choice(["Hwacat", "Uncat"])
        else:
            kind = "Hwacat"
        ranges = {
            "Hwacat": (2, 20),
            "Uncat": (25, 40),
            "Hwacat_angry": (40, 50),
        }
        low, high = ranges[kind]
        return kind, random.randint(low, high)

    def _activate_enemy(self, kind: str, size: int, position: Vec3):
        if self.pool:
            enemy = self.pool.pop()
            enemy.reset(kind, size, position)
        else:
            enemy = Enemy(kind, size, position)
        self.enemies.append(enemy)

    def maintain_population(self):
        now = time.time()
        if now < self.next_spawn_time:
            return
        self.next_spawn_time = now + 0.15
        if self.player.size >= 60:
            self.giant_growth_timer += 0.15
            self.giant_target_count = min(10, 1 + int(self.score.survival_time // 45))
            target = self.giant_target_count
        else:
            target = ENEMY_TARGET_COUNT
        while len(self.enemies) < target:
            kind, size = self._pick_enemy_kind_size()
            self._activate_enemy(kind, size, self._spawn_position())

    def update(self):
        self.maintain_population()
        for enemy in list(self.enemies):
            enemy.update(self.player)
            distance = (enemy.entity.position - self.player.position).length()
            if distance > SPAWN_MAX_DISTANCE * 2.6:
                self._recycle(enemy)
                continue
            if distance < self.player.radius + enemy.radius:
                self._handle_contact(enemy)

    def _handle_contact(self, enemy: Enemy):
        if self.player.size > enemy.size:
            self.player.grow(1)
            self.score.add_eat_score()
            self.particles.eat(enemy.entity.position)
            self.audio.play_eat()
            self._recycle(enemy)
        elif self.player.size < enemy.size:
            self.player.dead = True
            self.screen_effects.death()
            self.audio.play_death()
        else:
            away = self.player.position - enemy.entity.position
            enemy.apply_knockback(-away, 13.0)
            if away.length():
                self.player.entity.position += away.normalized() * 2.0

    def _recycle(self, enemy: Enemy):
        if enemy in self.enemies:
            self.enemies.remove(enemy)
        enemy.disable()
        self.pool.append(enemy)
