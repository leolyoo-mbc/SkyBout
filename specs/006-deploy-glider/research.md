# Research: 기존 Unity 프로젝트의 글라이더 확장

**Date**: 2026-11-01
**Scope**: 5단계 구현 계획을 위한 읽기 전용 코드 조사 및 Unity 6000.3 API 문서 확인. 코드 수정·테스트 실행은 하지 않았다.

## 조사 질문과 해소 결과

| 질문 | 근거 | 결정 |
| --- | --- | --- |
| 현재 기술 환경은 무엇인가? | ProjectVersion.txt, Packages/manifest.json, asmdef | Unity 6000.3.26f1, 기존 Input System/URP/Test Framework 유지 |
| Space를 어떻게 확장하는가? | PlayerInputReader.cs, PlayerMovement.cs | 기존 JumpPressed에서 입력 시 의도 분기, 실행 시 재검증 |
| 전개 상태는 어디에 두는가? | PlayerMovement.cs의 enum 및 접지 갱신 | enum에 Gliding 추가, 접지/비활성화 덮어쓰기 수정 |
| 일정 하강과 중력은 어떻게 양립하는가? | Rigidbody API 및 현재 검증 | Gliding에서만 useGravity=false, 나머지 true |
| 비활성화 시 이미 펼친 글라이더는? | OnDisable 및 spec FR-009 | 상태·음수 Y속도·중력 해제 유지, 재활성화 시 낙사/지지 검사 |
| 표시와 회귀를 어떻게 검증하는가? | 기존 씬/PlayMode fixture 및 spec SC | 씬 자식 시각 표시, 새 동작 시험 및 기존 전체 회귀 |

기술적 미확정 사항은 남아 있지 않다. 실제 수치 체감·시각 가독성과 테스트 실행 결과는 구현 후 검수 대상이지 설계 완료의 증거가 아니다.

## Decision 1 — 기존 프로젝트·입력 자산 유지

**Decision**: `Assets/Scenes/SampleScene.unity`, `PlayerInputActions`와 `PlayerInputReader.JumpPressed`를 그대로 사용한다. `PlayerMovement`가 입력 순간 실제 지지로 jumpRequested 또는 glideRequested 중 하나를 기록한다. 이미 Gliding이면 새 Space를 버린다. 실행할 때 원래 의도의 조건을 다시 확인하고 무효면 폐기한다.

**Rationale**: 현재 RequestJump와 ProcessJumpRequest는 이미 입력 시/실행 시 접지를 각각 확인한다. InputAction Button은 새 누름을 전달하고 기존 실제 키 해제 정책이 초점 복귀·재시작 후 입력 재생을 막는다. 새 키 폴링이나 액션을 추가할 이유가 없다.

**Alternatives considered**: Glide 액션을 같은 Space에 추가하면 이벤트 중복·생성 자산 변경을 유발한다. 물리 틱의 isPressed 확인은 점프 키 유지가 자동 전개로 이어질 수 있다. 요청을 실행 시점에 재분류하면 공중 입력이 착지 후 점프하는 기존 금지 규칙을 깨뜨린다.

## Decision 2 — 상호 배타적인 Gliding enum 상태

**Decision**: `PlayerState`에 Gliding을 추가한다. UpdateGroundedState는 비지지 Gliding을 유지하고 OnDisable은 기존 생존 상태를 무조건 Airborne으로 바꾸지 않도록 수정한다. 입력 요청 초기화와 지속 동작 상태 초기화를 구분한다.

**Rationale**: 별도 isGliding bool은 Grounded+isGliding, Eliminated+isGliding 같은 불가능한 조합을 만들 수 있다. 기존 enum에 한 값을 추가하고 이미 변경해야 하는 두 상태 갱신 지점을 명시적으로 처리하는 편이 상태의 진실 원천을 하나로 유지한다. 연구 에이전트는 bool을 최소 변경 대안으로 제시했으나 이번 설계에서는 조합 불변식 보존을 우선했다.

**Alternatives considered**: 별도 bool은 작은 변경이지만 매 전이에서 두 값을 동기화해야 한다. 범용 상태머신이나 별도 글라이더 Rigidbody 제어 컴포넌트는 이번 네 상태에 과도하고 물리 소유권을 분산한다.

## Decision 3 — 전개 중에만 중력 해제와 일정 음수 속도

**Decision**: `glideDescentSpeed`는 초기 2 m/s, 양의 유한값이다. 전개 직후부터 Y속도는 -glideDescentSpeed로 두며 useGravity=false로 일정하게 유지한다. 착지·탈락·재시작·컨트롤러 제거에서 중력을 복원한다. 상태별 중력 설정을 검증하도록 기존 ValidateSettings를 확장한다. 수직 제어는 CanMove 조기 반환보다 앞에 둔다.

**Rationale**: 기본 물리는 중력 -9.81, fixed timestep 0.02초이고 수평 계산은 Y를 그대로 보존한다. 중력을 켠 채 Y=-2를 매 틱 넣으면 시뮬레이션 후에는 -2.1962가 되어 설정과 결과가 다르다. 중력을 끄면 상승 중 전개에서도 위치 이동 없이 즉시 일정 하강을 시작하고, 컴포넌트가 비활성화되어도 물리가 진행되는 한 마지막 하강 속도를 유지한다.

