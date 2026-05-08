# Hunyuan3D Flask Web App

사진 1장 또는 여러 장을 업로드해서 **Hunyuan3D 2.1**(PBR) 기반 GLB 모델을 생성하는 Flask 웹앱입니다. 생성된 GLB는 웹에서 미리보기/다운로드가 가능하고, `instance/outputs` 아래에 보관되어 게임 프로젝트에서 바로 asset 경로로 쓸 수 있습니다.

## 구조

```text
hunyuan3d-webapp/
  app.py                       # Flask routes + background job queue
  hunyuan_service.py           # Hunyuan3D 2.1 integration wrapper
  templates/                   # Upload and job status pages
  static/                      # CSS, polling + Ctrl+V paste JS
  scripts/download_models.py   # Pre-download all 2.1 model assets into the repo
  scripts/texture_worker.py    # Separate process for PBR texture generation
  scripts/check_env.py         # Environment/import sanity check
```

## 준비

공식 Hunyuan3D 2.1 저장소와 모델 가중치는 매우 커서 이 웹앱에 vendoring하지 않습니다. 별도 위치에 공식 저장소를 클론하고 `.env`에서 `HUNYUAN3D_REPO`로 연결하세요.

공식 README 기준으로 Hunyuan3D 2.1은 Python 3.10, PyTorch 2.5.1+cu124에서 테스트되었고, **shape 10GB / texture 21GB VRAM**이 필요합니다. 본 웹앱은 shape 파이프라인을 릴리즈한 뒤 texture 워커를 띄우는 순차 실행이라, **RTX 3090 Ti 24GB 한 장으로 품질 프리셋이 가능**합니다.

```powershell
conda activate 3d
cd E:\AI\3d_modeling\hunyuan3d-webapp
pip install -r requirements-web.txt
```

공식 저장소 설치.

```powershell
cd E:\AI\3d_modeling
git clone https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1.git
cd Hunyuan3D-2.1
pip install torch==2.5.1 torchvision==0.20.1 torchaudio==2.5.1 --index-url https://download.pytorch.org/whl/cu124
pip install -r requirements.txt
cd hy3dpaint/custom_rasterizer; pip install -e .; cd ..\..
cd hy3dpaint/DifferentiableRenderer; bash compile_mesh_painter.sh; cd ..\..
```

## 환경변수

`.env.example`을 `.env`로 복사한 뒤 최소한 아래만 채워 두면 됩니다.

```powershell
HUNYUAN3D_REPO=E:\AI\3d_modeling\Hunyuan3D-2.1
HY3DGEN_MODELS=E:\AI\3d_modeling\Hunyuan3D-2.1\local_models
HUNYUAN_LOCAL_MODEL_DIR=E:\AI\3d_modeling\Hunyuan3D-2.1\local_models\Hunyuan3D-2.1
HUNYUAN_DEVICE=cuda
```

- `HY3DGEN_MODELS` — shape 파이프라인이 디스크에서 바로 읽도록 지정합니다. 첫 실행 전에 `scripts/download_models.py`로 채워 두세요.
- `HUNYUAN_LOCAL_MODEL_DIR` — texture(paint) 파이프라인이 사용할 로컬 캐시(`hunyuan3d-paintpbr-v2-1` 상위 폴더)입니다.

나머지 `HUNYUAN_TEXTURE_*`, `HUNYUAN_LOW_VRAM` 같은 플래그의 기본값은 3090 Ti 24GB에서 **품질 우선**으로 맞춰져 있습니다(아래 프리셋 참고).

## 모델 다운로드 (최초 1회)

아래 스크립트는 `HUNYUAN3D_REPO` 안의 `local_models/`와 `hy3dpaint/ckpt/`에 모든 필수 가중치를 받아 둡니다.

```powershell
conda activate 3d
cd E:\AI\3d_modeling\hunyuan3d-webapp
python scripts/download_models.py
```

받히는 파일.

