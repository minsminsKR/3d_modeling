# HappyToyV2 기능별 수정 지도

수정할 기능의 행에서 시작하고 해당 코드와 검증만 읽는다. 공유 규칙·저장·입력
경계에 영향을 주면 관련 행까지 범위를 넓힌다. 전체 리팩터링과 배포 준비는 전체
검사와 새 플레이어 빌드를 실행한다. 아래 경로는 Unity 프로젝트 기준이다.
경로가 없는 코드 파일명과 `UI/`·`AI/`·`Core/`는 `Assets/Scripts/` 아래를 뜻한다.

lady 피날레의 렌더링은 `GraphicsLightingPresentation`이 소유한 `LadyFinaleLighting` 범위에 둔다.
실제 손전등과 사용 중인 플레이어 향의 2.5m 국소 불빛·연기만 남기고, 다른 광원·환경광·반사·발광·불꽃을 끈다. 모드 종료 때 원래 조명 상태를 복원한다.
공유 재질·손전등 충전/입력·평소 입구 0.45배는 이 범위에서 변경하지 않는다.
`LadyEncounter.ChooseSpawn`은 완전한 NavMesh 경로 길이 18~72m인 회랑 후보를 균등 무작위로 고른다.
새 출현마다 맵 시드·전역 게임 RNG와 독립적인 난수를 쓰며, 저장 복원은 선택했던 위치를 유지한다.
`Core/LadyHeartbeatClock`은 32m 이상 58BPM에서 2m 이내 168BPM으로 부드럽게 변하는 박동 간격·시작 페이드·정지 규칙을 소유한다.
`LadyHeartbeatAudio`는 실제 청진 녹음의 짧은 두 접촉음을 고정 음높이로 반복하며 실시간 거리를 읽는다. 일시정지는 함께 멈추고 부적/아이템 휠에서는 플레이어 박동을 유지한다.
`LadyEncounter`의 실제 일반/캐비닛 잡힘 성공 결과만 박동 정지와 3초 병원 녹음 심정지음으로 전환한다. 결과 화면에서도 재생하고 재시작 시 소유 음원/소스를 해제한다.
레이디 자산 마감은 `SourceArt/Lady/polish_game.py` / `polish_far_lod.py`가 기존 생성 원본을
사용해 UV·게임 PBR·원거리 메시·수직 지지를 정리한다. 원래 76개 관절 회전 곡선은
그대로 보존한다. `LadyGameImport`는 FBX 전체 기준 포즈와 실제 움직이는 곡선을 유지하며
수치 잡음 scale/정적 회전 그룹만 기준 포즈에 둔다. `LadyMotion`의 동작 감소는 수직
지지 오프셋에도 18%를 적용한다. 자산/포즈만 변경할 때 `lady-polish`(Core·실제 FBX의
2,239프레임 동등성·3LOD/28관절·실제 발 지지·동작 감소, 무화면/무음)를 실행하고
새 Windows 빌드를 만든다. AI/전환/음향 변경은 기존 `lady`도 실행한다.
심정지음은 병원 녹음의 710Hz 기본음을 남기는 zero-phase 580–900Hz 대역 처리·12/90ms 가장자리 페이드로 거친 배음을 줄인다. 제작 원본·후보 비교·CC BY 4.0 고지와 재현은 `ThirdParty/Audio/LadyFinale/` 및 `SourceArt/Audio/render_lady_heartbeat.py`에 있다. `lady` 프로필은 실제 무음 믹서에서 원/근거리·일시정지·부적·마스터 무음·잡힘 결과를 검사한다.

