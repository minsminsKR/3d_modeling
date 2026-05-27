import { LIMITS } from "./config.js";

export const UPGRADE_KEYS = Object.freeze({
  fireRate: "fireRate",
  damage: "damage",
  allies: "allies",
  spread: "spread",
  projectile: "projectile",
  crit: "crit",
});

export const MID_BOSS_TYPES = Object.freeze([
  "giantSoldier",
  "heavyArmor",
  "shieldBoss",
  "speedBoss",
  "explosiveBoss",
]);

export const BOSS_TYPES = Object.freeze(["midBoss", "bigBoss", ...MID_BOSS_TYPES]);

export const BALANCE = Object.freeze({
  run: {
    initialExpNeed: 90,
    initialNextWaveZ: 12,
    initialNextGateZ: 42,
  },
  waves: {
    triggerLookahead: 54,
    firstSpawnDistance: 30,
    spawnDistance: 46,
    bossSpawnDistance: 50,
    firstInterval: 20,
    intervalFloor: 10,
    intervalDecayPerWave: 0.42,
    minEnemiesPerWave: 3,
    baseEnemyCount: 3,
    enemyCountPerDifficulty: 8,
    enemyCountVariance: 4,
    maxEnemiesPerWave: Math.min(180, Math.max(120, Math.floor(LIMITS.enemies * 0.25))),
    midBossEvery: 15.0,
    bigBossEvery: 90.0,
    midBossInterval: 15.0,
    bigBossInterval: 90.0,
    midBossTypes: MID_BOSS_TYPES,
  },
  gates: {
    triggerLookahead: 76,
    interval: 38,
    ally_gate_interval: 30.0,
  },
  caps: {
    allyCap: 280,
    fireRateMin: 0.5,
    damageMax: 3.4,
    spreadMax: 5.2,
    extraProjectilesMax: 3,
    critMax: 0.45,
  },
});

const ENEMY_BASE = Object.freeze({
  basic: { hp: 30, speed: 1.0, points: 1, scale: 0.72, height: 1.1, radius: 0.56, sway: 0.18, color: 0xff9452 },
  fast: { hp: 16, speed: 1.8, points: 2, scale: 0.52, height: 0.78, radius: 0.42, sway: 0.84, color: 0xff5757 },
  tank: { hp: 88, speed: 0.6, points: 6, scale: 1.05, height: 1.65, radius: 0.88, sway: 0.04, color: 0x8c67ff },
  midBoss: { hp: 440, speed: 0.68, points: 20, scale: 1.5, height: 2.25, radius: 1.24, sway: 0.03, color: 0xffd45b },
  giantSoldier: { hp: 440, speed: 0.68, points: 20, scale: 1.5, height: 2.25, radius: 1.24, sway: 0.03, color: 0xffc15c },
  heavyArmor: { hp: 640, speed: 0.52, points: 30, scale: 1.72, height: 2.58, radius: 1.42, sway: 0.02, color: 0x8f9cff },
  shieldBoss: { hp: 540, speed: 0.6, points: 26, scale: 1.62, height: 2.38, radius: 1.48, sway: 0.025, color: 0x44e0c0 },
  speedBoss: { hp: 330, speed: 1.2, points: 22, scale: 1.28, height: 1.92, radius: 1.04, sway: 0.28, color: 0xff536f },
  explosiveBoss: { hp: 470, speed: 0.72, points: 28, scale: 1.46, height: 2.18, radius: 1.22, sway: 0.09, color: 0xff8a32 },
  bigBoss: { hp: 900, speed: 0.52, points: 40, scale: 2.12, height: 3.18, radius: 1.74, sway: 0.02, color: 0xff5ee8 },
});

export const MID_BOSS_SPECS = Object.freeze({
  giantSoldier: ENEMY_BASE.giantSoldier,
  heavyArmor: ENEMY_BASE.heavyArmor,
  shieldBoss: ENEMY_BASE.shieldBoss,
  speedBoss: ENEMY_BASE.speedBoss,
  explosiveBoss: ENEMY_BASE.explosiveBoss,
});

const GATE_SPECS = Object.freeze([
  { key: "add", value: 2, label: "+2", color: 0x19e6a9, weight: 18 },
  { key: "add", value: 5, label: "+5", color: 0x1dffc1, weight: 14 },
  { key: "mult", value: 2, label: "x2", color: 0x3ea7ff, weight: 7, dramatic: true },
  { key: "mult", value: 3, label: "x3", color: 0xc05eff, weight: 4, dramatic: true },
]);

export function nextWaveInterval(wave) {
  return Math.max(
    BALANCE.waves.intervalFloor,
    BALANCE.waves.firstInterval - wave * BALANCE.waves.intervalDecayPerWave,
  );
}

