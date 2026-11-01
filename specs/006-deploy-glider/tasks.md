---
description: "기존 Unity 프로젝트를 확장하는 5단계 글라이더 구현 작업"
---

# Tasks: 공중 글라이더 전개와 이동

**Input**: `specs/006-deploy-glider/`의 [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/player-glider.md](contracts/player-glider.md), [quickstart.md](quickstart.md).

**Prerequisites**: Unity 6000.3.26f1의 현재 프로젝트 `E:\projects\SkyBout`. 기존 `Assets/Scenes/SampleScene.unity`를 확장하며 새 프로젝트·패키지·입력 액션·네트워크 기능은 만들지 않는다. 헌법은 미작성 템플릿이므로 예시를 정책으로 적용하지 않는다.

**Tests**: 명세의 User Scenarios & Testing, SC-001~008 및 기존 기준 보존 요구를 실행 가능한 자동·씬·수동 검증으로 분해했다. 테스트를 구현보다 먼저 작성하고 해당 동작이 없어서 실패하는지 확인한다. 컴파일 오류나 시험 0개 수집은 유효한 실패 증거가 아니다. 실행 불가 항목은 미검증으로 남긴다.

**Organization**: 공통 준비 후 US1(P1), US2(P1), US3(P2) 순서로 기능을 누적한다. 2026-11-01 구현·자동 검증을 수행하여 30/31개 작업을 완료했다. T030 사람의 직접 조작감·작은 화면 검수만 미완료다. 상세 결과는 [quickstart.md](quickstart.md) 및 `TestResults/006-verification.md`에 기록했다.

## Format: `[ID] [P?] [Story] Description`

- `[P]`는 명시된 선행 작업 완료 후 같은 단계에서 다른 파일과 병렬 작성할 수 있음을 뜻한다.
- `[US1]`~`[US3]`은 명세의 사용자 스토리다. 공통 준비·최종 검증에는 붙이지 않는다.
- 경로는 저장소 루트 기준이다. `TestResults/`의 실행 증거는 gitignore 대상이며 완료 판정에 경로를 연결한다.
- Unity 씬 편집·임포트·Test Runner 실행은 동일 Editor에서 직렬 수행한다. 파일 병렬 작성과 Unity 동시 실행을 혼동하지 않는다.

## Phase 1: Setup — 현재 프로젝트와 기준선

**Purpose**: 새 프로젝트를 생성하지 않고 현재 코드·씬·시험의 기준선을 확보한다.

