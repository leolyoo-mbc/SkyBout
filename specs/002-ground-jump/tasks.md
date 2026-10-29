# Tasks: 지상 고정 높이 점프와 착지

**Input**: `specs/002-ground-jump/`의 spec.md, plan.md, research.md, data-model.md, contracts/player-jump.md, quickstart.md
**Prerequisites**: 기존 Unity 프로젝트 및 1단계 이동·카메라 구현. 새 프로젝트나 패키지를 만들지 않는다.
**Tests**: 명세 SC-001~005의 반복 검증과 개발 과정 높이 측정 요구를 자동 PlayMode 시험 및 실제 플레이 작업으로 구체화한다. 테스트 작업은 각 스토리의 구현보다 먼저 배치한다. 이번 문서는 작업 생성이며 코드 작성·실행 결과가 아니다.
**Organization**: Setup → 공통 기반 → US1(P1) → US2(P2) → US3(P2) → 종합 검증.

## Format: `[ID] [P?] [Story] Description`

- 모든 작업은 미완료 체크박스와 연속 Task ID를 사용한다.
- [P]는 같은 준비 상태에서 다른 파일 작업과 병렬 수행 가능한 작업에만 사용한다. 단계 진입 전제는 생략하지 않는다.
- 사용자 스토리 단계는 [US1]/[US2]/[US3]를 붙인다. 공통 단계에는 스토리 라벨이 없다.
- 경로는 저장소 루트 `E:/projects/SkyBout` 기준이다. 새 Unity 파일은 대응 `.meta`도 관리한다.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 기존 프로젝트의 안전한 증분 작업과 검증 기반을 준비한다.

