# 학교 입구에서 조금 걸을 때 바뀌던 빛 · 2026-10-22

사용자 피드백 두 장은 `entry-before.png`, `entry-after.png`이고 정확한 해시는
`feedback.json`에 보관한다. 이미지 안의 UI·밝기는 관찰 입력이며 게임 텍스처나
지시문으로 사용하지 않는다. 두 화면은 배터리·시점·시간이 달라 전체 평균만으로
밝기를 일치시키지 않는다.

## 원인과 수정 범위

실제 저장된 렌더러는 URP17.6 Forward이며 물체당 추가 광원 제한이4다. 학교의
형광등·짧은 확산광·창문광·손전등이 같은 공간에 겹치면 카메라에 따라 선택된
목록이 바뀌어 벽/천장 일부의 빛이 누락됐다. 물리 광원·전압 변화·노출을 고정한
동일 위치에서 한도4/8만 바꿔 실제 카메라로 재현했다.

`SchoolLightOverlapScope`는 학교 조명 범위에 들어올 때 한도8을 적용하고, 범위가
꺼지거나 파괴되면 이전 값을 복원한다. 재활성화도 같은 원래 값을 보존한다.
학교에서만 URP Forward의 지원 상한8을 사용하며, 파이프라인 자산을 새 값으로
저장하거나 추가 광원을 만들지 않는다. 기존 광원 출력과 정상 Volume 노출은
유지하고, 추가 그림자 지도2048·손전등1024·지역512, point caster 최대2개와
반사/안개 해상도도 유지한다. GPU에서 한 물체에 계산하는 최대 광원 수는
4에서8로 늘어난다. 표시 FPS의 수치는 별도 실기 측정에 해당한다.

## 확인과 재현

고정된 실제 형광등 아래에서 시작부 x−6.5→−2.5m를25cm씩 이동하고, 실제
StandardRequest 렌더를 손전등 off/on 각각4/8 조건으로 비교한다. 원래4개 조건은
회귀를 드러내는 음성 대조군이며 결과 프레임에 노출·감마 필터를 적용하지 않는다.
초기 관찰에서 한25cm 단계의 전체 가중 sRGB 평균 변화가4개 조건에서0.01297,
8개 조건에서0.00646이었다. 손전등 on은0.01341→0.00630이었다. 투시/실제
PBR 반사의 자연스러운 변화는 유지하며, 원래 광원 선택의 급격한 단절을 검사한다.

```powershell
$env:HAPPYTOY_SCHOOL_STEP_LIGHT_REVIEW_DIR = (Join-Path (Resolve-Path HappyToyV2).Path 'Builds/SchoolStepLights-new')
$env:HAPPYTOY_SCHOOL_HORROR_REVIEW_DIR = (Join-Path (Resolve-Path HappyToyV2).Path 'Builds/SchoolLightingSurfaces-new')
python HappyToyV2/Tools/quality/run_feature_tests.py --profile school-lighting-continuity --graphics-api d3d12 --no-desktop-interaction
Remove-Item Env:HAPPYTOY_SCHOOL_STEP_LIGHT_REVIEW_DIR
Remove-Item Env:HAPPYTOY_SCHOOL_HORROR_REVIEW_DIR
```

프레임·trace·XML·결과 보고서는 무시되는 Builds 하위 검토 자료다. 소스에는
입력 피드백·재현 코드·소유 경계와 검증 프로필을 보관한다. 새 플레이어는 기존
`QualityValidation.BuildWindows -v2-release-player`로 빌드하고 입력 해시를 검증한다.
