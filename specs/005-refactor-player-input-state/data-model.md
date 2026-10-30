# Data Model: 플레이어 입력·상태 처리

## 1. PlayerState — 하나의 동작 상태

`PlayerMovement` 내부 Enum: `Grounded`, `Airborne`, `Eliminated`. 상태 필드는 하나이며 기존 grounded/isEliminated 저장 필드는 제거한다. `public bool IsEliminated`는 `state == Eliminated`로 계산한다. UI·접촉·입력·물리 분기 모두 같은 상태를 참조한다.

| 현재 상태 | 조건 | 다음 상태 | 부수 효과 |
| --- | --- | --- | --- |
| 초기화 | 아직 실제 지지를 확인하지 않음 | Airborne | 최초 위치·방향 저장 |
| Grounded 또는 Airborne | 발 Y < fallDeathHeight | Eliminated | 재시작·점프 요청과 지지 캐시 제거, 수평 이동 차단 |
| Grounded 또는 Airborne | 생존하며 현재 HasGroundSupport 성공 | Grounded | 지상 새 점프 허용 |
| Grounded 또는 Airborne | 생존하며 현재 HasGroundSupport 실패 | Airborne | 지상 점프 불허 |
| Grounded | 유효 pendingJump + focused + 실제 지지 | Airborne | 기존 보정식으로 수직 속도 적용, 요청 소비 |
| Eliminated | 유효 pendingRestart + focused | Airborne | 최초 자세·속도 복원, 지지 재획득 대기, 카메라 스냅 |
| Eliminated | 그 외 입력·시간 경과 | Eliminated | 안내 유지, 수평 이동 차단 |
| 생존 | 비활성화 | Airborne | 지지 캐시·예약 제거 |
| Eliminated | 비활성화 | Eliminated | 탈락 유지, 예약 제거 |

### 전이 순서와 불변식

1. FixedUpdate는 새 낙사를 가장 먼저 판정한다. 새 탈락 경로는 예약을 지운 뒤 해당 물리 갱신을 탈락 상태로 종료하며 동일 입력으로 복귀하지 않는다.
2. 이미 탈락한 상태만 유효한 재시작 요청을 처리한다. 복귀한 물리 갱신에서는 일반 점프·이동을 추가 실행하지 않는다.
3. 생존 중 현재 접촉·속도·기하로 지상/공중을 갱신한 뒤 점프 요청을 한 번 소비한다. 입력 이벤트 시점에도 HasGroundSupport를 확인하여 공중 입력 예약을 방지한다.
4. 초점·중립 차단 이전에 기존과 동일한 점프 허용 순서를 보존한다. 초점 상실은 요청을 제거한다. waitingForNeutral은 기존처럼 이동 재개 조건이며, 새로운 점프 제한을 몰래 추가하지 않는다.
5. 수평 이동은 기존 계산·수치·수직 속도 보존을 따른다. 초점 없음·중립 대기는 수평 이동만 정지한다.
6. 초점 상실만으로 진행 중인 공중 물리를 정지하거나 탈락 상태를 변경하지 않는다.
7. 재시작은 grounded를 강제하지 않는다. 실제 접촉 재획득 후 Grounded로 전환한다.

## 2. InputSession — 생성 클래스 소유권

별도 서비스/모델 클래스를 만들지 않고 PlayerMovement의 private `PlayerInputActions` 필드로 보관한다.

| 정보 | 의미 | 수명/검증 |
| --- | --- | --- |
| actions | Generate C# Class로 생성한 인스턴스 | Awake 생성, OnDestroy Dispose |
| Player.Move | Value/Vector2, 기존 WASD·방향키 | performed/canceled, 크기 1 제한 |
| Player.Jump | Button/Space | initialStateCheck false, 새 지상 누름만 예약 |
| Player.Restart | Button/R | initialStateCheck false, 새 탈락 후 누름만 예약 |
| bindingMask | Keyboard&Mouse 그룹 | 활성화 전 설정; 다른 장치 동시 조작 방지 |
| devices | 기본은 플랫폼 장치, 테스트에서는 가상 장치 제한 | 테스트만 reflection으로 소유 인스턴스 접근 가능; 새 public 주입 API 불필요 |

