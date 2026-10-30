# Tasks: 탈락 후 재시작

**Input**: `specs/004-restart-after-elimination/`의 spec.md, plan.md, research.md, data-model.md, contracts/restart.md, quickstart.md

**Prerequisites**: 기존 Unity 6000.3.26f1 프로젝트와 이동·점프·낙사 구현. 프로젝트·패키지를 새로 만들거나 버전을 바꾸지 않는다.

**Tests**: 명세의 User Scenarios & Testing, SC-001~006 및 계획의 검증 설계에 따라 PlayMode 테스트를 포함한다. 새 동작은 먼저 실패를 확인한 후 구현한다. 명세·계약을 실제로 재현하는 시험이어야 하며 단순 필드 대입만으로 입력 동작을 통과시키지 않는다.

**Organization**: US1은 기본 재시작 MVP, US2는 의도하지 않은 재시작 방지다. 공통 준비 후 US1 → US2 순서로 진행한다. 모든 경로는 저장소 루트 기준이다.

## Format: `[ID] [P?] [Story] Description`

- `[P]`: 해당 단계의 선행 작업 완료 후 다른 파일 작업과 병렬 가능. 같은 Unity Editor에서 테스트를 동시에 실행한다는 뜻은 아니다.
- `[US1]`, `[US2]`: spec.md의 사용자 스토리와 연결한다.
- 완료 체크는 실제 구현·실행 증거가 있을 때만 표시한다. 환경 때문에 실행하지 못한 검증은 미완료로 남긴다.

## Phase 1: Setup — 기존 프로젝트 확인

**Purpose**: 현재 프로젝트와 테스트 기준선을 확인한다. 별도 초기화 스크립트나 패키지는 추가하지 않는다.

