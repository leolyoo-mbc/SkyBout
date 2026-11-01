---
description: "현재 Unity 프로젝트의 글라이딩 목표 높이 보정 구현 작업"
---

# Tasks: 글라이딩 목표 비행 높이 보정

**Input**: `specs/007-glide-height-control/`의 [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/glide-height.md](contracts/glide-height.md), [quickstart.md](quickstart.md).

**Prerequisites**: 기존 Unity 6000.3.26f1 프로젝트와 006 글라이더 구현. 현재 기능 경로는 `.specify/feature.json`으로 결정하며 Git 브랜치 이름에 의존하지 않는다.

**Tests**: 명세 SC-001~SC-007의 반복 검증과 계획의 자동/실제 씬 검증에 따라 시험 작업을 포함한다. 각 스토리의 시험을 먼저 작성해 변경 전 동작과의 차이를 확인한 후 구현한다. 수집 0개·컴파일 오류·수학 예측을 Unity 시험 통과로 처리하지 않는다.

**Organization**: Setup → Foundational → US1(P1) → US2(P1) → US3(P2) → Polish. 이번 파일 생성 시점의 모든 작업은 미실행이다.

**최신 사용자 교정**: 최초 구현 완료 후 ‘Space를 떼면 접기’와 ‘비행 높이2’를 요청받았다. 현재 계약은 **유지 전개/해제 접기/새 누름 재전개, 목표 발Y=2m**다. 아래 T001~T029는 최초 구현의 실행 이력이며 목표0/전개 보존 가정은 Phase7에서 명시적으로 교체한다. 현재 **33/34 완료**, T027 사람 직접 검수만 미완료다.

## Format: `[ID] [P?] [Story] Description`

- `[P]`: 명시된 선행 작업 완료 후 서로 다른 파일에서 병렬 작성 가능. 동일 Editor에서 시험 실행·씬 편집은 직렬 처리한다.
- `[US1]`, `[US2]`, `[US3]`: 명세의 사용자 스토리. Setup/Foundational/Polish에는 붙이지 않는다.
- 경로는 저장소 루트 `E:\projects\SkyBout` 기준이다. 기존 프로젝트·씬·GUID·사용자 변경을 보존하며 새 프로젝트나 별도 런타임 컨트롤러를 만들지 않는다.
- `TestResults/007-*`는 구현 단계에 생성할 증거 경로다. 실행하지 않은 시험은 미검증으로 기록한다. 저장소 정책상 무시되는 결과는 추적 상태도 기록하고 강제 추가하지 않는다.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 현재 프로젝트와 기존 동작의 기준선을 확보한다. 패키지·프로젝트를 새로 초기화하지 않는다.

- [X] T001 `TestResults/007-environment.md`에 현재 Git 상태·사용자 변경, Unity/패키지 버전, 물리 간격 0.02초, `Assets/Scenes/SampleScene.unity`의 섬 표면 Y=0·Feet 오프셋 -1·낙사 높이 -10·기존 입력/카메라/날개 참조를 기록하고 현재 프로젝트/Editor 일치를 확인한다.
- [X] T002 기존 전체 PlayMode 시험을 변경 전에 실행하여 `TestResults/007-baseline.xml` 및 `TestResults/007-baseline.log`에 실제 수집·실행·통과·실패·건너뜀과 실행 환경을 기록한다; 도구가 JSON만 제공하면 원본 JSON을 보존하고 XML을 네이티브 결과인 것처럼 표기하지 않으며 기존 실패는 원인과 이번 변경과의 관계를 분리한다.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 모든 스토리의 회귀 범위와 고립된 시험 실행 기반을 확정한다.

