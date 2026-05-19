# Prop Model Assets

공포 프롭 GLB/GLTF 모델을 보관하는 폴더입니다. 텍스처 PNG/JPG는 `assets/textures/props/<prop_kind>/`에 둡니다. 현재 필요한 정확한 파일 분류는 `assets/PROP_ASSET_REQUIREMENTS.md`를 봅니다.

권장 구조:

```text
assets/props/wrapped_body/model.glb
assets/props/watching_mask/model.glb
assets/props/red_puddle/model.glb
assets/props/hanging_bundle/model.glb
assets/props/broken_doll_pile/model.glb
assets/props/mannequin/model.glb
assets/props/doll_circle/model.glb
assets/props/shards/model.glb
assets/props/cicada_shells/model.glb
assets/props/paper_strip/model.glb
assets/props/wire_bundle/model.glb
assets/props/barred_window/model.glb
assets/props/barricade_planks/model.glb
```

`src/config/gameConfig.js`의 `props[].id`와 `placeholderKind`를 보고 어떤 위치에 어떤 GLB를 연결할지 정합니다.

이번 프롭 목록처럼 위치별로 따로 만든 모델은 아래처럼 id와 같은 폴더를 씁니다.

```text
assets/props/placeholder-wrapped-body-1f/model.glb
assets/props/silent-mannequin-1f/model.glb
assets/props/barricade/model.glb
```
