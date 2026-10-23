# 노후 이중 형광등

두 개의 평행한 형광관, 금속 반사판, 소켓과 체결부를 가진 독립 생성 모델이다. 참조와 원본은 소등 상태이며 실제 빛과 전압 변화는 기존 학교 조명 시스템이 소유한다.

## 생성과 마감

- `reference.png`는 OpenAI built-in imagegen으로 만든 개별 객체 참조이다. 정확한 입력은 `reference-prompt.txt`, 생성 옵션과 요청/작업 식별자는 `generation-settings.json`에 있다.
- 지정 A100 모델링 서비스의 CLI로 TRELLIS.2 / 1024 cascade / 4K PBR / 12 steps / split GPU를 명시해 생성한다. 실제 기록의 옵션과 SHA-256이 최종 기준이다.
- 변경하지 않은 `original.glb`와 원본의 내장 PBR, 생성 기록 및 처리 입력을 보존한다. 다운로드 ZIP과 중간 로그는 게임 `Builds/ModelingDownloads/SchoolRenewal/fluorescent-fixture`에 둔다.
- Blender 4.0.2와 저장소 공통 `../finish_asset.py`로 원본의 복제본만 LOD화한다. 1.2m 폭, 8,000 / 2,000 삼각형 상한으로 두 LOD를 내보내고, 원본 색/ORM을 정돈된 UV로 전사하며 실제 원본 형상의 tangent normal을 베이크한다. Near 2K / far 1K이다.
- `fluorescent-fixture.blend`에는 변경하지 않은 원본 컬렉션, full-resolution packed PBR, 편집 가능한 게임 LOD 및 재현 코드가 들어간다. `export.json`과 `Assets/Resources/SchoolRenewal/fluorescent-fixture/asset.json`이 실제 치수·면 수·파일 해시를 기록한다.
- CLI 생성 뒤 동일 작업을 웹앱에서 회전하며 형상/재질을 확인하고, Unity에서 원래 학교 공간의 크기·조명·충돌과 함께 검증한다. 이 자료의 생성 전 설정은 그러한 최종 검토를 대신하지 않는다.

## 완료된 튜브·재질 마감

생성 원본의 미세한 비다양체 연결을 게임 복제본에서만 **1.5mm voxel 표면 수선**으로 정리했다. 완성 LOD는 **7,998 / 2,000 삼각형**이며 실제 생성 튜브 두 개와 반사판의 깊이 분리를 유지한다. `finish_geometry.py`와 `validation.json`은 실제 ray 검사를 기록한다.

`bake_tube_emission.py`는 `tube-emission-regions.json`의 실측 튜브 공간, 원본 albedo 밝기, 실측 반사판 배제 경계를 함께 사용해 각 LOD의 UV로 발광 마스크를 베이크한다. 실제 FBX의 튜브 표면 12점은 마스크 값 1, 케이스 표면 6점은 0이다. 케이스 전체나 추가 기본 도형 튜브를 발광시키지 않는다. `validate_lamp.py`가 이 검사와 실제 내보낸 모델의 발광 검토 렌더를 재현한다.

Unity는 기존 광원의 위치·세기·범위·그림자·깜빡임을 유지하고 두 LOD의 소유 재질만 함께 갱신한다. 생성 모델은 1.2×0.16×0.22m 설치 범위에 맞추며 천장까지 남는 9cm는 두 개의 가는 보조 행거로 연결한다. 이 지지부만 작은 건축 조립 디테일이다.

## 고지

참조 이미지와 생성 자산은 이 프로젝트 작업을 위해 제작했다. 별도의 공개 스캔이나 구매 prop을 새 모델의 원본으로 사용하지 않았다. 생성 도구의 라이선스와 생성 결과의 권리를 혼동하지 않으며, 보존된 upstream 도구 고지는 `../../SchoolRetractableGate/TRELLIS.2-LICENSE.txt`를 참조한다. 원본 GLB와 Blender 소스는 저장소의 Git LFS 설정을 따른다. 개인 CLI 상태·인증 쿠키·비밀번호는 소스에 포함하지 않는다.
