// C. elegans 커넥톰 임계값 적분-발화(threshold integrate-and-fire) 시뮬레이터.
// Busbice/GoPiGo 모델과 동일한 동작:
//   - 감각 자극(dendriteAccumulate) = 해당 감각뉴런의 출력 시냅스 가중치를 표적에 누적
//   - 스텝마다 |누적값| > threshold 인 뉴런이 발화 → 표적에 가중치 전파 후 자신은 0으로 리셋
//   - 근육 세포는 발화하지 않고, 좌/우 그룹 누적값이 운동 출력이 된다

let CONNECTOME_DATA = null;

export async function loadConnectomeData(url = "/data/connectome.json") {
  const res = await fetch(url);
  if (!res.ok) {
    throw new Error(`커넥톰 데이터를 불러오지 못했습니다: ${res.status}`);
  }
  CONNECTOME_DATA = await res.json();
  return CONNECTOME_DATA;
}

export function getConnectomeData() {
  return CONNECTOME_DATA;
}

export class Brain {
  // connections: { 세포이름: [[표적이름, 가중치], ...] } — 생략하면 원본 커넥톰 사용
  constructor(connections = null) {
    const data = CONNECTOME_DATA;
    this.threshold = data.threshold;
    this.cells = data.neurons.concat(data.muscles);
    this.neuronCount = data.neurons.length;
    this.n = this.cells.length;
    this.index = new Map(this.cells.map((name, i) => [name, i]));

    this.connections = connections ?? data.connections;
    this.targets = new Array(this.n).fill(null);
    this.weights = new Array(this.n).fill(null);
    for (const [pre, list] of Object.entries(this.connections)) {
      const pi = this.index.get(pre);
      if (pi === undefined) continue;
      const t = new Int32Array(list.length);
      const w = new Float32Array(list.length);
      for (let k = 0; k < list.length; k++) {
        t[k] = this.index.get(list[k][0]) ?? -1;
        w[k] = list[k][1];
      }
      this.targets[pi] = t;
      this.weights[pi] = w;
    }

    this.acc = new Float32Array(this.n); // 시냅스 누적 전위
    this.snap = new Float32Array(this.n); // 스텝 시작 시점 스냅샷
    this.fired = new Uint8Array(this.n); // 직전 스텝 발화 여부 (시각화용)
    this.stimulated = new Uint8Array(this.n); // 직전 스텝 감각 자극 여부 (시각화용)

    this.leftIdx = data.leftMuscles
      .map((m) => this.index.get(m))
      .filter((i) => i !== undefined);
    this.rightIdx = data.rightMuscles
      .map((m) => this.index.get(m))
      .filter((i) => i !== undefined);

    this.sensory = {};
    for (const [group, names] of Object.entries(data.sensory)) {
      this.sensory[group] = names
        .map((n) => this.index.get(n))
        .filter((i) => i !== undefined);
    }
  }

  // 감각뉴런 자극: 해당 뉴런의 출력 가중치를 표적 세포에 누적한다.
  stimulateGroup(groupName, scale = 1) {
    const indices = this.sensory[groupName];
    if (!indices) return;
    for (const i of indices) {
      this.propagate(i, scale);
      this.stimulated[i] = 1;
    }
  }

  propagate(i, scale) {
    const t = this.targets[i];
    if (!t) return;
    const w = this.weights[i];
    const acc = this.acc;
    for (let k = 0; k < t.length; k++) {
      if (t[k] >= 0) acc[t[k]] += w[k] * scale;
    }
  }

  // 한 스텝 실행. 좌/우 근육 누적값과 발화 수를 반환한다.
  step() {
    const { acc, snap, fired, threshold, neuronCount } = this;
    snap.set(acc);
    fired.fill(0);

    let firedCount = 0;
    for (let i = 0; i < neuronCount; i++) {
      if (Math.abs(snap[i]) > threshold) {
        this.propagate(i, 1);
        acc[i] = 0;
        fired[i] = 1;
        firedCount++;
      }
    }

    let left = 0;
    let right = 0;
    for (const i of this.leftIdx) left += acc[i];
    for (const i of this.rightIdx) right += acc[i];
    // 근육 누적값은 매 스텝 소모된다 (원본 motorcontrol과 동일)
    for (let i = neuronCount; i < this.n; i++) acc[i] = 0;

    return { left, right, firedCount };
  }

  clearStimulationFlags() {
    this.stimulated.fill(0);
  }
}

// 두 부모의 커넥톰을 교차(세포 단위로 무작위 선택)하고 약간 변이시킨 자손 커넥톰 생성.
// 변이: 각 시냅스가 mutationRate 확률로 가중치 ±1 (0은 되지 않게 유지)
export function breedConnections(connA, connB, mutationRate = 0.02) {
  const child = {};
  for (const pre of Object.keys(connA)) {
    const source = Math.random() < 0.5 ? connA : connB;
    const list = source[pre] ?? connA[pre];
    child[pre] = list.map(([post, w]) => {
      let weight = w;
      if (Math.random() < mutationRate) {
        weight += Math.random() < 0.5 ? -1 : 1;
        if (weight === 0) weight = w > 0 ? 1 : -1;
      }
      return [post, weight];
    });
  }
  return child;
}
