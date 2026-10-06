# 다른 PC에서 이어서 개발하기

이 프로젝트의 코드, Unity 자산/메타데이터, 프로젝트·패키지 설정, 제작 원본,
재생성·검증 도구와 크레딧을 Git으로 함께 전달한다. Unity 빌드와 Library 캐시는
소스에 포함하지 않으며 새 PC에서 다시 생성한다.

## 처음 받기

```sh
git clone https://github.com/minsminsKR/3d_modeling.git
cd 3d_modeling
git lfs install
git lfs pull
git switch main
```

기존 checkout은 먼저 작업을 보존한 뒤 `git pull --ff-only origin main`, `git lfs pull`
을 실행한다. Git LFS는 원본 학교 씬을 실제 파일로 내려받는 데 필요하다.
Unity Hub에서 **6000.6.0f1**을 설치하고 `HappyToyV2` 폴더를 연다. 패키지는
`Packages/manifest.json`과 `packages-lock.json` 기준으로 Unity가 복원한다.
게임 실행은 Editor Play로 시작하거나 Unity CLI로 새 Windows 빌드를 만든다.

```sh
cd HappyToyV2
unity run . --editor-version 6000.6.0f1 --format json -- -executeMethod HappyToy.V2.Editor.QualityValidation.BuildWindows
```

Unity CLI는 별도 설치 도구이며 기본 빌드 결과는 `HappyToyV2/Builds/Windows`다.
원본 `Assets/Annex/SchoolAnnex.unity`는 재생성하거나 덮어쓰지 않는다.

## 제작 원본과 도구

독립 Python 3.11 환경에서 생성 도구의 실제 사용 버전을 설치한다.

```sh
python -m pip install -r Tools/requirements-authoring.txt
```


- `SourceArt/EnemyRefinementV2/*.blend`: 네 적의 실제 편집 원본.
- `SourceArt/GraphicsUpgrade/Props/*.blend`: 여덟 소품의 실제 편집 원본.
- `SourceArt/seal-altar-v2.blend`: 앞선 제단 제작 원본.
- 게임에 사용되는 FBX·텍스처·재질·셰이더와 `.meta`는 `Assets`에 함께 있다.
- `SourceArt/GraphicsUpgrade`: 모델 제작, Poly Haven 취득, 종이/소품 PBR 생성 도구.
  `author_props_v3.py`는 Blender **4.0.x**, Python PBR 도구는 **NumPy·Pillow**를
  사용한다. 결과는 도구 옆의 `proposed`/`staged`에 생성되며 자동 배포하지 않는다.
  완성 `.blend`에서 직접 편집할 수 있고, 생성 이미지의 프롬프트와 실제 PNG도 저장돼 있다.
- `SourceArt/EnemyRefinementV2`: 아래 순서로 원본 FBX에서 검토 후보를 재생성한다.
  원본 입력은 프로젝트 상대 경로이고 결과는 무시되는 `work/` 폴더에 저장한다.

```sh
blender --background --python SourceArt/EnemyRefinementV2/audit_models.py
blender --background --python SourceArt/EnemyRefinementV2/refine_models.py
blender --background --python SourceArt/EnemyRefinementV2/check_export.py
```

후보를 실제 Assets에 적용하려면 기존 Unity importer·계층·스킨·클립·물리 검사를
통과시켜야 한다. 제작 도구 재생성은 원본 자산을 자동으로 덮어쓰지 않는다.
외부 자산 권리와 채널·해시는 `ThirdParty/Audio`, `ThirdParty/Graphics`에 보관한다.
로컬 생성 이미지 도구의 절대 경로는 과거 출처 기록이며 필수 입력 경로가 아니다.

## 푸시 전에 필수 소스 누락 확인

저장소 루트에서 필요한 변경을 stage한 후 실행한다.

```sh
python HappyToyV2/Tools/quality/check_source_completeness.py
```

검사는 Assets/Packages/ProjectSettings/SourceArt/Tools/ThirdParty와 프로젝트 안내
문서가 Git index에 있는지, 필요한 LFS 파일이 내려받아졌는지 확인한다.
실제 Unity 컴파일·플레이·새 PC에서의 전체 빌드 성공을 대체하지 않는다.
`Tools/quality/verify_build_inputs.py`, `verify_graphics_build_inputs.py`에는 현재
소스와 새 빌드의 해시를 확인하는 도구를 함께 제공한다. 개인 설정과 승인 규칙,
비밀값, 빌드·캐시·반복 staging 복사본은 전달할 필수 소스로 취급하지 않는다.