- [X] T003 `TestResults/007-test-migration.md`에 `Assets/Tests/PlayMode/GliderTests.cs` 및 `Assets/Tests/PlayMode/GliderSceneTests.cs`의 -2m/s·무조건 하강·하강 시간 비교·필연적 낙사·속도 기반 착지·비활성 하강 단언을 새 계약/FR/SC와 대응시킨다; 입력·수평 조작·실제 지지·경계·R 복귀·날개 렌더·동일 인스턴스 보존 단언은 유지 대상으로 명시한다.
- [X] T004 `Assets/Tests/PlayMode/GlideHeightTests.cs`에 기존 가상 Keyboard·비활성 생성 후 참조 설정·격리 물리 fixture 패턴을 따르는 시험 기반을 준비한다; 섬 밖 발 높이 배치, 초기 실제 Y속도 지정, 틱별 위치/속도 수집, 실제 상태 대기, 활성 컨트롤러에만 FixedUpdate 호출, 입력/씬/물리/초점 설정 teardown 복원을 제공하고 `.meta`는 Unity로 생성한다.

**Checkpoint**: T001~T004 완료 전 스토리 구현을 시작하지 않는다. 기존 사용자의 런타임 변경을 덮어쓰지 않는다.

---

## Phase 3: User Story 1 — 낮거나 높은 위치에서 목표 높이로 합류하기 (Priority: P1) — MVP

**Goal**: 새 공중 전개 시 기존 수직 속도를 이어받아 목표 발 높이에 부드럽게 수렴하고 무입력으로 유지한다.

**Independent Test**: 충돌/낙사 여유가 있는 섬 밖에서 목표 대비 -3/-1/0/+1/+3m, 초기 속도 0을 각 5회 전개하고, -3/+3m × 초기 속도 -5/+5m/s 네 조합도 각 5회 전개한다. 5초 이내 ±0.1m 도달 후 10초 유지, 안정 높이 폭 ≤0.1m, 0.1초 속도 변화 ≤1m/s, 반대편 0.1m 초과 진입 최대 1회다.

### Tests for User Story 1

- [X] T005 [P] [US1] `Assets/Tests/PlayMode/GlideHeightMathTests.cs`에 `CalculateGlideVerticalVelocity`의 결정성·목표 위/아래/동일·초기 ±5m/s·dt 적용·가속도 한도·다른 월드 목표/발 오프셋 입력을 검증하고 0.02초 이산 적분으로 13개 고유 초기 조건의 5초 수렴/10초 유지/초과 통과 횟수를 시험한다; 누락 함수로 인한 컴파일 실패와 동작 단언 실패를 구분하고 Unity가 `.meta`를 생성하게 한다.
- [X] T006 [P] [US1] `Assets/Tests/PlayMode/GlideHeightTests.cs`에 SC-001의 25회 및 SC-002의 20회 실제 Rigidbody 수렴 시험을 추가한다; 전개 틱 위치 차이 ≤0.001m·첫 틱 속도 변화 ≤0.2m/s·모든 연속 5틱 속도 차이 ≤1m/s(수치 오차 ≤0.00001m/s)·SC-003 안정 폭/초과 통과를 기록하고 목표 부근에서 초기 속도가 있는 경우, 비전개 공중/지상의 보정 미적용 및 반복 Space의 목표 불변도 검증한다.
- [X] T007 [P] [US1] `Assets/Tests/PlayMode/GliderTests.cs`에서 폐기될 `InvalidDescentSpeedIsRejected`를 목표 유한성/낙사 관계, 응답과 파생값, 가속도 검증 시험으로 교체한다; 0/음수/NaN/양·음 무한대·응답 제곱 overflow/underflow·가속도 10 초과를 포함하고 유효한 0/음수 목표(낙사 경계 위)는 허용하며 Awake와 실행 중 OnValidate 오류/비활성화 경로를 모두 확인한다.

### Implementation for User Story 1

