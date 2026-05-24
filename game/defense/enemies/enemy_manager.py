from __future__ import annotations
import math
import random
from ursina import Entity, Vec3, color, destroy, invoke



ENEMY_DATA = {
    "basic": dict(hp=24, speed=1.8, points=1, scale=0.9, col=color.rgb32(220, 130, 50), asset="Hwacat_angry"),
    "fast": dict(hp=14, speed=3.7, points=2, scale=0.62, col=color.rgb32(255, 65, 65), asset="Hwacat_angry"),
    "tank": dict(hp=75, speed=0.9, points=6, scale=1.55, col=color.rgb32(125, 60, 205), asset="Cyclopse"),
    "exploder": dict(hp=34, speed=2.3, points=4, scale=1.05, col=color.rgb32(255, 90, 25), asset="Hwacat_angry"),
    "shooter": dict(hp=38, speed=1.25, points=5, scale=1.0, col=color.rgb32(60, 130, 255), asset="Hwacat"),
    "boss": dict(hp=650, speed=0.55, points=35, scale=3.2, col=color.rgb32(255, 60, 210), asset="Cyclopse"),
}


class Enemy(Entity):
    def __init__(self):
        super().__init__(model="cube", enabled=False)
        self.enemy_type = "basic"
        self.health = 20
        self.max_health = 20
        self.speed = 2.0
        self.points = 1
        self.active = False
        self.radius = 0.8
        self.shoot_timer = 0.0
        self.knockback = Vec3(0, 0, 0)
        self.base_color = color.white

    def spawn(self, pos: Vec3, enemy_type: str, wave_multiplier: float = 1.0):
        data = ENEMY_DATA[enemy_type]
        self.position = pos
        self.enemy_type = enemy_type
        self.max_health = int(data["hp"] * wave_multiplier)
        self.health = self.max_health
        self.speed = data["speed"] * min(1.65, 1.0 + wave_multiplier * 0.035)
        self.points = data["points"]
        s = data["scale"]
        self.scale = (s, s, s)
        self.radius = s * 0.55
        self.color = data["col"]
        self.base_color = data["col"]
        self.y = s * 0.5
        self.shoot_timer = random.uniform(0.0, 1.4)
        self.knockback = Vec3(0, 0, 0)

        self.model = "sphere" if enemy_type in ("tank", "boss") else "cube"
        self.texture = None
        self.active = True
        self.enabled = True

    def update_enemy(self, player_pos: Vec3, dt: float):
        if not self.active:
            return
        sway = math.sin(self.z * 0.12 + self.x) * 0.25 if self.enemy_type in ("fast", "exploder") else 0.0
        self.position += Vec3(sway, 0, -1) * self.speed * dt
        if self.knockback.length() > 0.02:
            self.position += self.knockback * dt
            self.knockback *= max(0.0, 1.0 - dt * 8.0)
        self.look_at(Vec3(player_pos.x, self.y, player_pos.z))

    def take_damage(self, amount: int, direction: Vec3) -> bool:
        self.health -= amount
        self.knockback += direction.normalized() * min(7.0, 1.5 + amount * 0.05)
        self.color = color.white
        invoke(setattr, self, "color", self.base_color, delay=0.055)
        return self.health <= 0

    def deactivate(self, fake_death: bool = False):
        self.active = False
        if fake_death:
            self.animate_scale(self.scale * 0.08, duration=0.18)
            self.animate_y(self.y + 0.5, duration=0.12)
        self.enabled = False


class EnemyShot(Entity):
    def __init__(self):
        super().__init__(model="sphere", color=color.rgb32(80, 160, 255), scale=0.22, enabled=False)
        self.active = False
        self.direction = Vec3(0, 0, -1)
        self.speed = 9.0

    def fire(self, position: Vec3, target: Vec3):
        self.position = position + Vec3(0, 0.4, 0)
        self.direction = (target - self.position).normalized()
        self.active = True
        self.enabled = True

    def update_shot(self, dt: float):
        if not self.active:
            return
        self.position += self.direction * self.speed * dt

    def deactivate(self):
        self.active = False
        self.enabled = False


