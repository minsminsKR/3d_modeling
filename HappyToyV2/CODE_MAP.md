# HappyToyV2 기능별 수정 지도

수정할 기능의 행에서 시작하고 해당 코드와 검증만 읽는다. 공유 규칙·저장·입력
경계에 영향을 주면 관련 행까지 범위를 넓힌다. 전체 리팩터링과 배포 준비는 전체
검사와 새 플레이어 빌드를 실행한다. 아래 경로는 Unity 프로젝트 기준이다.
경로가 없는 코드 파일명과 `UI/`·`AI/`·`Core/`는 `Assets/Scripts/` 아래를 뜻한다.

| 기능 | 진입점 / 실제 책임 소유자 | 검사 프로필 |
|---|---|---|
| 메뉴·일시정지·설정 | `Assets/Scripts/GameShell.cs`는 화면 전환·입력 허용을 조정한다. `Assets/Scripts/UI/ShellPauseScreen.cs`, `ShellSettingsScreen.cs`는 각 화면의 컨트롤을 소유한다. | `ui` |
| 기록·결과·페이지 이동 | `UI/ShellJournalScreen.cs`, `ShellRecordsScreen.cs`, `ShellResultScreen.cs`. 화면용 데이터와 Action을 받아 표시하며 게임 진행을 직접 변경하지 않는다. | `ui`, 저장 변경 시 `checkpoint` |
| HUD·메뉴 공통 스타일 | `UI/ShellHudView.cs`는 HUD 컨트롤을 소유한다. `UI/FlashlightGauge.cs`는 낡은 금속 손전등 PNG와 얇은 황동 잔량 레일을 표시한다. 아이콘 제작 원본·프롬프트는 `SourceArt/Hud`에 있다. `UI/ShellLayout.cs`는 공통 배치·스타일을 만든다. `GameShellView.cs`는 Unity 패널·입력·각 모듈 연결을 담당한다. | `ui`, 손전등 표시만 변경 시 `flashlight-hud` |
| 타이틀·게임 시작 전환 | `GameShellTitleView.cs`, `CinematicTitleScene.cs`, `GameShellStartTransition.cs`. 타이틀·접근 장면은 60Hz 예약을 유지하고, 동작 감소 시 정적인 타이틀은 저빈도 갱신한다. `UI/TitleApproachFootsteps.cs`는 로딩 접근 중 기존 `mask-heavy-step` 녹음을 2D로 재생한다. 타이틀 일시정지만 우회하며 마스터 음량·진단 무음·종료 정리를 따른다. 실제 로딩 중 입력 차단과 접근 연출을 보존한다. | `title`, 공통 UI 변경 시 `ui` |
| 콩알탄 6발 묶음·충돌·유인 | `Core/SnapperStockRules.cs`는 묶음당 6발·최대 30발·구형 저장 변환을, `FirecrackerInventory.cs`는 입력과 재고를 소유한다. 기존 이름/GUID/enum은 저장 호환을 위해 유지한다. `FirecrackerProjectile.cs`는 첫 고체 충돌에서 한 번만 파열하고 `SnapperImpactEffect.cs`는 짧은 종이 먼지·작은 섬광을 표시한다. `StalkerBrain.HearSnapper`는 실제 소리 경로를 거쳐 위치를 기억하며 베이비도 숨은 플레이어 좌표 대신 그 위치를 수색한다. 기존 `NoiseInvestigationClock`은 도착 후 6초를 보장한다. 백수귀의 청각 무반응·목격 추격 우선은 유지한다. `SnapperVisuals`는 학교/회랑 공급품과 투척 낱알에 웹앱 모델을 붙인다. `Assets/Editor/SnapperAssetsImport.cs`, `SourceArt/Snappers/`, `SourceArt/Audio/render_snappers.py`, `ThirdParty/Audio/Snappers/`가 PBR 원본·Blender·참조·실제 CC0 녹음·재생성 경로다. | `snappers`, `core`; 실제 플레이 검증은 사용자 요청 시 별도 |
| 적의 시야·청각 | `StalkerBrain.cs`와 `AI/StalkerPerception.cs`가 실제 가림·층·소리 경로를 처리한다. 회랑 백수귀는 `LanternMaskEncounter.cs` / `LanternMaskRunner.cs`의 정면 시야만 사용하며 발소리·폭죽 콜백을 거부한다. | `core`, `ai`, `corridor` |
| 공격·캐비닛 은신 | `AI/StalkerAttack.cs`는 일반 공격 예고/회복과 표적을 소유한다. `Core/CabinetEncounterClock.cs`는 목격한 진입의 접근→두리번거림→후퇴 또는 발각 돌진→문 앞 예고를 판정한다. `AI/EnemyCabinetEncounter.cs`가 실제 NavMesh·문·현재 가림을 연결하며 Stalker/백수귀/마네킹이 공유한다. `PlayerMotor.Hide`는 콜라이더를 숨기기 전에 모든 관찰자에게 알린다. `Core/CabinetTauntMemory`가 동일 캐비닛·몬스터의 4m 이내 실제 목격 진입을 세며 세 번째에는 확률을 덮어쓰고 바로 돌진한다. `AI/PlayerCabinetMemory`는 실제 관찰자와 돌진 중 재출입 금지, 12m 이탈 시 초기화, 선택 체크포인트 기록을 연결한다. 일반 진입의 75%/25% 한 번 판정과 구형 저장 호환은 유지한다. | `core`, `cabinet-expression`, `ai`, 저장 변경 시 `checkpoint` |
| 적의 문 통과·추적 경로 | `AI/StalkerDoorPassage.cs`는 문 통과와 제한된 대기를 소유한다. `AI/CorridorRouteGuide.cs`는 지도 4의 타일 중앙 경유와 회전을, 독립 `Core/CorridorRouteRules.cs`는 연결된 다음 타일을 선택한다. `EnemyNavigation.FollowPath`는 원래 최종 목표·경로 증거를 보존하며 실제 이동을 연결한다. | `ai-doors`, `corridor` |
| 몬스터 간 통행·양보 | `AI/EnemyPassageTraffic.cs`가 실제 NavMesh 위의 짧은 양보 이동과 경로 복귀를 소유한다. `Core/MonsterPassingRules.cs`는 통행 우선권을 한쪽에만 준다. 기존 AI의 목표·저장 위치는 바꾸지 않는다. | `traffic-loading`, 문/공통 경로 변경 시 `ai-doors` |
| 열린 문 다시 닫기 | `DoorInteractionTarget.cs`의 물리·NavMesh 제외 문틀 쿼리를 `PlayerMotor.FindFocus`가 실제 가림과 함께 판정한다. `Interactable.cs`는 문짝 이동·점유·소리의 기존 소유자다. | `corridor`, `ai-doors`, `ui` |
| 큰 몬스터의 벽 겹침·걷기/추격 동작 | `AI/EnemyBodyClearance.cs`는 렌더링 몸체 크기의 트리거 쿼리(상호작용·NavMesh 베이크 제외)로 실제 NavMesh 이동을 보정한다. `AI/EnemyLocomotionPose.cs`는 원본 리그·클립 위에 복원 가능한 팔다리 동작을 더한다. `V1MonsterMotion.cs`가 이동 속도·상태·공격·일시정지를 연결한다. | `readability`, 문/경로 변경 시 `ai-doors` |
| 사이클롭스·운캣·베이비 추격 표정 | `EnemyChaseFace.cs`는 각 actor의 새 얼굴 리소스를 실제 Chase/캐비닛 발각 돌진 중 섞고 수색·배회·비활성화 시 원래 재질로 복원한다. `CyclopseChaseFace`는 기존 컴포넌트 API/GUID를 유지한다. 사이클롭스·운캣의 선택적인 `CabinetFaceData`/`EnemyCabinetFace`가 캐비닛 발각 돌진 중 턱·눈꺼풀·볼을 추가 변형하고 머리 뼈에 붙인 개별 크기·위치·간격·기울기·파손을 지정한 실제 이빨 22개·잇몸·입 안·혀와 보완 입술 면을 펼친다. 닫힘/열림 메시 쌍으로 윗턱은 얕게 유지하고 아래턱·턱끝은 크게 내려가며 한쪽으로 틀어지게 한다. 이빨을 세로로 확대하지 않는다. 일반 추격은 기존 표정이며 탈출 시 닫힘·일시정지·사망 유지·비활성 복원을 소유한다. 원래 사이클롭스 외눈·운캣 두 눈과 안광 소유권은 유지한다. 운캣의 기존 추격용 입 안 덮개는 캐비닛 전용 입이 켜진 동안 숨긴다. `UncatCabinetFaceImport`는 운캣의 얼굴 방향·깊이와 별도 치열/턱 프로필을 적용하며 `SourceArt/UncatCabinetFace`에 원본을 보관한다. `CyclopseCabinetFaceImport`와 `SourceArt/CyclopseCabinetFace`가 프로필·생성 피부·Blender 원본·재현 도구를 보관한다. `V1MonsterMotion`이 기존 studio skin에 연결하며 모델·클립·지지는 유지한다. 기존 `CyclopseExpressionData`/lit shader를 공유한다. `MonsterRedEyes.BindExpressionMaterials`가 교체된 얼굴 재질에도 원래 안광 마스크·밝기·토글을 적용하고 shader는 얼굴 영역 발광을 유지한다. `Assets/Editor/BabyEyeMaskImport.Apply`는 베이비의 실제 game UV 삼각형 전체에서 홍채·동공 마스크를 재생성하며 SourceArt/CreatureStudio/Baby/eyes-surface-*에 설정·결과·해시를 보관한다. `Assets/Editor/CyclopseExpressionImport.Apply`와 `CreatureFaceImport.Apply`가 `SourceArt/CyclopseExpression/`, `SourceArt/ChaseFaces/`의 원본·정확한 입력·프롬프트·프로필에서 리소스를 재생성한다. `CreatureFaceAuthoring`은 원본 정면/적용 결과를 검토한다. | `uncat-cabinet-face`, `cyclopse-cabinet-face`, `cabinet-expression`, `readability`, 새 플레이어의 각 눈 발광 on/off 비교·캐비닛 진단 |
| 획득 아이템 테두리·촛불 위기 소등 | `PickupOutline.cs`와 `Resources/PickupOutline.shader`는 실제 획득 가능/표시 상태와 깊이 검사를 따른다. `CandleDanger.cs`는 가까운 몸체와 공격을 판정하고 `WaymarkCandle.cs`는 점화 기억을 보존해 자동 복원한다. | `readability`, 저장 변경 시 `checkpoint` |
| 베이비·적별 감각 | `StalkerCorridorBaby.cs`, `CorridorBabyBehaviour.cs`, `Core/CorridorBabyMemory.cs`, `Core/CorridorThreatRules.cs`. 운캣과 학교 화캣의 정체성은 별도다. | `core`, `ai` |
| 서랍·가구 상호작용 | `CorridorDrawer.cs`, `CorridorFurniturePlacement.cs`, `CorridorFurnitureLibrary.cs`, `PlayerMotor.cs`. 움직이는 서랍과 플레이어 충돌·상호작용 거리를 확인한다. | `drawer` |
| 기억 단상 표시·정렬 | `HauntedCorridorPresentation.DressShrines`는 실제 `Memory altar` 충돌 단상의 위치를 기준으로 모델을 놓는다. 기억 위치·통로·저장 좌표는 유지한다. | `shrines`, 새 플레이어 표면 확인 |
| 회랑 생성·방·제단 | `CorridorLayout.cs` / `CorridorRun.cs`의 새 지도 버전은 5다. `Core/CorridorShelterRules.cs`는 한 문짜리 막다른 방과 우회로 위의 두 문짜리 방을 선택한다. `CorridorShelterRoom.cs`는 각 방의 실제 문 목록을 연결한다. 기존 지도 1~4는 같은 버전으로 재구성한다. | 방·출구 선택/저장 변경은 `shelter`, 일반 회랑은 `corridor`, 전체 연결망 변경 시 `full` |
| 운냥이 배회·추격 종소리 | `UncatBellVoice.cs`는 실제 움직임·추격 상태와 녹음 기반 공간 음원을 연결한다. `Core/EnemyBellClock.cs`는 배회/추격 간격과 음량을 판정한다. 제작 원본·비교는 `SourceArt/Audio/render_uncat_bell.py`와 `uncat-bell-selection.json`에 있다. | `core`, `corridor`, 기존 무음 음향 검사 |
| 한국적인 소품 | `KoreanCorridorDressing.cs`는 위치 데이터만 받아 중앙 경로·가구·문 이동을 피한다. `KoreanCorridorProp.cs`는 자산/구역 정체성을 제공한다. `SourceArt/KoreanCorridorProps/`에는 웹앱 GLB/PBR·입력·프롬프트·Blender·LOD 재생성 도구가 있다. | `corridor`, `readability`, `full`, 새 플레이어 표면 확인 |
| 학교 공간·첫 등장 | `SchoolCampusLayout.cs`, `SchoolCampusArchitecture.cs`, `SchoolCampusRooms.cs`, `SchoolFirstAppearances.cs`, 각 등장 이벤트. | `ai`, 공간/저장 버전 변경 시 `full` |
| 저장·재개·게임 진행 | `GameSession.cs`, `CorridorRunCheckpoint.cs`, `ChapterRunCheckpoint.cs`, 각 `*CheckpointStore.cs` / `*Checkpoint.cs`. 체크포인트 partial은 기존 저장 형식을 연결하는 어댑터다. | `core`, `checkpoint` |
| 그래픽 설정·그림자 예산·성능 비교 | `Assets/Editor/GraphicsLightingSetup.cs`가 소유 URP 자산의 HDR·MSAA4·SMAA·깊이 재구성 SSAO를 재생성한다. `GraphicsLightingPresentation.cs`는 모드별 조명/반사를 복원한다. `LocalShadowBudget.cs`는 매 프레임 실제 광원에서 `ShadowAtlasPlan.Rebuild`의 재사용 목록을 채우며 동일 거리/ID 우선순위로 그림자를 선택한다. `GraphicsOptimizationProbe.cs`는 명시된 인자로만 정적 카메라의 GPU 완료 대기 렌더링 시간을 측정한다. | `graphics`, 공통 렌더링 변경/배포는 `full` 및 새 플레이어 표면·안광 검사 |
| 소리·자산 | `ExternalAudio.cs`, `EnemyAcoustics.cs`, `EnemyStudioVisual.cs`와 각 자산의 SourceArt/ThirdParty 안내. | 해당 기능 검사 및 기존 무음 네이티브 진단 |

