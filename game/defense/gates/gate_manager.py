from __future__ import annotations
import random
from ursina import Entity, Vec3, color, destroy, Text

class Gate(Entity):
    def __init__(self, x: float, z: float, gate_type: str, value: int, side: str):
        # Determine color based on gate value / type
        col = color.rgba(0, 150, 255, 120) if gate_type == "mult" else color.rgba(0, 200, 150, 120)
        
        super().__init__(
            model="cube",
            color=col,
            position=(x, 1.0, z),
            scale=(3.0, 2.0, 0.25),
            collider="box"
        )
        self.gate_type = gate_type # "add" or "mult"
        self.value = value
        self.side = side # "left" or "right"
        self.active = True
        
        # Render text
        text_str = f"x{value}" if gate_type == "mult" else f"+{value}"
        self.text_label = Text(
            text=text_str,
            parent=self,
            position=(0, 0, -0.6),
            scale=15,
            color=color.white,
            origin=(0, 0)
        )

    def deactivate(self):
        self.active = False
        self.color = color.rgba(100, 100, 100, 50)
        if self.text_label:
            self.text_label.color = color.gray

    def destroy_self(self):
        if self.text_label:
            destroy(self.text_label)
        destroy(self)

class GatePair:
    def __init__(self, z: float):
        self.z = z
        # Generate random gate configurations
        left_type = random.choice(["add", "mult"])
        right_type = random.choice(["add", "mult"])
        
        if left_type == "mult":
            left_val = random.choice([2, 3])
        else:
            left_val = random.choice([5, 10, 15])
            
        if right_type == "mult":
            right_val = random.choice([2, 3])
        else:
            right_val = random.choice([5, 10, 15])
            
        # Spawn left and right gates
        self.left_gate = Gate(-2.5, z, left_type, left_val, "left")
        self.right_gate = Gate(2.5, z, right_type, right_val, "right")
        self.active = True

    def check_collision(self, player_pos: Vec3) -> Gate | None:
        if not self.active:
            return None
            
        # Check if player crossed Z threshold of the gates
        if abs(player_pos.z - self.z) < 1.0:
            # Check left gate
            if abs(player_pos.x - self.left_gate.x) < 1.5:
                self.deactivate()
                return self.left_gate
            # Check right gate
            elif abs(player_pos.x - self.right_gate.x) < 1.5:
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
        self.next_spawn_z = 40.0
        self.spawn_interval = 45.0

    def update(self, player_pos: Vec3, ally_manager, particles, audio) -> bool:
        """Updates gates, checks collision, spawns new gates, returns True if a gate was triggered."""
        # Spawn new gates ahead of player
        if player_pos.z + 80.0 > self.next_spawn_z:
            self.gate_pairs.append(GatePair(self.next_spawn_z))
            self.next_spawn_z += self.spawn_interval
            
        # Check collision with player
        triggered = False
        for gp in self.gate_pairs:
            if gp.active:
                hit_gate = gp.check_collision(player_pos)
                if hit_gate:
                    triggered = True
                    # Apply gate reward
                    if hit_gate.gate_type == "mult":
                        ally_manager.multiply_allies(hit_gate.value, player_pos)
                    else:
                        ally_manager.add_allies(hit_gate.value, player_pos)
                    # Play effects
                    particles.play_gate_crossing(hit_gate.position)
                    audio.play_gate()
                    
        # Cleanup past gates (far behind player)
        for gp in list(self.gate_pairs):
            if gp.z < player_pos.z - 20.0:
                gp.destroy_self()
                self.gate_pairs.remove(gp)
                
        return triggered

    def clear(self):
        for gp in self.gate_pairs:
            gp.destroy_self()
        self.gate_pairs.clear()
        self.next_spawn_z = 40.0
