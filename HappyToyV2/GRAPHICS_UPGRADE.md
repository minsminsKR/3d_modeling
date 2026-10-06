# 그래픽 업그레이드 — 모델·재질·조명과 검증 범위

2026-10-06의 별도 그래픽 요청에 따라 촛불·보급품·등불·제단·캐비닛·문 손잡이를 새 3D 자산으로 만들고, 회랑과 학교의 건축·가구 표면을 실제 크기의 PBR 재질로 연결했다. HDR 조명·접촉 AO·국소 반사·후처리를 별도 소유 자산으로 적용했다. 최종 후보 [Builds/GraphicsReview-20261006-0806-fixed16](Builds/GraphicsReview-20261006-0806-fixed16/)의 현재 입력2,329개·graphics165개·두 크레딧·원본 보호3개와 native 후 재해시가 통과했다. 최종 visible20 PNG의 실제 scope 검사와 개별 화면 검토는 **ACCEPTED_FOR_DECLARED_GRAPHICS_SCOPE**다. 기전 근거는 Editor targeted4/4, 통합 PlayMode72/73 + 환경 focused4/4 + native 결함 수정 focused6/6이며 단일 전체73/73 실행으로 표시하지 않는다. 처음 발견한 실패와 실제 시각 NEEDS_REPAIR 기록은 보존한다. 이전 피드백의64/64·Build24를 이번 그래픽 증거로 재사용하지 않는다.

## 실제 모델 변경

모델은 Blender 4.0.2로 직접 제작했다. 편집 가능한 packed `.blend`는 [SourceArt/GraphicsUpgrade/Props](SourceArt/GraphicsUpgrade/Props/), Unity FBX는 [Assets/Resources/GraphicsUpgrade/Props](Assets/Resources/GraphicsUpgrade/Props/)에 있다. 모델마다 하나의 공유 정적 메시·UV·노멀·재질 슬롯을 사용하며, FBX에 콜라이더·Rigidbody·애니메이션을 추가하지 않는다. 표의 크기는 인스턴스 배치 전 원본의 미터 단위 bounds이다.

| 자산 키 | 삼각형 수 | 크기 X × Y × Z (m) | 새 형상 |
| --- | ---: | --- | --- |
| `candle-waymark` | 8,010 | .224 × 1.27836 × .224 | 둥글고 불규칙하게 녹은 윗면·파인 왁스 우물·젖은 pool·굳은 drips와 puddle, 꼬인 검은 심지·끝의 ash, 말린 황동 받침과 접합부·철제 기둥·베벨 발판. |
| `battery-supply` | 8,460 | .224 × 1.334 × .224 | 감싼 배터리 두 개, rolled seams·압착 cap·돌출 단자·실제 극성 기호, 띠와 keeper·단조 받침. |
| `paper-lantern` | 7,988 | .36881 × .54096 × .36948 | 접힌 washi shade, 부풀어진 외피를 따르는 12개 곡선 rib, 둘레 hoop·금속 cap·걸이. |
| `seal-altar` | 8,408 | .810 × .68950 × .600 | 판재·장부 rail·recessed panel·bead·peg·벌어진 다리·금속 strap와 nail. 기존 .85 × .72 × .65 물리 footprint 안에 배치. |
| `cabinet-shell` | 11,452 | .899 × 1.87550 × .67777 | 학교용 낡은 enamel locker. 굽힌 shell·문 seam·louver relief·hinge·곡선 손잡이·홈이 있는 나사·종이 label. |
| `cabinet-timber` | 13,332 | .899 × 1.87550 × .67777 | 회랑용 timber cupboard. 같은 bounds·hardware에 실제 raised stile와 mortise rail을 추가. |
| `door-hardware` | 1,144 | .061 × .211 × .02915 | 베벨 plate·손가락 recess·곡선 pull·slotted screw. 실제 움직이는 문짝을 따른다. |
| `candle-flame` | 48 | .02921 × .064 × .02530 | 좁고 비대칭인 곡선 리본 3개와 투명 RGBA 이미지. 밝은 중심과 부드러운 외곽을 가진 약 5cm 불꽃. |

회랑 바닥 Y=0과 기존 station 기준 Y=.03의 차이는 **body와 flame만 -.03m** 내려 보정했다. 상호작용 root·stable ID·BoxCollider·point light 위치·E/F·충전·점화·저장은 유지한다. 학교 station offset은 0이다. 제단은 모델 최저점과 실제 바닥을 맞추고 문 손잡이는 바깥으로 향하는 별도 회전 mount에 붙인다. 해당 배치의 실제 PlayMode 접지 검사는 통합 11과 focused 12에서 통과했다.

