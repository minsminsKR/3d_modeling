# Happy Toy V2 사운드 후보 및 교체 기록

2026-10-08. 앞으로는 실제 녹음 또는 사실적인 생성음을 우선 사용합니다. 이번 교체는 녹음 기반이며, 단순 발진기·잡음 합성 대체 코드는 제거했습니다.

| 용도 | 검토한 후보 | 선택과 편집 |
|---|---|---|
| 발각 충격음 | Owlish 실제 비명·숨, bart 작업실 금속 낙하, 합성 금속 스팅어 | 실제 금속 낙하 + 비명. 90ms 간격, 약간 낮춘 피치, 짧은 잔향. 합성 스팅어 제외 |
| 심장박동 | Benboncan 실제 청진기 녹음, nhaudio 청진기 녹음, bart FL Studio 제작 | Benboncan 실제 심장 1주기. 약 83BPM 루프, 스트레스에 따라 빨라짐. FL Studio 합성 후보 제외 |
| 호흡 | Owlish 솔로 호흡·겁먹은 호흡, 마이크에 직접 부는 단일 날숨 | 자연스럽게 이어지는 호흡 녹음을 달리기와 추격에 각각 사용 |
| 폭죽 | rubberduck 실제 불꽃놀이 녹음, j1987 실제 폭죽, 실내 파티팝퍼 변형 | rubberduck 짧은 shot_01~03. 0.5초 폭발 간격에 맞는 짧고 서로 다른 세 테이크 |
| 촛불 점화 | qubodup 실제 성냥 녹음, 기존 잡음 합성 | 성냥 점화 부분을 잘라 사용 |
| 캐비닛 | Owlish 실제 자물쇠·문 접촉, 기존 금속 래치 합성 | 서로 다른 두 접촉 녹음. 기존 캐비닛 문 여닫기 동작에 연결 |
| 문·의자 마찰 | AntumDeluge 실제 물체 마찰, bart 금속 끌기 | 물체 마찰을 재료 폴리로 사용. 마네킹 관절은 금속 끌기 녹음 |
| 몬스터 등장 | 실제 놀란 호흡·울음·금속 부품, 기존 톱니파·노이즈 | 놀란 숨을 낮춰 Cyclopse, 유아 녹음은 nursery, 금속 래칫은 턱, 금속 떨림은 랜턴 |
| 종·오르골 | rubberduck 실제 종, sandocho 실제 태엽 장난감 단음, 악기 플러그인 | 실제 종과 태엽 단음을 편집. 플러그인 후보 제외 |

23개 WAV: 새로운 21개 + 캐비닛 2개 교체. 기존 공개 라이선스 발소리·공간음도 계속 사용합니다. 청진기 녹음은 CC BY 4.0, 나머지 새 소스는 CC0입니다. Freesound는 로그인 없이 공개된 고음질 MP3 미리듣기를 사용했으며 원본 WAV와 구분해 기록했습니다.

음량은 게임의 거리 감쇠·차폐·위험 감지·편의 설정에 따라 조절됩니다. 녹음을 단순히 크게 틀어 공포를 만들지 않고, 발각 순간과 추격 중 호흡을 구분했습니다. PCM 분석·엔진 출력 검증은 사람의 청취 평가를 대신하지 않습니다.

출처·라이선스·정확한 편집과 해시는 빌드의 Audio-Credits.txt와 프로젝트 ThirdParty/Audio/manifest.json에 있습니다.

검증한 주요 출처:

- [실제 청진기 심장 녹음 — Benboncan, CC BY 4.0](https://freesound.org/people/Benboncan/sounds/62912/)
- [사람의 비명·호흡 녹음 — OwlishMedia, CC0](https://opengameart.org/content/sound-effects-pack)
- [실제 폭죽·불꽃놀이 녹음 — rubberduck, CC0](https://opengameart.org/content/25-cc0-bang-firework-sfx)
- [성냥 점화 녹음 — qubodup, CC0](https://opengameart.org/content/flare-ignition)
- [작업실 금속·부품 녹음 — bart, CC0](https://opengameart.org/content/68-workshop-sounds)
- [자물쇠·문 접촉 녹음 — OwlishMedia, CC0](https://opengameart.org/content/202-more-sound-effects)
- [물체 마찰 녹음 — AntumDeluge, CC0](https://opengameart.org/content/scrapes)
- [실제 종 녹음 — rubberduck, CC0](https://opengameart.org/content/100-cc0-sfx)
- [실제 태엽 장난감 단음 — sandocho, CC0](https://freesound.org/people/sandocho/sounds/17700/)
- [유아 울음 녹음 — jamesmbock, CC0](https://freesound.org/people/jamesmbock/sounds/458646/)