**Alternatives considered**: 중력을 유지하고 적분량을 매번 보정하는 방법은 기존 useGravity 검증을 유지하지만 비활성화 중 보정이 중단되고, 매우 작은 하강 설정에서 적분 전 속도가 양수가 된다. 속도 상한만 제한하면 상승을 계속하므로 현재 명세와 다르다. 부력·양력 시뮬레이션, 목표 고도 제어는 6단계 또는 별도 범위다.

Unity 문서는 일반적인 물리 시뮬레이션에서 매 틱 직접 속도 제어를 권장하지 않는다. 이번 결정은 기존 게임 컨트롤러처럼 물리적 사실성보다 명시된 일정 속도 조작을 우선하는 제한적 운동 제어다. 외부 힘·전투 넉백을 일반화하지 않는다.

## Decision 4 — 실제 지지·낙사 우선과 시각 자식

**Decision**: `GroundSupport`는 변경하지 않고 재사용한다. 낙사는 FixedUpdate 첫 판정이다. 착지 인식 시 Gliding을 종료하고 첫 착지 렌더까지 시각이 남지 않게 한다. 기존 UpdateGroundContacts가 유효 접촉을 수집한 직후 Gliding의 실제 지지를 재검사하여 착지 전이를 반영하고, 다음 FixedUpdate에서도 같은 조건을 재확인한다. 표시 자식은 씬에 하나만 만들고 상태에 따라 SetActive한다.

**Rationale**: 현재 지지는 실제 접촉과 현재 발밑 Raycast를 함께 요구하여 옆면·근접 지면 오판정을 방지한다. 단순 높이 비교나 trigger는 기존 계약을 깨뜨린다. 전개마다 Instantiate하면 반복 시 표시 누적과 관리 비용이 생긴다.

**Alternatives considered**: 별도 trigger 착지, 높이만으로 접기, 기존 Player Collider 확대는 거부한다. 완성형 아트·애니메이터·런타임 시각 생성기는 최소 식별 요구보다 범위가 크다.

## Decision 5 — 회귀 기대 변경은 공중 Space 확장에 한정

**Decision**: 기존 전체 시험의 실제 기준선을 먼저 확보한다. 공중 Space 무동작 단언은 전개/추가 점프 금지 단언으로 갱신하고 나머지 기준은 유지한다. 새 GliderTests는 입력·상태·물리, GliderSceneTests는 실제 씬·표시·반복을 검증한다.

**Rationale / 발견된 구체적 영향**:

- `FallEliminationTests.SpaceOutsideIslandNeverAddsJumpOrGliderFiveTimes`는 직접 충돌한다. 글라이더 미제공 기대만 교체하며 추가 점프 금지는 유지한다.
- `JumpTests.AirAndHeldInputNeverAutoJump`는 새 공중 입력으로 느린 하강이 시작될 수 있다. 기존 점프 결과를 약화하지 말고, 전개된 경우의 착지 대기는 설정·높이 기반 유한 timeout으로 구분한다.
- `JumpTests`의 지상 수락 후 지지 상실 요청은 전개로 재해석하지 않아야 한다. 공중 입력 후 단순 Y<0 검증은 전개 상태와 일정 하강 속도 검증으로 보강한다.
- `RestartTests`에는 복귀 직후 접촉 재구축 전 새 Space를 누르고 Y==0을 확인하는 사례가 있다. 실제 비지지일 때 새 Space는 이제 전개 의도다. 이 사례는 자동 입력 재생 금지와 명시적 새 공중 입력을 분리하고, 전개 후 정상 지지 재획득/지상 복귀를 단언하도록 갱신한다. 키를 유지한 복귀가 자동 전개하지 않는 기존 단언은 변경하지 않는다.
- `PlayerInputRefactorTests`의 원본/생성 계약과 private 상태 이름 의존은 보존한다. 새 관측은 IsGliding을 사용하고 광범위한 필드 이름 변경은 하지 않는다.
- 비활성화 상태에서 충돌 수집은 기존처럼 차단한다. 재활성화 시 WakeUp과 이후 접촉 재수집으로 지지를 복구하며, 기존 캐시를 이용해 허위 착지하지 않는다.

**Alternatives considered**: 충돌하는 테스트 삭제, 전체 timeout 일괄 확대, 과거 통과 수 복사는 검증 의미를 손상하므로 거부한다. headless 결과를 사람의 가독성·조작감 검수로 대체하지 않는다.

## Sources

- `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/TimeManager.asset`, `ProjectSettings/DynamicsManager.asset`, `Packages/manifest.json`.
- `Assets/Scripts/PlayerMovement.cs`, `PlayerInputReader.cs`, `GroundSupport.cs`, `MovementMath.cs`, `TopDownCameraFollow.cs`.
- `Assets/Player_Actions.inputactions`, `Assets/Scenes/SampleScene.unity`, `Assets/Tests/PlayMode/`.
- [Unity 6000.3 Rigidbody.linearVelocity](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rigidbody-linearVelocity.html): 월드 공간 속도, 동적 Rigidbody 조건, 직접 속도 제어의 일반적 주의점.
- [Unity 6000.3 Rigidbody.useGravity](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rigidbody-useGravity.html): 중력 영향 해제 동작.

독립 연구 에이전트의 읽기 전용 코드 조사 결과를 대조했다. 권장 대안 중 별도 bool·중력 유지 대신 상호 배타 상태·비활성 중 일정 하강을 충족하는 위 결정을 선택했다.
