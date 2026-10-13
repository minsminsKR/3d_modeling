# HappyToyV2 기능별 수정 지도

수정할 기능의 행에서 시작하고 해당 코드와 검증만 읽는다. 공유 규칙·저장·입력
경계에 영향을 주면 관련 행까지 범위를 넓힌다. 전체 리팩터링과 배포 준비는 전체
검사와 새 플레이어 빌드를 실행한다. 아래 경로는 Unity 프로젝트 기준이다.
경로가 없는 코드 파일명과 `UI/`·`AI/`·`Core/`는 `Assets/Scripts/` 아래를 뜻한다.

| 기능 | 진입점 / 실제 책임 소유자 | 검사 프로필 |
|---|---|---|
| 메뉴·일시정지·설정 | `Assets/Scripts/GameShell.cs`는 화면 전환·입력 허용을 조정한다. `Assets/Scripts/UI/ShellPauseScreen.cs`, `ShellSettingsScreen.cs`는 각 화면의 컨트롤을 소유한다. | `ui` |
| 기록·결과·페이지 이동 | `UI/ShellJournalScreen.cs`, `ShellRecordsScreen.cs`, `ShellResultScreen.cs`. 화면용 데이터와 Action을 받아 표시하며 게임 진행을 직접 변경하지 않는다. | `ui`, 저장 변경 시 `checkpoint` |
| HUD·메뉴 공통 스타일 | `UI/ShellHudView.cs`는 HUD 컨트롤을 소유한다. `UI/ShellLayout.cs`는 공통 배치·스타일을 만든다. `GameShellView.cs`는 Unity 패널·입력·각 모듈 연결을 담당한다. | `ui` |
| 타이틀·게임 시작 전환 | `GameShellTitleView.cs`, `CinematicTitleScene.cs`, `GameShellStartTransition.cs`. 타이틀·접근 장면은 60Hz 예약을 유지하고, 동작 감소 시 정적인 타이틀은 저빈도 갱신한다. 실제 로딩 중 입력 차단과 접근 연출을 보존한다. | `title`, 공통 UI 변경 시 `ui` |
| 적의 시야·청각 | `StalkerBrain.cs`와 `AI/StalkerPerception.cs`가 실제 가림·층·소리 경로를 처리한다. 회랑 백수귀는 `LanternMaskEncounter.cs` / `LanternMaskRunner.cs`의 정면 시야만 사용하며 발소리·폭죽 콜백을 거부한다. | `core`, `ai`, `corridor` |
| 공격·캐비닛 은신 | `AI/StalkerAttack.cs`는 공격 예고/회복과 예고 시 확정한 표적을 소유한다. `Core/EnemyAttackClock.cs`, `CabinetHidingRules.cs`는 독립 규칙이다. `PlayerMotor.cs`, `Interactable.cs`는 실제 은신 입출력을 연결한다. | `core`, `ai`, 저장 변경 시 `checkpoint` |
| 적의 문 통과·추적 경로 | `AI/StalkerDoorPassage.cs`는 문 통과와 제한된 대기를 소유한다. `AI/CorridorRouteGuide.cs`는 지도 4의 타일 중앙 경유와 회전을, 독립 `Core/CorridorRouteRules.cs`는 연결된 다음 타일을 선택한다. `EnemyNavigation.FollowPath`는 원래 최종 목표·경로 증거를 보존하며 실제 이동을 연결한다. | `ai-doors`, `corridor` |
| 열린 문 다시 닫기 | `DoorInteractionTarget.cs`의 물리·NavMesh 제외 문틀 쿼리를 `PlayerMotor.FindFocus`가 실제 가림과 함께 판정한다. `Interactable.cs`는 문짝 이동·점유·소리의 기존 소유자다. | `corridor`, `ai-doors`, `ui` |
| 큰 몬스터의 벽 겹침·걷기/추격 동작 | `AI/EnemyBodyClearance.cs`는 렌더링 몸체 크기의 트리거 쿼리(상호작용·NavMesh 베이크 제외)로 실제 NavMesh 이동을 보정한다. `AI/EnemyLocomotionPose.cs`는 원본 리그·클립 위에 복원 가능한 팔다리 동작을 더한다. `V1MonsterMotion.cs`가 이동 속도·상태·공격·일시정지를 연결한다. | `readability`, 문/경로 변경 시 `ai-doors` |
| 획득 아이템 테두리·촛불 위기 소등 | `PickupOutline.cs`와 `Resources/PickupOutline.shader`는 실제 획득 가능/표시 상태와 깊이 검사를 따른다. `CandleDanger.cs`는 가까운 몸체와 공격을 판정하고 `WaymarkCandle.cs`는 점화 기억을 보존해 자동 복원한다. | `readability`, 저장 변경 시 `checkpoint` |
| 베이비·적별 감각 | `StalkerCorridorBaby.cs`, `CorridorBabyBehaviour.cs`, `Core/CorridorBabyMemory.cs`, `Core/CorridorThreatRules.cs`. 운캣과 학교 화캣의 정체성은 별도다. | `core`, `ai` |
| 서랍·가구 상호작용 | `CorridorDrawer.cs`, `CorridorFurniturePlacement.cs`, `CorridorFurnitureLibrary.cs`, `PlayerMotor.cs`. 움직이는 서랍과 플레이어 충돌·상호작용 거리를 확인한다. | `drawer` |
| 회랑 생성·방·제단 | `CorridorLayout.cs` / `CorridorRun.cs`의 새 지도 버전은 5다. `Core/CorridorShelterRules.cs`는 한 문짜리 막다른 방과 우회로 위의 두 문짜리 방을 선택한다. `CorridorShelterRoom.cs`는 각 방의 실제 문 목록을 연결한다. 기존 지도 1~4는 같은 버전으로 재구성한다. | 방·출구 선택/저장 변경은 `shelter`, 일반 회랑은 `corridor`, 전체 연결망 변경 시 `full` |
| 운냥이 배회·추격 종소리 | `UncatBellVoice.cs`는 실제 움직임·추격 상태와 녹음 기반 공간 음원을 연결한다. `Core/EnemyBellClock.cs`는 배회/추격 간격과 음량을 판정한다. 제작 원본·비교는 `SourceArt/Audio/render_uncat_bell.py`와 `uncat-bell-selection.json`에 있다. | `core`, `corridor`, 기존 무음 음향 검사 |
| 한국적인 소품 | `KoreanCorridorDressing.cs`는 위치 데이터만 받아 중앙 경로·가구·문 이동을 피한다. `KoreanCorridorProp.cs`는 자산/구역 정체성을 제공한다. `SourceArt/KoreanCorridorProps/`에는 웹앱 GLB/PBR·입력·프롬프트·Blender·LOD 재생성 도구가 있다. | `corridor`, `readability`, `full`, 새 플레이어 표면 확인 |
| 학교 공간·첫 등장 | `SchoolCampusLayout.cs`, `SchoolCampusArchitecture.cs`, `SchoolCampusRooms.cs`, `SchoolFirstAppearances.cs`, 각 등장 이벤트. | `ai`, 공간/저장 버전 변경 시 `full` |
| 저장·재개·게임 진행 | `GameSession.cs`, `CorridorRunCheckpoint.cs`, `ChapterRunCheckpoint.cs`, 각 `*CheckpointStore.cs` / `*Checkpoint.cs`. 체크포인트 partial은 기존 저장 형식을 연결하는 어댑터다. | `core`, `checkpoint` |
| 소리·렌더링·자산 | `ExternalAudio.cs`, `EnemyAcoustics.cs`, `GraphicsLightingPresentation.cs`, `EnemyStudioVisual.cs`와 각 자산의 SourceArt/ThirdParty 안내. | 해당 기능 검사 및 기존 무음 네이티브 진단; 공통 변경 시 `full` |

