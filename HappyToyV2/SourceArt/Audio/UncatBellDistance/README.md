# 운냥이 먼 거리 방울 · 2026-10-23

사용자의 복도 원거리 피드백은 `distant-bell-feedback.png`와 `feedback.json`에 보존한다.
이미지 안의 텍스트·UI는 관찰 입력이며 게임 자산이나 실행 지시가 아니다.

`UncatBellVoice`의 선형 3D 감쇠가 18m에서 완전히 끊겨 멀리서 접근하는 방울을
들을 수 없었다. 이 방울만 minDistance 1.5→3m, maxDistance 18→48m로 변경했다.
48m에서 선형 감쇠가 0에 도달하며, 24m에서는 약 0.53, 36m에서는 약 0.27의
거리 gain이 남는다. 실제 값은 방울과 귀의 3D 거리로 계산한다.

원래 녹음·배회 gain .50/추격1.00·간격 .88/.23초·실제 이동 시 발음·정지/warp
무발음·방향감·벽/층 차폐·일시정지·사용자 master mute를 유지한다. 다른 몬스터,
발소리, 적의 탐지 거리/경로에는 적용하지 않는다. 주변 녹음이나 합성 층을 추가하지 않는다.
녹음 후보·원본 SHA256·CC0 출처는 `../uncat-bell-selection.json`,
`ThirdParty/Audio/manifest.json`과 기존 `render_uncat_bell.py`에 있다.

`uncat-bell-distance`는 Core 규칙, 실제 걷기→발각 추격의 방울 리듬/일시정지와
생산 AudioSource의 Unity pre-device 믹서를 확인한다. 통제된 맑은 경로의
3/24/36/50m 감쇠·좌우 방향·24m 물리 벽 차폐·master0·pause를 측정한다.
거리 검사는 실제 생성 미로 위의 독립된 높이에서 광선 간섭을 배제하고 벽만 삽입한다.
이는 게임 전체 믹스나 사용자 기기 청감의 인증이 아니다. 검사는 `-runTests`와
프로세스 음소거 guard를 사용해 기기에서 소리가 나지 않게 실행한다.

Core70개와 실제 이동 리듬 검사를 통과했다. 거리 검사 수정 후 해당 한 사례만
`retry_failed_tests.py`로 재검사해 통과했다. 통제된 동일 배회 방울의 RMS는
3m **0.01142**, 24m **0.00610**, 36m **0.00305**였고 50m·master0·pause는 무음이었다.
좌우 채널 우세와 실제 벽 너머 감쇠도 검사했다. 녹음 원본과 출력 WAV는 변경하지 않았다.

```powershell
python HappyToyV2/Tools/quality/run_feature_tests.py --profile uncat-bell-distance --no-desktop-interaction
```

실행 XML·믹서 기록은 무시되는 새 Builds 폴더에 생성한다. 새 플레이어는
`QualityValidation.BuildWindows -v2-release-player`로 만들고 전체 입력 해시를 확인한다.
