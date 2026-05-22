from __future__ import annotations
import random
from ursina import Entity, Vec3, color, destroy

from core.asset_loader import safe_entity
from core.config import ASSETS

class Enemy(Entity):
    def __init__(self):
        super().__init__(
            model="cube",
            enabled=False
        )
        self.enemy_type = "basic"
        self.health = 20
        self.max_health = 20
        self.speed = 2.0
        self.points = 1
        self.active = False
        self.radius = 0.8

    def spawn(self, pos: Vec3, enemy_type: str, wave_multiplier: float = 1.0):
        self.position = pos
        self.enemy_type = enemy_type
        self.active = True
        
        # Load asset based on type
        if enemy_type == "fast":
            self.max_health = int(12 * wave_multiplier)
            self.speed = 3.5
            self.points = 2
            self.scale = (0.6, 0.6, 0.6)
            self.color = color.rgb(255, 60, 60) # Fast red enemy
            asset = ASSETS.characters.get("Hwacat_angry")
        elif enemy_type == "tank":
            self.max_health = int(60 * wave_multiplier)
            self.speed = 0.8
            self.points = 5
            self.scale = (1.5, 1.5, 1.5)
            self.color = color.rgb(120, 50, 200) # Purple giant
            asset = ASSETS.characters.get("Cyclopse")
        else: # basic
            self.max_health = int(24 * wave_multiplier)
            self.speed = 1.8
            self.points = 1
            self.scale = (0.9, 0.9, 0.9)
            self.color = color.rgb(220, 130, 50) # Orange basic enemy
            asset = ASSETS.characters.get("Hwacat_angry")
            
        self.health = self.max_health
        self.y = self.scale.y * 0.5
        
        # Load model safely
        model = str(asset.model) if asset and asset.model else "cube"
        texture = str(asset.texture) if asset and asset.texture else None
        
        self.model = model
        if texture:
            self.texture = texture
            
        self.enabled = True

    def update(self):
        if not self.active:
            return
        # Move towards negative Z (towards the player)
        self.position += Vec3(0, 0, -1) * self.speed * 0.016

    def take_damage(self, amount: int) -> bool:
        """Subtract health and return True if dead."""
        self.health -= amount
        # Visual flash effect on hit
        self.blink_color()
        return self.health <= 0

    def blink_color(self):
        original_color = self.color
        self.color = color.white
        from ursina import invoke
        invoke(setattr, self, "color", original_color, delay=0.08)

    def deactivate(self):
        self.enabled = False
        self.active = False

