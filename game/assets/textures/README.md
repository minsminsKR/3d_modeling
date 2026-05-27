# Happy Toy Texture Assets

환경 스킨 PNG를 보관하는 폴더입니다.

권장 규칙:

- 벽 텍스처: `walls/`
- 바닥 텍스처: `floors/`
- 천장 텍스처: `ceilings/`
- 계단 텍스처: `stairs/`
- 문 텍스처: `doors/`
- 캐비넷 텍스처: `cabinets/`
- 소품 텍스처: `props/`

PNG 한 장으로도 적용할 수 있습니다. 벽이나 바닥처럼 반복되는 재질은 이음새가 티 나지 않는 tileable 이미지가 가장 좋고, 크기는 `1024x1024` 또는 `2048x2048`처럼 정사각형 power-of-two를 권장합니다.

예시:

```text
assets/textures/walls/old_green_wall.png
assets/textures/floors/dark_concrete_floor.png
assets/textures/ceilings/stained_ceiling.png
assets/textures/stairs/old_concrete_stair_tread.png
assets/textures/doors/brown_wood_door.png
assets/textures/cabinets/rusted_locker.png
```
