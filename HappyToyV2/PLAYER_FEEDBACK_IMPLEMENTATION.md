# 플레이 피드백 구현·검증 — 2026-10-06

기반은 GitHub `main`의 `04b4fd99d22ca41fdd76d98914670ca1620f1a13`, 개발 브랜치는 `codex/happytoy-feedback-2026-10-06`이다. 현재 변경은 로컬 개발 상태이며 커밋·푸시하지 않았다. 원본 학교 씬 SHA256은 `0f2d25f211c76c7aa5299702ad15cf52d042f5a316ef32f5c4f43d69994aaede`로 유지된다. 원본 FBX·색상 아틀라스·기존 제단은 덮어쓰지 않는다.

## 최신 8개 요구사항

| 요구사항 | 현재 구현 | 실제 검증과 남은 범위 |
| --- | --- | --- |
| 몬스터가 문을 열고 닫기 | Stalker·가면·마네킹이 실제 이동 문짝에 접근하고 밀기·통과·닫기를 공유한다. 공동 예약, 마지막 통과자의 닫기, 플레이어 끼임·움직이는 문 반전·중복 음 방지, 정지·저장 복원을 지원한다. 문 가장자리 접근이 벽기둥으로 밀리는 문제는 유효한 문 중앙 접근 경로로 수정했다. | 기존 실제 회랑·학교 문 6개와 고급 가면·마네킹 통과 검사를 통과했다. 중앙 접근·도착 수색·후속 새 소리까지 포함한 최종 통합 PlayMode **64 / 64**가 통과했다. |
| 추격 은신 75% 생존·25% 사망 | 성공 진입마다 플레이어가 한 번 추첨한다. 여러 활성 같은 층 추격자도 같은 결과를 공유한다. 조용한 진입은 추첨하지 않는다. 생존은 목격·이전 공격으로 덮어쓰지 않고, 판정 ID·결과·횟수를 저장한다. | 경계·중복·다중 적·일시정지·두 모드 체크포인트 검사 통과. 10만 표본: 생존 74,714 / 사망 25,286. 자연 RNG 전체 경로는 최종 Windows 검증 대상이다. |
| 실제 시야 발각 즉시 추격 | 실제 거리·시야·가림 판정 이후 Stalker와 등장 완료 가면이 같은 갱신에서 Chase·목적지를 설정한다. | 시야·가림·실제 발소리·마지막 관찰 수색 검사 통과. 전체 생존 경로와 기존 등장·변신을 최종 통합 검사에 포함했다. |
| 달리기 소리 지점 조사 | 실제 발 접촉 이벤트의 투영 위치만 조사한다. 실제 NavMesh 길이·원래 이동 속도·닫힌 문 수로 이동 예산을 계산한다. 최대 90초로 제한한 이동과 도착 후 수색 시간을 분리한다. 새 실제 소리나 시야만 목적지를 바꾼다. | 순수 시간·일시정지·유한 지연·저장 복원 검사 통과. 학교 가면의 실제 발소리→문 통과→도착 수색은 통과했다. 회랑 측면 접근·실제 문 통과·도착 수색·그 뒤 새 소리 전환은 최종 통합 검사에서 통과했다. |
| 적·소품 모델과 배치 | 원본 네 적의 형상·UV·본·원본 클립을 보존한 별도 노멀·smoothness 개선 자산을 선택했다. 실제 애니메이션 바인딩 루트와 같은 포즈로 크기를 맞추고 바닥 배치를 보정했다. 새 베벨·패널·철못 제단과 실제 동선 기반 5개 구역의 노후화·격자·벽 표식을 추가했다. | 64개 클립 포즈와 실제 이동 중 렌더된 모델의 최저점이 바닥에 맞는 검사, 원본 클립·변형·정지·12개 실제 카메라 화면 통과. 최저 스킨 정점에는 꼬리·손·몸이 포함될 수 있으므로 모든 발의 접지나 발소리 동기화를 증명하지 않는다. 독립적인 본 가중치 스킨 진단을 거쳐 Uncat의 실제 이동 후보를 같은 프레임 발·손 접지에 맞춰 한 번 재생하도록 수정했다. 정지·일시정지·warp·disable의 오래된 후보 제거 검사도 최종 통합에서 통과했다. 다른 형상·클립의 완벽한 발소리 동기화와 사람의 공포감은 미검증이다. |
| 바닥·천장 재질 | 새 v2 PNG, Unity 높이 기반 노멀, 실제 metre UV·셀 간 연속 좌표, mipmap·trilinear·anisotropy·Mirror sampling을 적용했다. | 실제 바닥·천장·통로 화면, UV·공유 자산 수명·구역 물리 보존 검사 통과. 런타임 셰이더 조합 4개를 별도 Resources 재질로 유지하며 반복 생성 GUID 보존까지 검증했다. 새 Windows 후보의 실제 첫 촛불 주변 화면에서 색상 재질·발광·국소 빛과 누락 없는 표면을 확인했다. 방향별 노멀·네 적의 smoothness 검수는 이 화면만으로 증명하지 않는다. |
| 어둠과 유한 배터리 | 손전등 240초, 보급 +120초·최대 240초(기존 저장 단위 180/90 유지, 소모율 0.75). 활성 점등 중 소모·고갈·F 재점등·HUD, 회랑 6개·학교 5개 유한 보급. 정지·소등·저장 복원·중복·상한을 처리한다. | 실제 F/E 입력, 고갈·상한·정지·소등·보급·두 모드 복원 통과. 전체 경로의 상당 부분은 손전등을 끄는 전략이므로 장시간 점등 자원 균형·초보 가독성은 별도 검증이다. |
| 직접 점화하는 촛불 | E 점화, 작은 국소 빛·불꽃·직접 합성한 점화음, 재점화 중복 없음, 점화 상태 저장. seed 73은 15개, 211은 17개. 촛불은 길 표식이다. | 실제 E 점화·재접근·정지·저장·모드 전환 검사 통과. Native 회랑 seed 73·211에서 실제 첫 점화와 초기 배터리 습득 후 화면·잔량을 보존했다. 전체 경로 탈출 검증과 구분한다. |