- [X] T008 [P] [US1] T005~T007 작성 및 변경 전 실패 확인 후 `Assets/Scripts/MovementMath.cs`에 현재 발 높이·목표·현재 Y속도·응답·최대 가속도·deltaTime을 받는 순수 `CalculateGlideVerticalVelocity`를 추가한다; `a=clamp(response²*error-2*response*velocity, ±maxAcceleration)`, `next=velocity+a*deltaTime`을 사용하고 위치 쓰기·상태 캐시·즉시 속도 clamp 없이 기존 수평/점프 함수를 보존한다.
- [X] T009 [P] [US1] T005~T007 작성 및 변경 전 실패 확인 후 `Assets/Scripts/PlayerMovement.cs`의 `glideDescentSpeed`를 `glideTargetFootHeight=0`, `glideHeightResponse=3`, `glideVerticalAcceleration=10`으로 교체한다; 데이터 모델 제약 “유한, `fallDeathHeight`보다 큼. 전개 위치/지형과 독립”, “양의 유한값, 제곱 및 두 배도 양의 유한값”, “유한, 0 초과 10 이하. 한 틱 보정 속도 변화 상한”, `fallDeathHeight`의 “기존 엄격한 미만 비교 유지”를 적용하고 오류 메시지/단위를 갱신하며 기존 필수 참조·몸체 검증을 유지한다; 잘못된 값은 자동 보정하지 않고 기존 로그/비활성화 경로로 처리한다.
- [X] T010 [US1] T008~T009 후 `Assets/Scripts/PlayerMovement.cs`의 `ApplyGlideVelocity`에서 기존 `FootPosition.y`와 실제 Rigidbody Y속도로 새 계산을 호출하여 X/Z를 보존해 적용한다; 전개 시 Y 초기화·위치 스냅·숨겨진 속도 캐시를 추가하지 않고 기존 낙사→접지→요청 소비→수직 보정→CanMove/수평 갱신 순서와 Gliding의 중력 꺼짐을 유지한다.
- [X] T011 [US1] `Assets/Tests/PlayMode/GliderTests.cs`의 전개 입력 시험에서 상수 -2m/s·항상 하강 및 동일 6m 하강 비교만 목표 접근/연속성 계약으로 교체한다; 지상 키 유지·짧은 공중 누름·첫 요청 우선·요청 의도 고정·반복 입력 무시·날개·중력 단언은 유지하고 수평/착지/생명주기 전용 메서드는 후속 스토리에서 갱신하도록 T003 대응표에 기록한다.
- [X] T012 [US1] `Assets/Scenes/SampleScene.unity`의 기존 Player 설정에서 폐기된 `glideDescentSpeed` 키를 제거하고 목표 발 높이 0·응답 3·가속도 10을 명시적으로 저장한다; 이전 속도 값을 높이로 FormerlySerializedAs 매핑하지 않고 기존 GUID·섬·Player/Feet·날개·입력·카메라·물리 설정을 보존한다.
- [X] T013 [US1] `Assets/Tests/PlayMode/GlideHeightMathTests.cs`, `Assets/Tests/PlayMode/GlideHeightTests.cs` 및 US1로 갱신한 기존 시험을 실행하고 `TestResults/007-us1.json`과 `TestResults/007-height-samples.csv`에 실제 결과·각 초기 조건·틱/발 높이/속도·도달 시간·10초 폭·통과 횟수를 기록한다; 순수 수학과 Unity 물리 결과를 구분하며 아직 갱신 전인 US2/US3 시험의 예상 불일치도 숨기지 않는다.

**Checkpoint / MVP**: US1은 무입력 상승·하강·유지를 독립 시연할 수 있다. 실제 섬 착지와 전체 회귀 검수 전에는 기능 전체 완료로 보고하지 않는다.

---

## Phase 4: User Story 2 — 높이를 맞추면서 기존 방향 조작과 착지 유지하기 (Priority: P1)

**Goal**: 높이 보정 중 기존 수평 조작과 실제 지지 접촉 착지를 유지하고 장애물 관통을 방지한다.

**Independent Test**: 목표 아래/위/도달 상태에서 8방향·전환·키 해제를 시험한다. 하강 착지·옆면·상승 천장 차단을 각 5회 검증하고 기존 SampleScene에서 실제 착지→날개 닫힘→새 점프를 확인한다.

### Tests for User Story 2

