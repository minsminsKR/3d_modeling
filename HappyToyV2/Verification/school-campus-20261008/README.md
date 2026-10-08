# 폐교 재구성 · 안광 · 상호작용 검증

Unity 6000.6.0f1 일반 Windows Player `Builds/SchoolCampus-20261008-Final`로 확인했다.
새 폐교는 지도 버전 2이며 기존 버전 1 저장은 원래 학교에서 재개한다.

- EditMode 배터리·조명 규칙 11개 통과.
- 관련 PlayMode 고유 검사 16개 통과. 각 수정의 최종 통과 결과를 합친 집계이며,
  하나의 전체 테스트 모음을 모두 실행한 결과로 표시하지 않는다.
- 실제 기억 순서·액자/베이비 등장, 제단에서 학교 전환과 학교 저장 슬롯 보존,
  두 계단의 추격·정지·변경된 층에서 저장/재개, 신·구 지도 저장 복원을 확인했다.
- 복사된 피아노의 2.2m 폭과 건반, 벤치의 접지/0.4m 좌석, 의자 등판과 기울어진
  악보·배치된 실제 정적 메시를 검사했다.
- 최종 일반 Player: 18개 방과 28개 새 문 양쪽의 실제 발판·선 자세·캡슐 통과,
  완전한 방 진입/귀환 경로·기억 접근을 통과했다. 원래 서쪽 문 3개도 실제로 열었다.
- 최종 일반 Player: 여섯 얼굴의 30개 렌더 상태를 확인했다. 15m 소등 상태에서도
  각 눈의 붉은 픽셀이 검출됐고, 뒤쪽/불투명 판 뒤에서는 눈이 보이지 않았다.
- 최종 일반 Player: 가면과 베이비의 계단 왕복 4회, 지지 바닥 없는 이동 표본 0개.
- 실제 빌드 입력 해시·소스 인계 누락 검사를 통과했다. 일반 Player의 developmentBuild=false.

`school-campus-report.json`, `threat-visuals.json`, `school-stairs.json`은 최종 Player의
원본 보고서다. 선택된 화면만 이 폴더에 보관하며, 전체 화면과 각 테스트의 초기 실패/
수정 결과는 작업 출력 `game/verification/school-rebuild-20261008`에 있다.

이 검사는 통제된 경로·입력·배우·카메라/렌더 검증이다. 전체 자연 플레이의 생존 균형,
기기별 GPU 성능, 사람이 느끼는 공포와 청감 품질까지 인증하지 않는다.
전체 native 화면은 같은 Player를 각 인자로 실행하면 재생성한다.

```powershell
HappyToyV2.exe -v2-school-campus-output <학교 출력 폴더>
HappyToyV2.exe -v2-threat-visual-output <눈빛 출력 폴더>
HappyToyV2.exe -v2-school-stair-output <계단 출력 폴더>
```