## 저장·자산 계약

- 배터리 도입 전 정상 저장의 `lightingVersion=0`은 충전된 새 조명으로 이행한다. 새 내용이 들어 있는 저장에서 버전 표식을 제거하거나 잘못된 ID·숫자·수량을 넣으면 거부한다. Unity가 생성하는 정말 빈 이전 DTO만 이전 상태로 인정한다.
- 은신 중 중단 저장 거부는 유지한다. 마지막 성공 진입 결과는 저장하지만 복원 때 재추첨하지 않는다.
- 이동 문은 실제 문짝·음원·요청 상태를 공유한다. 렌더링·조명 장식은 새 물리 장애물이나 암묵적인 안전 구역을 만들지 않는다.
- 새 제단은 `SourceArt/seal-altar-v2.blend`와 `Assets/Resources/Corridor/seal-altar-v2.fbx`, 새 적은 `SourceArt/EnemyRefinementV2/`와 `Assets/Resources/EnemyRefinement/`에 있다. 자세한 생성 지시·출처·재생성 범위는 [자산 출처 기록](SourceArt/FeedbackAssetProvenance.md)을 따른다.
- 새 색상 PNG의 원본 픽셀은 보존한다. `*-normal-v2.png`는 같은 높이 입력이며 Unity importer가 노멀로 변환한다. 생성기가 수학적으로 완전한 무이음 재질을 보장한다고 주장하지 않는다.
- 촛불은 그림자를 요청하지 않는다. 기존 손전등과 가까운 두 국소 등불의 그림자 예산은 유지한다. 생성한 메시·재질·텍스처·클립은 정리하고 원본 공유 자산은 파괴하지 않는다.
- Windows 빌드에는 기존 `Audio-Credits.txt`와 실제 빌드 입력 SHA256 목록을 포함한다. 셰이더 유지 자산은 AssetDatabase로 생성한 뒤 BuildPlayer와 입력 해시를 기록한다.

## 최신 실행 증거

작업 증거는 현재 `../game/verification/development-feedback/`에 있다. 아래 숫자는 해당 실행의 범위이며 전체 개발 변경 완료를 대신하지 않는다.

