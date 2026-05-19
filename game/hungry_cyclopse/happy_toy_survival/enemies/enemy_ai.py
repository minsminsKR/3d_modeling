from __future__ import annotations

import random

from ursina import Vec3, time


class EnemyAI:
    def __init__(self, enemy):
        self.enemy = enemy
        self.state = "wandering"
        self.direction = Vec3(random.uniform(-1, 1), 0, random.uniform(-1, 1)).normalized()
        self.next_turn = 0.0

    def _wander(self):
        if time.time() > self.next_turn or self.direction.length() == 0:
            self.direction = Vec3(random.uniform(-1, 1), 0, random.uniform(-1, 1)).normalized()
            self.next_turn = time.time() + random.uniform(1.0, 3.0)
        return self.direction

    def choose_direction(self, player, is_giant: bool):
        offset = player.position - self.enemy.entity.position
        distance = offset.length()
        to_player = offset.normalized() if distance else Vec3(0, 0, 1)

        if is_giant:
            if self.state == "wandering" and distance < self.enemy.vision_range:
                self.state = "chasing"
            elif self.state == "chasing" and distance > self.enemy.give_up_range:
                self.state = "wandering"
            return to_player if self.state == "chasing" else self._wander()

        if random.random() < 0.02:
            if self.enemy.size < player.size and random.random() < 0.5:
                self.state = "fleeing"
            elif self.enemy.size > player.size and random.random() < 0.5:
                self.state = "chasing"
            else:
                self.state = "wandering"
        if self.state == "fleeing":
            return -to_player
        if self.state == "chasing":
            return to_player
        return self._wander()