`RoomAmbience.ConfigureRunPositions`는 위치와 회랑/학교 모드를 명시적으로 받는다.
학교의 `ChapterAtmosphere`와 `SchoolCampusRooms`는 학교 모드를 전달한다. 위치
배열이 있다는 이유만으로 학교 음원을 회랑 음원으로 교체하지 않는다.

## 모듈 경계

- `Assets/Scripts/Core/HappyToy.V2.Core.asmdef`: System만 사용하는 독립 규칙 10개.
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
모두 실행한다. 필터가 0개 검사를 선택하거나 검사 실패·건너뜀·결과 누락이 있으면
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
도깨비 기와와 숨는 방을 캡처한다. 프로세스 무음 가드에서 시작하며, 표면 검사는
가드 실패 시에도 리스너 0을 유지한다. 일반 플레이에는 설치되지 않는다. 이 검사는
생존 난이도나 최종 음향 믹스를 인증하지 않는다.

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

```powershell
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.QualityValidation.BuildWindows -v2-release-player -v2-build-output Builds/Windows-Structure
```

스테이징 뒤 필수 소스는 `python HappyToyV2/Tools/quality/check_source_completeness.py`로
확인한다. 소스 검사는 Unity 컴파일·통합 검사·실제 플레이어 빌드 성공을 대신하지 않는다.
변경 범위에 대한 검사로 문제가 해결되면 새 근거 없이 같은 전체 검사를 반복하지 않는다.
