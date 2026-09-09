import * as THREE from "three";

const NOTES = [
  "급훈이 뒤집혀 있다. ‘뛰지 마세요’가 ‘뛰면 들립니다’로 고쳐져 있다.",
  "신발장 안에 이름표가 네 장. 당신의 칸만 비어 있다.",
  "방송: 오늘은 하교하지 않습니다. 복도에서 기다리십시오.",
  "교실 칠판에 같은 문장이 겹쳐 있다. 손전등을 끄면 더 잘 보인다.",
  "제단함은 처음 깨어난 홀에 있다. 이름을 모으기 전에는 열리지 않는다.",
  "발소리가 당신 것이 아니면, 벽장 안으로 들어가 호흡을 끊어라.",
  "바깥 별관은 본관과 번호만 다르다. 도서실과 화장실, 닫힌 교실이 섞여 있다.",
  "종이 한 음 모자란 채로 울리면, 복도가 당신의 이름을 외운다.",
  "손전등 밖은 복도의 몫이다. 꺼진 형광등은 장식이 아니다.",
  "교실 번호가 어제와 같다. 창밖의 운동장은 없다.",
  "지하 계단을 내려가면 물이 무릎까지 찬다. 이름을 적지 마라.",
  "2층 액자 아래는 아직 젖어 있다. 손전등을 벽에 대지 마라.",
  "별관 복도는 본관과 같다. 문이 다르면 길을 외워라.",
];

export class LoreNote {
  constructor(config) {
    this.id = config.id;
    this.label = config.label || "떨어진 종이";
    this.body = config.body || NOTES[Math.abs(hashString(config.id)) % NOTES.length];
    this.position = new THREE.Vector3(...config.position);
    this.yaw = config.yaw ?? 0;
    this.read = false;

    this.group = new THREE.Group();
    this.group.name = this.id;
    this.group.position.copy(this.position);
    this.group.rotation.y = this.yaw;

    const paperMap = new THREE.TextureLoader().load("/assets/textures/props/lore-note/basecolor.png");
    paperMap.colorSpace = THREE.SRGBColorSpace;
    const paper = new THREE.Mesh(
      new THREE.PlaneGeometry(0.28, 0.38),
      new THREE.MeshStandardMaterial({
        map: paperMap,
        color: 0xe8dcc4,
        emissive: 0x1a140c,
        emissiveIntensity: 0.08,
        roughness: 0.92,
        side: THREE.DoubleSide,
      }),
    );
    paper.position.z = 0.01;
    this.group.add(paper);

    const pin = new THREE.Mesh(
      new THREE.CircleGeometry(0.018, 8),
      new THREE.MeshStandardMaterial({ color: 0x6a1c16, roughness: 0.4 }),
    );
    pin.position.set(0, 0.12, 0.012);
    this.group.add(pin);
  }

  distanceTo(point) {
    return Math.hypot(this.position.x - point.x, this.position.y - point.y, this.position.z - point.z);
  }

  isInteractable() {
    return true;
  }

  getPrompt() {
    return this.read ? `E - ${this.label} 다시 읽기` : `E - ${this.label} 읽기`;
  }

  interact(context) {
    this.read = true;
    context.hud?.setStatus?.(this.body, 5200);
    context.game?.onLoreRead?.(this);
  }
}

function hashString(value) {
  let hash = 0;
  const text = String(value);
  for (let i = 0; i < text.length; i += 1) {
    hash = Math.imul(hash, 31) + text.charCodeAt(i);
  }
  return hash | 0;
}