| 용도 | 위치 |
|------|------|
| Shape (3.3B) | `<repo>/local_models/tencent/Hunyuan3D-2.1/hunyuan3d-dit-v2-1/` |
| PBR Texture (2B) | `<repo>/local_models/Hunyuan3D-2.1/hunyuan3d-paintpbr-v2-1/` |
| DINOv2-giant | `<repo>/local_models/facebook--dinov2-giant/` |
| Real-ESRGAN | `<repo>/hy3dpaint/ckpt/RealESRGAN_x4plus.pth` |

### 원본 다운로드 링크 (브라우저·수동 확인용)

스크립트가 받는 것과 동일한 소스입니다. Hugging Face 모델은 동의·토큰이 필요할 수 있습니다.

| 용도 | 링크 |
|------|------|
| Hunyuan3D 2.1 저장소 | [huggingface.co/tencent/Hunyuan3D-2.1](https://huggingface.co/tencent/Hunyuan3D-2.1) |
| Shape (`hunyuan3d-dit-v2-1`) | [tree/main/hunyuan3d-dit-v2-1](https://huggingface.co/tencent/Hunyuan3D-2.1/tree/main/hunyuan3d-dit-v2-1) |
| PBR Texture (`hunyuan3d-paintpbr-v2-1`) | [tree/main/hunyuan3d-paintpbr-v2-1](https://huggingface.co/tencent/Hunyuan3D-2.1/tree/main/hunyuan3d-paintpbr-v2-1) |
| DINOv2-giant | [huggingface.co/facebook/dinov2-giant](https://huggingface.co/facebook/dinov2-giant) |
| Real-ESRGAN `RealESRGAN_x4plus.pth` | [xinntao/Real-ESRGAN 릴리스 직링크](https://github.com/xinntao/Real-ESRGAN/releases/download/v0.1.0/RealESRGAN_x4plus.pth) |

`huggingface_hub`는 이미 받은 파일은 건너뛰므로 재실행해도 안전합니다.

## 품질 우선 프리셋 (RTX 3090 Ti 24GB)

| 항목 | 기본값 | 메모 |
|------|--------|------|
| Shape Steps | 50 | 공식 gradio 기본 30보다 상향 |
| Guidance | 7.5 | 공식과 동일 |
| Octree | 512 | 최고 해상도 |
| Texture Views | 6 | 2.1 README의 `max_num_view` 상한 |
| Texture Resolution | 512 | 2.1 README 권장 |
| Render Size | 2048 | `Hunyuan3DPaintConfig` 기본 |
| Texture Map Size | 4096 | `Hunyuan3DPaintConfig` 기본 |
| FlashVDM | off | 속도 대신 품질 유지 |
| Low VRAM | off | 24GB에서는 불필요 |

VRAM이 빠듯하면 `.env`에서 `HUNYUAN_LOW_VRAM=1` 또는 `HUNYUAN_TEXTURE_MAP_SIZE=2048`부터 낮추세요.

## 실행

```powershell
conda activate 3d
cd E:\AI\3d_modeling\hunyuan3d-webapp
python scripts/check_env.py
python app.py
```

PowerShell 프로필의 conda 초기화가 불안정하면 이 형태로도 됩니다.

```powershell
conda run -n 3d python app.py
```

브라우저에서 `http://127.0.0.1:5000`을 여세요.

## 단일 이미지 3D 추론

기본 모델은 Hunyuan3D 2.1 single-image shape 모델입니다. 2.1에는 별도 multiview shape 변종이 없기 때문에, 여러 장을 업로드하면 **첫 번째 이미지로 shape를 만들고 나머지는 텍스처 단계에 참고**됩니다.

품질은 입력 이미지에 크게 좌우됩니다. 배경이 깔끔하게 제거된 물체 사진, 피규어 사진, 3/4 각도 사진이 가장 입체적이고, 정면 캐릭터 일러스트처럼 원본이 납작한 이미지는 얇은 판처럼 추론될 수 있습니다. 이런 경우 seed를 바꾸거나 Octree 512 + PBR 텍스처 생성을 켜 보세요.

## 게임 연동

완료된 모델은 아래 위치에 저장됩니다.

```text
instance/outputs/<job_id>/model.glb
```

Panda3D, Ursina, pygltflib 기반 렌더러처럼 GLB/glTF를 읽을 수 있는 엔진을 쓰면 경로만 넘기면 됩니다.
