# 학교 진행 디지털 시계

입구의 맞은편(동쪽) 벽에 낡은 디지털 벽시계를 설치한다. 학교를 새로 시작하면
`00:00`이며, 현재 이상현상 없는 관찰 단계에서는 앞쪽 연결 평면을 걸어 통과할 때
한 시간 증가한다. 뒤쪽 연결은 시간을 유지한다. 현실 시간·대기·일시정지·같은
평면 근처의 머무름은 진행 시간을 증가시키지 않는다. 8회 탈출·이상현상·실패 리셋은
아직 구현하지 않는다. 24회 이후에도 누적 시간을 표시하며 자정으로 되감지 않는다.

`Core/SchoolProgressClock`이 전진 증가와 저장 범위를 소유한다.
`SchoolLoopWorld`는 기존 연결 판정 결과를 전달하고 세 구역 시계를 동기화한다.
`SchoolProgressClockDisplay`는 공유 생성 원본 prefab과 숫자 변경 때만 갱신하는
작은 7세그먼트 메시를 소유한다. 추가 광원을 만들지 않으며 LED는 전용 URP 셰이더로
주변 조명과 무관하게 읽힌다. 메시를 챕터 해제 시 파괴한다. 모델에는 고정 숫자가 없다.

저장은 지도 버전 3을 유지하고 선택적인 `loop.clock.hours`를 추가한다.
이 필드가 없는 구형 저장은 양방향 통과 수를 유지하면서 시계를 `00:00`으로 복원한다.
구형 `traversals`는 양방향 횟수이므로 성공 진행 시간으로 추측 변환하지 않는다.

## 원본과 재현

지정 A100 서비스 `https://agrees-mic-icon-contribution.trycloudflare.com`의
모델링 CLI를 사용한다. 요청 `791d5129-4cc1-466c-9b50-dfd870ee55bd`,
작업 `ed7b69e1-6a28-4779-95eb-7f902a9889d1`이다.
TRELLIS.2 / 1536 cascade / 8K PBR / 12 steps / seed 1234 / split GPU,
배경 제거 켜짐을 명시한다. 실제 적용 옵션과 입력/출력 SHA-256은 보존된 작업 기록이 기준이다.

- `reference.png`, `reference-prompt.json`: built-in ImageGen 입력 및 정확한 프롬프트.
- `clock/original.glb`: 지정 서비스의 변경하지 않은 실제 생성 원본과 내장 PBR.
- `clock/generation-record.json`, `request.json`: 실제 요청/작업 및 입력·결과 기록.
- `clock/processed_inputs/01.png`: 실제 배경 제거 뒤 모델에 전달된 입력.
- `clock/outputs/`, `clock/inputs/`: archive의 추가 생성 기록과 원본 입력.
- `clock/clock.blend`: 원본 컬렉션과 packed PBR, 편집 가능한 파생 LOD 및 마감 코드.
- `clock/export.json`, `provenance.json`: 게임 내보내기, 예산·경계·해시·재현 기록.

Blender **4.0.2**, Unity **6000.6.0f1**, URP **17.6.0**을 사용한다.
공식 Blender 배포/고정 해시는 기존 `SourceArt/GraphicsUpgrade/Architecture/README.md`를 따른다.
저장소 루트에서:

```powershell
blender -b --factory-startup --threads 2 --python-exit-code 1 --python HappyToyV2/SourceArt/SchoolProgressClock/finish_clock.py
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.SchoolProgressClockImport.Apply
python HappyToyV2/SourceArt/SchoolProgressClock/record_sources.py
```

`finish_clock.py`는 저장소의 기존 `SourceArt/CorridorEntrance/finish_entrance_assets.py`의 경계/삼각형/저장 도우미와 `SourceArt/SchoolLoop/rebake_music_lod.py`를 재사용한다.
생성 원본의 복제본만 폭 1.4m, 12,000 / 8,000 삼각형 상한의 두 LOD와
게임용 1K PBR로 마감한다. 원본에 없는 Normal/AO를 원본 지도라고 만들지 않는다.
양쪽 LOD의 색/ORM을 원본 표면에서 새 UV로 재베이크해 감면 뒤 늘어진 무늬를 방지한다.
얇은 케이스 면을 유지하기 위해 양쪽 모두 원본 기반 경계 감면을 사용하고 voxel 재구성을 적용하지 않는다. 원본은 변경하지 않는다.
기본 형상을 바꿔야 한다면 같은 지정 서비스에서 재생성한다.
정적인 벽시계이므로 리그·스킨·애니메이션은 필요하지 않다.

```powershell
$env:HAPPYTOY_SCHOOL_CLOCK_REVIEW_DIR = (Join-Path (Resolve-Path HappyToyV2).Path 'Builds/SchoolClockReview-new')
python HappyToyV2/Tools/quality/run_feature_tests.py --profile school-clock --no-desktop-interaction --graphics-api d3d12
Remove-Item Env:HAPPYTOY_SCHOOL_CLOCK_REVIEW_DIR
```

검사는 실제 W 입력 전진 2회/후진 1회, 일시정지, 저장/복원 및 구형 저장,
세 구역의 원본 메시/PBR/LOD 공유, 정상 카메라 LED 화면 기여와 해제 자원 정리를 포함한다.
검토 PNG/실행 로그/다운로드 ZIP과 플레이어 빌드는 파생물이므로 `Builds/` 아래에 둔다.
에디터 화면 검사는 네이티브 지속 FPS를 인증하지 않는다.

## 고지

이 입력 이미지와 생성 자산은 프로젝트 작업물로 보존한다. 생성 도구의 라이선스와
생성 결과의 권리를 혼동하지 않는다. 보존된 TRELLIS.2 고지는
`SourceArt/SchoolRetractableGate/TRELLIS.2-LICENSE.txt`를 참조한다.
원본 GLB와 Blender는 저장소 Git LFS 규칙을 따르며, clone/pull 후 `git lfs pull`로 복원한다.
개인 CLI 상태·인증 쿠키·비밀번호는 소스에 포함하지 않는다.
