# A100 모델링 CLI

`modeling_cli.py`는 실행 중인 웹앱의 HTTP API를 호출하는 Python 클라이언트입니다.
웹과 동일한 A100 서버, 작업 큐, 생성 설정, 작업 저장소를 사용합니다. 클라이언트
컴퓨터에는 GPU, PyTorch, 모델 가중치가 필요하지 않습니다. CLI로 생성한 작업은
웹의 작업 화면에 표시되고, 완료된 결과는 `/saved`의 보관 모델에서도 확인할 수
있습니다. CLI가 GPU 추론 자체를 가속하는 것은 아닙니다.

## 설치와 접속

Python 3.10 이상과 외부 Python 의존성 `requests`가 필요합니다. 클라이언트를
다른 컴퓨터로 옮길 때 `modeling_cli.py`, `modeling_contract.py`,
`modeling_collection.py` 세 파일을 모두 같은 디렉터리에 보존합니다.
CUDA, PyTorch, 모델 가중치나 서버의 웹앱 패키지는 클라이언트에 설치하지 않습니다.

Windows PowerShell:

```powershell
py -3 -m pip install "requests>=2.31,<3"
$env:MODELING_SERVER = "https://agrees-mic-icon-contribution.trycloudflare.com"
py -3 .\modeling_cli.py login
```

Linux/macOS:

```bash
python3 -m pip install 'requests>=2.31,<3'
export MODELING_SERVER='https://agrees-mic-icon-contribution.trycloudflare.com'
python3 modeling_cli.py login
```

공개 터널 주소가 바뀌면 현재 웹에서 사용하는 origin으로 `MODELING_SERVER`를
바꾸거나 `--server https://현재주소`를 지정합니다. URL에는 `/jobs` 등의 경로를
붙이지 않습니다. `--server`는 환경 변수와 저장된 기본 주소보다 우선합니다.
성공한 `login`의 주소는 다음 실행을 위해 로컬 설정에 저장됩니다.

`login`은 웹과 같은 `/access/login`의 CSRF form과 세션 쿠키를 사용합니다.
비밀번호는 터미널에서 화면에 표시되지 않는 입력으로 받으며, 명령 인자·로그·기록에
저장하지 않습니다. 자동화에서는 별도로 보호한 UTF-8 파일을 사용할 수 있습니다.

```bash
python3 modeling_cli.py login --password-file /private/workspace-password.txt
```

직접 A100 안에서 실행할 때는 기존 웹앱의 내부 origin을 사용할 수 있습니다.
내부 앱에 게이트웨이가 없다면 별도 로그인 없이 기존 API에 접속합니다.
아래 주소는 내부 바인딩 예시이므로 실제 서비스 포트를 확인하여 지정합니다.
외부 접속을 위해 내부 포트를 새로 공개할 필요는 없습니다.

```bash
python3 modeling_cli.py --server http://127.0.0.1:5001 login
```

## 서버 기능과 준비 상태 확인

AI 자동화는 제출 전에 같은 서버의 기능과 상태를 확인합니다. 두 명령 모두 기존
웹 로그인 세션을 사용하며 새 토큰, 서비스 포트 또는 GPU 작업을 만들지 않습니다.

```bash
python3 modeling_cli.py capabilities --json
python3 modeling_cli.py doctor --json
```

`capabilities`는 `schema_version: 1`, `api_version: "1.1"`과 함께 모델별
`available`, `defaults`, `parameters`, `unavailable_reasons`를 반환합니다.
공통 `parameters`, 업로드·배치 `limits`, 지원 `features`와 API `endpoints`도
포함됩니다. CLI의 도움말보다 서버 응답이 현재 가용 모델·허용값·기본값의 기준입니다.
CLI는 명시한 옵션만 전송합니다. `models.MODEL.defaults`는 API에서 옵션을
생략했을 때의 실제 기본값이고, `models.MODEL.web_defaults`는 웹 폼의 초기
선택값입니다. 예를 들어 Hunyuan의 API 기본 `texture`는 `off`이며 웹 폼은
텍스처 실행기가 준비되면 `on`을 선택할 수 있습니다. TRELLIS.2는 두 경우 모두
텍스처가 활성화됩니다. 웹의 선택과 같게 만들려면 해당 값을 명시해서 제출합니다.

