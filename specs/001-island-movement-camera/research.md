# Research: 하늘섬 기본 이동과 탑뷰 카메라

**Date**: 2026-10-29
**Scope**: 기존 Unity 프로젝트의 정적 검토와 공식 문서 조사. Editor 실행·조작감 검증 결과는 아니다.

## 1. 기존 프로젝트 재사용

- **Decision**: Unity `6000.3.26f1`, C#, URP `17.3.0`, Input System `1.20.1`, Test Framework `1.6.0`을 그대로 사용한다. 기존 `Assets/Scenes/SampleScene.unity`를 기능 검증 씬으로 발전시킨다.
- **Rationale**: 프로젝트에 Player, Ground, PlayerInput, Rigidbody와 이동 스크립트가 이미 있다. 새 프로젝트·패키지 설치가 필요하지 않다.
- **Alternatives considered**: 새 Unity 프로젝트, CharacterController 전환, 이동 프레임워크 도입은 재사용 이점과 사용자 선호를 잃으므로 제외한다.
- **Evidence**: `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `Assets/Scenes/SampleScene.unity`, `Assets/Scripts/PlayerMovement.cs`.

## 2. 물리와 캐릭터 이동의 역할

- **Decision**: dynamic Rigidbody와 중력·충돌을 유지한다. 기존처럼 목표 수평 속도에 제한된 가속도로 접근하도록 `linearVelocity.x/z`를 제어하고 Y 속도는 보존한다. Transform 이동이나 지상 높이 고정은 사용하지 않는다.
- **Rationale**: 사용자가 선호한 구조를 유지하면서 지상 조작감을 직접 조정할 수 있다. 이는 힘만으로 자연 운동을 재현하는 설계가 아니라 물리 충돌을 이용하는 캐릭터 속도 제어다.
- **Alternatives considered**: `AddForce`도 가능하나 이번 지상 이동만을 위해 반드시 변환할 이유는 없다. 넉백·외부 힘과의 혼합은 향후 전투 설계에서 검토한다.
- **Limit**: 속도 제어는 외부 충격을 목표 속도 쪽으로 되돌릴 수 있다. 이번 단계에서 넉백 호환성이나 슈퍼바이브의 실제 물리값을 검증했다고 주장하지 않는다.
- **Sources**: [Rigidbody.linearVelocity](https://docs.unity3d.com/ja/6000.0/ScriptReference/Rigidbody-linearVelocity.html), [Unity 6000.3 AddForce](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/rigidbody/addforce).

## 3. 접지·출발·제동 수정

- **Decision**: 접지 검사는 `FixedUpdate` 시작에 수행한다. Ground 레이어의 비트리거 충돌체만 검사하고, 자기 Collider는 Ground에 포함하지 않는다. 기존 CheckSphere 방식은 유지한다.
- **Rationale**: 현재는 `Update`의 접지 결과를 물리 단계에서 사용한다. 같은 물리 단계의 위치로 검사하면 오래된 접지 결과 사용을 줄인다. 단일 평탄섬에서는 경사면·계단용 별도 이동 시스템이 필요하지 않다.
- **Decision**: 최고 속도와 별도로 출발 가속도, 정지 감속도, 방향 전환 가속도를 Inspector에 노출한다. 입력이 없으면 감속, 정지 상태에서 출발하면 출발 가속, 이미 이동하며 방향을 바꾸면 방향 전환 가속을 적용한다. 같은 방향에서 목표 속도보다 빠른 경우에는 감속을 적용한다.
- **Rationale**: 현재 방향 일치도 판정은 정지 상태에서도 제동 분기에 들어간다. 질량 80, 추진력 800, 제동 배율 5, timestep .02에서 출발 첫 단계 증가량은 1m/s, 이후 직진 단계는 .2m/s다. 제동값을 바꾸면 출발도 달라지는 결합을 제거한다.
- **Migration**: 기존 `moveForce / mass`를 초기 가속도 10m/s², 여기에 기존 제동 배율을 곱한 50m/s²를 초기 감속·전환 가속도로 옮긴다. 최고 속도 8m/s는 초기값으로 재사용한다. 모두 조정용 출발점이며 합격 수치가 아니다. 스크립트 필드 변경과 씬 직렬화 값을 함께 갱신한다.
- **Alternatives considered**: 제동 조건 한 줄만 고치면 출발 버그는 줄지만 제동과 전환의 조정이 계속 묶인다. 별도 이동 상태 클래스는 현재 규모에 불필요하다.
- **Sources**: [FixedUpdate](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/monobehaviour/fixedupdate), [Physics.CheckSphere](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/physics/checksphere).

## 4. 입력과 초점 복귀

- **Decision**: 기존 PlayerInput의 Send Messages와 `OnMove(InputValue)`를 유지한다. `Player/Move`의 WASD 조합을 사용하고 입력 크기를 1 이하로 제한한다. 월드 X/Z가 화면 좌우/위아래와 대응하도록 카메라 yaw를 0으로 유지한다.
- **Evidence**: 씬 PlayerInput의 GUID는 `Assets/Player_Actions.inputactions.meta`와 일치한다. 프로젝트 공용 입력 자산인 `InputSystem_Actions.inputactions`와 구분해 실제 연결된 자산을 재사용한다. Keyboard&Mouse 스킴은 키보드와 마우스를 모두 요구하므로 자동 테스트도 두 가상 장치를 제공한다.
- **Decision**: 초점 상실·컴포넌트 비활성화 시 캐시 입력과 수평 속도를 비운다. 초점 복귀 시에는 중립 입력을 확인한 뒤 새 입력을 받아 이동한다. 복귀 시 실제 키 상태도 확인해, 계속 누른 키의 재동기화를 새 누름으로 처리하지 않는다.
- **Rationale**: Input System은 초점 상실 시 취소, 복귀 시 장치 동기화를 수행할 수 있어 기본 동작만으로 FR-010을 보장할 수 없다.
- **Alternatives considered**: 입력 시스템 전체 교체, 자체 키 바인딩 UI, 전역 background 설정 변경은 필요하지 않다.
- **Source**: [Input System BackgroundBehavior](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/api/UnityEngine.InputSystem.InputSettings.BackgroundBehavior.html).

## 5. 카메라

- **Decision**: Main Camera에 작은 `TopDownCameraFollow` 하나를 추가한다. perspective 카메라를 유지하고 target, pitch, distance, fieldOfView, followSharpness를 직렬화한다. `LateUpdate`에서 보간된 플레이어 Transform을 추적하고 프레임 시간에 따른 지수 보간을 사용한다. 첫 화면 전에는 초기 구도로 즉시 배치한다.
- **Rationale**: 현재 씬 카메라는 고정돼 있다. Rigidbody interpolation은 이미 켜져 있으므로 유지하고 표시 위치를 추적한다. 입력에 따른 카메라 회전·커서 오프셋은 없다.
- **Initial values**: 현재 pitch 55도, FOV 60도를 출발점으로 삼고 거리와 추적 반응을 플레이하며 조정한다. 카메라 yaw/roll은 0이다.
- **Alternatives considered**: Cinemachine 신규 의존성과 orthographic 전환은 이번 캐릭터 단일 추적에 필요하지 않다.
- **Sources**: [LateUpdate](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/monobehaviour/lateupdate), [Rigidbody interpolation](https://docs.unity.com/en-us/engine/6000.3/manual/physics-section/physics-overview/rigidbody/rigidbody-interpolation).

## 6. 섬·공중 동작·검증 범위

- **Decision**: Ground를 유한한 두께가 보이는 단일 하늘섬으로 구성한다. 평탄한 윗면, Ground 레이어와 Collider를 유지하고 주변에는 아래쪽 공간이 보이게 한다. 경계 clamp·숨은 벽·Y 고정·자동 복귀를 넣지 않는다.
- **Decision**: 기존 공중 제어는 호환 동작으로 보존하되 새로운 점프·글라이딩·낙사·리스폰 기능을 추가하지 않는다. 공중 제어가 새 가속 필드에 의존하면 해당 참조만 함께 이관한다. 섬 밖으로 떨어졌다면 Play를 종료하고 재실행한다.
- **Decision**: 반복 가능한 속도·입력 회귀는 소수 PlayMode 테스트로 확인하고 실제 키보드·초점·카메라 구도·조작감은 직접 플레이한다. 수치 조정은 Inspector에서 하고 별도 설정 화면·저장 시스템을 만들지 않는다.
- **Rationale**: 현재 기능 범위를 지키면서 물리 기반 이동과 향후 낙하를 방해하지 않는다. Editor 실행 방법은 검증 가이드에 명시하며 계획 단계에서 성공했다고 간주하지 않는다.

## Resolution

구현 방식의 미정 사항은 위 결정으로 해소했다. 사용자 조정 수치의 최종값은 의도적으로 유보하며 추가 명확화 질문이나 다음 단계 진행 차단 사유가 아니다.
