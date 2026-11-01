# Tasks: 같은 높이의 하늘섬 사이 점프·글라이딩 이동

**Input**: `specs/008-island-traversal/`의 spec.md, plan.md, research.md, data-model.md, contracts/island-traversal.md, quickstart.md

**Prerequisites**: 설계 게이트 통과. 실제 Git 브랜치는 `main`, 활성 기능은 `.specify/feature.json`으로 찾는다. 헌법은 미작성 템플릿이므로 예시 원칙은 적용하지 않는다.

**Tests**: 명세 FR-010 및 SC-001~SC-007에서 실제 이동 시험과 결과 기록을 요구하므로 자동·수동 검증 작업을 포함한다. 이번 명령은 작업 목록만 생성하며 구현이나 시험 실행은 하지 않는다.

**Organization**: 사용자 스토리별 구현/검증 단위. 기존 이동 코드의 재구현, 신규 패키지·관리자·런타임 데이터 모델은 필요하지 않다.

## Format: `[ID] [P?] [Story] Description`

- `[P]`: 선행 작업 완료 후 다른 파일 작업과 병렬 수행 가능. 동일 Unity Editor의 테스트/사람 플레이는 동시에 실행하지 않는다.
- `[US1]`, `[US2]`, `[US3]`: 명세의 사용자 스토리. 공통 준비와 마무리에는 스토리 라벨이 없다.
- 파일 경로는 저장소 루트 기준. 문서상의 `A`는 Ground, `B`는 IslandShort, `C`는 IslandLong이다.
- 체크박스는 실제 완료 후에만 갱신한다. 관찰자 미확보·Editor 접근 불가 등으로 미검증이면 해당 검증 작업을 완료 처리하지 않는다.

## Path Conventions

- 플레이 씬: `Assets/Scenes/SampleScene.unity`
- 신규 시험: `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`, 동일 이름 `.cs.meta`는 Unity가 생성
- 기존 어셈블리: `Assets/Tests/PlayMode/SkyBout.PlayModeTests.asmdef` 재사용
- 설계 문서: `specs/008-island-traversal/`
- 실제 구현 단계의 증거: `TestResults/008-*`; 테스트 통과로 보이는 가짜 결과를 미리 작성하지 않는다.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 현재 사용자 작업과 기존 플레이 기준을 보존하고 검증 환경을 준비한다. 새 프로젝트를 초기화하지 않는다.

- [X] T001 `Assets/Scenes/SampleScene.unity`, `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/TimeManager.asset` 및 `Assets/Scripts/PlayerMovement.cs`의 기존 변경·미저장 씬·시작 자세·이동 설정을 확인하고 `TestResults/008-baseline.md`에 기준선을 기록한다; 사용자 변경은 덮어쓰지 않고 Editor 6000.3.26f1, fixedDeltaTime=0.02, 기존 Ground/Player/Camera 참조와 X>16, Z≈0 낙사 통로를 보존 대상으로 명시한다.
- [X] T002 `specs/008-island-traversal/quickstart.md`의 실행 방식으로 기존 전체 PlayMode 기준선을 실행해 `TestResults/008-baseline-playmode.xml`에 저장하고 기존 실패·실행 환경·최신 Unity CLI 버전을 `TestResults/008-baseline.md`에 기록한다; 실행 중인 같은 프로젝트 Editor가 있으면 그 창의 Test Runner를 사용하고 별도 배치 프로세스를 중복 실행하지 않는다.

**Checkpoint**: 기존 실패와 새 회귀를 구분할 기준선 확보. 실행이 불가능하면 이유를 기록하고 검증을 차단 상태로 남긴다.

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 모든 스토리가 공유하는 실제 씬 시험과 증거 기록 기반을 마련한다. 이 단계가 끝나기 전 스토리 기능 검증을 시작하지 않는다.

