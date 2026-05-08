# Model Test Map

브라우저에서 실행하는 3D 모델 테스트 맵입니다.

평면 맵 위에 작고 큰 장애물을 배치해 Hunyuan3D 웹앱에서 만든 모델과 Mixamo에서 리깅/모션 처리한 모델을 빠르게 확인하는 용도입니다.

## 폴더 구조

```text
model_test/
  main.py
  web_server.py
  requirements.txt
  README.md
  web/
    index.html
    styles.css
    app.js
  model/
    사이클롭스/
      source/    # Hunyuan3D 원본 GLB/OBJ/텍스처
      mixamo/    # Mixamo에서 받은 FBX/DAE/GLB 모션
      exports/   # Blender 등에서 변환한 테스트용 GLB
      packages/  # Mixamo 업로드용 ZIP 보관
```

## 웹 실행 방법

서버 컴퓨터에서는 로컬 GUI 창을 열기 어렵기 때문에 이 방식을 권장합니다.

```bash
source /home/ai-server-04/mskang/3d_modeling/.venv/bin/activate
python model_test/web_server.py --host 0.0.0.0 --port 8000
```

그 다음 브라우저에서 아래 주소로 접속합니다.

```text
http://서버IP:8000
```

서버 안에서만 확인한다면 아래 주소도 가능합니다.

```text
http://127.0.0.1:8000
```

## 웹 조작법

- `W`, `A`, `S`, `D` 또는 방향키: 모델 이동
- 마우스 드래그: 시점 회전
- 마우스 휠: 줌
- `Q`, `E`: 모델 회전
- `+`, `-`: 모델 크기 조절
- `R`: 위치/크기 초기화

## 모델 관리

모델 파일은 `model_test/model/<캐릭터명>` 폴더에서 관리합니다.

웹 버전은 아래 확장자를 목록에 표시하고 로딩합니다.

- `.fbx`
- `.glb`
- `.gltf`
- `.obj`
- `.zip`

`.zip` 파일은 웹 서버가 자동으로 `model_test/model/_extracted` 폴더에 압축 해제한 뒤, ZIP 안에 있는 `.fbx`, `.glb`, `.gltf`, `.obj` 파일을 선택 목록에 표시합니다.

`.gltf`, `.obj`처럼 텍스처나 `.bin`, `.mtl` 파일을 함께 쓰는 모델은 관련 파일도 같은 폴더 구조 안에 같이 있어야 합니다.

## FBX 사용 흐름

Mixamo에서 받은 `.fbx` 또는 `.zip`을 `model_test/model` 폴더에 넣고 웹 페이지의 모델 선택 목록에서 고르면 됩니다.

`Walking`처럼 모션 파일도 FBX라면 `mixamo/Walking.fbx`에 넣으면 웹의 `파일 / 모션` 선택 목록에 표시됩니다. 다만 캐릭터 메시가 없는 애니메이션 전용 FBX라면 화면에 모델이 보이지 않을 수 있습니다. 캐릭터 테스트용으로는 Mixamo에서 캐릭터가 포함된 FBX 또는 GLB를 준비하는 것이 좋습니다.

Mixamo FBX는 애니메이션과 캐릭터 메시를 포함해도 Hunyuan3D 텍스처가 빠지거나 단색 재질로 보일 수 있습니다. 완성된 외형으로 보려면 Hunyuan3D에서 나온 텍스처 이미지를 아래 이름으로 같이 넣어둡니다.

```text
model_test/model/<캐릭터명>/
  source/
    model_textured.jpg
  mixamo/
    Walking.fbx
    Drunk Run Forward.fbx
```

웹 테스트맵은 `source/model_textured.jpg`, `source/model_textured.png`, `source/model_textured.webp` 순서의 이름을 우선 찾아 Mixamo FBX에 다시 적용합니다. 이 파일이 없으면 모델은 움직이지만 밋밋한 기본 색으로 보일 수 있습니다.

FBX가 브라우저에서 제대로 표시되지 않거나 텍스처 좌표가 맞지 않으면 Blender에서 `.glb`로 변환해서 테스트하는 것을 권장합니다.

권장 변환 흐름:

```text
Hunyuan3D 모델 생성
-> Mixamo에서 자동 리깅 및 모션 적용
-> FBX 다운로드
-> Blender에서 FBX 열기
-> File > Export > glTF 2.0 선택
-> GLB로 내보내기
-> model_test/model 폴더에 저장
-> 웹 페이지에서 모델 선택
```

## 데스크톱 실행

`main.py`는 Ursina 데스크톱 버전입니다. 서버에 GUI가 없으면 실행할 수 없습니다.

```bash
source /home/ai-server-04/mskang/3d_modeling/.venv/bin/activate
python model_test/main.py
```

GUI 없이 맵 설정과 모델 폴더만 확인하려면 아래 명령을 사용합니다.

```bash
python model_test/main.py --check
```

## GUI 실행 오류

데스크톱 버전에서 아래와 같은 오류가 나오면 코드 문제가 아니라 현재 터미널이 그래픽 창을 열 수 없는 상태입니다.

```text
Could not open display ":0.0"
Exception: Could not open window.
```

이 테스트 맵은 3D 창을 띄워야 하므로 데스크톱 화면, VNC, NoMachine, X11 forwarding 같은 GUI 접속 환경에서 실행해야 합니다.

단순 실행 확인만 필요하면 아래처럼 가상 디스플레이를 사용할 수 있습니다.

```bash
xvfb-run -s "-screen 0 1280x720x24" python model_test/main.py
```

`xvfb-run`이 없다면 OS 패키지 설치가 필요합니다.

```bash
sudo apt install xvfb
```

## 맵 수정

웹 버전의 장애물은 `web/app.js`의 `obstacles` 배열에서 관리합니다.

각 항목의 `position`, `scale`, `color` 값을 바꾸면 맵 구성을 쉽게 조정할 수 있습니다.
