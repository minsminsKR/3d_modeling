# Happy Toy Project Map

이 문서는 `game/happy_toy` 전체 구조를 총괄합니다. 나중에 게임이 커졌을 때는 먼저 이 파일을 보고 어떤 모듈을 수정할지 정하면 됩니다.

## 실행 파일

| 파일 | 역할 | 주로 수정할 때 |
| --- | --- | --- |
| `index.html` | 게임 화면, HUD, 시작/포획/클리어/일시정지 오버레이의 DOM 뼈대 | 화면 요소를 추가할 때 |
| `styles.css` | 전체 화면 캔버스, HUD, 시작/일시정지 화면, 공포 연출 스타일 | UI 분위기와 레이아웃을 바꿀 때 |
| `web_server.py` | 로컬 정적 서버. FBX/JS/CSS/JPG MIME 타입 처리 | 포트, 호스트, 정적 제공 방식을 바꿀 때 |
| `vendor/three/` | Three.js와 FBXLoader 로컬 사본 | Three.js 버전을 고정하거나 오프라인 실행성을 관리할 때 |
| `verification/verify-gameplay.mjs` | Playwright 기반 자동 검증. 초기화, 벽/바닥/천장/계단/문/캐비넷 텍스처 적용, 미닫이문 패널 이동, 열리는 문-방 연결, 잠긴 문, 계단 waypoint 기반 층간 추격, stuck 방지, 2층 탐색 공간, 층별 walkable/blocked/void/drop zone 검증, 등 뒤 걷기/달리기 감지, 층간 포획 방지, 캐비넷 탈출 시점, 순찰 복귀, 추격 포기, Cyclopse 지면 보정, 일시정지 메뉴, 열쇠/클리어, 캐비넷 생존/사망 흐름 확인 | 큰 기능을 바꾼 뒤 브라우저 동작을 재검증할 때 |
| `verification/capture-stair-views.mjs` | Playwright 기반 계단 시점 캡처. 2층 진입 정면 벽, 1층 계단 옆 틈, 2층에서 뒤돌아본 계단실, 2층 복도 진입 시야를 후레쉬 켠 상태로 저장 | 계단실 구조/시야를 고친 뒤 스크린샷으로 재확인할 때 |

## 엔트리와 코어

| 파일 | 역할 | 주로 수정할 때 |
| --- | --- | --- |
| `src/main.js` | 브라우저 진입점. `Game` 생성, 초기화, 초기화 실패 표시 담당 | 앱 시작 절차를 바꿀 때 |
| `src/core/Game.js` | 렌더러, 기본 검수용 밝기/안개/조명, 장면, 맵, 플레이어, 적, HUD, 루프를 조립하는 최상위 모듈. 열쇠 수, 클리어 상태, 일시정지, 캐비넷 이벤트도 여기서 총괄 | 큰 시스템을 추가하거나 연결할 때 |
| `src/core/Loop.js` | `requestAnimationFrame` 기반 업데이트 루프 | 일시정지, 고정 timestep, 디버그 속도 조절을 넣을 때 |
| `src/core/Input.js` | 키보드/마우스 입력 수집, pointer lock 처리. 한글 입력 상태에서도 물리키 WASD/E/F/Esc를 잡도록 alias 처리 | 조작키, 게임패드, 마우스 감도 입력 방식을 바꿀 때 |
| `src/events/HorrorEventManager.js` | 매미 복도 챕터용 조명 깜빡임, 위치 기반 scripted scare, 문 닫힘 이벤트 | 반복 복도, 환각, 조명/문 이벤트를 추가할 때 |

## 설정

| 파일 | 역할 | 주로 수정할 때 |
| --- | --- | --- |
| `src/config/gameConfig.js` | 플레이어 속도, 층별 walkable/room/blocked/void/drop/transition waypoint, 맵 벽/문/열쇠/캐비넷/최종 장치/소품, 적 캐릭터 에셋과 AI 수치 | 맵, 캐릭터, 순찰 경로, 감지 범위, 추격 포기 거리, 캐비넷 확률을 바꿀 때 |