- [X] T003 [P] `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`에 기존 `GliderSceneTests.cs` 패턴의 실제 SampleScene 로드·소유권 추적·가상 Keyboard·입력 장치 제한·초점 설정·teardown을 구현하고 Unity 생성 `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs.meta`를 보존한다; 씬을 재사용한 경우에도 각 사례의 원래 시작 자세/상태를 복원하고 입력 장치·설정을 복구하며 운영 코드에 테스트용 public setter를 추가하지 않는다.
- [X] T004 [P] `specs/008-island-traversal/quickstart.md`에 스토리별 시험 메서드 접두사 `US1_`, `US2_`, `US3_`와 필터 실행/결과 저장 방법, 16:9 1280×720 렌더 검수 전제 및 baseline 결과 위치를 구체화한다; `--filter IslandTraversalSceneTests` 전체 실행과 Test Runner 대안을 유지하고 테스트 0건은 미검증으로 명시한다.
- [X] T005 `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`에 `data-model.md`의 4개 도움닫기 시작점·출발선·제동 위치를 테스트 전용 정의로 추가하고 실제 입력으로 이동하는 보조 루틴과 목적지 접지 단언을 작성한다; “출발선 통과 직전/근처에 입력을 발행하되 발 중심은 출발 섬 표면 안쪽이어야 한다”, “점프 시점의 수평 속도는 8±0.1m/s를 검증한다”, “`Grounded`이고 발 위치의 아래 raycast가 **목적지 Collider**를 가리켜야 한다”, “발 높이가 목적지 상단 ±0.05m, 수직 속도 절댓값 ≤0.1m/s이며 최소 5회 연속 물리 갱신 동안 지지가 유지되어야 한다”를 그대로 적용한다; 독립 시험 초기 배치 후 접촉을 안정화하고 주행 도중 위치·속도·상태를 주입하지 않는다.
- [X] T006 `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`에 시도별 결과 기록과 `TestResults/008-traversal-attempts.csv` 생성을 추가한다; 필드는 “scenario, direction, attempt, launchPosition, launchSpeed, mode, destination, landedCollider, footHeight, elapsedGameSeconds, result, reason”이며 “벡터/문자열 CSV escaping과 invariant 수치 형식을 사용한다”; 실패 원인도 남기고 시험마다 파일을 덮어써 이전 스토리의 결과를 유실하지 않도록 실행 식별/초기화 경계를 정한다.

**Checkpoint**: fixture·실제 입력·목적지 접지·결과 기록을 각 스토리에서 재사용할 수 있다. T003과 T004만 파일이 독립적이며 병렬 가능하다. T005→T006은 같은 테스트 파일이므로 순차 수행한다.

## Phase 3: User Story 1 - 짧은 섬 사이 간격을 점프로 건너기 (Priority: P1) — MVP

**Goal**: 같은 높이의 섬 3개를 제공하고 A–B의 점프 왕복 및 새 지상 조작을 확인한다.

**Independent Test**: US1 시험만 실행하여 실제 씬에 분리된 섬 3개와 안전한 시작 지지가 있고 A→B/B→A 비전개 점프 각 5회가 목적지에 착지한 뒤 걷기·새 점프까지 성공함을 확인한다. 긴 구간 글라이딩 성공에 의존하지 않는다.

### Tests for User Story 1

- [X] T007 [US1] `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`에 `US1_` 씬 계약 시험을 먼저 작성하고 섬 추가 전 실패를 확인한다; A 중심 (0,-1,0)/크기 (32,2,32), B (0,-1,26)/(12,2,12), C (26,-1,26)/(12,2,12), identity 회전·분리된 4m/14m 간격을 단언하고 “활성 Renderer와 활성 비트리거 정적 Collider, Ground 레이어 인덱스 3을 가진다. 새 Rigidbody는 없다”, “모든 상단 Y는 시험 허용 오차 0.001m 안에서 0이다. 섬 사이에는 보이지 않는 연결 Collider가 없어야 한다”를 검증한다; 빈 공간 지지 없음과 옆면만의 허위 착지 없음도 확인한다.

### Implementation for User Story 1

- [X] T008 [US1] `Assets/Scenes/SampleScene.unity`의 기존 Ground mesh/collider 구성을 복제해 IslandShort와 IslandLong을 T007의 좌표·크기로 추가한다; “원래 Ground는 이름·변환·자산 참조를 유지한다. B/C는 서로 다른 씬 객체 ID를 가지며 기존 객체의 ID를 재사용하지 않는다”를 지키고 Player/Camera 루트·최초 (0,1,0) 및 방향·기존 GUID·카메라 설정·동쪽 낙사 공간을 보존한다; 새 관리자·Rigidbody·입력·이동 성능 변경은 추가하지 않는다.
- [X] T009 [US1] `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`에 `US1_` 짧은 구간 양방향 점프 각 5회와 착지 후 걷기·새 점프 시험을 추가한다; A→B는 (0,1,11)에서 W 도움닫기/Z=15.2 출발, B→A는 (0,1,25)에서 S 도움닫기/Z=20.8 출발로 실제 점프 위치·속도를 기록하고 비행 중 IsGliding=false 및 목적지 안정 지지를 단언한다; 점프 입력 유지만으로 자동 전개하지 않는지도 확인한다.
- [X] T010 [US1] `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`의 `US1_` 시험을 실행해 `TestResults/008-us1.xml`에 저장하고 결과 CSV에 각 시도를 남긴다; SC-002 10회 성공, 씬 배치·충돌 기준과 착지 후 조작을 확인한 뒤 W/S 양방향 최종 접근·점프·착지의 실제 화면을 `TestResults/008-camera-us1-*.png`에 캡처한다; 실패 시 씬/시험 원인을 수정하되 기존 물리 기준을 약화하지 않는다.

