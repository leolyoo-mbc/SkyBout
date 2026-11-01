# Data Model: 공중 글라이더

## PlayerMovement 소유 상태

| 필드/관측값 | 형식 | 규칙 |
| --- | --- | --- |
| state | Grounded / Airborne / Gliding / Eliminated | 상호 배타적. Gliding은 생존 공중만 의미 |
| IsEliminated | 읽기 전용 bool, 기존 | 기존 계약 유지 |
| IsGliding | 읽기 전용 bool, 신규 | state == Gliding; 시각과 테스트의 관측값 |
| jumpRequested | bool, 기존 | 입력 시 지상에서만 설정, 다음 물리 틱에 소비 |
| glideRequested | bool, 신규 | 입력 시 비전개 공중에서만 설정, 다음 물리 틱에 소비 |
| restartRequested | bool, 기존 | 탈락 후 새 R만 허용 |
| glideDescentSpeed | 직렬화 float | 기본 2 m/s, 유한하고 0보다 큼 |
| gliderVisual | 직렬화 GameObject 참조 | 실제 씬에서 지정. Player 자손, 비충돌·비물리 표시 전용 |
| startingPosition / startingRotation | 기존 자세 | 복귀 기준 변경 없음 |

요청은 저장 게임 데이터가 아니며 소비 또는 초점 상실·비활성화·탈락·재시작 때 폐기한다. jumpRequested와 glideRequested는 한 Space 입력에 함께 설정하지 않는다. 동일 틱에 여러 이벤트가 오면 첫 유효 미처리 Space 요청만 보유하며 상태를 바꾼 후 다른 동작을 연쇄 실행하지 않는다.

## 관계

- `PlayerInputReader`는 기존 JumpPressed 이벤트를 전달할 뿐 접지나 전개를 판단하지 않는다. `Jump`라는 액션명은 호환성을 위해 유지한다.
- `GroundSupport`가 발아래 실제 접촉과 현재 Raycast로 지지를 판정한다. Collider 옆면은 지상이 아니다.
- `MovementMath`는 Gliding에서도 현재 Y 속도를 보존하면서 XZ만 계산한다.
- `gliderVisual`의 활성 여부는 상태 변경에 종속되며 게임 규칙의 별도 진실 원천이 아니다.

## 상태 전이

| 현재 | 조건 | 다음 | 효과 |
| --- | --- | --- | --- |
| Grounded | 유효 새 Space, 실행 시 지지 유지 | Airborne | 기존 점프 속도, 중력 켬 |
| Grounded | 실제 지지 상실 | Airborne | 기존 추락 |
| Airborne | 새 Space 요청, 실행 시에도 비지지·생존 | Gliding | 중력 끔, Y = -glideDescentSpeed, 표시 켬 |
| Airborne / Gliding | 하강 중 실제 지지 | Grounded | 중력 켬, 표시 끔, 남은 전개 요청 폐기 |
| Gliding | 새 Space 또는 키 유지 | Gliding | 아무 동작도 저장하지 않음 |
| 생존 전체 | 발 Y < fallDeathHeight | Eliminated | 중력 켬, 표시 끔, 모든 요청 제거, 기존 안내 |
| Eliminated | 기존 조건의 새 R | Airborne | 시작 자세·속도·입력 정리; 지지 재획득 후 Grounded |
| 생존 공중 | 초점 상실 또는 비활성화 | 기존 공중 상태 | 요청·수평 조작 제거, 전개 여부 보존 |

경계와 같은 높이는 생존이다. 초점 복귀나 재활성화만으로 전개하거나 접지하지 않는다. 비활성화 시 기존 접촉 캐시는 비우되 Gliding을 Airborne으로 덮어쓰지 않는다. 비활성화 도중 물리가 계속되면 중력이 꺼진 동적 Rigidbody는 마지막 일정 하강을 계속한다. 다시 활성화된 첫 틱에서 낙사와 실제 지지를 재판정한다. GameObject 자체 비활성화나 Editor 일시정지로 멈춘 물리를 별도로 진행시키지는 않는다.

## 물리 처리 순서

1. 발 위치로 낙사부터 검사한다. 새 탈락 틱에는 전개·점프·재시작을 실행하지 않는다.
2. 이미 탈락이면 기존 새 재시작 요청만 처리한다.
3. 실제 지지를 확인한다. 지지하면 Grounded 및 기본 중력, 비지지 Gliding은 유지, 그 외 Airborne.
4. 입력 시 확정된 점프/전개 요청을 한 번 소비한다. 실행 시 조건 불일치면 버리고 다른 종류로 변환하지 않는다.
5. Gliding이면 중력이 꺼졌는지 보장하고 Y 속도를 -glideDescentSpeed로 맞춘다.
6. 초점·이동 해제 대기면 XZ를 중단한다. 그렇지 않으면 기존 수평 계산을 적용한다. 5번은 수평 조기 반환 전에 실행한다.
7. 이후 Unity 물리와 충돌 콜백이 진행된다. UpdateGroundContacts가 실제 접촉을 수집한 직후 Gliding이면 실제 지지를 다시 확인하여 착지 전이를 반영한다. 다음 FixedUpdate에서도 재검사한다. 첫 착지 표시 프레임에 잔류하지 않는지는 씬 시험으로 확인한다.

## 검증 불변식

- 위치 직접 변경은 기존 재시작만 수행한다. 전개는 transform/Rigidbody.position을 바꾸지 않는다.
- useGravity == false는 Gliding에서만 허용한다. 일반 공중·지상·탈락·재시작은 true다.
- `ValidateSettings`는 Gliding에서 컨트롤러가 끈 중력을 오설정으로 판정하지 않도록 상태를 고려한다. 양의 유한 하강 속도와 기존 지지·물리 제약은 검증한다.
- OnDisable은 현재 전개 여부·수직 속도를 보존한다. OnDestroy는 컨트롤러만 제거되는 경우 남은 Rigidbody의 기본 중력을 복원하고 입력 구독을 해제한다.
- 지상·탈락·재시작 후 첫 렌더에서 표시가 꺼져 있고, 반복해도 표시 객체가 늘지 않는다.