class EnemyManager:
    def __init__(self):
        self.enemies: list[Enemy] = []
        self.pool: list[Enemy] = [Enemy() for _ in range(90)]
        self.shots: list[EnemyShot] = [EnemyShot() for _ in range(40)]
        self.next_spawn_z = 25.0
        self.spawn_interval = 18.0
        self.wave_count = 0

    def _get_enemy_from_pool(self) -> Enemy:
        for enemy in self.pool:
            if not enemy.active:
                return enemy
        enemy = Enemy()
        self.pool.append(enemy)
        return enemy

    def _get_shot(self) -> EnemyShot | None:
        for shot in self.shots:
            if not shot.active:
                return shot
        if len(self.shots) < 80:
            shot = EnemyShot()
            self.shots.append(shot)
            return shot
        return None

    def _available_types(self) -> list[str]:
        types = ["basic"]
        if self.wave_count >= 3:
            types.append("fast")
        if self.wave_count >= 5:
            types.append("tank")
        if self.wave_count >= 7:
            types.append("exploder")
        if self.wave_count >= 9:
            types.append("shooter")
        return types

    def spawn_wave(self, player_z: float):
        self.wave_count += 1
        wave_multiplier = 1.0 + self.wave_count * 0.13
        spawn_z = player_z + 66.0
        if self.wave_count % 8 == 0:
            enemy = self._get_enemy_from_pool()
            enemy.spawn(Vec3(0, 0, spawn_z + 7.0), "boss", wave_multiplier)
            self.enemies.append(enemy)
            return

        wave_style = random.choice(["line", "cluster", "double_line", "pinch"])
        types = self._available_types()
        count = random.randint(5, 8) + min(10, self.wave_count // 2)
        if wave_style == "line":
            spacing = 10.0 / max(1, count - 1)
            positions = [Vec3(-5.0 + i * spacing, 0, spawn_z) for i in range(count)]
        elif wave_style == "double_line":
            positions = [Vec3(random.uniform(-5, 5), 0, spawn_z + (i % 2) * 3.2) for i in range(count)]
        elif wave_style == "pinch":
            positions = [Vec3(random.choice([-4.7, 4.7, random.uniform(-1.5, 1.5)]), 0, spawn_z + random.uniform(-2, 4)) for _ in range(count)]
        else:
            positions = [Vec3(random.uniform(-5.0, 5.0), 0, spawn_z + random.uniform(-4, 4)) for _ in range(count)]

        for pos in positions:
            enemy_type = random.choice(types)
            enemy = self._get_enemy_from_pool()
            enemy.spawn(pos, enemy_type, wave_multiplier)
            self.enemies.append(enemy)

    def update(self, player, bullet_pool, ally_manager, particles, audio, dt: float, progression=None) -> tuple[list[Enemy], bool]:
        killed: list[Enemy] = []
        player_died = False
        difficulty = 1.0 + self.wave_count * 0.08

        if player.position.z + 72.0 > self.next_spawn_z:
            self.spawn_wave(player.position.z)
            self.next_spawn_z += max(11.0, self.spawn_interval - min(6.0, self.wave_count * 0.18))

        for enemy in self.enemies:
            if enemy.active:
                enemy.update_enemy(player.position, dt)
                if enemy.enemy_type in ("shooter", "boss"):
                    enemy.shoot_timer += dt
                    delay = 1.8 if enemy.enemy_type == "shooter" else 0.7
                    if enemy.shoot_timer >= delay:
                        enemy.shoot_timer = 0.0
                        shot = self._get_shot()
                        if shot:
                            shot.fire(enemy.position, player.position)

        for shot in self.shots:
            if not shot.active:
                continue
            shot.update_shot(dt)
            if (shot.position - player.position).length() < 0.75:
                shot.deactivate()
                if len(ally_manager.allies) > 0:
                    ally_manager.remove_ally(len(ally_manager.allies) - 1)
                else:
                    player_died = True
            elif shot.z < player.position.z - 8:
                shot.deactivate()

        active_enemies = [e for e in self.enemies if e.active]
        for bullet in [b for b in bullet_pool.pool if b.active]:
            for enemy in active_enemies:
                if not enemy.active or not bullet.active:
                    continue
                if abs(bullet.x - enemy.x) < enemy.scale.x * 0.62 + bullet.radius and abs(bullet.z - enemy.z) < enemy.scale.z * 0.68 + bullet.radius:
                    hit_pos = Vec3(bullet.position)
                    damage = bullet.damage
                    explosion = bullet.explosion_radius
                    bullet.deactivate()
                    particles.play_hit(hit_pos, progression.combo if progression else 0)
                    audio.play_hit()
                    if enemy.take_damage(damage, bullet.direction):
                        killed.append(enemy)
                        enemy.deactivate(fake_death=True)
                        particles.play_death(enemy.position, enemy.points)
                    if explosion > 0:
                        for splash in active_enemies:
                            if splash.active and splash is not enemy and (splash.position - hit_pos).length() < explosion:
                                if splash.take_damage(max(12, damage // 2), bullet.direction):
                                    killed.append(splash)
                                    splash.deactivate(fake_death=True)
                                    particles.play_death(splash.position, splash.points)

        for enemy in [e for e in self.enemies if e.active]:
            if abs(player.position.x - enemy.x) < (enemy.scale.x + player.scale.x) * 0.45 and abs(player.position.z - enemy.z) < (enemy.scale.z + player.scale.z) * 0.55:
                enemy.deactivate()
                killed.append(enemy)
                particles.play_death(enemy.position, enemy.points)
                if len(ally_manager.allies) > 0:
                    ally_manager.remove_ally(len(ally_manager.allies) - 1)
                else:
                    player_died = True
                    break
            for idx, ally in enumerate(list(ally_manager.allies[:120])):
                if not enemy.active:
                    break
                if abs(ally.entity.x - enemy.x) < (enemy.scale.x + ally.entity.scale.x) * 0.45 and abs(ally.entity.z - enemy.z) < (enemy.scale.z + ally.entity.scale.z) * 0.45:
                    enemy.deactivate()
                    killed.append(enemy)
                    particles.play_death(enemy.position, enemy.points)
                    ally_manager.remove_ally(idx)
                    break

        for enemy in list(self.enemies):
            if not enemy.active or enemy.z < player.position.z - 18.0:
                if enemy in self.enemies:
                    self.enemies.remove(enemy)
        return killed, player_died

    def active_count(self) -> int:
        return sum(1 for enemy in self.enemies if enemy.active)

    def clear(self):
        for enemy in self.enemies:
            enemy.deactivate()
        self.enemies.clear()
        for shot in self.shots:
            shot.deactivate()
        self.next_spawn_z = 25.0
        self.spawn_interval = 18.0
        self.wave_count = 0

    def destroy(self):
        self.clear()
        for enemy in self.pool:
            destroy(enemy)
        for shot in self.shots:
            destroy(shot)