**Checkpoint**: US1을 독립 시연할 수 있다. 총 3개 섬이 있으나 긴 구간 이동·전체 인수 완료를 아직 주장하지 않는다.

## Phase 4: User Story 2 - 긴 구간을 글라이딩하고 목적지에 착지하기 (Priority: P1)

**Goal**: 긴 구간의 비전개 점프 실패와 글라이딩 왕복·유지 비행·접기 착지 및 여러 섬 연속 이동을 입증한다. 기존 글라이더 구현을 재작성하지 않는다.

**Independent Test**: US2 시험에서 B↔C 비전개 실패 각 5회와 글라이딩 착지 각 5회, 목적지 10초 유지 후 3초 내 착지 5회를 확인한다. 독립 초기 배치는 fixture로 준비할 수 있으므로 US1 시험 실행 결과에 의존하지 않는다. 연속 주행만 A의 최초 위치부터 실제 입력으로 시작한다.

### Tests for User Story 2

- [X] T011 [US2] `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`에 `US2_` 긴 구간 비전개 점프 실패 시험을 추가한다; B→C는 (1,1,26)에서 D/X=5.2, C→B는 (25,1,26)에서 A 키/X=20.8을 사용해 각 5회 전속력 점프를 수행하고 “실패란 목적지의 실제 상단 지지를 얻지 못한 채 표면 아래로 떨어지는 것이며 단순 시간 초과만으로 성공/실패를 추정하지 않는다”를 적용한다.
- [X] T012 [US2] `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`에 T011과 같은 출발 조건의 `US2_` 글라이딩 왕복 각 5회를 추가한다; 지상 점프→Space 해제→새 공중 Space 유지→방향 이동→목적지 중심 부근 제동→Space 해제→실제 목적지 착지를 거치고 날개 표시·중력 복원·후속 걷기·새 점프를 검증한다; T011의 비전개 실패 조건을 변경하지 않는다.
- [X] T013 [US2] `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`에 `US2_` 목적지 위 WASD만 해제 후 Space 유지 시험 5회를 추가한다; 목표에 안정화한 뒤 발 Y=2±0.1m를 10초 유지하고 Space 해제 후 3초 내 실제 목적지 착지, 자동 접기·순간 이동 없음 및 새 조작 가능을 확인한다; 목적지 위라는 이유로 목표 높이를 변경하지 않는지도 단언한다.
- [X] T014 [US2] `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`에 `US2_` 카메라 및 공중 방향 전환/지나친 목적지로 돌아오기 시험을 추가한다; 1280×720에서 4개 방향의 출발선 직전 약 1m 최종 접근·비행·착지에 플레이어와 이동 축 중앙선/목적지 가까운 가장자리 교차점을 측정해 z>0, x/y∈[0.02,0.98]을 확인하고 `TestResults/008-camera-us2-*.png`를 저장한다; 위치 주입·자동 전진 없이 기존 WASD로 복귀하며 기존 pitch=55°, distance=18, FOV=60°, sharpness=10이 유지되는지 검증한다.
- [X] T015 [US2] `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`에 `US2_` A→B→C→B→A 연속 주행 3회를 추가한다; 매 회 최초 시작점에서 실제 입력만 사용하고 각 섬의 안정 착지·걷기·새 점프, 이동 중 허위 접지/관통 없음 및 T014의 전 구간 가시성을 확인한다; 독립 시험용 초기 배치/속도 주입 보조 함수를 연속 주행 도중 호출하지 않는다.

### Integration for User Story 2

- [X] T016 [US2] `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`의 `US2_` 시험을 실행해 `TestResults/008-us2.xml`과 `TestResults/008-traversal-attempts.csv`에 결과를 남기고 SC-003~SC-005의 시도 수·시간·좌표를 검증한다; 실패하면 씬/시험 입력과 실제 렌더를 조사하고 `Assets/Scenes/SampleScene.unity`의 지형 배치 변경이 필요할 때 4m/14m·동일 상단·기존 낙사 통로 제약을 유지해 재검증한다; 카메라/이동 동작 변경이 필요한 경우 범위 재검토 없이 진행하지 않는다.