- [X] T014 [P] [US2] `Assets/Tests/PlayMode/GlideHeightTests.cs`에 세 높이 관계에서 SC-004 수평 속도·가감속·대각선·해제 후 정지 및 수직 보정 지속을 추가하고, 목표보다 높은 지지 표면 하강 착지·옆면·천장 차단/제거를 각 5회 시험한다; 목표 도달/낮은 속도를 착지로 판정하지 않고 관통·허위 접지·경로 개방 후 보정 재개를 단언한다.
- [X] T015 [P] [US2] `Assets/Tests/PlayMode/GliderTests.cs`의 수평 이동/비지지 표면 시험에서 -2m/s·무조건 하강 단언을 교체하고 착지 대기를 실제 Grounded·날개 닫힘 기준으로 변경한다; 기존 점프→전개→착지→새 점프 및 Space 유지 시 자동 점프 금지를 10회 유지하며 바닥 Y=0과 목표 Y=0에서 실제 접촉 여부를 검증한다.
- [X] T016 [P] [US2] `Assets/Tests/PlayMode/GliderSceneTests.cs`의 실제 SampleScene 점프·전개·착지 시험에서 속도 기반 착지 대기를 제거하고 실제 접촉·첫 착지 표시 프레임의 날개 제거·중력 복원·새 점프를 검증한다; 기존 날개의 비충돌 구성·렌더 식별·카메라 추적 단언은 보존한다.

### Implementation for User Story 2

- [X] T017 [US2] T014~T016의 실패가 있으면 `Assets/Scripts/PlayerMovement.cs`의 수평 속도 Y보존, 실제 지지의 Gliding 종료, 충돌 콜백의 즉시 날개 닫힘을 최소 범위로 수정한다; 기존 `GroundSupport`의 실제 접촉 기준과 PlayerMovement 단일 소유권을 보존하고 정상 동작이면 불필요하게 코드를 바꾸지 말고 `TestResults/007-us2.md`에 보존 근거를 기록한다.
- [X] T018 [US2] `Assets/Scenes/SampleScene.unity`의 기본 목표 Y=0에서 T016의 실제 접촉 착지를 실행하여 `TestResults/007-scene-landing.md`와 `TestResults/007-landed.png`에 상태/접촉/날개/새 점프 증거를 기록한다; 목표 근처에서 떠 있기만 하면 완료를 막고 원인을 진단하며 근접 접지·목표의 몰래 하향 변경·물리 전역 설정 변경으로 통과시키지 않는다; 설계 수정이 필요하면 `specs/007-glide-height-control/research.md` 및 계약에 변경 근거를 먼저 남긴다.
- [X] T019 [US2] T017~T018 후 US1+US2 시험과 기존 `Assets/Tests/PlayMode/MovementMathTests.cs`, `Assets/Tests/PlayMode/MovementAndCameraTests.cs`, `Assets/Tests/PlayMode/JumpTests.cs`를 실행하고 `TestResults/007-us2.json` 및 `TestResults/007-us2.md`에 SC-004~SC-005 각 조건의 실제 결과·실패/미검증·수평 회귀·관통 여부를 기록한다.

**Checkpoint**: US1 수렴을 유지하면서 실제 착지와 기존 조작이 독립 검증된다. T018 실패를 허용 오차 완화로 숨기지 않는다.

---

## Phase 5: User Story 3 — 탈락·재시작 후에도 일관된 높이 보정 사용하기 (Priority: P2)

**Goal**: 탈락 우선·R 초기화·동일 목표 및 입력 해제 정책을 유지하고 초점/비활성 경계를 구분한다.

**Independent Test**: 경계 위/동일/아래 및 아래 진입+전개를 각 5회, 초점/컴포넌트/전체 Player 비활성을 각 5회 시험한다. 전개→시험용 경계 아래 이동→탈락→R→새 점프·전개를 10회 반복하고 잔류 움직임·과거 입력 재실행이 없어야 한다.

### Tests for User Story 3