`doctor`는 `ok`, `status` (`ready` 또는 `degraded`), 항목별 `checks`,
`capabilities`, GPU/worker/queue 등 런타임 상태를 반환합니다. 개인 서버 경로나
인증 쿠키는 진단 JSON에 포함하지 않습니다. 준비되지 않았으면 진단 항목을 보존한
채 `code: "SERVER_NOT_READY"`, `retryable`, `next_action`을 추가하고 종료 코드
`1`을 반환합니다. 인증 실패나 서버 연결 실패도 아래의 공통 오류 형식으로 반환합니다.

## 생성, 상태 확인, 완료 대기, 다운로드

Windows:

```powershell
py -3 .\modeling_cli.py generate "C:\Models\reference.png"
py -3 .\modeling_cli.py status JOB_ID
py -3 .\modeling_cli.py wait JOB_ID --timeout 7200 --poll-interval 5
py -3 .\modeling_cli.py download JOB_ID --output "C:\Models\generated"
```

Linux/macOS에서 한 번에 완료까지 기다리고 보관 패키지를 받는 예시:

```bash
python3 modeling_cli.py generate ./reference.png --wait --download ./generated
python3 modeling_cli.py generate ./front.png ./second-character.png --download ./batch
```

여러 이미지는 기존 웹의 일괄 생성과 동일하게 이미지별 작업이 됩니다. 여러 시점
이미지를 자동으로 하나의 모델에 합치는 명령이 아닙니다. `--download DIR`에는 완료
대기가 포함됩니다. 생성 응답의 `web_url`과 각 작업의 웹 링크를 열면 같은 작업을
웹에서 확인하고 기존 리깅·관절 편집 기능으로 이어갈 수 있습니다.

각 결과는 `JOB_ID.zip`으로 저장됩니다. ZIP에는 원본 입력 이미지, `model.glb`,
`generation-record.json`, 존재하는 `metadata.json`이 포함됩니다. 생성된 원본
GLB·OBJ·MTL·PBR 텍스처와 리깅·Blender 등 결과 폴더의 추가 authoring 자료도
`outputs/` 아래에 함께 보존됩니다. 서버 생성 기록에는 실제 생성 옵션과 입력 및
결과 파일별 SHA-256이 들어갑니다. CLI는 ZIP을 자동으로 추출하지 않으며, 임시
파일에 다운로드하고 ZIP 구조·작업 ID·각 입력/결과의 크기와 SHA-256을 서버 기록과
대조한 뒤 최종 파일로 교체합니다.
`JOB_ID.download.json`에는 서버 생성 기록, 다운로드 시각, 전체 ZIP SHA-256,
바이트 수를 보관합니다. `download`를 직접 실행하면 해당 작업의 ZIP과 영수증을
갱신합니다. `collect`와 `resume`는 같은 출력 폴더에 있는 기존 ZIP의 전체 해시와
영수증·내장 입력/결과 해시를 검사하여 유효하면 다운로드를 생략합니다. 다른 출력
폴더나 손상된 ZIP은 새로 다운로드합니다. 웹에서 리깅·관절 등을 편집한 뒤 추가된
자료까지 받으려면 `collect REQUEST_UUID --output DIR --refresh`를 사용합니다.
게임에 사용한 원본 패키지는 프로젝트의 추적되는 authoring 폴더에도
보관해야 합니다. 클라이언트 개인 기록 폴더는 Git에 넣지 않습니다.

## 서버 기본값과 선택 옵션