- `full-edit-03.xml`: 전체 EditMode **55 / 55 통과**. 앞선 `full-edit-02`의 셰이더 자산 이름 실패는 보존했고, 실제 저장 이름·재실행·GUID 보존을 수정 후 검증했다.
- `arrival-ground-play-13.xml`: **4개 중 3개 통과**. 네 적 64포즈·12개 카메라, Baby 원본 5초 안전 시간, 학교 실제 소리 도착 통과. 회랑 측면 문 접근 실패는 보존하고 수정했다.
- `arrival-ground-captures-13/`: 해시 검증된 화면 14장과 실제 모델 배치·카메라 JSON 2개. 원본 클립 객체, 스킨 변형, 공유 맵, 컨트롤러 단일성 검증 포함.
- `enemy-foley-play-14.xml`: **1 / 1 통과**, 네 적의 보행·추격 각각 4초, 실제 NavMesh 이동·발소리·자연 시간·바닥 배치 관찰. `enemy-foley-captures-14/`의 실제 프레임 스트립·JSON 24개는 접촉 시점을 분석하기 위한 진단이며 정확한 동기화 통과 선언은 아니다.
- `integration-play-19.xml`: 최종 기전·문·등장·조명·두 모드 저장·자원 수명·실제 weighted-limb 접촉 **64 / 64 통과**. 원본 학교 씬 SHA256 유지.
- `intro-audio-play-17.xml`: **3 / 3 통과**. 실제 등장 오디오와 변신·변형 흐름 검증.
- `search-door-play-18.xml`: **12개 중 11개 통과**. 기존 11개는 통과했고 새 도착 수색 fixture의 공개멤버 조회 실패를 보존했다. 공개 저장 DTO를 읽도록 수정한 해당 fixture는 19에서 통과했다.
- `integration-play-15.xml`: **59개 중 56개 통과**. 앞선 첫 등장 오디오·학교 문 통과·도달 불가 수색 시간 제한의 실패 세 개를 보존하고 최종 19에서 다시 검증했다.
- `Builds/FeedbackReview-20261006-0443/`: 새 Windows 후보 빌드 성공. `native-readiness/build20-input-verification.json`에서 실제 입력 2,126개 전체 경로·해시, 셰이더 유지 재질 4개, EXE·DLL·데이터·크레딧 동봉을 확인했다. 사용자 직접 플레이용으로 실행했던 후보이며 보존했다.
- `native-runs-21/corridor73/`: 자연 시간 **FAIL**, 기억 3개 회수 후 153.876초에 문 대기 중 Uncat에게 피격됐다. 실제 은신 확률 두 번은 모두 생존했다. 별도 `native-readiness/corridor73-capture-integrity.json`에서 저장 PCM·해시·비정상 값·클리핑·자연 시계 관계는 정상으로 확인했으며 경로 실패를 통과로 바꾸지 않는다.
- `native-runs-21/corridor211/`: 자연 시간 **FAIL**, 36.746초에 실제 상호작용 거리 밖에서 문에 초점을 맞추려다 중단됐다. 몸통 진행 감지와 눈의 2.2m 상호작용 범위 차이이며, 자동 입력 접근 경로의 수정 대상이다. 게임의 상호작용 범위를 넓히거나 AI를 억제해서 해결하지 않는다.
- `native-runs-21/school/interrupted-for-manual-play.json`: 사용자의 직접 플레이 요청으로 해당 자동 실행만 중단했다. 미완료 학교 실행은 통과·탈출 근거로 사용하지 않는다.
- `sector-art-play-05.xml`: **3 / 3 통과**, 5개 기억 구역과 실제 갈림길 화면 검토, 물리·경로·원본 상태 보존.
- Python 품질 도구: 80개 중 **71 통과·9 건너뜀**. 선택적인 C# parser 부재 6개와 Windows symlink 생성 권한 3개이며, Unity의 실제 C# 컴파일 검증은 별도로 수행했다.

실패 XML·로그를 삭제하거나 나중 결과로 덮어쓰지 않는다. `enemy-ground-play-11`은 테스트가 같은 아티팩트 이름을 재사용해 전체 전송 검증도 거부됐다. 이후 한 번만 누적 보고하도록 고쳤으며 원래 실패와 전송 거부는 그대로 보존한다.

## 남은 필수 검증

