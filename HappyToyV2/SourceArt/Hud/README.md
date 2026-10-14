# 손전등 HUD 제작 원본

2026-10-14: image_gen으로 낡은 철·황동 손전등 아이콘을 제작했다.

- 원본: `flashlight-metal-original.png` (알파 투명 PNG).
- 프롬프트·생성 기록·SHA-256: `flashlight-metal-generation.json`.
- Unity 사용 파일: `Assets/Resources/Hud/flashlight-metal.png`. 원본과 바이트가 같다.
- `UI/FlashlightGauge.cs`가 아이콘 옆에 황동색 연속 잔량 레일을 그린다.
- 이미지 편집/크롭 없이 Unity에서 비율을 유지하여 표시한다.
- 검사: `Tools/quality/run_feature_tests.py --profile flashlight-hud`.
