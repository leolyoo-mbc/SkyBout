# Research: 지상 점프와 착지

## 조사 범위와 근거

저장소의 PlayerMovement.cs, TopDownCameraFollow.cs, Player_Actions.inputactions, SampleScene.unity, MovementAndCameraTests.cs, asmdef 두 개, manifest.json, ProjectVersion.txt와 Physics/Time/Input 설정을 직접 조사했다. 실행 검증은 구현 단계에서 수행한다.

## 1. 기존 프로젝트 통합

- **Decision**: Unity 6000.3.26f1 프로젝트와 기존 Rigidbody 이동을 확장한다. 패키지는 변경하지 않는다.
- **Rationale**: 플레이어는 질량 80, 중력 사용, 수직 이동 허용, linearDamping 0이며 기존 수평 속도 변경은 Y를 보존한다. 씬 Ground는 Ground 레이어의 고정 지형이며 윗면은 Y=0, 플레이어 중심은 Y=1이다.
- **Alternatives considered**: 새 프로젝트, CharacterController 전환, Transform 기반 점프는 기존 동작과 충돌 처리를 불필요하게 바꾸므로 제외.

## 2. 단발 입력과 물리 틱 연결

- **Decision**: 기존 Player 맵에 Button/Jump와 Keyboard Space를 추가한다. PlayerInput SendMessages 방식의 OnJump에서 누름/해제를 구분한다. 지상·초점·중립 확인을 통과한 새 누름만 1회 요청으로 보관하고 바로 다음 FixedUpdate에서 재검사 후 소비 또는 폐기한다.
- **Rationale**: Update 입력과 FixedUpdate의 실행 빈도가 다르므로 짧은 누름을 잃지 않되, 공중 입력을 착지까지 저장하면 안 된다. 이벤트 수신 시와 소비 시 모두 현재 지지 상태를 검사한다. 오래된 grounded 값만으로 입력을 허용하지 않는다.
- **Alternatives considered**: 매 물리 틱 isPressed 검사(연속 점프), 무조건 요청 저장(착지 예약), 직접 Keyboard.current 검사(기존 자산 경로 우회)를 제외.
- Input System 1.20.1 PlayerInput 소스에서 SendMessages는 Button의 canceled 콜백을 전달하지 않음을 확인했다. 따라서 onAfterUpdate에서 Jump 컨트롤의 해제를 매번 확인해 jumpHeld를 초기화한다. OnJump의 해제 분기는 직접 전달되는 경우도 안전하게 처리한다.
- 초점 상실·비활성화는 요청을 지운다. 초기 활성화/초점 복귀 시 Jump가 해제된 것을 확인하기 전 누름을 허용하지 않는다. 기존 Move 중립 게이트와 Jump 해제 게이트는 독립적으로 관리해 WASD를 누른 채 새 점프가 가능하게 한다. Jump 해제는 이미 유효하게 받은 단발 요청을 취소하지 않는다.

## 3. 지지 접촉과 착지

- **Decision**: 기존 CheckSphere 단독 판정을 발밑 지지 접촉 검사로 교체한다. OnCollisionEnter/Stay의 Collision.GetContacts(contactBuffer) 재사용 ContactPoint 버퍼에서 Ground 마스크, 위쪽 법선(예: dot(up) >= 0.7), 발 부근 접점, 접촉 오프셋 수준의 분리 거리만 허용한다. 짧은 아래 방향 쿼리로 현재 발아래 표면도 확인하여 직전 물리 스텝의 오래된 접촉으로 가장자리 이탈을 허용하지 않는다.
- **Rationale**: CheckSphere는 법선과 실제 지지 여부를 알 수 없어 옆면이나 착지 직전 공중을 지상으로 잘못 판단할 수 있다. 상승 속도가 유의미하게 양수이거나 이륙 요청을 소비한 틱이면 지상으로 판단하지 않는다. 근처 표면만으로 접지를 부여하지 않는다.
- **Alternatives considered**: 출발 Y와 비교(허공 착지), 넓은 OverlapSphere만 사용(옆면 오인), OnCollisionStay 불리언만 저장하고 형상을 재확인하지 않는 방식(접촉 소멸·가장자리 이탈 오인)을 제외.
- 구현 조사에서 Physics.GetContacts/Rigidbody.GetContacts는 Unity 3D API에 없음을 Editor 리플렉션으로 확인했다. Collision.GetContacts로 유효 지지 Collider 집합을 갱신하고 OnCollisionExit에서 제거한다. 수면 중 Stay 콜백이 없어도 집합은 유지하되 현재 발아래 Raycast를 매번 확인한다. OnDisable에서 집합을 비운다.
- 초기 프레임은 물리 지지 정보가 준비되기 전까지 공중으로 취급한다. 정상적으로 정착한 뒤 입력 검증을 시작한다. 버퍼가 찬 경우 지지 검사에 실패할 수 있으므로 단순 씬에 충분한 용량을 정하고 포화 시 검증 실패를 드러낸다.
- 지형·몸체 크기는 기존 단위 스케일 캡슐 기준이다. 경사·이동 발판 확장은 하지 않는다.