- [ ] T001 `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `Assets/Scripts/PlayerMovement.cs`, `Assets/Scripts/TopDownCameraFollow.cs` 및 `Assets/Tests/PlayMode/`를 확인하고 기존 세 테스트 클래스의 기준선 결과를 `specs/004-restart-after-elimination/quickstart.md`에 기록한다; Unity 6000.3.26f1과 기존 패키지를 유지하고 사용자 변경 `SkyBout.slnx`는 건드리지 않는다.

## Phase 2: Foundational — 공유 입력 자산

**Purpose**: 두 스토리가 사용하는 동일한 재시작 입력 경로를 준비한다.

- [ ] T002 `Assets/Player_Actions.inputactions`의 기존 Player 맵에 Restart 액션을 추가한다: “타입: Button, expectedControlType: Button”, “바인딩: `<Keyboard>/r`, 그룹: Keyboard&Mouse”, “initialStateCheck: false”, “기존 Move/Jump 액션 ID와 바인딩·제어 스킴은 유지한다.”; 기존 자산 GUID와 SendMessages 구성을 보존한다.

**Checkpoint**: 실제 입력 자산을 로드하는 기존 fixture가 Restart 액션을 포함한다. 아직 R 입력만으로 복귀하는 구현은 없어야 한다. 두 스토리는 이 단계 완료에 의존한다.

## Phase 3: User Story 1 — 탈락 후 같은 섬에서 다시 시작 (Priority: P1) — MVP

**Goal**: 탈락 후 R을 눌러 동일 플레이어가 최초 시작 자세에서 다시 이동·점프·추락을 시험한다. 안내와 탑뷰도 복귀한다.

**Independent Test**: SampleScene에서 자연 낙사 → 안내 확인 → 새 R 입력 → 시작 위치 복귀 → 무입력 2초 안정 → 이동·점프·착지 → 재탈락을 검증한다. 실행을 종료하거나 플레이어를 재생성하지 않는다.

### Tests for User Story 1

- [ ] T003 [P] [US1] `Assets/Tests/PlayMode/RestartTests.cs`에 기존 FallEliminationTests 패턴의 가상 Keyboard/Mouse·실제 액션 자산·수동 물리 스텝 fixture와 기본 복귀 시험을 추가한다; 최초 위치·회전 복원, 선속도·각속도 초기화, 실제 지지 재획득, 무입력 2초 안정 10회, 복귀 후 이동·새 점프·착지, 낙사 경계 바로 위·동일·아래 각 5회 및 Restart 누락 시 오류·비활성화를 검증한다; 카메라 없는 fixture도 복귀해야 하며 구현 전 실패를 확인한다.
- [ ] T004 [P] [US1] `Assets/Tests/PlayMode/RestartSceneTests.cs`에 실제 `Assets/Scenes/SampleScene.unity`를 한 번 로드한 뒤 같은 인스턴스로 20회 반복하는 시험과 Game 뷰 안내 시험을 추가한다; 초기 위치의 안전성, 시작 자세·설정·객체 수·카메라 타깃 불변, 안내 1초 이내/복귀 2초 이내 각각 10회, 화면 밖 안내 유지, 복귀 프레임 카메라 스냅과 안내 제거를 검증하고 구현 전 실패를 확인한다; 테스트 후 씬·장치·전역 설정을 복원한다.

### Implementation for User Story 1

- [ ] T005 [P] [US1] `Assets/Scripts/TopDownCameraFollow.cs`에 기존 설정 검증·오류 처리를 보존하는 `public void SnapToTarget()`을 추가하고 Start의 초기 배치와 공유한다; “초기 배치와 재시작은 같은 배치 계산을 사용하고 평상시 LateUpdate 보간은 유지한다.”를 준수한다.
- [ ] T006 [US1] `Assets/Scripts/PlayerMovement.cs`에서 Awake에 최초 Rigidbody 위치·회전을 보관하고 OnRestart(InputValue)의 요청을 FixedUpdate에서 한 번 적용한다; “최초 Rigidbody 월드 위치. 재시작마다 덮어쓰지 않는다.”, “최초 Rigidbody 월드 회전.”, “낙사로 true, 유효한 재시작 적용으로만 false. 읽기 전용 IsEliminated 유지.”, “동일 인스턴스 유지. 복귀 시 위치·회전 복원, linearVelocity·angularVelocity 모두 0.”을 준수한다; 지지 상태는 “복귀 시 캐시를 비우고 false로 시작하여 실제 접촉으로 다시 판정.”하고 기존 발 위치의 엄격한 낙사 비교를 유지한다; 필수 액션에 Restart를 검증하고 카메라 참조는 “SampleScene의 Main Camera. 카메라 없는 물리 테스트에서는 null 허용.”으로 연결하며 Rigidbody 보간·Transform 동기화 후 T005의 스냅을 호출한다.
- [ ] T007 [US1] `Assets/Scripts/PlayerMovement.cs`의 기존 OnGUI 고대비 패널에 `R: 재시작` 안내를 추가한다; 폰트 제약 시 계약에 정의된 `R: Restart`를 사용하고 탈락 동안 화면 밖에서도 읽을 수 있게 유지하며 생존 복귀 시 제거한다; 별도 HUD·메뉴·설정 화면은 추가하지 않는다.
- [ ] T008 [US1] `Assets/Scenes/SampleScene.unity`의 기존 PlayerMovement에 cameraFollow를 기존 Main Camera의 TopDownCameraFollow로 연결한다; 기존 플레이어·섬·카메라와 물리 설정은 유지하고 `Assets/Tests/PlayMode/RestartTests.cs.meta`, `Assets/Tests/PlayMode/RestartSceneTests.cs.meta`를 Unity가 생성하도록 한다.
- [ ] T009 [US1] `Assets/Tests/PlayMode/RestartTests.cs`와 `Assets/Tests/PlayMode/RestartSceneTests.cs`의 US1 시험을 실행하여 SC-001~003 및 복귀 후 낙사 경계 검증을 통과시키고 결과·실제 실행 환경을 `specs/004-restart-after-elimination/quickstart.md`에 기록한다; 렌더 시험을 headless 결과로 대체하지 않는다.

**Checkpoint**: 새 R 입력으로 기본 복귀·안내 제거·카메라 재배치·재탈락이 가능하고 실제 지지 판정이 유지된다. US1만으로 반복 시험의 MVP를 시연할 수 있다. US2 경계 검증 전에는 전체 기능 완료로 간주하지 않는다.

## Phase 4: User Story 2 — 의도하지 않은 재시작 없이 반복 시험 (Priority: P2)

**Goal**: 생존 R, 탈락 전 키 유지, 연타, 초점 복귀가 임의 초기화를 만들지 않는다. 유지 WASD는 재개하되 저장된 Space는 자동 실행하지 않는다.

**Independent Test**: 같은 플레이어에 생존 R, 탈락 전부터 R 유지, 탈락 후 R 유지·연타를 각각 5회 입력하여 오초기화 0회를 확인한다. 초점 전환과 낙사/R 충돌 후에도 새 R 입력으로만 복귀해야 한다.

### Tests for User Story 2

- [ ] T010 [P] [US2] `Assets/Tests/PlayMode/RestartTests.cs`에 생존 지상/공중 R 무시, 탈락 전부터 R 유지, 탈락 후 유지·연타, 같은 입력 갱신 내 press/release, 초점 상실·복귀, 비활성화·재활성화, 새 탈락과 R 입력 충돌, 요청 후 초점 상실, 유지 WASD 재개, 유지 Space 자동 점프 방지 및 해제 후 새 점프를 시험하는 사례를 추가하고 구현 전 실패를 확인한다; SC-004·006 경계는 각 5회, 탈락 중 WASD/Space 10초 차단은 5회 확인한다.
- [ ] T011 [P] [US2] `Assets/Tests/PlayMode/RestartSceneTests.cs`에 실제 씬에서 탈락 중 입력 초점 전환과 R·Space 유지 후 복귀를 검증하는 통합 사례를 추가한다; 의도하지 않은 복귀·점프가 없고 키를 놓은 뒤 새 입력은 정상 작동하는지 확인하며 구현 전 실패를 확인한다.

### Implementation for User Story 2

- [ ] T012 [US2] `Assets/Scripts/PlayerMovement.cs`의 요청·해제 관찰·초점 및 비활성화 경로를 보강한다; pendingRestart는 “탈락·활성·초점·새 누름 조건이 충족될 때만 예약. 실행·새 탈락·비활성화·초점 상실 시 제거.”하고 restartHeld/restartReadyAfterRelease는 “Jump와 동일한 방식으로 새 누름과 실제 해제를 구분. 초점 전환 후 유지 입력은 허용하지 않는다.”를 지킨다; 탈락 조기 반환 전 Restart 해제를 관찰하고 새 낙사는 요청보다 우선한다; 기존 입력 상태는 “저장된 움직임·점프 요청 제거. 현재 Move 값은 복귀 시 다시 읽고, Jump는 해제 후 새 누름만 허용.”하며 평상시 OnEnable/초점 복귀의 중립 입력 정책을 유지한다.
- [ ] T013 [US2] `Assets/Tests/PlayMode/RestartTests.cs` 및 `Assets/Tests/PlayMode/RestartSceneTests.cs`의 US2 시험과 US1 시험을 함께 실행하여 SC-004~006과 기존 복귀 동작을 확인하고 `specs/004-restart-after-elimination/quickstart.md`에 결과를 기록한다; 생존 중 요청이 남거나 같은 물리 판정에서 새 탈락이 취소되는 회귀가 없어야 한다.

**Checkpoint**: US1 기본 흐름과 US2 입력 경계를 각각 시험할 수 있고 모두 통과한다. US2는 같은 PlayerMovement 복귀 경로를 보강하므로 US1 완료에 의존한다.

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: 기존 이동·점프·낙사와 전체 완료 기준을 검증한다. 요구하지 않은 정리·최적화·기능 추가는 하지 않는다.

- [ ] T014 `Assets/Tests/PlayMode/MovementAndCameraTests.cs`, `Assets/Tests/PlayMode/JumpTests.cs`, `Assets/Tests/PlayMode/FallEliminationTests.cs`를 전체 실행하고 Restart 필수 액션 추가로 실제 깨진 fixture·오류 메시지 기대값만 필요한 경우 수정한다; R 미입력 시 탈락 유지, 기존 지지·점프·초점·카메라 검증은 삭제하거나 약화하지 않는다; 결과를 `specs/004-restart-after-elimination/quickstart.md`에 기록한다.
- [ ] T015 `specs/004-restart-after-elimination/quickstart.md`의 수동 절차를 실제 Game 뷰에서 수행하여 작은 뷰의 안내 가독성, 화면 밖 탈락, 복귀 프레임 시점, 무입력 안정성 및 동일 실행 20회 반복을 확인하고 SC-001~006별 증거·시간·결과를 해당 문서에 기록한다; 기존 시각 통합 시험도 Editor에서 확인한다.
- [ ] T016 `specs/004-restart-after-elimination/quickstart.md`의 계획 단계 안내와 실행 명령을 실제 구현에 맞게 갱신하고 `specs/004-restart-after-elimination/tasks.md`의 완료 상태를 증거와 대조한다; `Assets/Player_Actions.inputactions`, `Assets/Scenes/SampleScene.unity`, `Assets/Scripts/`, `Assets/Tests/PlayMode/`의 최종 diff와 .meta 포함 여부를 확인하며 사용자 변경·자산 GUID·기존 설정 보존 및 `git diff --check`를 검증한다.

## Dependencies & Execution Order

### Phase Dependencies

```text
T001 → T002
         ↓
      T003 ∥ T004  (실패하는 시험 작성)
         ↓
      T005 → T006 → T007 → T008 → T009  (US1 MVP)
                                   ↓
                              T010 ∥ T011  (추가 실패 시험)
                                   ↓
                              T012 → T013  (US2)
                                   ↓
                              T014 → T015 → T016