새 캐릭터를 추가할 때는 `ENEMY_CONFIGS`에 항목을 하나 추가하고, 아래 에셋 폴더에 FBX와 텍스처를 넣습니다.

```text
assets/characters/<CharacterName>/
  mixamo/
    Walking.fbx
    Run.fbx
  source/
    model_textured.jpg
```

벽, 바닥, 문 같은 환경 스킨은 아래 폴더에 넣습니다. PNG 한 장으로도 적용할 수 있고, 반복되는 벽/바닥 재질은 tileable 이미지가 가장 좋습니다.

```text
assets/textures/
  walls/wall.png
  floors/floor.png
  ceilings/ceiling.png
  stairs/stair.png
  doors/doors.png
  cabinets/cabinet.png
  props/
```

## 월드와 충돌

| 파일 | 역할 | 주로 수정할 때 |
| --- | --- | --- |
| `src/world/MapBuilder.js` | 1층/2층 바닥, 계단, 천장, 벽, 문, 열쇠, 캐비넷, 최종 장치, 장난감/촛불/벽등/낮은 난간/얼룩/천/배관/바리케이드/창살 창/전선/매미 허물/마네킹 오브제 생성, debug overlay 생성 | 맵 구조와 방 배치, 수집품/은신처, 공포 분위기 오브젝트를 바꿀 때 |
| `src/world/TextureLibrary.js` | `assets/textures`의 벽/바닥/천장/계단/문/캐비넷 PNG를 Three.js material로 변환하고 기본 fallback material 제공 | 환경 스킨 파일명, 반복 방식, 재질 밝기/거칠기를 바꿀 때 |
| `src/world/Door.js` | 두 패널로 갈라지는 미닫이문과 locked/blocked door의 렌더링/상태/충돌/상호작용, connectedRoomId debug 정보 | 잠긴 문, 문 레일/손잡이, 소리, 자동문을 만들 때 |
| `src/world/KeyItem.js` | 방 곳곳의 수집 가능한 열쇠. 렌더링, 프롬프트, 수집/리셋 상태 담당 | 열쇠 외형, 수집 조건, 아이템 종류를 바꿀 때 |
| `src/world/Cabinet.js` | 숨을 수 있는 캐비넷. 텍스처 body 외형, 내부 시점, 몬스터 대기 위치, 충돌 크기 제공 | 은신처 시점, 캐비넷 스킨, 크기/위치, 대기 위치를 바꿀 때 |
| `src/world/FinalExit.js` | 마지막 방의 장난감 상자. 열쇠 3개 전달 시 클리어를 요청 | 엔딩 조건, 최종 장치 외형, 보상 연출을 바꿀 때 |
| `src/world/CollisionWorld.js` | 층별 walkable/room/blocked/void area, landing/drop/stair transition zone, stair entry/exit waypoint graph, 벽/문 충돌체 관리, 플레이어/적 이동 보정, 시야 차단 판정, 층간 A* 경로 탐색 | 층 구조, 계단, 충돌 방식, 장애물, 시야/경로 판정을 개선할 때 |