export function enemyStats(type, elapsed, spawnCount = 0) {
  const safeElapsed = Number.isFinite(elapsed) ? elapsed : 0;
  const resolvedType = type === "midBoss" ? midBossTypeForElapsed(safeElapsed) : type;
  const base = ENEMY_BASE[resolvedType] || ENEMY_BASE.basic;
  const difficultyLevel = Math.floor(safeElapsed / 30);
  const isBoss = BOSS_TYPES.includes(resolvedType);
  
  let hp;
  if (resolvedType === "bigBoss") {
    const elapsedDiff = Math.max(0, safeElapsed - 90);
    const countFactor = Math.max(0, spawnCount - 1);
    hp = Math.floor(15000 + countFactor * 35000 + elapsedDiff * 1000);
  } else if (isBoss) {
    const elapsedDiff = Math.max(0, safeElapsed - 15);
    const countFactor = Math.max(0, spawnCount - 1);
    hp = Math.floor(500 + countFactor * 12000 + elapsedDiff * 400);
  } else {
    const hpMult = 1.0 + difficultyLevel * 0.16;
    hp = Math.ceil(base.hp * hpMult);
  }
  
  const speedMult = 1.0 + Math.min(difficultyLevel, 14) * (isBoss ? 0.045 : 0.075);
  return {
    ...base,
    hp,
    speed: base.speed * speedMult,
  };
}

export function buildWavePlan(elapsed, playerZ) {
  const difficultyLevel = Math.floor(elapsed / 30);
  const style = waveStyle(Math.floor(elapsed / 10) + 1);

  const lateRamp = Math.floor(difficultyLevel / 3) * 2;
  const minCount = BALANCE.waves.baseEnemyCount + difficultyLevel * BALANCE.waves.enemyCountPerDifficulty + lateRamp;
  const variance = BALANCE.waves.enemyCountVariance + Math.floor(difficultyLevel / 5);
  const maxCount = minCount + variance;
  let count = minCount + Math.floor(Math.random() * (maxCount - minCount + 1));
  count = clamp(count, BALANCE.waves.minEnemiesPerWave, BALANCE.waves.maxEnemiesPerWave);

  const spawnZ = playerZ + BALANCE.waves.spawnDistance;
  let tankCount = 0;
  return wavePositions(style, count, spawnZ).map((pos, index) => {
    const type = pickEnemyType(difficultyLevel, index, tankCount);
    if (type === "tank") tankCount += 1;
    return { ...pos, type };
  });
}

export function killExp(points) {
  if (points >= 34) return 85;
  if (points >= 18) return 46;
  if (points >= 6) return 11;
  if (points >= 2) return 5;
  return 3;
}

export function midBossTypeForElapsed(elapsed) {
  const safeElapsed = Number.isFinite(elapsed) ? elapsed : 0;
  const interval = Math.max(1, BALANCE.waves.midBossInterval);
  const slot = Math.floor(Math.max(0, safeElapsed) / interval) % MID_BOSS_TYPES.length;
  return MID_BOSS_TYPES[slot];
}

export function nextExpNeed(currentNeed, level) {
  // level is the level we are graduating to (e.g., if currently level 1, level is 2)
  return Math.floor(80 * Math.pow(1.32, level - 1));
}

export function weaponIndexForState({ currentIndex, kills, level, wave, elapsed }) {
  const score = kills * 0.7 + Math.max(0, level - 1) * 5 + Math.max(0, wave - 1) * 3 + elapsed * 0.08;
  const next = score >= 145 ? 7 : score >= 112 ? 6 : score >= 82 ? 5 : score >= 58 ? 4 : score >= 36 ? 3 : score >= 18 ? 2 : score >= 7 ? 1 : 0;
  return Math.max(currentIndex, next);
}

export function rollGateSpec({ wave = 0, excludeKey = "" } = {}) {
  const choices = GATE_SPECS.filter((spec) => spec.key !== excludeKey && wave >= (spec.minWave || 0));
  const total = choices.reduce((sum, spec) => sum + spec.weight, 0);
  let roll = Math.random() * total;
  for (const spec of choices) {
    roll -= spec.weight;
    if (roll <= 0) return publicGateSpec(spec);
  }
  return publicGateSpec(choices[0]);
}

export function clampRunStats(state) {
  state.allyCount = clamp(Math.floor(state.allyCount), 0, state.allyCap);
  state.fireRateMult = clamp(state.fireRateMult, BALANCE.caps.fireRateMin, 1.3);
  state.damageMult = clamp(state.damageMult, 1, BALANCE.caps.damageMax);
  state.spreadBonus = clamp(state.spreadBonus, 0, BALANCE.caps.spreadMax);
  state.extraProjectiles = clamp(Math.floor(state.extraProjectiles), 0, BALANCE.caps.extraProjectilesMax);
  state.critChance = clamp(state.critChance, 0, BALANCE.caps.critMax);
}

