# 그림자복도 패치노트 / 세션 인수인계

> 다른 세션·에이전트는 이 파일을 먼저 읽으십시오.
> 목표 골은 2026-09-10에 사용자가 중단했습니다. 완성형이 아닙니다.

- 브랜치: `cursor/ghost-mode-and-baby-horror-8707` (base `main`)
- PR: https://github.com/minsminsKR/3d_modeling/pull/3
- 기록 당시 HEAD: `b026401`
- 게임: `game/happy_toy` (Three.js + `python3 web_server.py`)
- 에셋: `game/assets` → `/assets/`
- 마지막 WASD: `verify-shadow-run` PASS, `verify-shadow-corridor` PASS (`b026401`)

원래 골: Happy Toy를 그림자복도(어둠 속 미로 추격, 숨기, 손전등, 탐색, 스토리, 음성, 일관형 포그) 수준의 완성형 1인칭 공포 게임으로 만든다.

---

## 지금 게임은 어디까지인가

플레이 가능한 폐교 1인칭입니다. 손전등 밖은 거의 보이지 않고, Uncat이 1층을 쫓고, 신발장에 숨고, 혼 4개를 모아 시작 홀 제단함으로 탈출합니다.

맵은 네 장입니다. 지하 1, 본관 1층, 별관 1층, 2층 1. 그래프는 `src/world/schoolMaze.js`.

1층 복도는 16m 타일 학교입니다. 추격/숨기 코어는 3.4m 십자 복도이고, 정체성 복도는 폭·한쪽 벽·외벽 헐·창 단차·교실 단차·L자 칸·중간 배플로 서로 다르게 보이게 만들었습니다. 그래도 타일 접합은 상자가 만나는 구조입니다. 사람 녹음 음성은 없고, 실 GUI 마우스 플레이는 이 환경에서 검증하지 못했습니다.

---

## 패치 이력 (테마순)

아래는 `main` 대비 이 브랜치에서 쌓인 방향입니다. 개별 커밋은 `git log origin/main..HEAD`를 보십시오.

### 1. Happy Toy → 학교 공포 루프

- 저택/엔들리스 미로를 네 장 학교 맵으로 교체
- 손전등 HUD, 포그, 클릭 시작, backtick 유령 모드
- Uncat을 검은 실루엣으로 교체. 본관·별관 1층만 추적
- 층별 헌터: 지하 Baby, 2층 Hwacat
- 신발장 숨기, 혼 4개, 제단 탈출
- 한국어 PA (`game/assets/voice/*.ogg`). 지금은 pitched gTTS
- B1 침수 / 2층 혈흔 / 갤러리 / 보일러 등 층 분위기

### 2. 1층 추격 복도

- 타일 중심을 지나는 3.4m 학교 복도
- 교실 문 뒤 책상·칠판, 신발장은 교실 안
- `(1,0)→(2,0)` 교실 컷스루 (동쪽 세면). 서쪽 문 −5.25 고정
- 창 복도, 연결복도(유리), 별관 운동장 우물, 강당·로비·아트리움
- 별관 특수방: 스튜디오, 방송, 암실, 대기실, 가정, 서도, 교무, 과학, 보건, 음악, 체육관

### 3. 정체성 복도 (복사 3.4m 탈출)

타일마다 폭(`getHallClear`)과 한쪽 벽(`getHallSides`)을 다르게 둠.

- 공용 락커/PA/시계/문유리/책상 그리드를 닫힌 정체성 복도에서 제거
- 닫힌 교실 코너를 진짜 방으로 막고 L자 칸막이
- 옥상 `(0,2)`는 N+W L자 복도 (플러스 상자 아님)
- 무대 `(6,2)` 북쪽 교실 볼륨 폐쇄
- 중간 배플·볼·도그레그 (세탁, 표본, 인형, 별관 남볼, 기념관, 보육, 서고, 무대, 실험)
- 닫힌 면만 외벽 헐을 당김. 열리는 면은 반드시 7.8
- 닫힌 창 벽 2단 단차 (`split`)
- 닫힌 교실 장내벽은 문 밴드 유지, 중간만 단차 (`bands`)
- 열리는 T면은 문↔T 남는 반쪽만 단차. 검증된 pinch/offset along은 `getHallSides`에 고정

