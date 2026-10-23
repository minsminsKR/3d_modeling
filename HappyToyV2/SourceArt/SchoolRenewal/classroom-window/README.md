# 낡은 학교 복도 창틀

네 칸 미닫이와 상부 환기창을 가진 독립된 생성 창 조립품이다. 기존 학교의 교실 시야를 유지하기 위해 생성 유리의 불투명 여부를 확인하며 필요한 경우 파생 메시에서 유리 면만 분리한다.

## 생성과 마감

- `reference.png`는 OpenAI built-in imagegen으로 만든 개별 객체 참조이다. 정확한 입력은 `reference-prompt.txt`, 생성 옵션과 요청/작업 식별자는 `generation-settings.json`에 있다.
- 지정 A100 모델링 서비스의 CLI로 TRELLIS.2 / 1024 cascade / 4K PBR / 12 steps / split GPU를 명시해 생성한다. 실제 기록의 옵션과 SHA-256이 최종 기준이다.
- 변경하지 않은 `original.glb`와 원본의 내장 PBR, 생성 기록 및 처리 입력을 보존한다. 다운로드 ZIP과 중간 로그는 게임 `Builds/ModelingDownloads/SchoolRenewal/classroom-window`에 둔다.
- Blender 4.0.2와 저장소 공통 `../finish_asset.py`로 원본의 복제본만 LOD화한다. 1.6m 높이, 8,000 / 2,500 삼각형 상한으로 두 LOD를 내보내고, 원본 색/ORM을 정돈된 UV로 전사하며 실제 원본 형상의 tangent normal을 베이크한다. Near 2K / far 1K이다.
- `classroom-window.blend`에는 변경하지 않은 원본 컬렉션, full-resolution packed PBR, 편집 가능한 게임 LOD 및 재현 코드가 들어간다. `export.json`과 `Assets/Resources/SchoolRenewal/classroom-window/asset.json`이 실제 치수·면 수·파일 해시를 기록한다.
- CLI 생성 뒤 동일 작업을 웹앱에서 회전하며 형상/재질을 확인하고, Unity에서 원래 학교 공간의 크기·조명·충돌과 함께 검증한다. 이 자료의 생성 전 설정은 그러한 최종 검토를 대신하지 않는다.

## 완료된 형상 마감

생성 원본의 미세한 비다양체 연결이 목표 LOD 감면을 막아, 복제본만 **4mm voxel 표면 수선**을 한 뒤 실측 8개 유리 영역을 제거했다. 원본의 불투명 유리는 그대로 보존하고 게임에서는 기존 학교의 투명 유리를 새 프레임 뒤에 조립한다. `pane-cutouts.json`은 정확한 경계를, `finish_geometry.py`는 재현되는 수선/분리와 개구 검사 절차를 기록한다.

완성 LOD는 **7,998 / 2,500 삼각형**이다. `validate_window.py`로 실제 FBX 각각의 8개 창에 72개 시선을 검사해, 총 144개 ray가 실제 메시를 통과함을 확인했다. `validation.json`에 결과가 있다. 원본의 4K PBR과 geometry는 변경하지 않았고, 게임용 2K/1K 색·ORM·tangent normal은 이 원본에서 전사했다.

## 고지

참조 이미지와 생성 자산은 이 프로젝트 작업을 위해 제작했다. 별도의 공개 스캔이나 구매 prop을 새 모델의 원본으로 사용하지 않았다. 생성 도구의 라이선스와 생성 결과의 권리를 혼동하지 않으며, 보존된 upstream 도구 고지는 `../../SchoolRetractableGate/TRELLIS.2-LICENSE.txt`를 참조한다. 원본 GLB와 Blender 소스는 저장소의 Git LFS 설정을 따른다. 개인 CLI 상태·인증 쿠키·비밀번호는 소스에 포함하지 않는다.