- [X] T020 [P] [US3] `Assets/Tests/PlayMode/GlideHeightTests.cs`에 엄격한 발 경계 판정과 전개 겹침을 각 5회, 초기 고속 하강 감속 중 실제 경계 통과, 탈락/재시작의 비보정·최초 자세·0속도·동일 목표, Space 유지와 새 입력 정책을 추가한다; 경계 동일 높이는 적분 전 판정과 이후 하강 결과를 분리하고 10회 재시작 시 이전 보정/표시가 남지 않음을 확인한다.
- [X] T021 [P] [US3] `Assets/Tests/PlayMode/GliderTests.cs`의 생명주기/비활성 착지·낙사 기대를 새 계약으로 교체한다; 초점 상실의 진행 틱에서는 보정 지속, 컴포넌트 비활성에서는 보정 없이 실제 Y속도로 관성 이동, 전체 Player 비활성에서는 몸체 정지를 각각 5회 확인하며 복귀 후 실제 속도에서 수렴·요청 폐기·키 해제 정책을 검증한다; 명시적 하강 속도로 비활성 중 착지/경계 통과를 준비해 복귀 판정 우선순위와 파괴 시 중력/표시 정리도 유지한다.
- [X] T022 [P] [US3] `Assets/Tests/PlayMode/GliderSceneTests.cs`의 자연 이탈→전개→필연적 낙사 루프를 실제 걷기 이탈→전개→수렴/10초 유지 확인→시험용 경계 아래 이동→탈락→R→새 점프·전개의 10회 시험으로 교체한다; 강제 시험 배치와 기능 이동을 구분하고 기존 안내 표시·복귀 시간·동일 Player/카메라/섬 인스턴스·날개 렌더를 보존한다.

### Implementation for User Story 3

- [X] T023 [US3] T020~T022를 기준으로 `Assets/Scripts/PlayerMovement.cs`에서 탈락 검사 우선, 기존 재시작의 “선형/각속도 0, 요청/접촉 초기화”, 초점 요청/수평 정리, 비활성 전개/목표 유지, 활성화 WakeUp/지지 재구축을 보존하거나 필요한 최소 수정만 한다; 비활성 중 별도 보정기·중력 강제 복원·과거 속도 복원·비활성 시간 재생을 추가하지 않고 보정 메모리가 없는 구조를 유지한다.
- [X] T024 [US3] 경계/재시작/초점/비활성 시험 및 기존 `Assets/Tests/PlayMode/FallEliminationTests.cs`, `Assets/Tests/PlayMode/RestartTests.cs`, `Assets/Tests/PlayMode/RestartSceneTests.cs`, `Assets/Tests/PlayMode/PlayerInputReaderTests.cs`, `Assets/Tests/PlayMode/PlayerInputRefactorTests.cs`를 실행하여 `TestResults/007-us3.json`에 결과를 기록한다; 비활성 컨트롤러에 FixedUpdate를 강제 호출하지 않고 runInBackground=0의 실제 앱 중지와 물리 틱 진행을 구분한다.
- [X] T025 [US3] T022의 실제 씬 10회 반복을 실행하고 `TestResults/007-repeat-timings.csv`, `TestResults/007-eliminated.png`, `TestResults/007-restored.png`에 각 회차 전개/유지/탈락/복귀 시각과 인스턴스·상태·표시를 기록한다; 정상 체공을 낙사 누락으로 오판하지 않고 SC-006~SC-007의 잔류 보정·자동 입력·탈락 구제 0회를 확인한다.

**Checkpoint**: 세 스토리의 상태/입력/수직 계약이 모두 연결되며 각 증거를 통과/실패/미검증으로 구분한다.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: 전체 회귀와 실제 조작감 검수를 마무리하고 구현 결과를 문서화한다.

