# 폐교 환경 · 2026-10-22

사용자가 첨부한 `References/school-horror-reference.png`의 젖은 타일, 층층이
벗겨진 회벽, 오염된 교실 유리, 드문 황색 형광등과 차가운 방 안의 명암을
현재 학교 관찰 복도 버전 3에 적용한다. 원본 입력과 SHA256·용도는
`References/provenance.json`에 보존한다. 이미지는 시각 참고용이며 게임 텍스처로
투사하지 않는다.

두 번째 마감은 실제 촬영 스캔을 추가로 확보해 밋밋했던 회벽과 체크무늬 바닥을
교체하고, 천장의 실제 개구부·두꺼운 박리/파손 가장자리·촬영 스캔 기반 잔해와
실제 장면을 비추는 젖은 바닥의 평면 반사를 적용한다. 확보한 원본·라이선스·
다운로드 기록과 편집 제작 소스를 모두 프로젝트에 보존한다.

기존 4.4m × 38.4m의 연결 복도·실제 문/유리 충돌·닫힌 교실·양 끝 연결 이동,
지도/저장 버전과 음악실 생성 모델을 유지한다. 학교 관찰 모드에서만 적용하며
기존 학교 저장과 회랑에는 이 표면·안개·추가 광원을 적용하지 않는다.

## 소유 경계

- `SchoolLoopArchitecture`: 학교별 PBR 건축, 물리 동선과 창틀/문. 복도 천장은
  60cm 마감판과 별도 구조 슬래브이며, 누수 구간의 마감판을 실제로 제거한다.
- `SchoolHorrorSurfaces`: 확보한 CC0 촬영 스캔을 파생한 2K 벽·천장·타일·유리,
  표면별 roughness/normal/AO, 두 공간 스케일의 습기. 공유 원본을 변경하지 않는다.
- `SchoolDecayDressing`: 기존 제작 원본이 있는 책상·의자·선반/캐비닛 재사용,
  매점/정독반 마감, 신발장·녹슨 원형 배관·말린 종이/낙하 석고. 소품 충돌은
  중앙 구역 바깥 가장자리에만 두고 시각 잔해에는 충돌을 만들지 않는다.
- `SchoolStructuralDecay`: 두께가 있는 박리 회벽·처진 파손 마감·노출 천장틀과
  실제 스캔에서 파생한 골재/콘크리트 잔해를 묶는다. 제작 원본과 Blender 재현은
  `StructuralDecay/`에 있다. 중앙 통행 경로에는 새 잔해 충돌을 만들지 않는다.
- `SchoolHorrorLighting`: 실제 형광등 모델의 부분 점등, 차가운 창문 국소광,
  작은 붉은 경보등, 학교 전용 색보정, 카메라 범위 안개, 128px box-projected
  반사 프로브 2개. 프로브는 초기 한 차례씩 순차 캡처한다.
- `SchoolPlanarReflection`: 실제 플레이어 시점의 반사 카메라와 최대 640×360 HDR
  타깃 하나를 소유한다. 바닥의 사선 클리핑과 재귀 차단, 카메라 범위 shader 상태
  복원을 적용하며, 반사에는 후처리를 중복 적용하지 않는다. 바닥 거칠기에 따라
  mip을 섞고 마른 구간에서는 반사를 줄인다. 장면 종료 때 카메라/타깃을 해제한다.
- `SchoolFluorescent` / 독립 `Core/SchoolFluorescentClock`: 실제 광원/튜브 발광과
  국소 산란의 같은 완만한 전압 저하 시계. 일시정지에서 멈추고 동작 감소에서는
  안정된 출력과 정적인 안개를 사용한다.

건축/배관/종이와 박리 마감 형상은 미터 단위 건축 코드다. 새 AI 기본 모델은 만들지 않았다.
책상/의자와 기존 생성 선반·피아노·악보대는 원래 메시/제작 원본과 라이선스를
사용한다. 표면 스캔 출처와 재현 환경은 [SURFACES.md](SURFACES.md), 입력·출력과
제작 코드 해시는 `surface-manifest.json`에 있다.

추가 확보 표면은 Poly Haven의 `rough_plaster_broken`, `worn_mossy_plasterwall`,
`peeling_painted_wall`, `dirty_tiles`다. 2K 원본 16장의 실제 크기·공식 MD5·SHA256과
API/CC0 기록은 `Acquired/`에 있다. 잔해는 `concrete_debris`의 실제 높이 스캔과
`namaqualand_stones_01`의 개별 사진측량 메시를 사용한다. 전자는 두꺼운 높이장
패치이며 완전한 부피의 잔해 스캔이라고 주장하지 않는다.

## 재현·검증

Unity **6000.6.0f1**, URP **17.6.0**, Python **3.11**, 고정 패키지는
`requirements-surfaces.txt`다. 저장소 루트에서 실행한다.

