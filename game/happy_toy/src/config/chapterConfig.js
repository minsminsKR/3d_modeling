import { ENEMY_CONFIGS, MAP_CONFIG } from "./gameConfig.js";

export const CHAPTERS = [
  {
    id: 1,
    eyebrow: "SHADOW CORRIDOR",
    title: "그림자복도",
    description:
      "폐교의 여름. 출석부가 아직 열려 있습니다. 흩어진 네 개의 이름(혼)을 첫 홀의 제단함에 돌려놓기 전에는 하교할 수 없습니다. 본관과 별관, 물이 찬 지하, 피가 마른 2층. 손전등 밖은 복도의 몫입니다.",
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