| 기능 | 진입점 / 실제 책임 소유자 | 검사 프로필 |
|---|---|---|
| F1·F2 테스트 모드 | `GameSessionCheats`가 실제 키 입력·무적/발각 방지 상태를 소유한다. F2 ON은 `PlayerItemLoadout.Unlimited`를 통해 재고 0에서도 콩알탄·덫·향·부적을 사용하고 각 재고 소모와 `PlayerFlashlight` 배터리 소모를 생략한다. 실제 재고·배터리·저장 값은 유지하고 OFF 즉시 일반 소모로 돌아온다. 효과 수명·쿨타임·배치·입력 제한은 각 소유자가 유지한다. 아이템 휠과 HUD는 명시한 무한 표시 데이터를 받아 ∞를 표시한다. F2 ON은 `PlayerMotor.RestoreSprintForCheat`로 스태미나를 즉시 완충하고 고갈 잠금을 해제한다. `PlayerMotor.FixedUpdate`는 활성 중 스태미나를 1로 유지하고 실제 달리기에만 원래 runSpeed의 2배를 적용한다. 걷기·웅크리기·충돌·일시정지는 유지하며 OFF는 원래 속도/소모를 사용한다. | `cheats` (Core·실제 F1/F2·무적/발각·스태미나·속도·OFF·벽/경사 회귀) |
| lady · 마지막 기억 추격 | `Core/LadyFinaleClock`은 마지막 기억·0.9초 침묵 전환 규칙, `LadyEncounter`는 회랑 5기억 전환·실제 경로의 원거리 출현·저장/복원, `LadyFrozenActors`는 기존 적의 영구 포즈/이동 정지, `EnemyChaseFace`는 피날레 동안 표정의 잔여 보간 정지, `LadyFinaleAudio`는 쿵 한 번과 플레이어 발소리/숨소리의 listener 범위를, `LadyHeartbeatAudio`는 거리 기반 박동·확정 잡힘 심정지음을 소유한다. `LadyPursuit`는 실제 플레이어/은신처로 무조건 경로 탐색·문 통과·접촉, `LadyMotion`은 불규칙한 머리/사지 37.3초 액션과 일시정지·부적·동작 감소를 소유한다. `Core/LadySnapperFlinch`는 콩알탄 한 충돌마다 누적 없이 0.5초 정지를 소유하며 pause/부적에서는 시계를 보존한다. `LadyPursuit.ReactToSnapper`는 정면 2.5m·같은 높이·물리 가림 또는 직접 몸 접촉을 확인하고 이동/접촉 공격/실제 포즈를 함께 멈춘다. 덫 eligibility에는 포함하지 않는다. `LadyGameImport`는 실제 28관절·3LOD·PBR·포즈 외곽을 가져오며 원본 GLB/8K·AI 리그·편집 Blender·재현 도구는 `SourceArt/Lady/`에 보존한다. | `lady` (Core·저장 codec·실제 회랑 추격/부적/덫/정지·무음 믹서·손전등 on/off 실제 카메라·새 게임 조명 복원·시작 0.45/반사 회귀) + 새 Windows 빌드 |
| 시작 공간 밝기 선택 브라우저 | `EntranceBrightnessTests`의 명시 `HAPPYTOY_ENTRANCE_LIGHTING_REVIEW_DIR`가 `EntranceLightingReviewExport`를 호출한다. 실제 시작/대문/천장 카메라와 9개 입구 조명의 원래 기준 0~2배·0.05 간격 41단계를 무음 렌더하고 두 반사 프로브를 단계마다 다시 캡처한다. 2026-10-21부터는 천장 채움광이 없는 촛불·종이등 9개 리그를 기준으로 하며 과거 천장등 리그 미리보기를 재사용하지 않는다. 내보낼 때 적용 배율을 나누어 기준을 유지하며 manifest.defaultGain으로 현재 게임 값을 기록한다. 게임 소스에는 값을 적용하지 않는다. `Tools/lighting_review/index.html`은 휠·슬라이더로 해당 프레임과 정확한 수치를 표시하며 `serve.py`는 127.0.0.1에서만 제공한다. 출력은 `Builds/`에 두고 제작 원본·파라미터·해시를 manifest에 기록한다. | 해당 시작 카메라 검사에 export 경로를 명시한 단일 실행 + 브라우저 휠/시점 확인; 재현은 `Tools/lighting_review/README.md` |
| 회랑 음향·이동 선택·시작 밝기 피드백 | `CyclopseBreathingVoice`는 기존 컴포넌트/GUID/API를 유지하며 배회/추격 모두 같은 녹음 호흡을 쓰고 추격 시 기존 1.12배 속도·.66 음량으로 전환한다. 추격 비명과 해당 Resources/카탈로그 항목은 제거했다. `render_chase_voices.py`는 실제 개의 나무 바닥 발 접촉 녹음으로 `mask-beast-step`만 만든다. `CorridorMaskAudio.ContactCue`가 인게임·타이틀 접근의 공통 발소리 뱅크를 선택한다. `render_snappers.py`는 한 충돌·한 AI 관측에 0/75/150ms 세 틱을 담은 음원 3종을 만든다. `EnemyBellClock`은 종소리 배회 .50/추격 1.0, bell 원본 편집 peak .60을 사용한다. `PlayerMotor`는 휠을 열어도 이동/달리기/자세·물리를 유지하고 선택 중 마우스/오른쪽 스틱 시선·사용·상호작용을 차단한다. `GameShell`이 기존 .25배 시간을 소유한다. `CorridorEntranceRitualDressing.LightMultiplier`는 현재 불빛 리그에 .45배를 한 번 적용한다. 2026-10-21 요청으로 천장의 320→144 채움광과 출처 없는 암벽 광원을 제거하고 실제 사찰등 5개·촛불 광원 6개의 최대 7m 불빛으로 교체했다. 그림자 후보 2개·전역 노출·미로 조명은 유지한다. 원본/후보/해시/CC0·CC BY 4.0 출처는 `ThirdParty/Audio/BeastFootfalls`와 `SourceArt/Audio/chase-voice-selection.json`에 있다. | `corridor-feedback` (Core·실제 키/패드·무음 믹서·시작 카메라 비교), 다른 변경은 해당 프로필 |
| 회랑 백수귀 재접촉·감속 문 파괴 | `AI/WraithRetreatNavigation`은 추격 기억 만료·캐비닛 생존 뒤 마지막 목격 지점에서 18m 이상 떨어진 실제 경로 목적지를 고르고, `Core/WraithRetreatClock`의 24초 동안 이동·도착 후 대기를 소유한다. 이동 경로가 목격 지점으로 되돌아오는 후보는 낮게 평가한다. `LanternMaskEncounter`가 원래 시야를 계속 적용하며 재목격 시 이탈을 취소한다. 속도·출현 조건·학교 행동은 유지한다. `ChapterActorProgress`는 선택 이탈 목적지/잔여 시간을 보존하며 구형 저장은 0으로 복원한다. `InteractableDoorDestruction.BreakForMaskOnRoute`는 회랑의 실제 문 통과 경로·1.75m 접촉·가림을 확인한 경우 장애물 회피로 현재 속도가 0이어도 파괴한다. 일시정지·시간 정지·덫·열린 문·경로 밖 문은 기존 guard를 따른다. | `wraith-behavior` (Core·저장·회랑 이탈/문·학교/덫 회귀); 외형 변경 시 `wraith-hallway` |
| 향·부적·손 액션·아이템 휠 | `PlayerRitualInventory`는 향/조각/완성 부적 재고와 10초 연소 수명을, 독립 `Core/TalismanStockRules`, `TalismanBurnClock`, `ItemWheelRules`는 조합·시계·원형 선택을 소유한다. `IncenseGuidance`는 실제 NavMesh 경로 거리로 가까운 기억 하나를 골라 수집까지 유지하고 회수 완료 시 봉헌 제단을 향한다. `IncenseGuidance`는 향 끝을 따라가는 주황빛 point light(range 2.5m, intensity 1.5, 마지막 1초 감쇠)를 소유한다. `LadyFinaleLighting`은 그 실제 광원과 실제 안내 연기 렌더러만 예외로 두며 장식 향과 다른 불꽃은 계속 끈다. `IncenseSmokeTrail`은 향 끝에서 피어올라 이동하는 하나의 연속 메시와 흐르는 밀도·잔류 흔적을 소유하며 일시정지에서 함께 멈춘다. `RitualWorld`는 기존 기억 주변 아이템과 빈 자원 서랍 4곳·선반 6곳의 추가 향/조각을 연결한다. 새 ritual 저장 버전 2는 전체 물품 정체성을 검증하며 버전 1은 기존 소모 상태를 보존하고 새 가구 공급품을 도입한다. `CorridorDrawer.AddPickup`은 실제 트레이 이동과 닫힌 내용물 상호작용 차단을 소유한다. `RitualPickupPlacement`는 실제 지지면을 향한 하향 ray와 회전된 모델의 메시 바닥을 맞춰 향/부적 조각을 놓고, 상호작용 상자는 지지면 위에 둔다. `PlaceOnSupport`는 닫힌 책상 상판 대신 실제 서랍 바닥을 직접 검사한다. 원본 모델과 기존 아이템 정체성·저장 호환성은 유지한다. `AI/EnemyTimeStop`은 기존 AI의 정지 guard와 함께 원래 NavMesh/애니메이션/포즈/소리·등장 코루틴 시계를 보존·복원한다. `FirstPersonHands`는 해부학적으로 마감한 생성 손 리그·손끝/손바닥 접촉점 기반 그립·투척과 하단 모서리 별도 시점 렌더, 위→아래 연소·컬러 불꽃을 소유한다. 손전등은 감싸 쥐고 향/부적은 검지·엄지로 집으며 콩알탄/덫은 손바닥으로 지지한다. 세계 흑백은 소유 Volume이며 URP 자산을 변경하지 않는다. `PlayerItemLoadout`은 선택/사용, `PlayerItemWheel`은 Tab/RB hold와 선택 입력, `UI/ItemWheelOverlay`는 표시 데이터만 받는다. 시간/커서는 `GameShell`이 소유하며 `Core/ItemWheelRules.SlowTimeScale`의 0.25배를 적용하며 플레이어 이동도 같은 배율로 계속한다. `SourceArt/PlayerItems`에 GPU 0 GLB·입력·설정·리그·Blender·재현 도구가 있다. | `supplies-voices` (가구 공급), `ritual-items`, 향만 변경 시 `incense`, 바닥 배치만 변경 시 `ritual-pickups`; `core`; 무음 연기 시각 검토 `IncenseSmokePreview.Render`, 정지/동작 미리보기 `PlayerHandsPreview.Render` / `RenderActions`, GIF 조립 `Tools/quality/render_hand_action_gifs.py` |
| 검은 로딩 화면 | `SceneLoadingOverlay`가 장면 교체에도 유지되는 검은 커튼·비동기 scene load·완료 표시 수명을 소유한다. `UI/LoadingSealView`는 우측 하단 Loading..·퍼센트와 아래부터 채우는 벡터 연꽃만 표시한다. `Core/LoadingProgress`는 단조 진행·완료 전 99% 상한을 소유한다. 회랑은 `CorridorRun.BuildIncremental`의 실제 구조/가구/NavMesh/적/표현 완료 콜백, 장면 교체는 AsyncOperation 진행과 목적지 준비 완료로 갱신한다. `GameShell`은 입력과 재시작 gate를 유지한다. | `loading`, `ui`; 장면/챕터 회귀 포함 |
| 메뉴·일시정지·설정 | `Assets/Scripts/GameShell.cs`는 화면 전환·입력 허용을 조정한다. `Assets/Scripts/UI/ShellPauseScreen.cs`, `ShellSettingsScreen.cs`는 각 화면의 컨트롤을 소유한다. | `ui` |
| 기록·결과·페이지 이동 | `UI/ShellJournalScreen.cs`, `ShellRecordsScreen.cs`, `ShellResultScreen.cs`. 화면용 데이터와 Action을 받아 표시하며 게임 진행을 직접 변경하지 않는다. | `ui`, 저장 변경 시 `checkpoint` |
| HUD·메뉴 공통 스타일 | `UI/ShellHudView.cs`는 HUD 컨트롤과 하단 획득 안내를 소유한다. 안내는 좁은 고정 폭·내용 높이로 21:9 글자 폭 반올림에 의한 잘림을 막고 중앙 시야를 비운다. `UI/FlashlightGauge.cs`는 낡은 금속 손전등 PNG와 얇은 황동 잔량 레일을 표시한다. 아이콘 제작 원본·프롬프트는 `SourceArt/Hud`에 있다. `UI/ShellLayout.cs`는 공통 배치·스타일을 만든다. `GameShellView.cs`는 Unity 패널·입력·각 모듈 연결을 담당한다. | `ui`, 획득 안내만 변경 시 `CompactPickupFeedbackKeepsTheCentralViewClearAcrossScreenSizes` 및 `AuthoredCameraRendersWorldAimAndStopsAtPhysicalWall`, 손전등 표시만 변경 시 `flashlight-hud` |
| 타이틀·게임 시작 전환 | `GameShellTitleView.cs`, `CinematicTitleScene.cs`, `GameShellStartTransition.cs`. `CinematicTitleScene`은 타이틀 전용 백수귀 복제본의 거대한 크기·고정된 몸체/발 지지·느린 머리 클립 재생과 별도 카메라를 소유하며, `Resources/TitleAtmosphere.shader`는 어두운 실루엣과 강한 원래 눈 표면의 안광을 담당한다. 공유 원본 모델/재질/클립과 게임 백수귀 크기·AI는 유지한다. 타이틀·접근 장면은 60Hz 예약을 유지하고, 동작 감소 시 머리를 멈추며 정적인 타이틀은 저빈도 갱신한다. `UI/TitleApproachFootsteps.cs`는 인게임 `CorridorMaskAudio.ContactCue`의 현재 PCM으로 0.32초 간격 13접촉·4.25초의 소유 트랙을 미리 준비해 2D native 재생한다. 타이틀 listener pause에서는 DSP 예약 시계도 멈추므로 `PlayScheduled`에 의존하지 않고 ignore-pause 단일 트랙의 sample cursor로 관측한다. `GameShellStartTransition`은 접근 3.4초와 0.85초 암전을 먼저 마친 뒤, 완전한 검정 화면에서만 회랑 생성/베이크를 진행하고 준비 후 시야를 연다. 별도 카메라도 Unity 메인 스레드 생성을 격리하지 않는 점을 반영한 순서다. 발소리 뱅크는 인게임과 항상 함께 변경한다. 타이틀 일시정지만 우회하며 마스터 음량·진단 무음·종료 정리를 따른다. 실제 로딩 중 입력 차단과 접근 연출을 보존한다. | `finale-start-audio`, `title`, 발소리 공유 변경 시 `wraith-footsteps`, 공통 UI 변경 시 `ui` |
| 콩알탄 6발 묶음·충돌·유인 | `Core/SnapperStockRules.cs`는 묶음당 6발·최대 30발·구형 저장 변환을, `FirecrackerInventory.cs`는 입력과 재고를 소유한다. 기존 이름/GUID/enum은 저장 호환을 위해 유지한다. `LadySnapperTarget`은 Ignore Raycast layer 2의 query-only trigger capsule을 소유하며 `FirecrackerTrajectory`의 예측/실제 공통 step에서 더 가까운 벽과 Lady 몸 접촉을 선택한다. 플레이어 충돌/상호작용/덫 판정을 추가하지 않는다. `FirecrackerProjectile.cs`는 첫 고체/몸 충돌에서 세 틱 음원을 한 번 재생하고 `SnapperImpactEffect.cs`는 짧은 종이 먼지·작은 섬광을 표시한다. `StalkerBrain.HearSnapper`는 실제 소리 경로를 거쳐 위치를 기억하며 베이비도 숨은 플레이어 좌표 대신 그 위치를 수색한다. 기존 `NoiseInvestigationClock`은 도착 후 6초를 보장한다. 백수귀의 청각 무반응·목격 추격 우선은 유지한다. `SnapperVisuals`는 학교/회랑 공급품과 투척 낱알에 웹앱 모델을 붙인다. `Assets/Editor/SnapperAssetsImport.cs`, `SourceArt/Snappers/`, `SourceArt/Audio/render_snappers.py`, `ThirdParty/Audio/Snappers/`가 PBR 원본·Blender·참조·실제 CC0 녹음·재생성 경로다. | `snappers`, `core`, 레이디/충돌 변경 시 `lady-items`; 실제 플레이 검증은 사용자 요청 시 별도 |
| 몬스터 덫·5초 구속 | `MonsterTrapInventory`는 R/LB 입력·재고·설치 피드백을, `TrapPlacement`는 실제 NavMesh/물리 바닥·빈 설치 면적을 소유한다. `PlacedMonsterTrap`은 발밑/이동 구간 충돌·한 번 발동·성공 직후 모델 소비·해제/수명을, 독립 `Core/TrapHoldClock`은 일시정지 중 소비되지 않는 5초를 소유한다. `EnemyTrapRestraint`는 위치·회전·몸체 클립을 고정/복원하며 각 AI 소유자가 자신의 감각·공격·이동 시계를 정지한다. `EnemyChaseFace`는 구속 중 잡아죽일 때의 얼굴을 펼친다. `TrapWorld`는 학교/회랑 공급품과 선택 저장 필드·실제 바닥 복원을, `TrapVisuals`는 웹앱 기반 기계 턱/압력판과 `Consume`의 모델 비활성화를, `TrapAudio`는 실제 CC0 덫·스프링·금속 녹음 3종을 소유한다. 모델이 사라져도 덫 root·5초 구속·발동/해제 소리는 유지한다. `Assets/Editor/TrapAssetsImport`, `TrapAssetsPreview`, `SourceArt/Traps/`, `SourceArt/Audio/render_traps.py`, `ThirdParty/Audio/Traps/`에 원본·편집 Blender·프롬프트·GPU 0 생성 설정·재현 도구·라이선스가 있다. | `traps`, `core`; 플레이어 실행은 별도 요청 시 |
| 백수귀 외형 크기·문 파괴·잔해 | `BaeksugwiHallwayPresentation`은 게임 모델에만 별도 자식 피벗을 두고 32개 달리기/머리 포즈의 측정 외곽을 폭 2.52m·높이 2.36m로 맞춘다. `BaeksugwiGameLod`/`BaeksugwiGameLodImport`는 전체 손/팔 보호와 양방향 전체 정점·파생면 내부 거리 검사를 통과한 단일 게임 메시(target 400k/max 400k)와 원래 63개 bone 이름/바인딩을 검증하고 `MaskHorrorVisual`이 게임 복제본에만 적용한다. `SourceArt/BaeksugwiStudio/derive_game_lod.py`와 recipe/identity/report·편집 GameLOD Blender/FBX가 재현 원본이며 게임 전용 Resources `GameLodModel`/`GameLod` 경계와 원래 32포즈 보수적 bounds·offscreen false를 유지하며 실제 메시/눈 atlas·culling 검사와 동일 조건 실제 프레임 비교는 별도다. `MaskHorrorVisual`이 이 피벗을 연결하며 root/NavMesh 반경 .3m·높이 2.1m·공격 판정과 원본/타이틀 프리팹은 보존한다. `LanternMaskRunner.TickRunnerDoor`는 학교/회랑의 실제 경로를 따라 닫힌 문에 접근하고 `InteractableDoorDestruction`이 접촉·파괴/복원·문짝 물리를 소유한다. 열린 문은 그대로 통과한다. `DoorShatterDebris`는 지정 웹앱 GPU 0 모델의 갈라진 목재·문틀·작은 파편을 흩뜨린 뒤 바닥 잔해로 합쳐 보존하며 충돌·NavMesh는 만들지 않는다. `DoorDestructionAudio`가 기존 실제 CC0 목재/금속 파괴 녹음을 문 위치에서 한 번 재생한다. 학교의 선택 broken/locked 저장 필드와 기존 회랑 broken 필드는 잔해를 무음 복원한다. `BaeksugwiHallwayAuthoring.Apply`, `SourceArt/BaeksugwiStudio/hallway-presentation.json`이 측정/재현 원본이다. | `wraith-hallway`, `core`; 수동 플레이어 실행은 별도 요청 시 |
| 문 파괴 연기·먼지 | `DoorShatterDebris.Launch`가 실제 파괴 순간에만 `DoorBreakDust`를 생성한다. 문 높이의 목재 가루와 .85초 뒤 바닥 착지 먼지를 방출하며 3.8초 안에 사라진다. `Resources/DoorBreakDust.shader`가 부드러운 깊이 경계·절차적 구름·주변 조명을 처리한다. 수동 파티클 시뮬레이션은 게임 입력이 허용될 때만 전진해 일시정지에서 고정되며, 사망/리셋 시 정리된다. 저장 복원은 잔해를 Seal만 하므로 먼지를 재생하지 않는다. `DoorBreakDustPreview.Render`는 실제 효과와 파편을 무음 렌더링한다. | `door-dust`, `core`; 플레이어 실행은 별도 요청 시 |
| 적의 시야·청각 | `StalkerBrain.cs`와 `AI/StalkerPerception.cs`가 실제 가림·층·소리 경로를 처리한다. 회랑 백수귀는 `LanternMaskEncounter.cs` / `LanternMaskRunner.cs`의 정면 시야만 사용하며 발소리·폭죽 콜백을 거부한다. | `core`, `ai`, `corridor` |
| 공격·캐비닛 은신 | `AI/StalkerAttack.cs`는 일반 공격 예고/회복과 표적을 소유한다. `Core/CabinetEncounterClock.cs`는 목격한 진입의 접근→두리번거림→후퇴 또는 발각 돌진→문 앞 예고를 판정한다. `AI/EnemyCabinetEncounter.cs`가 실제 NavMesh·문·현재 가림을 연결하며 Stalker/백수귀/마네킹이 공유한다. 실제 캐비닛 치명타 뒤에는 `CabinetDeathSequence`가 게임 진행을 고정한 채 열린 입·캐비닛 정면 접근·플레이어 시점을 1.65초 표시하며, `GameSession`/`GameShell`의 완료 콜백이 결과 화면을 연다. 명시적 일시정지와 동작 감소를 따른다. `PlayerMotor.Hide`는 콜라이더를 숨기기 전에 모든 관찰자에게 알린다. `Core/CabinetTauntMemory`가 동일 캐비닛·몬스터의 4m 이내 실제 목격 진입을 세며 세 번째에는 확률을 덮어쓰고 바로 돌진한다. `AI/PlayerCabinetMemory`는 실제 관찰자와 돌진 중 재출입 금지, 12m 이탈 시 초기화, 선택 체크포인트 기록을 연결한다. 일반 진입의 75%/25% 한 번 판정과 구형 저장 호환은 유지한다. | `core`, `cabinet-expression`, `ai`, 저장 변경 시 `checkpoint` |
| 적의 문 통과·추적 경로 | `AI/StalkerDoorPassage.cs`는 문 통과와 제한된 대기를 소유한다. `AI/CorridorRouteGuide.cs`는 지도 4의 타일 중앙 경유와 회전을, 독립 `Core/CorridorRouteRules.cs`는 연결된 다음 타일을 선택한다. `EnemyNavigation.FollowPath`는 원래 최종 목표·경로 증거를 보존하며 실제 이동을 연결한다. | `ai-doors`, `corridor` |
| 적의 순찰 벽 막힘 복구 | `Core/PatrolProgressClock`은 실제 위치 기준 3초 동안 20cm 미만 변위의 지속 이동을 판정한다. `AI/StalkerPatrolProgress`는 원래 목표·실제 NavMesh 이동·몸체 변위를 관찰하고, `StalkerBrain`만 다음 순찰 인덱스·문 요청 해제·새 경로 선택을 소유한다. 운냥이·사이클롭스·베이비의 공통 순찰에 적용하며 문 앞 의도적 정지·양보·도착·베이비 소리 대기는 관찰을 초기화한다. 추격/수색/조사 목표와 기존 저장 필드·원본 순찰 마커는 유지한다. | `patrol-navigation` (Core·실제 벽/몸체 막힘 뒤 이탈·양방향 모퉁이 56건·일반/두 문 통과·순찰 대기/일시정지·부적), 기존 문 정책 변경은 `ai-doors` |
| 몬스터 간 통행·양보 | `AI/EnemyPassageTraffic.cs`가 실제 NavMesh 위의 짧은 양보 이동과 경로 복귀를 소유한다. `Core/MonsterPassingRules.cs`는 통행 우선권을 한쪽에만 준다. 기존 AI의 목표·저장 위치는 바꾸지 않는다. | `traffic-loading`, 문/공통 경로 변경 시 `ai-doors` |
| 열린 문 다시 닫기 | `DoorInteractionTarget.cs`의 물리·NavMesh 제외 문틀 쿼리를 `PlayerMotor.FindFocus`가 실제 가림과 함께 판정한다. `Interactable.cs`는 문짝 이동·점유·소리의 기존 소유자다. | `corridor`, `ai-doors`, `ui` |
| 큰 몬스터의 벽 겹침·걷기/추격 동작 | `AI/EnemyBodyClearance.cs`는 렌더링 몸체 크기의 트리거 쿼리(상호작용·NavMesh 베이크 제외)로 실제 NavMesh 이동을 보정한다. `AI/EnemyLocomotionPose.cs`는 원본 리그·클립 위에 복원 가능한 팔다리 동작을 더한다. `V1MonsterMotion.cs`가 이동 속도·상태·공격·일시정지를 연결한다. | `readability`, 문/경로 변경 시 `ai-doors` |
| 사이클롭스·운캣·베이비 추격 표정 | `EnemyChaseFace.cs`는 각 actor의 새 얼굴 리소스를 실제 Chase/캐비닛 발각 돌진 중 섞고 수색·배회·비활성화 시 원래 재질로 복원한다. `CyclopseChaseFace`는 기존 컴포넌트 API/GUID를 유지한다. 사이클롭스·운캣의 선택적인 `CabinetFaceData`/`EnemyCabinetFace`가 캐비닛 발각 돌진 중 턱·눈꺼풀·볼을 추가 변형하고 머리 뼈에 붙인 개별 크기·위치·간격·기울기·파손을 지정한 실제 이빨 22개·잇몸·입 안·혀와 보완 입술 면을 펼친다. 닫힘/열림 메시 쌍으로 윗턱은 얕게 유지하고 아래턱·턱끝은 크게 내려가며 한쪽으로 틀어지게 한다. 이빨을 세로로 확대하지 않는다. 일반 추격은 기존 표정이며 탈출 시 닫힘·일시정지·사망 유지·비활성 복원을 소유한다. 원래 사이클롭스 외눈·운캣 두 눈과 안광 소유권은 유지한다. 운캣의 기존 추격용 입 안 덮개는 캐비닛 전용 입이 켜진 동안 숨긴다. `UncatCabinetFaceImport`는 운캣의 원본 입꼬리 두 점과 머리 축에서 기울기·깊이를 구하고 사진 UV와 턱 좌표를 분리해 원본 볼로 연결하며, 별도 치열/턱 프로필을 적용하고 `SourceArt/UncatCabinetFace`에 원본을 보관한다. `CyclopseCabinetFaceImport`와 `SourceArt/CyclopseCabinetFace`가 프로필·생성 피부·Blender 원본·재현 도구를 보관한다. `V1MonsterMotion`이 기존 studio skin에 연결하며 모델·클립·지지는 유지한다. 기존 `CyclopseExpressionData`/lit shader를 공유한다. `MonsterRedEyes.BindExpressionMaterials`가 교체된 얼굴 재질에도 원래 안광 마스크·밝기·토글을 적용하고 shader는 얼굴 영역 발광을 유지한다. `Assets/Editor/BabyEyeMaskImport.Apply`는 베이비의 실제 game UV 삼각형 전체에서 홍채·동공 마스크를 재생성하며 SourceArt/CreatureStudio/Baby/eyes-surface-*에 설정·결과·해시를 보관한다. `Assets/Editor/CyclopseExpressionImport.Apply`와 `CreatureFaceImport.Apply`가 `SourceArt/CyclopseExpression/`, `SourceArt/ChaseFaces/`의 원본·정확한 입력·프롬프트·프로필에서 리소스를 재생성한다. `CreatureFaceAuthoring`은 원본 정면/적용 결과를 검토한다. | `uncat-cabinet-face`, `cyclopse-cabinet-face`, `cabinet-expression`, `readability`, 새 플레이어의 각 눈 발광 on/off 비교·캐비닛 진단 |
| 획득 아이템 테두리·촛불 위기 소등 | `PickupOutline.cs`와 `Resources/PickupOutline.shader`는 실제 획득 가능/표시 상태와 깊이 검사를 따른다. `CandleDanger.cs`는 가까운 몸체와 공격을 판정하고 `WaymarkCandle.cs`는 점화 기억을 보존해 자동 복원한다. | `readability`, 저장 변경 시 `checkpoint` |
| 베이비·적별 감각 | `StalkerCorridorBaby.cs`, `CorridorBabyBehaviour.cs`, `Core/CorridorBabyMemory.cs`, `Core/CorridorThreatRules.cs`. 운캣과 학교 화캣의 정체성은 별도다. | `core`, `ai` |
| 서랍·가구 상호작용 | `CorridorDrawer.cs`, `CorridorFurniturePlacement.cs`, `CorridorFurnitureLibrary.cs`, `PlayerMotor.cs`. 움직이는 서랍과 플레이어 충돌·상호작용 거리를 확인한다. | `drawer` |
| 기억 단상 표시·정렬 | `HauntedCorridorPresentation.DressShrines`는 실제 `Memory altar` 충돌 단상의 위치를 기준으로 모델을 놓는다. 기억 위치·통로·저장 좌표는 유지한다. | `shrines`, 새 플레이어 표면 확인 |
| 회랑 생성·방·제단 | `CorridorLayout.cs` / `CorridorRun.cs`의 새 지도 버전은 5다. `Core/CorridorShelterRules.cs`는 한 문짜리 막다른 방과 우회로 위의 두 문짜리 방을 선택한다. `CorridorShelterRoom.cs`는 각 방의 실제 문 목록을 연결한다. 기존 지도 1~4는 같은 버전으로 재구성한다. | 방·출구 선택/저장 변경은 `shelter`, 일반 회랑은 `corridor`, 전체 연결망 변경 시 `full` |
| 동굴 시작 공간·봉인된 사찰 대문 | `CorridorEntranceChamber`는 지도 5의 큰 동굴 방·바닥/벽/천장·cell 0 연결 통로·시작 위치·봉인 대문 상호작용을 소유한다. 기존 23개 바위의 가장 낮은 LOD 정적 충돌과 대문의 실제 모델/닫힌 문 몸체를 유지한다. `CorridorEntrancePresentation`은 조립만 담당하고 `CorridorEntranceModels`는 FBX와 키가 겹치지 않는 `<key>-visual` prefab의 공유 메시·외부 재질·PBR을 직접 연결한다. `CorridorEntranceCavern`은 기존 암벽/천장과 물리 바닥 위 최대 2.5cm의 생성 바닥 LOD를, `CorridorEntranceRitualDressing`은 생성 수호상·촛불/생성 육각 사찰등·부적줄·젖은 표면·바닥 문양과 구역별 고정 배치를 소유한다. `EntranceFirelight`는 실제 등/촛불 위치의 11개 광원과 약한 변동·일시정지·동작 감소를 소유한다. `EntranceFirelightEnvironment`는 입구 세계 카메라·반사 캡처 동안만 환경 채움광을 0으로 하고 렌더 직후 원복한다. `BuddhistLanternVisual`은 입구와 일반 회랑의 공통 생성 사찰등 시각 경계다. 사찰등 5개와 일반 회랑 천장 등은 `buddhist-lantern-visual`의 공유 PBR/2LOD와 UV의 佛 먹글씨를 사용한다. 회랑에서는 높이 .58m로 기존 광원 중앙에 맞춰 천장 아래 여유와 기존 `ThreatFixtureFlicker` 점멸을 유지한다. 원본 948,015면은 Blender MCP에서 19,999/11,999면·2K로 마감하고 `rebake_lantern_atlas.py`가 감면 후 원본 색/ORM을 재베이크한다. 실제 시작/근접 카메라·공유 LOD/재질 수명을 검사한다. 대문 앞 ±3.8m의 봉헌 사찰등 2개와 촛불 군집은 문짝·기둥을 드러내며 실제 spawn/접근 카메라의 문짝 밝기·기여와 기존 E/충돌을 `entrance-lighting`으로 검사한다. 수호상 앞 ±9.8m의 봉헌 촛불 두 무더기는 5.8m 범위에서 얼굴을 아래로부터 밝히며 추가 그림자 후보를 만들지 않는다. 천장/암벽 채움광은 없다. `GraphicsLightingPresentation`이 이 범위를 소유·해제하고 피날레 전 해제한다. `CorridorEntranceVapour`는 2개의 유한 향 연기만 20Hz로 갱신하고 일시정지에서 멈춘다. 회색 선으로 보이던 바닥 안개 판은 만들지 않는다. 각 소유자는 자신의 런타임 메시/재질만 해제하며 장식은 충돌·아이템·상호작용을 추가하지 않는다. `GraphicsLightingPresentation`은 동굴 Bounds 안에서 실제 동굴 크기의 반사 box를 사용하고 기존 완료 확인/3초 간격·모드 복원 경계를 유지한다. 그림자 후보 광원은 2개이며 기존 전체 그림자 예산을 따른다. `CorridorRun`이 NavMesh 베이크 전에 연결하고, 봉인 대문 안내 `문은 굳게 닫혀있다`·중앙 통로·저장 형식·지도 1~4는 유지한다. 원본/입력/웹앱 기록·Blender·재현 도구·참조는 `SourceArt/CorridorEntrance/`, 게임 자산은 `Resources/CorridorEntrance/`다. | `entrance`, 입구/회랑 사찰등·수호상 불빛 변경 시 `temple-lanterns`, 국소 밝기 배율 변경 시 `entrance-lighting`, 반사/공유 조명 변경 시 `graphics`, 좌표/저장 변경 시 `checkpoint`, 기존 회랑 경로 변경 시 `corridor` |
| 운냥이 배회·추격 종소리 | `UncatBellVoice.cs`는 실제 움직임·추격 상태와 녹음 기반 공간 음원을 연결한다. 방울만 선형3D min3/max48m로 원거리 접근을 들려주고 기존 벽/층 차폐·방향·pause/mute를 따른다. `Core/EnemyBellClock.cs`는 배회/추격 간격과 음량을 판정한다. 제작 원본·비교는 `SourceArt/Audio/render_uncat_bell.py`와 `uncat-bell-selection.json`, 거리 피드백/재현은 `SourceArt/Audio/UncatBellDistance/README.md`에 있다. | `uncat-bell-distance` (Core·실제 걷기/추격·무음 생산 믹서의3/24/36/50m·좌우 방향·물리 벽·pause/master0), 다른 회랑 변경은 `corridor` |
| 사이클롭스 거친 호흡 | `CyclopseBreathingVoice`는 가슴 위치의 독립 3D 녹음 루프·배회/추격 음량과 속도·정지/종료/소유 자원 정리를 소유한다. `StalkerBrain.Awake`가 해당 정체성에만 연결하며 AI 감각/이동 규칙은 바꾸지 않는다. `EnemyAcoustics`의 벽·층·거리 차폐와 공통 일시정지/음소거·부적 시간 정지를 따른다. 원본 CC0 사람 호흡·후보 비교·재현 도구는 `ThirdParty/Audio/RecordedHorror`, `SourceArt/Audio/render_cyclopse_breath.py`, `cyclopse-breath-selection.json`에 있다. | `cyclopse-voice` (Core·추격/배회 호흡·비명 제거·무음 실제 믹서), `supplies-voices` |
| 한국적인 소품 | `KoreanCorridorDressing.cs`는 위치 데이터만 받아 중앙 경로·가구·문 이동을 피한다. `KoreanCorridorProp.cs`는 자산/구역 정체성을 제공한다. `SourceArt/KoreanCorridorProps/`에는 웹앱 GLB/PBR·입력·프롬프트·Blender·LOD 재생성 도구가 있다. | `corridor`, `readability`, `full`, 새 플레이어 표면 확인 |
| 학교 진행 디지털 시계 | `Core/SchoolProgressClock`은 전진 통과당 +1시간과 누적 표기를, `SchoolLoopWorld`는 기존 연결 결과 전달과 세 구역 동기화를, `SchoolProgressClockDisplay`는 생성 원본/공유 PBR·LOD와 변경 시 갱신되는 7세그먼트 표시를 소유한다. 새 시작 00:00, 역방향 유지, pause 중 무증가다. `SchoolLoopCheckpoint`는 선택적 `loop.clock.hours`를 보존하고 구형 지도3 저장은 00:00으로 복원한다. 입구 맞은편 동쪽 벽에 설치하며 추가 광원·통행 충돌이 없다. 원본/프롬프트/작업/packed Blender/재현 도구는 `SourceArt/SchoolProgressClock`이다. | `school-clock` (Core·실제 W 이동/역방향/pause·저장 이행·원본 PBR/LOD·정상 카메라 LED 가독성·해제); 이상현상/실패/8회 탈출은 후속 범위 |
| 학교 관찰 루프·음악실 | 현재 메뉴/제단 뒤 학교는 지도 버전 3이다. `Core/SchoolLoopPlan`은 치수·세 방·양 끝 연결 평면을, `SchoolLoopWorld`는 구역별 조립/배칭·물리/NavMesh·시선/자세를 보존하는 연결 이동을 소유한다. `SchoolLoopArchitecture`는 늘 열린 철문과 잠긴 교실/닫힌 창문·국소 형광등을, `SchoolMusicRoomDressing`은 생성 피아노/악보대·공유 좌석·음악실 마감을 담당한다. `LegacySchoolRetirement`가 이 모드에서만 구형 지형/조명/충돌/배우를 제외한다. `MemoryChapter.Loop`은 기존 학교 facade와 새 지도 사이의 명시적 경계다. 현재 시작은 `BeginSchoolLoop`/`CreateSchoolLoop`; 기존 `BeginChapter`/`CreateChapter`와 `SchoolCampus*`/`SchoolFirstAppearances`는 구형 버전 1/2 저장 및 기존 회귀를 보존한다. 새 지도에 구형 수집/계단/추격을 적용하지 않는다. `SchoolLoopCheckpoint`는 버전 3의 위치·연결 횟수·조명/아이템 저장을 검사한다. 원본·사용자 평면도·Blender/PBR/LOD·재현 도구는 `SourceArt/SchoolLoop`에 있다. | `school-loop`; 학교 전체 교체/저장 경계는 `full` 두 모드와 새 Windows 빌드. 이상현상/8회 탈출 규칙은 별도 후속 범위 |
| 학교 반복 보행 인물 | `Core/SchoolWalkerRoute`는 한 차례 보행·양보·진행 거리/회전·대기와 다음 통과 요청을 소유한다. `SchoolWalkerEncounter`는 중앙 구역의 인물 하나와 실제 카메라/벽 가림을, `SchoolWalkerMotion`은 실제 이동 거리에 맞춘 생성 리그의 walk/idle 샘플링을 소유한다. `SchoolLoopWorld`가 정적 배칭 뒤 한 번 조립하고 실제 양방향 연결 통과를 전달한다. `SchoolWalkerCheckpoint`와 `SchoolLoopCheckpoint`가 선택 `loop.walker` 상태를 저장하며 지도 버전 3을 유지한다. `Editor/SchoolWalkerImport`와 `SourceArt/SchoolWalker`가 지정 서비스 원본·리그/동작·Blender·PBR·3LOD를 소유한다. | `school-walker` (Core·실제 한 인물/걷기/양보·양방향 숨은 재등장·pause/부적·구형 저장/현재 보행 복원·공유 자원/해제·정상 카메라·기존 JSON codec); 새 Windows 빌드 |
| 저장·재개·게임 진행 | `GameSession.cs`, `CorridorRunCheckpoint.cs`, `ChapterRunCheckpoint.cs`, 각 `*CheckpointStore.cs` / `*Checkpoint.cs`. `CorridorCheckpointCodec.cs` / `ChapterCheckpointCodec.cs`가 검증된 JSON·복사 경계를 소유하며 `CheckpointJsonFields`는 root의 선택 `traps` 필드 누락/null을 보존한다. JsonUtility가 구형 누락 필드를 빈 inline 객체로 만드는 경우도 빈 덫 데이터로 오인하지 않으며, 실제 존재하는 잘못된 덫 데이터는 계속 거부한다. 저장소는 이 codec을 거쳐 복사/직렬화/역직렬화한다. 체크포인트 partial은 기존 저장 형식을 연결하는 어댑터다. | `core`, `checkpoint`; 덫 codec 변경 시 학교/회랑 중단 복원·덫 저장 회귀 |
| 폐교 노후화·젖은 바닥·학교 전용 조명 | `SchoolHorrorSurfaces`는 확보한 촬영 스캔 파생 2K PBR와 학교 전용 URP 표면/유리를 소유한다. `SchoolLoopArchitecture`는 실제 마감판 개구부와 별도 구조 천장을, `SchoolStructuralDecay`는 두꺼운 박리·파손 가장자리·노출 천장틀과 스캔 잔해를 담당한다. `SchoolDecayDressing`은 기존 저작 모델의 정독반/매점·복도 수납과 배관/종이를 묶는다. `SchoolHorrorLighting`은 학교 카메라 범위의 안개·색보정·부분 형광등·차가운 창문광·128px 초기 반사 프로브 2개를 담당한다. `SchoolPlanarReflection`은 최대640×360 HDR의 실제 반사 카메라·사선 바닥 클리핑·재귀 차단과 카메라 범위 GL/셰이더 상태 복원을 소유하며 젖은 바닥의 실제 반사를 만든다. `Core/SchoolFluorescentClock`은 완만한 전압 저하와 pause/동작 감소를 소유한다. `SchoolLoopWorld`가 조립하고 소유 자원을 해제한다. 지도3·닫힌 교실·이동/저장과 기존 생성 음악실을 유지한다. 촬영 스캔 원본·CC0·Blender 제작 파일·재현/해시·한계는 `SourceArt/SchoolHorror`에 있다. | `school-horror` (Core·실제 이동/저장/아이템·평면 반사 on/off 화면 기여·자산 해제·pause·렌더 상태 원복·실제 URP 카메라) + 새 Windows 빌드; 공통 파이프라인 변경 시 `graphics`/`full` |
| 그래픽 설정·그림자 예산·성능 비교 | `Assets/Editor/GraphicsLightingSetup.cs`가 소유 URP 자산의 HDR·MSAA4·SMAA·깊이 재구성 SSAO를 재생성한다. `GraphicsLightingPresentation.cs`는 모드별 조명/반사를 복원한다. `LocalShadowBudget.cs`는 매 프레임 실제 광원에서 `ShadowAtlasPlan.Rebuild`의 재사용 목록을 채우며 동일 거리/ID 우선순위로 그림자를 선택한다. `GraphicsOptimizationProbe.cs`는 명시된 인자로만 정적 카메라의 GPU 완료 대기 렌더링 시간을 측정한다. | `graphics`, 공통 렌더링 변경/배포는 `full` 및 새 플레이어 표면·안광 검사 |
| 회랑 진행 중 프레임·장시간 관찰 | 명시적 `NativeCorridorPlayAudit` 성능 모드는 `CorridorPerformanceSampling`으로 실제 주 카메라 콜백 간격·지원 CPU/GPU/메모리 counter를 제한된 버퍼에 기록한다. PNG·추가 렌더·readback·PCM은 생략한다. `NativeCorridorInputRoute`의 선택 patrol은 네 번째 기억 뒤 실제 적 4종 활성부터 기존 입력 이동을 순환한다. `NativeAuditMovement`는 선택한 준비 이동의 긴 안전 직선만 가속하고 원래 walk/run 값을 복원한다. patrol·문·상호작용·위험 주변은 정속이며 가속/전환 프레임은 정속 성능 근거에서 제외한다. 일반 실행·AI·저장·시간 배율은 유지한다. | 새 네이티브 플레이어의 동일 seed/API/해상도/설정 경로 비교; 경로 변경 시 `corridor`, 공유 규칙 변경 시 `core` |
| 소리·자산 | `ExternalAudio.cs`, `EnemyAcoustics.cs`, `EnemyStudioVisual.cs`와 각 자산의 SourceArt/ThirdParty 안내. | 해당 기능 검사 및 기존 무음 네이티브 진단 |

