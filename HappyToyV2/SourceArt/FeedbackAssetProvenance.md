# 피드백 자산 출처와 재생성 범위

2026-10-06, 기반 `04b4fd99d22ca41fdd76d98914670ca1620f1a13`. 기존 학교 씬·원본 FBX·원본 색상 아틀라스는 보존한다. 새 자산은 별도 v2 파일이며, 아래 경로는 HappyToyV2 프로젝트 기준이다.

## 바닥·천장

`Assets/Resources/Corridor/aged-floor-v2.png`와 `aged-ceiling-v2.png`는 built-in imagegen으로 생성한 원본 1254×1254 RGB PNG다. 저장된 생성 지시의 요약은 다음과 같다.

- 바닥: 정면에 수직으로 본 오래된 어두운 참나무 판재. 엇갈린 다양한 이음, 미세한 나뭇결과 축축한 마모. 중립적인 확산광. 과장된 그림자·피·문자 없이 재질로 반복 사용한다.
- 천장: 정면에 수직으로 본 회색·베이지의 낡은 석회 회반죽. 가는 균열, 습기·곰팡이 얼룩과 닳은 입자감. 중립적인 확산광. 번개 모양의 굵은 균열·보·문자 없이 재질로 반복 사용한다.

작업 원본은 각각 `exec-879a3cce-e743-4ca4-b41a-df6d5620be04.png`, `exec-e65d893b-2f22-4d76-b1c2-68538f9c6e1c.png`이며 프로젝트의 색상 PNG와 픽셀이 동일하다. `*-normal-v2.png`도 같은 원본 높이 입력이다. 완성된 접선 공간 노멀을 이미지 파일에 그려 넣은 것이 아니라 `CorridorSurfaceImport`가 Unity에서 회색 높이 기반 노멀로 변환한다.

Importer는 Texture2D, 색상 sRGB/노멀 linear, mipmap·trilinear·anisotropy 8·Mirror wrapping을 명시한다. 실제 metre UV와 연속 월드 좌표를 사용한다. 생성기가 수학적으로 완벽한 이음없는 텍스처를 보장한다고 주장하지 않는다. 실제 Unity 카메라의 셀 경계·거리에 따른 반복·손전등 표면 검수가 증거다.

## 제단

`SourceArt/seal-altar-v2.blend`와 `Assets/Resources/Corridor/seal-altar-v2.fbx`는 Blender 4.0.2에서 직접 만든 새 모델이다. 패널·장부 프레임·판재·철못, 베벨과 weighted normals를 갖춘다. 원래 충돌체와 기억 위치를 유지한다. FBX의 축 변환은 모델 루트에 보존하고 외부 런타임 래퍼를 배치한다.

`Tools/build_seal_altar_v2.py`는 재생성 도구다. Blender에서 실행하면 도구 옆의 `altar-art/` 검토 폴더로 출력한다. 재생성 결과를 곧바로 게임 자산이나 원본 씬에 덮어쓰지 않는다. Unity importer와 치수·충돌·공유 메시 수명 검사를 통과한 후보를 별도 버전으로 선택한다.

## 네 적

선택된 원본은 Cyclopse의 `Assets/Art/CandidateShapeSkin/Cyclopse/Walking.fbx`, Uncat의 `Assets/Art/V1/Uncat/Walking.fbx`, Hwacat_angry의 `Assets/Art/V1/Hwacat_angry/Zombie Run.fbx`, Baby의 `Assets/Art/V1/Baby/Zombie Crawl.fbx`다.

개선 자산은 `Assets/Resources/EnemyRefinement/`와 `SourceArt/EnemyRefinementV2/`에 별도로 있다. 원본의 표면 좌표·삼각형·UV·관절 가중치·원본 AnimationClip을 유지한다. 면적 가중 노멀과 같은 위치·관절 가중치·재질·완만한 각도에서만 seam normal을 연결하고, UV에 맞춘 미세 표면 노멀·변화하는 smoothness를 추가한다. 별개의 얼굴이나 실루엣을 다시 만든 모델로 설명하지 않는다.

Unity의 실제 가져오기 계층에서 세 모델에 추가된 identity Armature를 상대 애니메이션 바인딩 루트로 사용한다. 원본 클립 객체와 본의 로컬 기준을 검증한다. 실제 GPU 스킨과 같은 카메라·같은 TRS의 CPU 스냅샷 비교에서 네 모델 모두 `BakeMesh(..., true)`가 IoU 0.9998 이상으로 일치했다. 원본 파일은 수정하지 않고 생성된 런타임 인스턴스에만 선택 자산을 적용한다.

기존 모델·오디오의 출처와 라이선스는 프로젝트 ThirdParty 문서를 따른다. 이 개선은 새로운 외부 모델이나 녹음을 추가하지 않는다. 촛불 점화음은 직접 합성한 효과다. Windows 배포에는 기존 `Audio-Credits.txt`를 포함한다.

## 증거와 한계

로컬 작업 증거는 `../game/verification/development-feedback/`에 있으며 이전 실패 로그도 보존한다. `enemy-art/refinement-report.json`과 `fbx-roundtrip-results.json`은 원본·후보 형상 비교, `skin-mask-captures-08/`은 실제 렌더 좌표 증거다. 최종 아트 접지·실제 카메라·Windows shader variant 검증 결과는 `PLAYER_FEEDBACK_IMPLEMENTATION.md`의 최신 실행 내역에서 확인한다.

Blender 비교와 통제된 Unity 화면은 실제 생존 난이도·사람의 공포감·목표 기기의 성능을 증명하지 않는다.