T016에서 카메라 시야 문제로 섬 배치를 조정하면 `specs/008-island-traversal/data-model.md`의 좌표, 시험의 도움닫기 시작점·출발선·제동 위치, T007~T015의 관련 좌표·검증값을 함께 갱신한다. 같은 높이, 4m/14m 간격, 기존 낙사 시험 공간과 이동 성능은 유지한다.

**Checkpoint**: 짧은/긴 구간 대비와 연속 이동이 재현된다. 자동 viewport 통과만으로 사람이 빈 공간을 식별했다는 결론을 내리지 않는다.

## Phase 5: User Story 3 - 이동 실패 후 시작 섬에서 반복 시험하기 (Priority: P2)

**Goal**: 다른 섬에서 출발한 이동 실패 뒤에도 최초 섬으로 돌아와 동일 배치에서 새 시도를 할 수 있다.

**Independent Test**: B/C 위의 독립 초기 조건에서 섬 사이로 이동·전개·접기·자연 낙사·새 R 복귀·새 점프/전개를 10회 반복한다. US2의 연속 주행 성공이 선행 조건은 아니다.

### Tests for User Story 3

- [X] T017 [US3] `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`에 `US3_` 원격 섬 출발 후 실제 빈 공간 접기·낙사·새 R·새 점프/공중 전개 반복 10회를 추가한다; “섬 방문이 시작 자세를 갱신하지 않는다. 경계와 같은 Y=-10은 그 이유만으로 탈락하지 않는다”를 보존하고 발 Y<-10 탈락·안내·조작 차단 및 R 후 2초 이내 최초 (0,1,0)/방향·중력/속도/날개 초기화를 확인한다; 초기와 첫 5회 재시작의 섬 변환/개수/안전 지지가 동일하며 플레이어·카메라 인스턴스가 늘어나지 않는지도 단언한다.
- [X] T018 [US3] `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`에 `US3_` 유지 입력·초점 상실 경계 시험을 추가한다; 지상에서 누른 Space를 착지까지 유지해도 글라이더가 자동 전개되거나 착지 후 다시 점프하지 않음을 검증한다; 공중에서 새로 누른 Space를 유지하면 목적지 위에서도 비행을 계속하고 Space를 해제해야 하강·착지하며 착지 후 새 누름으로만 점프함을 검증한다; 원격 이동 중 Space 해제와 새 누름 재전개, 탈락 중 유지한 Space의 R 복귀 후 자동 점프·전개 금지와 유지한 WASD의 기존 즉시 반영 규칙 보존, 초점 복귀만으로 자동 전개하지 않고 해제 후 새 입력에 반응함을 검증한다; 지지·낙사 경계와 비활성화 회귀는 기존 `Assets/Tests/PlayMode/GlideHeightTests.cs` 및 `PlayerInputReaderTests.cs`도 함께 유지한다.

### Integration for User Story 3

- [X] T019 [US3] `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`의 `US3_` 시험을 실행해 `TestResults/008-us3.xml`과 `TestResults/008-traversal-attempts.csv`에 기록하고 SC-001의 재시작 5회 및 SC-006의 실제 낙사/복귀 10회 조건을 대조한다; 안내 화면과 복귀 화면은 `TestResults/008-camera-us3-eliminated.png`, `TestResults/008-camera-us3-restored.png`에 남기고 유지 입력 자동 실행·잔류 비행·잘못된 시작 섬 복귀가 0회임을 확인한다.

**Checkpoint**: 모든 스토리를 각각 검증할 수 있고 실패 후 새 시도로 돌아오는 흐름이 유지된다.

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: 전체 회귀, 실제 사용자 관찰, 재현 가능한 최종 증거를 마무리한다.

