# Data Model: 탈락 후 재시작

별도 저장소·파일 저장·새 도메인 객체 없이 기존 MonoBehaviour의 런타임 상태를 확장한다. 아래 신규 필드명은 설계상 이름이며 구현 시 주변 명명 규칙을 따른다.

## PlayerMovement

| 상태 | 형식/수명 | 규칙 |
| --- | --- | --- |
| startPosition | Vector3 / Awake에서 한 번 저장 | 최초 Rigidbody 월드 위치. 재시작마다 덮어쓰지 않는다. |
| startRotation | Quaternion / Awake에서 한 번 저장 | 최초 Rigidbody 월드 회전. |
| cameraFollow | TopDownCameraFollow / 직렬화 참조 | SampleScene의 Main Camera. 카메라 없는 물리 테스트에서는 null 허용. |
| isEliminated | 기존 bool | 낙사로 true, 유효한 재시작 적용으로만 false. 읽기 전용 IsEliminated 유지. |
| pendingRestart | bool / 일시적 요청 | 탈락·활성·초점·새 누름 조건이 충족될 때만 예약. 실행·새 탈락·비활성화·초점 상실 시 제거. |
| restartHeld, restartReadyAfterRelease | bool / 입력 상태 | Jump와 동일한 방식으로 새 누름과 실제 해제를 구분. 초점 전환 후 유지 입력은 허용하지 않는다. |
| grounded, supportingColliders | 기존 상태 | 복귀 시 캐시를 비우고 false로 시작하여 실제 접촉으로 다시 판정. |
| input, pendingJump, jumpHeld, jumpReadyAfterRelease | 기존 상태 | 저장된 움직임·점프 요청 제거. 현재 Move 값은 복귀 시 다시 읽고, Jump는 해제 후 새 누름만 허용. |
| body | 기존 Rigidbody | 동일 인스턴스 유지. 복귀 시 위치·회전 복원, linearVelocity·angularVelocity 모두 0. |

### 불변 조건

- 낙사는 기존 `FootPosition.y < fallDeathHeight` 비교를 유지한다. 같은 높이는 생존이다.
- 최초 시작 위치는 안전한 섬 표면에 있고 발 위치가 낙사 높이보다 높다. SampleScene 통합 시험으로 이 전제를 확인한다.
- 생존 중 Restart 입력은 저장하지 않는다. 요청이 여러 번 도착해도 한 번만 적용한다.
- 탈락 중에는 이동·점프를 수행하지 않는다. Restart 입력과 해제 관찰만 계속 가능하다.
- 평상시 초점 복귀/OnEnable의 중립 입력 정책은 유지한다. 재시작 시 유지 중 WASD의 재개만 별도 처리한다.
- 런타임에서 설정값이나 시작 위치를 임의 변경하지 않는다. 영구 저장은 없다.

## 상태 전이

| 이전 상태 | 사건 | 결과 |
| --- | --- | --- |
| 생존 | R 입력 | 변화 없음, 요청 없음 |
| 생존 | 발이 낙사 경계 아래 | 탈락; 지지·점프·재시작 예약 제거; 그 물리 판정에서 복귀 불가 |
| 탈락 | R 유지, WASD/Space, 시간 경과, 초점 복귀 | 탈락 유지 |
| 탈락 | 초점 있는 활성 상태에서 새 R 누름 | 재시작 요청 예약 |
| 탈락 + 요청 | 다음 FixedUpdate | 시작 자세 복원, 속도·캐시 초기화, 생존 복귀, 카메라 스냅, 안내 제거 |
| 복귀한 생존 | 다음 물리 접촉 | 실제 섬 지지 획득; 새 점프 허용 |
| 복귀한 생존 | 다시 경계 아래 추락 | 같은 규칙으로 재탈락 |

‘복귀’는 별도의 지속 상태나 enum이 아니라 기존 생존 상태로 돌아가는 원자적인 처리다. 정상 물리 접촉 갱신을 거쳐 2초 이내에 지상 조작 가능해야 한다.

## 입력 자산

`Assets/Player_Actions.inputactions`의 기존 Player 맵에 다음만 추가한다.

- 이름: Restart
- 타입: Button, expectedControlType: Button
- 바인딩: `<Keyboard>/r`, 그룹: Keyboard&Mouse
- initialStateCheck: false
- 기존 Move/Jump 액션 ID와 바인딩·제어 스킴은 유지한다.

## TopDownCameraFollow

- 기존 target, pitch, distance, fieldOfView, followSharpness를 유지한다.
- `SnapToTarget()`은 기존 검증을 거쳐 타깃과 설정에 맞는 위치·회전을 즉시 적용한다.
- 초기 배치와 재시작은 같은 배치 계산을 사용하고 평상시 LateUpdate 보간은 유지한다.

## 화면 안내

- 표시 조건: isEliminated가 true인 동안.
- 내용: 탈락 사실 + R 키로 재시작 가능하다는 설명.
- 플레이어 월드 좌표와 무관한 기존 화면 중앙 패널을 사용한다. 새 UI 상태 객체나 플레이어 생성은 없다.
