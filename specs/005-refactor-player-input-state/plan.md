# Implementation Plan: 플레이어 입력·상태 처리 리팩토링

**Branch**: `main` | **Date**: 2026-10-30 | **Spec**: [spec.md](spec.md)

**Input**: 사용자 지시: Player Input 컴포넌트와 SendMessages 의존성을 제거하고 Generate C# Class와 입력 이벤트로 대체한다. 상호 배타적 상태는 Enum으로 통합하고 C# 8.0 switch 패턴 매칭으로 FixedUpdate를 정리한다. 독립 입력 정보·기존 동작을 보존하고 씬·테스트도 갱신한다.

**Feature directory**: `specs/005-refactor-player-input-state`. setup-plan.ps1의 BRANCH 출력은 기능 식별자이며 실제 Git 브랜치는 `main`이다. 브랜치를 생성·전환하지 않았다.

## Summary

기존 Player_Actions 자산에서 `PlayerInputActions` C# 클래스를 생성하고 PlayerMovement가 인스턴스·이벤트·Enable/Disable/Dispose를 직접 관리한다. PlayerInput 컴포넌트는 SampleScene에서 실제 삭제하고 코드·테스트의 관련 의존성도 제거한다. grounded와 isEliminated는 `Grounded / Airborne / Eliminated` 단일 Enum으로 교체한다. FixedUpdate는 낙사 우선 순서를 지키는 C# 8.0 tuple pattern·when guard와 switch expression으로 정리한다. 독립적인 초점·중립·요청·실제 키 해제 정보는 유지한다. 생성 경로를 기존 런타임 asmdef 아래에 두어 새 어셈블리·패키지·입력 서비스 없이 완성한다.

## Technical Context

**Language/Version**: C# / Unity 6000.3.26f1. 현재 생성 csproj의 C# 9.0 환경에서 C# 8.0 패턴 문법 사용. csproj·언어 버전 직접 수정 없음.

**Primary Dependencies**: 기존 Input System 1.20.1, Rigidbody/Physics, TopDownCameraFollow, IMGUI, 기존 URP. PlayerInput은 제거 대상이며 새 패키지는 없다.

**Storage**: 런타임 상태·최초 자세만 메모리에 보관. 원본 inputactions와 자동 생성 C# 및 meta는 저장소에서 관리. 영구 데이터 없음.

**Testing**: Unity Test Framework, NUnit/UnityTest, 기존 5개 PlayMode 클래스와 필요한 최소 신규 회귀 시험. 가상 Keyboard/Mouse·수동 Physics.Simulate·실제 SampleScene 렌더를 유지.

**Target Platform**: Windows Unity Editor, 로컬 키보드 플레이어 1명.

**Project Type**: 기존 Unity 3D 탑뷰 게임의 동작 보존 리팩토링.

**Performance Goals**: 기존 조작감·점프 높이 ±5% 유지, 탈락 안내 1초 이내, 새 R 후 2초 이내 조작, 동일 실행 20회 복귀와 활성화 전환 후 중복 처리 0회. 불필요한 프레임별 객체 생성·입력 자산 재생성 없음.

**Constraints**: 기존 GUID·직렬화 값·배치·물리 및 초점 정책 보존. 생성 파일 수동 편집 금지. 호환용 PlayerInput 또는 중복 상태 bool을 남기지 않음. 독립 입력 정보를 단일 Enum에 강제로 넣지 않음. 사용자 변경 `SkyBout.slnx` 보존.

**Scale/Scope**: 기존 씬 1개, PlayerMovement 중심, 생성 클래스 1개, 기존 테스트 5개 클래스. 글라이더·온라인·입력 장치 자동 전환·새 관리자·UI 확장 제외.

## Constitution Check

*GATE: Phase 0 전과 Phase 1 후 확인.*

`.specify/memory/constitution.md`는 미작성 템플릿이며 확정된 원칙·버전이 없다. 아래는 임의의 헌장 조항이 아니라 사용자 요구·명세에 대한 설계 점검이다.

| 점검 | 설계 전 | 설계 후 |
| --- | --- | --- |
| 기존 플레이 동작·설정 보존 | PASS | PASS — 기존 물리·지지·초점 순서와 인수 기준 유지 |
| PlayerInput과 입력 SendMessages 완전 제거 | PASS | PASS — 씬·런타임·다섯 테스트 이행 범위 명시 |
| 최소 변경·기존 어셈블리 재사용 | PASS | PASS — 생성 코드만 Scripts/Input에 추가, 새 서비스·패키지 없음 |
| 독립 입력 정보 보존 | PASS | PASS — Enum은 지상·공중·탈락만, 요청·초점·해제 별도 |
| 검증 근거와 미완료 구분 | PASS | PASS — 기준선 재실행 예정, 004 미완료 검수 유지 |