`RoomAmbience.ConfigureRunPositions`는 위치와 회랑/학교 모드를 명시적으로 받는다.
학교의 `ChapterAtmosphere`와 `SchoolCampusRooms`는 학교 모드를 전달한다. 위치
배열이 있다는 이유만으로 학교 음원을 회랑 음원으로 교체하지 않는다.

## 모듈 경계

- `Assets/Scripts/Core/HappyToy.V2.Core.asmdef`: System만 사용하는 독립 규칙 14개.
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

## 검증 명령

저장소 루트에서 Python 3.11 이상과 프로젝트의 Unity CLI를 사용한다.
Unity **6000.6.0f1**을 설치하고 기존 Packages lock을 복원한다.

```powershell
python HappyToyV2/Tools/quality/run_feature_tests.py --list
python HappyToyV2/Tools/quality/run_feature_tests.py --profile core
python HappyToyV2/Tools/quality/run_feature_tests.py --profile title
python HappyToyV2/Tools/quality/run_feature_tests.py --profile ui --dry-run
python HappyToyV2/Tools/quality/run_feature_tests.py --profile drawer
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

`shelter`는 Core와 500개 시드의 방 혼합·기존 지도 4 재현, 실제 W/E 입력으로
입장→입구 닫기→다른 문으로 탈출, 사이클롭스/운캣의 실제 두 문 통과·닫기,
독립적인 두 문 상태의 저장·재개와 기존 회랑 체크포인트·중단·제단 검사를 묶는다.
기본은 한 문/두 문 방 1:1, 최대 8개다. 안전한 막다른 방 후보가 없는 시드에서도
우회로 위에 두 문 방 2개를 확보한다. 연결망 자체는 지도 4와 동일하다.

`title`은 원본 머리 클립·몸체 고정·일시정지·동작 감소와 실제 메뉴 시작 전환을
검사한다. `-quiet-diagnostics -v2-start-loading-output <새 결과 폴더>` 네이티브
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
5, 최신 학교 공간/계단 검사는 기본 지도 2를 사용한다. 자연 입력 생존 검사는
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

Unity MCP는 설치된 Unity CLI의 `unity mcp --project-path <프로젝트>`와
`com.unity.pipeline`을 사용한다. `Tools/quality/unity_mcp_call.py --output
<새 결과 JSON>`은 설정 파일을 바꾸지 않고 도구 목록을 읽는다. `--tool`과
필요한 `--arguments-file`로 해당 목록의 스키마에 맞는 호출을 재현할 수 있다.
연결된 Editor에서는 Pipeline 안내의 `set_autotick`을 먼저 확인한다.
검사 실행 중 자산을 바꾸지 않으며, MCP 응답과 실제 새 빌드 검증은 구분한다.

2026-10-15 최적화 검증: 전체 EditMode 117개 통과. 전체 PlayMode는
263/268 통과 후 촛불 청각 거리·표정 메시·실제 제단 지지체 기준을 고쳐
관련 재검사 6개가 통과했다. 합산 고유 항목은 266/268 통과이며, 한 번의
전체 녹색 실행은 아니다. `CorridorDifferentSeedFiveMemoryRunSurvivesThroughRealInput`과
`CorridorFiveMemoryRunSurvivesThroughRealKeyboardMouseAndDoors`는 자연 입력
생존 경로에서 계속 사망했다. 시험용 경로 보정은 되돌렸고 게임의 발각·공격·
은신 RNG·세 번째 장난 강제 포획 규칙을 완화하지 않았다.
초기 DX12 Editor 전체 검사는 UIElements 텍스처 업로드 중 장치 제거/응답 중단
오류로 종료되어, 두 전체 스위트는 Editor만 `-force-d3d11`로 실행했다.
프로젝트/플레이어 기본 API는 유지했으며 새 DX12 플레이어의 얼굴·캐비닛·가독성·
회랑 검사 6회/261항목은 통과했다. 장시간 DX12 안정성과 전체 생존 성공을
보증하는 결과는 아니다. 재현 가능한 도구만 커밋하고 상세 결과·비교 PNG/JSON은
`Verification/README.md` 정책대로 로컬 `Builds/`와 `Verification/`에 둔다.

```powershell
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.QualityValidation.BuildWindows -v2-release-player -v2-build-output Builds/Windows-Structure
```

스테이징 뒤 필수 소스는 `python HappyToyV2/Tools/quality/check_source_completeness.py`로
확인한다. 소스 검사는 Unity 컴파일·통합 검사·실제 플레이어 빌드 성공을 대신하지 않는다.
변경 범위에 대한 검사로 문제가 해결되면 새 근거 없이 같은 전체 검사를 반복하지 않는다.
