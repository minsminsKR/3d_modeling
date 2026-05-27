export const ASSETS = {
  player: {
    label: "Player Uncat",
    modelUrl: "/assets/characters/Uncat/mixamo/Run.fbx",
    textureUrl: "/assets/characters/Uncat/source/model_textured.jpg",
    height: 1.95,
  },
  cyclopse: {
    label: "Cyclopse",
    modelUrl: "/assets/characters/Cyclopse/mixamo/Run.fbx",
    textureUrl: "/assets/characters/Cyclopse/source/model_textured.jpg",
    height: 1.9,
  },
  hwacat: {
    label: "Hwacat",
    modelUrl: "/assets/characters/Hwacat/mixamo/Normal_standing.fbx",
    textureUrl: "/assets/characters/Hwacat/source/model_textured.jpg",
    height: 2.65,
  },
  hwacatAngry: {
    label: "Hwacat Angry",
    modelUrl: "/assets/characters/Hwacat_angry/mixamo/Zombie Run.fbx",
    textureUrl: "/assets/characters/Hwacat_angry/source/model_textured.jpg",
    height: 3.85,
  },
};

export const LIMITS = {
  allies: 360,
  bullets: 840,
  muzzleFlashes: 420,
  enemies: 680,
  pickups: 180,
  particles: 360,
  modelDecorators: 90,
};

export const LANES = {
  minX: -6,
  maxX: 6,
  width: 12,
};

export const RUNNER = {
  playerSpeed: 8.8,
  roadSegmentLength: 36,
  roadAheadDistance: 250,
  roadRecycleBehind: 52,
  gateHitWidth: 2.15,
  gateHitDepth: 1.85,
};

export const WEAPONS = [
  {
    name: "Pistol",
    fireRate: 0.32,
    damage: 12,
    speed: 33,
    spread: 0,
    count: 1,
    color: 0xffff77,
    effect: { level: 1, bulletScale: 0.32, bulletLength: 0.35, trailWidth: 0.08, trailLength: 0.15, flashScale: 0.12, flashTtl: 0.035, particles: 0, hitParticles: 0, hitSize: 0.1 },
  },
  {
    name: "Dual Pistol",
    fireRate: 0.25,
    damage: 12,
    speed: 35,
    spread: 1.4,
    count: 2,
    color: 0x7effff,
    effect: { level: 2, bulletScale: 0.36, bulletLength: 0.38, trailWidth: 0.1, trailLength: 0.18, flashScale: 0.14, flashTtl: 0.04, particles: 0, hitParticles: 1, hitSize: 0.12, muzzlePerProjectile: true },
  },
  {
    name: "SMG",
    fireRate: 0.088,
    damage: 8,
    speed: 40,
    spread: 4.0,
    count: 1,
    color: 0x9cff72,
    effect: { level: 3, bulletScale: 0.28, bulletLength: 0.32, trailWidth: 0.06, trailLength: 0.2, flashScale: 0.12, flashTtl: 0.03, particles: 0, hitParticles: 0, hitSize: 0.1 },
  },
  {
    name: "Rifle",
    fireRate: 0.15,
    damage: 26,
    speed: 47,
    spread: 0.8,
    count: 1,
    color: 0xffbb55,
    effect: { level: 4, bulletScale: 0.45, bulletLength: 0.5, trailWidth: 0.15, trailLength: 0.35, flashScale: 0.2, flashTtl: 0.05, particles: 1, hitParticles: 2, hitSize: 0.16 },
  },
  {
    name: "Shotgun",
    fireRate: 0.41,
    damage: 13,
    speed: 34,
    spread: 11.5,
    count: 6,
    color: 0xff7f42,
    effect: { level: 5, bulletScale: 0.38, bulletLength: 0.35, trailWidth: 0.12, trailLength: 0.18, flashScale: 0.32, flashTtl: 0.05, particles: 1, hitParticles: 1, hitSize: 0.14, wideFlash: true },
  },
  {
    name: "Laser",
    fireRate: 0.064,
    damage: 10,
    speed: 60,
    spread: 0.4,
    count: 2,
    color: 0x7fffff,
    effect: { level: 7, bulletScale: 0.26, bulletLength: 1.0, trailWidth: 0.22, trailLength: 1.0, flashScale: 0.28, flashTtl: 0.045, particles: 1, hitParticles: 2, hitSize: 0.18, muzzlePerProjectile: true },
  },
  {
    name: "Minigun",
    fireRate: 0.039,
    damage: 10,
    speed: 55,
    spread: 6.2,
    count: 1,
    color: 0xff6574,
    effect: { level: 6, bulletScale: 0.32, bulletLength: 0.4, trailWidth: 0.1, trailLength: 0.3, flashScale: 0.18, flashTtl: 0.035, particles: 0, hitParticles: 1, hitSize: 0.12 },
  },
  {
    name: "Rocket",
    fireRate: 0.54,
    damage: 80,
    speed: 30,
    spread: 1.0,
    count: 1,
    color: 0xff78ff,
    splash: 2.4,
    effect: { level: 8, bulletScale: 0.9, bulletLength: 0.75, trailWidth: 0.35, trailLength: 0.6, flashScale: 0.5, flashTtl: 0.08, particles: 3, hitParticles: 5, hitSize: 0.26, smoke: true },
  },
];

export const UPGRADES = [
  { key: "fireRate", title: "Fire Rate +20%", body: "More bullets, faster ramp." },
  { key: "damage", title: "Damage +30%", body: "Enemies melt sooner." },
  { key: "allies", title: "Ally Spawn +5", body: "Instant crowd surge." },
  { key: "spread", title: "Spread Shot", body: "Wider bullet wall." },
  { key: "projectile", title: "Double Projectile", body: "Extra projectile per shot." },
  { key: "crit", title: "Crit Chance +10%", body: "Spike damage moments." },
];
