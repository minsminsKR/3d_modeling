# 낡은 학교 교실문

닫힌 문 한 장과 금속 손잡이·경첩·유리 관찰창을 독립된 생성 모델로 제작한다. 문틀·충돌·잠금 표시는 Unity 학교 조립이 소유한다.

## 생성과 마감

- `reference.png`는 OpenAI built-in imagegen으로 만든 개별 객체 참조이다. 정확한 입력은 `reference-prompt.txt`, 생성 옵션과 요청/작업 식별자는 `generation-settings.json`에 있다.
- 지정 A100 모델링 서비스의 CLI로 TRELLIS.2 / 1024 cascade / 4K PBR / 12 steps / split GPU를 명시해 생성한다. 실제 기록의 옵션과 SHA-256이 최종 기준이다.
- 변경하지 않은 `original.glb`와 원본의 내장 PBR, 생성 기록 및 처리 입력을 보존한다. 다운로드 ZIP과 중간 로그는 게임 `Builds/ModelingDownloads/SchoolRenewal/classroom-door`에 둔다.
- Blender 4.0.2와 저장소 공통 `../finish_asset.py`로 원본의 복제본만 LOD화한다. 2.44m 높이, 10,000 / 3,000 삼각형 상한으로 두 LOD를 내보내고, 원본 색/ORM을 정돈된 UV로 전사하며 실제 원본 형상의 tangent normal을 베이크한다. Near 2K / far 1K이다.
- `classroom-door.blend`에는 변경하지 않은 원본 컬렉션, full-resolution packed PBR, 편집 가능한 게임 LOD 및 재현 코드가 들어간다. `export.json`과 `Assets/Resources/SchoolRenewal/classroom-door/asset.json`이 실제 치수·면 수·파일 해시를 기록한다.
- CLI 생성 뒤 동일 작업을 웹앱에서 회전하며 형상/재질을 확인하고, Unity에서 원래 학교 공간의 크기·조명·충돌과 함께 검증한다. 이 자료의 생성 전 설정은 그러한 최종 검토를 대신하지 않는다.

## 완료한 모델 검토와 게임용 마감

- 실제 웹앱 작업 `31ea452d-6f95-440d-a4d9-b958be4c4c40`의 정면과 회전 시점을 검토했다. 노후한 회녹색 도장, 문짝의 표면 깊이, 손잡이·경첩, 좁게 매립된 철망유리 관찰창을 확인했으며 기록은 `web-review.json`에 있다. 관찰창은 때가 낀 불투명 유리로 유지한다.
- `finish_geometry.py`는 원본의 게임용 복제본에만 3mm voxel 표면 정리를 수행한다. 원본 GLB·PBR 및 고해상도 소스 컬렉션은 보존한다. 새로 만든 기본 도형을 생성 모델의 대용으로 쓰지 않는다.
- 최종 내보내기는 10,000 / 3,000 삼각형, near 2K / far 1K UV·PBR·실제 high-to-low normal이다. 실제 FBX를 다시 가져오면 퇴화 면 4개가 정리되어 9,996 / 3,000 삼각형이 된다. `validate_door.py`와 `validation.json`은 두 LOD에서 닫힌 문판, 25.5 / 24.1mm 관찰창 매립 깊이, 약 139mm 손잡이 돌출을 확인한다.
- 정규화된 소스의 전체 크기는 약 1.046m 폭 × 2.440m 높이 × 0.347m 깊이이며, Blender +Y가 손잡이가 있는 정면이다. `measure_core.py`와 `leaf-core-measurements.json`은 손잡이·경첩을 제외한 실제 문판 범위를 별도로 기록한다. 이 생성 모델은 문틀 없는 문짝이므로 Unity가 기존 건축 문설주와 상단 보를 구조물로 조립한다.
- 최종 Blender 렌더와 실제 FBX 검사는 생성 형상 및 마감 검증이다. 학교 공간의 최종 배치·충돌·조명과 플레이어 검증 결과는 Unity 통합 검증 기록에서 확인한다.

## 고지와 재현

참조 이미지와 생성 자산은 이 프로젝트 작업을 위해 제작했다. 별도의 공개 스캔이나 구매 prop을 새 모델의 원본으로 사용하지 않았다. 생성 도구의 라이선스와 생성 결과의 권리를 혼동하지 않으며, 보존된 upstream 도구 고지는 `../../SchoolRetractableGate/TRELLIS.2-LICENSE.txt`를 참조한다. 원본 GLB와 Blender 소스는 저장소의 Git LFS 설정을 따른다. 개인 CLI 상태·인증 쿠키·비밀번호는 소스에 포함하지 않는다.
