# A100 소스 설치와 개발

CLI 클라이언트는 Python 3.10 이상과 `requests`만 필요하며 GPU 런타임은 설치하지 않습니다.
[CLI-A100.md](CLI-A100.md)의 설치·로그인·생성·복구 예시를 따르세요.
CLI에는 `modeling_cli.py`, `modeling_collection.py`, `modeling_contract.py`,
`modeling_state.py`, `modeling_download.py`를
같은 폴더에 함께 보존합니다. 서버에는 `app.py`, `generation_api.py`,
`modeling_capabilities.py`, `modeling_contract.py`, `modeling_history.py`,
`modeling_archives.py`를 함께 배포해야 합니다.
`python scripts/validate_cli_api.py`는 별도 임시 저장소에서 웹/CLI 계약과
실제 HTTP 클라이언트 동작을 검사하며 GPU 추론이나 운영 작업 저장소를 사용하지 않습니다.
`python scripts/validate_cli_agent.py`는 기능·준비 상태 조회, 오류 복구 계약,
부분 성공 배치 회수와 재개를 같은 방식으로 검사합니다.
`scripts/validate_cli_state.py`, `scripts/validate_history_index.py`,
`scripts/validate_archive_cache.py`, `scripts/validate_download_resume.py`는
상태/쿠키 변경 저장, 외부 변경, 이력 페이지, 캐시 무효화·동시 접근, 대용량 ZIP의
전송 중단·프로세스 종료 후 이어받기를 검사합니다. 측정은
[CLI-PERFORMANCE.md](CLI-PERFORMANCE.md)를 따릅니다. 운영 설정의
`MODELING_ARCHIVE_CACHE_MAX_BYTES`는 기본 10 GiB이며 디스크 공간에 맞게 조정합니다.

CHG-20261011-A100-011: 검증된 서버 버전 2026.10.11.11의 GitHub 소스 반영.
이 문서는 새 Linux 사본을 준비하기 위한 안내다. 실행 중인 서버와 모델·결과·인증정보는 별도로 보존한다.

## 소스 가져오기

```bash
git clone https://github.com/minsminsKR/3d_modeling.git
cd 3d_modeling/hunyuan3d-webapp
python3 scripts/fetch_upstream.py
cp .env.a100.example .env
```

`upstream/pins.json`은 8개 공식 코드 저장소의 검증된 커밋을 고정한다. `upstream/patches/`에는 Hunyuan 입력/PBR 보존과 SkinTokens의 Blender·CPU·loopback 호환성 수정이 있다. 기존 upstream 사본의 커밋이 다르면 덮어쓰지 않고 중단한다. 원본 코드와 라이선스는 해당 저장소에서 내려받는다.

`.env`의 FLASK_SECRET_KEY를 새 무작위 값으로 교체한다. 모델 경로는 웹앱 폴더를 기준으로 한다. 서버 비밀번호·HF 토큰을 저장소 파일에 넣지 않는다. 모델 라이선스와 DINOv3/RMBG-2.0 접근 승인은 사용할 계정에서 별도로 준비한다.

## 생성 환경과 모델

NVIDIA 드라이버, CUDA toolkit, C++ 빌드 도구가 있는 Linux x86_64가 필요하다. Hunyuan과 TRELLIS는 Python 3.10을 사용하며, 가상환경을 기존 서버에서 복사하지 않는다.

```bash
bash scripts/setup_a100.sh
bash scripts/setup_trellis_a100.sh
.venv/bin/python scripts/download_a100_models.py
.venv/bin/python scripts/download_omni_ema.py
# 승인된 HF 계정으로 터미널 로그인; 토큰은 명령행 인자로 전달하지 않는다.
.venv-trellis/bin/huggingface-cli login
.venv-trellis/bin/python scripts/download_trellis_a100.py
mkdir -p logs instance
```

Hunyuan Torch 2.5.1/cu124, TRELLIS Torch 2.6.0/cu124를 분리한다. `requirements-*.lock.txt`는 서버에서 확인한 패키지 기록이며, CUDA extension은 고정한 로컬 코드에서 빌드한다. 전체 lock을 무작정 다른 Python/CUDA 환경에 설치하지 않는다.

## 리깅 환경

