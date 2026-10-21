# 학교 인쇄물과 손상된 천장 마감

`build_notices.py`는 이미 저장된 사진 기반 `GraphicsPbr/paper-aged/albedo.png`,
`SchoolHorror/wall-alternate/albedo.png`와 `Assets/Resources/Fonts/Korean.ttf`를
입력으로 사용한다. 시스템 글꼴이나 네트워크에
의존하지 않는다. 글꼴 원래 SIL OFL 1.1 전문은 같은 `Fonts/OFL.txt`에 있고,
종이 사진의 원래 출처·권리는 `ThirdParty/Graphics`의 기존 크레딧에 유지한다.

6개 양식의 한국어 문구는 이 프로젝트에서 직접 쓴 일반적인 학교 안내문이다.
특정 학교의 실제 문서나 외부 로고를 복제하지 않았다. 실제 사진의 종이 섬유와
공간적으로 이어지는 회색 물때 경계·대각선 접힘·옅어진 복사 인쇄를 결정적으로
조합한다. 미장 사진은 Poly Haven `peeling_painted_wall` CC0 스캔에서 유래한
약한 표면 오염 전사에 사용한다. 원본·저자·라이선스는 `surface-manifest.json`과
`Acquired/`에 보존한다.
생성 이미지의 원본 입력과 결과 SHA-256은 `notice-manifest.json`에 기록한다.

고정된 Python 3.11/3.12 환경에서 같은 폴더의 `requirements-surfaces.txt`를 설치한 뒤,
저장소 루트에서 다음을 실행한다.

```powershell
python HappyToyV2/SourceArt/SchoolHorror/build_notices.py
```

결과는 `Assets/Resources/SchoolHorror/notices/notice-0.png` ~ `notice-5.png`다.
`SchoolDecayDressing`은 기존 종이 PBR을 복제해 각 인쇄 알베도를 적용하고,
종이의 실제 Normal/AO/거칠기 맵을 함께 사용한다. 곡면 종이 메시의 가장자리는
말리고 모서리 삼각형 일부가 실제로 빠진 모양이며, 물리나 이동 경로를 만들지
않는다. 바닥에는 구간당 238장의 전체/부분 잎을 10개의 겹친 가장자리 더미로
배치한다. 게시판은 2/8/1/6/3/7장으로 밀도와 회전을 달리하고 일부만 겹친다.

예전 가짜 처진 천장판은 제거했다. 천장 마감의 실제 빠진 셀과 별도 위쪽 구조
슬래브는 `SchoolLoopArchitecture`가 소유하고, 파손 단면·노출 철망·지지선은
`SchoolStructuralDecay`가 소유한다. 모든 파생 메시·재질은 학교 구역과 함께
해제되며, 원래 FBX·사진·글꼴은 변경하지 않는다.