- [X] T001 `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `Assets/Scenes/SampleScene.unity`와 `specs/006-deploy-glider/checklists/requirements.md`를 확인하고 실제 Git 상태·Unity 버전·사용자 변경·Console 상태를 `TestResults/006-environment.md`에 기록한다. 기존 변경을 초기화하거나 패키지를 업그레이드하지 않는다.
- [X] T002 `Assets/Tests/PlayMode/`의 기존 전체 시험을 Editor Test Runner로 실행하여 실제 수집 목록·통과/실패/건너뜀·실행 시각을 `TestResults/006-baseline.xml`과 `TestResults/006-baseline.log`에 저장한다. 과거 005 결과를 기준선으로 복사하지 않는다.
- [X] T003 `Assets/Scenes/SampleScene.unity`, `Assets/Player_Actions.inputactions`, `Assets/Scripts/Input/PlayerInputActions.cs`의 설정·GUID·입력 ID·발/카메라 참조·중력/물리 간격을 `TestResults/006-preservation.md`에 기록하여 변경 후 비교 기준을 만든다. 실제 씬에 남은 이전 직렬화 필드명과 FormerlySerializedAs 연결도 확인한다.

---

## Phase 2: Foundational — 검증 기반과 변경 경계

**Purpose**: 모든 스토리에서 재사용할 안전한 시험 환경과 회귀 변경 기준을 준비한다. T001~T003 완료 후 수행한다.

- [X] T004 `Assets/Tests/PlayMode/GliderTests.cs`에 기존 패턴의 비충돌 시험 위치·Ground 바닥·발 자식·동적 Rigidbody·가상 Keyboard fixture와 입력/물리 스텝 도우미를 준비한다. actions.devices를 시험 장치로 제한하고 InputSystem/Physics/시간 설정과 장치를 teardown에서 복원한다. 아직 없는 IsGliding·glideRequested·glideDescentSpeed·gliderVisual은 이름으로 reflection 조회하고 멤버가 없으면 명시적인 NUnit 단언 실패를 내는 도우미를 둔다. 새 API를 직접 참조해 컴파일을 막거나 멤버 누락을 건너뜀/통과로 처리하지 않는다. `Assets/Tests/PlayMode/GliderTests.cs.meta`는 Unity로 생성하고 기존 asmdef를 사용한다.
- [X] T005 `Assets/Tests/PlayMode/FallEliminationTests.cs`, `Assets/Tests/PlayMode/JumpTests.cs`, `Assets/Tests/PlayMode/RestartTests.cs`에서 공중 Space 무동작·복귀 직후 접촉 재구축·고정 착지 대기와 충돌하는 기대를 찾아 `TestResults/006-regression-map.md`에 FR별로 기록한다. 추가 점프 금지·키 유지·비전개 물리 기준은 보존하며 변경 허용 범위를 research.md의 Decision 5와 대조한다.

**Checkpoint**: 기준선·보존 항목·fixture가 준비되어야 US1을 시작한다. 공통 준비에서 글라이더 전체를 미리 구현하지 않는다.

---

## Phase 3: User Story 1 — 공중에서 글라이더 펼치기 (Priority: P1) — MVP

**Goal**: 새 공중 Space로 한 번 전개하고, 기존 탑뷰에서 표시를 확인하며 일반 추락보다 천천히 내려간다.

**Independent Test**: 상승·정점·하강·걷기 이탈에서 각 5회 전개, 0.2초 이내 관측. 점프 키 유지·전개 후 반복 입력은 각 5회 오동작 0. 동일 발 높이 2→-4 m 하강 비교 5쌍 모두 글라이딩이 느려야 한다(SC-001~002).

### Tests for User Story 1

- [X] T006 [US1] `Assets/Tests/PlayMode/GliderTests.cs`에 4종 공중 전개 각 5회, 지상 점프 키 유지·전개 중 재누름 각 5회, 짧은 누름·해제와 한 틱 다중 입력의 단발 소비를 검증한다. 입력 시 점프 수락 후 지지 상실 및 전개 수락 후 착지는 다른 동작으로 재해석하지 않는 시험을 추가한다. 높이 2→-4 m 하강 비교 5쌍, 스텝 후 속도 ±0.05 m/s, 전개 즉시 위치 차이 0.001 m 이내 및 0·음수·NaN·Infinity 설정 거부 시험도 여기서 먼저 작성한다. T004의 관측 도우미로 컴파일·시험 수집을 확인한 뒤 아직 없는 멤버/동작으로 단언이 실패하는 결과를 `TestResults/006-us1-red.md`에 남기고 T008~T010을 시작한다. 기존 생성 입력 계약을 변경하지 않는다.
- [X] T007 [P] [US1] `Assets/Tests/PlayMode/GliderSceneTests.cs`에 실제 `Assets/Scenes/SampleScene.unity`의 Player·표시 자식 참조·Collider/Rigidbody 부재·전개 전 비표시·새 공중 입력 후 0.2초 이내 렌더 표시를 검증하는 시험을 작성하고 해당 `.cs.meta`를 Unity로 생성한다. 이 파일 자체에 T004와 같은 reflection·명시적 단언 실패 방식의 관측 도우미를 두어 T006 편집에 의존하지 않으며, 새 멤버나 씬 표시가 없는 상태의 기대 실패를 `TestResults/006-us1-scene-red.md`에 기록한다. 기존 씬이 이미 열렸으면 무단으로 닫지 않고 시험이 연 씬·임시 렌더 자원만 정리한다.

### Implementation for User Story 1

- [X] T008 [US1] `Assets/Scripts/PlayerMovement.cs`에 Gliding enum 값, IsGliding, glideRequested, glideDescentSpeed, gliderVisual을 추가한다. data-model.md 제약인 state의 “상호 배타적. Gliding은 생존 공중만 의미”, IsGliding의 “state == Gliding; 시각과 테스트의 관측값”, 속도의 “기본 2 m/s, 유한하고 0보다 큼”, 표시의 “실제 씬에서 지정. Player 자손, 비충돌·비물리 표시 전용”을 지킨다. 기존 IsEliminated는 “기존 계약 유지”, startingPosition/startingRotation은 “복귀 기준 변경 없음”으로 유지한다.
- [X] T009 [US1] `Assets/Scripts/PlayerMovement.cs`에서 기존 JumpPressed 구독을 실제 지지에 따른 점프/전개 의도 분기로 확장한다. jumpRequested의 “입력 시 지상에서만 설정, 다음 물리 틱에 소비”, glideRequested의 “입력 시 비전개 공중에서만 설정, 다음 물리 틱에 소비”를 지키고 한 누름·한 틱에는 첫 유효 Space 의도만 저장한다. 실행 시 재검증 실패는 폐기하고 이미 Gliding인 Space는 무시한다. ResetRequests는 새 요청도 지운다.
- [X] T010 [US1] T006에서 작성한 하강·위치 보존·설정 거부 시험의 기대 실패를 확인한 뒤 `Assets/Scripts/PlayerMovement.cs`에 전개 시 중력 해제와 Y=-glideDescentSpeed의 일정 하강을 구현하고 CanMove 조기 반환 전에 수직 제어를 배치한다. 위치는 변경하지 않고 XZ 계산은 기존 MovementMath를 유지한다. ValidateSettings에 0·음수·NaN·Infinity 거부와 상태별 useGravity 검증을 추가한다. 구현 후 `Assets/Tests/PlayMode/GliderTests.cs`의 해당 시험을 재실행하여 실패→통과를 확인한다. 이 작업에서 처음 테스트를 작성하지 않는다.
- [X] T011 [US1] `Assets/Scripts/PlayerMovement.cs`에 상태 종속 null 안전 표시 제어를 연결한다. UpdateGroundedState의 비지지 Gliding을 보존하고, 기존 착지·탈락·재시작 경로에서 표시 해제·기본 중력 복원을 연결해 MVP가 기존 플레이를 파괴하지 않게 한다. OnDisable은 이미 전개된 상태·Y속도를 지우지 않고, OnDestroy는 남은 Rigidbody 중력을 복원한다. 자세한 경계 검증은 US2·US3에서 보강한다.
- [X] T012 [US1] `Assets/Scenes/SampleScene.unity`의 기존 Player에 비활성 날개 모양 시각 자식을 추가하고 gliderVisual 및 속도 2를 연결한다. `Assets/Materials/Glider.mat`과 `.meta`를 URP 호환의 식별 가능한 재질로 생성한다. 날개에는 물리 컴포넌트를 두지 않고 기존 Player·섬·카메라·발·레이어·GUID·조작 설정을 T003 기준으로 보존한다.
- [X] T013 [US1] `Assets/Tests/PlayMode/FallEliminationTests.cs`의 SpaceOutsideIslandNeverAddsJumpOrGliderFiveTimes 등 공중 Space 무동작 기대만 전개·추가 점프 금지·완만 하강 기대로 갱신한다. 반복 횟수와 기존 무전개 추락·낙사 단언을 유지하고 변경 근거를 `TestResults/006-regression-map.md`에 추가한다.
- [X] T014 [US1] `Assets/Tests/PlayMode/GliderTests.cs`와 `Assets/Tests/PlayMode/GliderSceneTests.cs`의 US1 시험, T013에서 갱신한 FallEliminationTests 및 T005에서 영향받은 것으로 식별한 기존 시험을 실행하고 `TestResults/006-us1.md`에 SC-001~002 결과·하강 5쌍 표·전개 지연·스크린샷 경로를 기록한다. US1 시험과 갱신한 FallEliminationTests는 통과해야 한다. JumpTests·RestartTests 중 T019·T026에서 바꿀 과거 공중 Space 기대와 직접 충돌하는 실패만 시험명·실패 단언·FR 근거·해소 작업 ID를 `TestResults/006-regression-map.md`에 연결해 알려진 기대 불일치로 별도 기록할 수 있다. 이를 통과로 집계하지 않으며 미등록 실패·새 비전개 회귀는 단계 완료를 막는다. T019/T026에서 해당 불일치를 해소하고 T028에서는 예외 없이 전체 결과를 판정한다. 상태 조회만으로 렌더 확인을 대신하지 않는다.

**Checkpoint**: T014의 필수 통과 항목을 충족하고 미해소된 과거 기대 불일치를 별도 공개하면 전개·하강·표시 MVP를 데모할 수 있다. 이것은 기존 전체 회귀 통과를 뜻하지 않는다. 이동·착지 및 반복 안정성의 전체 기능 완료는 이후 스토리 검증을 요구한다.

---

## Phase 4: User Story 2 — 글라이딩 방향 조작과 착지 (Priority: P1)

**Goal**: 기존 WASD 수평 조작을 유지하고 실제 섬 표면에 착지하면 전개를 종료한다.

**Independent Test**: 8방향·방향 전환·입력 해제를 각 5회 시험하고, 섬 위 전개→착지→새 점프 10회와 옆면 접촉 5회를 확인한다. 착지 첫 렌더에 날개가 남지 않아야 한다(SC-003~004).

### Tests for User Story 2

- [X] T015 [US2] `Assets/Tests/PlayMode/GliderTests.cs`에 글라이딩 8방향·방향 전환·해제 각 5회, 대각선 최고 속도 제한, 섬 위 착지→새 점프 10회, 옆면 5회와 trigger/근접 지면 비착지 시험을 추가한다. 기존 MovementMath와 동일한 가속·감속·Y 보존 기준을 적용하고 글라이딩 중 Space 입력을 착지까지 유지해도 자동 점프가 없는지 각 5회 확인한다.
- [X] T016 [P] [US2] `Assets/Tests/PlayMode/GliderSceneTests.cs`에 실제 씬 착지 첫 렌더의 표시 제거·중력 복원·새 지상 점프와 탑뷰 추적 검증을 추가한다. 기존 카메라를 변경하지 않고 화면 안 플레이어·전개 여부 식별을 확인한다.

### Implementation for User Story 2

- [X] T017 [US2] `Assets/Scripts/PlayerMovement.cs`의 UpdateGroundContacts와 접지 갱신을 정리해 실제 접촉 수집 후 Gliding의 지지를 확인하면 즉시 Grounded로 전환하고 표시·전개 요청을 제거하며 중력을 복원한다. FixedUpdate에서도 재확인하고 옆면·출발 높이만으로 종료하지 않는다. `Assets/Scripts/GroundSupport.cs`의 기존 접촉+Raycast 판정은 재사용한다.
- [X] T018 [US2] `Assets/Scripts/PlayerMovement.cs`에서 Gliding에도 기존 UpdateHorizontalVelocity와 키 해제 감속을 적용하고, 이동 차단 시 XZ만 멈추며 일정 하강은 유지되도록 통합한다. `Assets/Scripts/MovementMath.cs`와 `Assets/Scripts/TopDownCameraFollow.cs`는 변경하지 않고 T015·T016으로 동일 조작 계약을 검증한다.
- [X] T019 [US2] `Assets/Tests/PlayMode/JumpTests.cs`의 AirAndHeldInputNeverAutoJump 및 공중 Space 관련 단언을 전개 상태·일정 하강 검증으로 보강한다. 전개된 경우만 높이·하강 속도 기반 유한 착지 대기를 사용하고 기존 비전개 점프 높이·지상 수락 후 지지 상실·자동 재점프 금지·반복 횟수를 완화하지 않는다. T014에서 등록한 JumpTests 기대 불일치가 모두 해소되었는지 재실행하여 근거를 `TestResults/006-regression-map.md`에 기록한다.
- [X] T020 [US2] `Assets/Tests/PlayMode/GliderTests.cs`, `Assets/Tests/PlayMode/GliderSceneTests.cs`, 기존 JumpTests·MovementMathTests·MovementAndCameraTests를 실행하여 `TestResults/006-us2.md`에 SC-003~004와 비전개 회귀 결과·첫 착지 화면 근거를 기록한다.

**Checkpoint**: US1·US2를 합쳐 한 섬에서 점프→전개→조작→착지→새 점프를 독립 검증할 수 있다.

---

## Phase 5: User Story 3 — 낙사·재시작과 생명주기 보존 (Priority: P2)

**Goal**: 글라이딩에도 기존 낙사·R 재시작을 적용하고 초점·활성화 전환에서 과거 입력 재생을 방지한다.

**Independent Test**: 낙사 경계 위·동일·아래 및 동시 입력 각 5회, 명세의 입력·활성화 경계 각 5회, 같은 실행에서 전개→낙사→R 복귀 20회를 완료한다. 안내 1초·복귀 2초 기준과 첫 복귀 화면의 날개/안내 부재를 확인한다(SC-005~007).

### Tests for User Story 3

- [X] T021 [US3] `Assets/Tests/PlayMode/GliderTests.cs`에 전개 중 낙사 경계 위·동일·아래 각 5회, 아래 진입+전개 요청 5회, 빠른 하강의 경계 통과, 탈락 후 입력 차단과 새 R 복귀를 추가한다. restartRequested의 “탈락 후 새 R만 허용” 및 경계 아래에서만 탈락을 검증하고 과거 Space 유지 복귀·새 공중 Space를 구분한다.
- [X] T022 [US3] `Assets/Tests/PlayMode/GliderTests.cs`에 초점 전환·컴포넌트 disable/enable·GameObject 비활성화/재활성화·미처리 요청 취소를 각각 5회 추가한다. 이미 전개된 상태와 음수 수직 속도 보존, 물리 진행 중 일정 하강, 비활성 중 착지/경계 통과 후 재활성화 정리, 실제 키 해제 후 조작 재개 및 컨트롤러만 제거할 때 중력 복원을 검증한다. 멈춘 GameObject의 물리 진행을 요구하지 않는다.
- [X] T023 [P] [US3] `Assets/Tests/PlayMode/GliderSceneTests.cs`에 자연 입력·물리 생명주기로 같은 Player의 전개→낙사→R 재시작 20회 시험을 추가한다. 탈락 안내 1초 이내·유효 R 후 조작 복귀 2초 이내, 첫 복귀 렌더의 표시/안내 부재, 플레이어·카메라·날개 수 불변과 초기 자세·속도·지지 재획득을 기록한다.

### Implementation for User Story 3

- [X] T024 [US3] `Assets/Scripts/PlayerMovement.cs`의 낙사 최우선 순서와 EliminatePlayer/RestartAtStartingPosition을 완성한다. 탈락은 점프·전개·같은 틱 재시작보다 우선하고 모든 요청·표시를 지우며 중력을 복원한다. 복귀는 기존 자세/속도/카메라 스냅과 입력 정책을 유지하고 비전개 Airborne에서 실제 지지를 다시 획득한다. 과거 Space는 실행하지 않고 유지 WASD는 기존대로 재개한다.
- [X] T025 [US3] `Assets/Scripts/PlayerMovement.cs`의 OnApplicationFocus/OnDisable/OnEnable/OnDestroy를 T022와 연결한다. 요청·접촉 캐시·수평 이동 정리와 지속 Gliding 보존을 분리하고, 재활성화 시 WakeUp 및 낙사/접촉 재수집으로 실제 상태를 복구한다. 비활성 중 착지 후 중력이 꺼진 채 남거나 제거 후 Rigidbody가 부유하지 않게 한다. `Assets/Scripts/PlayerInputReader.cs`의 기존 구독·키 해제 정책은 유지한다.
- [X] T026 [US3] `Assets/Tests/PlayMode/RestartTests.cs`의 복귀 직후 접촉 재구축 전 새 Space 사례를 명시적 공중 전개와 과거 유지 입력 금지로 분리한다. 새 전개 이후 실제 지상 복귀·새 점프까지 검증하고 기존 새 R·동시 낙사·유지 WASD·무입력 안정 기준은 보존한다. `Assets/Tests/PlayMode/RestartSceneTests.cs`와 `Assets/Tests/PlayMode/PlayerInputRefactorTests.cs`도 실행하여 계약 보존을 확인하고 변경 필요 시 전개 확장에 한정해 `TestResults/006-regression-map.md`에 근거를 적는다. T014에서 등록한 RestartTests 기대 불일치까지 모두 해소되었는지 재실행하여 기록한다.
- [X] T027 [US3] 신규 두 테스트 파일 및 낙사·재시작·입력 관련 기존 시험을 실행하여 `TestResults/006-us3.md`에 SC-005~007, 경계별 5회·입력 경계별 5회·자연 반복 20회의 결과와 시간·렌더 증거를 기록한다. 비활성화나 초점 전환의 미검증 사례를 통과로 합산하지 않는다.

**Checkpoint**: 세 스토리의 흐름을 같은 씬·같은 Player로 검증했다. 전체 회귀와 사람의 직접 검수는 다음 단계에서 별도로 판정한다.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: 적용 가능한 기존 요구 전체와 새 SC-001~008을 최종 대조한다.

- [X] T028 `Assets/Tests/PlayMode/` 전체를 Editor Test Runner에서 실행하고 `TestResults/006-final.xml`, `TestResults/006-final.log`, `TestResults/006-verification.md`에 실제 수집 수·기준선 대비 변화·실패/건너뜀을 기록한다. T005의 승인 범위 외 기존 동작 단언·반복 횟수·임계값을 보존하고 SC-008을 판정한다. T014의 임시 기대 불일치 목록이 모두 해소되었는지 확인하며, 남은 실패는 알려진 실패라도 최종 통과에서 제외한다. 선택적 headless 결과는 전체/렌더 시험을 대신하지 않는다.
- [X] T029 `Assets/Scripts/PlayerMovement.cs`, `Assets/Scenes/SampleScene.unity`, `Assets/Materials/Glider.mat` 및 신규 테스트 `.meta`를 정적 검토하고 git diff --check를 실행한다. `Assets/Player_Actions.inputactions`, 생성 입력 코드·asmdef·기존 씬 설정·GUID가 T003과 같고 새 패키지·두 번째 컨트롤러·전개마다 객체 생성·중복 입력 구독이 없는지 `TestResults/006-preservation.md`에 대조 기록한다.
- [ ] T030 `specs/006-deploy-glider/quickstart.md`의 실제 Game 뷰 절차를 `Assets/Scenes/SampleScene.unity`에서 수행하여 `TestResults/006-manual.md`에 검수자·뷰 크기·0.2초 전개·가독성·방향 조작·첫 착지/복귀 화면·20회 반복을 기록한다. 사람이 필요한 검수는 자동 상태/픽셀 시험으로 대체하지 말고 접근 불가 시 차단 사유와 미검증을 남긴다.
- [X] T031 `specs/006-deploy-glider/quickstart.md`에 이번 실행의 기준선·US별/전체 결과·증거 경로·수동 검수 상태를 요약하고 `TestResults/006-verification.md`에서 FR-001~011 및 SC-001~008을 통과/실패/미검증으로 최종 대조한다. 미실행 작업은 `specs/006-deploy-glider/tasks.md`에서 완료 표시하지 않고 계획·계약과 실제 구현 차이가 있으면 명시한다.

---

## Dependencies & Execution Order

### Phase Dependencies

```text
T001 → T002 → T003 → T004 → T005
  → US1: (T006 ∥ T007) → T008 → T009 → T010 → T011 → T012 → T013 → T014
  → US2: (T015 ∥ T016) → T017 → T018 → T019 → T020
  → US3: ((T021 → T022) ∥ T023) → T024 → T025 → T026 → T027
  → T028 → T029 → T030 → T031
