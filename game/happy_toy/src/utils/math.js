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
  const cx = center.x !== undefined ? center.x : (center[0] ?? 0);
  const cy = center.y !== undefined ? center.y : (center[1] ?? 0);
  const cz = center.z !== undefined ? center.z : (center[2] ?? 0);

  const sx = size.x !== undefined ? size.x : (size[0] ?? 0);
  const sy = size.y !== undefined ? size.y : (size[1] ?? 0);
  const sz = size.z !== undefined ? size.z : (size[2] ?? 0);

  return {
    minX: cx - sx / 2,
    maxX: cx + sx / 2,
    minY: cy - sy / 2,
    maxY: cy + sy / 2,
    minZ: cz - sz / 2,
    maxZ: cz + sz / 2,
  };
}