### 4. 검증·캡처

- `verification/verify-shadow-run.mjs` : WASD 보행 + 메시/수치 assert
- `verification/verify-shadow-corridor.mjs` : 추격·숨기·층 헌트·사망
- 캡처 스크립트는 아티팩트를 **덮어쓰지 않음**. 새 파일명만 추가

---

## 절대 회귀 금지

월드 +Z가 남쪽입니다. 플레이어 반지름 0.34, walkSpeed 3.15.
포그 `fogNear: 1.45`, `fogFar: 8.8` (B1 8.4, 별관 8.8, 2층 8.2).

### 추격 / 컷스루

다음 타일은 3.4m (`clear = 1.7`)이고 장내벽을 단차내지 않습니다.

- 추격: `(0,0)` `(1,0)` `(0,1)` `(0,-1)`
- 동쪽 세면: `(2,0)` — `(1,0)→(2,0)` 컷스루용. 헐 네 면 7.8
- 세면 서쪽 문 along **−5.25**
- 세면 창 along **`[-5.35, -3.15, 3.15, 5.35]`**
- `(0,0)` `(1,0)` 추격 골방을 가두지 말 것

### 헐 / 열리는 면

- 열리는 면 외벽은 **반드시 7.8**. `getHallHull`이 `getOpenings` 뒤에 다시 잠급니다. 이 잠금을 빼지 말 것
- 플러스 타일(네 방향 열림) 헐을 당기지 말 것: 별관 `(4,0)`, 트로피 `(7,0)`, 세면사거리 `(5,0)`, 기념관 `(6,0)`, 추격 타일
- 보육/창고: 동쪽은 창. **서쪽만 닫을 것.** 동쪽을 조그하지 말 것
- 트로피 SE를 L조그하지 말 것 (넓은 보행 `(114.8, 2.85)`)
- 연습실 북쪽을 단차내지 말 것 (배플이 `sides.n`에 붙음)
- 창 벽이 **그래프 오프닝**이면 2단 `split`을 넣지 말 것 (T 채움)
- 교실 문 밴드를 움직이지 말 것. 문 along은 `getHallSides`에 남을 것

### `getHallClear` (T줄기는 이 값, 그래프 중심)

| 타일 | clear |
| --- | --- |
| 추격 4칸, 세면 `(2,0)` | 1.7 |
| 별관 `(4,0)` / 세탁 `(4,-1)` | 1.22 |
| 트로피 `(7,0)` | 2.18 |
| 세면사거리 `(5,0)` | 1.42 |
| 석고 `(-1,0)` / 표본 `(6,-1)` | 1.48 |
| 보육 `(2,1)` | 1.28 |
| 창고 `(2,-1)` | 1.38 |
| 인형 `(-2,1)` | 1.52 |
| 서고 `(-2,-1)` | 1.18 |
| 실험 `(5,-2)` | 1.32 |
| 다실 `(-1,1)` | 1.45 |
| 아케이드 `(4,1)` | 1.58 |
| 무대 `(6,2)` | 1.92 |
| 그 외 | 1.7 |

EW 복도는 n/s만 어긋나게 하고 e/w는 clear. NS 복도는 e/w만 어긋나게 하고 n/s는 clear. 플러스(계단)만 네 면을 어긋나게 함. 수치는 `getHallSides` authored 테이블을 그대로 쓰십시오.

### 검증된 WASD (성공 반경 0.9, 실패는 dist < 2.2면 ok)

짧은 목표를 가까이 두면 거짓 통과합니다. 벽 증명은 **벽을 지나 조준**한 뒤 막힌 정지를 assert하십시오. 충돌은 벽을 타고 T 틈으로 미끄러집니다. offset 보행은 T 가지 밖에서 시작하십시오.