차단 위반·미해결 설계 질문 없음. PASS는 설계 검토 결과이며 시험 통과가 아니다.

## Project Structure

### Documentation (this feature)

```text
specs/005-refactor-player-input-state/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── contracts/
│   └── player-input.md
├── quickstart.md
└── checklists/requirements.md
```

`tasks.md`는 다음 `/speckit.tasks`에서 생성한다. 이번 명령은 Phase 1 설계까지만 수행한다.

### Source Code (repository root)

```text
Assets/
├── Player_Actions.inputactions          # 내용/ID 보존
├── Player_Actions.inputactions.meta     # Generate C# Class 설정
├── Scenes/SampleScene.unity             # PlayerInput 컴포넌트/목록 참조 삭제
├── Scripts/
│   ├── Input/PlayerInputActions.cs      # Unity 자동 생성 + 폴더/파일 meta
│   ├── PlayerMovement.cs               # 생성 입력 소유권·이벤트·Enum·switch
│   ├── TopDownCameraFollow.cs           # 기존 SnapToTarget 사용, 변경 불필요
│   └── SkyBout.Runtime.asmdef           # 기존 Unity.InputSystem 참조 유지
└── Tests/PlayMode/
    ├── MovementAndCameraTests.cs
    ├── JumpTests.cs
    ├── FallEliminationTests.cs
    ├── RestartTests.cs
    ├── RestartSceneTests.cs
    ├── PlayerInputRefactorTests.cs      # 필요한 최소 신규 계약/회귀 검증 + meta
    └── SkyBout.PlayModeTests.asmdef     # 기존 사용
```

**Structure Decision**: 기존 전역 MonoBehaviour 구조와 테스트 fixture를 유지한다. 루트 Assets의 생성 코드는 런타임 asmdef에서 참조할 수 없으므로 Scripts 아래에 생성한다. 필요 없는 공통 입력 래퍼·FSM 클래스 계층·테스트 공개 주입 API를 만들지 않는다. Unity가 신규 meta를 생성하며 기존 자산 GUID는 유지한다.

## Phase 0: Research 결과

[research.md](research.md)에 로컬 패키지 소스와 프로젝트 조사 결과를 기록했다. 조사용 하위 에이전트 도구가 없어 직접 확인했다. 미확정 사항은 모두 해소했다.

핵심 결정:

1. 생성 클래스 `PlayerInputActions`, 경로 `Assets/Scripts/Input/PlayerInputActions.cs`, namespace 없음.
2. 직접 named performed/canceled 구독. 생성 인터페이스·PlayerInput 병행은 사용하지 않음.
3. onAfterUpdate의 실제 control 해제 관찰 유지. canceled만으로 초점 복귀 안전성을 대체하지 않음.
4. state 하나로 grounded/isEliminated 교체. 독립 bool은 역할이 있는 한 보존.
5. Keyboard&Mouse mask로 기존 검증 대상 범위 유지. 비키보드 바인딩은 자산에 보존.
6. 기존 누락 액션 시험은 원본/생성 자산 계약과 음성 사례로 이행하며 동작 시험을 약화하지 않음.

## Phase 1: Design

### 입력 소유권 및 호출자 이행

- Awake에서 Rigidbody·최초 자세·생성 인스턴스를 초기화하고 기존 설정 검증을 유지한다. PlayerInput null 검사와 Start의 문자열 액션 조회는 제거한다. 불필요해진 Start는 삭제 가능하다.
- OnEnable에서 먼저 입력/요청·준비 상태를 초기화하고 named 이벤트·onAfterUpdate를 구독한 뒤 Player 맵을 활성화한다. 기존 WakeUp·접촉 재획득 정책을 보존한다.
- Move는 performed/canceled에서 이동값을 처리한다. Jump/Restart는 performed에서 새 입력 조건과 실제 지지·현재 탈락 상태를 검사해 예약만 한다. canceled에서 짧은 유효 요청을 지우지 않는다.
- OnDisable은 구독 해제 후 Disable, 지지·요청·수평 입력 초기화. OnDestroy는 Dispose. 초기 검증 실패의 부분 초기화와 반복 생명주기에도 안전해야 한다.
- 키 해제/중립 확인은 typed actions.controls로 한다. FindAction·InputValue·GetComponent<PlayerInput>를 런타임에서 제거한다.
- 테스트와 실제 씬의 모든 PlayerInput 호출자를 함께 갱신한다. 테스트의 물리 스텝용 SendMessage는 입력 SendMessages와 달라 그대로 사용할 수 있다.

### 단일 상태 및 FixedUpdate 구조

