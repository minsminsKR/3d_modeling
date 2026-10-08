# 폐교 공간 설계 원본

새 학교 장의 지도 버전은 **2**다. 제작 원본은 별도 로컬 씬 생성기가 아니라
`Assets/Scripts/SchoolCampusLayout.cs`의 미터 단위 공간·문·계단 계획과
`SchoolCampusArchitecture.cs`, `SchoolCampusRooms.cs`의 편집 가능한 C# 구성이다.
Unity 6000.6.0f1에서 학교 장을 시작하면 동일한 지형·충돌·내비게이션을 재현한다.

- 1층 기존 입구·교실·화장실을 유지하며 동쪽에 6개 독립된 방과 북·남측 순환복도를 만든다.
- 2층은 음악실, 기록실, 두 교실, 미술실, 악기 보관실로 구성한다.
- 지하는 인형 보관실, 기계실, 폐기 문서고, 시설 관리실, 봉인 보관실, 자재 창고로 구성한다.
- 실제 교실 문 28개, 새로운 은신 캐비닛 5개와 층별 유도 표지를 배치한다.
- 기존 두 계단의 위치·폭·25단 높이와 뛰어다니는 배우의 물리 이동을 유지한다.

건물은 좁은 복도, 실제 벽과 문으로 둘러싸인 방, 두 출입구가 있는 주요 방을
구분한다. 음악실은 복도 안에 열린 공간으로 섞이지 않는다. 문을 열고 닫는 잎은
정적 NavMesh에서 제외하며, 플레이어 충돌과 몬스터의 문 통과 검사에서 실제로 다룬다.

책상·의자·침대·피아노·악보대·서류 가구는 Git에 포함된 원본 학교의 기존 형상과
편집 원본을 재사용한다. 새 벽·창틀·배관·설비 외형은 위의 C# 코드가 편집 원본이며
치수와 UV를 미터 단위로 생성한다. PBR 사진 재질과 기존 프롭의 출처·라이선스는
`ThirdParty/Graphics/manifest.json` 및 `Graphics-Credits.txt`를 따른다. 추가 외부
다운로드·프로젝트 밖 스크립트·절대 워크스테이션 경로는 필요하지 않다.

기존 `Assets/Annex/SchoolAnnex.unity`는 보존한다. 학교 중단 저장의
`simulationVersion=1`은 원래 학교, `simulationVersion=2`는 새 캠퍼스를 선택한다.
`CreateChapterForCheckpoint`가 버전을 선택하며 서로 다른 지도에 저장 좌표를
적용하지 않는다. 두 버전 모두 학교 슬롯과 조명·진행·문·배우 상태를 보존한다.

검증 도구는 `CloudSchoolCampusTests.cs`, `CloudSchoolStairPursuitTests.cs`,
`SchoolCampusAudit.cs`, `SchoolStairAudit.cs`다. 일반 빌드의 선택 검증 인자는
`-v2-school-campus-output <출력 폴더>`와 `-v2-school-stair-output <출력 폴더>`다.
출력은 검증용으로 배우/카메라를 제어하며 플레이 생존이나 GPU 성능을 인증하지 않는다.