현재 충돌은 AABB 기반이지만 `floorAreas`, `roomAreas`, `blockedAreas`, `voidAreas`, `landingAreas`, `dropZones`, `ramps`, `transitionWaypoints`를 분리해 층별 이동을 검증합니다. 플레이어와 적은 이동 후 `CollisionWorld.resolveActorPosition()`을 거치며, 같은 X/Z에 아래층 바닥이 있어도 명시된 `dropZone`이나 계단 `transitionZone`이 아니면 Y만 낮춰 이동하지 않습니다. invalid landing을 시도하면 콘솔에 경고를 남기고 이동을 취소합니다. 계단 시각 메시는 `MapBuilder.createStairways()`가 복도 바닥에서 이어지는 짧은 랜딩, 촘촘한 계단 블록/라이저, 양쪽 벽, 벽부착 손잡이로 만듭니다. 2층 계단참은 `upper-stair-north-wall`, `upper-stair-west-lower-shaft-wall`, `upper-stair-east-lower-shaft-wall`, `upper-stair-south-back-wall`, `stairwell-upper-ceiling`, `upper-landing-left-return-wall`, `upper-landing-east-wall`, `upper-corridor-south-end`가 실제 벽/천장으로 닫아 계단을 올랐을 때나 뒤돌아봤을 때 큰 빈 void가 보이지 않게 합니다. 플레이어 카메라는 `PlayerController.updateCamera()`가 `verticalCameraSmoothness`로 Y 이동을 부드럽게 보간합니다. 2층 바닥은 `slabThickness`가 있는 박스 지오메트리라 아래층에서는 천장/슬래브처럼 보입니다. 계단 끝에는 `second-stair-top-panel` 상부 랜딩 슬래브를 둬 2층 복도 바닥과 자연스럽게 이어집니다. 복잡한 3층 이상 구조물이 늘어나면 `transitionWaypoints`의 링크를 추가해 층간 그래프를 확장합니다.

## 플레이어

| 파일 | 역할 | 주로 수정할 때 |
| --- | --- | --- |
| `src/player/PlayerController.js` | 1인칭 WASD 이동, 마우스 시점, 런타임 마우스 감도, E 상호작용, 캐비넷 내부 시점/이동 잠금 | 앉기, 달리기, 체력, 손전등 배터리, 아이템 사용을 넣을 때 |
| `src/player/FlashlightController.js` | F키 후레쉬 on/off 상태 관리 | 배터리, 깜빡임, 고장, 아이템식 후레쉬를 넣을 때 |

상호작용키는 요청대로 `E`로 고정되어 있습니다. 문, 열쇠, 캐비넷, 최종 장치는 모두 `PlayerController.setInteractables()`에 등록된 공통 상호작용 대상입니다.

## 적과 캐릭터

| 파일 | 역할 | 주로 수정할 때 |
| --- | --- | --- |
| `src/entities/EnemyManager.js` | 여러 적 생성, 업데이트, 위협도/포획 상태 총괄. 캐비넷 이벤트에 들어갈 대표 추격 몬스터 선택 | 적 종류를 늘리거나 스폰 규칙을 바꿀 때 |
| `src/entities/Enemy.js` | 적 하나의 복도 위주 순찰/감지/경로 기반 추적/추격 포기/캐비넷 앞 대기 상태 머신 | AI 행동, 추격 로직, 공격, 수색 패턴을 바꿀 때 |
| `src/loaders/CharacterLoader.js` | Mixamo FBX 로드, `model_textured.jpg` 적용, 크기 정규화, 지정 액션의 root 수직 이동 잠금 | 다른 포맷, 여러 모션, 텍스처 방식을 붙일 때 |

현재 적은 `Walking.fbx`의 첫 애니메이션을 기본 순찰 루프로 재생합니다. 추격과 캐비넷 조사 상태에서는 `gameConfig.js`의 `animationUrls.chase`가 가리키는 각 캐릭터의 `Run.fbx`를 재생합니다. 캐릭터 텍스처는 `model_test`와 같은 방식으로 조명에 묻히지 않는 `MeshBasicMaterial`에 적용합니다. Mixamo 보행/달리기 FBX의 `Hips.position` root motion drift는 루프 리셋 때 모델이 튀지 않도록 `CharacterLoader`에서 제거하고, 실제 월드 이동은 `Enemy`의 그룹 이동이 담당합니다. 캐릭터가 하늘에 뜨지 않도록 `Enemy.snapModelToGround()`가 매 프레임 모델 바운딩박스를 바닥에 맞춥니다. Cyclopse의 Run 모션은 `lockRootVerticalActions: ["chase"]`로 수직 root/hips 이동을 잠그고, `visualGroundSink: 0.55`로 시각상 발이 바닥에 닿게 전용 접지 보정을 추가합니다.