- [X] T026 기존 전체 PlayMode와 신규 높이 시험 및 `Assets/Tests/PlayMode/GliderSceneTests.cs`를 최종 실행하여 `TestResults/007-final.xml`, `TestResults/007-final.log`, `TestResults/007-verification.md`에 실제 수집 수·실패/건너뜀·FR-001~FR-011/SC-001~SC-007 대응 결과·006 단언 교체 근거를 기록한다; JSON 기반 도구이면 원본을 보존하고 보조 XML 출처를 명시하며 0개 수집/과거 결과 재사용을 금지한다.
- [ ] T027 `specs/007-glide-height-control/quickstart.md`의 실제 SampleScene 절차를 사람이 직접 실행하여 `TestResults/007-manual.md`에 검수자·뷰 크기·상승/하강/감속/10초 유지·반복 출렁임·WASD·착지·R·초점·날개/한글 안내·카메라 조작감 결과를 기록한다; 사람이 검수하지 못하면 이유와 미검증을 남기고 이 작업을 완료 체크하지 않으며 자동 화면으로 006의 미완료 사람 검수까지 대체하지 않는다.
- [X] T028 `specs/007-glide-height-control/quickstart.md`와 `specs/007-glide-height-control/research.md`에 최종 실제 설정·구현 범위·시험 원본 경로·검수 통과/실패/미검증·설계 변경 유무를 반영한다; 수학 예측과 Unity 실측을 구분하고 `specs/007-glide-height-control/tasks.md`의 체크는 실제 완료 근거가 있는 작업에만 갱신한다.
- [X] T029 `Assets/Scripts/PlayerMovement.cs`, `Assets/Scripts/MovementMath.cs`, `Assets/Scenes/SampleScene.unity` 및 신규 시험의 `.meta`를 최종 diff로 검사하여 GUID/입력/카메라/패키지/물리 전역 설정 보존·단일 컨트롤러·틱별 할당/구독 추가 없음·죽은 glideDescentSpeed 참조 없음·git diff --check 결과를 `TestResults/007-final-review.md`에 기록하고 범위 밖 변경은 건드리지 않는다.

---

## Phase 7: 사용자 피드백 교정 — Hold-to-Glide & Height2

**Goal**: 실제 요청대로 Space 해제에 접히고 중력 낙하하며 새 공중 누름으로 재전개한다. 기본 높이는 점프와 같은 발Y=2m다.

- [X] T030 `Assets/Tests/PlayMode/GlideHeightTests.cs`에 Space 유지→해제→WASD 유지 낙하→재전개5회 및 물리 틱 전 해제된 공중 탭의 비전개 시험을 먼저 추가하고 `TestResults/007-hold-red.json`에서 실제 해제 후 IsGliding 잔류 실패를 확인한다.
- [X] T031 `Assets/Scripts/PlayerInputReader.cs`에 유효 유지/해제 신호를, `Assets/Scripts/PlayerMovement.cs`에 전개 취소를 연결하여 Space 해제/초점/비활성 시 전개 요청·날개·높이 보정을 끝내고 중력을 복원한다; 위치/수직 속도 스냅 없이 새 누름 재전개, 지상 짧은 탭 점프 보존, 복귀 후 자동 재전개 금지를 구현한다.
- [X] T032 `Assets/Scripts/PlayerMovement.cs`의 기본 목표를2로 변경하고 `Assets/Scenes/SampleScene.unity`에 이미 저장된 사용자 지정2를 보존한다; `Assets/Tests/PlayMode/GliderSceneTests.cs`에서 점프/목표2 일치·Space 유지 중2m 체공·해제 낙하/실제 착지·새 점프를 확인한다.
- [X] T033 `Assets/Tests/PlayMode/GliderTests.cs`, `Assets/Tests/PlayMode/GlideHeightTests.cs`, `Assets/Tests/PlayMode/PlayerInputReaderTests.cs`, `Assets/Tests/PlayMode/GliderSceneTests.cs`, `Assets/Tests/PlayMode/FallEliminationTests.cs`, `Assets/Tests/PlayMode/JumpTests.cs`를 유지 입력 계약으로 갱신한다; 실제 씬10회는 강제 배치 대신 Space 해제 후 자연 낙사/R 복귀로 검증하고 원점 fixture의 명시적 목표0과 실제 씬 기본2를 구분한다.
- [X] T034 전체 시험을 실행하여 `TestResults/007-hold-final.json`의159/159 통과와 실제 씬/회귀 증거를 `TestResults/007-hold-verification.md`에 기록하고 `specs/007-glide-height-control/spec.md`, `plan.md`, `data-model.md`, `contracts/glide-height.md`, `quickstart.md`, `research.md`를 최신 교정에 맞춘다; 사람 직접 검수는 T027 미완료로 남기고 최종 diff/문서 링크를 확인한다.

