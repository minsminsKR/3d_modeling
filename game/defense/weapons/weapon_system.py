from __future__ import annotations
import random
from ursina import Entity, Vec3, color, destroy

class Bullet(Entity):
    def __init__(self):
        super().__init__(
            model="cube",
            color=color.yellow,
            scale=(0.15, 0.15, 0.5),
            enabled=False
        )
        self.damage = 10
        self.speed = 35.0
        self.direction = Vec3(0, 0, 1)
        self.active = False
        self.range = 35.0
        self.start_z = 0.0

    def fire(self, start_pos: Vec3, direction: Vec3, damage: int, speed: float, col=color.yellow):
        self.position = start_pos
        self.direction = direction.normalized()
        self.damage = damage
        self.speed = speed
        self.color = col
        self.start_z = start_pos.z
        self.enabled = True
        self.active = True

    def update(self):
        if not self.active:
            return
        # Move forward
        self.position += self.direction * self.speed * 0.016 # fallback approximate dt or handle globally
        # Check range
        if self.position.z - self.start_z > self.range:
            self.deactivate()

    def deactivate(self):
        self.enabled = False
        self.active = False

class BulletPool:
    def __init__(self, size=150):
        self.pool = [Bullet() for _ in range(size)]
        self.index = 0

    def get_bullet(self) -> Bullet:
        # Loop through pool to find inactive bullet
        start = self.index
        while True:
            bullet = self.pool[self.index]
            self.index = (self.index + 1) % len(self.pool)
            if not bullet.active:
                return bullet
            if self.index == start:
                # If pool is full, expand it
                new_bullet = Bullet()
                self.pool.append(new_bullet)
                return new_bullet

    def clear(self):
        for b in self.pool:
            b.deactivate()

class Weapon:
    def __init__(self, name: str, fire_rate: float, damage: int, bullet_speed: float, spread: float, count: int, col):
        self.name = name
        self.fire_rate = fire_rate
        self.damage = damage
        self.bullet_speed = bullet_speed
        self.spread = spread
        self.count = count
        self.color = col

WEAPONS = {
    "Pistol": Weapon("Pistol", 0.45, 12, 35.0, 0.0, 1, color.yellow),
    "Dual Pistol": Weapon("Dual Pistol", 0.35, 12, 35.0, 1.5, 2, color.cyan),
    "SMG": Weapon("SMG", 0.14, 8, 40.0, 4.0, 1, color.lime),
    "Rifle": Weapon("Rifle", 0.22, 22, 45.0, 0.8, 1, color.orange),
    "Minigun": Weapon("Minigun", 0.07, 10, 50.0, 6.0, 1, color.red)
}

class WeaponSystem:
    def __init__(self):
        self.current_weapon = WEAPONS["Pistol"]
        self.bullet_pool = BulletPool()

    def get_weapon_for_allies(self, ally_count: int, kills: int) -> Weapon:
        # Logic for upgrading weapons
        if ally_count >= 40 or kills >= 100:
            return WEAPONS["Minigun"]
        elif ally_count >= 20 or kills >= 50:
            return WEAPONS["Rifle"]
        elif ally_count >= 10 or kills >= 25:
            return WEAPONS["SMG"]
        elif ally_count >= 4 or kills >= 10:
            return WEAPONS["Dual Pistol"]
        return WEAPONS["Pistol"]

    def update_weapon(self, ally_count: int, kills: int):
        self.current_weapon = self.get_weapon_for_allies(ally_count, kills)

    def shoot(self, position: Vec3, direction: Vec3 = Vec3(0, 0, 1)):
        wp = self.current_weapon
        # Handle multiple bullets
        if wp.count == 1:
            spread_angle = random.uniform(-wp.spread, wp.spread)
            rad = spread_angle * 0.0174533
            bullet_dir = Vec3(direction.x + rad, direction.y, direction.z)
            b = self.bullet_pool.get_bullet()
            b.fire(position, bullet_dir, wp.damage, wp.bullet_speed, wp.color)
        else:
            # Multi shot (e.g., dual pistol, offset left and right)
            for i in range(wp.count):
                offset = -0.35 + (i * 0.7)
                start_p = position + Vec3(offset, 0, 0.1)
                spread_angle = random.uniform(-wp.spread, wp.spread)
                rad = spread_angle * 0.0174533
                bullet_dir = Vec3(direction.x + rad, direction.y, direction.z)
                b = self.bullet_pool.get_bullet()
                b.fire(start_p, bullet_dir, wp.damage, wp.bullet_speed, wp.color)
