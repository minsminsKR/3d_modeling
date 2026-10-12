# 백수귀 불규칙한 머리·게임 시작 로딩 연출 — 2026-10-12

백수귀의 `Head_Writhe`를 61초의 불규칙한 방향 전환·멈춤 동작으로 교체했다.
Roll은 양쪽으로 약 55도까지 기울어진다. 난수 seed, 회전 범위, 타이밍,
편집 가능한 Blender action, FBX와 Unity 회전 clip을 함께 보존한다.
원래 Run action의 curve hash는 변경되지 않았다.

첫 메뉴의 새 회랑·폐교 시작 버튼은 준비 coroutine을 실행한다. 실제 월드 생성,
방·가구·장식 생성과 메시 결합을 프레임별로 나누고 NavMesh는 비동기로 굽는다.
시간 배율이 0인 로딩 중에도 메뉴의 백수귀가 원래 보행 clip으로 카메라 쪽에
서서히 접근하며, 입력은 월드 준비가 끝나기 전까지 잠긴다. 준비가 끝나면
플레이 화면으로 전환한다. 로딩을 늘리기 위한 고정 대기 시간은 넣지 않았다.
동작 감소에서는 메뉴의 머리 회전을 멈추고 접근 속도를 줄인다.

즉시 준비 API는 기존 체크포인트·진단 fixture의 호환성을 위해 같은 생성 단계를
동기적으로 실행한다. 이번 접근 연출은 첫 메뉴의 **새 회랑 시작 / 폐교의 기억**에
적용되며, 이어하기·플레이 중 재시작·초기 앱 실행의 장면 로딩은 별도 흐름이다.

## 검증

최신 관련 사례 9개 통과. 초기 사망·재시작 회귀 검사에는 이전 UI 캡처 이름과
이미 교체된 노이즈 텍스처 8개를 기대하는 오래된 기준이 남아 있었다.
실제 캡처 대상과 현재 소유한 노이즈·주변부 텍스처 기준으로 고친 뒤 재실행은 통과했다.
로딩의 실제 측정은 회랑 약 6.7초 / 99회 화면 갱신, 학교 약 1.3초 / 18회 갱신이다.
최장 로딩 프레임은 각각 약 0.33초 / 0.30초였으며, 무중단 프레임률을 보장하는 기록은 아니다.
손끝 지원면으로 몸체의 보행 높이를 맞추고, 큰 기울임 중 머리 장식이 바닥으로
내려가는 경우 머리만 들어 올려 바닥 관통을 피한다. PNG 표본의 실제 피부 바닥 간격은
-1.5cm~3cm 허용 범위 안이었다. 현재 소스 입력 2,877개와 Windows 빌드 해시가 일치한다.
정리한 결과는 `verification.json`에 있다.


- `playmode-final.xml`: 머리 Roll, 기존 2.4초 주기와의 차이, 머리 외 관절 보존,
  61초 clip의 181개 표본 피부 바닥·높이 범위, 실제 학교 메뉴 시작,
  회랑 생성 중 접근·보행·입력 차단·중복 시작 방지, 일시정지·추격 종료를 검사한다.
- `recovery-regression.xml`: 실제 사망·재시작·메뉴 반복 후 UI 및 렌더 대상 소유권 검사.
- `native-head/baeksugwi-head.json`: Windows 플레이어에서 실제 메뉴와 제어된 회랑
  추격 화면, 바닥·천장 범위, 일시정지·추격 종료 검사. 전체 생존 플레이의 증거는 아니다.
- `native-corridor/menu-approach.json`, `native-school/menu-approach.json`: 실제 메뉴
  포인터 경로로 시작해 접근 거리·보행·화면 갱신·최대 로딩 프레임 간격·플레이 진입을 기록한다.
- `build-inputs.json`: 현재 Assets/Packages/ProjectSettings와 새 Windows 빌드의 해시 비교.

자동 검사는 무음으로 실행하고, 메뉴 진단은 출력 디렉터리 안의 격리된 저장 공간을
사용한다. 화면 PNG와 로그는 선택적 로컬 진단 결과이며, 소스 리소스가 아니다.

## 재현

저장소 루트에서 Unity `6000.6.0f1`, Blender `4.0.2`를 사용한다.
모델 생성은 필요하지 않으며 기존 Studio 원본을 그대로 사용한다.

```powershell
& blender --background --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/BaeksugwiStudio/author_head_motion.py
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.BaeksugwiHeadImport.Apply
unity test HappyToyV2 --editor-version 6000.6.0f1 --mode PlayMode --filter Baeksugwi --output HappyToyV2/Verification/menu-approach-20261012/playmode.xml
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.QualityValidation.BuildWindows -v2-build-output Builds/Local-IrregularHeadLoading-20261012 -v2-release-player
```

플레이어 진단에는 `-quiet-diagnostics -v2-menu-approach-output <출력 폴더>`를 전달한다.
학교 검사에는 `-v2-menu-approach-school`도 추가한다. 머리 검사에는
`-quiet-diagnostics -v2-baeksugwi-head-output <출력 폴더>`를 사용한다.
빌드와 출력 경로는 각 실행 환경에서 저장소 기준의 절대 경로로 해석해 전달한다.
