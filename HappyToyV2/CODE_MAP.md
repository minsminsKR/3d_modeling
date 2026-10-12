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
| 타이틀·게임 시작 전환 | `GameShellTitleView.cs`, `CinematicTitleScene.cs`, `GameShellStartTransition.cs`. UI 모듈 변경도 실제 로딩 중 입력 차단과 타이틀 접근 연출을 보존한다. | `ui` |
| 적의 시야·청각 | `StalkerBrain.cs`가 게임 상태를 조정하고 `AI/StalkerPerception.cs`가 실제 가림·층·소리 경로의 증거를 반환한다. | `core`, `ai` |
| 공격·캐비닛 은신 | `AI/StalkerAttack.cs`는 공격 예고/회복과 예고 시 확정한 표적을 소유한다. `Core/EnemyAttackClock.cs`, `CabinetHidingRules.cs`는 독립 규칙이다. `PlayerMotor.cs`, `Interactable.cs`는 실제 은신 입출력을 연결한다. | `core`, `ai`, 저장 변경 시 `checkpoint` |
| 적의 문 통과·추적 경로 | `AI/StalkerDoorPassage.cs`는 문 통과와 제한된 대기를 소유한다. `EnemyNavigation.cs`, `StalkerDoorTraversal.cs`는 실제 NavMesh/층 경로를 처리한다. | `ai-doors` |
| 큰 몬스터의 벽 겹침·걷기/추격 동작 | `AI/EnemyBodyClearance.cs`는 렌더링 몸체 크기의 트리거 쿼리(상호작용·NavMesh 베이크 제외)로 실제 NavMesh 이동을 보정한다. `AI/EnemyLocomotionPose.cs`는 원본 리그·클립 위에 복원 가능한 팔다리 동작을 더한다. `V1MonsterMotion.cs`가 이동 속도·상태·공격·일시정지를 연결한다. | `readability`, 문/경로 변경 시 `ai-doors` |
| 획득 아이템 테두리·촛불 위기 소등 | `PickupOutline.cs`와 `Resources/PickupOutline.shader`는 실제 획득 가능/표시 상태와 깊이 검사를 따른다. `CandleDanger.cs`는 가까운 몸체와 공격을 판정하고 `WaymarkCandle.cs`는 점화 기억을 보존해 자동 복원한다. | `readability`, 저장 변경 시 `checkpoint` |
| 베이비·적별 감각 | `StalkerCorridorBaby.cs`, `CorridorBabyBehaviour.cs`, `Core/CorridorBabyMemory.cs`, `Core/CorridorThreatRules.cs`. 운캣과 학교 화캣의 정체성은 별도다. | `core`, `ai` |
| 서랍·가구 상호작용 | `CorridorDrawer.cs`, `CorridorFurniturePlacement.cs`, `CorridorFurnitureLibrary.cs`, `PlayerMotor.cs`. 움직이는 서랍과 플레이어 충돌·상호작용 거리를 확인한다. | `drawer` |
| 회랑 생성·제단 | `CorridorLayout.cs`, `CorridorRun.cs`, `CorridorRunAltar.cs`, `CorridorAltarChamber.cs`. 저장된 지도 버전과 생성 순서를 보존한다. | `checkpoint`, 전체 공간 변경 시 `full` |
| 학교 공간·첫 등장 | `SchoolCampusLayout.cs`, `SchoolCampusArchitecture.cs`, `SchoolCampusRooms.cs`, `SchoolFirstAppearances.cs`, 각 등장 이벤트. | `ai`, 공간/저장 버전 변경 시 `full` |
| 저장·재개·게임 진행 | `GameSession.cs`, `CorridorRunCheckpoint.cs`, `ChapterRunCheckpoint.cs`, 각 `*CheckpointStore.cs` / `*Checkpoint.cs`. 체크포인트 partial은 기존 저장 형식을 연결하는 어댑터다. | `core`, `checkpoint` |
| 소리·렌더링·자산 | `ExternalAudio.cs`, `EnemyAcoustics.cs`, `GraphicsLightingPresentation.cs`, `EnemyStudioVisual.cs`와 각 자산의 SourceArt/ThirdParty 안내. | 해당 기능 검사 및 기존 무음 네이티브 진단; 공통 변경 시 `full` |

## 모듈 경계

- `Assets/Scripts/Core/HappyToy.V2.Core.asmdef`: System만 사용하는 독립 규칙 7개.
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
python HappyToyV2/Tools/quality/run_feature_tests.py --profile ui --dry-run
python HappyToyV2/Tools/quality/run_feature_tests.py --profile drawer
python HappyToyV2/Tools/quality/run_feature_tests.py --profile full
```

`core`는 빠른 EditMode 규칙 검사, `ui` / `ai` / `ai-doors` / `drawer` / `checkpoint` / `readability`는
해당 실제 동작의 PlayMode 검사다. `full`은 필터 없는 EditMode와 PlayMode를
모두 실행한다. 필터가 0개 검사를 선택하거나 검사 실패·건너뜀·결과 누락이 있으면
성공으로 처리하지 않는다. `--dry-run`은 명령을 확인하며 검사를 통과한 것으로
표시하지 않는다. 결과 XML과 프로필 보고서는 무시되는 `Builds` 아래에 저장한다.
각 검사에 독립 Editor 로그를 남기며 실패한 검사 수와 누락된 선택자도 요약한다.
Windows Editor의 GPU 오류를 진단할 때는 `--graphics-api d3d11` 또는 `d3d12`를
명시할 수 있다. 이 옵션은 해당 검사 프로세스에만 적용하며 프로젝트/플레이어
설정을 바꾸지 않는다. 다른 API에서의 통과는 기본 API의 렌더링 통과를 뜻하지 않는다.

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
