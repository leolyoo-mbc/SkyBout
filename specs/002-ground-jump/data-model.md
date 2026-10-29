# Data Model: 점프 상태와 설정

영속 데이터나 네트워크 모델은 추가하지 않는다. 모두 기존 PlayerMovement의 런타임 상태 또는 직렬화 설정이다.

## JumpSettings

| 필드 | 타입/단위 | 규칙 |
| --- | --- | --- |
| jumpHeight | float, m | 유한한 양수. 초기 제안 2, 검증값 2/4. 이륙 시 지면 대비 발의 수직 상승량 |
| 기존 수평 설정 | float | maxSpeed/acceleration/deceleration/turnAcceleration 유지. 지상·공중 공통 |
| groundLayer | LayerMask | Ground 포함, 자기 레이어 제외 |
| groundCheckTransform | Transform | 기존 발 위치 자식 참조 유지 |
| 지지 판정 상수 | 내부 값 | 위쪽 법선 기준, 발 부근 거리와 작은 접촉 허용 오차. 일반 사용자 튜닝 항목으로 확대하지 않음 |

중력은 Physics.gravity에서 읽고 별도 중력을 중복 적분하지 않는다. Rigidbody는 useGravity=true, isKinematic=false, linearDamping=0 및 Y 이동 허용이어야 한다.

## JumpRuntime

| 필드/개념 | 의미/수명 |
| --- | --- |
| body/playerInput | 기존 컴포넌트 참조 |
| grounded | 현재 유효한 발밑 지지 접촉. 단순 높이나 근접 여부가 아님 |
| motionState | Grounded, Rising, Falling. grounded와 수직 속도로 도출해 중복 상태 불일치 방지 |
| jumpHeld | 눌림 전이를 구분하는 현재 입력 상태 |
| jumpReadyAfterRelease | 활성화/초점 복귀 후 해제 확인 여부 |
| pendingJump | 지상에서 유효하게 받은 누름 1회. 다음 FixedUpdate에서 무조건 소진 |
| focused/waitingForNeutral/input | 기존 이동 초점·중립·Vector2 상태 유지 |
| contactBuffer | 고정 크기 재사용 ContactPoint 배열 |

Jump 해제 게이트는 Move 중립 게이트와 분리한다. 초점 복귀 후 이동 키를 해제하지 않아도 수평 이동은 기존 게이트에 의해 중단되며, 점프 역시 새 Space 누름 없이는 실행되지 않는다.

## GroundSupport

Rigidbody와 Ground Collider의 관계로, 마스크·법선·발 부근 접점·분리 거리 및 현재 발아래 표면 확인을 모두 만족할 때만 유효하다. Trigger, 옆면 법선, 멀어진 접점, 상승 중 잔류 접촉은 제외한다. 접촉은 매 물리 틱 및 입력 수신 시 재검증하고 오래된 grounded만으로 누름을 접수하지 않는다.

## State Transitions

| 시작 | 조건 | 결과 |
| --- | --- | --- |
| 초기/비활성 | 활성화 | pending 제거, 해제 대기, 실제 지지 준비 전 Falling |
| Falling | 유효한 발밑 접촉 및 상승 아님 | Grounded |
| Grounded | 초점·해제 게이트 통과한 새 Space 누름 | pendingJump 접수 |
| Grounded+pending | 다음 FixedUpdate에서도 지지 유효 | Y 초기 속도 설정, XZ 보존, Rising, pending 제거 |
| pending 보유 | 지지 상실/초점 상실/비활성 | 요청 폐기, 점프 없음 |
| Rising | Y 속도 <= 0 | Falling |
| Rising/Falling | Space 누름 | 입력 상태만 갱신, pending 생성 없음 |
| Grounded | 발밑 지지 상실 | Falling, 코요테 타임 없음 |
| 임의 | Space 해제 | 다음 새 누름 허용; 자동 점프 없음 |
| 임의 | 초점 상실 | pending 제거, 수평 정지, 점프 해제 게이트 잠금; 물리 Y는 유지 |
| 임의 | 씬 재로드 | 기존 안전한 위치에서 새 런타임 상태 생성 |

착지 직전 누름은 지지 없는 시점에서 폐기되므로 착지로 부활하지 않는다. 홀드와 새 누름은 구별하며 지속 홀드로 상태를 전이하지 않는다.

## Validation Mapping

- FR-001~003/009 → JumpSettings와 유효 누름 → 정점 높이 시험.
- FR-004/010 → GroundSupport와 Falling→Grounded → 중앙/가장자리/옆면 시험.
- FR-005~006/011 → 입력 전이·요청 수명·초점 초기화 → 자동 점프 0회 시험.
- FR-007~008 → 기존 수평 설정 및 카메라 참조 → 8방향 이동·가시성 회귀.