추격 중 플레이어와의 거리가 `ENEMY_CONFIGS[].giveUpRange`보다 멀어지면 해당 몬스터는 추격을 포기하고 순찰로 돌아갑니다. 층이 다를 때는 계단 우회 거리를 감안해 `interFloorGiveUpRange` 또는 기본 `giveUpRange * 1.8`을 사용합니다. 플레이어가 벽이나 닫힌 문 뒤에 있으면 `Enemy.getChaseTarget()`이 `CollisionWorld.findPath()`로 문 구멍과 `stair-entry-1f`/`stair-exit-2f` waypoint를 통과하는 경로를 잡고, 문 근처에 도착하면 `openDoorOnPath()`가 잠기지 않은 문만 엽니다. 평상시 배회도 `ENEMY_CONFIGS[].waypoints`의 전역 루트를 사용하며 `allowInterFloorPatrol=true`이면 방, 복도, 계단, 2층 목적지까지 pathfinding을 허용합니다. 계단이나 문 앞에서 실제 이동량이 거의 없으면 `Enemy.updateStuckState()`가 `tryUnstuck()`을 호출하고, 순찰 중에는 순간이동 대신 다음 waypoint로 넘겨 배회를 이어갑니다. 순찰 지점에서는 짧게 멈춰 `lookAroundTurnSpeed` 속도로 주변을 훑고, 캐비넷 이벤트가 끝나면 가까운 전역 순찰 지점으로 재진입합니다. 캐비넷 조사 중에는 접근할 때와 문 앞에서 대기할 때 모두 `chase` 액션, 즉 Run 모션을 유지합니다.

감지는 층과 방향을 같이 봅니다. `floorAwarenessHeight`보다 Y 차이가 크면 같은 XZ에 있어도 감지/포획/위협도를 만들지 않습니다. 몬스터가 등을 보인 상태에서는 걷는 플레이어를 시야로 잡지 못하고, `Shift`로 달리는 플레이어만 `hearingRange` 안에서 소리로 감지합니다. 정면에서는 `frontAwarenessDot` 기준의 시야각과 `CollisionWorld.hasLineOfSight()`를 통과해야 추격합니다.

플레이어가 캐비넷에 들어간 상태에서는 감지와 포획 판정을 막고, `Game`이 선택한 대표 추격 몬스터 한 마리만 캐비넷 앞 대기 판정을 수행합니다. 몬스터가 캐비넷 앞 대기 위치에 도착하면 `Enemy.playIdlePose()`가 보행/점프 루프를 멈추고 정지 포즈를 유지합니다. 플레이어가 캐비넷에서 나오면 `Cabinet.getExitPosition()`과 `PlayerController.exitCabinet()`이 캐비넷 바깥 정면 방향으로 위치와 yaw를 맞춥니다.

## UI

| 파일 | 역할 | 주로 수정할 때 |
| --- | --- | --- |
| `src/ui/Hud.js` | 상호작용 문구, 상태 메시지, 위협 화면 효과, 시작/포획/클리어/일시정지 화면과 마우스 감도 표시 관리 | 체력 UI, 인벤토리, 목표 표시, 자막을 넣을 때 |

게임 로직은 DOM을 직접 만지지 않고 `Hud` 메서드를 호출하도록 분리했습니다.

브라우저 콘솔에서는 개발/검증용으로 `window.__happyToy`를 통해 현재 `Game` 인스턴스에 접근할 수 있습니다. 문 상호작용, 플레이어 위치, 적 상태, `horrorEventManager`를 빠르게 확인할 때 사용합니다. URL에 `?debug=1`을 붙이면 화면 우상단 debug HUD와 walkable/blocked/void/drop zone overlay가 켜집니다. debug HUD는 현재 floor, X/Z, tile type, 아래층 같은 X/Z의 landing 가능 여부, 최근 drop 시도 target, walkable/room/blocked/void/stair/door 카운트, 가장 가까운 문과 connected room, stair waypoint 링크, 몬스터별 floor/state/path target/stuck timer를 표시합니다.

