from __future__ import annotations
from dataclasses import dataclass
import random


@dataclass(frozen=True)
class UpgradeOption:
    key: str
    title: str
    description: str
    weight: int = 10


UPGRADE_POOL = [
    UpgradeOption("fire_rate", "Fire Rate +20%", "More bullets, more screen fill.", 12),
    UpgradeOption("damage", "Damage +30%", "Each hit lands harder.", 12),
    UpgradeOption("ally_spawn", "Ally Spawn +5", "Instant army growth.", 10),
    UpgradeOption("spread", "Spread Shot", "Adds wider firing lanes.", 8),
    UpgradeOption("double_projectile", "Double Projectile", "One extra projectile per shot.", 6),
    UpgradeOption("crit", "Crit Chance +10%", "Chance to deal burst damage.", 7),
    UpgradeOption("move_speed", "Move Speed +10%", "Cleaner gate choices.", 5),
    UpgradeOption("ally_cap", "Ally Cap +25", "Lets the army grow bigger.", 5),
]


class RunProgression:
    def __init__(self):
        self.level = 1
        self.exp = 0
        self.exp_to_next = 30
        self.coins = 0
        self.gems = 0
        self.combo = 0
        self.combo_timer = 0.0
        self.pending_upgrade = False

        self.fire_rate_mult = 1.0
        self.damage_mult = 1.0
        self.extra_projectiles = 0
        self.spread_bonus = 0.0
        self.crit_chance = 0.0
        self.move_speed_mult = 1.0
        self.ally_cap = 250

    def update(self, dt: float):
        if self.combo_timer > 0:
            self.combo_timer -= dt
            if self.combo_timer <= 0:
                self.combo = 0

    def add_kill_reward(self, points: int):
        self.combo += max(1, points)
        self.combo_timer = 2.2
        self.coins += points * (1 + min(9, self.combo // 10))
        if random.random() < min(0.20, 0.04 + points * 0.01):
            self.gems += 1
        self.add_exp(6 + points * 2)

    def add_exp(self, amount: int):
        self.exp += amount
        if self.exp >= self.exp_to_next:
            self.exp -= self.exp_to_next
            self.level += 1
            self.exp_to_next = int(self.exp_to_next * 1.28 + 12)
            self.pending_upgrade = True

    def roll_options(self, count: int = 3) -> list[UpgradeOption]:
        options = random.choices(UPGRADE_POOL, weights=[u.weight for u in UPGRADE_POOL], k=count * 3)
        unique: list[UpgradeOption] = []
        used = set()
        for option in options:
            if option.key in used:
                continue
            used.add(option.key)
            unique.append(option)
            if len(unique) >= count:
                break
        return unique or UPGRADE_POOL[:count]

    def apply_upgrade(self, key: str, ally_manager=None, player_pos=None, player=None, weapon_system=None):
        if key == "fire_rate":
            self.fire_rate_mult *= 0.80
        elif key == "damage":
            self.damage_mult *= 1.30
        elif key == "ally_spawn" and ally_manager and player_pos:
            ally_manager.add_allies(5, player_pos)
        elif key == "spread":
            self.spread_bonus += 1.5
        elif key == "double_projectile":
            self.extra_projectiles += 1
        elif key == "crit":
            self.crit_chance = min(0.60, self.crit_chance + 0.10)
        elif key == "move_speed":
            self.move_speed_mult *= 1.10
        elif key == "ally_cap":
            self.ally_cap += 25
        if weapon_system:
            weapon_system.set_run_modifiers(self)
        if player:
            player.move_speed_mult = self.move_speed_mult
        self.pending_upgrade = False

    @property
    def exp_ratio(self) -> float:
        return max(0.0, min(1.0, self.exp / max(1, self.exp_to_next)))
