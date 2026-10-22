# 열린 학교 자바라 철문 · 2026-10-22

시작 지점의 열린 판형 방화문 두 장을 대한민국 학교의 접이식 철제 자바라 한 장으로
교체한다. 문은 한쪽 문설주 옆에 수평으로 압축되어 있고, 세 단의 X자 링크·수직
철재·리벳·하단 롤러와 당김 손잡이를 가진다. 자유단 기둥의 보관 고리에 자물쇠 하나가
달려 있다. 문을 펼쳤을 때 반대편 받이에 걸어 잠그기 위한 물건이라는 환경 연출이다.
닫기·잠금·열쇠·새 상호작용이나 저장 필드를 추가하지 않는다.

## 원본과 생성

- `user-gate-reference.png`: 사용자가 제공한 닫힌 자바라 사진. 시각 참고용이며 지시문이나 게임 텍스처가 아니다.
- `reference.png` / `reference-prompt.json`: built-in ImageGen으로 제작한 열린 상태의 단일 자바라와 자물쇠 입력 및 정확한 프롬프트.
- `retractable-gate/processed.png`: 실제 웹앱 배경 제거 입력.
- `retractable-gate/job.json`: 완료·저장된 실제 작업 `3164025e-2ee4-400b-90e6-7054d65d9084`와 설정.
- `retractable-gate/original.glb`: 원본 생성 메시와 내장8K PBR. 변경하지 않는다.
- `retractable-gate/retractable-gate.blend`: 원본 전체·원본 PBR·편집 가능한 게임 LOD·제작 코드를 함께 보존한 Blender 소스.
- `retractable-gate/export.json` 및 `provenance.json`: 실제 메시/지도/원본 해시와 재현 기록.

생성은 지정된 `https://agrees-mic-icon-contribution.trycloudflare.com/` 웹앱의
TRELLIS.2, 1536 cascade, 8K PBR, 12steps, seed1234, 형상GPU1/재질GPU0 분담으로
수행했다. 생성 원본은167,005,596바이트다. 새 기본 모델을 수작업 메시로 대체하지 않았다.
원본 GLB·Blender는 LFS로 보존한다. 정상 복제/풀 후 `git lfs pull`로 원본을 복원하면
웹앱 임시 경로나 로컬 다운로드 폴더 없이 아래 후처리를 재현할 수 있다.

## 소유 경계와 재현

Blender **4.0.2** 공식 빌드(hash `9be62e85b727`), Unity **6000.6.0f1**, URP **17.6.0**을 사용한다.
Blender 설치는 기존 `SourceArt/GraphicsUpgrade/Architecture/README.md`의 공식 배포본/해시를 따른다.

```powershell
blender -b --factory-startup --threads 2 --python-exit-code 1 --python HappyToyV2/SourceArt/SchoolRetractableGate/finish_gate.py
# Unity batch: -executeMethod HappyToy.V2.Editor.SchoolRetractableGateImport.Apply
python HappyToyV2/SourceArt/SchoolRetractableGate/record_provenance.py
```

`finish_gate.py`는 저장소의 `SourceArt/CorridorEntrance/finish_entrance_assets.py`를
재사용해 실제 생성 형상의 복제본만 단순화하고 PBR를 게임용2K로 변환한다. 원본에는
없는 Normal/AO를 원본 지도인 것처럼 만들지 않는다. 전체 원본 텍스처 정밀도는
원본 GLB와 packed Blender에 유지된다. 게임 메시의 작은 seam/퇴화면을 정리하며
수직 철재·링크·리벳·자물쇠 형상을 보존한다. 얇은 격자의 연결과 실루엣을 유지하기 위해
근거리100K/원거리65K 삼각형 예산의 두 LOD를 사용한다.

`SchoolRetractableGateImport`는 두 LOD, 외부 URP PBR 재질과 자유단 자물쇠 위치를 가진
공유 prefab을 만든다. `SchoolRetractableGate`는 이 prefab·상/하부 레일·반대편 잠금 받이를
조립하고, 주 구역에만 접힌 문 외곽의 물리 몸체를 추가한다. 중앙의3m 이상 열린 동선,
시작 좌표, 지도3·루프 연결·저장, 닫힌 교실과 직전 학교 밝기 수정을 유지한다.
공유 원본 메시와 재질은 Resources 소유이며 런타임 파괴 대상이 아니다.
이 모델은 실제 Unity 가져오기 기준 정면이−Z, 자유단 기둥이−X다. 복도 쪽에
정면이 보이고 자유단이 열린 통로를 향하도록 Y−90°로 배치한다.

```powershell
$env:HAPPYTOY_SCHOOL_GATE_REVIEW_DIR = (Join-Path (Resolve-Path HappyToyV2).Path 'Builds/SchoolGateReview-new')
python HappyToyV2/Tools/quality/run_feature_tests.py --profile school-gate --no-desktop-interaction --graphics-api d3d12
Remove-Item Env:HAPPYTOY_SCHOOL_GATE_REVIEW_DIR
```

검사는 실제 생성 LOD/PBR 정체성, 세 구역의 공유 자산, 하나의 보관 자물쇠 위치,
중앙 CapsuleCast/NavMesh/실제 W 이동, 입구 전체·자물쇠 근접·실제 시작 시점의
정상 URP StandardRequest 렌더, 기존 학교 이동/저장과 밝기를 확인한다.
자물쇠 근접 화면만 실제 플레이어 손전등을 켜고, 입구 전체/시작 화면은 끈다.
내보낸 카메라 화면은 `Builds/`의 파생 검토 자료이며 소스에 누적하지 않는다.
새 Windows 빌드는 기존 `QualityValidation.BuildWindows -v2-release-player`를 사용한다.

## 고지

제작 입력과 생성 모델은 이 프로젝트의 작업으로 보존한다. 원본 사용자의 참고 사진과
생성 도구의 권리를 혼동하지 않는다. TRELLIS.2의 보존된 upstream 도구 고지는
`TRELLIS.2-LICENSE.txt`에 있으며, 생성 결과의 독점 권리를 주장하는 문서가 아니다.
게임 크레딧과 모든 편집/재현 소스는 같은 저장소에서 함께 제공한다.