CLI는 지정하지 않은 생성 옵션을 서버로 보내지 않습니다. 따라서 웹앱의 현재
기본 모델·GPU 배분·시드·배경 제거·해상도 설정이 유지됩니다. 최종 적용된 옵션은
작업 상태와 서버 생성 기록에서 확인합니다.

```bash
python3 modeling_cli.py generate ./reference.png \
  --model trellis2 --gpu split --seed 1234 \
  --option remove_background=on \
  --option trellis_pipeline=1536_cascade \
  --option trellis_texture_size=8192 \
  --download ./generated
```

`--model`, `--seed`, `--gpu` 외의 필드는 반복 가능한 `--option KEY=VALUE`로
지정합니다. 지원하지 않는 키, 중복된 키와 잘못된 범위는 제출 전에 거부합니다.

| 키 | 값 |
| --- | --- |
| `model` | `hunyuan3d-2.1`, `hunyuan3d-omni`, `trellis2` |
| `gpu_id` (`--gpu`) | `auto`, `split`, 0 이상의 GPU 번호 |
| `seed` | 0–4294967295 |
| `remove_background`, `texture` | `on`, `off` |
| `trellis_pipeline` | `512`, `1024_cascade`, `1536_cascade` |
| `trellis_steps` | 1–50 |
| `trellis_texture_size` | `4096`, `8192` |
| `num_inference_steps` | 1–100 |
| `guidance_scale` | 0.1–20 |
| `octree_resolution` | 64–512 |
| `num_chunks` | 1000–300000 |
| `texture_profile` | `standard`, `detail`, `ultra` |

모델 사용 승인, 설치된 모델, 가능한 GPU 조합과 텍스처 제약은 웹과 동일하게 서버가
최종 검사합니다. 클라이언트에 새 모델 실행기나 별도 설정 기본값은 없습니다.

## 연결이 끊겼을 때와 생성 기록

제출 전 CLI는 UUID 요청 ID, 정확한 입력 파일 사본, 입력 SHA-256, 제출 옵션을
로컬에 먼저 기록합니다. `Idempotency-Key`로 같은 UUID를 전송하므로 응답을 받기
전에 연결이 끊겨도 동일 ID로 재실행하면 새 GPU 작업을 중복 생성하지 않습니다.
처음 표시한 `Preserved request ID`를 보관합니다.

```bash
# 이전 제출의 정확한 로컬 입력/옵션을 사용하여 복구합니다.
python3 modeling_cli.py generate --request-id REQUEST_UUID --download ./generated

# UUID 전체 작업을 복구하고, 완료된 모델을 한 번 회수합니다.
python3 modeling_cli.py collect REQUEST_UUID --output ./generated --json

# 모든 작업을 계속 추적하며 완료된 모델을 회수합니다.
python3 modeling_cli.py resume REQUEST_UUID --output ./generated --timeout 7200 --json
# collect --wait도 전체 작업의 완료 대기와 회수를 수행합니다.
python3 modeling_cli.py collect REQUEST_UUID --output ./generated --wait --json

# 로컬 요청과 이미 받은 작업 ID를 확인합니다. 네트워크가 필요 없습니다.
python3 modeling_cli.py history
python3 modeling_cli.py history --request-id REQUEST_UUID --json

# 서버에 영구 보관된 CLI 작업 목록과 요청 ID를 조회합니다.
python3 modeling_cli.py history --remote --limit 50 --offset 0
python3 modeling_cli.py history --remote --request-id REQUEST_UUID --json
```

