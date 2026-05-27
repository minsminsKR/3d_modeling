# Prop Asset Requirements

이 문서는 현재 준비할 프롭 파일을 3D 모델 필요 항목과 텍스처만 필요한 항목으로 확정 분류합니다.

## 3D Model Required

아래 항목은 PNG만으로는 형태와 실루엣이 부족하므로 GLB 모델을 준비합니다. PNG가 있다면 같은 폴더에 참고 이미지나 모델 텍스처로 함께 둘 수 있지만, 최종 게임 적용 기준은 `model.glb`입니다.

1. `placeholder-wrapped-body-1f.png` -> `assets/props/placeholder-wrapped-body-1f/model.glb`
2. `placeholder-watching-mask-1f.png` -> `assets/props/placeholder-watching-mask-1f/model.glb`
3. `placeholder-hanging-bundle-stair.png` -> `assets/props/placeholder-hanging-bundle-stair/model.glb`
4. `placeholder-broken-doll-pile-2f.png` -> `assets/props/placeholder-broken-doll-pile-2f/model.glb`
5. `silent-mannequin-1f.png` -> `assets/props/silent-mannequin-1f/model.glb`
6. `silent-mannequin-2f.png` -> `assets/props/silent-mannequin-2f/model.glb`
7. `upper-doll-circle.png` -> `assets/props/upper-doll-circle/model.glb`
8. `upper-mirror-shards.png` -> `assets/props/upper-mirror-shards/model.glb`
9. `cicada-shells.png` -> `assets/props/cicada-shells/model.glb`
10. `corridor-wire.png` -> `assets/props/corridor-wire/model.glb`
11. `barred-window.png` -> `assets/props/barred-window/model.glb`
12. `barricade.png` -> `assets/props/barricade/model.glb`

## Texture Only

아래 항목은 바닥/벽/얇은 표면에 붙는 표현이므로 투명 PNG 텍스처만 준비합니다. 권장 파일명은 `basecolor.png`입니다.

1. `placeholder-red-puddle-1f.png` -> `assets/textures/props/placeholder-red-puddle-1f/basecolor.png`
2. `placeholder-red-puddle-hwacat.png` -> `assets/textures/props/placeholder-red-puddle-hwacat/basecolor.png`
3. `hanging-paper.png` -> `assets/textures/props/hanging-paper/basecolor.png`

## Notes

- GLB 모델 폴더에는 `model.glb`를 기본 파일명으로 둡니다.
- GLB가 자체 텍스처를 포함하지 않는다면 필요한 PNG를 같은 모델 폴더 또는 `assets/textures/props/<same-id>/`에 함께 둡니다.
- Texture only 항목은 가능하면 투명 배경 PNG로 만듭니다.

## Current Integration

- 현재 `assets/textures/props/...`에 들어온 GLB 파일은 게임에서 바로 참조하기 쉽도록 `assets/props/<prop-id>/model.glb`에도 복사되어 있습니다.
- 현재 `placeholder-red-puddle-1f`, `placeholder-red-puddle-hwacat`, `hanging-paper` PNG는 `basecolor.png` 이름으로 정리되어 `gameConfig.js`의 `textureUrl`에서 사용합니다.
- `MapBuilder`는 `assetUrl`이 있는 프롭을 `GLTFLoader`로 비동기 로드하고, `size` 기준으로 자동 스케일/접지 보정합니다. 로딩 실패 시 기존 fallback 프롭이 남아 콘솔 경고로 확인할 수 있습니다.
- GLB 방향/비율이 맞지 않으면 `assetRotation`, `assetFitAxes`, `assetScale`, `assetScaleMode: "stretch"`를 프롭 설정에 추가해 보정합니다. 통로를 실제로 막는 바리케이드는 충돌 크기와 시각 크기가 어긋나지 않도록 `stretch`를 사용합니다.
- `upper-doll-circle/model.glb`는 원형 인형 배치처럼 X/Z 폭이 넓고 Y 높이가 낮은 3D 프롭입니다. 인형방 중앙에 크게 배치하고, 낮게 읽히지 않을 정도의 Y 두께만 보강합니다.
