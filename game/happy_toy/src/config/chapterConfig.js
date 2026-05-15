import { ENEMY_CONFIGS, MAP_CONFIG } from "./gameConfig.js";

export const CHAPTERS = [
  {
    id: 1,
    eyebrow: "Chapter 1",
    title: "낡은 여름 복도",
    description: "고정된 1층/2층 실내 복도를 지나 네 개의 열쇠를 찾습니다.",
  },
];

export function createChapterSession() {
  return {
    ...CHAPTERS[0],
    mapConfig: MAP_CONFIG,
    enemyConfigs: ENEMY_CONFIGS,
  };
}

export function getChapterById(chapterId) {
  return CHAPTERS.find((chapter) => chapter.id === Number(chapterId)) || CHAPTERS[0];
}