`RoomAmbience.ConfigureRunPositions`는 위치와 회랑/학교 모드를 명시적으로 받는다.
학교의 `ChapterAtmosphere`와 `SchoolCampusRooms`는 학교 모드를 전달한다. 위치
배열이 있다는 이유만으로 학교 음원을 회랑 음원으로 교체하지 않는다.

학교 연결 통로의 마감은 `SchoolDecayDressing.ConnectorHistory`와
`SchoolStructuralDecay.ConnectorDamage`가 새 생성 집기·콘크리트/돌과 기존 종이·물막을
배치한다. `SchoolLoopArchitecture`는 새 통로의 실제 천장 개구부와 철문 보에서
분리된 형광등 위치를 소유한다. `SchoolExitSign`은 기존 생성 케이스/공유 LOD·PBR을
재사용한 천장 중앙 양면 녹색 비상구 표시와 국소 불빛만 소유한다. 진행 출구나
새 상호작용을 추가하지 않는다. `school-connectors`는 기존 `school-turnbacks`와
유도등·형광등/보 간섭·통로/NavMesh·폐교 장식·실제 게임 카메라 네 구도를 검사한다.
원본 사용자 참조와 자산 재사용 내역은 `SourceArt/SchoolHorror/ConnectorPresentation`이다.

