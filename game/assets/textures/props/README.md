# Prop Textures

공포 프롭의 표면 이미지 PNG/JPG를 여기에 넣습니다. GLB 모델 파일은 `assets/props/<prop_kind>/`에 두고, 이 폴더에는 그 모델이 참조하거나 게임에서 직접 재질로 쓸 텍스처만 둡니다. 현재 필요한 정확한 파일 분류는 `assets/PROP_ASSET_REQUIREMENTS.md`를 봅니다.

권장 구조:

```text
assets/textures/props/wrapped_body/
assets/textures/props/watching_mask/
assets/textures/props/red_puddle/
assets/textures/props/hanging_bundle/
assets/textures/props/broken_doll_pile/
assets/textures/props/mannequin/
assets/textures/props/doll_circle/
assets/textures/props/shards/
assets/textures/props/cicada_shells/
assets/textures/props/paper_strip/
assets/textures/props/wire_bundle/
assets/textures/props/barred_window/
assets/textures/props/barricade_planks/
```

권장 파일명:

```text
basecolor.png
normal.png
roughness.png
emissive.png
opacity.png
```

PNG 한 장만 준비할 경우 `basecolor.png`로 넣으면 됩니다.

이번 프롭 목록에서 텍스처만 필요한 항목은 아래 세 개입니다.

```text
assets/textures/props/placeholder-red-puddle-1f/basecolor.png
assets/textures/props/placeholder-red-puddle-hwacat/basecolor.png
assets/textures/props/hanging-paper/basecolor.png
```