1. 통과한 최종 EditMode 55개·PlayMode 64개와 실패 이력을 보존한다. Native 전체 경로에서도 AI 억제·속도 수정·순간 이동·은신 생존 강제를 사용하지 않는다.
2. 입력 경로 수정 후 새 `Builds/FeedbackReview-20261006-wrap-24/` 후보를 빌드했다. 입력 2,126개 전체 경로·해시, 셰이더 유지 재질과 크레딧을 다시 확인했다. 기존 직접 플레이 후보는 덮어쓰지 않았다.
3. 관찰된 자동 입력 문제의 수정과 제어된 회귀 검사 4개는 통과했다. 수정 후 자연 시간 seed 73/211 회랑과 학교 5개 기억·네 계단·탈출을 다시 실행하는 검증은 남는다. 실제 DSP·WAV·클리핑·정지·시계·오류·모드 전환과 경로·정리·PCM 파일 저장 시간을 구분한다. 현재 두 회랑 실패와 학교 중단 기록을 보존한다.
4. 배포판에서 노멀·smoothness·발광이 유지되는지 확인한다. 네 모델은 다시 만든 해부학적 조각이나 모든 발 접촉 동기화의 완료물로 설명하지 않는다.
5. 장시간 손전등 점등의 보급 균형, 실제 기기 청감·첫 플레이 공포감·목표 기기의 focused 성능은 근거가 없으면 미검증으로 남긴다. 숨긴 자동 경로의 Update 간격은 GPU 시간이나 실제 화면 표시 FPS가 아니다.

로드맵 P1/P2 확장과 이번 최신 8개 피드백의 구현·증거를 구분한다.

## 현재 진행분 마무리 — 사용자 범위 제한

사용자의 “지금작업중인것만 마무리 해” 요청에 따라 이미 진행 중이던 자동 입력 경로·검증 보고서 수정과 새 후보 생성까지 마무리했다. 추가 아트·학교 그림자 예산·새 감사 도구·전체 native 경로 재실행은 시작하지 않았다. 최신 8개 요구사항 전체가 최종 완료됐다고 선언하지 않는다.

- 회랑 문 앞 대기·아이템 초점 대기에서 실제 시야/인식 경고를 관찰하면 실제 이동으로 회피하고 다시 접근한다. 문이 움직이는 동안 통과를 강행하지 않는다.
- 회랑·학교의 몸통 진행 감지가 눈의 상호작용 거리보다 먼저 문 대기를 시작하던 문제를 고쳤다. 게임의 2.2m 상호작용 범위·AI·속도·은신 RNG는 변경하지 않았다.
- 활성 Update에서 실제 targetFrameRate/VSync 값을 기록한다. 요청한 targetFrameRate와 화면 표시 FPS를 구분하며 VSync가 요청값을 무시할 수 있음을 명시한다.
- `native-controller-play-23.xml`: 제어된 실제 물리·입력 회귀 **4 / 4 통과**, 28.882초. 앞선 `native-controller-play-22.xml`은 **3 / 4**이며 존재하지 않는 문 ID를 사용한 fixture 조회 실패를 보존했다. 실제 생성되는 문과 실제 진입 쪽을 기준으로 고쳤다. 첫 native 실패 로그에는 문 stableId가 없어 정확한 당시 ID를 관찰 사실로 단정하지 않는다.
- `Builds/FeedbackReview-20261006-wrap-24/`: Unity Windows 빌드 성공. `native-readiness/build24-input-verification.json`에서 현재 입력 2,126개 전체 경로·해시와 크레딧·유지 재질 4개를 확인했다. 이전 Build20의 native 실패·중단은 보존했으며 새 후보의 탈출 통과 결과로 바꾸지 않는다.
- `REQUIREMENTS_COVERAGE_REVIEW.md`: 기전 통과와 적 형태·공포 배치의 부분 완료, 배포판 표면·장시간 보급 균형·첫 플레이 가독성·다수 촛불 focused 성능의 미검증을 구분한다. 학교 원본 Spot 조명이 추가 그림자 atlas를 압박하는 기존 예산 문제도 후속 검토로 남겼다.