## 모듈 경계

학교 새 모델 키트는 `SchoolRenewalModels`가 생성 자원 경계를,
`SchoolRenewalArchitecture`가 기존 물리 벽/바닥에 맞춘 시각 마감과 원본 벽의
창턱/상부 절단을 소유한다. `Editor/SchoolRenewalImport`는 18종의 PBR/노멀·2LOD를
재구성하고 빌드 완전성을 검사한다. 집기 배치는 기존 두 Dressing 모듈,
잔해는 `SchoolStructuralDecay`, 두 LOD의 튜브 발광은 `SchoolFluorescent`가 맡는다.
`SchoolHorrorSurfaces.RenewedFloor`는 새 UV 아틀라스와 기존 월드 수분/평면 반사를
결합한다. 충돌·저장·루프 평면·광원 출력/노출은 유지한다. 원본 GLB/PBR·입력·생성
작업·packed Blender·재현 도구/라이선스는 `SourceArt/SchoolRenewal`에 있다.
`SchoolRadiatorDressing`은 기존 평면 난방기 덮개를 생성 주철 라디에이터로 교체하고,
기존 통행 폭과 벽 연결을 유지한다. `SchoolRoomSign`은 벽에 수직인 생성 법랑 간판과
정확한 한글 양면 이름을 소유한다. 매점·정독반·음악실은 양방향에서 읽히며 문 ID,
충돌·NavMesh·광원을 추가하지 않는다. 두 모델은 공용 PBR·2LOD 자원을 재사용한다.
`school-renewal`은 Core·18종 실제 배치·PBR·좌석 방향·튜브 LOD/pause·기존 이동/닫힌
교실·젖은 반사·시계/자바라/연결부와 정상 카메라 검사를 묶는다. 새 Windows 빌드와
`validate_kit.py`/스테이징 후 소스 완전성 확인을 함께 수행한다.
두 소품만 수정할 때는 `school-wayfinding`으로 Core·학교 루프/저장 규칙, 라디에이터
실제 배치/정상 카메라, 양면 한글/방향별 정상 카메라, 실제 왕복 루프 이동·양쪽
시임 화면·자바라 통행·승인 밝기를 검사한다. 선택 이미지 출력은 Builds 아래의
`HAPPYTOY_SCHOOL_RADIATOR_REVIEW_DIR`와 `HAPPYTOY_SCHOOL_ROOM_SIGN_REVIEW_DIR`다.

