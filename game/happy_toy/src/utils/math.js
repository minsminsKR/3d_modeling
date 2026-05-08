// 여러 모듈에서 공유하는 가벼운 수학 유틸리티입니다.
// 충돌, 시야 판정, 캐릭터 방향 전환처럼 엔진 공통 계산을 이곳에 둡니다.

import * as THREE from "three";

export function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}

export function vectorFromArray(values) {
  return new THREE.Vector3(values[0], values[1] ?? 0, values[2]);
}

export function distance2D(a, b) {
  const dx = a.x - b.x;
  const dz = a.z - b.z;
  return Math.hypot(dx, dz);
}

export function direction2D(from, to) {
  const direction = new THREE.Vector3(to.x - from.x, 0, to.z - from.z);
  const length = Math.hypot(direction.x, direction.z);
  if (length <= 0.0001) {
    return new THREE.Vector3(0, 0, 0);
  }
  return direction.divideScalar(length);
}

export function yawFromDirection(direction) {
  return Math.atan2(direction.x, direction.z);
}

export function smoothStep(current, target, speed, deltaTime) {
  const alpha = 1 - Math.exp(-speed * deltaTime);
  return current + (target - current) * alpha;
}

export function makeAabbFromCenter(center, size) {
  return {
    minX: center[0] - size[0] / 2,
    maxX: center[0] + size[0] / 2,
    minY: (center[1] ?? 0) - (size[1] ?? 0) / 2,
    maxY: (center[1] ?? 0) + (size[1] ?? 0) / 2,
    minZ: center[2] - size[2] / 2,
    maxZ: center[2] + size[2] / 2,
  };
}