**Checkpoint**: 159/159 통과(335.17초). Space 해제 시 접기·중력 낙하,2m 유지, 실제 씬10회 자연 낙사/재시작을 검증했다. 별도의 모든 슈퍼바이브 규칙 복제나 직접 사람 검수를 완료했다고 주장하지 않는다.

---

## Dependencies & Execution Order

### Phase Dependencies

```text
T001 → T002 → T003 → T004
                       ↓
                    US1 (T005–T013): 수렴 MVP
                       ↓
                    US2 (T014–T019): 이동·실제 착지
                       ↓
                    US3 (T020–T025): 탈락·복귀·생명주기
                       ↓
                    Polish (T026–T029)
```

US2와 US3는 **동작상 US1 제어에 의존**한다. 각 스토리는 자기 인수 시나리오로 별도 필터 실행할 수 있지만 동일 `PlayerMovement.cs`·`GliderTests.cs`·`GliderSceneTests.cs`를 다루므로 스토리 전체를 병렬 구현하지 않는다. US2→US3 순서는 공유 파일 충돌과 Editor 상태 간섭을 피하기 위한 실행 순서이며, US3의 탈락/입력 의미를 US2가 새로 정의하는 것은 아니다.

### Within Each Story

- US1: T005/T006/T007 병렬 작성 가능 → 변경 전 실패 확인 → T008/T009 병렬 구현 가능 → T010 → T011 → T012 → T013.
- US2: T014/T015/T016 병렬 작성 가능 → T017 → T018 → T019. 실제 착지 게이트를 통과하지 못하면 다음 완료 판정을 진행하지 않는다.
- US3: T020/T021/T022 병렬 작성 가능 → T023 → T024 → T025.
- Polish: T026 → T027 → T028 → T029. 사람이 수행하지 못한 T027은 미완료로 남겨 나머지 결과만 기록할 수 있으나 전체 검수 완료라고 선언하지 않는다.
- 시험 실행은 하나의 Unity Editor에서 직렬 처리한다. 새 테스트 파일을 병렬 작성해도 컴파일/실행 체크포인트는 합쳐서 한 번에 진행한다.
- 기존 동작이 계약을 이미 충족하는 유지 작업은 불필요한 코드 수정 대신 해당 시험 통과와 경로 근거를 기록한다.

### Requirement Coverage

| 요구/기준 | 주요 작업 |
| --- | --- |
| FR-001, 설정/발 좌표 기준 | T005~T010, T012 |
| FR-002~FR-003, SC-001~SC-003 | T005~T006, T008, T010, T013, T027 |
| FR-004, 상태별 보정 범위 | T006, T010, T020, T023 |
| FR-005, SC-004 | T014~T015, T017, T019 |
| FR-006, 전개 입력/표시 | T006, T011, T015~T016, T021~T022 |
| FR-007, SC-005 | T014~T019 |
| FR-008~FR-009, SC-006 | T020, T022~T025 |
| FR-010, SC-007 생명주기 | T021, T023~T024 |
| FR-011, SC-007 회귀/실제 프로젝트 | T001~T003, T012, T016, T019, T024~T029 |

---

## Parallel Example: User Story 1

T004 완료 후 서로 다른 파일에서 작성한다. 모두 작성/실패 확인 후 구현 그룹으로 넘어간다.

```text
T005: Assets/Tests/PlayMode/GlideHeightMathTests.cs — 순수 계산 계약
T006: Assets/Tests/PlayMode/GlideHeightTests.cs — 실제 물리 수렴
T007: Assets/Tests/PlayMode/GliderTests.cs — Inspector 제약

T008: Assets/Scripts/MovementMath.cs — 다음 수직 속도 계산
T009: Assets/Scripts/PlayerMovement.cs — 새 설정과 검증 (통합 호출은 T010)
```

## Parallel Example: User Story 2

US1 체크포인트 이후 시험 작성만 병렬로 진행하고 런타임 수정과 Editor 실행은 합쳐서 처리한다.