학교 보행 인물은 세 개의 반복 건축물에 복제하지 않고 중앙 구역에 하나만 둔다.
`Core/SchoolWalkerRoute`는 `(0.65, 33)`에서 남쪽 `(0.65, 2.2)`로 걸은 뒤
입구의 `(-3, 1)`로 꺾여 이동하며, 플레이어와 가까우면 열린 쪽으로 양보한다.
일반적인 뒤돌아보기로 재등장하지 않는다. 양쪽 실제 전환 평면을 지난 경우에만
`SchoolWalkerEncounter.OnTraversal`이 다음 통과를 예약하며, 현재 몸 전체와 새 시작
위치가 실제 벽에 가려진 뒤 같은 인물을 재배치한다. 화면 밖이라는 이유만으로
재배치하지 않으며 젖은 바닥의 반사 카메라와 공유되는 `Renderer.isVisible`에 의존하지
않는다. 본인만의 텔레포트나 적 AI·공격·플레이어를 미는 충돌체는 추가하지 않는다.
`SchoolWalkerMotion`은 자동 Animator 시계 대신 진행 거리와 저장된 대기 시간을 사용해
walk/idle을 혼합하고, 일시정지·부적의 시간 정지에서 실제 뼈 자세까지 고정한다.
`loop.walker`는 경로·통과/예약 번호·동작 위상을 보존한다. 구형 v3 JSON의 누락/null은
`ChapterCheckpointCodec`의 중첩 필드 경계에서 보존해 새 보행의 먼 시작점으로 복원하며,
시작점 가까이에 저장된 플레이어는 반대 보행 차선으로 피한다. 실제 존재하는 잘못된
상태·미래 통과 번호는 계속 거부한다. 시계의 전진 전용 규칙·지도·문 ID는 유지한다.
`school-walker`는 보행/저장과 기존 통과·시계·밝기·소유 자원 및 공용 JSON codec 회귀를
집중 검사한다. `HAPPYTOY_SCHOOL_WALKER_REVIEW_DIR`를 Builds 아래에 명시하면 실제
정상 카메라의 일반 조명/손전등 화면을 저장한다. 제작·검토·남은 한계는
`SourceArt/SchoolWalker`의 인계 문서를 따른다.

몬스터 자산 마감은 `SourceArt/CreatureStudio/tools/polish_game_assets.py`와
`polish-settings.json`이 기존 근접 메시·UV·가중치·원본 동작을 보존하며 게임 전용
비금속 PBR과 보수적인 원거리 복사본을 만든다. `EnemyStudioGameImport.PrimarySkin`은
지지·눈·표정 제작 도구의 근접 스킨 기준을 소유한다. `StaticScaleCurveCleanup`은
모든 관련 액션에서 같은 scale 곡선만 기준 포즈에 둔다. `MonsterRedEyes`는 같은
UV의 원거리 스킨에도 기존 발광 토글을 연결하고 `EnemyChaseFace`는 표정이 켜진 동안
근접 LOD를 유지한다. `V1MonsterMotion`과 캐비닛 사망 구도는 primary skin으로
해부학·접촉·표정 기준을 유지한다. `model-polish`는 기존 근접 정점 대응과 전체
FBX 포즈/LOD, 실제 재생·지면 접촉·전체 LOD 눈 발광·재질 소유권 복원을 검사한다.
공유 시각 연결 변경 및 푸시 전에는 `full` 양쪽 스위트와
새 Windows 빌드·소스 해시 검증을 실행한다. 읽기 전용 전체 모델 목록 도구는
`Tools/quality/model_inventory_unity.csx`다.

회랑 두 시드 전체 생존 검사와 네이티브 감사는 `NativeCorridorInputRoute`의 같은
실제 입력 경로를 사용한다. 별도 테스트 전용 탐색기는 두지 않는다. 동굴 시작에서
배터리·촛불까지 실제 최단 접근 경로를 고르며, 조준 중 공격은 관측한 경고로
피하고 필요하면 현재 자세의 접근점을 다시 구해 걸어간다. 마지막 기억의 피날레는
기존 적의 정지를 존중하고 실제 C/Shift 입력으로 제단에 달려간다. AI·이동 속도·
체력·은신 RNG는 바꾸지 않는다.

- `Assets/Scripts/Core/HappyToy.V2.Core.asmdef`: System만 사용하는 독립 규칙 모듈.
  UnityEngine, 씬, `GameSession`, UI, 물리/NavMesh를 참조하지 않는다.
- `Assets/Tests/EditMode/Core/HappyToy.V2.CoreTests.asmdef`: Core를 직접 참조하는
  빠른 규칙 검사. 씬 로드·프레임 대기·문자열 리플렉션이 없다.
- 기존 씬 컴포넌트와 `UI/`, `AI/` 모듈은 현재 `Assembly-CSharp`에 남는다.
  UI는 표시 데이터·동작 콜백을, AI는 필요한 물리 입력·판정 결과를 경계로 쓴다.
  `GameShellView`, `StalkerBrain`의 기존 스크립트 GUID·공개 API·저장 연결은 유지한다.
- 기존 실제 게임 통합 검사는 `Tests/PlayMode`에 있다. `RuntimeAccess`는 기존
  게임 타입과 분리된 Core 타입을 구분해서 찾는 호환 도구다. 새 순수 규칙 검사에는
  이 호환 도구를 사용하지 않는다.
- `*Audit`, `*Probe`의 기존 진단은 명시된 인자로 설치되는 선택 검사다.
  자동 음향 검사는 프로젝트의 무음 지침을 따른다.

`lady-items`는 Core와 실제 암전 URP 카메라에서 향 연기/국소 불빛의 독립 픽셀 기여, 새 점화/소등/일시정지와 제단 안내를 검사한다. 실제 콩알탄의 정면 바닥/뒤쪽 몸 접촉·벽/거리/방향·예측과 동일 충돌, 0.5초 정지·부적/일시정지/0.25배 시간 보존·추격 재개와 기존 문/덫/마스터 무음/3틱 녹음을 함께 확인한다.

## 검증 명령

`SkinnedMeshPerformanceSetup.Apply`는 플레이어 빌드의 GPU batched 스키닝
설정을 보존한다. 공유 렌더 설정이므로 변경 시 `graphics`, 전체 양쪽 Unity
스위트와 새 플레이어의 실제 표면 검토를 실행한다.
`CorridorRevisionAuthoring.ImportAndReview`는 관련 생성 자산 가져오기와 손
정지 포즈 렌더를 명시적으로 묶는 편집 도구이며 플레이 모드를 시작하지 않는다.

저장소 루트에서 Python 3.11 이상과 프로젝트의 Unity CLI를 사용한다.
Unity **6000.6.0f1**을 설치하고 기존 Packages lock을 복원한다.

```powershell
python HappyToyV2/Tools/quality/run_feature_tests.py --list
python HappyToyV2/Tools/quality/run_feature_tests.py --profile core
python HappyToyV2/Tools/quality/run_feature_tests.py --profile title
python HappyToyV2/Tools/quality/run_feature_tests.py --profile ui --dry-run
python HappyToyV2/Tools/quality/run_feature_tests.py --profile drawer
python HappyToyV2/Tools/quality/run_feature_tests.py --profile corridor-performance
python HappyToyV2/Tools/quality/run_feature_tests.py --profile school-wayfinding --no-desktop-interaction
python HappyToyV2/Tools/quality/run_feature_tests.py --profile full
```

`core`는 빠른 EditMode 규칙 검사, `ui` / `ai` / `ai-doors` / `drawer` / `checkpoint` / `readability` / `corridor`는
해당 실제 동작의 PlayMode 검사다. `full`은 필터 없는 EditMode와 PlayMode를
모두 실행한다. 자연 입력 생존 경로를 포함한 전체 스위트는 각 단계 기본 7,200초,
기능별 검사는 1,800초의 대기 한도를 사용한다. `--timeout`으로 명시할 수 있다.
필터가 0개 검사를 선택하거나 검사 실패·건너뜀·결과 누락이 있으면
성공으로 처리하지 않는다. `--dry-run`은 명령을 확인하며 검사를 통과한 것으로
표시하지 않는다. 결과 XML과 프로필 보고서는 무시되는 `Builds` 아래에 저장한다.
각 검사에 독립 Editor 로그를 남기며 실패한 검사 수와 누락된 선택자도 요약한다.
Windows Editor의 GPU 오류를 진단할 때는 `--graphics-api d3d11` 또는 `d3d12`를
명시할 수 있다. 이 옵션은 해당 검사 프로세스에만 적용하며 프로젝트/플레이어
설정을 바꾸지 않는다. 다른 API에서의 통과는 기본 API의 렌더링 통과를 뜻하지 않는다.