## 유틸리티

| 파일 | 역할 | 주로 수정할 때 |
| --- | --- | --- |
| `src/utils/math.js` | 거리, 방향, 보간, AABB 계산 | 공통 수학 함수가 필요할 때 |

## 현재 게임 흐름

```text
index.html
-> src/main.js
-> Game.init()
-> MapBuilder가 맵/문/열쇠/캐비넷/최종 장치/충돌 생성
-> PlayerController가 1인칭 이동과 공통 E키 상호작용 준비
-> EnemyManager가 Uncat/Cyclopse FBX 로드
-> Loop가 매 프레임 Esc 일시정지, 문, 열쇠, 최종 장치, 캐비넷 이벤트, 플레이어, 적, 렌더링 업데이트
```

## 현재 게임 목표

1. 낡은 여름 복도 분위기의 1층 방과 복도 안 계단으로 올라가는 2층 인형방/거울방/기록 alcove를 탐색하며 열쇠 3개를 수집합니다.
2. Uncat과 Cyclopse가 추격하면 거리를 크게 벌리면 추격이 풀립니다.
3. 캐비넷에 숨으면 추격 중이던 대표 몬스터가 캐비넷 앞에 섭니다.
4. 캐비넷 판정은 한 번만 굴립니다. `CABINET_CONFIG.deathChance`가 기본 20%이며, 사망이면 문이 열리고 포획 화면이 뜹니다. 생존이면 몬스터가 5초 동안 앞에 있다가 순찰로 돌아갑니다.
5. 마지막 방의 장난감 상자에 열쇠 3개를 건네면 클리어 화면이 뜹니다.

## 일시정지 흐름

`Esc` 또는 pointer lock 해제로 `Game.pause()`가 호출되면 `isPaused`가 켜지고 게임 월드 업데이트가 멈춥니다. 메뉴 중앙에는 `계속하기`, `다시하기`, `종료`, 마우스 감도 슬라이더가 있습니다. 감도 슬라이더는 `PlayerController.setMouseSensitivity()`로 런타임 감도만 바꾸며, `종료`는 브라우저 탭을 강제로 닫지 않고 `Game.quitToTitle()`로 타이틀 화면에 돌아갑니다.

## 다음 확장 후보

| 목표 | 수정 시작점 |
| --- | --- |
| 새 방/복도/층 추가 | `src/config/gameConfig.js`의 `MAP_CONFIG.floorAreas`, `roomAreas`, `blockedAreas`, `voidAreas`, `landingAreas`, `dropZones`, `transitionWaypoints`, `ramps`, `floorPanels`, `stairways`, `walls`, `doors`, `props` |
| 환경 스킨 교체 | `assets/textures`, `src/world/TextureLibrary.js` |
| 새 열쇠/은신처 추가 | `src/config/gameConfig.js`의 `MAP_CONFIG.keys`, `cabinets` |
| 새 캐릭터 추가 | `assets/characters`, `src/config/gameConfig.js`의 `ENEMY_CONFIGS` |
| 문 잠금/열쇠 | `src/world/Door.js`, `src/world/KeyItem.js`, `src/core/Game.js`, `src/ui/Hud.js` |
| 적이 문과 계단을 통해 추격 | `src/entities/Enemy.js`, `src/world/Door.js`, `src/world/CollisionWorld.js` |
| 더 똑똑한 추적/3층 확장 | `src/world/CollisionWorld.js`, `src/entities/Enemy.js`, `src/config/gameConfig.js`의 `transitionWaypoints` |
| 공격/사망 연출 | `src/entities/Enemy.js`, `src/ui/Hud.js`, `src/core/Game.js` |
| 사운드 | 실제 음원 파일 준비 후 파일 기반 사운드 모듈, `src/core/Game.js` |
