---

description: "Task list for 플레이어 입력·상태 처리 리팩토링"
---

# Tasks: 플레이어 입력·상태 처리 리팩토링

**Input**: Design documents from `/specs/005-refactor-player-input-state/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/player-input.md](contracts/player-input.md), [quickstart.md](quickstart.md)

**Tests**: 포함한다. 기존 5개 PlayMode 클래스의 이행과 신규 `PlayerInputRefactorTests.cs`가 [contracts/player-input.md](contracts/player-input.md) §5와 spec SC-001~007에서 명시적으로 요구된다. 기존 시험을 삭제·완화해 통과시키지 않는다.

**Organization**: 사용자 스토리별 단계로 구성한다. 단, `PlayerMovement.cs` 한 파일을 공유하는 리팩토링이므로 같은 파일을 수정하는 작업은 순차 실행한다. 각 스토리는 자기 시험 묶음으로 독립 검증한다.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 다른 파일을 수정하며 미완료 작업에 의존하지 않아 병렬 실행 가능
- **[Story]**: 사용자 스토리 (US1, US2, US3)
- 모든 작업에 정확한 파일 경로를 포함한다

## Path Conventions

Unity 단일 프로젝트: 런타임 `Assets/Scripts/`, 테스트 `Assets/Tests/PlayMode/`, 씬 `Assets/Scenes/`, 입력 자산 `Assets/`. 증거 산출물은 gitignore된 `TestResults/`에 둔다.

`.specify/memory/constitution.md`는 미작성 템플릿이므로 추가 헌장 원칙을 적용하지 않는다.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 변경 전 기준선과 계약 스냅샷을 고정한다. 코드는 수정하지 않는다.

- [ ] T001 [P] 기존 다섯 PlayMode 클래스(MovementAndCameraTests, JumpTests, FallEliminationTests, RestartTests, RestartSceneTests) 전체를 Unity 6000.3.26f1 Test Runner에서 실행하고, 실행 시각·시험/통과/실패/건너뜀 수·실패 원인·단독 재실행 결과를 `TestResults/005-baseline.xml`과 `TestResults/005-baseline.log`에 기록한다. 임계값을 낮추거나 실패 시험을 제외해 기준선을 만들지 않는다.
- [ ] T002 [P] 변경 전 계약 스냅샷을 `TestResults/005-baseline-contract.md`에 기록한다: `Assets/Player_Actions.inputactions` GUID `f0a2cf4a96d9f914da69d6bb017890e3`, Player 맵 ID `df70fa95-8a34-4494-b137-73ab6b9c7d37`, Move/Jump/Restart 액션 ID·타입·initialStateCheck, 모든 바인딩 ID/경로/그룹, 5개 controlScheme, `Assets/Scenes/SampleScene.unity`의 PlayerInput fileID `2083166631`(목록 610행)과 PlayerMovement 직렬화 값, 사용자 변경 `SkyBout.slnx`의 현재 해시.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 생성 입력 클래스, 단일 상태 필드, 입력 소유권, 씬 마이그레이션, 자산 계약 시험을 준비한다.

**CRITICAL**: 이 단계가 끝나기 전에는 사용자 스토리 구현을 시작하지 않는다. T003/T004는 병렬 가능하고, T005 → T006 → T007은 순차다.

- [ ] T003 [P] `Assets/Scripts/Input` 폴더가 없으면 먼저 생성하고(폴더 meta 포함), `Assets/Player_Actions.inputactions.meta`에서 Generate C# Class를 활성화(`generateWrapperCode: 1`)하고 `wrapperClassName: PlayerActions`, `wrapperCodePath: Assets/Scripts/Input/PlayerActions.cs`, `wrapperCodeNamespace` 비움으로 설정한 뒤 재임포트해 `Assets/Scripts/Input/PlayerActions.cs`와 폴더/파일 meta를 생성한다. 생성 파일은 수동 편집하지 않고, SkyBout.Runtime.asmdef(`Unity.InputSystem` 참조)에서 컴파일되며 Console 오류가 없는지 확인한다. 원본 자산의 GUID·맵·액션·바인딩 JSON은 변경하지 않는다.
- [ ] T004 [P] `Assets/Scripts/PlayerMovement.cs`에 private enum `PlayerState { Grounded, Airborne, Eliminated }`와 단일 `state` 필드를 추가하고 기존 `grounded`/`isEliminated` 저장 필드를 제거한다. `public bool IsEliminated => state == PlayerState.Eliminated`를 유지하고 `RefreshSupport`, `HasGroundSupport`, `OnGUI`, `OnDrawGizmosSelected`, FixedUpdate의 상태 조회를 새 필드로 치환하되 동작 의미는 바꾸지 않는다. maxSpeed/acceleration/deceleration/turnAcceleration/jumpHeight/fallDeathHeight/probe/mask/cameraFollow 직렬화 이름과 값을 보존한다.
- [ ] T005 `Assets/Scripts/PlayerMovement.cs`의 입력 소유권을 PlayerInput에서 생성 클래스로 이행한다: `PlayerInput` 필드·`GetComponent<PlayerInput>`·`Start()` 문자열 `FindAction` 검사·`InputValue` 콜백을 제거하고, Awake에서 `private PlayerActions actions`를 생성하며 `bindingMask`를 Keyboard&Mouse로 설정한다. OnEnable은 focused/waitingForNeutral/input/ResetJumpInput/ResetRestartInput 초기화 → `body != null && !body.isKinematic`이면 `body.WakeUp()`으로 접촉 콜백 재획득 → named performed/canceled 메서드와 `InputSystem.onAfterUpdate` 구독 → `actions.Player.Enable()` 순서, OnDisable은 구독 해제 → `actions.Player.Disable()` → 요청·입력·지지 정리 순서, OnDestroy는 `actions.Dispose()` 한 번으로 바꾼다. 세 액션의 named 핸들러를 만들고 기존 OnMove/OnJump/OnRestart 조건을 1차 이식한다(이후 스토리 단계에서 다듬는다). 초기화·검증 실패 후 부분 초기화와 반복 생명주기에 null 안전해야 한다. (T003, T004 완료 후)
- [ ] T006 `Assets/Scenes/SampleScene.unity`에서 PlayerInput 컴포넌트(fileID `2083166631`, 610행 컴포넌트 목록 참조 포함)를 실제 삭제하고 `UnityEngine.InputSystem.PlayerInput|2083166631` 검색 결과가 0건인지 확인한다. PlayerMovement fileID·직렬화 값·Rigidbody/Collider/GroundCheck/cameraFollow 참조·GameObject/Transform 식별자는 유지하며 Missing Script와 dangling reference가 없어야 한다. 다른 자산·패키지·프로젝트 설정은 변경하지 않는다. (T005 완료 후)
- [ ] T007 `Assets/Tests/PlayMode/PlayerInputRefactorTests.cs`와 Unity 생성 meta를 추가해, 원본 `Assets/Player_Actions.inputactions`와 `Assets/Scripts/Input/PlayerActions.cs` 생성 인스턴스의 맵·액션 이름/ID/타입/바인딩/initialStateCheck/controlScheme 일치를 검증한다. 이름 변경·바인딩 누락 등 음성 사례가 실제로 거부되는지도 검증하고, SampleScene Player에 `UnityEngine.InputSystem.PlayerInput` 컴포넌트가 없음을 확인한다. 생성 파일은 비교 대상일 뿐 수정하지 않는다. (T003, T006 완료 후)

**Checkpoint**: 생성 입력이 컴파일되고, PlayerMovement가 이를 소유하며, 씬에 PlayerInput이 없고, 자산 계약 시험이 존재한다.

---

## Phase 3: User Story 1 - 기존 조작 그대로 이동하고 점프하기 (Priority: P1) — MVP

**Goal**: WASD 8방향 이동·가속/감속·방향 전환·대각 제한과 Space 새 입력 점프를 생성 입력으로 그대로 유지한다.

**Independent Test**: `MovementAndCameraTests`, `JumpTests`, `PlayerInputRefactorTests`의 이동·점프 사례를 가상 Keyboard/Mouse로 실행해 8방향 최고 속도, 반대 키, 20회 점프·착지, 높이 ±5%, Space 유지·공중 입력 무점프를 확인한다. US2 없이도 이 묶음만으로 통과/실패를 판정할 수 있다.

- [ ] T008 [US1] `Assets/Scripts/PlayerMovement.cs`의 Move performed/canceled named 핸들러를 구현한다: performed에서 `ReadValue<Vector2>()`를 `Vector2.ClampMagnitude(..., 1f)`로 저장하되 `isActiveAndEnabled && !IsEliminated && focused && !waitingForNeutral`일 때만, canceled에서 input을 `Vector2.zero`로 정리한다. 반대 이동 키 동시 입력으로 벡터가 0이 되는 경우와 실제 키 해제를 동일시하지 않도록 waitingForNeutral 판정과 분리한다.
- [ ] T009 [US1] `Assets/Scripts/PlayerMovement.cs`의 Jump performed/canceled 핸들러를 구현한다: performed에서 새 누름(`!jumpHeld`)·`jumpReadyAfterRelease`·focused·생존·현재 `HasGroundSupport()`를 모두 만족할 때만 `pendingJump = true`로 예약하고, canceled는 `jumpHeld`만 내리며 `pendingJump`를 지우지 않는다. 공중 입력·키 유지·옛 접촉은 예약하지 않는다.
- [ ] T010 [US1] `Assets/Scripts/PlayerMovement.cs` FixedUpdate의 생존 경로를 정리한다: `state = HasGroundSupport() ? PlayerState.Grounded : PlayerState.Airborne`, `pendingJump` 1회 소비, `(PlayerState.Grounded, pendingJump, focused)` tuple pattern/`when` guard에서 기존 반 스텝 중력 보정식(`sqrt(2 * -Physics.gravity.y * jumpHeight) + gravity * Time.fixedDeltaTime * 0.5f`)으로 점프 후 Airborne 전환. 이동률은 무입력 감속, 방향 전환(`Dot < 0.95f`), 초과 속도 감속, 가속을 C# 8.0 switch expression/`when` guard로 표현하고 `Vector3.MoveTowards`, 수평/수직 속도 보존, maxSpeed/acceleration/deceleration/turnAcceleration 값을 유지한다. C# 8.0에 없는 relational/or pattern은 쓰지 않는다.
- [ ] T011 [P] [US1] `Assets/Tests/PlayMode/MovementAndCameraTests.cs` fixture와 시험을 생성 인스턴스 기반으로 이행한다: `AddComponent<PlayerInput>`·`SwitchCurrentControlScheme`·`ActivateInput`·`SwitchCurrentActionMap`·`PlayerNotifications` 제거, reflection으로 PlayerMovement의 `actions`를 얻어 `devices`를 테스트 Keyboard/Mouse로 제한하고 `bindingMask`를 확인하며 teardown에서 복원. `grounded` reflection을 `state` Enum 관찰로 바꾸고 8방향·대각 속도·반대 키·초점 복귀·카메라 시험의 기존 기대값은 유지한다.
- [ ] T012 [P] [US1] `Assets/Tests/PlayMode/JumpTests.cs` fixture와 시험을 생성 인스턴스 기반으로 이행한다: PlayerInput 생성·스킴·활성 호출 제거, Jump 액션 타입과 `<Keyboard>/space` 바인딩 검증 유지, `pendingJump`·`grounded` reflection을 `pendingJump`·`state`로 갱신, `MissingRequiredActionIsRejected`는 T007의 자산 계약 음성 시험으로 대체한다. 20회 점프·착지, 높이 ±5%, Space 유지·공중 입력 무점프, 짧은 누름, 초점/비활성화, 실제 SampleScene 시험은 삭제·완화하지 않는다.
- [ ] T013 [US1] `Assets/Tests/PlayMode/PlayerInputRefactorTests.cs`에 SC-001/SC-007 입력 경계를 추가한다: 물리 갱신 사이 Space 짧은 press/release가 정확히 1회 점프하고 무효 입력이 나중에 재실행되지 않음, 반대 이동 키 동시 입력에서 이동량 0이어도 중립으로 오판하지 않음, 각 사례 5회 반복. 기존 JumpTests/MovementAndCameraTests의 동등 시험은 재사용하고 중복 시험을 만들지 않는다.

**Checkpoint**: US1 시험 묶음이 통과하고, 이동·점프 결과가 기준선과 ±5% 이내로 일치한다.

---

## Phase 4: User Story 2 - 같은 규칙으로 탈락하고 반복 재시작하기 (Priority: P1)

**Goal**: 엄격한 발 위치 낙사, 탈락 중 조작 차단·안내 유지, 새 R 입력 1회 복귀, 20회 반복과 카메라·안내 정리를 유지한다.

**Independent Test**: `FallEliminationTests`, `RestartTests`, `RestartSceneTests`로 경계 위/동일/아래, 10초 차단, 낙사-R 동시 우선, 10~20회 복귀, 유지 입력, 실제 씬 20회 반복·인스턴스/카메라/안내를 확인한다. US1 시험과 독립적으로 판정 가능하다.

- [ ] T014 [US2] `Assets/Scripts/PlayerMovement.cs` FixedUpdate 맨 앞에 새 낙사 판정을 구현한다: 생존 상태에서 `FootPosition.y < fallDeathHeight`일 때만 `state = PlayerState.Eliminated`, `pendingRestart`·`pendingJump`·지지 캐시 제거 후 즉시 return하여 같은 물리 갱신에서 복귀·이동하지 않는다. 경계와 같거나 위면 탈락하지 않는 엄격 비교와 고속 통과의 첫 아래 관측 처리를 유지한다.
- [ ] T015 [US2] `Assets/Scripts/PlayerMovement.cs`의 탈락 분기를 tuple switch로 정리한다: `(PlayerState.Eliminated, pendingRestart, focused)`인 경우에만 `Restart()`, 그 외에는 `StopHorizontalMovement()` 후 return. 탈락 중 Move/Jump 요청을 무시하고 `OnGUI`의 `"ELIMINATED\nR: 재시작"` 안내를 유지하며 모든 탈락 분기가 파생 `IsEliminated`를 사용한다.
- [ ] T016 [US2] `Assets/Scripts/PlayerMovement.cs`의 `Restart()`를 갱신한다: `startPosition`/`startRotation` 복원, linear/angular velocity 0, 지지 캐시 제거, `state = PlayerState.Airborne`(grounded 강제 금지), `ResetJumpInput()`, `pendingRestart = false`, `waitingForNeutral = false`, 유지 WASD는 typed `actions.Player.Move.ReadValue<Vector2>()`로 `ClampMagnitude` 재개, 유지 Space는 재점프하지 않음, `body.WakeUp()`, `cameraFollow.SnapToTarget()`. 새 낙사 경로에서는 `pendingRestart`를 지우고 즉시 return한다.
- [ ] T017 [US2] `Assets/Scripts/PlayerMovement.cs`의 Restart performed/canceled 핸들러를 구현한다: 이미 `Eliminated`·`isActiveAndEnabled`·focused·`restartReadyAfterRelease`·새 누름에서만 `pendingRestart = true` 후 ready를 소비하고 한 번 복귀 시 요청을 소비한다. 생존 R, 탈락 전부터의 유지 R, 시간 경과, 초점 복귀만으로는 예약하지 않으며 새 낙사·초점 상실·비활성화 시 `pendingRestart`를 제거한다. 새 public 주입 API는 만들지 않는다.
- [ ] T018 [P] [US2] `Assets/Tests/PlayMode/FallEliminationTests.cs`와 `Assets/Tests/PlayMode/RestartTests.cs`를 생성 인스턴스·Enum 관찰로 이행한다: PlayerInput 생성/스킴/활성 호출 제거, `grounded` reflection을 `state`로 갱신, `MissingRestartActionIsRejected`는 자산 계약 음성 시험으로 대체. 경계 위/동일/아래, 10초 차단·비복귀, 낙사-R 동시 우선, 10회 재시작·자세/속도/실제 지지, 생존 R·유지 R, 유지 Space 무재점프 시험은 유지한다.
- [ ] T019 [P] [US2] `Assets/Tests/PlayMode/RestartSceneTests.cs`를 실제 SampleScene에 맞게 이행한다: `GetComponent<PlayerInput>()` 제거, 씬 PlayerMovement가 소유한 `actions`를 reflection으로 얻어 `devices`를 가상 Keyboard/Mouse로 제한, fixture 입력 맵 비활성화로 두 입력 경쟁 방지, `grounded`→`state` 갱신, PlayerInput 컴포넌트 부재 확인. 20회 자연 낙사에서 같은 플레이어·카메라 인스턴스, 자세·속도·지지 재획득, 유지 WASD 재개·유지 Space 무재점프, 카메라 스냅, 첫 복귀 렌더 안내 제거 검증을 유지한다.

**Checkpoint**: US1과 US2 시험 묶음이 각각 통과하고, 반복 재시작 후 중복 플레이어·카메라·안내가 0개다.

---

## Phase 5: User Story 3 - 초점 전환과 조작 재개 시 오입력 방지 (Priority: P2)

**Goal**: 초점 상실·비활성화 후에도 이전 입력이 재실행되지 않고, 실제 키 해제 확인 뒤 새 입력만 한 번 처리된다.

**Independent Test**: 생존·공중·탈락 상태별 초점 전환과 비활성화·재활성화를 5회씩, 그리고 활성화 전환 20회 후 Space/R 1회 입력이 1회만 동작하는지 확인한다. US1/US2 시험과 별도로 오입력 0회를 판정한다.

- [ ] T020 [US3] `Assets/Scripts/PlayerMovement.cs`의 `CheckForNeutralInput`을 typed 액션 기반으로 재작성한다: `actions.Player.Restart/Jump/Move.controls`의 `IsActuated()`를 순회해 실제 control 해제만 인정하고, Restart는 탈락 중에도, Jump는 생존 중에 해제를 관찰하며, `waitingForNeutral`은 Move의 모든 control이 비작동일 때만 해제한다. 값 0과 키 해제를 동일시하지 않고 `FindAction`/`InputValue`를 사용하지 않는다.
- [ ] T021 [US3] `Assets/Scripts/PlayerMovement.cs`의 초점·비활성화 생명주기를 구현한다: `OnApplicationFocus`는 `focused` 갱신 후 `waitingForNeutral = true`, jump/restart 요청·준비 초기화, `StopHorizontalMovement()`를 수행하고 진행 중 공중 물리와 탈락 상태는 덮어쓰지 않는다. `OnDisable`은 구독 해제 → `actions.Player.Disable()` → 생존 시 `Airborne`/탈락 시 `Eliminated` 유지 → 요청·지지 정리 순서를 지키며, 복귀·재활성화만으로 이동·점프·재시작을 재실행하지 않는다. `OnDestroy` Dispose는 한 번만 수행한다.
- [ ] T022 [US3] `Assets/Scripts/PlayerMovement.cs`의 활성화를 멱등하게 만든다: `actions.Player.Enable()` 전에 `bindingMask` Keyboard&Mouse를 적용하고 OnEnable마다 중복 구독이 없어야 하며, OnDisable 후 재활성화 20회에도 한 번의 유효 입력이 한 번만 처리되도록 이벤트 구독/해제와 pending 소비를 점검한다. 새 public 테스트 주입 API·상태 setter·호환 bool은 만들지 않는다.
- [ ] T023 [P] [US3] 초점·재활성화 시험을 생성 입력 기반으로 이행한다: `Assets/Tests/PlayMode/MovementAndCameraTests.cs`의 `FocusRegainRequiresReleasingKeysBeforeMoving`, `Assets/Tests/PlayMode/JumpTests.cs`의 `FocusAndDisableClearRequestsAndRequireRelease`·`ReenablingSleepingPlayerRebuildsSupport`, `Assets/Tests/PlayMode/FallEliminationTests.cs`·`Assets/Tests/PlayMode/RestartTests.cs`의 `PendingRestartIsDiscardedAcrossLifecycleChange`·`FocusReturnWithHeldRestartRequiresReleaseAndFreshPress`, `Assets/Tests/PlayMode/RestartSceneTests.cs`의 `FocusAndHeldKeysDoNotReviveOrJumpWithoutFreshInput`에서 PlayerInput 의존을 제거한다. 생존·공중 상태의 `FocusRegainRequiresReleasingKeysBeforeMoving`과 `FocusAndDisableClearRequestsAndRequireRelease`는 초점 상실·복귀 및 비활성화·재활성화를 각각 5회 반복하도록 루프를 추가하고, 탈락 상태의 기존 5회 루프(`PendingRestartIsDiscardedAcrossLifecycleChange`, `FocusReturnWithHeldRestartRequiresReleaseAndFreshPress`, `FocusAndHeldKeysDoNotReviveOrJumpWithoutFreshInput`)는 유지한다.
- [ ] T024 [P] [US3] `Assets/Tests/PlayMode/PlayerInputRefactorTests.cs`에 활성화 전환 20회 후 Space/R 한 번 입력이 한 번만 처리되는 시험과 생존·공중·탈락 상태별 초점 전환에서 의도하지 않은 이동·점프·재시작 0회 시험을 추가한다. T023에서 이행한 기존 클래스의 동등 시험과 중복되지 않게 한다.

**Checkpoint**: US1·US2·US3가 각각 독립적으로 통과하고, 재활성화 20회 후 중복 처리 0회다.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: 전체 회귀, 수동·시각 검수, 요구사항별 증거 정리, 저장소 위생.

- [ ] T025 [P] `quickstart.md` §2 정적 검색을 실행해 결과를 `TestResults/005-static-verification.txt`에 기록한다: 수기 런타임 파일(생성 산출물 `Assets/Scripts/Input/PlayerActions.cs` 제외)의 `PlayerInput|InputValue|PlayerNotifications|FindAction(` 0건, `Assets/Tests/PlayMode`의 PlayerInput 구성 호출 0건(부재 검증 타입 참조 제외), `Assets/Scenes/SampleScene.unity`의 `UnityEngine.InputSystem.PlayerInput|2083166631` 0건, `Assets/Player_Actions.inputactions` GUID 유지, 생성 `Assets/Scripts/Input/PlayerActions.cs` 수동 diff 없음, `git diff --check` 무오류.
- [ ] T026 기존 5개 클래스와 `Assets/Tests/PlayMode/PlayerInputRefactorTests.cs` 전체를 Editor Test Runner에서 실행해 `TestResults/005-after.xml`·`TestResults/005-after.log`에 기록하고 T001 기준선과 시험별로 대조한다. 시험 삭제·완화·임계값 인하 없이 새 실패 0건이어야 하며 카메라 시간 의존·초점 민감 실패와 사유를 숨기지 않는다.
- [ ] T027 실제 Editor Game 뷰에서 `specs/005-refactor-player-input-state/spec.md` SC-001~007의 수동·시각 항목을 수행하고 `TestResults/005-manual.md`에 기록한다: 8방향·정지/이동 점프 각 5회, Space 유지·공중 반복, 낙사 안내 가독성(뷰 크기·잘림), 새 R 복귀 첫 화면 안내 제거, 무입력 2초 10회, WASD/Space 유지 복귀 5회, 동일 실행 20회, 초점·비활성화 경계. 사람의 가독성·조작감 확인을 자동 픽셀 검사로 대체하지 않는다.
- [ ] T028 SC-001~007 증거를 `TestResults/005-verification.md`에 요구사항별로 정리한다: 경계 위/동일/아래 각 5회, 10초 차단 5회, 짧은 입력·반대 키 각 5회, 초점/재활성화 5회·20회, 20회 재시작 시간·중복 0, 기존 004 인수 기준 대조. 미검증 항목을 통과로 합산하지 않고, 004 T015/T017/T018을 완료로 소급하지 않는다.
- [ ] T029 [P] 최종 diff를 검토해 새 패키지·asmdef·언어 버전 변경 0, 사용자 변경 `SkyBout.slnx` 보존, `Assets/InputSystem_Actions.inputactions`와 URP/프로젝트 설정 무변경, `Assets/Scripts/Input/PlayerActions.cs` 및 폴더/파일 meta 커밋 대상 포함, `TestResults/`(gitignore) 외 임시 산출물 없음을 확인하고 논리 단위로 커밋한다.
- [ ] T030 [P] `specs/005-refactor-player-input-state/quickstart.md` §5 증거 표의 "미실행" 상태를 실제 실행 결과·근거 경로·미완료 항목으로 갱신하되, 접근할 수 없었던 사람 검수 항목은 미완료와 차단 사유를 그대로 남긴다.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 의존 없음. T001과 T002는 병렬 가능.
- **Foundational (Phase 2)**: Setup 완료 후. T003과 T004는 병렬 가능하나, T005는 둘 모두 필요. T006은 T005 이후, T007은 T003·T006 이후.
- **US1 (Phase 3)**: Foundational 완료 후. T008 → T009 → T010 순차(같은 파일). T011/T012는 T010 이후 병렬, T013은 T011/T012 이후.
- **US2 (Phase 4)**: Foundational 완료 후 착수 가능하지만 `PlayerMovement.cs`를 US1과 공유하므로 T014~T017은 US1의 T010 이후 순차 실행. T018/T019는 T017 이후 병렬.
- **US3 (Phase 5)**: T020~T022는 US2의 T017 이후 순차(같은 파일). T023/T024는 T022 이후 병렬.
- **Polish (Phase 6)**: 모든 스토리 완료 후. T025는 독립 실행 가능, T026~T028은 순차, T029/T030은 T028 이후 병렬.

### User Story Dependencies

- **US1 (P1, MVP)**: Foundational만 필요. 이동·점프 시험 묶음으로 독립 검증.
- **US2 (P1)**: Foundational만 논리적으로 필요하지만 같은 런타임 파일 순서 때문에 US1 이후 실행. US2 시험 묶음은 US1 시험과 독립적으로 판정.
- **US3 (P2)**: Foundational의 입력 소유권과 US1/US2의 요청 처리에 의존. 자체 초점·재활성화 시험으로 독립 검증.

### Within Each User Story

- 구현은 `PlayerMovement.cs`에서 순차 진행하고, 시험 이행은 구현 완료 후 진행한다.
- 기존 시험의 기대값·반복 횟수를 낮추지 않는다.
- 각 스토리 checkpoint에서 해당 시험 묶음을 실행하고 결과를 기록한다.

### Parallel Opportunities

- Setup: T001 ∥ T002
- Foundational: T003 ∥ T004
- US1: T011 ∥ T012
- US2: T018 ∥ T019
- US3: T023 ∥ T024
- Polish: T025, 그리고 T028 완료 후 T029 ∥ T030

---

## Parallel Example: User Story 1

```text
# 구현 T008~T010 완료 후 서로 다른 시험 파일을 병렬 이행:
Task: "MovementAndCameraTests를 생성 인스턴스 기반으로 이행 (T011)"
Task: "JumpTests를 생성 인스턴스 기반으로 이행 (T012)"

# 두 파일 완료 후 새 계약 시험 파일에 입력 경계 추가:
Task: "PlayerInputRefactorTests에 짧은 입력·반대 키 사례 추가 (T013)"
```

## Parallel Example: User Story 2

```text
Task: "FallEliminationTests·RestartTests를 생성 입력/Enum 관찰로 이행 (T018)"
Task: "RestartSceneTests를 실제 SampleScene 생성 입력으로 이행 (T019)"
```

## Parallel Example: User Story 3

```text
Task: "기존 클래스들의 초점·재활성화 시험 이행 (T023)"
Task: "PlayerInputRefactorTests에 20회 재활성화 단일 처리 시험 추가 (T024)"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 Setup으로 기준선과 계약 스냅샷을 남긴다.
2. Phase 2 Foundational을 완료한다(생성 클래스·단일 상태·소유권·씬 마이그레이션·계약 시험).
3. Phase 3 US1을 완료하고 `MovementAndCameraTests` + `JumpTests` + 이동/점프 경계 시험을 실행한다.
4. **STOP and VALIDATE**: 8방향·점프 높이 ±5%·무추가 점프를 기준선과 비교한다.
5. 이동·점프 리팩토링을 데모/커밋 단위로 인도한다.

### Incremental Delivery

1. Setup + Foundational → 입력 소유권 이전 기반 완료.
2. US1 → 이동·점프 회귀 통과 → MVP.
3. US2 → 낙사·재시작 회귀와 실제 씬 20회 통과 → 1차 완료.
4. US3 → 초점·재활성화 오입력 0회 → 전체 기능 완료.
5. Polish → 전체 회귀·수동 검수·증거 정리, 미검증 항목 분리.

### Parallel Team Strategy

1. Setup은 T001/T002를 병렬로 수행한다.
2. Foundational의 T003/T004를 병렬 수행한 뒤 T005~T007을 순차 진행한다.
3. 이후 `PlayerMovement.cs` 작업은 한 명이 순차 수행하고, 다른 인원은 각 스토리의 시험 파일(T011/T012, T018/T019, T023/T024)을 병렬로 이행한다.
4. 한 파일 동시 편집으로 인한 충돌을 피하기 위해 런타임 수정과 시험 수정의 담당을 분리한다.

---

## Notes

- [P]는 다른 파일을 수정하고 미완료 작업에 의존하지 않는 경우에만 사용한다. `PlayerMovement.cs`를 수정하는 T004·T005·T008~T010·T014~T017·T020~T022는 순차 실행한다.
- [Story] 라벨은 스토리 추적성과 독립 시험 범위를 나타낸다.
- 생성 `Assets/Scripts/Input/PlayerActions.cs`는 재생성 산출물이며 손으로 수정하지 않는다.
- 테스트의 `SendMessage("FixedUpdate"/"OnApplicationFocus"/"LateUpdate")`는 물리·수명 호출용이며 입력 라우팅 SendMessage 제거 대상이 아니다.
- PlayerInput 부재를 검증하는 시험의 타입 참조는 정적 검색 예외로 허용한다.
- 시험 실패·미검증·기존 004의 미완료 수동 검수를 통과로 기록하지 않는다.
- 각 작업 또는 논리 단위 후 커밋하되 사용자 변경 `SkyBout.slnx`는 포함하지 않는다.
