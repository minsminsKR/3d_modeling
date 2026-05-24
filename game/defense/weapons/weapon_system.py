from __future__ import annotations
import math
import random
from ursina import Entity, Vec3, color, destroy, invoke


class Bullet(Entity):
    def __init__(self):
        super().__init__(model="cube", color=color.yellow, scale=(0.12, 0.12, 0.65), enabled=False)
        self.damage = 10
        self.speed = 35.0
        self.direction = Vec3(0, 0, 1)
        self.active = False
        self.range = 42.0
        self.start_pos = Vec3(0, 0, 0)
        self.radius = 0.22
        self.explosion_radius = 0.0
        self.crit = False
        self.trail = Entity(model="cube", color=color.rgba32(255, 255, 120, 80), scale=(0.05, 0.05, 1.4), enabled=False)

    def fire(self, start_pos: Vec3, direction: Vec3, damage: int, speed: float, col, explosion_radius: float = 0.0, crit: bool = False):
        self.position = start_pos
        self.start_pos = Vec3(start_pos)
        self.direction = direction.normalized()
        self.damage = damage
        self.speed = speed
        self.color = col
        self.explosion_radius = explosion_radius
        self.crit = crit
        self.scale = (0.18, 0.18, 0.85) if crit else (0.12, 0.12, 0.65)
        self.enabled = True
        self.active = True
        self.trail.enabled = True
        self.trail.color = color.rgba32(col.r * 255, col.g * 255, col.b * 255, 70)

    def update(self, dt: float | None = None):
        if dt is None:
            return
        if not self.active:
            return
        self.position += self.direction * self.speed * dt
        self.look_at(self.position + self.direction)
        self.trail.position = self.position - self.direction * 0.65
        self.trail.look_at(self.position)
        if (self.position - self.start_pos).length() > self.range:
            self.deactivate()

    def deactivate(self):
        self.enabled = False
        self.active = False
        self.trail.enabled = False

    def destroy_self(self):
        destroy(self.trail)
        destroy(self)


class BulletPool:
    def __init__(self, size=260):
        self.pool = [Bullet() for _ in range(size)]
        self.index = 0
        self.max_size = 420

    def get_bullet(self) -> Bullet | None:
        start = self.index
        while True:
            bullet = self.pool[self.index]
            self.index = (self.index + 1) % len(self.pool)
            if not bullet.active:
                return bullet
            if self.index == start:
                if len(self.pool) >= self.max_size:
                    return None
                new_bullet = Bullet()
                self.pool.append(new_bullet)
                return new_bullet

    def update(self, dt: float):
        for bullet in self.pool:
            if bullet.active:
                bullet.update(dt)

    def clear(self):
        for bullet in self.pool:
            bullet.deactivate()

    def destroy(self):
        for bullet in self.pool:
            bullet.destroy_self()


class MuzzleFlashPool:
    def __init__(self, size: int = 36):
        self.pool = [Entity(model="sphere", scale=0.22, color=color.rgba32(255, 230, 80, 180), enabled=False) for _ in range(size)]
        self.index = 0

    def play(self, position: Vec3, col):
        flash = self.pool[self.index]
        self.index = (self.index + 1) % len(self.pool)
        flash.position = position
        flash.color = col
        flash.scale = 0.22
        flash.enabled = True
        flash.animate_scale(0.65, duration=0.055)
        invoke(setattr, flash, "enabled", False, delay=0.07)

    def destroy(self):
        for flash in self.pool:
            destroy(flash)


class BaseWeapon:
    name = "Pistol"
    tier = 0
    fire_rate = 0.45
    damage = 12
    bullet_speed = 36.0
    spread = 0.0
    projectile_count = 1
    recoil = 0.04
    color = color.yellow
    sound_key = "pistol"
    explosion_radius = 0.0

    def fire(self, system, position: Vec3, direction: Vec3 = Vec3(0, 0, 1)):
        count = max(1, self.projectile_count + system.extra_projectiles)
        total_spread = self.spread + system.spread_bonus
        for i in range(count):
            lane_offset = 0 if count == 1 else (i - (count - 1) * 0.5) * 0.28
            spread_angle = random.uniform(-total_spread, total_spread)
            rad = math.radians(spread_angle)
            bullet_dir = Vec3(direction.x + math.sin(rad), direction.y, direction.z).normalized()
            bullet = system.bullet_pool.get_bullet()
            if not bullet:
                return
            crit = random.random() < system.crit_chance
            damage = int(self.damage * system.damage_mult * (2.5 if crit else 1.0))
            bullet.fire(position + Vec3(lane_offset, 0, 0.1), bullet_dir, damage, self.bullet_speed, self.color, self.explosion_radius, crit)
        system.muzzle_flashes.play(position + Vec3(0, 0.05, 0.35), self.color)
        system.event_bus.emit("weapon_fired", weapon=self.name, position=position, sound_key=self.sound_key)

    def effective_fire_rate(self, system) -> float:
        return max(0.035, self.fire_rate * system.fire_rate_mult)


