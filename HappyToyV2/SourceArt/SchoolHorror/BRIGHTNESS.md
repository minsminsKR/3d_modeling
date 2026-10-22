# 학교 인게임 밝기와 검토 화면 일치 · 2026-10-22

사용자 피드백 원본과 SHA256은 `References/ingame-brightness-feedback.*`에 보존한다.
이전 승인 복도 렌더의 가중 sRGB 평균은 **0.04579**, 가까운 왼쪽 벽은 **0.08486**이다.
사진 전체를 게임 텍스처로 사용하거나 UI가 다른 두 화면의 전체 평균을 직접 비교하지 않는다.

## 원인과 소유 경계

기존 검토 함수는 URP `SingleCameraRequest`를 사용했다. 고정 URP **17.6.0**의
이 경로는 정상 카메라 스택의 `UpdateVolumeFramework`를 실행하지 않아 앞서 렌더한
카메라의 Volume 상태에 의존했다. 같은 생산 카메라의 실측은 기존 요청 **0.048406**,
일반 스택의 `StandardRequest` **0.006493**이었다. 일반 스택에서는 학교의 ACES,
contrast10, exposure+0.1이 실제로 적용되어 그림자가 심하게 눌렸다.

`SchoolHorrorLighting`이 학교 전용 프로필을 **Neutral / exposure−0.1EV /
contrast0 / saturation−18**로 설정한다. 형광등·창문·손전등 출력, 그림자 예산,
공통 파이프라인, 다른 게임 모드와 사용자 설정은 이 수정의 대상이 아니다.
카메라 밖 환경 복원·pause·소유 리소스 정리는 기존 경계를 유지한다.

공개하는 학교 검토 이미지는 `SchoolHorrorFrame`의 **StandardRequest**로 촬영한다.
Windows의 `SchoolHorrorReviewProbe.Pose`도 같은 정상 스택을 사용한다. 검토에서만
밝기를 올리거나 출력 이미지에 감마/노출 필터를 더하지 않는다.

## 회귀 검사와 재현

`SchoolNormalCameraStackKeepsTheApprovedPreviewBrightness`는 실제 학교를 만들고
손전등을 끈 뒤 원래 승인 시점에서 정상 스택을 렌더한다. 먼저 다른 카메라처럼
Volume mask0 상태를 설치해, 검토가 이전 카메라 상태를 재사용하는 회귀도 검출한다.
전체 가중 sRGB 평균 **0.038–0.055**, 가까운 왼쪽 벽 **0.065–0.115**를 요구한다.
작은 차이는 실제 형광등 전압 저하·해상도/AA에 허용하되 검게 눌린 이전 화면은 실패한다.
수정 후 전체 관련 검사는87 EditMode·12 PlayMode가 통과했으며, 같은 시점의 정상
스택 평균은0.046280, 가까운 벽은0.084887이었다.
이는 표시 FPS/자연 플레이 또는 OS의 HDR 디스플레이 검증이 아니다.

```powershell
$env:HAPPYTOY_SCHOOL_HORROR_REVIEW_DIR = (Join-Path (Resolve-Path HappyToyV2).Path 'Builds/SchoolBrightness-new')
python HappyToyV2/Tools/quality/run_feature_tests.py --profile school-brightness --graphics-api d3d12 --no-desktop-interaction
# 학교 전체 관련 회귀와9시점 촬영
python HappyToyV2/Tools/quality/run_feature_tests.py --profile school-horror --graphics-api d3d12 --no-desktop-interaction
Remove-Item Env:HAPPYTOY_SCHOOL_HORROR_REVIEW_DIR
```

Windows 빌드의 조용한 밝기 검토는 기존 명시 옵션에
**`-v2-school-horror-brightness-only`**를 추가한다. 새 격리 프로필과 현재 프로세스만의
음소거 guard, batch/cursor 억제, 정상 URP 스택과1280×720 sRGB 타깃을 사용한다.
반사 프로브의 실제 완료 상태를 별도로 기록하며, 이 모드에서 미완료 박스 프로브를
완료로 간주하지 않는다. 미완료 프로브가 diffuse/톤매핑 측정을 막지는 않는다.
실제 밝기/벽 가시성 범위와 오류 셰이더 검사에 성공해야만 PASS다.
일반 플레이에는 촬영·설정 저장·자동 시작을 추가하지 않는다.

```powershell
& '<fresh-build>/HappyToyV2.exe' -batchmode -quiet-diagnostics -v2-audit-no-desktop-interaction -v2-school-horror-review-output '<new-evidence-folder>' -v2-school-horror-brightness-only -logFile '<new-evidence-folder>/Player.log'
```
