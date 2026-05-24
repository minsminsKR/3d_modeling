export const UPGRADE_KEYS = Object.freeze({
  fireRate: "fireRate",
  damage: "damage",
  allies: "allies",
  spread: "spread",
  projectile: "projectile",
  crit: "crit",
});

export const BALANCE = Object.freeze({
  run: {
    initialExpNeed: 90,
    initialNextWaveZ: 12,
    initialNextGateZ: 42,
  },
  waves: {
    triggerLookahead: 54,
    firstSpawnDistance: 30,
    spawnDistance: 42,
    bossSpawnDistance: 50,
    firstInterval: 25,
    intervalFloor: 15,
    intervalDecayPerWave: 0.45,
    midBossEvery: 6,
    bigBossEvery: 12,
  },
  gates: {
    triggerLookahead: 76,
    interval: 38,
    ally_gate_interval: 30.0,
  },
  caps: {
    allyCap: 220,
    fireRateMin: 0.5,
    damageMax: 3.4,
    spreadMax: 5.2,
    extraProjectilesMax: 3,
    critMax: 0.45,
  },
});

const ENEMY_BASE = Object.freeze({
  basic: { hp: 34, speed: 2.65, points: 1, scale: 0.78, height: 1.2, radius: 0.62, sway: 0.16, color: 0xff9452 },
  fast: { hp: 14, speed: 5.25, points: 2, scale: 0.55, height: 0.8, radius: 0.45, sway: 0.78, color: 0xff5757 },
  tank: { hp: 74, speed: 1.62, points: 6, scale: 1.05, height: 1.65, radius: 0.88, sway: 0.04, color: 0x8c67ff },
  midBoss: { hp: 380, speed: 1.18, points: 18, scale: 1.5, height: 2.25, radius: 1.26, sway: 0.03, color: 0xffd45b },
  bigBoss: { hp: 760, speed: 0.92, points: 34, scale: 2.08, height: 3.12, radius: 1.68, sway: 0.02, color: 0xff5ee8 },
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

export function enemyStats(type, elapsed) {
  const base = ENEMY_BASE[type] || ENEMY_BASE.basic;
  const difficultyLevel = Math.floor(elapsed / 30);
  const hpMult = 1.0 + difficultyLevel * 0.18;
  const speedMult = 1.0 + difficultyLevel * 0.12;
  return {
    ...base,
    hp: Math.ceil(base.hp * hpMult),
    speed: base.speed * speedMult,
  };
}

export function buildWavePlan(elapsed, playerZ) {
  const difficultyLevel = Math.floor(elapsed / 30);
  const style = waveStyle(Math.floor(elapsed / 10) + 1);

  const minCount = 2 + difficultyLevel * 2;
  let maxCount = minCount + 1;
  if (difficultyLevel >= 3) {
    maxCount = minCount + 2;
  }
  let count = minCount + Math.floor(Math.random() * (maxCount - minCount + 1));
  count = Math.min(count, 12); // clamp to max_enemies_per_spawn = 12

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
  const styles = ["line", "cluster", "double_line", "pinch"];
  return styles[(wave - 1) % styles.length];
}

function wavePositions(style, count, spawnZ) {
  if (style === "line") {
    const spacing = 8.4 / Math.max(1, count - 1);
    return Array.from({ length: count }, (_, i) => ({
      x: -4.2 + i * spacing + (Math.random() - 0.5) * 0.9,
      z: spawnZ + (i % 2) * 0.9 + (Math.random() - 0.5) * 1.5,
    }));
  }
  if (style === "double_line") {
    return Array.from({ length: count }, (_, i) => ({
      x: -4.8 + Math.random() * 9.6,
      z: spawnZ + (i % 2) * 3.2 + (Math.random() - 0.5) * 1.8,
    }));
  }
  if (style === "pinch") {
    return Array.from({ length: count }, (_, i) => {
      const side = i % 3 === 2 ? -1.2 + Math.random() * 2.4 : (i % 2 === 0 ? -4.8 : 4.8);
      return {
        x: side + (Math.random() - 0.5) * 0.6,
        z: spawnZ + Math.random() * 4 + (Math.random() - 0.5) * 1.2,
      };
    });
  }
  return Array.from({ length: count }, () => ({
    x: -4.5 + Math.random() * 9,
    z: spawnZ - 2 + Math.random() * 5,
  }));
}

function pickEnemyType(difficultyLevel, index, tankCount) {
  const roll = Math.random();
  const tankLimit = difficultyLevel >= 5 ? 3 : difficultyLevel >= 3 ? 2 : 1;
  if (difficultyLevel >= 2 && tankCount < tankLimit && (index === 0 || roll < 0.09)) return "tank";
  if (difficultyLevel >= 1 && roll < 0.28) return "fast";
  return "basic";
}

function publicGateSpec(spec) {
  const { weight, minWave, ...publicSpec } = spec;
  return { ...publicSpec };
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}
