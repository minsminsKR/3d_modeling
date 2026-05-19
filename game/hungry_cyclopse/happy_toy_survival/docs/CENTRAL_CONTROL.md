# Central Control

이 문서는 `Hungry one eye Cyclopse` 프로토타입의 중심 문서입니다. 새 개발자나 AI 에이전트는 먼저 이 파일을 읽고, 수정할 시스템별 문서로 이동하면 됩니다.

## Runtime Entry

`main.py`가 `HungryCyclopseApp`을 만들고 즉시 `GameManager`를 생성합니다. Ursina의 전역 `update()`와 `input()`은 모두 `GameManager.update()`와 `GameManager.input()`으로 전달됩니다.

## Main Owner

`core/game_manager.py`가 전체 시스템을 조립합니다.

- `WorldManager`: 청크 월드와 공포 분위기
- `PlayerController`: 플레이어 이동, 성장, 카메라, 손전등
- `EnemyManager`: 스폰, AI 업데이트, 먹기/사망/튕김 판정
- `ScoreManager`: 생존 점수와 시간
- `PauseManager`: ESC 일시정지
- `HUD`, `PauseMenu`, `GameOverUI`: 화면 표시
- `ParticleEffects`, `ScreenEffects`: 먹기/죽음 효과
- `AudioManager`: 효과음/배경음 구조

## Edit Map

- 성장 규칙, 속도, 스폰 거리, 에셋 루트: `core/config.py`
- 플레이어 이동과 stamina: `player/player_controller.py`
- 카메라 감도와 3인칭 거리: `player/camera_controller.py`
- 손전등 배터리: `player/flashlight.py`
- 적 종류와 크기 분포: `enemies/enemy_manager.py`
- 개별 적 속도와 radius: `enemies/enemy_base.py`
- AI 상태 전환: `enemies/enemy_ai.py`
- 청크 크기와 생성 반경: `core/config.py`, `world/chunk_manager.py`
- 프롭 밀도: `world/prop_spawner.py`
- HUD 표시 항목: `ui/hud.py`

## Core Rules

- 플레이어 내부 size 시작값은 `5`입니다.
- visual scale은 `size / 5`입니다.
- 먹을 때마다 size가 `+1` 증가합니다.
- 이동속도는 size와 무관하게 유지됩니다.
- 적 size는 UI에 표시하지 않습니다.
- 접촉 판정은 거리 기반입니다.
- 플레이어가 더 크면 먹고, 더 작으면 즉사, 같으면 knockback입니다.

## Asset Policy

`core/config.py`는 지정된 에셋 루트를 스캔합니다. 모델명이 정확히 바뀌어도 character 폴더명과 animation 키워드가 유지되면 자동 탐색됩니다. 엔진 로딩 실패는 `core/asset_loader.py`의 `safe_entity()`가 primitive fallback으로 처리합니다.

## Prototype Boundaries

현재 맵은 실제 막힌 미로가 아니라 청크 바닥, fog, 어두운 조명, 먼 실루엣, 랜덤 프롭으로 복도 느낌을 만듭니다. 플레이어가 계속 커지는 규칙 때문에 강한 벽 충돌은 의도적으로 피했습니다.