```text
T014: Assets/Tests/PlayMode/GlideHeightTests.cs — 수평/충돌 조건
T015: Assets/Tests/PlayMode/GliderTests.cs — 기존 fixture 회귀
T016: Assets/Tests/PlayMode/GliderSceneTests.cs — 실제 씬 착지/표시
```

## Parallel Example: User Story 3

US2 체크포인트 이후 파일별 시험 작성을 나눌 수 있다. 실제 씬/입력 전역 상태를 사용하는 실행은 직렬이다.

```text
T020: Assets/Tests/PlayMode/GlideHeightTests.cs — 경계/재시작
T021: Assets/Tests/PlayMode/GliderTests.cs — 생명주기 회귀
T022: Assets/Tests/PlayMode/GliderSceneTests.cs — 반복 시도/렌더
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Setup/Foundational에서 현재 상태·기준선·회귀 교체 범위·격리 fixture를 준비한다.
2. US1 시험을 먼저 작성하고 수학 계산·설정·기존 컨트롤러 통합·기존 씬 설정을 적용한다.
3. T013에서 섬 밖 상승/하강/유지와 연속성의 실제 물리 증거를 확인한다. 여기까지가 MVP다.
4. 이 단계의 시연 성공을 실제 섬 착지나 전체 기능 완료로 확대 해석하지 않는다.

### Incremental Delivery

1. US2에서 수평 조작과 실제 착지를 추가 검증한다. Y=0 목표에서 실제 접촉이 없으면 근접 접지로 우회하지 않고 게이트 실패를 해결한다.
2. US3에서 낙사·R·초점/컴포넌트/전체 Player 비활성 경계를 검증한다. 정상 유지 중 필연적 낙사를 기다리지 않는다.
3. 전체 회귀·사람 검수·문서/정적 검사로 마무리한다. 자동/실제 씬/사람 결과를 분리 보고한다.

## Notes

### 실행 결과 (2026-11-01)

- **28/29 완료**. T027만 미완료: 사람이 직접 조작감·자연스러움·가독성을 검수하지 않았다. 체크리스트 품질 마커는 변경하지 않았다.
- 기준선111/111(268.2초), 최종156/156(333.27초) 통과, 실패·건너뜀0. 실제 비행45회 최악2.60초 수렴, 도달 후10초 높이 폭 최대0.09967929m. 실제 씬10회 반복도 통과했다.
- 원본 `TestResults/007-final.json`, 요구사항 대조 `TestResults/007-verification.md`, 사람 검수 대기 `TestResults/007-manual.md`. TestResults는 기존 규칙대로 무시하며 강제 추가하지 않았다. XML/log는 JSON에서 만든 보조 보고서다.
- 연구의 제어식·기본 목표는 그대로 구현했다. 실제 표면 착지는 기존 접촉 기준으로 통과하여 별도 근접 접지/목표 하향 변경이 없었다. 런타임 생명주기 수정도 불필요했다.
- 시험 실행용 전체 제한만300초로 명시했고 합격 기준은 완화하지 않았다. T027은 수행하지 않은 채 다음 문서/정적 검사 작업을 완료할 수 있다는 실행 순서의 예외를 적용했다.

- 최초29개(Setup2, Foundational2, US1 9, US2 6, US3 6, Polish4)에 사용자 교정5개를 추가하여 총34개다. 최신 완료33개, 미완료 T027 한 개다.
- `[P]` 작업은 11개이며 위 선행 조건과 파일 경계를 지킬 때만 병렬 가능하다. 이는 현재 명령에서 병렬 구현을 실행하라는 뜻이 아니다.
- 명세 품질 체크리스트는 구현자가 완료 상태를 변경하는 작업 대상이 아니다. `tasks.md` 실행 체크와 구분한다.
- 헌법은 미작성 템플릿이므로 예시 원칙을 확정 정책으로 적용하지 않는다.
- 작업 생성만으로 런타임·씬·테스트 코드를 수정하거나 GitHub 이슈를 게시하지 않는다.