- [X] T001 `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `Assets/Scenes/SampleScene.unity`, `ProjectSettings/EditorBuildSettings.asset`, `SkyBout.slnx`의 현재 상태와 기존 변경을 확인하고 `specs/002-ground-jump/quickstart.md`에 실행 환경·기준 상태를 기록한다. Unity 6000.3.26f1 프로젝트를 재사용하고 패키지 추가나 사용자 변경 초기화를 하지 않는다.
- [X] T002 `Assets/Tests/PlayMode/MovementAndCameraTests.cs`를 기존 Test Runner에서 실행해 기준 결과를 `specs/002-ground-jump/quickstart.md`에 기록한다. 실행 환경이 없으면 미실행과 사유를 남기고 통과로 표시하지 않는다.

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 세 스토리에 공통인 테스트 픽스처와 지지 판정을 마련한다. 이 단계 완료 전 스토리 구현을 시작하지 않는다.

- [X] T003 `Assets/Tests/PlayMode/JumpTests.cs`와 `Assets/Tests/PlayMode/JumpTests.cs.meta`에 기존 실제 입력 자산 복제, 가상 Keyboard, 동적 캡슐·Ground·발 위치 구성, 입력 업데이트와 물리 스텝 분리, 정착 대기 및 높이 기록 기능을 갖춘 공통 픽스처를 만든다. Space 단독 입력에 Move 벡터를 요구하지 않으며 입력/Physics 모드·기기·씬을 teardown에서 복원한다. 기존 `Assets/Tests/PlayMode/SkyBout.PlayModeTests.asmdef`를 재사용한다.
- [X] T004 `Assets/Tests/PlayMode/JumpTests.cs`에 평지 지지, 가장자리 이탈, 옆면·트리거 접촉, 발보다 먼 표면, 상승 중 잔류 접촉을 구분하는 접지 시험을 추가한다. 실제 물리 정착 전 접지로 간주하지 않으며, 동일 높이만으로 허공 착지를 허용하지 않는지 검증한다.
- [X] T005 `Assets/Scripts/PlayerMovement.cs`에서 CheckSphere 단독 접지를 재사용 접촉 버퍼와 현재 발아래 짧은 쿼리의 조합으로 교체한다. Unity 3D 지원 API인 `Collision.GetContacts(contactBuffer)`를 OnCollisionEnter/Stay에서 사용하고 OnCollisionExit에서 지지 집합을 갱신하며 마스크·위쪽 법선·발 부근 접점·접촉 허용 거리·상승 배제를 검사한다. 모델 제약 “Ground 포함, 자기 레이어 제외”, “기존 발 위치 자식 참조 유지”, “위쪽 법선 기준, 발 부근 거리와 작은 접촉 허용 오차. 일반 사용자 튜닝 항목으로 확대하지 않음”, “고정 크기 재사용 ContactPoint 배열”을 지키고 포화 진단을 마련한다. `Assets/Tests/PlayMode/MovementAndCameraTests.cs`의 지상 시작 시험에 실제 정착 스텝을 먼저 추가하고 T004 및 기존 접지 시험을 실행해 결과를 확인한다.

**Checkpoint**: 실제 지지와 단순 근접을 구분하며 테스트 픽스처가 반복 실행 후 전역 상태를 복원한다.

## Phase 3: User Story 1 — 지상에서 점프하고 다시 착지 (Priority: P1) — MVP

**Goal**: 지상 Space 누름으로 정해진 높이까지 점프하고 착지 후 새 입력으로 반복할 수 있다.
**Independent Test**: 섬 중앙에서 20회 연속 점프·착지·재점프를 완료한다. 높이 2m/4m, 짧은 누름·긴 누름·정지·이동 각각 5회에서 정점 오차 ±5%를 확인한다. 이동 조건은 기존 수평 이동으로 실행하고 공중 감속 세부 검증은 US3에서 수행한다.

### Tests for User Story 1

- [X] T006 [P] [US1] `Assets/Tests/PlayMode/JumpTests.cs`에 `contracts/player-jump.md`의 Player/Jump Button·Space 바인딩 계약, 실제 자산 입력 경유, 중앙 20회 상승·하강·착지·재점프 및 2m/4m 조건별 5회 높이 측정 시험을 추가한다. 발의 정착 높이 대비 정점을 측정해 ±5%와 관통·공중 정지 0회를 판정한다. 구현 전 실패가 기능 부재 때문인지 확인한다.

### Implementation for User Story 1

- [X] T007 [P] [US1] `Assets/Player_Actions.inputactions`에 `Player/Jump` Button(expectedControlType=Button), `<Keyboard>/space`, `Keyboard&Mouse` 그룹을 추가한다. 기존 Player 맵·Move 액션·바인딩의 ID와 GUID를 보존하고 홀드·반복 인터랙션을 넣지 않는다.
- [X] T008 [US1] `Assets/Scripts/PlayerMovement.cs`에 직렬화 jumpHeight와 점프 런타임 필드를 추가한다. 모델 제약 “유한한 양수. 초기 제안 2, 검증값 2/4. 이륙 시 지면 대비 발의 수직 상승량”, “현재 유효한 발밑 지지 접촉. 단순 높이나 근접 여부가 아님”, “Grounded, Rising, Falling. grounded와 수직 속도로 도출해 중복 상태 불일치 방지”를 반영한다. Player/Move·Player/Jump 누락 및 Rigidbody·중력 설정의 진단을 추가한다.
- [X] T009 [US1] `Assets/Scripts/PlayerMovement.cs`에 `OnJump(InputValue)` 누름·해제 수신과 1회 요청 소비를 구현한다. 제약 “지상에서 유효하게 받은 누름 1회. 다음 FixedUpdate에서 무조건 소진”을 지키고 입력 수신 시·소비 시 현재 지지를 재검사한다. 유효 누름 직후 해제해도 요청은 보존하되 공중 누름은 접수하지 않고 이륙 틱에는 접지를 해제한다.
- [X] T010 [US1] `Assets/Scripts/PlayerMovement.cs`에서 `sqrt(2 * abs(Physics.gravity.y) * jumpHeight)` 기반 수직 초기 속도를 한 번 적용하고 XZ를 보존한다. “useGravity=true, isKinematic=false, linearDamping=0 및 Y 이동 허용”을 검증하고 중력·충돌은 PhysX에 맡긴다. 실제 높이 오차가 ±5%를 넘으면 고정 간격 보정을 적용해 재측정하되 정점/착지 좌표를 강제 고정하지 않는다.
- [X] T011 [US1] `Assets/Scenes/SampleScene.unity`의 기존 Player에 jumpHeight=2를 직렬화하고 기존 PlayerInput SendMessages·Player 맵·입력 자산·발 참조·Ground 마스크를 유지한다. 스크립트 GUID와 안전한 시작 위치를 보존하고 Y 이동 잠금이나 별도 점프 오브젝트를 추가하지 않는다.
- [X] T012 [US1] `Assets/Tests/PlayMode/JumpTests.cs`에 잘못된 높이(0/음수/NaN/무한대), 중력·damping·키네마틱·Y 잠금, 필수 액션/참조/마스크 누락의 진단 시험을 추가하고 `Assets/Scripts/PlayerMovement.cs`의 검증과 대조한다. 올바른 설정은 활성 상태이고 잘못된 설정은 오류 후 기능을 실행하지 않아야 한다.
- [X] T013 [US1] `Assets/Tests/PlayMode/JumpTests.cs`의 US1 시험을 실행하고 중앙 20회 결과, 각 높이·입력 조건의 측정값과 허용 오차를 `specs/002-ground-jump/quickstart.md`에 기록한다. T006의 이동 표본은 필요 시 충분히 넓은 테스트 지형에서 실행하며 실패 또는 미실행을 통과로 처리하지 않는다.

**Checkpoint**: 기본 점프·착지·높이 조정이 기존 씬에서 동작하고 SC-001~002의 증거가 있다. 이 시점이 권장 MVP다.

## Phase 4: User Story 2 — 공중 입력으로 점프가 중복되지 않음 (Priority: P2)

**Goal**: 홀드·공중 반복 누름·착지 직전 입력·초점 전환으로 추가/예약 점프가 발생하지 않는다.
**Independent Test**: 상승·정점·하강 재입력, 착지 뒤까지 홀드, 착지 직전 누름을 각각 5회 실행해 추가/자동 점프 0회를 확인한다. 착지 후 해제와 새 누름은 1회 점프해야 한다.

### Tests for User Story 2

- [X] T014 [US2] `Assets/Tests/PlayMode/JumpTests.cs`에 상승·정점·하강 재입력, 착지 후까지 홀드, 착지 직전 누름 각각 5회와 착지 후 해제·새 누름 시험을 추가한다. 같은 입력 업데이트의 짧은 누름/해제, 한 입력 뒤 여러 FixedUpdate, 요청 후 지지 상실도 포함해 추가 상승·입력 예약·중복 소비를 검증한다.
- [X] T015 [US2] `Assets/Tests/PlayMode/JumpTests.cs`에 활성화 시 Space 홀드, 초점 상실 전 대기 요청, 공중 초점 상실/복귀, 비활성화/재활성화 시험을 추가한다. 복귀 홀드로 점프하지 않으며 해제 후 새 누름만 허용되고 초점 상실 시 Y는 유지되는지 검사한다.

### Implementation for User Story 2

- [X] T016 [US2] `Assets/Scripts/PlayerMovement.cs`에서 jumpHeld와 jumpReadyAfterRelease로 누름 전이·해제 게이트를 완성한다. 모델 제약 “눌림 전이를 구분하는 현재 입력 상태”, “활성화/초점 복귀 후 해제 확인 여부”를 지키고 Move 중립 게이트와 분리한다. 지속 홀드·공중 재입력이 pendingJump를 만들거나 착지 후 부활시키지 않도록 한다.
- [X] T017 [US2] `Assets/Scripts/PlayerMovement.cs`의 OnEnable/OnDisable/OnApplicationFocus와 입력 동기화 처리에서 pendingJump를 제거하고 해제 게이트를 재설정한다. “pending 제거, 수평 정지, 점프 해제 게이트 잠금; 물리 Y는 유지”를 지키고 기존 Move 중립 처리를 보존한다.
- [X] T018 [P] [US2] `specs/002-ground-jump/contracts/player-jump.md`에 T014~T015의 입력·물리 스케줄 경계와 기대 결과의 추적표를 추가한다. 단발 요청은 다음 틱까지만 유효하고 공중 누름은 예약되지 않음을 명시하며 기존 계약을 확장 기능으로 변경하지 않는다.
- [X] T019 [US2] `Assets/Tests/PlayMode/JumpTests.cs`의 US1·US2 시험을 함께 실행하고 SC-003, SC-005의 초점/허공 입력 결과를 `specs/002-ground-jump/quickstart.md`에 기록한다. 새 글라이더/낙사 기능이 호출되지 않는지도 확인한다.

**Checkpoint**: 단발 점프가 입력 경계와 초점 전환에도 유지된다. US1 회귀도 통과해야 한다.

## Phase 5: User Story 3 — 이동과 탑뷰 시점을 유지하며 점프 (Priority: P2)

**Goal**: 기존 화면 기준 WASD, 가속·감속·방향 전환과 탑뷰 추적을 점프 중에도 유지한다.
**Independent Test**: 네 방향·네 대각선 총 8방향 이동 점프와 공중 방향 변경/해제를 확인하고, 상승부터 착지까지 캐릭터 전체와 주변 표면이 보이며 커서가 카메라에 영향을 주지 않는지 검증한다.

### Tests for User Story 3

- [X] T020 [US3] `Assets/Tests/PlayMode/JumpTests.cs`에 8방향 이동 점프, 대각선 속도 제한, 공중 방향 변경·감속, 수평 갱신 시 Y 보존, 착지 후 이동 시험을 추가한다. 모델 제약 “maxSpeed/acceleration/deceleration/turnAcceleration 유지. 지상·공중 공통”을 기대값에 반영한다.
- [X] T021 [P] [US3] `Assets/Tests/PlayMode/MovementAndCameraTests.cs`에 실제 점프의 상승·정점·하강·착지 구간에서 카메라 추적, 캐릭터 전체/주변 표면 가시성, 정지·이동 중 커서 무관 동작을 검증하는 시험을 추가한다. 실제 Rigidbody 점프를 사용하고 상태별 화면 투영을 검사한다.

### Implementation for User Story 3

- [X] T022 [US3] `Assets/Scripts/PlayerMovement.cs`에서 airControl 경로와 공중 무입력 조기 반환을 제거하고 지상과 공중의 수평 가속·감속·방향 전환 계산을 통일한다. `Assets/Scenes/SampleScene.unity`의 airControl 직렬화 값을 정리하며 기존 maxSpeed 등 지상 수치는 변경하지 않고 수직 속도를 보존한다.
- [X] T023 [US3] `Assets/Tests/PlayMode/MovementAndCameraTests.cs`의 기존 0.3배 공중 가속 기대값을 공통 설정으로 갱신한다. 새로운 접지 검사에 필요한 실제 물리 정착 스텝을 추가하되 지상 8방향·반대 입력·중립 복귀·중력 보존 검증은 유지한다.
- [X] T024 [US3] `Assets/Scripts/TopDownCameraFollow.cs`와 `Assets/Scenes/SampleScene.unity`의 기존 추적을 2m/4m 점프에 대해 확인한다. T021 또는 수동 검증에서 가시성이 실패한 경우에만 최소 거리/시야 설정을 조정하고 기존 캐릭터 단독 추적·커서 무관 규칙을 보존한다. 모두 통과하면 변경 없이 결과만 남긴다.
- [X] T025 [US3] `Assets/Tests/PlayMode/JumpTests.cs`, `Assets/Tests/PlayMode/MovementAndCameraTests.cs`를 함께 실행하고 실제 씬에서 8방향·공중 변경/해제·가시성·착지 후 이동을 플레이해 SC-004 결과를 `specs/002-ground-jump/quickstart.md`에 기록한다.

**Checkpoint**: 세 스토리가 동작하며 기존 지상 이동·카메라 동작의 의도하지 않은 회귀가 없다.

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: 실제 씬 생명주기, 자산 무결성 및 명세 전체 기능 완료 증거를 확인한다. FPS·Profiler·메모리 할당 성능 검증은 제외한다.

- [X] T026 `Assets/Tests/PlayMode/JumpTests.cs`에 실제 `Assets/Scenes/SampleScene.unity`를 로드하고 자동 Update/FixedUpdate/LateUpdate로 동작하는 통합 시험을 추가한다. 실제 자산과 PlayerInput을 통해 점프·착지·재로드 후 안전한 지상 시작·가장자리 하강·옆면 비접지·초점 전환을 확인하며 실패 시 물리/입력 전역 설정과 원래 씬도 복원한다. 수동 메서드 호출 시험으로 이 통합 시험을 대체하지 않는다.
- [X] T027 [P] `Assets/Player_Actions.inputactions`, `Assets/Scenes/SampleScene.unity`, `Assets/Scripts/SkyBout.Runtime.asmdef`, `Assets/Tests/PlayMode/SkyBout.PlayModeTests.asmdef`, 신규 `Assets/Tests/PlayMode/JumpTests.cs.meta`의 GUID·참조·컴포넌트 연결과 컴파일을 점검한다. `ProjectSettings/EditorBuildSettings.asset`, `SkyBout.slnx`, `Packages/manifest.json`에 의도하지 않은 변경이 없는지 기준 상태와 비교한다.
- [X] T028 `specs/002-ground-jump/quickstart.md`의 전체 검증을 수행해 SC-001~005별 반복 횟수·높이 오차·추가 점프 횟수·실제 씬 결과를 최종 정리한다. `specs/002-ground-jump/tasks.md`는 증거가 있는 작업만 완료 처리하고 실패/미실행·잔여 위험을 명시한다. 글라이더·낙사 복귀·온라인 기능은 범위에 추가하지 않는다.

## Dependencies & Execution Order

### Phase Dependencies

```text
Setup (T001→T002)
  → Foundational (T003→T004→T005)
    → US1 (T006 || T007 → T008→T009→T010→T011→T012→T013)
      ├→ US2 (T014→T015 → T016→T017 → T019; T018 병렬 문서 작업)
      └→ US3 (T020 || T021 → T022→T023→T024→T025)
          → Polish (T026 || T027 → T028)