| 이름 | 경로 / 기대 |
| --- | --- |
| 별관 북쪽 offset | `(68.2, 0) → (68.2, −2.5)` 정지 z≈−1.18. along x=4.2는 `sides.n` 유지 |
| 별관 남 pinch | `(70.0, 0) → (70.0, 2.5)` 정지 z≈0.71, `ok: false` |
| 기념관 남 | `(99.4, 0) → (99.4, 3.2)` 정지 z≈1.78 — **동남**. 서남은 볼 가능 |
| 계단 남 | `(12.5, 16) → (12.5, 19.4)` 정지 z≈17.68 — **서남** |
| 보육 서 | `(32, 19.4) → (29.2, 19.4)` 정지 x≈30.79. along z=3.4는 `sides.w` 유지 |
| 보육 동 pinch | `(32,16) → (34.8,16)` — z≈0에서 동 clear 1.18 |
| 트로피 넓음 | `(114.8, 2.85)` 정지 z≈1.84. along x=2.8은 `sides.s` 유지 |
| 컷스루 | `(16,0) → (21.15,−2.15) → (22.7,−6.45) → (25.3,−6.45) → (26.85,−2.1) → (26.75,0) → (32,0)` |
| 옥상 L | `(0,22)→(0,28)→(0,32)→(−6,32)` — 열림은 N+W |
| 세면 NE | `(38.3, 0) → (38.3, −4.2)` |
| 무대 | `(90,32)→(96,32)→(96,26)` — 북쪽 T는 열림 |
| 세탁 척추 | `(64,−16)→(64,−22)` |
| 세탁 NW방 | `(64,−22.3)→(59.6,−22.3)` |
| 표본 NW방 | `(96,−22.4)→(91.6,−22.4)` — hull.w=7.40 |
| 아케이드 | `(64,10)→(64,16)→(70,16)→(64,16)→(64,22)` |
| 다실 | `(−10,16)→(−16,16)→(−16,22)` |
| 서고 | `(−32,−10)→(−32,−16)→(−32,−22)` |
| 연결복도 척추 | `(48, 0)` 통과 유지 |

첫 문 웨이포인트는 2.2m 틈 − r=0.34 안 (`|along − door| ≲ 0.76`).

`testSafeMode=true`는 noclip입니다. 보행 검증은 `ghostMode=true`, `testSafeMode=false`.

---

## 앞으로 구현할 것 (우선순위)

골은 멈췄지만, 다시 열면 이 순서가 맞습니다. 쉬운 호환 패치로 범위를 줄이지 마십시오.

1. **고유 메시 학교**
   - 플러스 타일(추격 제외)도 16m 상자가 아니라 작성된 접합이어야 함. 추격 3.4m와 세면 컷스루는 유지
   - 교실 **문** 장내벽은 아직 한 평면인 곳이 많음. 문 밴드는 고정, 문 없는 중간/남는 반쪽만 단차
   - 특수방(상담, 방송창고, 예절, 대기실, 교무, 과학, 암실, 로비 등)은 복도 장식이 아니라 방 타입. `getHallWallSteps`가 안 먹음. 방 메시를 직접 갈아엎을 것
   - 진짜 그림자복도급이려면 타일 키트가 아니라 복도 단위 오서링이 필요함

2. **사람 음성**
   - 현재 pitched gTTS. 사람 PA로 교체하거나, 교체 전엔 사람이라고 쓰지 말 것
   - `nurseryhall.ogg` 대사는 `보육 복도입니다. 요람이 서쪽 교실에 있습니다.`

3. **실 플레이 검증**
   - Playwright WASD는 통과함. 마우스 룩 + 손전등 + 숨기 + 추격은 사람이 브라우저에서 확인해야 함
   - `computerUse`는 이 환경에서 할당량/모델 문제로 실패했음. 대체는 Playwright 또는 사용자 URL
   - 실행: `cd game/happy_toy && python3 web_server.py --host 0.0.0.0 --port 8010`

4. **스토리/탐색 밀도**
   - 방마다 읽을 거리, 잘못된 길, 음성 비트, 층 전환이 그림자복도처럼 한 루프로 붙어야 함
   - 새 맵/테마/에셋/모델 교체는 허용. 추격 제약을 깨지 말 것

완료 판정은 “코드가 그럴듯함”이 아니라 위 요구를 항목별로 현재 상태로 증명한 뒤에만 하십시오.

---

## 작업 규칙 (다음 에이전트)