`wraith-hallway`는 Core와 백수귀의 실제 32개 포즈 외곽·모델 중복 확대 방지·원래 이동 반경,
학교 배회/수색/추격과 회랑의 닫힌/잠긴 문 파괴·열린 문 통과·물리적인 문턱 이동,
파괴 중이던 문을 이미 통과하던 다른 몬스터의 문 통행 상태 해제,
문 파괴음의 한 번 재생·파편 일시정지·바닥 잔해 합침/지속·학교와 회랑 저장 무음 복원을 확인한다.
기존 마네킹 문 통과와 덫 저장 경계도 관련 회귀로 포함한다. 기존 실제 목재/금속 녹음
`mask-door-smash`의 원본·라이선스·변환은 `ThirdParty/Audio/manifest.json`과 기존 Audio-Credits에 보존되어 있다.
문 파편 원본·생성 설정·참조·편집 가능한 Blender·PBR/노멀 베이크 도구는 `SourceArt/DoorDebris`에 있다.
`DoorDebrisImport.ApplyAndReview`가 별도 목재 조각 메시와 재질을 Editor API로 가져와
`Resources/DoorDebris/ShatteredDoor` 프리팹과 실제 바닥 배치를 무음 렌더링한다.
`door-dust`는 Core와 실제 학교/회랑 문 파괴 검사만 실행하며, 파괴 먼지 생성·일시정지 중
입자 좌표와 시계 고정·효과 자동 제거·학교/회랑 저장 복원 시 먼지 미재생을 확인한다.
시각 마감은 `DoorBreakDustPreview.Render`의 무음 프레임/깊이 경계 렌더링으로 확인한다.

`traps`는 Core 규칙과 실제 바닥 설치/R 입력·일시정지/중복 설치 재고 보존,
6종 몬스터의 실제 공격 예고 중 발밑 포착·5초 위치/회전/공격 정지·포획 표정·해제,
학교 은신 저장 회귀 및 회랑 덫 재고/공급품/설치 모델의 무음 저장 복원을 검사한다.

`corridor-performance`는 Core와 실제 양보 경로 쿼리 예산·실패 재시도·정지 후
최신 목적지 복귀, 덫들의 한 프레임 actor 공유·동적 생성/비활성·장면 교체 정리,
검증 이동의 명시적 가속/원래 속도 복원, 원본/타이틀을 보존한 게임 메시·63개
뼈 바인딩·실제 표면 렌더·32포즈 culling, 기존 문 통행·6종 포획·저장을 검사한다.
실제 Tab/전체 적 정지/일시정지의 정속 제외와 8프레임·0.25초/rolling 경계도 포함한다.
이 프로필은 실제 장시간 플레이어 FPS 측정을 대신하지 않는다.
발동 성공 즉시 모델만 사라지고 root·구속·발동/해제 소리는 유지하며 일시정지/해제 뒤에도
모델이 돌아오지 않는지 확인한다. 저장 복원된 미발동 덫은 모델이 보이는 상태로 대기한다.
구형 저장의 선택 덫 필드 누락도 허용한다. `verify_recorded_audio.py`는 덫 6종의
PCM24와 기존 PCM16 자원의 필수 목록·원본/완성 해시·CC0 출처를 독립 검사한다. `TrapAssetsPreview.Render`는 실제 게임
프리팹의 열림/닫힘/해제 각도를 무음으로 렌더링하며 게임이나 플레이어를 실행하지 않는다.

`shelter`는 Core와 500개 시드의 방 혼합·기존 지도 4 재현, 실제 W/E 입력으로
입장→입구 닫기→다른 문으로 탈출, 사이클롭스/운캣의 실제 두 문 통과·닫기,
독립적인 두 문 상태의 저장·재개와 기존 회랑 체크포인트·중단·제단 검사를 묶는다.
기본은 한 문/두 문 방 1:1, 최대 8개다. 안전한 막다른 방 후보가 없는 시드에서도
우회로 위에 두 문 방 2개를 확보한다. 연결망 자체는 지도 4와 동일하다.

`entrance`는 지도 5의 실제 동굴 시작 위치·몸체 여유·물리 바닥/높은 천장·cell 0까지의
NavMesh 경로와 봉인 대문의 실제 E 상호작용·이동 차단·게임 계속을 검사한다.
새 동굴 위치에서 저장한 뒤 fresh scene으로 복원하며 구형 지도의 출구를 보존한다.
기존 회랑 체크포인트 및 표시 소유자의 충돌/문/아이템/NavMesh 보존 검사도 함께 실행한다.
EditMode는 Core와 학교/회랑 저장소·선택 덫 JSON codec·조명 저장 규칙을 포함하며,
실제 시작 전환·학교 중단 복원·설치된 덫 복원도 관련 회귀로 실행한다.
실제 시작점에서 대문을 보는 카메라는 UV0·`-visual` prefab의 공유 모델/외부 재질과
Resources 맵 정체성 및 흰 알베도 대체 비교의 실제 텍스처 픽셀 기여를 확인한다. 대문 렌더러 기여·배경색 교체 비교도
확인해 보이는 하늘·천장/벽 틈을 검출한다. 원래 14m 적 가독성 검사는 유지한다.
`CorridorEntranceRitualDressingTests`는 생성 바닥/수호상과 실제 지지면·중앙 NavMesh,
공유 원본/PBR 불변 및 소유 자원 해제를 검사한다. 실제 URP 카메라의 16:9/4:3/21:9
offscreen 렌더에서 바닥·부적줄·촛불·수호상의 독립 픽셀 기여를 확인하고,
동굴 전체 반사 box·기존 그림자 예산·연기/반사 일시정지·기존 maze box 복귀를 검사한다.
바닥의 전체 요철은 14cm 이하이며 최고점만 기존 물리 바닥 위 2.5cm에 둔다.
정규화된 수호상 prefab 2.8m를 장면에서 3.5m로 균일 확대하는 배치는 의도적이다.
실제 새 시작점은 대문 앞 동굴 local `(0, .03, -9.2)`·yaw 0°로 회랑 입구를 바라본다. 대문/충돌/모델 크기와 기존 저장 좌표는 유지한다.
낮게 매단 .95~1.15m 불교 종이등 5개와 동선 양옆의 촛불 무더기 4곳과 수호상 앞 촛불 2곳만 짧은 불빛을 낸다. 천장의 144 채움광과 출처 없는 암벽 광원을 제거했고, 실제 시작/대문 카메라·FOV/pose를 검증한다.
새 모델의 렌더링·외관 검토와 Windows 플레이어 빌드는 별도 확인이다.

동굴에는 회랑용 이동 반사와 별개인 128px 고정 반사 하나를 한 번 캡처한다.
동굴과 연결 통로를 덮어 플레이어가 경계를 넘어도 젖은 바닥이 하늘 반사로 바뀌지 않는다.
낮은 회랑 천장과 동굴 통로 사이의 높이 차는 암벽 뒷면으로 닫으며 실제 시작 카메라의
배경색 교체 비교로 천장 이음새의 하늘 노출을 검사한다.
`EntranceFloorStaysDarkWhenBackingAcrossTheCorridorMouth`가 실제 z=8~20 카메라의
바닥 밝기와 완료된 캡처를 확인한다. `entrance`에는 공유 반사 생성/해제의 학교·회랑
그래픽 검사도 포함된다. 시작 구도에는 재사용 장승·옹기·어두운 이중 문틀·부적줄과
촛불을 배치하며 원래 통행 폭·그림자 후보 2개를 보존하며 실제 불빛은 총 11개다.

`temple-lanterns`는 `entrance-lighting`의 Core/조명 규칙과 실제 카메라 검사에 수호상 좌우 얼굴의 촛불 켜짐/꺼짐 비교, 일반 회랑 천장 등의 공유 메시/PBR/2LOD·천장 여유·실제 시작/기억 방 렌더, 장식 해제 시 공유 자원 보존과 위험 점멸 회귀를 묶는다. 별도의 손전등 없이 실제 조각 얼굴의 밝기와 모델 픽셀 기여를 확인한다.

`title`은 `BaeksugwiHeadResourceTests`의 EditMode 검사와 `CloudBaeksugwiHeadTests.cs`의 새 검사도 자동 선택한다.
`Resources/Baeksugwi/Baeksugwi`는 런타임 prefab만, `Resources/Baeksugwi/Head_Writhe`는
prefab-relative 머리 회전 clip만 반환해야 한다. 이름이 겹치던 원본 FBX는 각각
`Baeksugwi_Source.fbx`·`Head_Writhe_Source.fbx`로 구분하고 GUID를 보존한다.
EditMode 검사는 Resources 키의 유일성·원본/prefab/clip GUID·실제 머리 경로 바인딩을 확인한다.
원본 머리 클립·
느린 타이틀 재생·몸체/발 고정·Time.timeScale 0에서의 재생과 숨김/동작 감소 시 정지·실제 메뉴 시작 전환을
검사하며, 16:9·4:3·21:9의 실제 타이틀 렌더와 UI 표시 영역에서 큰 실루엣·안광을
확인한다. `-quiet-diagnostics -v2-start-loading-output <새 결과 폴더>` 네이티브
검사는 준비된 타이틀에서 캡처/읽기/디스크 쓰기 없이 5초간 실제 타이틀 카메라의
렌더 완료 콜백을 센다. 메인 Update FPS와 타이틀 텍스처 갱신률을 별도로 보고한다.
이는 하드웨어에 표시된 FPS나 GPU 시간 측정이 아니며, 이후 기존 시작 전환·서랍
검사도 실행한다. 일반 실행에는 이 진단을 설치하지 않는다.

`corridor`는 열린 문을 실제 E로 닫기, 발소리·폭죽의 생산 콜백을 거부하는
백수귀의 정면/차폐 시야, 문이 있는 방의 차폐·선반·캐비닛·저장, 실제 NavMesh
코너 이동, 실제 걷기→발각 추격의 종소리 간격·정지, 6m 추격 소등·점화 기억 복원,
생성 소품의 실제 PBR/메시와 방 중앙의 NavMesh를 검사한다. 배포 전에는 `full`과
새 플레이어 빌드·표면 확인도 실행한다.

새 플레이어에 `-quiet-diagnostics -v2-corridor-revision-output <결과 경로>`를
주면 `CorridorRevisionProbe.cs`가 실제 회랑 73의 손전등·카메라로 장승·옹기·
도깨비 기와와 숨는 방, 다섯 기억의 단상 정렬 화면과 금속 손전등 HUD의 완충·절반·부족·방전 실제 플레이어 화면을 캡처한다. 프로세스 무음 가드에서 시작하며, 표면 검사는
가드 실패 시에도 리스너 0을 유지한다. 일반 플레이에는 설치되지 않는다. 이 검사는
생존 난이도나 최종 음향 믹스를 인증하지 않는다.

`ritual-pickups`는 Core와 `RitualPickupSupportTests`의 실제 메시 바닥 접촉·E 수집·
fresh scene 복원 검사를 실행한다. 지하 테이블·맨바닥·높은 바닥의 실제 높이와 지지면
없는 배치의 거부도 확인한다. 기존 재고/공급품 JSON 복원·학교 중단 복원·회랑 표시
소유권 회귀와 세 시드의 가구/아이템/문 접근성·동굴 입구의 실제 통과를 함께 확인한다.
같은 배치 검사는 `incense`와 `ritual-items`에도 포함된다.
`ritual-items`는 향·재고·시간 정지·실제 아이템 휠 입력과 손 통합 검사를 묶는다.
손 검사는 가져온 해부학 리그·끝점 마커와 손전등 감싸기·향/부적 집기·콩알탄/덫
손바닥 지지를 확인한다. 중앙 시야·액션/일시정지는 16:9·4:3·21:9의 실제 URP
오프스크린 투영으로 확인하며 네이티브 창 크기 변경 검사는 아니다.
휠을 연 동안 게임 시간은 평소의 0.25배이며, 명시적
일시정지는 계속 0이다. 시각 검토는 `PlayerHandsPreview.Render` / `RenderActions`의
실제 runtime 포즈를 사용한다. Blender 참조 액션만으로 게임 손 포즈를 검증하지 않는다.

