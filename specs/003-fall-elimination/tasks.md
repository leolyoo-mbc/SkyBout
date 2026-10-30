# Tasks: 하늘섬 추락과 낙사 탈락

**Input**: `specs/003-fall-elimination/`의 [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/player-fall-elimination.md](contracts/player-fall-elimination.md), [quickstart.md](quickstart.md)

**Scope**: 현재 Unity 6000.3.26f1 프로젝트 확장. 3단계 추락·낙사 탈락만 구현한다. 재시작·글라이더·비행 높이 보정·온라인·새 프로젝트·새 패키지는 제외한다.

**Tests**: 명세의 SC-001~SC-005와 계획에 명시된 PlayMode·실제 씬 검증을 포함한다. 새 낙사 로직은 시험을 먼저 작성해 실패를 확인한다. 기존 코드가 이미 충족하는 추락 시험은 억지로 실패시키거나 재구현하지 않는다.

**Organization**: US1과 US2는 명세의 두 P1 사용자 흐름이다. 같은 런타임·시험 파일을 공유하므로 기본 실행은 순차다. 모든 체크박스는 미완료이며 작업 생성 시 기능 구현·시험 실행을 수행하지 않았다.

## Format: `[ID] [P?] [Story] Description`

- `[P]`: 명시된 선행 작업 이후 다른 파일 작업과 병렬 가능. 같은 파일 편집과 같은 Editor의 시험 실행은 직렬화한다.
- `[US1]`: 섬 밖으로 나가면 추락. `[US2]`: 낙사 높이 아래에서 탈락.
- 파일 경로는 저장소 루트 `D:/Projects/SkyBout` 기준이다.

## Phase 1: Setup — 기존 프로젝트와 기준 결과 확인

**Purpose**: 새 프로젝트를 만들지 않고 현재 코드·설정·사용자 변경을 보호한다.