Python 3.11.15 / Torch 2.7.0 cu126 / bpy 4.2.0 / FlashAttention 2.8.3의 별도 `.venv-rig`를 사용한다.

```bash
python3.11 -m venv .venv-rig
.venv-rig/bin/python -m pip install pip setuptools wheel
.venv-rig/bin/python -m pip install torch==2.7.0 torchvision==0.22.0 torchaudio==2.7.0 --index-url https://download.pytorch.org/whl/cu126
# FlashAttention은 Torch 설치 후 빌드한다.
.venv-rig/bin/python -m pip install flash-attn==2.8.3 --no-build-isolation
.venv-rig/bin/python -m pip install -r requirements-rigging-a100.txt --extra-index-url https://download.pytorch.org/whl/cu126
.venv-rig/bin/python scripts/download_rigging_a100.py
```

일부 bpy 4.2 wheel의 내부 WHEEL 태그 오류는 [RIGGING-A100.md](RIGGING-A100.md)의 호환성 항목을 참고한다. 시스템 CUDA 드라이버나 기존 다른 서비스는 변경하지 않는다.

## 편집 가능한 웹 원본

`editor-build/editor.js`가 Three.js 관절 편집기 원본이다. package-lock은 Three.js 0.186.1 / esbuild 0.25.12를 고정한다. 서버에서 검증한 bundle과 법적 고지도 포함되어 있다.

```bash
cd editor-build
npm ci
npm run build
cd ..
```

## 검사와 실행

```bash
.venv/bin/python validate_private_gateway.py
.venv/bin/python validate_png_upload.py
.venv/bin/python validate_dual_api.py
```

위 검사는 격리된 DATA 저장소와 가짜 생성 실행기를 사용하며 GPU 추론을 반복하지 않는다. 실제 새 환경에서는 `scripts/check_env.py`, `scripts/smoke_a100.py`, `scripts/validate_trellis_a100.py`, `scripts/validate_rigging_a100.py`로 모델 생성과 출력 검증을 수행한다. 이 검사는 시간이 걸리고 테스트 모델을 만든다. 일부 과거 결과 검증 스크립트는 기존 서버의 DATA/작업 파일이 필요하다.

앱은 설치 성공만으로 모델을 준비 완료로 간주하지 않는다. 실제 검증을 마친 다음 `logs/trellis-a100-ready.json`에는 `{"status":"ready"}`, `logs/rigging-ready.json`에는 `{"ready":true}`를 기록한다. 2GPU native 검증까지 마친 경우에만 `instance/trellis-dual-ready.json`에 `{"status":"ready"}`를 기록한다. 새 시스템에서 이를 검증 없이 만들지 않는다.

```bash
bash scripts/run_a100.sh
```

기본값은 TRELLIS.2 / 1536 cascade / 8K / 12 steps / GPU1 형상 + GPU0 재질이다. 검증된 split 토큰 한도는 65536이며 단일 GPU에서는 49152를 사용한다. 메모리가 부족하면 기존 코드가 단일 GPU 또는 재질 단계의 GPU1 재시도를 선택한다. 빠르게 프리셋은 512/4K의 독립 이미지 작업을 두 GPU에 배분한다. 두 40GB 카드의 메모리를 80GB로 합치는 구현은 아니다.

외부 HTTPS 보호 게이트웨이는 [PRIVATE-ACCESS-A100.md](PRIVATE-ACCESS-A100.md)를 참고한다. `setup_private_access.py`는 소유자 전용 인증 파일과 systemd unit을 준비하며, 터널을 자동으로 열지 않는다. 기존 서버의 인증 파일을 Git 사본으로 덮어쓰지 않는다.

## 저장소에 포함하지 않는 데이터

`.env`, `private-access/`, `instance/`, `logs/`, `backups/`, 가상환경, 다운로드한 모델과 캐시는 제외한다. 모델 다운로드와 원본 편집·빌드·검증 도구는 포함한다. `static/validation/rig-editor-four-arms.glb`만 작은 합성 QA fixture로 포함하며, 생성 원본은 `scripts/validate_rig_editor_gpu.py`에 있다.

GitHub 푸시는 코드 백업이며 서버를 재시작하거나 자동 배포하지 않는다. 현재 서버의 검증 상세는 CHANGELOG-A100.md와 TRELLIS-A100.md에 기록되어 있다.