`cabinet-expression`은 발각 돌진과 실제 치명타 뒤의 `CabinetDeathSequence`도 검사한다.
치명타가 확정되면 게임 진행을 고정하고 실제 몬스터 시각 몸체를 캐비닛 정면에서
플레이어로 향하게 해 열린 입을 가까이 들이댄다. 1.65초의 별도 시각 시계가 끝나야
게임오버 화면으로 넘어간다. 이 시계는 unscaled delta를 사용하지만 명시적 일시정지에서는
멈추고 동작 감소 설정을 따른다. 은신 확률·충돌·원래 공격 판정·저장 형식은 바꾸지 않는다.

`HAPPYTOY_CABINET_DEATH_REVIEW` 환경변수에 새 로컬 출력 폴더를 지정한 뒤
`cabinet-expression`을 실행하면 여섯 몬스터의 실제 플레이어 카메라·캐비닛·조명을
사용한 1280×720 PNG를 선택적으로 저장한다. 파일명은
`<몬스터>-player-cabinet-death.png`다. 평상시 검사에는 캡처를 추가하지 않으며,
결과 이미지는 선택 검토 자료로 `Builds/` 아래 등에 보관하고 소스에 넣지 않는다.

`uncat-cabinet-face`는 Core와 운캣 실제 캐비닛 발각/사망·탈출/일시정지·
원래 추격 얼굴/양눈 안광 검사, 사이클롭스 기존 얼굴 수명 검사 4개만 실행한다.
운캣은 기존 추격용 입 안 덮개와 캐비닛 전용 입이 겹치지 않는지도 검사한다.
조용한 얼굴 자산 미리보기는 `UncatCabinetFacePreview.Render`이다.

`cyclopse-cabinet-face`는 Core와 사이클롭스 전용 3개 통합 검사만 실행한다.
실제 캐비닛 발각 돌진/물리적 사망에서 열린 입 유지, 일시정지와 탈출 복원,
일반 추격의 기존 표정·안광·원본 스킨 보존을 검사한다. 전체 몬스터 검사가
필요할 때는 기존 `cabinet-expression`을 사용한다. 명시적 자산 미리보기는
`CyclopseCabinetFacePreview.Render`이며 실제 runtime 복제 메시/재질을 조용히
렌더링하고 게임 씬·플레이어를 시작하지 않는다.

`-quiet-diagnostics -v2-cabinet-expression-output <새 결과 폴더>`는 보존된 학교
캐비닛과 실제 사이클롭스를 사용해 추격 얼굴→원래 얼굴로 수색→후퇴→새 진입
발각 돌진→문 앞 예고→사망을 검증한다. 원래/새 얼굴은 실제 조명·URP 재질과
머리 위치를 쓰는 별도 근접 카메라로 캡처하며 생존 플레이·청감 검증은 아니다.

고정된 옛 학교/회랑 좌표를 사용하는 회귀 검사는 `BeginLegacyChapterFixture` /
`CreateLegacyCorridorFixture`로 지도 1을 명시한다. 최신 회랑 통합 검사는 기본 지도
5, 구형 학교 공간/계단 검사는 기존 호환 API의 지도 2를 사용한다. 실제 메뉴/제단 뒤 학교와 `school-loop`는 새 지도 3을 사용한다. 자연 입력 생존 검사는
`Time.captureDeltaTime=0`을 유지한다. 명시적인 전체 음향 캡처 검사의 고정 믹서
스텝은 별도 `controlledMixer` 입력으로 구분하며 자연 시간 생존 증거로 취급하지 않는다.

`readability`는 `CloudGameplayReadabilityTests.cs`의 실제 벽/리그/획득 상태/촛불 검사와
기존 큰 몬스터 문 통과·촛불 복원·리그 일시정지 검사를 실행한다. 새 플레이어의 실제
테두리 픽셀과 가림, 이동 중 걷기/추격 프레임은 선택 진단
`-quiet-diagnostics -v2-readability-output <새 결과 폴더>`로 확인한다.

전체 구조·어셈블리 변경 뒤에는 새 플레이어까지 확인한다.

`graphics`는 독립 규칙과 소유 파이프라인/SSAO 설정·그림자 선택 재사용,
학교·회랑의 원래 조명/반사 복원을 검사한다. 네이티브 성능 비교는
`-quiet-diagnostics -v2-graphics-optimization-output <새 결과 폴더>`를 사용하며,
학교는 `-v2-optimization-school`을 추가해 별도 프로세스에서 실행한다.
각 모드는 실제 NavMesh 위 두 정적 카메라, 64회 예열과 128회
1280×720 HDR 렌더링을 사용한다. 타이밍에는 동기식 한 픽셀 GPU 완료 읽기가
포함되고 PNG 저장은 제외된다. 표시 FPS나 순수 GPU 시간으로 해석하지 않는다.
로컬 네이티브 환경에서 실시간 반사 완료 응답이 확인되지 않아 이 비교에서는
양쪽 모두 반사 프로브·환경 반사를 제외하고 해당 범위를 보고서에 명시한다.
일반 게임의 반사 설정은 유지된다. 일시정지한 정적 장면에서 측정하며 추가
반사 요청이 생기면 실패한다. planner 바이트는 별도의 예열된 1,000회
팩터리/재사용 호출 비교이며 게임 프레임 전체 GC 측정이 아니다. Release의
할당 API가 양쪽 모두 0을 반환하면 카운터를 미지원으로 표시하며 0할당 증거가 아니다.
`Tools/quality/compare_graphics_optimization.py`는 동일 하드웨어/API/카메라/
MSAA/샘플 수를 확인해 변경 전후 중앙값·95백분위를 비교한다.

진행 중 성능은 새 플레이어에 `-v2-corridor-play-output <새 폴더>`와
`-v2-corridor-performance`를 함께 주어 실제 주 카메라 간격을 측정한다. 선택
`-v2-corridor-performance-patrol-seconds 360`은 성능 모드에서만 300~480초를 허용하며
기본은 0이다. 네 번째 기억 뒤 실제 4종 활성 시점부터 순환하며 원래 580/590/600초
전체 제한에 요청 초만 더한다. `-v2-audit-movement-multiplier 8`은 1~12의 선택 준비 이동
가속이며 기본 1이다. 원래 속도를 복원하며 가속·Tab 0.25배·전체 적 시간 정지·일시정지와
이후 최소 8개 실제 프레임/0.25초를 정속 통계에서 제외한다. 정속 rolling은 전체 이력이
안정화 뒤에 있어야 한다. 원시 구간은 플레이 중 느린 시간과 적 정지 구간도 보존한다.
`corridor-performance.json`의 10초 구간·정속 p50/p95/p99·1초 rolling 최소 FPS·60 미만
프레임 비율과 시간별 메모리를 같은 seed/API/해상도/설정에서 비교한다. 실제 렌더 콜백
300회 미만이나 버퍼 초과는 측정 실패다. 경로 사망·측정 유효성·16.67ms 예산 충족은
별도 결과이며, 경로/계측 PASS가 지속 60fps를 뜻하지 않는다. 지연 CPU/GPU 값의
시간 배율 귀속은 미검증이며 정속 근거는 실제 렌더 간격과 깨끗한 rolling 이력이다. 자세한 counter 범위와
관찰 부하는 `Tools/quality/README.md`를 따른다.

백그라운드 기능 검사는 `run_feature_tests.py --no-desktop-interaction`으로
명시적으로 `-v2-audit-no-desktop-interaction`을 Editor 인자에 추가할 수 있다.
같은 옵션은 Editor에 `-job-worker-count 2`도 전달해 진단의 Job System worker 수를 제한한다.
기본은 꺼져 있고 계획/summary에 요청 여부를 기록한다. Windows에서는 검사 자식 프로세스에
`STARTF_USESHOWWINDOW`/`SW_HIDE`, `CREATE_NO_WINDOW`, `BELOW_NORMAL_PRIORITY_CLASS`를
요청하고 summary의 `windowsHiddenBelowNormalRequested`에 기록한다. 옵션이 꺼져 있거나
Windows가 아니면 이 subprocess 설정을 추가하지 않는다. 이는 CPU 우선순위 요청이며
GPU 부하나 다른 작업의 프레임 저하가 없음을 보증하지 않는다. `DiagnosticDesktopPolicy`는
명시 flag와 `Application.isBatchMode`가 모두 참일 때만 `GameShell`/`PlayerMotor`/
`NativeCorridorPlayAudit`의 초기화·정리 커서 쓰기와 `PlayerItemWheel`의 OS 커서 중앙 이동을
생략한다. 일반 플레이는 그대로다.
native audit는 비배치 flag 요청을 거부하고 기존 키보드·마우스·게임패드·조이스틱을
해당 Unity process 안에서만 격리·복원한다.
공통 `CloudPlayModeTests` fixture도 같은 명시 조건에서만 background 입력을 켜기 전
초기 enabled 네 종류 장치를 격리하고 최종 finally에서 원래 enabled/current를 복원한다.
초기 disabled 장치와 나중에 만든 virtual 장치는 그대로 두며 이후 hotplug는 범위 밖이다.
순수 조건 네 가지는
`corridor-performance`의 `DiagnosticDesktopCursorSuppressionRequiresExplicitBatchOptIn`이
검사하며 실제 커서/포커스/OS 입력을 바꾸지 않는다. 자식 프로세스의 시작 창 숨김 요청이
Unity의 후속 창 표시를 보증하지는 않으므로 batch 실행과 커서 억제를 함께 사용한다.
전경 FPS의 근거가 아니며 사용자 작업 중 native/GPU 측정 보류 원칙을 대체하지 않는다.

Unity MCP는 설치된 Unity CLI의 `unity mcp --project-path <프로젝트>`와
`com.unity.pipeline`을 사용한다. `Tools/quality/unity_mcp_call.py --output
<새 결과 JSON>`은 설정 파일을 바꾸지 않고 도구 목록을 읽는다. `--tool`과
필요한 `--arguments-file`로 해당 목록의 스키마에 맞는 호출을 재현할 수 있다.
연결된 Editor에서는 Pipeline 안내의 `set_autotick`을 먼저 확인한다.
검사 실행 중 자산을 바꾸지 않으며, MCP 응답과 실제 새 빌드 검증은 구분한다.
Unity/Blender MCP의 Codex 등록·Blender 애드온 설치와 고정 의존성은
`Tools/mcp/README.md`와 `Setup-LocalMcp.ps1`이 담당한다.
`Tools/mcp/inspect_mcp.py`는 등록된 명령/환경을 사용해 실제 MCP 초기화·도구 목록과
명시한 도구 호출을 확인한다. 설치/연결 변경은 의존성·스크립트 구문·실제 Editor
조회로 검증하며 게임 테스트나 플레이어 빌드는 요구하지 않는다.
`Tools/mcp/lady_snapshot_blender.py`와 `lady_snapshot_unity.csx` / `lady_pose_unity.csx`는
별도 저우선순위 MCP 세션에서 레이디의 메시·가중치·재질·곡선과 제한된 CPU 포즈를
조회한다. 렌더·플레이·자산 저장 없이 `Builds/`에 결과를 쓰며 실제 외관/FPS 검증과 구분한다.

