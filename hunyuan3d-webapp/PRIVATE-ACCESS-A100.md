# A100 웹앱 외부 접속

CHG-20261011-A100-006 · 2026.10.11.7

- 현재 외부 주소: https://YOUR-TUNNEL-HOST.example
- 내부망 주소: http://192.168.0.16:5001
- 실제 GPU 서버: ai-server-04@192.168.0.16. 브라우저가 있는 컴퓨터에 GPU나 모델을 설치할 필요가 없다.
- 접속 비밀번호: 서버 `private-access/owner-password.txt`, 소유자만 읽을 수 있는 모드 0600. 로컬 전달 파일 `<owner-private-delivery-file>`은 현재 Windows 실행 사용자와 SYSTEM만 접근하도록 ACL을 설정했다. 비밀번호를 문서/로그/채팅에 복사하지 않는다.

## 접근 권한

외부 주소는 비밀번호 로그인이 필요하다. URL을 아는 것만으로 생성 결과, 모델 다운로드, API를 이용할 수 없다. 지인에게 URL과 비밀번호를 함께 전달하면 같은 작업 공간을 사용할 수 있다. 개인 계정이나 작업별 권한 분리는 제공하지 않는다. 로그인한 지인은 기존 모델을 보고 내려받고 수정하거나 생성 작업을 요청할 수 있다. 비밀번호를 전달받은 사람이 다시 전달하는 것도 기술적으로 막지 않는다.

이 보호는 **외부 HTTPS 주소**에 적용된다. 기존 내부망 포트 5001은 이번 변경으로 로그인 방식을 바꾸지 않았다. 서버와 같은 LAN에 있는 사용자는 기존 내부 주소로 접속할 수 있다.

로그인은 12시간 유효하다. 로그아웃은 외부 주소의 `/access`에서 실행한다. 쿠키는 Secure/HttpOnly/SameSite=Strict, 로그인 비밀번호는 scrypt 해시, 인증되지 않은 다운로드 및 API는 차단된다. 로그인 실패 8회/15분은 해당 IP에서 제한한다. 브라우저의 상태 변경 요청에는 같은 HTTPS Origin이 필요하다. 외부 요청은 HTTPS로 강제 전환되며 검색 색인은 금지된다.

## 실행 구조

인터넷 HTTPS → Cloudflare Quick Tunnel → `127.0.0.1:5002` 보호 프록시 → 기존 `127.0.0.1:5001` GPU 웹앱.

- `hunyuan3d-webapp.service`: 기존 GPU 앱. 코드/의존성/세대 작업 기본값은 변경하지 않았다.
- `hunyuan3d-private-gateway.service`: gunicorn 1 worker/8 threads, loopback 5002만 수신. 인증과 스트리밍 프록시. 로그인 비밀번호 및 쿠키를 GPU 앱에 전달하지 않는다.
- `hunyuan3d-private-tunnel.service`: 공식 cloudflared 2026.10.0. 공식 GitHub release SHA256 `d33ff2d14475178d2012c2c56beba87389ac5ded27649519f198a7d3134a99db` 확인 후 설치. 프록시 포트만 연결한다. 메트릭 포트 5004도 loopback에만 수신한다.
- 두 새 user services는 enable되었고 기존 서버의 user linger 설정으로 계속 실행된다. 공유기의 포트 포워딩, 방화벽, SSH, RTSP, 다른 웹앱은 변경하지 않았다.
- 현재 주소는 `private-access/public-address.json`에서 확인한다. 터널 프로세스가 종료되거나 서버가 재시작되면 주소가 달라질 수 있다.
- 서버가 꺼져 있거나 인터넷 연결이 끊기면 외부 사이트도 사용할 수 없다.

## 주소와 전송 한계

이 주소는 Cloudflare Quick Tunnel의 임시 호스트이다. 서버 서비스로 유지하지만 고정 도메인과 가용성 보장이 있는 운영 배포는 아니다. 같은 URL을 계속 쓰려면 사용자 소유 도메인과 Cloudflare 계정으로 정식 Named Tunnel을 연결해야 한다. 현재 보호 프록시는 그대로 재사용할 수 있다. 사용자 계정/도메인/터널 토큰 없이 정식 주소를 임의로 생성하지 않는다.