원본 학교 씬·원본 자산과 기존 플레이 후보를 보존했다. 변경은 로컬 `codex/happytoy-feedback-2026-10-06` 브랜치에 있으며 커밋·푸시하지 않았다. 현재 진행분 종료를 최신 8개 전체 완료로 해석하지 않는다.

## 별도 추가 요청 — 전체 그래픽 업그레이드

이후 사용자가 사실적인 그래픽과 촛불 개선을 새로 요청해 외부 CC0 재질·직접 제작 자산으로 별도 진행했다. 앞의 “현재 진행분 종료”는 당시 범위의 기록이며 이 새 요청을 미실행으로 해석하지 않는다. 자산·조명·출처·검증 전체 기록은 [GRAPHICS_UPGRADE.md](GRAPHICS_UPGRADE.md)에 있다.

- Blender 4.0.2의 8개 packed source/FBX: `candle-waymark`, `battery-supply`, `paper-lantern`, `seal-altar`, `cabinet-shell`, `cabinet-timber`, `door-hardware`, `candle-flame`. 모델링된 우물·wet pool·drip·검은 꼬인 wick·접합부·베벨과 작은 투명 리본 불꽃을 실제 기존 E target에 연결한다. root collider/ID·배터리·점화·checkpoint는 유지한다.
- 실제 2K 4채널 PNG 48개: Poly Haven CC0 스캔 6세트, 원본 procedural 종이 1세트·소품 5세트. AI로 생성한 사진 스타일의 투명 불꽃은 실제 촬영 사진으로 표시하지 않으며 prompt/해시를 `SourceArt/GraphicsUpgrade/flame-generation.json`에 보존했다. 출처·채널 변환·설치 해시는 `ThirdParty/Graphics/`와 `SourceArt/GraphicsUpgrade/`에 있다.
- 별도 owned URP pipeline/renderer의 HDR·DepthNormals SSAO, Neutral·작은 Bloom·medium SMAA, 128 HDR box-projected 국소 probe, 실제 2048² 그림자 atlas 예산과 cookie 손전등을 연결했다. 모드 종료 때 원래 Light/camera/RenderSettings를 복원한다. 원본 pipeline·renderer·학교 씬은 보존한다.
- 기존 네 적은 원본 형상·UV·본·클립을 유지한 기존 v2 자산이며 이번 조명/재질 렌더링 개선을 새 조각으로 설명하지 않는다. 원본 특수 그림·물·혈흔·투명 표현은 유지한다.
- `graphics-edit-05.xml` 최초 전체 **61/62**, `props-edit-07.xml` targeted **1/2** 실패 이력은 보존했다. 실제 8개 imported bounds에서 X handedness를 증명해 fixture에 적용했고, 잘못 잘린 expected scene SHA만 64자리 보호 baseline으로 고쳤다. 크기/원점의 2mm 허용치를 넓히지 않았다.
- `graphics-edit-repair-08.xml` targeted **4/4 통과**: 8개 모델의 실제 크기·UV·normal/tangent·topology·물리 없음·원본 hash, 자체 20map/불꽃 alpha, capture encoder의 sRGB 변환·nonfinite pixel 거부. 이것을 수정 후 전체 64개 Editor 재실행이나 PlayMode/native 통과로 확대하지 않는다.

- `graphics-play-09.xml`: 첫 통합 **69/73**, 562.179초. 첫 등장 2개와 sector/cabinet lifecycle 실패, GPU-only school plinth/bolt의 잘못된 CPU Combine 오류 기록을 보존했다. imported mesh는 원래 renderer를 유지하며 소유 readable mesh만 결합하도록 고쳤다.
- `graphics-intro-10.xml`: 실제 가면·마네킹 첫 등장과 안전 시간을 포함한 focused **2/2**, 33.396초.
- `graphics-play-11.xml`: 최신 통합 **72/73**, 566.886초. 실제 첫 등장·접지·유한 빛/저장·학교/회랑 조명 복원은 통과했고 unreadable Combine 오류는 없다. 마지막 sector fixture는 정상적인 shadow/no-shadow 두 batch에 `Single`을 적용해 실패했다.
- `graphics-environment-12.xml`: 마지막 fixture가 모든 실제 batch world vertices를 검사하도록 **test만 수정**한 뒤 focused **4/4**, 20.214초. sector·학교 lifecycle·회랑 lifecycle·회랑 camera를 통과했다. 공유 종이 PBR·다른 pigment/joinery/기호·실제 geometry·물리/경로 검증을 유지하며 하나의 batch나 다섯 별도 BaseMap을 강제하지 않는다.

