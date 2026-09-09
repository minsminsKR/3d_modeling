import { soundManager } from "./SoundManager.js";

const VOICE_LINES = {
  start: "오늘은 하교하지 않습니다. 복도에서 기다리십시오.",
  hunt: "누군가 복도를 걷고 있습니다.",
  pa: "방송입니다. 하교하지 않습니다. 복도에서 기다리십시오.",
  deep: "방송입니다. 이 복도는 끝이 없습니다. 교실 번호를 믿지 마십시오.",
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
