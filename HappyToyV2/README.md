# Happy Toy V2 — 돌아오지 않는 회랑

메인은 갈림길과 우회로가 있는 무작위 회랑이다. 흩어진 봉인 기억 다섯 개를 자유로운 순서로 회수하고 시작 지점의 문으로 돌아온다. 소리와 시야를 읽으며 우회·문 닫기·은신·유한 폭죽을 선택한다. 원본 추적자 네 정체성은 긴 시야, 먼 소리, 오래 남는 수색, 멈춰 살피는 배회로 구별된다.

‘폐교의 기억’은 원래 학교의 세 층과 다섯 기억, 마네킹·가면·액자·지하의 등장 흐름을 사용하는 별도 장이다. 두 탐색의 중단 저장과 결과 기록은 서로 다른 파일에 남는다.

피드백 개발은 `04b4fd99`를 기반으로 시작했으며 최신 게임플레이·그래픽·촛불 위험 반응 소스는 GitHub `main`에 반영했다. [현재 구현·검증 기록](PLAYER_FEEDBACK_IMPLEMENTATION.md)에서 미완료 항목과 최신 증거를 확인한다. 원본 `Assets/Annex/SchoolAnnex.unity`를 재생성하거나 덮어쓰지 않고 런타임 구역·표현·사운드를 개선한다.

앞선 기전 피드백 단계의 실행 증거는 `../game/verification/development-feedback/`에 있다. 진행 중이던 자동 입력 수정까지 포함한 새 Windows 후보를 `Builds/FeedbackReview-20261006-wrap-24/`에 생성했다. 실제 입력 파일 2,126개의 현재 경로·해시와 크레딧 동봉을 확인했다. 이 폴더의 `HappyToyV2.exe`를 직접 실행해 플레이할 수 있다. 실행 파일과 `_Data`·DLL·`Audio-Credits.txt`·실제 입력 해시 `quality-build.json`을 같은 폴더에 둔다. 이전 `CorridorReview`·`PolishReview` 실행 파일은 이번 수정의 빌드 증거로 재사용하지 않는다.

- WASD/마우스: 이동·시선, Shift: 달리기, C/Ctrl: 낮은 자세
- E: 문·기억·캐비닛, F: 손전등, Q: 폭죽
- 우클릭을 누르면 조준, 놓으면 취소. Q로 실제 투척
- J: 회수한 기록, Esc: 일시정지. 안전한 상태에서 중단 저장 후 이어하기

캐비닛 전환은 실제 문 녹음 하나만 재생하고 옷자락·추가 충격을 섞지 않는다. 배회하는 추적자는 실제 신발 접촉 여러 녹음과 실제 바닥 재질에 맞는 발소리를 사용한다. 사운드 출처·변환·해시는 `ThirdParty/Audio/manifest.json`, 사용 크레딧은 빌드와 함께 제공하는 `Audio-Credits.txt`에 있다.

자세한 생존·저장 안내는 `PLAY.md`, 이번 변경과 검증 범위는 `CORRIDOR_FOCUS.md`에서 확인한다. 이전 #16 개발 중단·이어하기 미지원 안내는 현재 상태와 다르므로 `Verification/document-history/pre-corridor-focus-2026-10-06/`에 원문을 보존했다.

앞으로 변경·개발할 항목과 우선순위·완료 기준은 [DEVELOPMENT_ROADMAP.md](DEVELOPMENT_ROADMAP.md)에 정리했다. 최종 EditMode **55 / 55**, 통합 PlayMode **64 / 64**가 통과했고 원본 학교 씬 해시는 유지됐다. 새 Windows 빌드는 성공했고 입력 2,126개·크레딧을 확인했다. 최초 후보의 자연 시간 회랑 검사는 seed 73 문 대기 중 피격과 seed 211 상호작용 거리 밖 초점 실패로 중단됐다. 이 자동 입력 경로를 수정한 회귀 검사 **4 / 4**가 통과했다. 수정 후 두 시드와 학교 전체 경로 재실행은 남아 있다. 학교 검사는 사용자의 직접 플레이 요청으로 중단했으며 통과로 기록하지 않는다. 전체 경로·배포판 표면·기기 청감·목표 기기 성능 검증은 아직 남아 있다.

## 새 그래픽 업그레이드 — 별도 진행 중

추가 요청에 따라 8개 직접 제작한 3D 소품(녹은 촛불·배터리·종이 등불·제단·학교/회랑 캐비닛·문 손잡이·비구형 불꽃), 12개 2K PBR 세트와 사진 스타일의 생성 RGBA 불꽃, SSAO·Neutral/Bloom·SMAA·국소 반사·실제 그림자 atlas 예산을 적용했다. 원본 학교 씬·원본 적 형상/클립·물리/상호작용·조명 저장 기전은 보존한다. 네 적은 기존 v2 자산의 렌더링 개선이며 새 조각으로 설명하지 않는다.

현재 실행할 그래픽 후보는 [GraphicsReview-20261006-0806-fixed16](Builds/GraphicsReview-20261006-0806-fixed16/)의 `HappyToyV2.exe`다. 전체 현재 입력2,329개·graphics165개·Audio/Graphics 크레딧·원본 보호3개와 native 후 재해시를 통과했다. 새 visible20개 PNG·scope 검사와 개별 화면 검토는 **ACCEPTED_FOR_DECLARED_GRAPHICS_SCOPE**다. 파란 base의 작은 teardrop 불꽃, 실제 화장실·두 lit candle, 학교3층의 올바른 반사를 확인했다. 이전 native20의 **NEEDS_REPAIR**와 실패 기록은 보존한다.

기전 검사는 Editor 최초61/62 뒤 targeted4/4, 통합 PlayMode **72/73 + 별도 환경4/4 + native 수정6/6**이며 단일 전체73/73 실행으로 표시하지 않는다. 그림검토는 배우를 정지한 declared graphics scope다. 기존v2 적의 모든 손발 plant·live locomotion·전체 생존·기기 청감·사람의 공포·HDR monitor·목표 기기 GPU FPS는 이 화면으로 인증하지 않는다. 위의 Build24와 이전64/64는 이번 그래픽 변경의 통과 증거가 아니다. 출처·자산 경로·실제 증거와 남은 범위는 [GRAPHICS_UPGRADE.md](GRAPHICS_UPGRADE.md), 재질 크레딧은 [Graphics-Credits.txt](ThirdParty/Graphics/Graphics-Credits.txt)에 있다.

## 다른 PC에서 이어서 작업

[소스 인계·설치 안내](SOURCE_HANDOFF.md)를 따라 clone/pull·Git LFS 복원 후 Unity 6000.6.0f1에서 프로젝트를 연다. 모든 필수 편집 원본과 제작 도구를 소스와 함께 전달하며, 푸시 전 `Tools/quality/check_source_completeness.py`로 누락을 확인한다. 촛불 위험 반응과 마지막 Windows 후보의 기록은 [PLAYER_FEEDBACK_IMPLEMENTATION.md](PLAYER_FEEDBACK_IMPLEMENTATION.md)의 마지막 추가 요청 절에 있다.