export function applyProgressionReward(state, key) {
  if (key === UPGRADE_KEYS.fireRate) state.fireRateMult *= 0.88;
  if (key === UPGRADE_KEYS.damage) state.damageMult *= 1.18;
  if (key === UPGRADE_KEYS.allies) state.allyCount += 5;
  if (key === UPGRADE_KEYS.spread) state.spreadBonus += 1.05;
  if (key === UPGRADE_KEYS.projectile) state.extraProjectiles += 1;
  if (key === UPGRADE_KEYS.crit) state.critChance += 0.08;
  clampRunStats(state);
}

export function applyGateReward(state, spec, weaponCount) {
  if (spec.key === "add") state.allyCount += spec.value;
  if (spec.key === "mult") state.allyCount = Math.max(state.allyCount + 1, Math.floor(state.allyCount * spec.value));
  if (spec.key === "fireRate") state.fireRateMult *= 0.92;
  if (spec.key === "damage") state.damageMult *= 1.14;
  if (spec.key === "spread") state.spreadBonus += 0.8;
  if (spec.key === "random") state.weaponIndex = Math.max(state.weaponIndex, 2 + Math.floor(Math.random() * Math.max(1, weaponCount - 2)));
  clampRunStats(state);
}

function waveStyle(wave) {
  const styles = ["line", "cluster", "double_line", "pinch", "surge"];
  return styles[(wave - 1) % styles.length];
}

function wavePositions(style, count, spawnZ) {
  if (style === "line") {
    const perRow = Math.min(10, Math.max(5, Math.ceil(Math.sqrt(count) * 1.55)));
    return Array.from({ length: count }, (_, i) => ({
      x: laneX(i % perRow, Math.min(perRow, count - Math.floor(i / perRow) * perRow), 10.2) + (Math.random() - 0.5) * 0.65,
      z: spawnZ + Math.floor(i / perRow) * 2.1 + (i % 2) * 0.55 + (Math.random() - 0.5) * 1.1,
    }));
  }
  if (style === "double_line") {
    return Array.from({ length: count }, (_, i) => ({
      x: -4.8 + Math.random() * 9.6,
      z: spawnZ + (i % 2) * 3.0 + Math.floor(i / 10) * 1.35 + (Math.random() - 0.5) * 1.4,
    }));
  }
  if (style === "pinch") {
    return Array.from({ length: count }, (_, i) => {
      const side = i % 3 === 2 ? -1.2 + Math.random() * 2.4 : (i % 2 === 0 ? -4.8 : 4.8);
      return {
        x: side + (Math.random() - 0.5) * 0.6,
        z: spawnZ + Math.floor(i / 5) * 1.05 + Math.random() * 3.6 + (Math.random() - 0.5) * 1.0,
      };
    });
  }
  if (style === "surge") {
    return Array.from({ length: count }, (_, i) => {
      const row = Math.floor(i / 8);
      const centerBias = (Math.random() - 0.5) * (i % 4 === 0 ? 4.0 : 8.8);
      return {
        x: clamp(centerBias + (Math.random() - 0.5) * 1.7, -5.2, 5.2),
        z: spawnZ - 1.5 + row * 1.45 + Math.random() * 5.6,
      };
    });
  }
  const depth = 5 + Math.floor(count / 8) * 1.25;
  return Array.from({ length: count }, () => ({
    x: -4.5 + Math.random() * 9,
    z: spawnZ - 2 + Math.random() * depth,
  }));
}

function pickEnemyType(difficultyLevel, index, tankCount) {
  const roll = Math.random();
  const tankLimit = difficultyLevel >= 9 ? 6 : difficultyLevel >= 6 ? 5 : difficultyLevel >= 3 ? 3 : 1;
  const tankChance = Math.min(0.18, 0.075 + difficultyLevel * 0.012);
  const fastChance = Math.min(0.46, 0.26 + difficultyLevel * 0.022);
  if (difficultyLevel >= 2 && tankCount < tankLimit && (index % 11 === 0 || roll < tankChance)) return "tank";
  if (difficultyLevel >= 1 && roll < fastChance) return "fast";
  return "basic";
}

function laneX(index, count, width) {
  const spacing = width / Math.max(1, count - 1);
  return -width / 2 + index * spacing;
}

function publicGateSpec(spec) {
  const { weight, minWave, ...publicSpec } = spec;
  return { ...publicSpec };
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}
