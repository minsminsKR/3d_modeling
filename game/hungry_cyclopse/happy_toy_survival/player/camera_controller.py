from __future__ import annotations

import math

from ursina import Vec3, camera, clamp, held_keys, mouse, raycast


class ThirdPersonCamera:
    def __init__(self, target):
        self.target = target
        self.yaw = 0.0
        self.pitch = 18.0
        self.distance = 10.0
        self.height = 3.1
        self.sensitivity = 85.0
        camera.fov = 82

    @property
    def forward_flat(self) -> Vec3:
        forward = Vec3(camera.forward.x, 0, camera.forward.z)
        return forward.normalized() if forward.length() else Vec3(0, 0, 1)

    @property
    def right_flat(self) -> Vec3:
        right = Vec3(camera.right.x, 0, camera.right.z)
        return right.normalized() if right.length() else Vec3(1, 0, 0)

    def update(self, dt: float):
        if held_keys["right mouse"] or mouse.locked:
            self.yaw += mouse.velocity[0] * self.sensitivity * 80 * dt
            self.pitch -= mouse.velocity[1] * self.sensitivity * 80 * dt
            self.pitch = clamp(self.pitch, -12, 58)

        target_pos = self.target.position + Vec3(0, self.height, 0)
        radians = math.radians(self.yaw)
        direction = Vec3(math.sin(radians), 0, math.cos(radians))
        desired = target_pos - direction * self.distance + Vec3(0, self.pitch / 12.0, 0)
        hit = raycast(target_pos, (desired - target_pos).normalized(), distance=self.distance, ignore=[self.target])
        if hit.hit:
            desired = hit.world_point + hit.normal * 0.6
        camera.position = camera.position + (desired - camera.position) * min(1.0, dt * 10.0)
        camera.look_at(target_pos)
