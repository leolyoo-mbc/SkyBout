# Data Model: 하늘섬 기본 이동과 탑뷰 카메라

**Date**: 2026-10-29

서버·DB 모델이 아니라 씬에 있는 오브젝트, Inspector 값, 플레이 중 상태를 설명한다. 저장은 Unity 씬 직렬화만 사용한다.

## 하늘섬 / Ground

| 항목 | 내용 |
|---|---|
| Identity | 씬의 단일 Ground 오브젝트, 기존 참조 보존 |
| 표면 | 수평 윗면과 유한한 경계, 눈에 보이는 두께 |
| Collider | 윗면과 몸체에 맞는 비트리거 3D 충돌체 |
| Layer | 기존 Ground 레이어 |
| 관계 | 플레이어 접지 검사의 대상 |

이동 시험을 할 충분한 면적이 필요하다. 경계는 표면이 끝나는 곳을 뜻하며 이동 금지선이 아니다. 별도 경계 상태나 자동 복귀 데이터를 만들지 않는다.

## 플레이어 / PlayerMovement

| Inspector 값 | 의미 / 검증 |
|---|---|
| maxSpeed | 지상 최고 수평 속도(m/s), 양수 |
| acceleration | 출발·직진 가속도(m/s²), 양수 |
| deceleration | 정지·초과 속도 감속도(m/s²), 양수 |
| turnAcceleration | 이동 중 방향 전환 가속도(m/s²), 양수 |
| groundCheckTransform | 필수 발밑 검사 위치, 플레이어 자식 |
| groundCheckRadius | 양수, 발밑 주변을 검사할 만큼 작은 값 |
| groundLayer | Ground를 포함하고 플레이어 자신은 제외한 비어 있지 않은 마스크 |
| airControl | 기존 호환 필드, 0~1. 새 공중 기능의 합격 기준은 아님 |

dynamic Rigidbody, CapsuleCollider, PlayerInput을 가진다. 중력과 interpolation을 유지하고 X/Z 회전을 잠근다. 질량은 양수이며 가속·감속 조작감은 별도 값으로 조정한다. 잘못된 필수 참조나 비유한 수치는 명확한 설정 오류로 처리한다. Inspector 범위 검증은 `OnValidate`와 초기화 확인으로 충분하며 별도 검증 서비스는 만들지 않는다.

### 런타임 상태

- 입력 벡터: 반대 방향 상쇄 후 길이 1 이하의 X/Y 값. 수평 월드 X/Z로 매핑한다.
- 접지 여부: 같은 `FixedUpdate`에서 Ground 검사로 갱신한다.
- 수평 속도: Rigidbody 속도의 X/Z. Y 속도는 중력/충돌 결과를 보존한다.
- 입력 허용 여부: 정상 입력 허용, 초점 없음, 복귀 후 중립 대기 중 하나. 별도 상태 클래스가 아니라 필요한 플래그로 표현한다.

### 상태 전환

1. 초기화 → 씬의 안전한 시작 위치, 정지. 유효한 참조 확인 후 입력 수신.
2. 지상 + 입력 → 목표 수평 속도 쪽으로 가속 또는 방향 전환.
3. 지상 + 무입력/상쇄 → 감속해 정지.
4. 접지 없음 → Y 속도에 중력이 적용된다. 기존 공중 제어 호환 외에 새 비행/낙사 상태를 추가하지 않는다.
5. 초점 상실/disable → 캐시 입력과 수평 속도 초기화.
6. 초점 복귀 → 실제 입력이 중립임을 확인 → 이후 새 입력 허용. 계속 누른 키의 동기화만으로는 이동하지 않는다.
7. Play 재실행 → 저장된 씬 시작 상태. 세션 위치를 따로 저장하지 않는다.

### 기존 직렬화 값 이관

`maxSpeed=8`은 유지 가능한 초기값이다. `moveForce=800`, `mass=80`에서 acceleration=10을 얻고, 기존 brakeMultiplier=5로부터 deceleration/turnAcceleration의 시작값 50을 얻는다. 새 필드명과 씬 값은 같은 변경으로 이관하고 `PlayerMovement.cs.meta` GUID는 보존한다. 이는 최종 조작감이나 슈퍼바이브 수치의 보장이 아니다.

## 카메라 / TopDownCameraFollow

| Inspector 값 | 의미 / 검증 |
|---|---|
| target | 필수 플레이어 Transform |
| pitch | 0도 초과 90도 미만의 내려다보는 각도 |
| distance | target으로부터의 거리, 양수 |
| fieldOfView | perspective 세로 시야각, 0도 초과 180도 미만 |
| followSharpness | 추적 반응 계수, 양수. 클수록 빠르게 따라감 |

Main Camera와 연결한다. yaw/roll은 0이며 커서 오프셋 필드는 없다. 첫 화면 전 target을 기준으로 배치하고 이후 `LateUpdate`에서 추적한다. 설정값이 유효 범위여도 극단적인 근접/넓은 구도는 검증을 통해 선택하지 않으며 자동 구도 보장 시스템은 만들지 않는다. target 누락은 명확한 설정 오류로 처리하고 예외 반복을 막는다.

## 조정과 저장

플레이 중 수치는 비교용이고 Play 종료 시 Unity가 되돌릴 수 있다. 선택한 값은 Edit Mode의 씬 컴포넌트에 반영해 저장한다. 런타임 설정 파일, 사용자별 프로필, 튜닝용 ScriptableObject는 추가하지 않는다.