- `Builds/GraphicsReview-20261006-0726/`: 최초 그래픽 Windows 후보. `run-20261006-0726/build-input-proof.json`은 당시 입력2,327개·graphics165개·크레딧 동봉·원본 보호3개의 PASS를 기록했다. 이후 실제 결함 수정 전 후보이며 최종 source의 통과로 재사용하지 않는다.
- 최초 hidden native는 probe timeout으로 **FAIL/PNG0**였다. 같은 EXE의 visible 실행은 실제20개 PNG와 metadata/artifact를 통과했지만, `visual-review-root-first.json`의 실제20개 검토는 **NEEDS_REPAIR**다. 불꽃 폭, 학교 상층의 실제 반사 선택, 잘못 고른 회랑 문/화장실/다수 촛불 framing을 확인했다. 첫 metadata PASS를 시각 완료로 기록하지 않는다.
- 실제 4개 촛불/배터리 화면과 source geometry/alpha 분석을 거쳐 flame material의 수평 UV .40/.30만 적용했다. 원본 메시/이미지·조명 강도를 유지하며 몸통 source 계산6.92→17.3mm/높이49.9mm, alpha99.999831%를 보존한다. 학교 반사는 실제 layer0 support/3층 높이·작은 위치 구역과 torch/점화 변화로 갱신하고 pause·기존3초 cadence를 유지한다.
- `graphics-native-repair-14.log`의 CS0136 compile 실패는 보존했다. `GraphicsPresentationAudit`의 local `supportPlan` 이름을 고친 후 `graphics-native-repair-15.xml` focused **6/6**, 20.847초가 통과했다. 실제 학교3층 support·pause/반사·빛 변화, atlas와 두 모드 조명 복원, 실제 회랑 subject, 실제 점화/정지를 검증했다. 기존 물리/콜라이더·point-light intensity는 유지한다.

- 최종 `Builds/GraphicsReview-20261006-0806-fixed16/`:08:07:49.1896443Z 생성. `run-20261006-0806-fixed16/build-input-proof.json`과 `build-input-proof-after-native.json`에서 현재 입력2,329개·graphics165개·두 크레딧·원본 학교/pipeline/renderer 보호3개를 통과했고 native 촬영 뒤 재해시도 일치했다.
- 최종 실제 native PID42848: 자연78.829초·exit0·20개 PNG/errors없음. `native-art-visible/graphics-review.json` PASS와 `capture-scope-proof.json` **CAPTURE_SCOPES_PASS**를 기록했다. 감사 경과를 FPS로 계산하지 않는다.
- `visual-review-root-final.json`:Root가20개 실제 원본 PNG를 각각 ViewImage로 확인하고 SHA를 기록한 **ACCEPTED_FOR_DECLARED_GRAPHICS_SCOPE**. 불꽃은 blue-base/pale-yellow teardrop, 실제 화장실 두 toilet/sink와 타일, 실제 두 lit candle, 지상/상층/지하 probeY1.5/6.5/-3.5를 확인했다. 회랑 문은 실제 subject지만 비스듬한 camera로 hardware 근접 세부는 제한된다. 최초 NEEDS_REPAIR를 보존한다.
- 기존v2 네 적의 geometry/UV/rig/클립은 유지했다. 실제 enabled `V1MonsterMotion`과 production-convention `BakeMesh(true)` 모든 skin 최저점은 바닥과0~1.49e-8m로 일치한다. 다른 basis의 `BakeMesh(false)` 큰 음수를 렌더링 접지로 해석하지 않는다. Frozen patrol1.296초 Uncat의 weighted limb gap .319638m은 남으므로 모든 손발 plant·live AI locomotion의 인증이 아니다.