- [ ] T001 `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `Assets/Scenes/SampleScene.unity`, `Assets/Scripts/PlayerMovement.cs`와 두 기존 PlayMode 테스트의 현재 상태를 확인하고, `SkyBout.slnx` 사용자 변경 및 보존 대상 입력 자산·메타·설정의 diff/해시를 `specs/003-fall-elimination/quickstart.md`에 기록한다. Unity 6000.3.26f1과 현재 D: 프로젝트 경로를 사용하며 패키지를 추가하지 않는다.
- [ ] T002 `Assets/Tests/PlayMode/JumpTests.cs`와 `Assets/Tests/PlayMode/MovementAndCameraTests.cs`의 기존 시험을 실행하고 컴파일·실패 목록과 기준 결과를 `specs/003-fall-elimination/quickstart.md`에 기록한다. 과거 단계의 통과 기록으로 대체하지 않는다.

**Checkpoint**: 실행 환경과 기존 실패 여부가 확인되었고 사용자 변경의 보존 기준이 확보되었다.

## Phase 2: Foundational — 공유 시험 기반

**Purpose**: 두 사용자 흐름을 검증할 최소 픽스처만 준비한다. 별도 상태 서비스나 테스트 프레임워크 리팩터링은 하지 않는다.

- [ ] T003 `Assets/Tests/PlayMode/FallEliminationTests.cs`에 기존 픽스처 패턴을 따르는 플레이어·Ground·실제 Player_Actions 기반 가상 키보드·수동 물리 Step·새 인스턴스 생성/정리 헬퍼를 준비하고 `Assets/Tests/PlayMode/FallEliminationTests.cs.meta`를 Unity로 생성한다. 기존 `SkyBout.PlayModeTests.asmdef`를 그대로 사용하고 각 시험 종료 시 Physics/Input 설정, 씬, 가상 장치와 임시 오브젝트를 복구한다. 새 낙사 API 없이 픽스처 생성·정리부터 실행 가능하게 한다.

**Checkpoint**: 기능 시험은 새 인스턴스로 격리할 수 있고 공유 전역 설정이 누출되지 않는다.

## Phase 3: User Story 1 — 섬 밖으로 나가면 추락 (Priority: P1)

**Goal**: 걷기·점프로 섬 밖에 나가면 실제로 추락하고, 아직 표면 위로 돌아올 수 있다면 정상 착지한다. 공중 Space는 추가 점프·글라이더를 만들지 않는다.

**Independent Test**: 섬의 네 가장자리에서 걷기/점프 각 5회(총 40회), 표면보다 위에 있는 외측 점프에서 복귀 착지 5회, 중앙 점프·착지 10회, 섬 밖 Space 반복 5회. 낙사 상태나 안내가 아직 없어도 이 흐름을 검증할 수 있다.

### Tests for User Story 1

- [ ] T004 [US1] `Assets/Tests/PlayMode/FallEliminationTests.cs`에 네 가장자리 걷기/점프 각 5회, 표면 위에서 수평 복귀 착지 5회와 착지 후 이동·재점프, 중앙 점프·착지 10회, 섬 밖 Space 반복 5회 시험을 추가한다. 옆면·허공 비접지와 Y 중력 보존을 확인하고, 이 단계에서는 발 Y가 -10에 도달하기 전에 관찰을 끝내 새 낙사 상태에 의존하지 않도록 한다(SC-001, SC-004).

### Implementation and Validation for User Story 1

- [ ] T005 [US1] T004 결과를 기준으로 `Assets/Scripts/PlayerMovement.cs`의 기존 중력·FootPosition·접촉 기반 HasGroundSupport·공중 조작을 재사용한다. 실패가 있을 때만 해당 함수의 모든 호출부와 `Assets/Tests/PlayMode/JumpTests.cs`를 확인해 최소 수정하고, 이미 통과하면 런타임 변경 없이 재사용 결정을 기록한다. 새 접지·중력·킬 트리거를 추가하지 않는다.
- [ ] T006 [US1] `Assets/Tests/PlayMode/FallEliminationTests.cs`의 US1 시험과 두 기존 테스트군을 실행해 허공 착지·경계 차단·추가 점프 0회를 확인하고 횟수·결과를 `specs/003-fall-elimination/quickstart.md`에 기록한다.

**Checkpoint**: US1을 독립 시연할 수 있다. 이는 추락 동작의 중간 검증점이며 낙사까지 요구한 이번 기능의 최종 완료는 아니다.

## Phase 4: User Story 2 — 낙사 높이 아래에서 탈락 (Priority: P1)

**Goal**: 발이 낙사 경계 아래로 내려가면 한 번만 탈락하고, 플레이 입력이 차단되며 화면 중앙 안내가 유지된다.

**Independent Test**: 새 플레이어를 경계 위·동일·아래에 배치하고, 고속 하강·반복 입력·초점 복귀·재활성화 및 실제 씬 안내를 확인한다. US1의 40회 전체 시험을 먼저 반복하지 않아도 독립 실행 가능하다.

### Tests for User Story 2 — 구현 전 실패 확인

- [ ] T007 [US2] `Assets/Tests/PlayMode/FallEliminationTests.cs`에 발 Y 기준 경계 위·동일·아래 각 5회, 고속 경계 통과 5회, -10/-20 각각 5회, NaN/양·음 Infinity 거부 시험을 추가해 새 로직 부재의 실패를 확인한다. 정확한 동일 높이는 중력 적분 전 FixedUpdate 판정을 검사하고 고속 통과는 실제 Physics.Simulate로 분리한다. 유한한 0/양수도 허용함을 높인 지면 픽스처에서 검사한다(SC-002, SC-005).
- [ ] T008 [US2] `Assets/Tests/PlayMode/FallEliminationTests.cs`에 탈락 10회와 각 10초간 WASD/Space 입력 차단, 탈락 직전 보류 점프 폐기, 같은 틱의 접지·점프보다 탈락 우선, 수평 속도·지지 캐시 초기화 및 반복 틱 후 상태 유지를 검사하는 실패 시험을 추가한다. 동일 인스턴스의 위치·경계 변경, 초점 복귀·키 해제/재누름·컴포넌트 재활성화로 부활하지 않으며 새 인스턴스만 생존으로 시작함을 확인한다(SC-003).

### Implementation for User Story 2

- [ ] T009 [US2] `Assets/Scripts/PlayerMovement.cs`에 `fallDeathHeight`, `isEliminated`, 읽기 전용 `IsEliminated`를 추가한다. 모델 제약 “월드 Y 기준. 기본 -10m. 유한 실수만 허용하며 음수 전용 제한은 두지 않는다.”와 “인스턴스가 생성될 때 생존. 한 번 true가 되면 같은 인스턴스에서 false로 바꾸지 않는다.”를 적용하고 NaN/양·음 Infinity는 기존 설정 진단 경로로 거부한다. 기존 필드명을 유지하며 리셋 API는 만들지 않는다.
- [ ] T010 [US2] `Assets/Scripts/PlayerMovement.cs`의 FixedUpdate 첫 부분에 기존 FootPosition의 Y와 `fallDeathHeight`를 엄격한 `<`로 비교하는 탈락 판정을 추가한다. 모델 제약 “이 검사를 물리 틱의 점프·이동 처리보다 먼저 수행한다.”와 “탈락 진입 시 비우고 탈락 후 새 지지 상태를 부여하지 않는다.”를 적용한다. 한 번만 진입하며 기존 ResetJumpInput/StopHorizontalMovement를 재사용해 입력·보류 점프·grounded·지지 캐시를 초기화하고, 이후 수평 속도 0을 유지하되 수직 중력을 보존한다.
- [ ] T011 [US2] `Assets/Scripts/PlayerMovement.cs`의 OnMove, OnJump, 접촉 갱신과 활성/초점 처리 경로를 탈락 래치에 맞게 방어한다. 모델 제약 “초점 복귀·키 해제/재누름·높이 설정 변경·컴포넌트 재활성화·발 위치 변경은 부활 조건이 아니다.”를 지키고 SendMessages 입력만으로 이동·점프 요청이 생기지 않게 한다. 기존 생존 입력 규칙은 유지하고 탈락 처리로 컴포넌트를 비활성화하거나 Player를 파괴하지 않는다.
- [ ] T012 [P] [US2] T011 이후 `Assets/Scripts/PlayerMovement.cs`에 탈락 상태에서만 표시되는 OnGUI 중앙 고대비 패널과 `ELIMINATED` 문구를 추가한다. 16:9/4:3에서 화면 안에 읽기 쉬운 크기로 배치하며 버튼·재시작 안내·새 폰트 자산은 추가하지 않는다. `shortcut: 로컬 1인 프로토타입 안내, 실제 HUD·다국어 도입 시 UI 체계로 이전` 주석을 남기고 월드·카메라 가시성과 독립적으로 표시한다.
- [ ] T013 [P] [US2] T009 이후 `Assets/Scenes/SampleScene.unity`의 기존 PlayerMovement에 `fallDeathHeight: -10`을 저장한다. 모델 제약 “SampleScene의 초기값은 시작 표면 Y=0보다 낮아야 한다.”를 확인하고 Player 시작 (0,1,0), 발 오프셋 -1, 기존 Rigidbody·액션·카메라·GUID 연결을 유지한다. 새 바닥 Collider나 UI 오브젝트는 만들지 않는다.
- [ ] T014 [P] [US2] T011 이후 `Assets/Tests/PlayMode/JumpTests.cs`의 SampleScene 자연 수명주기 시험에서 추락 뒤 같은 인스턴스로 옆면 검사를 수행하는 구간을 확인한다. 이미 탈락했다면 정상 생존 접지를 기대하지 않도록 새 씬 인스턴스로 격리하거나 경계 위 관찰 구간으로 제한한다. 변경이 불필요하면 그 근거를 기록하고, 테스트용 부활 API·낙사 비활성화·약해진 접지 검사는 추가하지 않는다.
- [ ] T015 [US2] T012~T014 이후 `Assets/Tests/PlayMode/FallEliminationTests.cs`에 실제 SampleScene을 로드하는 자연 수명주기 통합 시험을 추가한다. Update/FixedUpdate/LateUpdate 직접 호출 없이 안전한 생존 시작, 발 기준 경계 아래 탈락, 입력 차단, 씬 재로드 후 새 생존 인스턴스와 기존 카메라 참조 유지를 검사한다. OnGUI의 실제 표시 증거는 별도 Game 뷰 검증에 맡기며 Camera.Render만으로 통과 처리하지 않는다.
- [ ] T016 [US2] `Assets/Tests/PlayMode/FallEliminationTests.cs`의 모든 US2 단위형·통합 시험을 실행하고 실패를 해결한다. `Assets/Scripts/PlayerMovement.cs` 및 `Assets/Scenes/SampleScene.unity`의 비유한 값 진단·탈락 우선·래치 유지·초기 높이 연결 결과와 SC-002/003/005 측정치를 `specs/003-fall-elimination/quickstart.md`에 기록한다.

**Checkpoint**: US2를 새 인스턴스에서 독립 검증할 수 있고 US1을 포함한 기능은 최종 교차 검증 준비가 되었다.

## Phase 5: Polish & Cross-Cutting Concerns — 최종 증거와 보존 확인

- [ ] T017 `specs/003-fall-elimination/quickstart.md`의 실제 Game 뷰 절차로 생존 중 안내 숨김, 탈락 10회 각각 판정 후 1초 이내 안내, 16:9/4:3 중앙 배치·가독성·대비, 플레이어가 카메라 밖이어도 안내 유지를 확인한다. Game 뷰 캡처와 판정→표시 시간 기록을 로컬 `TestResults/`에 저장하고 증거 위치·결과를 같은 quickstart.md에 기록한다. 임시 카메라 변경은 복구하고 미실행 항목은 명시한다.
- [ ] T018 `Assets/Tests/PlayMode/SkyBout.PlayModeTests.asmdef` 전체 PlayMode 시험을 실행하고 컴파일 오류·실패·경고를 확인한다. `specs/003-fall-elimination/quickstart.md`에 SC-001~SC-005 표본 충족 여부, 최종 결과 파일, 기존 이동·점프·카메라 회귀 결과를 기록한다. 배치 실행이 필요하면 가이드 명령을 사용하고 같은 프로젝트의 실행 중 Editor를 중복 실행하지 않는다.
- [ ] T019 `Assets/Scripts/PlayerMovement.cs`, `Assets/Scenes/SampleScene.unity`, `Assets/Tests/PlayMode/`, `Assets/Player_Actions.inputactions`, `Packages/manifest.json`, `ProjectSettings/`, `SkyBout.slnx`의 최종 diff·메타·보존 기준을 대조하고 결과를 `specs/003-fall-elimination/quickstart.md`에 기록한다. 무관한 설정 변경·생성물을 제외하되 기존 사용자 변경은 되돌리지 않는다. 재시작·글라이더·온라인·성능 벤치마크를 이번 완료 범위에 추가하지 않는다.

## Dependencies & Execution Order

### Phase Dependencies

```text
Phase 1 (T001 → T002)
  → Phase 2 (T003)
    → Phase 3 / US1 (T004 → T005 → T006)
      → Phase 4 / US2 (T007 → T008 → T009 → T010 → T011)
          ├─ T012 화면 안내
          ├─ T013 씬 값 저장 (T009부터 착수 가능)
          └─ T014 기존 씬 시험 격리
        → T015 → T016
          → Phase 5 (T017 → T018 → T019)