같은 UUID에 다른 이미지나 옵션을 제출하는 것은 거부합니다. 새 모델을 생성하려면
`--request-id`를 생략하거나 새 UUID를 지정합니다. 원래 클라이언트 기록이 없는 다른
컴퓨터에서도 원래 서버 주소와 UUID로 `collect` 또는 `resume`를 바로 실행할 수
있습니다. 서버 기록을 페이지 끝까지 조회하므로 한 요청의 작업이 누락되지 않습니다.
`resume`는 서버의 동일 UUID를 먼저 조회하고, 서버 작업이 하나라도 있으면 새 작업을
제출하지 않습니다. 서버에 전혀 없고 로컬에 제출 전·불확실한 제출 상태의 정확한
입력 사본이 남아 있을 때만 기존 UUID로 제출을 재시도합니다. 이전에 제출 완료로
기록된 작업이 서버에서 사라졌다면 자동 재생성하지 않습니다. `collect`는 생성 요청을
전송하지 않으며 실패한 모델도 두 명령이 자동 재생성하지 않습니다.
Ctrl+C는 로컬 대기만 중단하며 서버 작업은 계속
진행합니다. 작업 ID와 요청 기록은 보존됩니다.

기본 기록 위치는 `~/.hunyuan3d-cli`입니다. `--state-dir DIR`로 변경할 수 있습니다.
서버 origin마다 쿠키와 요청 기록을 분리합니다. POSIX에서는 기록 폴더는 `0700`,
쿠키·manifest·입력 사본은 `0600`으로 생성합니다. Windows에서는 사용자의 비공개
프로필 폴더 또는 접근 권한을 제한한 폴더를 사용합니다. 쿠키 파일은 로그인 세션을
포함하므로 소스 저장소나 공유 폴더에 복사하지 않습니다. CLI는 다른 origin으로의
리다이렉트·결과 URL을 거부하며, 세션을 다른 서버에 전송하지 않습니다.

## 스크립트 연동과 종료 코드

`--json`은 표준 출력에 JSON 결과만 출력합니다. 요청 ID·작업 ID 알림과 진행 상황은
표준 오류로 나갑니다. 공통 옵션은 하위 명령 앞뒤 모두 사용할 수 있습니다.

```bash
python3 modeling_cli.py --json generate ./reference.png > request.json
python3 modeling_cli.py status JOB_ID --json
python3 modeling_cli.py wait JOB_ID --timeout 3600 --poll-interval 5 --json
```

오류 JSON은 스키마 `1`을 사용합니다. `error`는 기존 클라이언트 호환 문자열이며,
`message`도 같은 설명입니다. AI는 문자열을 해석하기보다 `code`, `retryable`,
`next_action`을 읽습니다. `request_id`, `details`, `http_status`는 관련된 경우만
포함됩니다. 비밀 값, 비밀번호, 쿠키는 오류에 넣지 않습니다. `retryable: true`는
같은 ID로 다시 시도할 수 있다는 뜻이며 자동 재생성 허용은 아닙니다.
배치의 최상위 `PARTIAL_FAILURE.retryable`이 `false`여도 하위
`download_errors`·`status_errors`의 `retryable`과 `next_action`을 확인하여
같은 UUID의 `collect`·`resume`로 회수 오류를 복구할 수 있습니다.

```json
{
  "schema_version": 1,
  "ok": false,
  "error": "Server returned HTTP 429.",
  "message": "Server returned HTTP 429.",
  "code": "RATE_LIMITED",
  "retryable": true,
  "next_action": "Wait for retry_after_seconds, then retry using the original request ID.",
  "details": {"retry_after_seconds": 30},
  "http_status": 429
}
```