```powershell
python -m pip install -r HappyToyV2/SourceArt/SchoolHorror/requirements-surfaces.txt
python HappyToyV2/SourceArt/SchoolHorror/build_surfaces.py
python HappyToyV2/SourceArt/SchoolHorror/build_notices.py
$env:HAPPYTOY_SCHOOL_HORROR_REVIEW_DIR = (Join-Path (Resolve-Path HappyToyV2).Path 'Builds/SchoolHorrorReview-new')
python HappyToyV2/Tools/quality/run_feature_tests.py --profile school-horror --no-desktop-interaction
Remove-Item Env:HAPPYTOY_SCHOOL_HORROR_REVIEW_DIR
```

`school-horror`는 Core 규칙과 학교 지도/저장 검사, 실제 양방향 키 이동·아이템·
닫힌 문/유리, 생성 음악실 메시/텍스처 정체성, 재질 해제·조명 일시정지·카메라
환경 원복과 실제 평면 반사의 화면 기여·GL/셰이더 상태 원복·카메라/타깃 해제,
손전등 켜짐/꺼짐·누수/파손 천장/박리 벽 근접을 포함한 9개 실제 URP 카메라를 검사한다. 명시한 환경 변수가 있을 때만
1920×1080 원본 게임 렌더를 `Builds/`에 저장한다. 촬영물·빌드·테스트 로그는
파생 검토 자료이며 Git 소스에 누적하지 않는다. Windows 빌드는 기존
`HappyToy.V2.Editor.QualityValidation.BuildWindows -v2-release-player`를 사용한다.
복도/누수 시점에는 실제 반사 타깃의 선형 HDR EXR와 별도 Reinhard/sRGB 진단
미리보기·카메라/투영 기록도 저장한다. 이 미리보기는 최종 게임 카메라 화면과
구분하며, 실제 광원 위치와 바닥 투영의 일치를 확인하는 데 사용한다.

새 플레이어의 선택 무음 표면 검토는 다음 인자로만 설치된다. 일반 플레이에는
카메라 촬영이나 진단 오브젝트를 만들지 않는다. 소유 음소거 guard와 별도 기록
폴더를 사용한다. `-batchmode`와 `-v2-audit-no-desktop-interaction`의 실제 커서 쓰기
억제를 학교 생성 전에 확인하며, 배치 실행에서는 생산 카메라의 StandardRequest를
160×90 화면 밖 렌더 타깃으로 매 프레임 요청해 분할 반사 캡처의 진행을 유도한다.
실제 `ReflectionReady` 완료를 최대 25초만 기다리며, 완료되지 않으면 실패로 기록한다.
`reflection-progress.json`과 최종 `school-horror-review.json`에는 단계·렌더 요청 수·
반사/조명/품질 상태와 실제 완료 여부를 남긴다. 완료한 경우에만 일시정지 후 실제
URP 카메라 PNG를 저장한다. 임시 타깃과 카메라 설정은 성공/실패 모두 정리·복원한다.
실제 표시 FPS/청감/자연 플레이 인증은 아니다.

```powershell
& '<fresh-build>/HappyToyV2.exe' -batchmode -quiet-diagnostics -v2-audit-no-desktop-interaction -v2-school-horror-review-output '<new-evidence-folder>' -v2-school-horror-review-torch -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile '<new-evidence-folder>/Player.log'
```

6종 한국어 공지/시간표는 기존 촬영 종이 표면과 저장된 OFL 한국어 글꼴을
사용해 제작한다. 원문·폰트·생성 도구·해시는 `NOTICES.md`와
`notice-manifest.json`에 있다. 누수 위치의 처진 천장 마감판은 실제 두께와
불규칙 가장자리를 가진 건축 메시다. 천장 마감판이 실제로 빠진 구간 뒤에
구조 슬래브와 프레임이 보이며 구조 슬래브 자체를 뚫었다고 주장하지 않는다.

## 표현 범위와 남은 고품질 리소스

실제 촬영 스캔과 사진측량 자산 위에 저작한 손상 지도를 쓴 환경이다. 참조 장소 전체를 촬영한
포토그래메트리나 레이트레이싱 재구성은 아니다. 현재 간접광은 국소 광원과
반사 프로브로 근사하며 젖은 바닥에는 실제 장면의 평면 반사를 더한다. 안개는
거리 안개와 일부 광원 아래의 깊이 차폐 단일 산란이다. 전체 볼류메트릭 그림자나
실시간 전역 조명·레이 트레이싱을 계산하지 않는다.

레퍼런스에 더 가까운 최종 아트에는 오염·깨짐이 서로 다른 학교 창문/문 세트,
소화전·한국어 공지·교구의 더 많은
고유 고해상도 소스가 필요하다. 현재는 기존 모델과 재사용 가능한 스캔 파생
재질로 구성한 플레이 가능한 환경 마감이며, 참조와 동등한 최종 품질이나
사람이 느끼는 공포의 강도를 검증 결과로 주장하지 않는다.
