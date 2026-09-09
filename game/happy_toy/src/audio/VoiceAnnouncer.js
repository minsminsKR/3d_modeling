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
  f1maze: "복도가 갈라집니다. 한 길로 쫓기면 다른 길로, 아니면 신발장으로.",
  f1ring: "복도가 끝없이 이어집니다. 안개 끝의 발소리를 신발장으로 빼십시오.",
  library: "도서실입니다. 창이 판자로 막혀 있습니다. 책을 믿지 마십시오.",
  washroom: "화장실 문이 안쪽에서 잠겨 있습니다. 물을 틀지 마십시오.",
  boarded: "폐쇄된 교실입니다. 칠판의 출석을 읽지 마십시오.",
  windowHall: "창밖은 복도의 몫입니다. 운동장은 없습니다.",
  throughClass: "교실 뒷문이 옆 복도로 뚫려 있습니다. 쫓기면 책상 사이로.",
  gym: "체육관입니다. 줄은 남아 있는데 운동장은 없습니다.",
  skybridge: "본관과 별관을 잇는 유리복도입니다. 아래는 운동장이 아닙니다.",
  courtyard: "중정입니다. 난간 너머로 내려가지 마십시오.",
  memorial: "기념관입니다. 액자 속 얼굴이 비어 있습니다.",
  auditorium: "강당입니다. 막이 내려와 있고 객석이 당신을 셉니다.",
  foyer: "로비입니다. 표는 팔지 않습니다. 강당 문만 열려 있습니다.",
  atrium: "계단 우물입니다. 아래 복도가 당신을 올려다봅니다.",
  trophy: "트로피 복도입니다. 컵은 있는데 이름이 지워져 있습니다.",
  arcade: "중정 아케이드입니다. 기둥 너머로 난간만 보입니다.",
  art: "미술실입니다. 물감이 아직 마르지 않았습니다.",
  practice: "연습실입니다. 가운데가 막혀 있습니다. 남쪽으로 돌아가십시오.",
  studio: "촬영실입니다. 조명이 꺼져 있습니다. 얼굴을 카메라에 대지 마십시오.",
  broadcast: "방송실입니다. 마이크가 아직 뜨겁습니다. 이름을 대지 마십시오.",
  darkroom: "암실입니다. 빨간 불만 남아 있습니다. 물을 흔들지 마십시오.",
  greenroom: "대기실입니다. 의상이 이름을 입고 있습니다.",
  homeec: "가정실입니다. 재봉틀이 혼자 돌아가고 있습니다.",
  club: "서도부입니다. 먹물이 아직 마르지 않았습니다.",
  specimen: "표본 복도입니다. 유리 너머를 세지 마십시오.",
  stagewing: "무대 복도입니다. 의상이 이름을 걸고 있습니다.",
  laundry: "세탁 복도입니다. 수레가 어제 출석을 실었습니다.",
  lablink: "실험 복도입니다. 가스가 아직 식지 않았습니다.",
  av: "시청각실입니다. 화면이 꺼져 있어도 교실을 보고 있습니다.",
  supply: "비품실입니다. 철창 안에 어제 출석이 잠겨 있습니다.",
  counsel: "상담실입니다. 제단이 이름을 듣고 있습니다.",
  staticset: "방송 창고입니다. 화면이 아직 당신을 셉니다.",
  stairhall: "지하로 가는 복도입니다. 콘 너머로 물이 마릅니다.",
  nurseryhall: "보육 복도입니다. 요람이 서쪽 벽에 붙어 있습니다.",
  dollhall: "인형 복도입니다. 선반의 눈을 마주치지 마십시오.",
  archivehall: "서고 앞 복도입니다. 철 상자를 열지 마십시오.",
  storagehall: "창고 앞 복도입니다. 상자가 어제 출석을 담고 있습니다.",
  teahall: "다실 앞 복도입니다. 신발을 신지 마십시오.",
  dorm: "생활관입니다. 요람은 비어 있습니다.",
  dollclass: "인형 교실입니다. 눈이 마주치면 따라가십시오.",
  prepstore: "준비물 창고입니다. 선반 뒤에 도자기 열쇠가 있습니다.",
  closedlib: "폐관 도서실입니다. 대출 장부를 읽지 마십시오.",
  etiquette: "예절실입니다. 신발을 신지 마십시오.",
  roofhall: "옥상 복도입니다. 문이 판자로 막혀 있습니다.",
  lostfound: "분실물함입니다. 어제 신발을 찾지 마십시오.",
  nursery: "요람이 비어 있지 않습니다. 눈을 마주치지 마십시오.",
  shrine: "그림이 떨어지면 마지막 이름이 드러납니다.",
  stairB1: "지하 계단입니다. 물이 이름을 적고 있습니다.",
  stairF2: "2층 계단입니다. 액자 아래가 아직 젖어 있습니다.",
  b1deep: "물이 복도를 두 번 꺾습니다. 막다른 벽장에 숨으십시오.",
  f2deep: "피 묻은 미로입니다. 손전등을 끄지 말고 벽장으로 꺾으십시오.",
  b1east: "동쪽 물이 이름을 삼킵니다. 꺾인 복도에서 숨으십시오.",
  f2south: "남쪽 복도가 아직 피를 말리지 못했습니다. 꺾인 벽장으로.",
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

    const playedClip = soundManager.playVoiceLine?.(key);
    if (!playedClip) {
      soundManager.playVoiceCadence?.(line);
      this.speak(line, options);
    }
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