```

- Setup/Foundational은 모든 스토리를 막는 선행 조건이다. 기존 기준선 실패는 원인과 영향 범위를 명시하고, 해결 없이 통과로 덮지 않는다.
- US2·US3는 이미 전개하는 US1에 의존한다. 독립 테스트란 각 누적 기능을 자체 시나리오로 검증한다는 뜻이며 공중 전개 없이 착지/글라이딩 재시작을 구현한다는 뜻이 아니다.
- 우선순위가 같은 US1·US2도 공유 `PlayerMovement.cs`와 테스트 파일 변경 때문에 순서대로 구현한다. US3는 기존 안전한 낙사·복귀의 기본 연결을 US1에서 받아 경계·생명주기 보존을 완성한다.
- T006~T007은 T004~T005 완료 후, T015~T016은 T014 후, T021~T023은 T020 후 시작한다. 각 테스트 묶음은 해당 구현 전에 기대 실패를 확인한다.
- T006~T007은 아직 없는 멤버를 reflection으로 조회해 단언 실패로 보고하므로 T008의 선언에 컴파일 의존하지 않는다. T010의 하강·설정 시험도 T006에서 먼저 작성한다. 각 후속 구현이 이미 일부 시험을 통과시키는 경우에는 실제 결과를 기록하고 실패를 만들기 위해 구현을 되돌리지 않는다.
- T014는 US1 필수 시험 통과와 등록된 과거 기대 불일치를 구분하는 중간 게이트다. JumpTests 기대 변경은 T019, RestartTests 기대 변경은 T026에서 해소하며, T028의 최종 게이트에는 이 중간 예외를 적용하지 않는다.
- US1의 T011은 안전한 기존 종료 경로 연결, US2의 T017은 첫 착지 렌더와 실제 지지 정밀 처리, US3의 T024~T025는 경계 충돌·활성화·입력 재생에 대한 완성이다. 같은 구현을 세 번 새로 만들지 않는다.

### Parallel Opportunities & Examples

| Story | 병렬 작성 예시 | 공통 선행 조건 / 주의 |
| --- | --- | --- |
| US1 | T006: GliderTests.cs의 입력 계약 ∥ T007: GliderSceneTests.cs의 표시 계약 | T004~T005 완료. 서로 다른 파일; Unity 실행은 직렬 |
| US2 | T015: GliderTests.cs의 이동/접지 ∥ T016: GliderSceneTests.cs의 첫 착지 렌더 | T014 완료. 두 파일 동시 편집 가능 |
| US3 | T021→T022: GliderTests.cs의 경계/활성화 ∥ T023: GliderSceneTests.cs의 20회 반복 | T020 완료. T021과 T022는 같은 파일이므로 순차 |

런타임·씬 구현 단계에 가짜 병렬 표시는 부여하지 않는다. 현재 세 스토리는 동일한 컨트롤러를 확장하므로 스토리 전체를 동시에 구현하지 않는다.

## Requirements Traceability

| 요구사항 | 주요 구현/검증 작업 |
| --- | --- |
| FR-001~002 | T006, T008~T009, T013~T014 |
| FR-003 | T006, T010, T014 |
| FR-004 | T015, T018, T020 |
| FR-005 | T006~T007, T011~T014, T016 |
| FR-006 | T015~T017, T019~T020 |
| FR-007 | T021, T023~T024, T027 |
| FR-008 | T021, T023~T024, T026~T027 |
| FR-009 | T022, T025, T027 |
| FR-010 | T006, T009, T015, T019, T021~T022 |
| FR-011 / SC-008 | T002~T005, T013, T019~T020, T026, T028~T031 |
| SC-001~002 | T006~T007, T010, T014 |
| SC-003~004 | T015~T016, T020, T030 |
| SC-005~007 | T021~T023, T026~T027, T030 |

## Implementation Strategy

### MVP First — US1

공통 준비 5개와 US1 9개를 완료하고 T014에서 중단·검증한다. 기존 씬에서 새 공중 Space→전개 표시→느린 하강을 데모하는 범위다. 안전한 기존 착지·낙사·복귀 연결은 포함하지만 US2·US3의 정량 검수가 남아 있으므로 전체 글라이더 완료로 보고하지 않는다.

### Incremental Delivery

1. 현재 프로젝트·기준선과 시험 기반을 보존한다.
2. US1로 전개와 일정 하강을 추가한다.
3. US2로 동일 WASD와 실제 착지의 조작성·시각 계약을 검증한다.
4. US3로 낙사·재시작·활성화 경계와 20회 반복을 완성한다.
5. 전체 회귀·정적 보존·사람 검수를 마친 뒤 최종 결과를 보고한다. 실패·미검증은 별도 표시한다.

## Notes

- 전체 31개 작업: 공통 Setup/Foundational 5개, US1 9개, US2 6개, US3 7개, 최종 4개.
- 실행 결과: 기준선 84/84, 최종 111/111 자동 시험 통과. 모든 US1 중간 기대 불일치는 최종 해소했다. 원본은 `TestResults/006-baseline.json`, `006-final.json`이며 XML/log는 원본 JSON에서 만든 보조 보고서라고 명시했다.
- T024~T025의 기본 전이·정리는 T011에서 이미 연결되어 추가 경계 시험으로 검증했다. 동작을 중복 구현하거나 실패를 만들기 위해 되돌리지 않았다. 전체 GameObject 비활성화는 Unity의 물리 정지를 따르며 논리 전개 보존과 재활성화 후 일정 하강을 검증했다.
- T030은 `TestResults/006-manual.md`에 검수 대기로 기록했다. 자동 실제 씬 렌더·자연 반복 시험 통과를 사람의 직접 검수 완료로 간주하지 않는다.
- 패키지·언어 버전·입력 자산·카메라는 유지한다. 6단계 목표 높이 보정, 여러 섬, 온라인·공격·자기장은 범위 밖이다.
- 문서 생성은 GitHub 이슈 게시나 코드 변경을 수행하지 않는다. 게시 요청이 있는 후속 단계에서는 `docs/agents/issue-tracker.md`를 따른다.

## Phase 7: Convergence

현재 코드·테스트·증거를 명세와 대조해 확인한 검증 보완 작업이다. 기존 T001~T031의 번호·순서·완료 표시는 변경하지 않는다. 게임 규칙이나 6단계 높이 보정 기능을 추가하는 작업이 아니다. 기존 `TestResults/006-final.json`의 111/111 통과는 보존하되 아래 미검증 범위를 대신하는 근거로 사용하지 않는다.

- [ ] T032 MEDIUM — `Assets/Tests/PlayMode/GliderSceneTests.cs`의 실제 씬 검증을 확대하여 상승·정점·하강·걷기 이탈 각각 5회, 총 20회에서 새 Space 이벤트 등록부터 실제 전개 표시가 가능한 렌더 프레임까지 0.2초 이내인지 측정한다. `Assets/Tests/PlayMode/GliderTests.cs`의 직접 FixedUpdate 호출·상태 조회만으로 대체하지 않는다. 실제 씬에서 글라이딩 8방향 이동·방향 전환·입력 해제 각각 5회 동안 플레이어와 전개 표시가 탑뷰에서 식별되는지도 렌더/가시성으로 검증하고 결과·시간·화면 근거를 `TestResults/006-scene-coverage.md`에 남긴다. 해당 시험과 전체 회귀 실행 후 `specs/006-deploy-glider/quickstart.md` 및 `TestResults/006-verification.md`의 판정을 실제 검증 범위에 맞게 갱신한다. per SC-001, SC-003, T006~T007, T015~T016 (partial; F1)
- [ ] T033 MEDIUM — 기존 T030에 정의된 `Assets/Scenes/SampleScene.unity`의 사람 직접 검수를 완료할 수 있도록 검수자에게 `specs/006-deploy-glider/quickstart.md`의 실행 절차를 안내하고, 실제 결과가 제공되면 `TestResults/006-manual.md`의 검수자·Game 뷰 크기·조작감·작은 화면 날개/한글 안내 가독성·첫 착지/복귀 화면·20회 반복 근거를 확인하여 기록한다. 자동 시험이나 에이전트의 이미지 확인을 사람 검수로 대체하지 않는다. 사람 검수가 불가능하거나 응답을 받지 못하면 T030과 이 작업은 모두 미완료로 유지하고 차단 사유를 기록하며, 결과가 있는 경우에만 `specs/006-deploy-glider/quickstart.md`와 `TestResults/006-verification.md`의 수동 검수 상태를 갱신한다. per T030, plan: 실제 Game 뷰 수동 검수·자동/사람 검증 분리 (partial; F2)
- [ ] T034 LOW — `Assets/Tests/PlayMode/GliderSceneTests.cs`에서 각 증거 파일 저장 전 출력 디렉터리 존재를 보장하도록 fixture 준비 또는 공통 저장 경로를 보강한다. 현재 LandingFrameClosesWingAndFreshGroundPressJumps의 `TestResults/006-landed.png` 저장은 다른 시험이 디렉터리를 먼저 생성한 경우에만 안전하므로 이 선행 의존성을 제거한다. 기존 `TestResults/` 결과를 삭제하지 말고 존재하지 않는 임시 출력 경로를 이용한 디렉터리 생성 검증과 해당 착지 시험 단독 실행을 수행하여 `TestResults/006-scene-coverage.md`에 근거를 남긴다. per T016, T020, plan: 독립 실행 가능한 씬 검증 (partial; F3)