```

- T005는 독립 파일 변경이므로 T003/T004 작성과 병렬로 진행할 수 있다. 단, 각 동작의 시험 실패 확인은 해당 구현 적용 전에 마친다.
- T006은 T003/T004의 기본 동작 실패 확인과 T005 완료에 의존한다. 같은 PlayerMovement를 수정하는 T006/T007/T012는 병렬 처리하지 않는다.
- T008은 T006의 직렬화 필드 추가 후 수행한다. Unity 씬 편집·임포트·테스트 실행은 같은 프로젝트에서 직렬화한다.
- US2는 US1 복귀 경로를 사용한다. US1과 US2를 병렬 구현한다고 가정하지 않는다.
- T014~T016은 US1·US2 완료 후 수행한다. 기존 회귀 실패가 발견되면 전체 완료 전에 해결한다.

### User Story Dependencies

- **US1 (P1)**: Phase 1~2 후 시작. 다른 스토리 의존 없음. 단독 기본 흐름 시연 가능.
- **US2 (P2)**: US1 후 시작. 별도 입력 경계 시험으로 독립 검증하지만 복귀 구현은 공유한다.

## Parallel Example: User Story 1

```text
공유 입력 준비(T002) 후, 서로 다른 파일에서:
- T003: RestartTests.cs에 비시각 복귀·물리·검증 시험 작성
- T004: RestartSceneTests.cs에 실제 씬·안내·시점 시험 작성

