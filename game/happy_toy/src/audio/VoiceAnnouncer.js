import { soundManager } from "./SoundManager.js";

const VOICE_LINES = {
  start: "오늘은 하교하지 않습니다. 복도에서 기다리십시오.",
  hunt: "누군가 복도를 걷고 있습니다.",
  pa: "방송입니다. 하교하지 않습니다. 복도에서 기다리십시오.",
  f1b: "별관입니다. 같은 교실을 두 번 지나치지 마십시오.",
  b1: "방송이 끊깁니다. 지하의 물이 이름을 적고 있습니다.",
  f2: "2층입니다. 복도가 아직 마르지 않았습니다.",
  nurse: "보건실입니다. 장부에 끝나지 않은 출석이 남아 있습니다.",
  music: "음악실입니다. 한 음이 모자란 피아노가 열려 있습니다.",
  faculty: "교무실입니다. 지워진 네 이름을 찾으십시오.",
  science: "과학실입니다. 가스관이 아직 식지 않았습니다.",
  key: "이름을 찾았습니다.",
  key2: "이름이 둘입니다. 복도가 당신을 세기 시작합니다.",
  key3: "이름이 셋입니다. 마지막 혼은 아직 액자 뒤에 있습니다.",
  keysDone: "모든 이름을 모았습니다. 제단으로 돌아가십시오.",
  ritual: "제단이 문을 삼키려 합니다. 여섯을 세십시오.",
  ritualFail: "봉인이 끊겼습니다. 제단을 떠나지 마십시오.",
  death: "복도가 당신의 이름을 외웠습니다.",
  clear: "제단이 문을 삼켰습니다.",
  hide: "신발장 안으로. 호흡을 끊으십시오.",
  stairWait: "계단 입구에서 실내화가 멈추고 기다립니다.",
  leaveStart: "손전등이 유일한 창입니다. 발소리가 나면 신발장에 숨으십시오.",
  nursery: "요람이 비어 있지 않습니다. 눈을 마주치지 마십시오.",
  shrine: "그림이 떨어지면 마지막 이름이 드러납니다.",
  stairB1: "지하 계단입니다. 물이 이름을 적고 있습니다.",
  stairF2: "2층 계단입니다. 액자 아래가 아직 젖어 있습니다.",
  b1deep: "물이 복도를 두 번 꺾습니다. 막다른 벽장에 숨으십시오.",
  f2deep: "피 묻은 미로입니다. 손전등을 끄지 말고 벽장으로 꺾으십시오.",
};

export class VoiceAnnouncer {
  constructor() {
    this.lastSpokenAt = 0;
    this.lastKey = "";
    this.voicesReady = false;
    this.koVoice = null;
    if (typeof window !== "undefined" && window.speechSynthesis) {
      const refresh = () => {
        const voices = window.speechSynthesis.getVoices?.() || [];
        this.koVoice = voices.find((voice) => String(voice.lang || "").startsWith("ko")) || null;
        this.voicesReady = voices.length > 0;
      };
      refresh();
      window.speechSynthesis.addEventListener?.("voiceschanged", refresh);
    }
  }

  announce(key, text, options = {}) {
    const line = String(text || VOICE_LINES[key] || "").trim();
    if (!line) return false;
    const now = typeof performance !== "undefined" ? performance.now() : Date.now();
    if (key === this.lastKey && now - this.lastSpokenAt < 1800) return false;
    this.lastKey = key;
    this.lastSpokenAt = now;

    soundManager.playVoiceCadence?.(line);
    this.speak(line, options);
    return true;
  }

  speak(text, options = {}) {
    if (typeof window === "undefined" || !window.speechSynthesis) return false;
    try {
      window.speechSynthesis.cancel();
      const utterance = new SpeechSynthesisUtterance(text);
      utterance.lang = "ko-KR";
      utterance.rate = options.rate ?? 0.78;
      utterance.pitch = options.pitch ?? 0.62;
      utterance.volume = options.volume ?? 0.82;
      if (this.koVoice) utterance.voice = this.koVoice;
      window.speechSynthesis.speak(utterance);
      return true;
    } catch {
      return false;
    }
  }
}
