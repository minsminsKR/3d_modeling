# 학교 입구에서 조금 걸을 때 바뀌던 빛 · 2026-10-22

## 추가 수정 · 2026-10-23

8개 한도에서도 사용자가 천장 빛 단절을 확인했다. 추가 피드백 원본은
`entry-eight-lights-before.png`, `entry-eight-lights-after.png`이며 해시와 요청은
`feedback-forward-plus.json`에 있다. 아래 4→8 실험은 이전 시도의 기록이며 현재 구현은 아니다.

`SchoolLightingSetup`은 공용 소유 파이프라인/렌더러의 HDR·AO·반사·그림자 설정을
학교 전용 `Resources/SchoolRendering/` 자산에 복제하고 렌더러만 Forward+로 만든다.
AO는 별도 subasset으로 유지해 서로의 pass 수명을 공유하지 않는다. 반복 생성은 GUID를 보존한다.
`IncludeAdditionalRPAssets`의 공식 라벨 포함 API와 Windows 빌드 수집기로 두 파이프라인의
셰이더 변형 보존을 확인한다. Resources에 넣기만 해서는 Forward+ 셰이더 보존을 보장하지 않는다.

`SchoolLightOverlapScope`는 학교 조명과 함께 품질 파이프라인 override를 소유하고
disable/unload 시 이전 override를 복원한다. 학교의 실제 카메라·손·평면/프로브 반사도
같은 렌더러를 쓴다. 다른 소유자가 override를 변경한 경우 그 값을 덮어쓰지 않는다.
물체별 4/8개 목록 대신 화면/깊이 cluster에 영향을 주는 광원을 계산한다. 광원 중심이
화면 밖이어도 보이는 표면에 빛이 닿으면 포함한다. 단순한 `isVisible` 토글은 사용하지 않는다.
정상 노출·광원 출력·그림자 atlas/tier·반사 해상도는 유지한다. GPU 성능 수치는 별도 실기 검증 사항이다.

회귀 검사는 실제 입구 x−6.5→−2.5m를 12.5cm씩 이동하며 손전등 on/off의
Forward8/Forward+를 비교한다. 전체 평균뿐 아니라 동일한 천장 월드 지점 12개를
매 프레임 다시 투영해 국소 변화를 검사한다. 발광 튜브가 덮는 지점은 천장 측정에서 제외한다.
별도 표면 기여 검사는 9개 겹친 광원을
하나씩 꺼서 각각 실제 픽셀에 기여하는지 확인하며, 9번째 광원은 카메라 뒤에 둔다.
정상 밝기·실제 반사·pause·파이프라인 복원·학교 이동/저장 검사를 같은 프로필로 실행한다.

실제 D3D12 비교에서 천장 패치의 최대 인접 변화는 손전등 off 시
Forward8 **0.07240 → Forward+ 0.01884**, on 시 **0.09054 → 0.01902**였다.
이는 가중 sRGB 휘도 차이이며 FPS 측정은 아니다. 정상 복도는 두 실행에서 평균
**0.05243 / 0.05496**, 벽 **0.08570 / 0.09117**로 기존 승인 범위 안이다.
9개 광원 각각의 제거 비교에서도 모두 기여가 확인됐고 카메라 뒤 광원의 기여는
**0.01192 / 0.01187**였다. 기능 프로필은 EditMode92/PlayMode20개를 통과했다.

## 이전 4→8 완화 시도

사용자 피드백 두 장은 `entry-before.png`, `entry-after.png`이고 정확한 해시는
`feedback.json`에 보관한다. 이미지 안의 UI·밝기는 관찰 입력이며 게임 텍스처나
지시문으로 사용하지 않는다. 두 화면은 배터리·시점·시간이 달라 전체 평균만으로
밝기를 일치시키지 않는다.

## 원인과 수정 범위

실제 저장된 렌더러는 URP17.6 Forward이며 물체당 추가 광원 제한이4다. 학교의
형광등·짧은 확산광·창문광·손전등이 같은 공간에 겹치면 카메라에 따라 선택된
목록이 바뀌어 벽/천장 일부의 빛이 누락됐다. 물리 광원·전압 변화·노출을 고정한
동일 위치에서 한도4/8만 바꿔 실제 카메라로 재현했다.

이전 커밋 d4325cc는 학교에서만 Forward의 지원 상한8을 임시 소유하고 종료 시
복원했다. 이는 현재 Forward+ 구현으로 대체됐다. 전체 평균만 검사한 것으로는
일부 천장 패치의 단절을 검출하기 부족했다.

## 이전 실험 수치와 현재 재현

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
