# 촛불 위험 PlayMode 검사 staging

테스트 파일과 `.meta`만 작성했다. 프로젝트 원본을 직접 수정하거나 Unity/player를 실행하지 않았다. `CloudPlayModeTests` partial의 기존 실제 scene/physics/NavMesh·`RuntimeAccess`·실제 mouse/E helper를 재사용한다. Runtime은 root 소유다.

## 검사와 실제 실행 상태

초기 6개는 `proposed/Assets/Tests/PlayMode/CloudCandleDangerTests.cs`, `manifest.json`, `test-filter.txt`다. 새 GUID는 `aa64835ba3a64105b01c1e2dee8bbcea`이며 파일 baseline은 작성 당시 ABSENT였다. Root가 설치해 serial Unity `danger03`에서 **6/6**, 30.3435초·CLI exit0을 확인했다. 기존 runtime regression02도 별도 **3/3**, 23.4575초다. Root 실행 기록의 정확한 XML 경로와 최종 빌드 근거는 root가 관리한다.

1. `CandleProximityDimsAllLitMarkersBlackoutsAndRequiresActualManualRelight`: 실제 E로 점화, mode-owned 활성 Watchman을 실제 NavMesh에 배치하고 14.5m 안전→8m 경고/감광→1.7m 강제 소등을 확인한다. 모든 marker의 Lit/light/flame이 꺼지고 위험 중 TryIgnite/event 증가가 차단되며, 안전 복귀도 자동 점화하지 않고 실제 E가 하나만 다시 켠다.
2. `CandleActualFarSightBlackoutsBeforeEnemyUpdateAndPhysicalCoverPreventsIt`: 실제 회랑 문을 원래 API로 열고 실제 LOS/NavMesh pair를 찾는다. 12m 밖·Watchman의 실제 SightRange 안에서 Patrol/cone/eye ray가 실제 가림막에 막히면 안전하다. 가림막을 제거한 같은 프레임, brain Update/Chase 이전의 RefreshDanger가 blackout을 수행하고 다음 실제 Update가 Chase를 시작한다. 별도 visibility/anger flag를 주입하지 않는다.
3. `CandleDangerRejectsInactiveUnreadyWrongModeAndRealOtherFloorActors`: 실제 활성/ready controller를 바탕으로 brain 비활성·agent 비ready·다른 mode의 원본 authored actor·실제 상층 home floor를 각각 배제한다. 다른 층 actor는 실제 protected NavMesh에 있고 warning 거리 안이다.
4. `CandleDormantSchoolMaskAndUnreleasedMannequinStayHarmlessWithReadyNavigation`: 실제 Chapter 소유 mask/mannequin의 정상 Dormant/미Released 상태를 보존하고 같은 원래 층·2m 안에 둔다. NavMesh 준비만 갖춰도 harmless intro가 blackout을 유발하지 않는지 확인한다. IntroCompleted/Released를 강제 완료하지 않는다.
5. `CandleDangerPauseFreezesThreatStateAndReducedMotionKeepsSteadyDimming`: comfort mode의 실제 dimmed light/position/rotation/scale을 샘플링해 steady임을 확인한다. pause 중 실제 actor를 가까이 warp하고 RefreshDanger를 불러도 위험·점등·빛·flame pose가 얼어 있으며 resume 뒤 평가한다. 감광으로 작아진 flame scale을 원래 full scale과 같다고 주장하지 않는다.
6. `CandleBlackoutPersistsExtinguishedThroughBothModeCheckpointRoundTrips`: 실제 소등 후 안전하게 두 모드 DTO를 capture→기존 codec copy→실제 scene/session restart→apply 한다. extinguished marker·ignition count0·재생 없음·첫 안전 프레임의 소등을 확인하고 실제 E가 하나만 재점화한다.

7번째 파일은 `flicker-followup/proposed/Assets/Tests/PlayMode/CloudCandleDangerFlickerTests.cs`, 그 폴더의 별도 `manifest.json`·`test-filter.txt`다. 초기 6개 파일은 동결했다. 새 GUID는 `08a7fd8bb3634a1bb4c4787483b740f7`이며 첫 파일의 private partial helpers에 의존한다.

7. `CandleActualNormalMotionWarningGrowsFlickerDepthAndCompletedPulseCount`: normal-motion에서 실제 candle `LocalLight.intensity`를 자연 frame/TimeScale1로 각3 game-seconds, 약10m/3m 경고 위치에서 관찰한다. 30개 이상의 실제 샘플과 실제 low→high→low complete pulses를 요구하고 가까운 경고가 더 깊고 더 많은 pulse를 보이는지 검사한다. constant 기존 ~1Hz만으로는 3초의 complete pulse6개 한계를 통과할 수 없다. 이론적 brightness 함수나 예측 파형을 호출하지 않는다. 실제 cadence/원본 샘플을 한 번만 `candle-danger-live-flicker.json`으로 전송하며 중복 artifact 이름을 쓰지 않는다. Root 실제 `flicker04.xml` 1/1 PASS, 13.4816초, CLI exit0. XML 원본 artifact SHA를 검증해 추출했다. 약9.947m에서348샘플/깊이0.100779/완료pulse1개, 3m에서356샘플/깊이0.465775/완료pulse9개를 관측했다. 새 Windows 빌드 및2335개 현재 입력·165개 필수 그래픽 입력/보호 장면 검증도 통과했다. 최종 근거는 ../FINAL-REPORT.json과 ../build-input-proof.json이다.

## 설정과 한계

- 실제 소유 StalkerBrain은 관찰 중 enabled·NavMesh-ready이며 patrol/chase speed0, agent updateRotation=false이다. Proximity fixture의 플레이어는 cone 뒤에 있어 실제 시야로 감광 검사를 교란하지 않는다. 실제 LOS case만 brain을 실제 플레이어 쪽으로 돌린다.
- 여러 lit marker의 초기 상태는 공개 Restore(true)로 준비하고 실제 E 점화 count는 별도로 확인한다. 실제 global blackout·안전 복귀·재점화 동작을 검사한다.
- Threshold 위치는 branch representative14.5/8/1.7m이고 root가 실제12m warning/2m close constants와 `<=` 조건을 별도 검토했다. 12.000/2.000의 수학적 경계 전체를 독립 PlayMode assertion으로 측정했다고 주장하지 않는다. 추가 boundary patch는 만들지 않았다.
- Public API는 LightExplorationRun의 Danger/Blackout/RefreshDanger와 WaymarkCandle의 Danger/IgnitionBlocked/ApplyDanger다. 검사들은 ApplyDanger로 예상 결과를 그대로 주입하지 않고 실제 actor/physics를 통해 평가한다. Unsafe ignition 차단은 blackout으로 검사하며 warning 상태의 ignite 여부를 추가 contract로 만들지 않는다.
- 제어된 setup의 이동/warp·속도0·임시 물리 가림막을 숨기지 않는다. 원래 source scene이나 모델 자산을 수정하지 않으며 전체 생존 route·수동 공포감·기기 청감·GPU/FPS나 strobe comfort 인증으로 확대하지 않는다.