시험 작성과 병렬 가능한 독립 파일 작업:
- T005: TopDownCameraFollow.cs의 즉시 배치 함수 추출
```

카메라 구현이 포함된 시험은 적용 전 실패를 먼저 확인한다. 여러 작업자가 같은 Unity Editor를 동시에 제어하지 않는다.

## Parallel Example: User Story 2

```text
US1 완료(T009) 후, 서로 다른 파일에서:
- T010: RestartTests.cs에 입력 갱신·판정 순서 경계 시험 작성
- T011: RestartSceneTests.cs에 실제 씬의 초점·유지 입력 시험 작성

두 시험의 실패 확인 후:
- T012: PlayerMovement.cs를 한 작업자가 수정
```

## Implementation Strategy

### MVP First — US1

1. T001~T002로 기존 프로젝트와 입력 기반을 준비한다.
2. T003~T004에서 새 동작 실패를 확인한다.
3. T005~T008로 복귀·카메라·안내·씬 연결을 구현한다.
4. T009에서 기본 재시작과 반복 흐름을 검증한다. 이 범위를 최소 시연 가능한 MVP로 삼는다.

### Incremental Delivery

1. US1 검증 후 US2의 경계 시험을 추가하고 실패를 확인한다.
2. T012로 기존 공유 경로를 최소 보강하고 T013에서 두 스토리를 함께 검증한다.
3. 기존 세 클래스 회귀 및 실제 Game 뷰 검증까지 마친 후 전체 기능 완료를 판단한다.
4. 새 관리자·추상 계층·패키지·씬·네트워크·글라이더는 추가하지 않는다.

## Requirement Coverage

| 요구사항/성과 | 구현 및 검증 작업 |
| --- | --- |
| FR-001 안내 | T004, T007, T009, T015 |
| FR-002 명시적 새 R 입력 | T006, T010~T013 |
| FR-003~004 시작 자세·상태 초기화 | T003, T006, T009 |
| FR-005 조작·카메라 복귀 | T003~T006, T008~T009 |
| FR-006 낙사 경계 유지 | T003, T006, T014 |
| FR-007 동일 실행 반복·설정 보존 | T004, T006, T008~T009, T015~T016 |
| FR-008 탈락 조작 차단·점프/이동 입력 | T010~T014 |
| FR-009 새 탈락 우선 | T010, T012~T013 |
| SC-001~003 안내 시간·복귀 안정·20회 반복 | T003~T004, T009, T015 |
| SC-004~006 입력 경계·낙사 경계·조작 차단 | T003, T010~T013, T015 |

## Notes

- 총 16개: Setup 1개, Foundational 1개, US1 7개, US2 4개, 최종 검증 3개.
- 모든 작업은 미완료 상태로 생성한다. 이번 문서는 작업 생성 결과이며 기능 구현·시험 통과 기록이 아니다.
- Constitution은 미작성 템플릿이므로 새 원칙을 임의로 도입하지 않는다.
- 실패한 시험의 원인을 확인한 후 최소 수정한다. 과거 기능 명세·완료 기록을 소급 변경하지 않는다.
