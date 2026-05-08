# 3d_modeling

이미지 기반 3D 모델링·모션 관련 실험 저장소입니다.

## Hunyuan3D 웹앱 (`hunyuan3d-webapp`) 실행용 모델 원본 링크

가중치는 용량이 커서 GitHub에는 포함하지 않습니다. 다른 PC에서 실행할 때는 아래 원본에서 받거나, 저장소의 스크립트로 한 번에 받을 수 있습니다.

| 용도 | 원본 링크 |
|------|-----------|
| Hunyuan3D 2.1 (공식 HF 저장소) | [huggingface.co/tencent/Hunyuan3D-2.1](https://huggingface.co/tencent/Hunyuan3D-2.1) |
| Shape 가중치 (`hunyuan3d-dit-v2-1`) | [HF Files · hunyuan3d-dit-v2-1](https://huggingface.co/tencent/Hunyuan3D-2.1/tree/main/hunyuan3d-dit-v2-1) |
| PBR 텍스처 가중치 (`hunyuan3d-paintpbr-v2-1`) | [HF Files · hunyuan3d-paintpbr-v2-1](https://huggingface.co/tencent/Hunyuan3D-2.1/tree/main/hunyuan3d-paintpbr-v2-1) |
| DINOv2-giant (이미지 인코더) | [huggingface.co/facebook/dinov2-giant](https://huggingface.co/facebook/dinov2-giant) |
| Real-ESRGAN `x4plus` (텍스처 업스케일) | [GitHub Release · RealESRGAN_x4plus.pth](https://github.com/xinntao/Real-ESRGAN/releases/download/v0.1.0/RealESRGAN_x4plus.pth) |

자동으로 받기: `hunyuan3d-webapp`에서 `.env`를 채운 뒤 `python scripts/download_models.py` 실행. 자세한 절차는 [hunyuan3d-webapp/README.md](hunyuan3d-webapp/README.md)를 참고하세요.

## 하위 프로젝트

- **hunyuan3d-webapp** — Hunyuan3D 2.1 Flask 웹앱 ([상세 README](hunyuan3d-webapp/README.md))
- **Hunyuan3D-2.1**, **Hunyuan3D-Omni** — 업스트림/실험용 서브모듈성 디렉터리
- **game**, **model_test** — 게임·에셋 테스트
