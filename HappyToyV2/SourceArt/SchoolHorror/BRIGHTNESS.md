# 학교 인게임 밝기와 검토 화면 일치 · 2026-10-22

이동 중 광원 누락 수정은 `LightContinuity/README.md`에 있다. 2026-10-23에는
8개 한도에서도 남은 천장 빛 단절을 수정하기 위해 학교 전용 Forward+로 전환했다.
기존 정상 카메라의 Tone/노출 수치를 유지하며, 학교 종료 시 원래 파이프라인을 복원한다.

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
전체 가중 sRGB 평균 **0.038–0.057**, 가까운 왼쪽 벽 **0.065–0.115**를 요구한다.
작은 차이는 실제 형광등 전압 저하·해상도/AA에 허용하되 검게 눌린 이전 화면은 실패한다.
2026-10-23 Editor의 형광등 위상 변동을 반영해 평균 상한을0.055에서0.057로
조정했으며, 기존 native 검사 상한0.057과 일치시켰다. 최소 가시성 기준은 유지한다.
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

## Windows 광원 누락 복구 · 2026-10-23

학교 조명/노출 수치는 같아도 Windows의 벽과 바닥이 검게 나오는 회귀를 재현했다.
실제 기본 Forward+ 경로는 평균0.00435895/벽0.00253050, 같은 빌드·광원·노출의 명시적
Forward8 대조군은0.04225/0.08201이었다. 정상 학교의 cluster keyword와57개 광원
버퍼가 활성인 반면, 최종 shader compiler 감사에서는 D3D11/D3D12 모두 학교 표면의
`_CLUSTER_LIGHT_LOOP` 변형이0개였다. Editor 정상 카메라는0.04772/0.08475로 통과했다.

Unity6000.6의 엔진 `ShaderKeywordFilterUtil.GetKeywordFilterVariants`는 native
`QualitySettings.GetAllRenderPipelineAssetsForPlatform` 목록을 직접 읽는다.
URP의 `IncludeAdditionalRPAssets` 라벨 수집과는 별개여서 라벨만으로 등록한 학교
파이프라인의 cluster 변형이 먼저 제거됐다. `SchoolLightingSetup`은 예약 품질 단계
`School runtime shader variants`를 끝에 한 번만 추가하고 학교 파이프라인을 등록한다.
기존 품질 단계의 index·기본값·현재 선택과 공통 Forward 렌더러를 유지한다.
학교 종료 시의 파이프라인 복원 및 승인 Neutral/−0.1EV/대비0도 유지한다.

단일 EditMode 검사 `SchoolNativeShaderPrefilterIncludesBothPipelinesWithoutChangingExistingQualityChoices`가
native 수집기의 두 파이프라인 포함, 기존 선택 유지와 반복 실행 중복 방지를 확인한다.
옵션 `HAPPYTOY_SCHOOL_SHADER_AUDIT_DIR`를 Builds 하위 새 폴더로 지정한 빌드만
`SchoolShaderVariantAudit`가 최종 컴파일 직전 변형 개수를 기록한다. 캐시 때문에
callback이 없으면 미관측으로 기록하며 보존 성공으로 간주하지 않는다.
`SchoolHorrorReviewProbe`는 실제 renderer/keyword/cluster 버퍼도 기록한다.
`-v2-school-horror-forward-control`은 격리된 비교 실험 전용으로 일반 학교에 적용되지 않는다.
기본 Forward+의 새 Windows 검토는 **PASS**이며, 손전등을 끈 승인 시점의 평균은
**0.045082584**, 가까운 벽은 **0.083471261**로 복구됐다. Neutral/−0.1EV/대비0,
실제 ForwardPlus renderer와 cluster keyword를 유지한 결과다. 전후 보고서는
`Builds/Verification/SchoolBrightness-20261023-before/school-horror-review.json`과
`Builds/Verification/LightingRestore-20261023-school/school-horror-review.json`이다.
이 밝기 검토는 반사 프로브 완료나 자연 플레이 FPS를 인증하지 않는다.

공식 규칙: https://docs.unity3d.com/6000.6/Documentation/ScriptReference/ShaderKeywordFilter.FilterAttribute.html
