# 회랑 폴리 후보 비교 — 2026-10-09

목재 발자국 후보는 기존 Kenney 목재 접촉, evghenifloca의 오래된 마루 걷기,
vrodge의 삐걱대는 목재 바닥 걷기다. 두 Freesound 후보 모두 게시 페이지의
CC0 표기를 확인했다. 공개 고음질 MP3 미리듣기를 사용했으며 원본 WAV/AIFF와
구분한다. MP3를 WAV로 변환해도 원 녹음의 압축 품질이 복원되는 것은 아니다.

evghenifloca를 선택했다. 12.125초의 모노 녹음 안에서 서로 다른 다섯 접촉을
분리할 수 있고, 짧은 기존 접촉음보다 뒤따르는 마루판 마찰을 함께 보존한다.
vrodge의 23.423초 스테레오 녹음은 상대적으로 접촉 강도와 방 잔향 차이가 커서
반복 걷기용으로 사용하지 않았다. 비교 원본 두 개는 해시와 함께 보존한다.

발각음은 이전의 금속 낙하+비명과 금속 낙하+금속 끌기+겁먹은 호흡을 비교했다.
후자를 선택해 실제 금속의 거친 잔향을 중심으로 1.75초의 짧은 꼬리를 만들고
사람의 호흡은 낮은 레벨로 배치했다. 발각 이벤트의 방향/쿨다운/음소거 규칙을 따른다.
오실레이터·절차적 노이즈·다른 게임에서 추출한 사운드는 사용하지 않는다.

이 선택 기록과 PCM 수치는 사람의 공포 반응이나 최종 기기 청취 평가를 인증하지 않는다.
게임 안의 실제 걷기·발각·추격·캐비닛·일시정지 믹스는 별도로 검증해야 한다.

- [evghenifloca — Squeaky Wooden Parquet Footsteps, CC0](https://freesound.org/people/evghenifloca/sounds/817482/)
- [vrodge — Footsteps wooden floor creaking, CC0](https://freesound.org/people/vrodge/sounds/119522/)
- 나머지 원 녹음: `../RecordedHorror/sources.json`

공간음은 기존 LEGIT Audio 냉장 장치 실내음, vhio의 폐방어기지 실내 현장 녹음,
szegvari의 Dark Scape Old House를 비교했다. vhio는 녹음 장소·Zoom H5 장비·
녹음 방식이 명시되어 실제 바람과 공기 소리의 출처를 확인할 수 있어 선택했다.
Dark Scape는 사운드 디자인 드론으로 분류되어 이번 실제 공간음 교체에서 제외했다.
395초, 230초, 95초부터 각각 24초를 잘라 모노 변환·필터링·랩 크로스페이드로
22.5초 루프 세 개를 만들었다. 원 소스에는 먼 도로 소리가 있다는 사실도 기록했다.

공격음 네 개는 기존 부드러운 충격+유리/삐걱임 조합을 대신해 실제 작업실의
나무 접촉·래칫, 사람의 긴장된 호흡·유아 발성으로 구성했다. 동작 동기화를 위해
0.46초 길이를 유지했고, 공격 판정이나 몬스터 AI·모델에는 변화를 주지 않았다.

- [vhio — 폐방어기지 실내 바람·공기 현장 녹음, CC0](https://freesound.org/people/vhio/sounds/791287/)

재생성: CPython 3.11에 `SourceArt/Audio/requirements-corridor.txt`를 설치하고
`python SourceArt/Audio/render_corridor_foley.py` 실행. 기존 23개 녹음 재생성 도구를
돌린 경우 이 도구를 마지막에 실행해야 선택한 발각음이 유지된다.