2026-10-17 검증: 전체 EditMode 134/134 통과. 첫 전체 PlayMode는 265/302였으며
숨은 휠 자식의 잘림 오탐·구형 팩/음원/생성자 기대·관측점을 보정하고 21:9 획득
안내의 실제 잘림을 수정했다. 실패 항목과 새 시계/레이아웃 경계를 재검사한 뒤,
마지막 Core 54/54·PlayMode 7/7이 통과했다. 전체 실행과 관련 재검사의 최신
고유 항목 합산은 303/305이며 한 번의 전체 녹색 실행은 아니다.
`CorridorDifferentSeedFiveMemoryRunSurvivesThroughRealInput`과
`CorridorFiveMemoryRunSurvivesThroughRealKeyboardMouseAndDoors`는 이전과 같이
자연 입력 경로에서 사망한다. 게임의 발각·공격·은신 RNG·세 번째 장난 강제 포획
규칙이나 생존 경로 전략을 완화하지 않았다. 두 학교 실제 입력 탈출 경로는 통과했다.
Editor 검사는 `-force-d3d11`이며 프로젝트/플레이어 기본 API는 유지한다.
사용자 작업 중에는 새 플레이어 실행과 장시간 native FPS 검증을 보류한다.
에디터 표면/시계 검사는 새 플레이어의 지속 60fps를 보증하지 않는다.
재현 가능한 도구만 커밋하고 상세 결과·비교 PNG/JSON은
`Verification/README.md` 정책대로 로컬 `Builds/`와 `Verification/`에 둔다.

```powershell
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.QualityValidation.BuildWindows -v2-release-player -v2-build-output Builds/Windows-Structure
```

스테이징 뒤 필수 소스는 `python HappyToyV2/Tools/quality/check_source_completeness.py`로
확인한다. 소스 검사는 Unity 컴파일·통합 검사·실제 플레이어 빌드 성공을 대신하지 않는다.
변경 범위에 대한 검사로 문제가 해결되면 새 근거 없이 같은 전체 검사를 반복하지 않는다.

`supplies-voices`는 Core와 두 체크포인트 저장소/선택 codec, 실제 선반·서랍의 향/조각 지지면·E 수집·유한 재고·버전 1 복원을 검사한다. 기존 서랍/학교 저장, 운냥이 실제 배회→추격 종소리와 새 사이클롭스 공간 호흡의 벽·일시정지·음소거·거리·시간 정지·자원 정리를 무음으로 확인한다. 종소리는 배회 .50/추격1.00이며 기존 .88/.23초 간격과 음원은 유지한다. 원거리 방울 변경은 `uncat-bell-distance`로 검사한다. 실제 기기 청감·생존 플레이 인증은 별도다.


`school-loop`는 Core 지도/강체 연결 규칙과 실제 학교 메뉴 버튼, 구형 지형/배우 제외,
잠긴 출입문·물리 유리·실내로 갈 수 없는 경로, 실제 W 이동으로 양방향 모두 같은
왼쪽 교실/같은 입구에 진입하는 연결과 시선/지지면 보존·일시정지 중 무전환,
버전 3 저장·복원/불법 실내 좌표 거부를 확인한다. 앞 연결은 평행 이동,
뒤 연결은 180° 회전이며 이웃 미리보기와 플레이어 복귀에 같은 강체 변환을 쓴다.
2026-10-24의 `school-turnbacks`는 양끝에 추가한 8m 세로 통로와 불투명한 두 꺾임,
폭 전체의 서기/숙이기 시선 차단, 두 코너의 실제 W 이동, 새 끝 평면에서만 전환,
`PlayerWorldTransition.TraverseWorldSeam`의 이동 벡터 회전/낙하 속도·시선·자세 유지,
연장 통로의 저장 복원/기존 입구 좌표, 전진 시계·자바라·주 복도 밝기를 함께 검사한다.
중앙 복도의 방 좌표와 지도/저장 버전 3은 유지한다. 상단 전환은 z=46.4,
하단 전환은 z=-8이며, 도착점은 이전 복도가 벽에 가려진 하단 세로 통로다.
음악실의 원본 기반 두 LOD·공유 PBR·좌석/악보대 배치, 실제 복도 창문을 통한
피아노 기여와 복도/천장을 제거한 실제 Unity 평면 화면도 기록한다.
현재 학교는 관찰형 맵/음악실 제작 단계로, 이상현상 판정이나 최종 탈출을 구현하지 않는다.


## 빠른 검증 반복

일상 수정은 위 기능 프로필로 검증한다. 전체 실행에서 실패한 항목을 고친 뒤에는
`Tools/quality/retry_failed_tests.py --results <NUnit XML> --mode PlayMode`로 실패·미완료
사례만 새로 실행하고 `--include-profile <기능>`으로 영향받은 기능을 추가한다.
`--timings-only`는 Unity를 실행하지 않고 사례별 시간과 느린 검사를 읽는다.
`run_feature_tests.py`의 결과에도 이 시간 분석이 자동으로 남는다. 원본 전체 결과와
부분 재검사의 범위·판정은 함께 보존한다. 공유 경계/전체 개편/배포의 전체 검사 요구는 유지한다.

`school-horror`의 선택 렌더 내보내기는 `HAPPYTOY_SCHOOL_HORROR_REVIEW_DIR`를 새 `Builds/` 하위 폴더로 명시한다. 복도 정/역방향·정독반/음악실 창문·바닥/누수·파손 천장/박리 벽 근접·손전등 켜짐의9시점을 실제1920×1080 게임 카메라로 저장한다. 평면 반사 검사는 실제 바닥의 활성/비활성 픽셀 차이·소유 타깃과 카메라 수명·GL/전역 셰이더 원복을 확인한다. 화면은 참조 학교 전체의 포토그래메트리·볼류메트릭 그림자 또는 실제 플레이 FPS의 인증이 아니다. 재현과 남은 고품질 리소스는 `SourceArt/SchoolHorror/README.md`에 기록한다.

`SchoolHorrorReviewProbe`는 `-batchmode -v2-audit-no-desktop-interaction -quiet-diagnostics -v2-school-horror-review-output <새 폴더>`에만 설치되는 Windows 플레이어 표면 검토다. 별도 기록·현재 프로세스 음소거 guard를 사용하며 초기 반사 프로브 완료 후 실제 카메라 PNG를 저장한다. 배치 실행에서 캡처가 대기하면25초 제한 후 실패/상태를 기록하며 완료를 주장하지 않는다. 이 경로가 에디터 렌더 검토나 일반 플레이의 반사 완료를 대신 검증하지 않는다. `GameShell`은 이 명시 플래그에서만 자동 Begin을 생략한다. 일반 시작/플레이에는 촬영을 추가하지 않는다.

## 학교 인게임 밝기 · 2026-10-22

`SchoolHorrorLighting`은 학교 전용 Neutral/−0.1EV/contrast0 프로필로 승인된 복도 밝기를 유지한다. 검토의 `SingleCameraRequest`는 정상 카메라의 Volume 갱신을 생략하므로, 공개하는 학교 프리뷰와 `SchoolHorrorReviewProbe.Pose`는 `StandardRequest`의 정상 스택을 사용한다. `school-brightness`는 다른 카메라의 Volume 상태를 먼저 설치하고도 정상 스택에서 승인된 전체/벽 밝기가 나오는지 검사한다. `school-horror`에도 이 검사가 포함된다. Windows 밝기 검토는 기존 명시 옵션에 `-v2-school-horror-brightness-only`를 더하고 반사 프로브 완료와 밝기 판정을 분리해 기록한다. 재현·수치 기준·피드백 원본은 `SourceArt/SchoolHorror/BRIGHTNESS.md`에 있다.

## 학교 시작 자바라 · 2026-10-22

`SchoolLoopArchitecture.Gate`는 `SchoolRetractableGate`에 열린 입구 조립을 맡긴다. 지정 웹앱 생성 원본의 접힌 자바라 한 장과 자유단 보관 고리의 자물쇠 하나를 사용한다. `SchoolRetractableGateImport`가2LOD/외부 PBR prefab과 자물쇠 위치를 만들며 `QualityValidation.BuildWindows`가 자산 완전성을 확인한다. 닫기/잠금 상호작용·저장 버전은 추가하지 않는다. `school-gate` 프로필은 실제 원본 메시/PBR/LOD 정체성·3m 이상 열린 시작 동선의 CapsuleCast/NavMesh/W 이동·학교 이동/저장/밝기 회귀와 정상 카메라의 입구 전체/자물쇠 근접/시작 시점을 검사한다. `HAPPYTOY_SCHOOL_GATE_REVIEW_DIR`를 새 Builds 하위 폴더로 명시할 때만 렌더를 저장한다. 원본 이미지·프롬프트·실제 작업·GLB/8K·packed Blender·고정 도구·해시는 `SourceArt/SchoolRetractableGate/README.md`에 있다.

## 학교 이동 중 광원 겹침 · 2026-10-23

`SchoolLightOverlapScope`는 학교 전용 Forward+ 품질 파이프라인을 소유하고 disable/unload에서 이전 override를 복원한다. `SchoolHorrorLighting`이 이 범위를 조립한다. 4→8 완화 후에도 물체별 목록 교체로 남은 천장 빛 단절을 화면/깊이 cluster 계산으로 제거한다. 화면 밖 광원도 보이는 표면에 기여하면 포함한다. `Editor/SchoolLightingSetup`은 공용 설정을 `Resources/SchoolRendering/`의 별도 파이프라인·렌더러·독립 AO에 복제하고 공식 `IncludeAdditionalRPAssets` 라벨과 예약 품질 단계 `School runtime shader variants`를 함께 등록한다. 엔진 native `QualitySettings.GetAllRenderPipelineAssetsForPlatform` 수집기도 학교·공용 파이프라인을 모두 포함해야 하며, 라벨만으로 엔진 prefilter의 cluster 변형 제거를 막았다고 간주하지 않는다. 공용 기본 렌더러는 Forward다. 광원 출력·노출·그림자 지도2048/손전등1024/국소512·최대 두 point caster·반사 해상도는 유지한다.

`school-lighting-continuity`는 Core/atlas/그래픽 자산·Forward+ 빌드 수집과 실제12.5cm 입구 이동·고정 형광등·손전등 on/off·Forward8 대조군·동일 월드 천장12지점 재투영·9개 겹친 광원 각각의 픽셀 기여/카메라 뒤 광원·학교 범위 disable/re-enable/다음 모드 파이프라인 복원을 검사한다. 기존 학교 전체 표면/밝기/반사/조명 pause·실제 통행/저장/자바라와 회랑 그림자/렌더 범위도 포함한다. `HAPPYTOY_SCHOOL_STEP_LIGHT_REVIEW_DIR`를 Builds 하위로 명시할 때만 프레임과 실제 trace를 쓴다. 입력 피드백·해시와 재현은 `SourceArt/SchoolHorror/LightContinuity/README.md`에 있다.


학교 Windows 조명 변형의 엔진 prefilter 등록은 `Editor/SchoolLightingSetup`이 소유한다.
라벨 기반 URP 수집과 별도로 `QualitySettings.GetAllRenderPipelineAssetsForPlatform`에서
학교·공통 자산을 모두 확인한다. 예약 학교 품질 단계는 끝에만 추가하며 기존 선택을 보존한다.
관련 단일 EditMode 검사는 `SchoolNativeShaderPrefilterIncludesBothPipelinesWithoutChangingExistingQualityChoices`다.
명시 `HAPPYTOY_SCHOOL_SHADER_AUDIT_DIR`와 새 Windows 밝기 검토로 실제 cluster 변형 보존과
기본 Forward+의 벽/바닥 밝기를 확인한다. 재현은 `SourceArt/SchoolHorror/BRIGHTNESS.md`의 Windows 복구 절에 있다.

타이틀 머리 검사는 회전 값과 실제 타이틀 텍스처 변화를 함께 확인한다.
`-v2-baeksugwi-head-output <새 폴더> -v2-baeksugwi-title-only`는 기존 진단 무음 guard를
사용해 3초 동안 타이틀 텍스처3장과 머리 영역 픽셀 변화를 검사하고 회랑을 만들지 않는다.
batch 모드에서도 앞선 LateUpdate의 완성된 텍스처를 읽으며15초 안에 종료한다.