- [X] T020 `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs` 전체와 기존 전체 PlayMode를 순차 실행해 `TestResults/008-traversal.xml`, `TestResults/008-playmode-all.xml`에 저장하고 T002의 `TestResults/008-baseline-playmode.xml`과 비교한다; 필터 0건·새 실패·예외 로그를 통과로 처리하지 않으며 기존 Jump/FallElimination/Restart/Glider/GlideHeight/MovementAndCamera/Input 회귀를 약화하지 않는다.
- [ ] T021 `specs/008-island-traversal/quickstart.md`의 실제 Game 뷰 검수를 관찰자와 수행하여 `TestResults/008-validation.md`에 관찰자·환경·짧은 점프/긴 글라이딩/접기 착지 구별 가능 여부, 양방향 목적지·빈 공간 가시성 및 `TestResults/008-camera-*.png` 참조를 기록한다; 사람을 확보하지 못했으면 SC-007의 관찰 항목을 미검증으로 남기고 이 작업을 완료 처리하지 않는다.
- [X] T022 [P] 실제 구현된 시험 이름·실행 명령·기록 경로와 지원 화면비를 `specs/008-island-traversal/quickstart.md`에 최종 반영하고 `specs/008-island-traversal/contracts/island-traversal.md` 및 `data-model.md`의 배치/조작/착지 제약과 대조한다; T020/T021의 결과 파일을 동시에 수정하거나 아직 없는 증거를 통과로 기술하지 않는다.
- [ ] T023 `TestResults/008-validation.md`에 SC-001~SC-007과 FR-001~FR-010별 통과/실패/미검증·시도 수·XML/CSV/화면 증거·남은 문제를 종합하고 `Assets/Scenes/SampleScene.unity`, `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`, `.cs.meta` 및 기능 문서 diff/공백을 검토한다; 기존 GUID·런타임/물리/카메라 설정의 불필요한 변경이 없는지 확인하고 `specs/008-island-traversal/tasks.md`에는 실제 완료 작업만 체크한다.

## Dependencies & Execution Order

### Phase Dependencies

```text
T001 → T002
         ├→ T003 ─┐
         └→ T004 ─┴→ T005 → T006   [Foundation]
                            ├→ T007 → T008 → T009 → T010  [US1 / MVP]
                            ├→ T011 → T012 → T013 → T014 → T015 → T016  [US2]
                            └→ T017 → T018 → T019  [US3]
US1 + US2 + US3 → T020 → T021 → T023
US1 + US2 + US3 → T022 ─────────→ T023
```

US2/US3에 실제 새 섬이 필요하므로 **T008은 T011 및 T017의 추가 선행 조건**이다. T014/T015의 짧은 구간 재사용은 T009의 도움 루틴을 사용한다. 검증 도구의 공통 helper 변경은 해당 스토리 시험을 다시 실행한다.

### User Story Dependencies

- **권장 완료 순서**: US1(P1) → US2(P1) → US3(P2) → 최종 검수. 전체 스토리가 같은 테스트 파일을 수정하므로 이 순서가 충돌을 피한다.
- US2는 US1의 **씬 추가 T008과 이동 helper T009**를 재사용하지만 US1 시험 결과가 있어야만 개별 시험을 실행할 수 있는 것은 아니다. B/C 출발은 fixture로 준비한다.
- US3는 **씬 추가 T008** 이후 B/C 출발을 독립 준비할 수 있다. US2 성공이나 A→B→C 연속 주행에 의존하지 않는다.
- T020 이후 T021은 같은 Editor를 사용하므로 순차 수행한다. T022 문서 보정은 별도 파일이므로 T020 또는 T021과 병렬 수행할 수 있다. T023은 모든 최종 결과를 취합한 뒤 진행한다.

### Within Each User Story

- 신규 씬 계약 시험 T007은 섬 추가 전 실패를 확인하고 T008 후 통과를 확인한다.
- 이미 존재하는 점프/글라이더/재시작 동작에 대한 시험은 정상 기반에서 바로 통과할 수 있다. 일부러 운영 코드를 망가뜨려 실패를 만들지 않는다.
- 조건만 맞춘 테스트 통과보다 실제 목적지 Collider·연속 지지·입력 흐름을 검증한다. 측정 허용치를 늘려 실패를 숨기지 않는다.
- 각 단계 체크포인트에서 독립 시험을 통과한 뒤 다음 단계로 진행한다.

### Parallel Opportunities

- **T003 ∥ T004**: 새 테스트 fixture와 quickstart 실행 가이드는 서로 다른 파일이다. T005 전에 두 결과를 합친다.
- **T022 ∥ T020 또는 T021**: 기존 결과를 읽으며 가이드만 보정한다. Editor 조작이나 결과 보고서 편집은 병렬 수행하지 않는다.
- 씬 수정과 테스트 수정을 겉보기만 병렬로 표시하지 않는다. T007→T008의 red/green 순서와 동일 테스트 클래스 편집 충돌 때문에 스토리 내부 구현에 `[P]`를 붙이지 않았다.

