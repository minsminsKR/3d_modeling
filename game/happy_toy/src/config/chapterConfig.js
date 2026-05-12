import { ENEMY_CONFIGS, MAP_CONFIG } from "./gameConfig.js";
import { createChapterTwoMap } from "./chapterTwoGenerator.js";

export const CHAPTERS = [
  {
    id: 1,
    eyebrow: "Chapter 1",
    title: "낡은 여름 복도",
    description: "고정된 1층/2층 실내 복도를 지나 세 개의 열쇠를 찾습니다.",
    procedural: false,
  },
  {
    id: 2,
    eyebrow: "Chapter 2",
    title: "소음 복도",
    description: "Perlin noise seed로 매번 새로 이어지는 복도망을 탐색합니다.",
    procedural: true,
  },
];

export function createChapterSession(chapterId = 1, options = {}) {
  const normalizedId = Number(chapterId) === 2 ? 2 : 1;
  if (normalizedId === 2) {
    const seed = normalizeSeed(options.seed ?? Date.now());
    const mapConfig = createChapterTwoMap(seed);
    return {
      ...CHAPTERS[1],
      seed,
      mapConfig,
      enemyConfigs: createChapterTwoEnemies(mapConfig),
    };
  }

  return {
    ...CHAPTERS[0],
    seed: null,
    mapConfig: MAP_CONFIG,
    enemyConfigs: ENEMY_CONFIGS,
  };
}

export function getChapterById(chapterId) {
  return CHAPTERS.find((chapter) => chapter.id === Number(chapterId)) || CHAPTERS[0];
}

function createChapterTwoEnemies(mapConfig) {
  const patrolWaypoints = mapConfig.patrolWaypoints || [];
  const spawns = mapConfig.enemySpawns || [];
  return ENEMY_CONFIGS.map((config, index) => ({
    ...config,
    spawn: spawns[index] || config.spawn,
    waypoints: patrolWaypoints.length > 0 ? patrolWaypoints : config.waypoints,
    patrolWaypointsByFloor: null,
    allowInterFloorPatrol: false,
    detectionRange: config.detectionRange + 1.2,
    giveUpRange: config.giveUpRange + 5,
  }));
}

function normalizeSeed(seed) {
  const number = Number(seed);
  if (Number.isFinite(number) && number > 0) {
    return Math.floor(number) >>> 0;
  }
  return Date.now() >>> 0;
}
