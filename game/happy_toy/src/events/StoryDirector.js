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

    if (y > -1.2 && y < 2.2 && planar > 18) {
      this.fire(
        "f1maze",
        "f1maze",
        "복도가 갈라집니다. 한 길로 쫓기면 다른 길로, 아니면 신발장으로.",
        "school_chime",
      );
    }

    if (y > -1.2 && y < 2.2 && x > 16 && Math.abs(z) < 1.8) {
      this.fire(
        "f1ring",
        "f1ring",
        "복도가 끝없이 이어집니다. 안개 끝의 발소리를 신발장으로 빼십시오.",
        "school_chime",
      );
    }

    const cx = Math.round(x / 16);
    const cz = Math.round(z / 16);
    const lx = x - cx * 16;
    const lz = z - cz * 16;
    if (y > -1.2 && y < 2.2 && Math.abs(lx) > 6.35 && Math.abs(lz) > 5.35) {
      this.fire(
        "throughClass",
        "throughClass",
        "교실 뒷문이 옆 복도로 뚫려 있습니다. 쫓기면 책상 사이로.",
        "school_chime",
      );
    }

    if (y > -1.2 && y < 2.2 && x < -18 && Math.abs(z) < 1.8) {
      this.fire(
        "library",
        "library",
        "도서실입니다. 창이 판자로 막혀 있습니다. 책을 믿지 마십시오.",
        "school_chime",
      );
    }

    if (y > -1.2 && y < 2.2 && x > 26 && x < 40 && Math.abs(z) < 1.8) {
      this.fire(
        "washroom",
        "washroom",
        "화장실 문이 안쪽에서 잠겨 있습니다. 물을 틀지 마십시오.",
        "drip",
      );
    }

    if (y > -1.2 && y < 2.2 && z > 14 && Math.abs(x) < 1.8) {
      this.fire(
        "boarded",
        "boarded",
        "폐쇄된 교실입니다. 칠판의 출석을 읽지 마십시오.",
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
      if (x > 28) {
        this.fire(
          "b1east",
          "b1east",
          "동쪽 물이 이름을 삼킵니다. 꺾인 복도에서 숨으십시오.",
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
      if (z > -6) {
        this.fire(
          "f2south",
          "f2south",
          "남쪽 복도가 아직 피를 말리지 못했습니다. 꺾인 벽장으로.",
          "blood_drip",
        );
      }
    }
  }
}
