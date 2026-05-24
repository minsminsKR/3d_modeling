from __future__ import annotations
from dataclasses import dataclass
import random
from ursina import Entity, Vec3, color, destroy, Text


@dataclass(frozen=True)
class GateSpec:
    gate_type: str
    value: int | str
    label: str
    rarity: int
    color_value: object
    dramatic: bool = False


GATE_SPECS = [
    GateSpec("add", 5, "+5", 14, color.rgba32(0, 210, 150, 135)),
    GateSpec("add", 10, "+10", 11, color.rgba32(0, 235, 170, 150)),
    GateSpec("add", 25, "+25", 4, color.rgba32(60, 255, 170, 180), True),
    GateSpec("mult", 2, "x2", 8, color.rgba32(20, 150, 255, 165), True),
    GateSpec("mult", 3, "x3", 3, color.rgba32(180, 60, 255, 195), True),
    GateSpec("fire_rate", "fire_rate", "FIRE RATE", 7, color.rgba32(255, 210, 40, 165)),
    GateSpec("damage", "damage", "DAMAGE", 7, color.rgba32(255, 90, 60, 165)),
    GateSpec("spread", "spread", "SPREAD", 6, color.rgba32(60, 255, 240, 165)),
    GateSpec("random_weapon", "random_weapon", "RANDOM GUN", 4, color.rgba32(255, 80, 230, 185), True),
]


class Gate(Entity):
    def __init__(self, x: float, z: float, spec: GateSpec, side: str):
        super().__init__(
            model="cube",
            color=spec.color_value,
            position=(x, 1.0, z),
            scale=(3.0, 2.2, 0.25),
            collider="box",
        )
        self.spec = spec
        self.side = side
        self.active = True
        self.phase = random.random() * 10.0
        self.glow = Entity(
            model="cube",
            parent=self,
            color=color.rgba32(255, 255, 255, 35),
            position=(0, 0, 0.04),
            scale=(1.12, 1.12, 0.22),
        )
        self.text_label = Text(
            text=spec.label,
            parent=self,
            position=(0, 0.04, -0.62),
            scale=11 if len(spec.label) > 4 else 15,
            color=color.white if not spec.dramatic else color.gold,
            origin=(0, 0),
        )
        self.sub_label = Text(
            text="POWER" if spec.gate_type not in ("add", "mult") else "ARMY",
            parent=self,
            position=(0, -0.22, -0.62),
            scale=6,
            color=color.rgba32(255, 255, 255, 180),
            origin=(0, 0),
        )

    def update_visual(self, dt: float):
        if not self.active:
            return
        self.phase += dt
        pulse = 1.0 + 0.06 * random.uniform(-1, 1)
        self.glow.scale = (1.12 * pulse, 1.12 * pulse, 0.22)
        self.text_label.y = 0.04 + 0.02 * random.uniform(-1, 1)

    def deactivate(self):
        self.active = False
        self.color = color.rgba32(100, 100, 100, 45)
        self.glow.enabled = False
        self.text_label.color = color.gray
        self.sub_label.color = color.gray

    def destroy_self(self):
        destroy(self.sub_label)
        destroy(self.text_label)
        destroy(self.glow)
        destroy(self)


class GatePair:
    def __init__(self, z: float, difficulty: float):
        self.z = z
        self.active = True
        left_spec, right_spec = self._roll_pair(difficulty)
        self.left_gate = Gate(-2.65, z, left_spec, "left")
        self.right_gate = Gate(2.65, z, right_spec, "right")

    def _roll_pair(self, difficulty: float) -> tuple[GateSpec, GateSpec]:
        pool = GATE_SPECS[:]
        weights = []
        for spec in pool:
            weight = spec.rarity
            if spec.gate_type in ("fire_rate", "damage", "spread", "random_weapon"):
                weight += int(difficulty * 2)
            if spec.label in ("+25", "x3"):
                weight += int(difficulty)
            weights.append(weight)
        left = random.choices(pool, weights=weights, k=1)[0]
        right = random.choices(pool, weights=weights, k=1)[0]
        tries = 0
        while right.label == left.label and tries < 5:
            right = random.choices(pool, weights=weights, k=1)[0]
            tries += 1
        return left, right

    def update_visual(self, dt: float):
        self.left_gate.update_visual(dt)
        self.right_gate.update_visual(dt)

    def check_collision(self, player_pos: Vec3) -> Gate | None:
        if not self.active or abs(player_pos.z - self.z) >= 1.0:
            return None
        if abs(player_pos.x - self.left_gate.x) < 1.55:
            self.deactivate()
            return self.left_gate
        if abs(player_pos.x - self.right_gate.x) < 1.55:
            self.deactivate()
            return self.right_gate
        return None

    def deactivate(self):
        self.active = False
        self.left_gate.deactivate()
        self.right_gate.deactivate()

    def destroy_self(self):
        self.left_gate.destroy_self()
        self.right_gate.destroy_self()


class GateManager:
    def __init__(self):
        self.gate_pairs: list[GatePair] = []
        self.next_spawn_z = 38.0
        self.spawn_interval = 42.0

    def update(self, player_pos: Vec3, difficulty: float, dt: float) -> GateSpec | None:
        if player_pos.z + 88.0 > self.next_spawn_z:
            self.gate_pairs.append(GatePair(self.next_spawn_z, difficulty))
            self.next_spawn_z += max(30.0, self.spawn_interval - min(10.0, difficulty * 0.8))

        triggered: GateSpec | None = None
        for gp in self.gate_pairs:
            gp.update_visual(dt)
            if gp.active:
                hit_gate = gp.check_collision(player_pos)
                if hit_gate:
                    triggered = hit_gate.spec

        for gp in list(self.gate_pairs):
            if gp.z < player_pos.z - 20.0:
                gp.destroy_self()
                self.gate_pairs.remove(gp)
        return triggered

    def clear(self):
        for gp in self.gate_pairs:
            gp.destroy_self()
        self.gate_pairs.clear()
        self.next_spawn_z = 38.0