## Parallel Example: User Story 1

```text
실행 순서: T007(씬 계약 실패 확인) → T008(씬 추가) → T009(왕복 시험) → T010(검증).
동시 코드/씬 편집은 하지 않는다. 별도 검토자는 data-model.md의 A/B/C 좌표와
contracts/island-traversal.md의 씬 계약을 읽기 전용으로 대조할 수 있다.
```

## Parallel Example: User Story 2

```text
T011~T015는 IslandTraversalSceneTests.cs를 함께 수정하므로 순차 수행한다.
별도 검토자는 quickstart.md와 양방향 시험 조건을 읽기 전용으로 대조한다.
T016 실행 중 같은 Editor에서 다른 스토리 테스트/사람 플레이를 시작하지 않는다.
```

## Parallel Example: User Story 3

```text
T017 → T018 → T019를 순차 수행한다. 별도 검토자는 기존 입력/재시작 계약을
읽기 전용으로 검토할 수 있으나 같은 테스트 파일이나 결과 CSV를 동시에 쓰지 않는다.
병렬 편집이 꼭 필요하다는 이유로 새 런타임 모듈/분할 파일을 추가하지 않는다.
```

## Requirement Coverage

| 요구/성공 기준 | 관련 작업 |
| --- | --- |
| FR-001 / SC-001 지형·배치·안전 지지 | T007~T008, T017, T019 |
| FR-002 최초 위치·방향·동일 배치 | T001, T008, T017, T019 |
| FR-003 / SC-002~SC-003 짧은/긴 구간 대비 | T009~T012, T016 |
| FR-004 착지 후 걷기·새 점프 | T005, T009, T012, T015 |
| FR-005 실제 접촉·지지·옆면·관통 금지 | T005, T007, T009, T012, T015 |
| FR-006 / SC-004 최신 조작·높이·성능 보존 | T001, T009, T011~T013, T018, T020 |
| FR-007 접기 후 착지·이동 재개 | T012~T013, T015 |
| FR-008 / SC-005 카메라·연속 이동 | T010, T014~T016, T021 |
| FR-009 / SC-006 낙사·재시작·입력 경계 | T017~T020 |
| FR-010 / SC-007 절차·실제 기록·관찰 | T004, T006, T010, T016, T019~T023 |

## Implementation Strategy

### MVP First (User Story 1 Only)

1. T001~T006으로 기존 기준선과 시험 기반을 준비한다.
2. T007~T010으로 섬 3개 및 짧은 구간 점프 왕복을 완료한다.
3. 여기서 멈추고 US1을 독립 시연한다. 긴 구간은 존재하더라도 글라이딩·재시작 검수 완료로 보고하지 않는다.

### Incremental Delivery

1. US1: 섬 배치와 짧은 점프 왕복.
2. US2: 긴 구간 비전개 실패/글라이딩 성공, 목표 높이 유지, 접기 착지, 연속 주행.
3. US3: 원격 이동 실패와 최초 섬 복귀·입력 경계 반복.
4. 전체 회귀 및 사람 시각 검수 후 증거를 종합한다. 미검증 항목은 숨기지 않는다.

### Notes

- 2026-11-02 구현 상태: **21/23 완료**. 신규 클래스 10/10, 기존 포함 전체 PlayMode 169/169 통과. 기존 159개 시험의 누락·실패는 0개다. 마지막 전체 실행의 이동 기록 58건도 모두 통과했다. 결과는 `TestResults/008-validation.md`와 실제 XML/CSV에 기록했다.
- **T021은 사람 관찰자 검수 대기**이며 자동 결과나 AI 화면 확인으로 대체하지 않았다. **T023은 T021 이후 최종 인수 정리까지 대기**한다. 자동 검증 자료와 예비 보고서는 준비했지만 전체 인수 완료는 선언하지 않는다. 체크리스트 파일은 변경하지 않았다.
- 총 **23개**: Setup 2, Foundational 4, US1 4, US2 6, US3 3, Polish 4.
- `[P]` 3개: T003, T004, T022. 공유 씬/테스트 파일 때문에 스토리 구현은 의도적으로 순차다.
- 새 씬 객체와 테스트 `.meta`는 기존 자산 ID를 재사용하지 않는다. 저장소의 무관한 변경을 정리하거나 되돌리지 않는다.
- 이 작업 목록 생성은 GitHub 이슈 게시 요청이 아니므로 이 단계에서 외부 이슈를 등록하지 않는다.