외부 업로드 요청은 총 90 MiB까지 허용한다. 내부 앱의 기존 200 MiB 설정은 유지했다. 큰 GLB는 스트리밍 전송하며 Range/HEAD/Content-Disposition을 유지한다. GPU 계산은 기존 서버의 큐와 설정을 사용하므로 여러 사용자의 작업은 GPU 자원을 공유한다.

공식 설명: [Quick Tunnels](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/) — 임시 URL, 재시작 시 주소 변경, 가용성 보장 없음, 동시 진행 요청 200개 한계, SSE 미지원. 이 앱은 기존 JSON 상태 조회를 사용한다.

## 확인 및 중지

```bash
cd <checkout>/hunyuan3d-webapp
systemctl --user is-active hunyuan3d-webapp.service hunyuan3d-private-gateway.service hunyuan3d-private-tunnel.service
cat private-access/public-address.json
# 외부 접속만 중지. GPU 앱/내부망 주소는 그대로 유지한다.
systemctl --user disable --now hunyuan3d-private-tunnel.service hunyuan3d-private-gateway.service
```

비밀번호 교체 시 서버 owner-password 파일과 credentials의 password_hash 및 auth_epoch를 함께 교체하고 gateway만 재시작한다. 기존 로그인은 무효화된다. secret_key/비밀번호/인증 쿠키를 도구 출력이나 저장소에 노출하지 않는다.

## 검증

- `logs/private-gateway-validation.json`: 인증·CSRF·강제 로그인·우회 경로·서명 쿠키·로그아웃·로그인 제한·업로드 상한·실제 4MiB 스트리밍/Range/HEAD 32개 검사 통과.
- `logs/private-access-local-validation.json`: 실제 loopback 프록시에서 기존 홈/편집기/JS/실제 GLB 일부 전송과 GPU 2개 서버 연결, 미인증·외부 Origin 차단 16개 검사 통과.
- `logs/private-access-public-validation.json`: 실제 외부 HTTPS URL에서 로그인, HTTP→HTTPS, 홈/편집기/JS/GLB 일부 전송, 인증되지 않은 모델/API/생성 차단 18개 검사 통과.
- 기존 생성 작업 107개와 app.py SHA256 보존. 새로운 GPU 추론은 실행하지 않았다.

## 1033 업로드 후 이동 오류 수정 · CHG-20261011-A100-008

PNG 자체는 서버에 정상 접수됐지만, 결과 페이지로 이동하는 리다이렉트 응답의 본문이 프록시에서 누락되어 Cloudflare EOF/1033이 발생했다. 수정 후 기존 주소와 로그인을 유지하며 36개 회귀 검사와 실제 외부 리다이렉트 전송을 확인했다. 이런 경우 업로드 작업이 이미 실행 중일 수 있으므로 결과/최근 작업을 먼저 확인하고 재업로드로 중복 작업을 만들지 않는다.

임시 호스트의 수명·가용성 한계는 별도이며, 이 수정이 고정 도메인 전환을 의미하지 않는다. 이전 공부 앱들은 ChatGPT Sites 고정 주소를 쓰지만 GPU 계산은 A100 서버에 남겨야 하므로 그 서버의 접속 경로도 유지해야 한다.


## Owner-selected password update · CHG-20261011-A100-010 · 2026.10.11.11

The owner chose a simpler password instead of anonymous access. Password authentication remains required at https://YOUR-TUNNEL-HOST.example. Updated the password hash and auth epoch; existing logged-in sessions are invalidated. Plaintext is retained only in the protected owner delivery files; it is not written into source, docs or logs. Removed the unactivated anonymous-mode preparation from the live gateway source by restoring the protected version. No public policy was activated. Restarted only the gateway; the GPU app/tunnel and all 110 prior jobs were preserved. External new-password login, old-session invalidation, anonymous home302/API401 and private file permissions passed. Evidence: logs/access-password-rotation-validation.json.