class EnemyManager:
    def __init__(self):
        self.enemies: list[Enemy] = []
        self.pool: list[Enemy] = [Enemy() for _ in range(50)]
        self.next_spawn_z = 25.0
        self.spawn_interval = 20.0
        self.wave_count = 0

    def _get_enemy_from_pool(self) -> Enemy:
        for enemy in self.pool:
            if not enemy.active:
                return enemy
        new_enemy = Enemy()
        self.pool.append(new_enemy)
        return new_enemy

    def spawn_wave(self, player_z: float):
        self.wave_count += 1
        wave_multiplier = 1.0 + (self.wave_count * 0.15)
        
        # Decide wave shape (horizontal line, random cluster, or double line)
        wave_style = random.choice(["line", "cluster", "gate_blocker"])
        
        # Determine enemy types based on wave count
        types = ["basic"]
        if self.wave_count >= 3:
            types.append("fast")
        if self.wave_count >= 5:
            types.append("tank")
            
        spawn_z = player_z + 65.0
        
        if wave_style == "line":
            # Spawn a horizontal line of enemies
            count = random.randint(3, 7)
            spacing = 10.0 / max(1, count - 1)
            for i in range(count):
                x_pos = -5.0 + (i * spacing)
                enemy = self._get_enemy_from_pool()
                enemy_type = random.choice(types)
                enemy.spawn(Vec3(x_pos, 0, spawn_z), enemy_type, wave_multiplier)
                self.enemies.append(enemy)
                
        elif wave_style == "gate_blocker":
            # Spawn enemies right in front of where next gates might be
            for x_pos in [-2.5, 0.0, 2.5]:
                enemy = self._get_enemy_from_pool()
                enemy_type = random.choice(types)
                # Tank blockers are cool
                if enemy_type == "basic" and random.random() < 0.3:
                    enemy_type = "tank"
                enemy.spawn(Vec3(x_pos, 0, spawn_z), enemy_type, wave_multiplier)
                self.enemies.append(enemy)
                
        else: # cluster
            count = random.randint(4, 9)
            for _ in range(count):
                x_pos = random.uniform(-4.5, 4.5)
                z_pos = spawn_z + random.uniform(-3, 3)
                enemy = self._get_enemy_from_pool()
                enemy_type = random.choice(types)
                enemy.spawn(Vec3(x_pos, 0, z_pos), enemy_type, wave_multiplier)
                self.enemies.append(enemy)

    def update(self, player, bullet_pool, ally_manager, particles, audio, dt: float) -> tuple[int, bool]:
        """Updates enemies, checks bullet hits, player/ally hits. Returns (kills, player_died)."""
        kills = 0
        player_died = False
        
        # Trigger new waves ahead of player
        if player.position.z + 70.0 > self.next_spawn_z:
            self.spawn_wave(player.position.z)
            self.next_spawn_z += self.spawn_interval
            
        # Update enemies
        for enemy in self.enemies:
            if enemy.active:
                enemy.update()
                
        # Bullet collision check
        active_bullets = [b for b in bullet_pool.pool if b.active]
        active_enemies = [e for e in self.enemies if e.active]
        
        for bullet in active_bullets:
            for enemy in active_enemies:
                if not enemy.active or not bullet.active:
                    continue
                # Simple box check
                x_dist = abs(bullet.x - enemy.x)
                z_dist = abs(bullet.z - enemy.z)
                # Box boundaries based on enemy size/scale
                limit_x = enemy.scale.x * 0.7
                limit_z = enemy.scale.z * 0.8
                
                if x_dist < limit_x and z_dist < limit_z:
                    # Deactivate bullet
                    bullet.deactivate()
                    particles.play_hit(bullet.position)
                    
                    # Deal damage
                    if enemy.take_damage(bullet.damage):
                        enemy.deactivate()
                        kills += enemy.points
                        particles.play_death(enemy.position)
                        audio.play_death()
                        
        # Check enemy collision with Player and Allies
        player_pos = player.position
        active_enemies = [e for e in self.enemies if e.active]
        
        for enemy in active_enemies:
            # Distance from enemy to player
            px_dist = abs(player_pos.x - enemy.x)
            pz_dist = abs(player_pos.z - enemy.z)
            limit_x = (enemy.scale.x + player.scale.x) * 0.45
            limit_z = (enemy.scale.z + player.scale.z) * 0.55
            
            if px_dist < limit_x and pz_dist < limit_z:
                # Collision with user mob
                enemy.deactivate()
                particles.play_death(enemy.position)
                audio.play_death()
                
                if len(ally_manager.allies) > 0:
                    # Cancel out one ally (mob control style)
                    ally_manager.remove_ally(len(ally_manager.allies) - 1)
                else:
                    # No allies left -> Player dies!
                    player_died = True
                    break
                    
            # Check collision with individual allies as well (to prevent enemies bypassing them easily)
            for idx, ally in enumerate(list(ally_manager.allies)):
                if not enemy.active:
                    break
                ax_dist = abs(ally.entity.x - enemy.x)
                az_dist = abs(ally.entity.z - enemy.z)
                a_limit_x = (enemy.scale.x + ally.entity.scale.x) * 0.5
                a_limit_z = (enemy.scale.z + ally.entity.scale.z) * 0.5
                
                if ax_dist < a_limit_x and az_dist < a_limit_z:
                    # Ally is killed
                    enemy.deactivate()
                    particles.play_death(enemy.position)
                    audio.play_death()
                    ally_manager.remove_ally(idx)
                    break
                    
        # Cleanup far behind enemies
        for enemy in list(self.enemies):
            if enemy.z < player.position.z - 15.0:
                enemy.deactivate()
                self.enemies.remove(enemy)
                
        return kills, player_died

    def clear(self):
        for enemy in self.enemies:
            enemy.deactivate()
        self.enemies.clear()
        self.next_spawn_z = 25.0
        self.spawn_interval = 20.0
        self.wave_count = 0