- 내부 PlayerState는 Grounded/Airborne/Eliminated만 둔다. IsEliminated는 파생 조회이며 UI·RefreshSupport·HasGroundSupport도 state를 사용한다.
- FixedUpdate 순서: **새 낙사 우선 → 이미 탈락한 상태의 복귀/정지 분기 → 생존 지지 갱신 → 점프 요청 소비 → 초점/중립 수평 차단 → 기존 수평 이동**.
- tuple switch의 Eliminated+pendingRestart+focused 패턴으로 복귀를 구분하고 Grounded+pendingJump+focused 패턴에서 점프한다. 공통 이동을 상태별로 복제하지 않는다.
- 이동 rate는 무입력 감속, 방향 전환, 초과 속도 감속, 가속 순서를 지키는 switch expression/when guard로 표현한다. C# 8.0에 없는 relational/or pattern은 사용하지 않는다.
- 점프 높이·반 스텝 보정, 수평 MoveTowards, 발 위치 낙사 비교, 접촉·raycast 검증을 그대로 유지한다. 새 낙사 시 pendingRestart를 제거하고 즉시 return하여 같은 갱신의 복귀를 차단한다.
- 재시작은 Airborne부터 실제 지지 재획득. 비활성화 시 생존은 Airborne, 탈락은 Eliminated 유지. 초점 상실만으로 물리 상태를 덮어쓰지 않는다.

### 씬·자산 마이그레이션

- inputactions importer에서 자동 생성 옵션·클래스명·출력 경로만 설정한다. 재임포트 후 생성 파일이 SkyBout.Runtime에서 컴파일되는지 확인한다.
- SampleScene PlayerInput(fileID 2083166631)의 컴포넌트와 목록 항목을 실제 삭제하고 참조를 검색한다. 기존 PlayerMovement fileID·직렬화 필드·probe·cameraFollow·Rigidbody 값은 보존한다.
- 생성 파일과 신규 폴더/파일 meta를 커밋 대상에 포함한다. 다른 InputSystem_Actions 자산, 패키지·프로젝트 설정, 사용자 slnx는 변경하지 않는다.

### 검증 설계 및 요구사항 대응

| 요구사항 | 검증/이행 |
| --- | --- |
| FR-001~002 / SC-001 | 기존 이동·점프 시험을 생성 입력으로 구동, 8방향·높이·지지·연속 입력 유지 |
| FR-003~004 / SC-002·005 | 기존 낙사·Restart 경계·생존 R·유지·동시 판정 유지 |
| FR-005~006 / SC-003·004 | 같은 씬/인스턴스 20회, 자세·속도·카메라·유지 WASD/Space, 첫 복귀 화면 안내 제거 |
| FR-007~008 / SC-006·007 | 실제 control 해제, 빠른 입력, 반대 키, 초점·비활성화 상태별 5회, 재활성화 20회 후 단일 처리 |
| FR-009~010 / SC-007 | 기존 설정/ID 대조, PlayerInput 부재, 생성 자산 일치와 음성 사례, 기존 인수 기준 유지 및 미검증 분리 |

- 구현 전 전체 PlayMode 기준선을 실행하고 기록한다. 과거 004 기록의 66/66은 참고 자료일 뿐 이번 기준선 결과가 아니다. 카메라 시간 의존 실패와 초점 민감성은 숨기지 않는다.
- 기존 네 fixture의 PlayerInput 준비·해제·진단을 제거하고 PlayerMovement가 소유한 생성 인스턴스를 reflection으로 관찰한다. 새 public 테스트 전용 API를 만들지 않는다.
- 원본 자산 비교용 로딩은 유지 가능하되 런타임 입력은 생성 인스턴스로만 처리한다. 테스트 장치 제한·기존 integrationScene 분리·전역 설정 복구를 보존한다.
- 삭제된 grounded 내부 조회를 Enum으로 갱신한다. 기존 누락 액션 시험의 대체는 계약 불일치 음성 검증까지 포함한다. 타입/이름뿐 아니라 ID·바인딩·initialStateCheck·스킴도 대조한다.
- 렌더 확인은 실제 Editor Game 뷰에서 한다. 004 T017의 첫 복귀 렌더 안내 제거 누락은 이번 보존 검증에 포함한다. 이전 tasks의 체크를 소급 수정하지 않는다.
- 004 T015/T018의 한글 가독성·조작감 수동 검수는 실제 사람이 확인하지 못하면 미완료로 유지한다.

전이·필드는 [data-model.md](data-model.md), 소유권·씬·입력 계약은 [contracts/player-input.md](contracts/player-input.md), 실행 절차와 기록 형식은 [quickstart.md](quickstart.md)를 따른다.

## Complexity Tracking

정당화할 헌장 위반 없음. 새 패키지·입력 서비스·상태 클래스 계층·다중 사용자 페어링·호환용 PlayerInput을 추가하지 않는다.