class Pistol(BaseWeapon):
    name = "Pistol"


class DualPistol(BaseWeapon):
    name = "Dual Pistol"
    tier = 1
    fire_rate = 0.34
    damage = 12
    spread = 1.4
    projectile_count = 2
    color = color.cyan


class SMG(BaseWeapon):
    name = "SMG"
    tier = 2
    fire_rate = 0.14
    damage = 8
    bullet_speed = 42.0
    spread = 4.0
    color = color.lime
    sound_key = "smg"


class Rifle(BaseWeapon):
    name = "Rifle"
    tier = 3
    fire_rate = 0.21
    damage = 24
    bullet_speed = 48.0
    spread = 0.7
    color = color.orange
    sound_key = "rifle"


class Shotgun(BaseWeapon):
    name = "Shotgun"
    tier = 4
    fire_rate = 0.58
    damage = 13
    bullet_speed = 34.0
    spread = 10.0
    projectile_count = 6
    color = color.rgb32(255, 120, 50)
    sound_key = "shotgun"


class Laser(BaseWeapon):
    name = "Laser"
    tier = 5
    fire_rate = 0.09
    damage = 9
    bullet_speed = 62.0
    spread = 0.3
    projectile_count = 2
    color = color.azure
    sound_key = "laser"


class Minigun(BaseWeapon):
    name = "Minigun"
    tier = 6
    fire_rate = 0.055
    damage = 10
    bullet_speed = 54.0
    spread = 6.5
    color = color.red
    sound_key = "minigun"


class RocketLauncher(BaseWeapon):
    name = "Rocket Launcher"
    tier = 7
    fire_rate = 0.72
    damage = 80
    bullet_speed = 30.0
    spread = 1.0
    projectile_count = 1
    color = color.magenta
    sound_key = "rocket"
    explosion_radius = 2.4


WEAPON_CLASSES = [Pistol, DualPistol, SMG, Rifle, Shotgun, Laser, Minigun, RocketLauncher]


class WeaponSystem:
    def __init__(self, event_bus=None):
        from core.events import EventBus
        self.event_bus = event_bus or EventBus()
        self.weapons = [cls() for cls in WEAPON_CLASSES]
        self.current_weapon = self.weapons[0]
        self.bullet_pool = BulletPool()
        self.muzzle_flashes = MuzzleFlashPool()
        self.fire_rate_mult = 1.0
        self.damage_mult = 1.0
        self.extra_projectiles = 0
        self.spread_bonus = 0.0
        self.crit_chance = 0.0

    def set_run_modifiers(self, progression):
        self.fire_rate_mult = progression.fire_rate_mult
        self.damage_mult = progression.damage_mult
        self.extra_projectiles = progression.extra_projectiles
        self.spread_bonus = progression.spread_bonus
        self.crit_chance = progression.crit_chance

    def get_weapon_for_power(self, ally_count: int, kills: int):
        score = ally_count + kills * 0.45
        if score >= 135:
            return self.weapons[7]
        if score >= 95:
            return self.weapons[6]
        if score >= 68:
            return self.weapons[5]
        if score >= 45:
            return self.weapons[4]
        if score >= 25:
            return self.weapons[3]
        if score >= 11:
            return self.weapons[2]
        if score >= 4:
            return self.weapons[1]
        return self.weapons[0]

    def update_weapon(self, ally_count: int, kills: int):
        new_weapon = self.get_weapon_for_power(ally_count, kills)
        if new_weapon.name != self.current_weapon.name:
            old = self.current_weapon
            self.current_weapon = new_weapon
            self.event_bus.emit("weapon_upgraded", old=old.name, new=new_weapon.name)

    def force_random_weapon(self):
        self.current_weapon = random.choice(self.weapons[2:])
        self.event_bus.emit("weapon_upgraded", old="Random", new=self.current_weapon.name)

    def shoot(self, position: Vec3, direction: Vec3 = Vec3(0, 0, 1)):
        self.current_weapon.fire(self, position, direction)

    def clear(self):
        self.current_weapon = self.weapons[0]
        self.bullet_pool.clear()

    def destroy(self):
        self.bullet_pool.destroy()
        self.muzzle_flashes.destroy()
