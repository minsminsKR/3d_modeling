# 회랑 베이비 실제 울음 — 2026-10-10

선택 후보는 Alex_hears_things의 `Baby crying.wav`, MBPL의 `Crying baby 1`,
기존 jamesmbock의 `babycry.mp3`다. 각 공개 게시 페이지에서 CC0를 확인했다.
Alex는 Zoom H4n Pro로 녹음했다고 명시하고, MBPL은 실제 세 호흡을 기록한 뒤
관련 없는 소음을 Audacity에서 줄였다고 설명한다. jamesmbock은 자신의 아들을
iPhone으로 녹음했다고 명시하지만 원 게시 파일이 16 kHz/32 kbps 압축 음원이다.

Alex의 44초 녹음을 선택해 6–22초의 긴 호흡 구간을 보존했다. 짧은 3.224초
MBPL 구간은 제자리에서 오래 듣는 적의 루프로 반복하기에 짧아 비교 자료로만
보존했다. 기존 jamesmbock 녹음은 기존 공격/등장 단발음에 계속 사용한다.
선택 음원과 MBPL 비교 음원의 공개 HQ MP3 미리듣기·해시를 sources/에 보존한다.
원본 무손실 WAV를 다운로드했다고 주장하지 않는다.

선택 구간을 모노 48 kHz로 변환하고 85 Hz 하이패스/7.6 kHz 로우패스를 적용했다.
0.45초 랩 크로스페이드로 루프 이음을 줄였고, PCM peak를 0.50으로 맞췄다.
15.55초 동안 원래 목소리 높이와 숨 쉬는 간격을 유지한다. 절차적 잡음·발진음·
다른 게임의 소리는 섞지 않았다. 이것은 실제 아기의 녹음을 편집한 것이며,
별도로 아이에게 울음을 유발하거나 새로운 녹음을 요청한 것은 아니다.

- [Alex_hears_things — Baby crying.wav, CC0](https://freesound.org/people/Alex_hears_things/sounds/636845/)
- [MBPL — Crying baby 1, CC0](https://freesound.org/people/MBPL/sounds/667199/)
- [jamesmbock — babycry.mp3, CC0](https://freesound.org/people/jamesmbock/sounds/458646/)

재현: CPython 3.11 + `SourceArt/Audio/requirements-corridor.txt` 설치 후
`python SourceArt/Audio/render_corridor_baby.py`. 선택·보존된 음원의 원 해시와
마지막 WAV 해시는 `sources.json`, `prepared-cues.json`, 전체 `manifest.json`에 있다.
반복·위치·폐문 차폐·일시정지·음소거·종료 정리는 실제 Unity 검증의 별도 범위다.
PCM 수치와 이 후보 기록은 사람의 청취 품질이나 공포 반응을 인증하지 않는다.
