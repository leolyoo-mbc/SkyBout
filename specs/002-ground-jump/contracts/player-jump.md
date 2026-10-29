# Contract: 플레이어 점프 입력과 설정

## 범위

게임 사용자에게 노출되는 키보드 조작과 개발자가 조정하는 Inspector 설정의 계약이다. 외부 서비스 API, 네트워크 메시지 또는 저장 형식은 추가하지 않는다.

## 입력 자산

- 자산: Assets/Player_Actions.inputactions.
- 기존 Player/Move 이름·ID·바인딩과 Player 맵 ID를 보존한다.
- 새 액션: Player/Jump, Button, expectedControlType=Button.
- 새 바인딩: <Keyboard>/space, Keyboard&Mouse 그룹. 홀드/반복 인터랙션은 추가하지 않는다.
- 기존 PlayerInput의 SendMessages 및 기본 Player 맵을 유지한다.
- 수신부: PlayerMovement.OnJump(InputValue). 액션 객체 자체를 저장하는 대신 콜백 안에서 필요한 눌림 값을 읽는다.

## 사용자 관찰 동작

| 입력/상태 | 결과 |
| --- | --- |
| 지상에서 새 Space 누름 | 다음 유효 물리 틱에서 1회 점프 |
| Space 해제 | 높이 변경 없음; 다음 새 누름을 준비 |
| 공중 새 Space 누름 | 추가 점프/글라이더/착지 예약 없음 |
| 착지 후에도 Space 홀드 | 재점프 없음 |
| 착지 후 해제 후 새 누름 | 새 점프 1회 |
| 공중 WASD 변경/해제 | 지상과 동일한 설정으로 방향 변경/감속 |
| 초점 복귀 시 Space 홀드 | 자동 점프 없음; 해제 후 새 누름 필요 |

## Inspector 및 씬 계약

- 기존 PlayerMovement 컴포넌트와 GroundCheckOffset 자식 참조를 유지한다.
- jumpHeight는 유한한 양수이며 초기 제안 2m, 적용 범위는 장애물 없는 기존 평지다.
- 기존 maxSpeed/acceleration/deceleration/turnAcceleration 및 Ground 마스크를 보존한다.
- 기존 airControl=0.3 별도 경로는 제거하고 공중 조작을 공통 설정으로 통일한다.
- 필수 Rigidbody/PlayerInput/발 참조/마스크/입력 액션/중력·높이 설정이 잘못되면 명확한 오류를 기록하고 잘못된 점프를 실행하지 않는다.
- 씬 또는 입력 자산을 변경할 때 GUID와 기존 오브젝트 참조를 보존한다.

## 물리 갱신 계약

1. 현재 접지와 초점 상태를 확인한다.
2. pendingJump를 읽고 무조건 비운다. 현재 지지가 없으면 실행하지 않는다.
3. 유효 요청은 수직 초기 속도를 1회 설정하고 지상 판정을 해제한다.
4. 수평 이동은 지상/공중 공통 계산으로 갱신하며 Y를 덮어쓰지 않는다.
5. 중력 적분과 충돌 해결은 Rigidbody/PhysX가 수행한다.

시작 위치 Y나 이전 이륙 높이와 비교해 강제로 착지시키지 않는다. 비활성화 또는 초점 상실 시 요청을 삭제한다.

## 입력 경계 검증 추적

| 경계 | 시험 및 기대 결과 |
| --- | --- |
| 상승·정점·하강·착지 직전 재입력, 지속 홀드 | JumpTests.AirAndHeldInputNeverAutoJump: 각 조건 5회, 추가/예약 점프 없음 |
| 한 업데이트의 짧은 누름·해제, 여러 물리 틱 | ShortPressIsConsumedOnceAndLosingSupportDiscardsRequest: 1회만 소비 |
| 접수 후 발밑 지지 상실 | 동일 시험: 다음 틱에 폐기하고 추락 유지 |
| 초점·비활성화/재활성화 | FocusAndDisableClearRequestsAndRequireRelease: 요청 제거, Y 보존, 해제 후 새 누름만 허용 |

Input System 1.20.1의 SendMessages는 Button 해제 콜백을 생략하므로 onAfterUpdate에서 Jump 컨트롤이 해제됐는지 확인한다. 해제 확인은 유효하게 접수한 짧은 누름 요청을 취소하지 않는다.

## 검증 계약

실제 입력 자산·가상 키보드를 거쳐 누름/해제/초점 상태를 검증한다. 반복 횟수와 허용 오차는 [spec.md](../spec.md)의 SC-001~005를 따른다. 테스트 관측을 위해 내부 상태를 읽을 수 있으나 테스트 전용 공개 게임 API는 요구하지 않는다.
