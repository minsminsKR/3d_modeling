# 폐교 신발장

4열×4단 열린 수납칸과 두께가 있는 칸막이를 가진 독립 생성 신발장이다. 붓기·긁힘·오염은 실제 생성 PBR에 보존하며 중앙 이동 경로 밖에 설치한다.

## 생성과 마감

- `reference.png`는 OpenAI built-in imagegen으로 만든 개별 객체 참조이다. 정확한 입력은 `reference-prompt.txt`, 생성 옵션과 요청/작업 식별자는 `generation-settings.json`에 있다.
- 지정 A100 모델링 서비스의 CLI로 TRELLIS.2 / 1024 cascade / 4K PBR / 12 steps / split GPU를 명시해 생성한다. 실제 기록의 옵션과 SHA-256이 최종 기준이다.
- 변경하지 않은 `original.glb`와 원본의 내장 PBR, 생성 기록 및 처리 입력을 보존한다. 다운로드 ZIP과 중간 로그는 게임 `Builds/ModelingDownloads/SchoolRenewal/shoe-bank`에 둔다.
- Blender 4.0.2와 저장소 공통 `../finish_asset.py`로 원본의 복제본만 LOD화한다. 1.05m 높이, 12,000 / 3,000 삼각형 상한으로 두 LOD를 내보내고, 원본 색/ORM을 정돈된 UV로 전사하며 실제 원본 형상의 tangent normal을 베이크한다. Near 2K / far 1K이다.
- `shoe-bank.blend`에는 변경하지 않은 원본 컬렉션, full-resolution packed PBR, 편집 가능한 게임 LOD 및 재현 코드가 들어간다. `export.json`과 `Assets/Resources/SchoolRenewal/shoe-bank/asset.json`이 실제 치수·면 수·파일 해시를 기록한다.
- CLI 생성 뒤 동일 작업을 웹앱에서 회전하며 형상/재질을 확인하고, Unity에서 원래 학교 공간의 크기·조명·충돌과 함께 검증한다. 이 자료의 생성 전 설정은 그러한 최종 검토를 대신하지 않는다.

## 완료된 형상·LOD 확인

생성 원본의 복제본만 **3mm voxel 표면 수선**으로 정리한 후 높이 1.05m와 표준 앞면 Blender +Y로 맞췄다. 완성 LOD는 **12,000 / 3,000 삼각형**이며 실제 크기는 약 1.556×1.050×0.417m다. 원본의 실제 16개 수납 공간과 뒤판을 유지하며 기본 도형으로 재조립하지 않았다.

`validate_shoe_bank.py`는 실제 FBX 양 LOD 각각의 16개 칸이 앞에서 비어 있고 뒤판을 가지는지 확인한다. 중간 깊이의 다섯 가로 판도 실제 면으로 남아 있으며 최저 두께는 near 25.09mm / far 25.87mm로 15mm 이상이다. 결과는 `validation.json`에 있다. 원본 GLB/PBR는 변경하지 않고 각 게임 LOD의 색·ORM·실제 tangent normal을 원본에서 전사했다.

## 고지와 재현

참조 이미지와 생성 자산은 이 프로젝트 작업을 위해 제작했다. 별도의 공개 스캔이나 구매 prop을 새 모델의 원본으로 사용하지 않았다. 생성 도구의 라이선스와 생성 결과의 권리를 혼동하지 않으며, 보존된 upstream 도구 고지는 `../../SchoolRetractableGate/TRELLIS.2-LICENSE.txt`를 참조한다. 원본 GLB와 Blender 소스는 저장소의 Git LFS 설정을 따른다. 개인 CLI 상태·인증 쿠키·비밀번호는 소스에 포함하지 않는다.