## 4. 목표 높이와 중력

- **Decision**: 목표 높이 초기 튜닝값은 2m로 제안한다. 초기 수직 속도는 sqrt(2 * abs(Physics.gravity.y) * jumpHeight)를 기준으로 산정하고 0.02초 물리 적분 오차를 PlayMode 측정으로 확인한다. 2m와 4m에서 ±5% 목표를 시험한다.
- **Rationale**: 질량과 수평 속도에 독립적이며 Rigidbody의 중력·충돌을 그대로 쓴다. 필요하면 고정 시간 간격을 고려한 초기 속도 보정을 추가하고 동일 표본을 재검증한다. 정점에서 좌표를 강제로 옮기거나 Y를 고정하지 않는다.
- **Alternatives considered**: 고정 AddForce 힘(질량 의존), 시간 기반 Transform 보간(물리와 충돌), 홀드에 따른 지속 상승(명세 위반)을 제외.
- 유효 설정은 유한한 양의 높이, 음의 수직 중력, 동적·중력 사용 Rigidbody, damping 0, 수직 이동 허용이다. 비정상 설정은 오류를 알리고 기능을 비활성화한다. 임의의 극단적인 튜닝값까지 ±5%를 보장했다고 주장하지 않는다.

## 5. 공중 수평 조작의 명세 차이

- **Decision**: 공중에서도 지상과 같은 maxSpeed/acceleration/deceleration/turnAcceleration 계산을 적용한다. airControl 필드와 씬의 기존 0.3 직렬화 값은 정리하고 해당 테스트 기대값을 명세에 맞게 갱신한다.
- **Rationale**: 현재 구현은 공중 가속을 0.3배로 줄이고 입력 해제 시 감속하지 않는다. 명세 FR-007은 지상 설정을 유지한 공중 제어를 요구한다. 기존 동작을 그대로 두면 명세 불일치다.
- **Alternatives considered**: airControl=1만 설정하면 무입력 감속이 여전히 빠지므로 불충분하다. 별도 공중 튜닝은 범위 밖이다.

## 6. 카메라와 충돌 설정

- **Decision**: 현재 LateUpdate 캐릭터 추적을 유지하고 상승/하강 전 구간에서 캐릭터와 표면의 가시성을 검증한다. 기본값은 바꾸지 않으며 실패 시 최소한의 거리/시야 설정만 조정한다.
- **Rationale**: 현재 카메라는 Y를 포함한 target.position을 추적하며 커서를 사용하지 않는다. 검증 없이 새 연출을 추가할 이유가 없다.
- **Alternatives considered**: 지면 고정 카메라나 점프 전용 카메라 컴포넌트는 제외.
- 현재 씬 충돌 감지는 Discrete다. 2m/4m와 두꺼운 고정 지형에서 먼저 검증한다. 고속 낙하 관통이 재현되면 해당 플레이어에 Continuous Speculative를 평가하고 회귀 검증하며 전역 물리 설정은 변경하지 않는다.

## 7. 테스트 전략

- **Decision**: 기존 PlayMode 어셈블리에 JumpTests를 추가하고 실제 입력 자산을 복제하여 가상 키보드로 구동한다. 결정적 수동 물리 스텝 시험과 실제 SampleScene 자동 생명주기 시험을 구분한다.
- **Rationale**: 기존 테스트가 이 구조를 사용한다. GetContacts 기반 판정은 실제 Physics.Simulate 이후의 접촉 데이터를 검증하며 에디터 모드 콜백 발생을 전제하지 않는다. 입력과 물리 스케줄을 수동 호출한 시험만으로 실제 씬 통합 성공을 단정하지 않는다.
- **Alternatives considered**: 입력을 우회한 속도 직접 주입만으로 완료 판정, 수동 플레이만으로 높이 오차 판정은 제외.
- 기존 Press 헬퍼는 한 키 입력 시 항상 Move 벡터를 기대하므로 Space 시험에 그대로 재사용할 수 없다. 새 헬퍼는 이동/점프 입력을 구분한다.
- 기존 GroundedStateIsRefreshedInThePhysicsStepAndGravityIsPreserved 테스트의 0.3배 가속 기대는 갱신하되 Y 보존 검증은 유지한다. 정착이 필요한 테스트는 입력 전에 실제 물리 스텝을 수행한다.

## 조사 결과

계획을 막는 미확정 기술 사항은 없다. 실제 정점 오차, 씬 가시성 및 입력 순서 경계는 구현 후 테스트로 확인할 검증 항목이지 이미 통과한 결과가 아니다.
