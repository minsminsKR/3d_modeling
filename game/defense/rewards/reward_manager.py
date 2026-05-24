from __future__ import annotations
import random
from ursina import Entity, Vec3, color, destroy


class Pickup(Entity):
    def __init__(self):
        super().__init__(model="sphere", scale=0.22, enabled=False, collider=None)
        self.active = False
        self.kind = "coin"
        self.value = 1
        self.velocity = Vec3(0, 0, 0)

    def spawn(self, position: Vec3, kind: str, value: int):
        self.position = position + Vec3(random.uniform(-0.4, 0.4), 0.5, random.uniform(-0.4, 0.4))
        self.kind = kind
        self.value = value
        self.color = color.gold if kind == "coin" else color.azure
        self.velocity = Vec3(random.uniform(-1, 1), 2.8, random.uniform(-1, 1))
        self.active = True
        self.enabled = True

    def deactivate(self):
        self.active = False
        self.enabled = False


class RewardManager:
    def __init__(self, size: int = 80):
        self.pool = [Pickup() for _ in range(size)]

    def _get(self) -> Pickup:
        for pickup in self.pool:
            if not pickup.active:
                return pickup
        pickup = Pickup()
        self.pool.append(pickup)
        return pickup

    def spawn_enemy_rewards(self, position: Vec3, points: int):
        for _ in range(min(8, 1 + points)):
            self._get().spawn(position, "coin", max(1, points))
        if points >= 6 or random.random() < 0.08:
            self._get().spawn(position, "gem", 1)

    def update(self, player_pos: Vec3, progression, particles, dt: float):
        for pickup in self.pool:
            if not pickup.active:
                continue
            to_player = player_pos + Vec3(0, 0.5, 0) - pickup.position
            dist = to_player.length()
            if dist < 7.0:
                pickup.velocity += to_player.normalized() * 18.0 * dt
            pickup.velocity.y -= 6.0 * dt
            pickup.position += pickup.velocity * dt
            if pickup.y < 0.25:
                pickup.y = 0.25
                pickup.velocity.y *= -0.25
            if dist < 0.75:
                if pickup.kind == "coin":
                    progression.coins += pickup.value
                else:
                    progression.gems += pickup.value
                particles.play_pickup(pickup.position, pickup.kind)
                pickup.deactivate()

    def clear(self):
        for pickup in self.pool:
            pickup.deactivate()

    def destroy(self):
        for pickup in self.pool:
            destroy(pickup)
