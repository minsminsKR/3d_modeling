# Hungry one eye Cyclopse

Python + Ursina 기반 3D 생존 성장 게임 프로토타입입니다. 플레이어 Cyclopse는 자신보다 작은 적을 접촉해서 먹고 성장하며, 후반에는 Giant Cyclopse에게 쫓기며 살아남습니다.

## 설치

```powershell
cd E:\AI\3d_modeling\game\hungry_cyclopse\happy_toy_survival
python -m pip install ursina
```

## 실행

```powershell
python main.py
```

실행하면 바로 게임이 시작됩니다. 외부 FBX/GLB/JPG 에셋 로딩에 실패하면 primitive 모델로 자동 fallback합니다.

## 조작

- `WASD`: 카메라 기준 이동
- `Shift`: 달리기, stamina 소모
- `Mouse`: 시점 회전
- `F`: 손전등 on/off
- `ESC`: 일시정지 메뉴
- `R`: 게임오버 후 재시작

## 프로젝트 구조

- `main.py`: Ursina 앱 진입점
- `core/`: 게임 루프, 점수, 일시정지, 설정, 안전 에셋 로더
- `player/`: 플레이어 이동, 3인칭 카메라, 손전등/배터리
- `enemies/`: 적 엔티티, AI, 스폰/풀링/접촉 판정
- `world/`: 청크 월드, noise, 프롭 스폰
- `ui/`: HUD, pause menu, game over UI
- `effects/`: 먹기 파티클, 죽음 화면 효과
- `audio/`: 무음 fallback 오디오 매니저
- `docs/`: 시스템별 상세 문서

## 에셋

기본 에셋 루트는 `E:\AI\3d_modeling\game\happy_toy\assets`입니다. `core/config.py`가 Cyclopse, Hwacat, Uncat, Hwacat_angry 모델과 텍스처, GLB 프롭을 자동 탐색합니다.
