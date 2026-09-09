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

    if (y > -1.2 && y < 2.2 && x > 28 && x < 40 && z > 1.15 && z < 2.35) {
      this.fire(
        "windowHall",
        "windowHall",
        "창밖은 복도의 몫입니다. 운동장은 없습니다.",
        "school_chime",
      );
    }

    if (y > -1.2 && y < 2.2 && x > 42 && x < 54 && Math.abs(z) < 1.8) {
      this.fire(
        "skybridge",
        "skybridge",
        "본관과 별관을 잇는 유리복도입니다. 아래는 운동장이 아닙니다.",
        "school_chime",
      );
    }

    if (y > -1.2 && y < 2.2 && x > 74 && x < 86 && z > 10 && z < 22) {
      this.fire(
        "courtyard",
        "courtyard",
        "중정입니다. 난간 너머로 내려가지 마십시오.",
        "school_chime",
      );
    }

    if (y > -1.2 && y < 2.2 && x > 88 && x < 104 && Math.abs(z) < 1.8) {
      this.fire(
        "memorial",
        "memorial",
        "기념관입니다. 액자 속 얼굴이 비어 있습니다.",
        "school_chime",
      );
    }

    if (y > -1.2 && y < 2.2 && x > 104 && x < 120 && Math.abs(z) < 1.8) {
      this.fire(
        "trophy",
        "trophy",
        "트로피 복도입니다. 컵은 있는데 이름이 지워져 있습니다.",
        "school_chime",
      );
    }

    if (y > -1.2 && y < 2.2 && x > 56 && x < 72 && z > 8 && z < 24) {
      this.fire(
        "arcade",
        "arcade",
        "중정 아케이드입니다. 기둥 너머로 난간만 보입니다.",
        "school_chime",
      );
    }

    if (y > -1.2 && y < 2.2 && x > 56 && x < 72 && z > 24 && z < 40) {
      this.fire(
        "broadcast",
        "broadcast",
        "방송실입니다. 마이크가 아직 뜨겁습니다. 이름을 대지 마십시오.",
        "school_chime",
      );
    }

    if (y > -1.2 && y < 2.2 && x > 104 && x < 120 && z > 24 && z < 40) {
      this.fire(
        "practice",
        "practice",
        "연습실입니다. 가운데가 막혀 있습니다. 남쪽으로 돌아가십시오.",
        "school_chime",
      );
    }

    if (y > -1.2 && y < 2.2 && x > 120 && x < 136 && z < -8 && z > -24) {
      this.fire(
        "art",
        "art",
        "미술실입니다. 물감이 아직 마르지 않았습니다.",
        "school_chime",
      );
    }

    if (y > -1.2 && y < 2.2 && x > 120 && x < 136 && z < -24 && z > -40) {
      this.fire(
        "studio",
        "studio",
        "촬영실입니다. 조명이 꺼져 있습니다. 얼굴을 카메라에 대지 마십시오.",
        "school_chime",
      );
    }

    if (y > -1.2 && y < 2.2 && x > 104 && x < 120 && z > 12 && z < 20) {
      this.fire(
        "foyer",
        "foyer",
        "로비입니다. 표는 팔지 않습니다. 강당 문만 열려 있습니다.",
        "school_chime",
      );
    }

    if (y > -1.2 && y < 2.2 && x > 120 && x < 136 && z > 12 && z < 20) {
      this.fire(
        "auditorium",
        "auditorium",
        "강당입니다. 막이 내려와 있고 객석이 당신을 셉니다.",
        "school_chime",
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
      if (x > -18.6 && x < -14.2 && z > -22.4 && z < -12.4) {
        this.fire(
          "atrium",
          "atrium",
          "계단 우물입니다. 아래 복도가 당신을 올려다봅니다.",
          "blood_drip",
        );
      }
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