맵 활성화와 이벤트 구독은 enabled 생명주기에 일치해야 한다. 구독 해제 → Disable → 상태 정리 순서를 지키며 Dispose는 한 번 수행한다. 초기화·검증 실패 후 OnDisable/OnDestroy도 null·부분 초기화에 안전해야 한다.

## 3. 독립 입력 정보

| 필드 | 유지 이유 | 초기화/소비 |
| --- | --- | --- |
| input: Vector2 | 현재 수평 입력 | 초점 상실·비활성화·차단 시 0; 복귀는 현재 Move 다시 읽기 |
| focused: bool | 물리 상태와 독립적인 입력 허용 조건 | Application.isFocused 및 OnApplicationFocus |
| waitingForNeutral: bool | 시작·초점 복귀·재활성화 후 유지 입력 방지 | 각 활성 control이 모두 비작동일 때 해제; 재시작은 기존대로 예외적으로 해제 |
| pendingJump: bool | 입력과 물리 갱신 사이의 유효 요청 | 생존 지상 새 입력에서 예약, 다음 물리 갱신에서 소비; 탈락·비활성화·초점 상실·복귀 시 제거 |
| jumpHeld/jumpReadyAfterRelease: bool | 새 누름과 실제 해제 구분 | 활성·초점 조건에서 실제 control 해제 관찰로 준비; 초기화·복귀 시 준비 취소 |
| pendingRestart: bool | 탈락 후 유효 요청 | 한 번 복귀 시 소비; 새 탈락·비활성화·초점 상실 시 제거 |
| restartHeld/restartReadyAfterRelease: bool | 탈락 전 유지·연타·복귀 오입력 방지 | 실제 해제 관찰로 준비, 유효 요청에서 준비 소비; 생명주기 초기화 |

canceled는 pending 요청을 무조건 제거하지 않는다. 빠른 press/release에서 요청을 유지하되 새 상태에서 유효성을 다시 확인한다. Disable/초점 취소는 실제 해제 증거가 아니다. onAfterUpdate에서 Jump는 생존 중, Restart는 탈락 중에도 해제를 관찰하는 기존 정책을 유지한다. 재시작의 ResetJumpInput 후 유지 Space는 새 점프로 처리하지 않는다.

## 4. 보존할 물리·표시 정보

- `startPosition/startRotation`: 최초 Awake에 한 번 저장, 재시작 시 덮어쓰지 않음.
- supportingColliders/contactBuffer: 실제 접촉 기반 지지. 기존 normal·separation·raycast 기준과 접촉 overflow 오류 처리 유지.
- reportedContactOverflow: 진단 bool이며 동작 Enum에 넣지 않음.
- maxSpeed/acceleration/deceleration/turnAcceleration/jumpHeight/fallDeathHeight, probe·mask·cameraFollow: 기존 이름·직렬화 값 유지.
- body, eliminationStyle, cameraFollow: 기존 Rigidbody·IMGUI·카메라 사용. 새 HUD·관리자·영구 데이터 없음.

## 5. 검증 계약 변경

- 지상 관찰 테스트는 삭제된 grounded bool 대신 state를 읽고 실제 접촉·물리 결과를 함께 검증한다.
- IsEliminated는 기존 공개 조회 계약을 유지한다. Enum을 테스트 편의 때문에 public으로 확장하지 않는다.
- 생성 인스턴스와 원본 자산은 이름·ID·타입·바인딩·스킴·initialStateCheck가 일치해야 한다. 생성 파일에 직접 변경을 가하지 않는다.
- 설정 검증은 PlayerInput 의존성만 제거하며 Rigidbody·Ground·probe·유효 수치의 기존 실패 차단을 유지한다.
