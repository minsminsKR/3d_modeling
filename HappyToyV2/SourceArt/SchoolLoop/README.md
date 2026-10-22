# 학교 관찰 루프 · 지도 버전 3

사용자가 제공한 `References/school-loop-target-20261021.png`를 기준으로 학교를
한 층의 긴 복도로 교체한다. 입구에서부터 매점·정독반·음악실이 왼쪽에 이어진다.
이 단계에서 매점과 정독반은 방·문·창문·조명만 구성하고 음악실만 상세하게 마감한다.
이상현상의 생성/발견/성공/실패 및 8회 탈출 규칙은 이번 지도 제작 범위에 포함하지 않는다.

복도는 폭 4.4m, 길이 38.4m, 천장 3.25m다. 남서쪽 입구와 북동쪽 끝의 연결부를
걸으면 어느 쪽이든 같은 입구와 같은 방 순서로 이어진다. 앞쪽은 평행 연결,
뒤쪽은 입구 중심을 기준으로 180° 접힌 연결이다. 뒤쪽 이웃의 모델도 같은 강체
회전으로 미리 배치하므로 음수 scale이나 한글/모델 좌우 반전을 사용하지 않는다.
주 구역과 이웃 두 구역의 외형이 공유 메시·재질로 존재하며, 실제 물리/NavMesh는
주 구역만 소유한다. 연결 평면 통과 시 몸·시선의 상대 관계를 해당 이웃과 같은
변환으로 옮긴다. 일시정지 중에는 전환하지 않는다. 연결 횟수는 양쪽 모두 증가한다.
기존 버전3 저장 좌표·횟수·장비는 유지한다.

교실 출입문은 실제 닫힌 문 몸체로 막혀 있고 E로 잠겼음을 확인할 수 있다.
정독반은 도면처럼 앞뒤 두 출입문을 가지며, 음악실과 매점은 한 출입문씩 가진다.
복도 창문도 닫힌 투명 유리와 물리 몸체를 가진다. 문이나 유리를 통해 입실하지 않는다.
입구 철문은 `../SchoolRetractableGate/`의 생성 원본을 사용한, 한쪽에 접힌 자바라다.
자유단에 보관용 자물쇠 하나가 달려 있고 열린 상태로 고정되어 있다. 벽·창틀·천장 이음매·게시판·배관은
미터 단위의 편집 가능한 C# 건축 소스이며 실제 지지면·충돌과 일치한다.

- `Core/SchoolLoopPlan.cs`: Unity에 의존하지 않는 치수/방/연결 평면·이웃/복귀 강체 변환 규칙.
- `SchoolLoopWorld`: 배치 조립, 구역별 메시 배칭, NavMesh, 실제 플레이어 연결 전환과 소유 자원 정리.
- `SchoolLoopArchitecture`: 지도에 일치하는 건축·닫힌 문/창문·고정 철문·국소 형광등.
- `SchoolRetractableGate`: 생성 자바라/PBR·가이드 레일·보관 자물쇠와 열린 입구의 국소 조립/물리 외곽.
- `SchoolMusicRoomDressing`: 생성 피아노·악보대와 재사용 좌석·칠판·음향 마감 배치.
- `LegacySchoolRetirement`: 새 학교에서만 이전 지형·조명·충돌·배우를 제외한다.
- `SchoolLoopCheckpoint`: 지도 버전 3의 위치·연결 횟수·아이템·손전등 복원 경계.

메뉴의 **학교 복도**와 회랑 제단 이후의 실제 전환은 버전 3으로 들어간다.
기존 버전 1/2 저장 형식과 직렬화된 원본/GUID는 유지한다. 구형 저장과 기존 회귀
검사만 `BeginChapter`/`CreateChapter` 및 해당 버전을 명시적으로 사용할 수 있다.
현재 플레이 경로는 `BeginSchoolLoop`/`CreateSchoolLoop`다. 새 구역은 이전 학교의
계단·기억 수집·몬스터 추격·등장 이벤트를 시작하지 않는다. 회랑은 기존대로 동작한다.

음악실에는 새 그랜드피아노/벤치, 학생 의자 12개, 악보대 5개, 오선 칠판, 목재
음향 마감과 바깥 창문을 배치한다. 피아노와 악보대는 지정 웹앱의 실제 생성 결과다.
좌석은 기존 `Assets/Art/Finished/classroom-chair.fbx`를 공유하고 새 PBR prefab을
사용한다. 두 생성 원본 GLB/8K PBR, 실제 입력·전처리 이미지·프롬프트, 저장 완료
작업 기록, 편집 가능한 packed `.blend`, 원본 기반 LOD와 standalone 재현 도구를 보존한다.

재현 환경은 **Blender 4.0.2 / Unity 6000.6.0f1**다. 저장소 루트에서 실행한다.
출처 기록 도구는 가져오기·테스트 중인 Unity가 종료된 뒤 실행하며, 새 리소스의
텍스트 줄바꿈을 저장소의 LF 정책과 맞춘 후 실제 파일 해시를 기록한다.

```powershell
blender -b --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/SchoolLoop/finish_music_assets.py
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.SchoolLoopAssetsImport.Apply
python HappyToyV2/SourceArt/SchoolLoop/record_music_sources.py
python HappyToyV2/Tools/quality/run_feature_tests.py --profile school-loop --no-desktop-interaction
```

원본을 생성해야 할 때는 사용자 지정 웹앱으로 돌아간다. 현재 원본을 다른 생성기나
절차적 대체 모델로 교체하지 않는다. 근접 LOD는 실제 원본 atlas를 유지하며,
먼 LOD는 원본 표면에서 작은 voxel 단위로 정리한 뒤 색/ORM을 재베이크한다.
각 `export.json`의 실제 삼각형 수·경계·해시와 게임 `asset.json`을 함께 확인한다.
자동 감면이 목표를 초과하면 도구가 실패하며, 감면하지 않은 메시를 LOD라고 내보내지 않는다.

최종 검증은 실제 메뉴, 닫힌 문/유리, 양방향 실제 키 이동, 저장/복원과 원본 모델·맵
정체성, 실제 창문/복도/위에서 본 Unity 화면을 확인한다. 화면·빌드·로컬 검증 산출물은
`Builds/` 아래의 파생물이다. 스크린샷만으로 실제 플레이어 FPS나 생존 밸런스를 인증하지 않는다.

2026-10-23의 양방향 접힌 연결은 `school-loop`와 `school-horror`가 검사한다.
후자는 규칙91/통합13개를 통과했다. `SchoolLoopSeamTests`는 실제 두 연결부의
이웃과 복귀 지점에서 간판·자바라 기준점의 화면 투영을 비교하고 정상
StandardRequest 카메라 밝기 급변을 검사한다. `HAPPYTOY_SCHOOL_LOOP_REVIEW_DIR`를
새 Builds 하위 폴더로 명시할 때만 앞/뒤 통과 전후 PNG4장을 저장한다.
이 변경은 양 끝의 꺾인 통로에서 전환하며, 복도 중간의 시선 회전만으로 지형을 재배치하지 않는다.

참조 이미지는 내장 image_gen.imagegen 도구로 생성했다. 실제 입력과 최종 프롬프트는
[피아노](grand-piano/reference-generation.json)와 [악보대](music-stand/reference-generation.json)에 보존한다.