```

- US1과 US2 모두 P1이다. 위 순서는 같은 파일 충돌을 줄이기 위한 권장 통합 순서이지 US2가 US1의 전체 40회 시험에 기능적으로 의존한다는 뜻은 아니다.
- US2의 시험·핵심 구현은 T003 이후 독립 착수할 수 있지만, US1과 동시에 `FallEliminationTests.cs` 또는 `PlayerMovement.cs`를 수정하지 않는다.
- T007/T008은 T009~T011 전에 새 기능 부재의 실패를 확인한다. 컴파일 불가능한 테스트 파일을 방치하지 말고 기존 Reflection 패턴을 이용하거나 필요한 관찰 API가 생기는 즉시 실행한다.
- T012/T013/T014는 명시된 선행 작업 이후 서로 다른 파일이므로 병렬 편집 가능하다. Unity 임포트·PlayMode 시험은 병렬 실행하지 않는다.
- T015는 화면·씬·기존 시험 변경을 통합한 뒤 수행한다. T017~T019는 두 사용자 흐름 모두 완료되어야 시작한다.

### Parallel Example: User Story 1

US1의 시험 작성(T004), 최소 수정 판단(T005), 실행 기록(T006)은 선행 결과와 같은 파일에 의존하므로 직렬 실행한다. 억지로 `[P]`를 붙이거나 병렬화를 위해 파일을 나누지 않는다.

```text
T004: Assets/Tests/PlayMode/FallEliminationTests.cs 시험 작성
  → T005: Assets/Scripts/PlayerMovement.cs 재사용·최소 수정
    → T006: 시험 실행·quickstart.md 기록