기존 네 적은 앞선 `EnemyRefinementV2`의 원본 형상·UV·본·원본 클립을 보존한 자산을 계속 사용한다. 이번 요청에서는 새 건축·재질·빛·AO·반사로 렌더링을 개선한다. 네 적을 새로 만든 해부학적 조각이나 새 캐릭터 모델이라고 설명하지 않는다. 그림·혈흔·물·유리·기존 특수 shader와 확인되지 않은 imported material은 원래 표현을 보존한다.

## 12개 2K PBR 세트와 출처

[Assets/Resources/GraphicsPbr](Assets/Resources/GraphicsPbr/)에는 **12세트 × 4채널 = 48개 2048×2048 PNG**가 있다. 채널은 `albedo.png`, `normal.png`, `ao.png`, `metallic-smoothness.png`다. 데이터와 Unity import 설정을 함께 기록하며, 색상 이미지를 노멀 이름으로 바꿔 사용하지 않는다.

외부 6세트는 Poly Haven의 스캔 재질이며 [공식 라이선스](https://polyhaven.com/license)에 따라 CC0로 제공된다. 원본 다운로드·공식 체크섬·변환·출력 SHA256은 [polyhaven-pbr-manifest.json](ThirdParty/Graphics/polyhaven-pbr-manifest.json), 사용 크레딧은 [Graphics-Credits.txt](ThirdParty/Graphics/Graphics-Credits.txt)에 있다.

| 게임 재질 | 외부 원본 | 제작자 |
| --- | --- | --- |
| `wood-floor` | [wood_floor_worn](https://polyhaven.com/a/wood_floor_worn) | Dimitrios Savva |
| `wood-aged` | [wood_table_worn](https://polyhaven.com/a/wood_table_worn) | Dimitrios Savva, Rico Cilliers |
| `plaster-damp` | [worn_plaster_wall](https://polyhaven.com/a/worn_plaster_wall) | Dimitrios Savva |
| `concrete-rough` | [concrete_floor_02](https://polyhaven.com/a/concrete_floor_02) | Rob Tuytel |
| `ceramic-tile` | [floor_tiles_06](https://polyhaven.com/a/floor_tiles_06) | Rob Tuytel |
| `metal-rust` | [rusty_metal_02](https://polyhaven.com/a/rusty_metal_02) | Rob Tuytel |

자체 제작 6세트는 `paper-aged`와 `wax-tallow`, `wax-pool`, `brass-tarnished`, `cloth-charred`, `painted-metal`이다. 종이 섬유·왁스·산화 황동·탄 심지·칠이 벗겨진 금속의 색·미세 높이·AO·거칠기를 연관시킨 원본 procedural 데이터이며 사진 스캔으로 표시하지 않는다. [종이 출처 기록](SourceArt/GraphicsUpgrade/paper-source-manifest.json)과 [소품 PBR 출처·해시](SourceArt/GraphicsUpgrade/props-pbr-manifest.json)를 따른다. Wax·pool·cloth의 금속성은 0이며 wet pool은 별도 높은 smoothness 재질이다.

외부 OpenGL tangent normal은 실제 NormalMap으로 가져온다. 외부 ARM의 R은 AO, B는 metallic, `255-G`는 smoothness로 변환한다. 자체 map도 동일한 **R=metallic / A=smoothness / G=B=0** 계약을 사용한다. Albedo만 sRGB이며 normal·AO·packed data는 linear다. PBR은 mipmap·trilinear·anisotropy16·repeat를 사용하고 실제 재질의 물리 반복 크기에 맞춰 UV와 tangent frame을 만든다. 학교 건축은 기존 renderer/mesh/material 상태를 소유자가 보관하고 모드 종료 때 복원한다.

[candle-flame-v3.png](Assets/Resources/GraphicsUpgrade/Textures/candle-flame-v3.png)는 OpenAI 내장 이미지 생성으로 만든 **사진 스타일의 투명 이미지**다. 실제 촬영한 불꽃 사진으로 표시하지 않는다. 생성 지시·원본 경로·RGBA 검사·SHA256은 [flame-generation.json](SourceArt/GraphicsUpgrade/flame-generation.json)에 있다. 원본은 1024×1536 RGBA, alpha 0~254이며 flame importer의 최대 크기는 1024다. [CandleFlame.shader](Assets/Resources/GraphicsUpgrade/Shaders/CandleFlame.shader)는 부드러운 alpha·양면·ZWrite Off·emission 1.55·opacity .76을 사용한다. shader 자체 시간 애니메이션 없이 기존 일시정지·ReducedMotion gate를 따른다.

첫 실제 점화/근접 native 화면에서 불꽃이 가는 선처럼 보였다. 원본 sprite의 큰 투명 여백과 리본 geometry의 taper가 겹쳐 source 계산상 몸통이 약6.92mm, 실제 alpha 높이가49.9mm였다. 세 방향 리본의 실제 카메라 최대 투영 비율은 .990/.924여서 전체 edge-on 현상으로 설명되지 않았다. 원본 geometry/image를 유지하고 소유 flame material의 **수평 UV scale .40 / offset .30**만 적용해 몸통 약17.3mm를 사용하도록 고쳤다. alpha mass99.999831%와 부드러운 경계(alpha0)를 보존한다. [최초 4개 이미지 검토](../game/verification/graphics-upgrade/props/native-review-01/REVIEW.md)·[source/alpha 증거](../game/verification/graphics-upgrade/props/native-review-01/flame-readability-proof.json)를 보존한다. 수정 후 실제 [회랑 촛불 근접](../game/verification/graphics-upgrade/lighting/native-verification/run-20261006-0806-fixed16/native-art-visible/corridor-candle-close.png)과 [학교 촛불 근접](../game/verification/graphics-upgrade/lighting/native-verification/run-20261006-0806-fixed16/native-art-visible/school-candle-close.png)에서 파란 base·옅은 노란 teardrop 형태를 확인했다. source 계산과 실제 화면 증거를 구분한다.

원본 모델 bounds·재질 슬롯은 [props-model-manifest.json](SourceArt/GraphicsUpgrade/props-model-manifest.json), 설치된 48개 map·8개 FBX·8개 packed blend·불꽃의 byte/SHA256 목록은 [graphics-installed-asset-manifest.json](SourceArt/GraphicsUpgrade/graphics-installed-asset-manifest.json)에 있다. [모델 생성 코드](SourceArt/GraphicsUpgrade/author_props_v3.py)와 [소품 PBR 생성 코드](SourceArt/GraphicsUpgrade/build_prop_pbr.py)는 생성 당시의 기록이다. 재생성은 의존 map을 준비한 별도 staging에서 수행하며 현재 프로젝트 자산을 직접 덮어쓰는 실행 지침이 아니다.

## 렌더링과 소유권

[GraphicsLightingSetup](Assets/Editor/GraphicsLightingSetup.cs)은 원래 `SchoolPipeline 17.asset`·`SchoolRenderer 17.asset`을 덮어쓰지 않고 [Assets/GraphicsUpgrade/Settings](Assets/GraphicsUpgrade/Settings/)에 별도 pipeline/renderer를 생성한다. 현재 활성 pipeline은 HDR·depth·URP 17.6 postprocess shader resources와 실제 PBR keyword anchor를 유지한다.

- SSAO: standard DepthNormals, downsampled 4 samples, strength .65, radius .22m, direct-light contribution .1.
- 후처리: Neutral tonemapping, Bloom intensity .16 / threshold 1.1, medium SMAA. 게임 카메라의 실제 URP postprocess/volume/AA 상태를 capture camera에도 전달한다.
- 반사: 128 HDR의 국소 realtime box-projected probe 하나, reflection blending. 실제 회랑 셀과 학교의 발밑 support collider·층 높이·작은 위치 구역을 관찰한다. torch 켜짐/촛불 점화 수 변화도 관찰하여 같은 구역의 빛 변화 뒤 갱신한다. pause의 기존 InputAllowed gate와 이전 capture 완료·최소 3초 간격을 유지한다. 화면 공간 반사나 ray tracing을 적용했다고 주장하지 않는다.
- 그림자: 실제 2048² 추가광 atlas, 손전등 1024 tile 우선·최대 두 Point의 각 6×512 tile. Point가 적을 때 남은 면적에 실제 가까운 Spot을 배정한다. 촛불은 그림자를 요청하지 않으며 적 상태로 예산을 바꾸지 않는다.
- 손전등: 4900K·52°/22° beam·range18·intensity5·soft lens cookie. 켜짐·충전·F·배터리 소모의 기전은 원래 소유자가 유지한다.

`GraphicsPropLibrary.Attach`는 identity wrapper 안에 imported FBX child의 basis를 유지한다. 재질은 `GraphicsSurfaceLibrary.Pool.Resolve`가 캐시하고 공유 mesh·Resources texture를 복제/파괴하지 않는다. 소유한 run material·일시적 geometry·probe·volume·cookie만 정리한다. 조명은 모드 준비 전 Light/camera/RenderSettings를 보관하고 종료 때 복원한다. static architecture batching은 CPU-readable 소유 mesh만 결합하고 GPU-only imported mesh는 원래 renderer를 유지한다. 공유 소품과 batching 구현은 draw-call 감소·GPU 시간·표시 FPS의 실측 통과를 의미하지 않는다.

## 실제 검증 기록과 남은 범위

Unity는 `6000.6.0f1`이다. [lighting-setup.json](Verification/graphics-upgrade/lighting-setup.json)에서 원본 학교 씬·원본 pipeline/renderer SHA256과 별도 자산 GUID를 확인했다. 학교 원본 SHA256은 `0f2d25f211c76c7aa5299702ad15cf52d042f5a316ef32f5c4f43d69994aaede`다. [source-check-02.json](../game/verification/graphics-upgrade/source-check-02.json)은 당시 메타 GUID 1,161개와 씬 component 968개의 참조·씬/build-settings 보존을 검사한 **정적 기록**이며 당시 미실행한 컴파일·native·화면 항목까지 통과로 해석하지 않는다.

| 실행 기록 | 실제 결과 | 해석 |
| --- | --- | --- |
| [graphics-edit-05.xml](../game/verification/graphics-upgrade/graphics-edit-05.xml) | 최초 전체 EditMode **61/62** | Lantern 중심 X의 source→Unity handedness를 fixture에 반영하지 않아 한 검사 실패. 원래 실패를 보존했다. |
| [unity-imported-props-bounds.json](../game/verification/graphics-upgrade/props/grounding-repair/unity-imported-props-bounds.json) · [handedness-proof.json](../game/verification/graphics-upgrade/props/grounding-repair/handedness-proof.json) | 8개 실제 imported geometry 확인 | X만 반사하고 Y/Z·미터 크기를 유지. 최대 중심 오차 .000000134317m, 크기 오차 .000000210734m. imported rootScale100/rootEulerX270을 child wrapper가 보존한다. |
| [props-edit-07.xml](../game/verification/graphics-upgrade/props-edit-07.xml) | targeted **1/2** | 8개 모델 검사를 모두 진행한 뒤 잘못 잘린 54자리 expected SHA로 실패. 실제 원본은 64자리 보호 baseline과 일치했고 fixture 문자열을 바로잡았다. |
| [graphics-edit-repair-08.xml](../game/verification/graphics-upgrade/graphics-edit-repair-08.xml) | 수정 후 targeted **4/4** | 8개 prop의 실제 미터 bounds·topology·UV·normal/tangent·물리 없음·원본 hash, 자체 20map와 불꽃 alpha, SDR capture의 sRGB encoding·비정상 pixel 거부 통과. 크기·원점 2mm 한계를 넓히거나 모델 basis를 변경하지 않았다. |
| [graphics-play-09.xml](../game/verification/graphics-upgrade/graphics-play-09.xml) | 최초 통합 **69/73**, 562.179초 | 학교 첫 등장 2개 timeout, 5개 sector BaseMap의 과거 가정, 원래 cabinet renderer 복원 기대에서 실패. 당시 로그의 CPU-unreadable plinth/bolt Combine 오류도 보존했다. |
| [graphics-intro-10.xml](../game/verification/graphics-upgrade/graphics-intro-10.xml) | focused **2/2**, 33.396초 | 가면·마네킹의 실제 첫 등장·안전 시간과 문 통과를 별도로 재검증. |
| [graphics-play-11.xml](../game/verification/graphics-upgrade/graphics-play-11.xml) | 최신 통합 **72/73**, 566.886초 | 기존 64개 기전과 모델/접지/학교표면/조명·복원/자동입력 9개 포함. 첫 등장 2개는 통과하고 unreadable Combine 오류는 사라졌다. 남은 sector fixture의 `Single`은 같은 cell/material에 castShadows가 다른 정상 두 batch를 잘못 거부했다. |
| [graphics-environment-12.xml](../game/verification/graphics-upgrade/graphics-environment-12.xml) | 마지막 focused **4/4**, 20.214초 | product 변경 없이 fixture가 모든 실제 batch의 world vertices를 검사하도록 수정. sector·학교 lifecycle·회랑 lifecycle·회랑 camera 통과. **전체 단일 실행 73/73 기록은 없다.** |
| [최초 GraphicsReview-20261006-0726 빌드 입력 검사](../game/verification/graphics-upgrade/lighting/native-verification/run-20261006-0726/build-input-proof.json) | **BUILD_INPUTS_PASS** | 최초 후보의 전체 입력2,327개·graphics 입력165개·크레딧 동봉·원본 보호3개를 당시 소스와 확인했다. 이후 수정 전 후보이며 현재 최종 source의 통과로 재사용하지 않는다. |
| [최초 hidden native](../game/verification/graphics-upgrade/lighting/native-verification/run-20261006-0726/native-art/graphics-review.json) | **FAIL**, PNG0 | 첫 probe capture timeout을 보존했다. 같은 EXE의 visible 실행은 진행됐으며 이 hidden/window 동작만으로 product reflection bug를 단정하지 않는다. |
| [최초 visible native20 metadata](../game/verification/graphics-upgrade/lighting/native-verification/run-20261006-0726/native-art-visible/graphics-review.json) · [아티팩트 검사](../game/verification/graphics-upgrade/lighting/native-verification/run-20261006-0726/capture-artifacts-proof.json) | metadata/artifact **PASS**, 실제20 PNG | 실제 이미지 검토의 승인을 뜻하지 않는다. 최초 [scope 재검사](../game/verification/graphics-upgrade/lighting/native-verification/run-20261006-0726/capture-scope-rejection.json)는 FAIL이며 보존했다. |
| [최초 실제20개 이미지 검토](../game/verification/graphics-upgrade/lighting/native-verification/run-20261006-0726/visual-review-root-first.json) | **NEEDS_REPAIR** | 가는 불꽃, 학교 문을 회랑 문으로 고른 fixture, 실제 화장실이 아닌 camera, 촛불이 보이지 않는 many-candles framing, 학교 상층에서 지상 probe를 사용한 product 오류를 확인했다. |
| [graphics-native-repair-14.log](../game/verification/graphics-upgrade/graphics-native-repair-14.log) | 컴파일 **FAIL** | `GraphicsPresentationAudit`의 중복 local `support`가 CS0136을 발생시켰다. local을 `supportPlan`으로 고친 뒤 재실행했으며 실패 로그는 보존한다. |
| [graphics-native-repair-15.xml](../game/verification/graphics-upgrade/graphics-native-repair-15.xml) | 수정 후 focused **6/6**, 20.847초 | 실제 학교3층 support·pause/빛 변화 후 probe, 실제 두 모드 조명 복원·atlas, 회랑 subject 고르기, 실제 점화/pause를 확인했다. 물리 geometry/콜라이더와 기존 point-light intensity는 변경하지 않았다. |
| [최종 Build16 입력 검사](../game/verification/graphics-upgrade/lighting/native-verification/run-20261006-0806-fixed16/build-input-proof.json) · [native 후 재해시](../game/verification/graphics-upgrade/lighting/native-verification/run-20261006-0806-fixed16/build-input-proof-after-native.json) | **GRAPHICS_BUILD_INPUTS_PASS** | `GraphicsReview-20261006-0806-fixed16`, manifest 생성08:07:49.1896443Z. 현재 전체 입력2,329개·graphics165개·Audio/Graphics 크레딧·원본 학교/pipeline/renderer 보호3개를 확인했고 촬영 후에도 일치한다. |
| [최종 visible native 실행](../game/verification/graphics-upgrade/lighting/native-verification/run-20261006-0806-fixed16/native-process.json) · [실제20 PNG metadata](../game/verification/graphics-upgrade/lighting/native-verification/run-20261006-0806-fixed16/native-art-visible/graphics-review.json) · [scope 검사](../game/verification/graphics-upgrade/lighting/native-verification/run-20261006-0806-fixed16/capture-scope-proof.json) | **PASS / CAPTURE_SCOPES_PASS** | PID42848, 자연 경과78.829초, exit0, 실제20 PNG·errors없음. 이 경과는 probe settling·촬영·저장을 포함한 감사 시간이며 표시 FPS/GPU 시간으로 계산하지 않는다. |
| [최종 실제20개 이미지 검토](../game/verification/graphics-upgrade/lighting/native-verification/run-20261006-0806-fixed16/visual-review-root-final.json) | **ACCEPTED_FOR_DECLARED_GRAPHICS_SCOPE** | Root가20개 원본 PNG를 각각 ViewImage로 검토하고 SHA256을 기록했다. 기존 NEEDS_REPAIR를 보존하고 새 이미지의 소품·PBR·국소 빛·실제 층 반사를 승인했다. 전체 생존·live gait·기기 청감·사람 공포·GPU FPS 승인이 아니다. |

통과한 새 접지 fixture는 seed73의 실제 바닥 physics hit와 21개 station renderer 최저점을 독립 비교하고 target/light/미점화 상태를 함께 확인했다. 기존 실제 E/F·유한 보급·두 모드 점화 checkpoint·pause·mode teardown도 통합 11에서 통과했다. 다섯 sector는 동일한 실제 종이 PBR map을 공유하면서 다른 pigment·joinery·기호를 유지한다. fixture는 별도 texture 5개나 cell/material당 batch 1개를 강제하지 않고 실제 공유 map·서로 다른 pigment·실제 crossbar geometry·기호·물리/경로 보존을 검사한다.

현재 클래스 `GraphicsPresentationAudit`의 **20개 실제 카메라 화면**은 회랑 촛불 미점화/점화/근접, 배터리·문·등불·바닥/천장·캐비닛·봉인방·다수 촛불·네 적, 학교 복도·화장실·음악실·상층·지하·촛불을 검토한다. 회랑 item은 실제 회랑 root에서 선택하고 문짝 renderer의 실제 bounds를 겨냥한다. 화장실은 실제 tiled floor와 observer support, many-candles는 실제 가림 없는 두 lit flame이 화면에 보이는지를 확인한다. 이 검토는 배우를 명시적으로 정지하고 observer pose를 설정한 제어된 아트 검사다. PNG는 ARGBHalf render와 RGBAHalf readback의 선형 픽셀을 sRGB로 인코딩한 SDR 보존 파일이다. 전체 생존 경로·실제 입력 route·장시간 점등 균형·기기 청감·사람의 공포감·HDR display·실측 GPU FPS를 인증하지 않는다.

최종 실제 화면에서는 화장실의 두 toilet·두 sink·타일/partition, 실제 화면 안의 두 lit candle과 warm pool, 실제 회랑 door leaf를 확인했다. 학교 probe centerY는 지상1.5·상층6.5·지하-3.5이며 실제 supportY는 각각0·5·-5다. 문은 실제 회랑 subject지만 비스듬한 camera여서 hardware의 근접 세부 증거는 제한된다. 배터리 wrapper는 plain cream이고 강한 근접 torch의 wax highlight도 남아 있으며 실제 검토는 그 한계를 포함한 현재 그래픽 범위 승인이다.

네 적의 실제 metadata에서 `V1MonsterMotion.enabled=true`이고 production-convention `BakeMesh(true)+TransformPoint`의 모든 skin 최저점은 실제 바닥과0~1.49e-8m로 일치한다. `BakeMesh(false)`의 큰 음수는 원본 import scale basis를 잘못 적용한 다른 측정이며 실제 렌더링 접지로 사용하지 않는다. 최저점에는 몸/꼬리도 포함된다. 특히 Uncat frozen patrol1.296초의 weighted limb gap은 .319638m이므로 이20개 posed 이미지로 모든 발·손 plant나 live AI locomotion을 인증하지 않는다. 원래 형상·UV·rig·클립을 유지한 기존v2 자산이라는 설명도 유지한다.

현재 기전/환경 근거는 **전체72/73 + 마지막 focused4/4 + native 결함 수정 focused6/6**이며 이를 합쳐 단일 전체73/73로 표시하지 않는다. 최종 Build16과 declared graphics scope의 실제20개 시각 승인은 별도 근거다. 첫 hidden 실패·visible metadata PASS·실제 NEEDS_REPAIR·CS0136 및 앞선 fixture 실패를 삭제하지 않는다. 자연 시간 회랑 seed73/211·학교 전체 경로, 첫 플레이 가독성·장시간 자원 균형·기기 청감·사람의 공포감은 독립적으로 남아 있다. source를 새 해부학 조각으로 바꾸거나 날씨/노이즈 효과·path tracing을 추가/검증했다고 주장하지 않는다. SDR PNG는 HDR monitor를 인증하지 않으며 GPU timing feature의 존재는 실측 GPU 시간·목표 기기 FPS가 아니다. 실제 성능과 전체 생존을 제어된 아트 화면으로 대체하지 않는다.