현재 기전 근거는 **전체72/73 + 마지막 focused4/4 + native 결함 수정 focused6/6**이며 단일 전체73/73로 표시하지 않는다. 최종 Build16/native20의 declared graphics scope 승인과 전체 생존/자연 입력 route·장시간 보급 균형·기기 청감·첫 플레이 공포감·HDR monitor·목표 GPU 성능을 구분한다. 새 적의 해부학 조각·날씨/노이즈 효과/path tracing 완료를 주장하지 않는다. 이전 Build24·64/64와 native 실패/학교 중단을 새 그래픽 경로 통과로 재사용하지 않는다.

## 별도 추가 요청 — 적 접근과 발각에 반응하는 촛불

사용자의 새 요청으로 학교와 회랑의 수동 점화 촛불에 위험 반응을 추가했다. 앞의 Build16 그래픽 검수는 이 후속 기능의 실행 증거로 재사용하지 않는다.

- 현재 실행에 속한 활성·NavMesh 준비 완료 적 중 같은 층의 적만 고려한다. 회랑은 실제 해당 회랑 아래의 네 역할 Stalker, 학교는 현재 chapter의 Cyclopse·초상화/육아실 Stalker·등장 완료 가면·해제된 마네킹을 사용한다. 비활성 적·미해제 등장·다른 모드의 원본 적은 경고원이 아니다.
- 플레이어와 가장 가까운 유효 적 사이의 실제 3D 거리 **12m→2m**에 따라 촛불의 떨림 깊이와 속도가 연속적으로 증가한다. 물리벽 뒤의 같은 층 적도 접근 경고를 줄 수 있으며, 경고가 AI의 시야·청각·경로를 바꾸지는 않는다.
- 거리가 **2m 이하**이거나 현재 Stalker·등장 완료 가면의 실제 시야/Chase·공격 판정, 해제된 마네킹의 실제 공격 판정이 성립하면 **현재 실행의 켜진 수동 촛불을 모두 실제 소등**한다. 다른 층 적의 시야·이전 모드 적은 이 판정에 포함하지 않는다. 불꽃 GameObject와 국소 Light가 함께 꺼지며 손전등 배터리와 기존 적 기전은 바꾸지 않는다.
- 소등 뒤 안전한 거리로 벗어나도 자동으로 다시 켜지지 않는다. 촛불이 꺼진 상태는 기존 checkpoint의 `lit=false`로 보존하며, 복원 중 점화음이나 점화 횟수를 재생하지 않는다. 다시 켜려면 위험 소등 조건이 해제된 뒤 기존 **E** 상호작용을 사용한다. E 직전에 실제 위험을 다시 검사하므로 금지 상태에서 점화·음·횟수가 발생하지 않는다. 위험도 자체는 저장값이 아니라 현재 적 상태에서 다시 계산한다.
- **움직임 감소** 설정에서는 흔들림과 맥동 대신 위험에 따른 일정한 어두워짐을 사용한다. 일시정지·메뉴·저장 복원 입력 gate에서는 위험 갱신과 불꽃/밝기 애니메이션을 멈추고 현재 상태를 유지한다. 재개 후 실제 적 상태를 다시 반영한다.

검증 결과: 최종 위험 감지·위상 계산 수정 이후 기존 촛불 점화/두 모드 저장·복원/실제 모델 일시정지 회귀 `regression02.xml` **3/3 PASS**, 새 실제 거리·시야·전체 소등·재점화·대기/다른 모드/다른 층 제외·pause/ReducedMotion·두 모드 checkpoint 검사 `danger03.xml` **6/6 PASS**, 실제 조명 밝기 시계열로 접근에 따른 떨림 깊이·주기 증가를 확인한 `flicker04.xml` **1/1 PASS**를 기록했다. 이 세 targeted 실행을 단일 전체 테스트 실행으로 표시하지 않는다.

새 Windows 후보 **`Builds/CandleDangerReview-20261006/HappyToyV2.exe`** 빌드 성공 및 전체 현재 Assets/Packages/ProjectSettings 입력 해시·기존 그래픽 자산/원본 장면 보존 검증 **GRAPHICS_BUILD_INPUTS_PASS**. 결과는 `game/verification/candle-danger/FINAL-REPORT.json`과 `build-input-proof.json`에 보관한다. 이전 Build16은 그대로 보존하며, 새 후보의 실제 사용자 장시간 플레이·전체 생존 균형·새 native 화면 검수 완료는 주장하지 않는다.