- 브랜치는 `cursor/ghost-mode-and-baby-horror-8707`에 이어서 작업. `main`에 직접 올리지 말 것
- 테스트 전에 커밋·푸시·PR 갱신. 테스트 후 추가 수정이 있으면 다시 커밋/푸시/PR
- PR은 `ManagePullRequest`. `gh`는 읽기 전용
- JS는 `Cache-Control: no-store`
- Chrome: `CHROME_PATH=/usr/local/bin/google-chrome`. 런칭 사이 2초
- Playwright는 `game/happy_toy/node_modules`. 커밋하지 말 것
- `/opt/cursor/artifacts/`는 불변. 같은 파일명으로 다시 찍지 말 것. 캡처 스크립트는 기존 경로가 있으면 throw
- 기존 캡처를 재실행하지 말 것: `capture-f1-offset-shells.mjs`, `capture-f1-l-jogs.mjs`, `capture-f1-closed-l-jogs.mjs`, `capture-f1-roofhall-l.mjs`, `capture-f1-more-l-jogs.mjs`, `capture-f1-identity-baffles.mjs`, `capture-f1-closed-hulls.mjs`, `capture-f1-window-steps.mjs`, `capture-f1-class-steps.mjs`, `capture-f1-class-jogs.mjs`
- gTTS: `PYTHONPATH=/tmp/gtts-pkg` (글로벌 pip 금지). ffmpeg `asetrate=24000*0.84,aresample=24000,atempo=1.1` → vorbis 24kHz mono
- 포트 8010 서버가 켜져 있으면 죽이지 말 것
- `web_server.py`는 표준 라이브러리만. 가상환경 없이 실행 가능

---

## 핵심 파일

| 역할 | 경로 |
| --- | --- |
| 복도 폭/헐/단차/교실 장식 | `src/world/BackroomsGenerator.js` |
| 문판·오브다 스킵 | `src/world/MapBuilder.js` |
| 그래프 | `src/world/schoolMaze.js` |
| 스토리 | `src/events/StoryDirector.js` |
| 청크 입장 VO | `src/core/Game.js` `onEnterSchoolChunk` |
| 음성 | `src/audio/VoiceAnnouncer.js` |
| 서버 | `web_server.py` |
| WASD 검증 | `verification/verify-shadow-run.mjs` |
| 추격 루프 검증 | `verification/verify-shadow-corridor.mjs` |
| 스토리/음성/그래프 | `verification/verify-story-director.mjs`, `verify-voice-assets.mjs`, `verify-school-maze.mjs` |

좌표 헬퍼:

- `isClosedIdentityHall`, `isEastWashChunk`, `isPracticeChunk`, `getHallWindowSide`, `getHallNookMask`
- `getHallDoorAlong` — 추격은 ±5.25. 정체성 복도는 타일마다 다름
- `getHallFaceClear(cx, cz, side, along)` — 단차 포함 실제 장내벽 깊이
- `getHallWallSteps` — 창은 `{split,w,e}` 또는 `{split,n,s}`. 교실은 `{bands:[{from,to,clear}]}`
- 열리는 면에 `split`을 넣으면 필터가 버림. `bands`만 열리는 면에 허용 (T에서 떨어진 반쪽)

---

## 닫힌 방 L조그 (이미 들어감)

annex NE/NW, trophy SW (**SE 아님**), laundry NW/SW, specimen NW/SW, doll NW/SW, nursery SW/NW (**동 아님**), storage SW/NW (**동 아님**), arcade SW/NW, eastwash NE, washfour NE, angel NE, lablink SW (**SE 아님**), stage NE, tea NW/NE, archive NW/SW.

무대 `(6,2)`는 창이 남쪽이라 북쪽 방을 닫음 (`stagewing_room_nw/ne`).

---

## 실행

```bash
cd game/happy_toy
python3 web_server.py --host 127.0.0.1 --port 8010
```

브라우저: `http://127.0.0.1:8010`

```bash
export CHROME_PATH=/usr/local/bin/google-chrome
node verification/verify-shadow-run.mjs http://127.0.0.1:8010/
node verification/verify-shadow-corridor.mjs http://127.0.0.1:8010/
```