| 오류 코드 | 처리 방향 |
| --- | --- |
| `INVALID_INPUT` | capabilities의 옵션·범위를 확인하고 입력 수정 |
| `AUTH_REQUIRED`, `AUTH_REJECTED` | 같은 서버에 로그인한 뒤 기존 UUID로 복구 |
| `RATE_LIMITED` | `details.retry_after_seconds` 후 기존 ID로 재시도 |
| `NETWORK_ERROR`, `REQUEST_TIMEOUT`, `SERVER_UNAVAILABLE` | 연결·서버 확인 후 history/resume; 새 UUID 금지 |
| `SERVER_NOT_READY` | doctor의 실패 항목과 사용 불가 이유 확인 |
| `WAIT_TIMEOUT`, `INTERRUPTED` | 서버 작업은 유지됨; 기존 UUID로 resume |
| `REQUEST_CONFLICT` | 같은 UUID의 원본 이미지·옵션 보존 |
| `NOT_FOUND` | 서버 origin과 기존 ID 확인; 자동 재생성하지 않음 |
| `JOB_NOT_READY` | 완료까지 기다린 뒤 collect/download |
| `JOB_FAILED`, `PARTIAL_FAILURE` | 완료물을 보관하고 실패 목록 확인 |
| `ARTIFACT_UNAVAILABLE`, `INTEGRITY_ERROR` | 서버 자료 확인 후 같은 작업을 다시 다운로드 |
| `LOCAL_IO_ERROR` | 출력 경로·권한·디스크 여유 확인 후 resume |
| `UNSUPPORTED_API`, `PROTOCOL_ERROR` | 서버 주소와 CLI/API 버전 확인 |
| `INTERNAL_ERROR` | 서버 진단·로그 확인, 기존 UUID 기록 확인 |

| 코드 | 의미 |
| --- | --- |
| `0` | 명령 성공, 또는 대기한 모든 작업 완료 |
| `1` | 인증·네트워크·입력·진단 준비 실패·단일 다운로드 오류 또는 로컬 Ctrl+C |
| `2` | 서버 작업 실패·취소·배치 부분 성공; argparse 문법 오류도 `2`이며 JSON 오류 형식 지원 |
| `3` | 완료 대기 시간 초과. 서버 작업은 계속 진행 |

대기 기본 한도는 86400초, 기본 폴링 간격은 5초이며 최소 1초입니다. 생성 배치의
대기는 전체 작업에 하나의 시간 한도를 적용합니다. 한 작업이 실패해도 나머지 작업이
완료·실패·취소 상태가 될 때까지 계속 추적합니다. `generate --download`, `collect
--wait`, `resume`는 완료된 모델을 바로 회수하므로 형제 작업의 실패나 대기 시간
초과에도 성공한 파일과 영수증이 남습니다. 한 다운로드가 실패해도 다른 완료 모델을
계속 회수합니다. 인증·네트워크 오류가 생기면 같은 조회 회차의 다른 작업까지 가능한
회수를 시도한 뒤 반환합니다. HTTP 연결·응답 읽기의 기본 무응답 제한은 120초입니다. 느린 접속은
`--request-timeout 600`처럼 늘릴 수 있습니다. 인증·네트워크 오류로 무한 재시도하지
않으며, 로컬 기록과 동일 요청 ID를 사용해 명시적으로 이어갈 수 있습니다.

배치 회수 결과에는 `jobs`, `downloads`, `failed_jobs`, `pending_jobs`,
`download_errors`, `status_errors`, `summary`가 항상 들어갑니다. `summary`는
`total`, `completed`, `failed`, `pending`, `downloaded`, `download_errors`,
`status_errors`, `skipped` 개수입니다. `downloaded`에는 이번 실행에서 새로 받았거나
검증 후 재사용한 결과가 모두 포함됩니다. 각 `downloads[].skipped`로 구분합니다.
다운로드·상태 오류 항목은 공통 오류 형식과 `job_id`를 포함합니다. 전체 요청 기록
조회 오류에는 `job_id`가 없습니다. 이 집계와 마지막 결과는 로컬 요청 manifest에도
저장하며 기존 입력 사본·옵션·영수증을 유지합니다.

`collect` 기본 동작은 한 번 확인하는 것이므로 아직 실행 중인 모델만 남았으면 성공
코드 `0`과 `pending_jobs`를 반환합니다. 일부 작업 실패·회수 오류가 있으면 완료물을
반환하면서 코드 `2`를 사용합니다. 대기 중 한도가 끝나면 이미 받은 `downloads`를
포함하여 `WAIT_TIMEOUT`, 코드 `3`을 반환합니다. 서버의 미완료 작업은 계속 실행됩니다.

```bash
python3 modeling_cli.py --help
python3 modeling_cli.py generate --help
```
