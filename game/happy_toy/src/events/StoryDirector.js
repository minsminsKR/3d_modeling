import { soundManager } from "../audio/SoundManager.js";

// Height-based campaign spine. Stair chunks do not change cell ids when the
// player descends or climbs, so floor VO cannot rely on chunk enter alone.
export class StoryDirector {
  constructor(game) {
    this.game = game;
    this.reset();
  }

  reset() {
    this.fired = new Set();
  }

  fire(id, voiceKey, line, sfx) {
    if (this.fired.has(id) || !this.game?.isStarted || this.game.gameOver || this.game.gameCleared) {
      return false;
    }
    this.fired.add(id);
    if (!this.game._storyBeats) this.game._storyBeats = new Set();
    this.game._storyBeats.add(id);
    if (id === "b1floor") this.game._storyBeats.add("map:b1");
    if (id === "f2floor") this.game._storyBeats.add("map:f2");
    this.game._lastPaAt = this.game.playTime || 0;
    if (sfx) soundManager.playSFX(sfx);
    this.game.voiceAnnouncer?.announce(voiceKey, line);
    this.game.hud?.setStatus(line, 3800);
    return true;
  }

  update() {
    const game = this.game;
    if (!game?.isStarted || game.gameOver || game.gameCleared || game.isPaused) return;
    const player = game.player;
    if (!player) return;
    const { x, y, z } = player.position;
    const planar = Math.hypot(x, z);

    if (planar > 11) {
      this.fire(
        "leaveStart",
        "leaveStart",
        "손전등이 유일한 창입니다. 발소리가 나면 신발장에 숨으십시오.",
        "school_chime",
      );
    }

    if (y > -1.2 && y < 2.2 && Math.hypot(x - 16, z - 24.8) < 5.2) {
      this.fire(
        "stairB1",
        "stairB1",
        "지하 계단입니다. 물이 이름을 적고 있습니다.",
        "drip",
      );
    }

    if (y > -1.2 && y < 2.2 && Math.hypot(x + 16, z + 8.5) < 5.2) {
      this.fire(
        "stairF2",
        "stairF2",
        "2층 계단입니다. 액자 아래가 아직 젖어 있습니다.",
        "blood_drip",
      );
    }

    if (y < -2.2) {
      this.fire(
        "b1floor",
        "b1",
        "방송이 끊깁니다. 지하의 물이 이름을 적고 있습니다.",
        "drip",
      );
      if (Math.hypot(x + 3.5, z - 28.5) < 7) {
        this.fire(
          "nursery",
          "nursery",
          "요람이 비어 있지 않습니다. 눈을 마주치지 마십시오.",
          "drip",
        );
      }
      if (z > 42) {
        this.fire(
          "b1deep",
          "b1deep",
          "물이 복도를 두 번 꺾습니다. 막다른 벽장에 숨으십시오.",
          "drip",
        );
      }
    }

    if (y > 3.2) {
      this.fire(
        "f2floor",
        "f2",
        "2층입니다. 복도가 아직 마르지 않았습니다.",
        "blood_drip",
      );
      if (x < -27.4) {
        this.fire(
          "shrine",
          "shrine",
          "그림이 떨어지면 마지막 이름이 드러납니다.",
          "blood_drip",
        );
      }
      if (z < -37 || x < -40) {
        this.fire(
          "f2deep",
          "f2deep",
          "피 묻은 미로입니다. 손전등을 끄지 말고 벽장으로 꺾으십시오.",
          "blood_drip",
        );
      }
    }
  }
}