```

- 기본 실행 순서는 T001~T028이다. 높은 ID를 먼저 시작하려면 위 단계 전제와 아래 파일 소유권 조건을 모두 충족해야 한다.
- US1은 공통 기반만 필요하다. US2/US3은 실제 점프가 있어야 검증할 수 있으므로 US1에 의존한다. 서로의 기능 결과에는 의존하지 않지만 PlayerMovement.cs와 테스트 파일을 공유하므로 기본 구현 순서는 US2→US3이다.
- US3 테스트 초안은 US1 완료 후 US2와 병행 가능하나 같은 JumpTests.cs를 동시에 편집하지 않는다. 병렬화가 불편하면 완전히 순차 진행한다.
- T006과 T007은 공통 기반 완료 후 병렬 가능하나 T006의 실패 원인 확인은 T007 병합 후에도 수행한다.
- T008~T012는 자산·수신부·물리·씬 연결 순서로 수행한다. 필수 액션 검증을 추가할 때 기존 테스트도 실제 자산을 사용하도록 유지한다.
- T019는 T014~T018 완료 후, T025는 T020~T024 완료 후 수행한다.
- T026/T027은 세 스토리 완료 후 병렬 가능하다. T028은 두 작업 완료 후 모든 기능 검증 결과를 종합한다.

### Parallel Opportunities & Examples per Story

**US1** — 기반 완료 후 서로 다른 파일:
```text
T006: Assets/Tests/PlayMode/JumpTests.cs에 US1 시험 작성
T007: Assets/Player_Actions.inputactions에 Jump 액션 추가
```
시험이 실제 기능 부재로 실패하는지 확인한 뒤 런타임 구현으로 진행한다.

**US2** — T014~T015 작성 후 서로 다른 파일:
```text
T016→T017: Assets/Scripts/PlayerMovement.cs의 입력 전이·초점 처리
T018: specs/002-ground-jump/contracts/player-jump.md의 입력 경계 추적표
```
T018은 기존 계약과 작성된 시험만 참조하며 구현 완료에 의존하지 않는다.

**US3** — US1과 공통 픽스처 준비 후 서로 다른 파일:
```text
T020: Assets/Tests/PlayMode/JumpTests.cs의 공중 수평 조작 시험
T021: Assets/Tests/PlayMode/MovementAndCameraTests.cs의 점프 카메라 시험
```
공통 입력/접지 계약을 고정한 뒤 작성한다. T021과 T023은 같은 파일이므로 병렬 수행하지 않는다.

**Cross-cutting** — 세 스토리 완료 후 T026의 통합 시험 작성과 T027의 자산·참조 점검을 병렬 수행할 수 있다. 같은 Unity 인스턴스에서 테스트·플레이·자산 편집을 동시에 실행하지 않는다.

## Requirement Coverage

| 요구/기준 | 주 작업 | 확인 작업 |
| --- | --- | --- |
| FR-001~003, FR-009 / SC-001~002 | T007~T011 | T006, T012~T013 |
| FR-004, FR-010 / SC-001, SC-005 | T005, T010 | T004, T006, T026 |
| FR-005~006 / SC-003 | T009, T016 | T014, T019 |
| FR-007 / SC-004 | T022 | T020, T023, T025 |
| FR-008 / SC-004 | T024(필요 시에만 변경) | T021, T025 |
| FR-011 / SC-005 | T017, 기존 씬 시작 위치 유지 | T015, T026 |
| 회귀·실제 씬 무결성 | 기존 구조 보존 | T002, T026~T028 |

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Setup 및 Foundational을 완료한다.
2. US1 입력 자산·높이 설정·점프·착지를 구현한다.
3. T013에서 정지/이동 점프의 정점 및 착지를 검증하고 기본 데모를 확인한다.
4. US2의 경계 보호와 US3의 공중 조작 통일은 전체 기능 완료에 필요하므로 MVP 시연과 전체 명세 완료를 구분한다.

### Incremental Delivery

- 공통 접지 기반 → US1 점프/착지 → US2 입력 경계 → US3 이동/카메라 → 전체 실제 씬 검증 순으로 진행한다.
- 각 스토리의 시험은 해당 스토리만 선택 실행할 수 있어야 하며 후속 스토리 코드가 없다는 이유로 기본 완료 검증을 막지 않는다.
- 테스트 추가는 먼저 수행하고 관련 구현 후 결과를 확인한다. 환경 부재 시 시험 코드는 작성할 수 있으나 실행 통과로 기록하지 않는다.
- 기존 사용자 변경, 자산 ID, 참조를 보존한다. 정당한 설계 변경이 필요하면 research.md/계약/모델을 갱신한 뒤 진행한다.

## Notes

- 총 28개: Setup 2, Foundational 3, US1 8, US2 6, US3 6, Polish 3.
- [P] 5개: T006, T007, T018, T021, T027. 병렬 예시는 전제와 파일 충돌 제한을 따른다.
- .specify/memory/constitution.md는 미작성 템플릿이다. 확정되지 않은 원칙을 신규 의무로 만들지 않는다.
- 테스트 전용 공개 게임 API, 별도 점프 컴포넌트, 새 UI·패키지·프로젝트·네트워크 계층은 추가하지 않는다.