```

### Parallel Example: User Story 2

T011 완료 후 아래 편집 세 개를 병렬 실행할 수 있다. 합친 뒤 T015 통합 시험을 작성·실행한다.

```text
담당 A / T012: Assets/Scripts/PlayerMovement.cs 화면 안내
담당 B / T013: Assets/Scenes/SampleScene.unity 낙사 높이 저장
담당 C / T014: Assets/Tests/PlayMode/JumpTests.cs 기존 시험 격리
```

## Coverage & Independent Completion

| 요구사항/성과 | 주요 작업 |
| --- | --- |
| FR-001~003, SC-001 | T004~T006, T018 |
| FR-004~005, SC-002 | T007, T009~T010, T015~T016 |
| FR-006, SC-003 입력·상태 | T008~T011, T015~T016 |
| FR-007, SC-003 안내 | T012, T017 |
| FR-008, SC-005 높이 조정 | T007, T009, T013, T015~T016 |
| FR-009~010, SC-004, SC-005 카메라·화면 | T004~T006, T011~T015, T017~T018 |
| 사용자 변경·기존 자산 보존 | T001, T013~T014, T019 |

## Implementation Strategy

### MVP First

1. T001~T003으로 기존 프로젝트와 최소 시험 기반을 준비한다.
2. T004~T006으로 **US1만 독립 검증하는 첫 체크포인트**를 만든다. 기존 추락이 이미 통과하면 코드를 늘리지 않는다.
3. 이번 요청의 실용적인 최소 완료 범위는 **US1 + US2**다. US1만으로는 명세의 낙사 탈락을 충족하지 못하므로 완료로 보고하지 않는다.

### Incremental Delivery

1. US1: 기존 추락·복귀 착지·공중 입력 경계를 확정한다.
2. US2: 경계·상태·입력 차단을 시험 우선으로 구현한 뒤 안내·씬 연결을 통합한다.
3. 최종 단계: 실제 Game 뷰와 전체 회귀를 확인하고 검증 증거·미실행 항목을 기록한다.

## Notes

- 현재 헌법은 미작성 템플릿이며 새로운 원칙을 임의로 추가하지 않는다.
- 같은 인스턴스의 탈락 상태를 시험 편의상 초기화하지 않는다. 반복 시험에는 새 인스턴스/새 씬을 사용한다.
- 성능 최적화·추가 UI·재시작·장시간 관전용 추락체 정리 등은 추가하지 않는다.
- 런타임 신규 파일·새 패키지·어셈블리 개편 없이 기존 구조를 유지한다. 신규 파일은 기능 시험과 Unity 메타만 필요하다.
