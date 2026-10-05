# Happy Toy V2 상황별 사운드 — 2026-10-06 회랑 업데이트

현재 카탈로그는 61 WAV다. CC0 59개와 CC BY 3.0 캐비닛 문 녹음 2개를 사용하며, 각 파일의 원본·변환·SHA-256은 `ThirdParty/Audio/manifest.json`에 기록한다. 필수 크레딧 `ThirdParty/Audio/Audio-Credits.txt`는 새 Windows 빌드 옆에도 복사한다.

캐비닛 진입은 `cabinet-close` 한 클립, 이탈은 `cabinet-open` 한 클립만 재생한다. fractilegames가 실제 캐비닛을 여닫아 녹음한 [Creaky light wooden door](https://opengameart.org/content/creaky-light-wooden-door)를 열림·닫힘으로 잘라 mono 24kHz PCM16, 필터·경계 페이드·게인을 적용했다. 옷자락이나 별도 금속 충격을 섞지 않으며, 연속 조작은 앞 클립을 교체한다. 녹음이 없을 때도 단일 기계적 문소리로 대체한다.

Cyclopse·Hwacat·Uncat·Baby의 배회 발소리는 OwlishMedia의 CC0 [Sound Effects Pack](https://opengameart.org/content/sound-effects-pack)에 있는 실제 신발 녹음 세 가지씩을 순환한다. 원본 하나씩의 필터·게인만 조정하며 금속·유리 충격을 겹치지 않는다. 나무에서는 정체성별 신발 녹음, 실제 타일과 고인 물에서는 `step-stone` 5종과 `step-wet` 3종을 선택한다. 떠다니는 Lantern·Wraith와 실제 공격 큐는 각각의 기존 정체성을 유지한다.

기본 시작은 무작위 회랑이며 학교는 별도 장이다. 최신 검증 범위와 실행 후보는 `CORRIDOR_FOCUS.md` 및 `Verification/corridor-focus/`를 따른다. 학교 기억 공명은 다음 순서의 기억으로 안내하고, 실제 회수는 등장 조건을 계속 따른다.

## 아래 기록은 2026-10-05의 51개 카탈로그 단계 이력

아래 파일 수·기본 학교 모드·캐비닛 옷자락·단일 적 이동 변형 설명은 당시 구현 기록이다. 현재 동작은 위 업데이트가 대체한다.

# Happy Toy V2 상황별 사운드 개선 — 2026-10-05

목재·타일·고인 물의 발소리, 문·캐비닛·손전등·종이 접촉, 액자 등장, 몬스터 이동·공격, 공간 환경음에 외부 CC0 음원을 연결했다. 실제 발 접촉과 문짝 이동, 등장 이벤트가 소리를 시작한다. 기존 V1 몬스터 모델과 보호된 `Assets/Annex/SchoolAnnex.unity` 씬을 유지한다.

사용자 목표는 상황에 맞는 외부 사운드를 적용하고, 그림자복도에서 느끼는 소리 듣기·시선 끊기·은신·탐색의 긴장을 갖춘 게임으로 완성도를 높이는 것이다. 이번 변경은 그 목표의 사운드 구현과 회귀 검증 단계다. 실제 기기 청감, 첫 플레이의 이해도와 긴장 곡선, 아트·성능 검수는 계속해야 한다.

## 음원과 출처

`ThirdParty/Audio/manifest.json`의 51개 항목과 `Assets/Resources/Audio/External/`의 51개 WAV가 대응한다. 총 WAV 크기는 3,507,268바이트다. 각 항목은 원본 팩·파일·제작자·출처·다운로드 URL·변환·레이어·SHA-256을 기록한다. 2026-10-05에 제작자 또는 제출자의 원본 공개 페이지에서 다섯 팩의 CC0 표기를 확인했다.

| 팩 | 제작자 / 원본 공개 페이지 | 주 용도 | 주 원본으로 기록된 파일 수 |
| --- | --- | --- | ---: |
| Impact Sounds | [Kenney](https://kenney.nl/assets/impact-sounds) | 목재·콘크리트 발 접촉, 목재·금속·유리 충격, 적의 접촉 레이어 | 24 |
| 50 RPG sound effects | [Kenney](https://opengameart.org/content/50-rpg-sound-effects) | 문, 걸림쇠, 옷자락, 삐걱거림, 종이 넘김, 스위치 | 19 |
| The Shop 무료 샘플 | [LEGIT Audio](https://opengameart.org/content/the-shop) | 1층·2층·지하의 서로 다른 저음 공간 환경음 | 3 |
| 100 CC0 SFX #2 | [rubberduck](https://opengameart.org/content/100-cc0-sfx-2) | 실제 `footstep_wet_01..03` 물 발 접촉, 베이비 이동 레이어 | 4 |
| Dripping water loop | [Independent.nu, 제출자 qubodup](https://opengameart.org/content/dripping-water-loop) | 공간에 배치한 물방울 환경음 | 1 |

위 수는 각 파생 파일의 주 원본 팩 기준이다. 적의 복합 사운드는 여러 팩을 함께 쓰며, 모든 추가 원본과 레이어 게인은 manifest에 기록된다. [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) 출처 표기 의무와 별도로 제작자 크레딧을 보존했다. Kenney의 배포 라이선스는 `Kenney-Impact-License.txt`, `Kenney-RPG-License.txt`에 원문 그대로 보관했고, 나머지 출처 기록은 같은 디렉터리의 `*-source.json`과 보존된 공개 페이지 HTML에 있다.

## 상황별 연결

| 리소스 키 | 제공되는 변형 수 | 실제 사용 |
| --- | ---: | --- |
| `step-wood` | 5 | 복도·교실·상층 목재 바닥의 거리 기반 실제 발 접촉 |
| `step-stone` | 5 | 화장실 타일·돌·콘크리트 바닥 접촉 |
| `step-wet` | 3 | 실제 고인 물 영역 접촉, 베이비 등장 시 수면 접촉 |
| `door-open`, `door-close` | 2 / 4 | 문 조작 직후 구분되는 녹음 접촉음 |
| `door-seat` | 1 | 실제 문짝이 멈춘 뒤 걸림쇠, 장난감·랜턴 등장 기구 접촉 |
| `cabinet`, `cabinet-rustle` | 1 / 4 | 은신 진입·이탈 금속 접촉과 옷자락; 캐비닛을 향한 실제 적 공격 |
| `flashlight`, `discovery` | 1 / 3 | F 입력으로 켜고 끄는 스위치, 실제 기록·기억 회수 |
| `frame-strain`, `frame-impact` | 3 / 3 | 실제 화캣 액자 삐걱거림·낙하 접촉; 마네킹 관절 긴장 |
| `ambience-ground`, `ambience-upper`, `ambience-basement-bed`, `ambience-basement` | 각 1 | 세 층의 공간 환경음과 지하의 별도 물방울 |
| `enemy-{cyclopse,hwacat,uncat,baby,lantern,wraith}-movement` | 각 1 | 기존 여섯 정체성의 실제 이동 접촉 |
| `enemy-{cyclopse,hwacat,uncat,baby,lantern,wraith}-attack` | 각 1 | 정체성별 별도 충격·옷자락·기구 레이어 |

변형 수는 카탈로그에 제공되는 수다. 플레이어 발소리는 실제 카탈로그 변형 수를 기준으로 순환하고 작은 피치 차이를 준다. 물 발소리 세 변형이 연속해서 같은 클립을 선택하던 인덱스 문제도 수정했다. 현재 문·옷자락·종이·등장 호출은 해당 뱅크의 기본 변형을 사용한다. 물 발소리는 물방울 환경음에서 잘라낸 소리가 아니라 rubberduck 팩의 물 발 접촉 원본이다. 젖은 계단 재질 이름만으로 고인 물 접촉을 만들지 않으며, 보이는 수면의 실제 영역을 확인한다.

기본 플레이 학교 모드에서는 `ChapterAtmosphere`가 `RoomAmbience.ConfigureRunPositions`를 호출해 세 음원을 실제 층 위치에 배치한다. `ambience-ground`는 1층 `(-3, 1.2, -4)`, `ambience-upper`는 2층 `(29.8, 7.1, 31)`, `ambience-basement-bed` 공간 저음은 지하 `(13.8, -3.8, -29)`에서 재생된다. `washroom-drip`, `infirmary-wiring`, `classroom-draft`는 이전 방 배치에서 남은 오브젝트 식별자이며 현재 학교 모드의 물리 위치를 뜻하지 않는다. `FloorAtmosphere`가 `ambience-basement` 물방울을 한 번만 재생하므로 같은 녹음이 중복되지 않는다. 실제 학교 층 위치와 바닥 차음은 아래 PlayMode 검증이 확인한다. LEGIT Audio의 공개 무료 샘플 두 개만 사용했고, 별도 유료 전체 라이브러리는 사용하지 않았다. 이전 Ogrebane 출처 파일은 이전 검증의 이력으로 보존하며 현재 WAV 카탈로그에는 포함하지 않는다.

학교 기억 공명은 현재 회수할 수 있는 기억만 안내한다. 아직 잠긴 다음 기억에서 미리 소리가 나거나 일시정지 중 루프가 남지 않도록 진행·정지 상태를 따른다.

## 재생과 소유권

`ExternalAudio.Shared(cue, variant)`는 `Resources`의 원본 클립을 반환한다. `Owned(cue, variant)`는 해당 클립을 `Instantiate`한 독립 복사본을 반환한다. 알려진 키의 인덱스는 변형 수에 따라 순환하고, 없거나 로드되지 않은 키는 null을 반환한다. 플레이어, 문, 적과 등장 컴포넌트는 `Owned`만 받아 제거·재시도 시 자기 클립을 해제한다. 한 컴포넌트의 정리가 공유 원본이나 다른 컴포넌트의 클립을 파괴하지 않는다.

문은 움직이는 문짝에 음원을 붙인다. 손잡이 접촉 뒤 조용한 레일 소리를 실제 이동 동안 유지하고, 실제 이동이 끝난 뒤 걸림쇠를 재생한다. 중간 방향 전환은 앞선 소리를 교체한다. 조작이 시작된 프레임은 이동 실패 시간으로 세지 않으므로 로딩·렌더 지연으로 레일이 성급하게 멈추지 않는다. 벽·바닥·거리 감쇠는 실제 음원 위치에서 계산하며 문 자신의 충돌체는 제외한다. 문 자막도 실제로 들리는 범위에서 표시한다.

일시정지는 `AudioListener.pause`와 게임 입력 게이트를 따른다. 실제 이동 없이 발소리를 만들지 않고, 은신·순간 위치 이동은 누적 보폭을 리셋한다. 기존 웅크리기·달리기·물 접촉 소음 반경, 적에게 보고되는 발 접촉과 수면 물결 이벤트 계약을 유지한다. 환경음은 플레이어가 실제로 인식한 긴장에 따라 낮아지고, 보이지 않는 적의 내부 상태를 이용해 위험을 알려주지 않는다.

## 변환과 게인

원본을 미리 mono 24,000 Hz PCM16 WAV로 변환했다. 짧은 접촉은 필요에 따라 앞 침묵을 자르고 용도별 길이를 제한했으며, DC 평균을 제거하고 양 끝에 보통 6ms 페이드를 적용했다. 물방울은 35ms 경계 페이드를 사용했다. 새 공간 환경음은 실제 11.44초·13.54초 원본을 주파수 필터로 구분하고 0.75초 등전력 겹침으로 루프 경계를 연결했다. 결과 길이는 1층·지하 10.69초, 2층 12.79초이며 원본을 단순 반복해 길이를 부풀리지 않았다. 복합 사운드는 manifest의 레이어 게인으로 원본을 더한 뒤 최종 게인을 맞췄다.

파일별 절대 피크는 접촉·충격 약 0.58 이하, 환경음 약 0.42 이하로 맞췄다. RMS 상한은 접촉·충격 0.13, 환경음 0.065다. 이것은 파일별 측정값이며, 모든 상황에서 최종 믹스가 클리핑하지 않는다는 보장은 아니다. 별도 실제 Unity 믹스 검증에서 적 동시 재생과 문·등장·환경음의 검증 구간이 클리핑하지 않는지 확인한다.

`ExternalAudioImport`는 준비된 mono 파일을 다시 정규화하지 않고 PCM·DecompressOnLoad·preload로 가져오며 원본 샘플레이트를 보존한다. 실행 중 다운로드나 계정·네트워크 연결은 필요 없다.

## 유지되는 합성음과 누락 시 대체

| 컴포넌트 | 외부 녹음이 없을 때 / 계속 합성하는 부분 |
| --- | --- |
| `PlayerFeedback` | 목재·돌 발소리는 기존 마른 바닥 합성 접촉, 물은 합성 물 접촉, 스위치·옷자락·회수는 기존 합성 접촉으로 대체한다. 캐비닛 금속 클립이 없으면 그 추가 레이어를 생략한다. 플레이어 피로 호흡은 계속 원본 합성음이다. |
| `InteractionAudio` | 손잡이·걸림쇠는 원본 합성 접촉으로 대체한다. 조용한 슬라이딩 레일은 외부 일반 문 녹음에 덧붙이는 원본 합성음이다. |
| `EnemySoundProfile` / `StalkerFootsteps` | 여섯 정체성의 이동·공격 리소스가 없으면 기존 정체성별 합성 접촉을 만든다. 카탈로그에 없는 Generic도 합성한다. 캐비닛 공격 리소스가 없으면 기존 두 번의 합성 문 떨림을 유지한다. |
| `EncounterRevealAudio` | 외부에 연결된 액자·기구·옷자락·수면·마네킹 접촉이 없으면 원래 해당 큐 합성을 사용한다. `NurseryWhimper`, `CyclopseBreath`, `LanternRise`, `WraithGrowth`, `HwacatJaw`는 계속 원본 합성음이다. |
| `RoomAmbience` / `FloorAtmosphere` | 외부 환경음이 없으면 원래 V1 물방울·배선 드론·종이 바람과 지하의 불규칙 합성 물방울을 사용한다. |

몬스터의 울음·호흡·변신 전체를 외부 성우 녹음으로 교체한 상태는 아니다. 인식·심박·지각 긴장, 기억 공명, 폭죽 등의 기존 원본 음향도 유지된다.

## 검증 근거

이전 사운드 단계의 결과는 `Verification/audio-upgrade/`에 있다. 이번 학교 표현·전체 믹스·기록 개선의 검증은 `Verification/school-polish/`에 따로 보존한다. `school-polish/edit-02.xml`은 실제 Unity EditMode 28/28 통과이며 이전 저장의 optional 지표 호환성과 손상 데이터 보존을 포함한다. 아래 34개 통과 수치는 이전 단계의 고유 테스트 수다.

- `edit-02.xml`: 실제 Unity EditMode 21/21 통과. 보호된 씬·목표 에디터·게임플레이 어셈블리, 공격·스텔스·재시작·물결 규칙, 학교·회랑 저장 계약과 생성 맵 연결성을 검증한다.
- `play-02.xml`: 실제 Unity PlayMode 11/11 통과. 50개 외부 리소스·실제 변형·독립 클립 소유권, 키보드 이동의 나무·타일·물 발 접촉과 F 입력, 공간 환경음, 실제 액자 등장, 움직이는 문·일시정지·거리·마스터 음량·재시도, 여섯 적의 실제 믹스, 학교 중단·복원·손상 거부를 검증한다.
- `play-03.xml`: 실제 Unity PlayMode 6/6 통과. 학교 체크포인트 다섯 테스트와 실제 메뉴의 죽음·다시 시작·타이틀 복귀를 검증한다. 열린 문 사이의 플레이어를 복원하기 전에 문짝과 NavMesh carving 상태를 적용하는 추가 검증을 포함한다.
- 같은 `play-02.xml`의 `ChapterFiveMemoryRouteSurvivesThroughActualInputAndFourStairLegs`: 실제 이동·상호작용 입력으로 다섯 기억과 네 계단 이동을 거쳐 학교 탐색을 완료했다. 이 자동 전략의 통과는 첫 플레이의 난이도·공포감·모든 경로의 균형을 입증하지 않는다.
- `static.json`: 메타데이터·LFS·보호된 씬·스크립트 참조·감사 목록은 오류 없이 통과했다. 선택적 C# syntax/API-name 검사와 렌더된 시각 검토는 NOT RUN으로 남아 있다. Unity의 실제 컴파일·실행 결과는 위 XML에 별도로 있다.

`play-02.xml`과 `play-03.xml`의 중복 재검증을 제외하면 PlayMode 13개, EditMode 21개로 총 34개 고유 테스트가 통과했다. `play-01.xml`의 이전 5/10 결과는 첫 검증 기록으로 보존했다. 수정 후 결과는 `play-02.xml`과 `play-03.xml`이다. 테스트 출력에는 `external-school-materials.json`, `external-school-door.wav/json`, `external-school-portrait.wav/json`, `external-school-ambience.wav/json`, `enemy-movement-acoustics.wav/json`의 해시와 실제 Unity 출력 데이터가 포함된다. 녹음은 AudioRenderer의 기기 출력 전 믹스이며 사람의 청감 판정을 대신하지 않는다.

## 남은 완성도 작업

헤드폰과 스피커에서 실제 탐색·추격·은신·재시도를 플레이하며 발소리가 묻히지 않는지, 문 레일·물 접촉·캐비닛이 해당 동작처럼 들리는지 확인해야 한다. 짧은 환경음의 반복, 지하 물방울의 거리감과 잔향, 근접 적 겹침, 안도 구간과 다음 등장까지의 간격을 청감에 맞춰 조정한다. 필요하면 호흡·울음의 외부 녹음도 같은 출처 기록과 소유권 절차로 보강한다.

그림자복도처럼 느껴지는 완성도에는 사운드뿐 아니라 처음 들어온 플레이어의 길 이해, 공정한 추격·시야·은신 판단, 반복 플레이 변화, 읽기 쉬운 어둠, 몬스터·소품 애니메이션과 실제 성능이 함께 필요하다. 이 문서와 자동 검증은 전체 목표의 완료 판정이나 최종 아트·청감 승인으로 사용하지 않는다.

## 학교 전체 믹스 검증 범위 — 추가 검사

`school-polish/play-01.xml`에서 실제 입력의 전체 학교 경로와 은신·휴식·일시정지·재시도 실믹스 두 검증이 통과했다. 세 층과 15개 자연 이벤트 구간에서 전체 production 음원을 함께 측정했다. 전체 PCM 절대 피크는 0.378642, RMS 0.015160, 클리핑·비유한 샘플은 0개였다.

이 AudioRenderer 검증은 게임 시간 77.95초 동안 DSP 시간 99.75초를 렌더했다. 테스트의 오프라인 DSP가 시뮬레이션보다 약 28% 빨리 진행하므로 믹스 건강도·발생 상황 증거로 사용하며, 실시간 음향과 애니메이션의 정확한 동기화나 기기 출력 청감 인증으로 해석하지 않는다. 원본 WAV·JSON과 전송 해시는 `captures-01/`, 추가 계측 한계는 `live-mix-audit.json`에 보존한다.

최종 학교 표현·기록 개선까지 포함한 최신 고유 검증은 EditMode 30개·PlayMode 23개, 합계 53개 모두 Passed다. 범위별 원본 실행과 이전 실패 이력은 `Verification/school-polish/summary.json`과 `SCHOOL_POLISH.md`에 기록했다. 새 `Builds/PolishReview`는 1,968개 빌드 입력 일치와 실제 Windows 시작 검증을 통과했다.
