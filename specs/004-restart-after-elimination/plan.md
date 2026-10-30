# Implementation Plan: 탈락 후 재시작

**Branch**: `main` | **Date**: 2026-10-30 | **Spec**: [spec.md](spec.md)

**Input**: `specs/004-restart-after-elimination/spec.md` 및 사용자 지시 ‘현재 디렉토리의 유니티 프로젝트를 확장하는 식으로 구현’.

**Feature directory**: `specs/004-restart-after-elimination`. setup-plan.ps1의 BRANCH 출력은 기능 디렉토리 식별자 `004-restart-after-elimination`이며 실제 Git 브랜치는 `main`이다. 브랜치를 생성·전환하지 않는다.

## Summary

기존 Unity 프로젝트에 탈락 후 R 키 재시작만 추가한다. `PlayerMovement`에서 최초 자세를 보관하고 유효한 재시작 요청을 물리 갱신에서 적용하여 동일 플레이어를 복귀시킨다. 입력·낙하 속도·접촉·점프 예약을 정리하고 기존 탑뷰 카메라를 즉시 재배치한다. 기존 IMGUI 탈락 패널에 재시작 안내를 추가한다. 씬 재로드, 새 관리자, 새 패키지는 사용하지 않는다.

## Technical Context

**Language/Version**: C# / Unity 6000.3.26f1의 기존 스크립트 컴파일 환경. 언어 버전 재정의 없음.

**Primary Dependencies**: Unity Rigidbody/Physics, Input System 1.20.1의 PlayerInput SendMessages, 기존 TopDownCameraFollow, IMGUI. 기존 URP 17.3.0 유지.

**Storage**: 런타임 메모리의 시작 자세 및 입력 상태만 사용. 영구 저장 없음.

**Testing**: Unity Test Framework 1.6.0, NUnit, UnityTest, 기존 가상 Keyboard/Mouse 및 수동 Physics.Simulate 패턴. 실제 SampleScene PlayMode 통합 및 Game 뷰 확인.

**Target Platform**: 현재 Windows Unity Editor의 로컬 키보드 플레이. 별도 플랫폼·VR·온라인 요구 없음.

**Project Type**: 기존 Unity 3D 탑뷰 게임 확장.

**Performance Goals**: 탈락 안내 1초 이내, 유효 R 입력 후 2초 이내 지상 조작 가능, 재시작 적용 프레임에 탑뷰 복귀. 실행 종료 없는 20회 반복.

**Constraints**: 기존 이동·점프·낙사 경계와 초점 복귀 정책 보존. 입력은 기존 Player 액션 맵 사용. 최초 위치·설정·인스턴스 유지. 사용자 기존 변경 `SkyBout.slnx`는 수정하지 않는다.

**Scale/Scope**: 플레이어 1명, 고정 섬 1개, 기존 씬 1개. 글라이더·전투·자기장·온라인·새 메뉴는 제외.

## Constitution Check

*GATE: Phase 0 전 및 Phase 1 후 확인.*

`.specify/memory/constitution.md`는 자리표시자 템플릿이며 확정된 원칙·버전은 없다. 가상의 필수 원칙을 만들어 적용하지 않는다.

| 확인 항목 | 설계 전 | 설계 후 |
| --- | --- | --- |
| 기존 프로젝트 확장 및 4단계 범위 준수 | PASS | PASS — 기존 씬·스크립트 확장만 설계 |
| 새 의존성·관리 계층 없이 최소 변경 | PASS | PASS — 기존 입력·물리·안내 재사용 |
| 기존 조작·낙사 규칙 유지 | PASS | PASS — 명시적 R 복귀만 예외로 추가 |
| 비자명 상태·입력 분기에 대한 검증 | PASS | PASS — 새 PlayMode 시험과 기존 회귀 시험 지정 |

차단 위반 및 미해결 설계 질문 없음. 테스트 통과를 의미하는 게이트가 아니라 계획의 범위·설계 검토 결과다.

## Project Structure

### Documentation (this feature)

```text
specs/004-restart-after-elimination/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── restart.md
└── checklists/
    └── requirements.md
```

`tasks.md`는 다음 `/speckit.tasks` 단계에서 생성하며 이번 단계에서 작성하지 않는다.

### Source Code (repository root)

```text
Assets/
├── Player_Actions.inputactions          # Player/Restart 및 R 바인딩 추가
├── Scenes/
│   └── SampleScene.unity                # 기존 플레이어에 카메라 참조 연결
├── Scripts/
│   ├── PlayerMovement.cs               # 요청·초기화·안내 확장
│   ├── TopDownCameraFollow.cs           # 즉시 재배치 함수 공유
│   └── SkyBout.Runtime.asmdef           # 기존 사용, 변경 불필요
└── Tests/PlayMode/
    ├── RestartTests.cs                  # 신규: 비시각 물리·입력 검증
    ├── RestartSceneTests.cs             # 신규: 실제 씬·렌더 통합 검증
    ├── MovementAndCameraTests.cs         # 기존 회귀 검증
    ├── JumpTests.cs                     # 기존 검증 및 필요 시 오류 기대값 갱신
    ├── FallEliminationTests.cs          # 기존 탈락 검증 유지
    └── SkyBout.PlayModeTests.asmdef      # 기존 사용, 변경 불필요
```

**Structure Decision**: 기존 전역 MonoBehaviour와 PlayMode 테스트 구조를 유지한다. 새 파일의 `.meta`는 Unity가 생성·보존한다. 패키지·프로젝트 설정·기존 씬 객체를 새로 만들지 않는다. 직렬화 필드 이름과 기존 자산 GUID를 유지한다.

## Phase 0: Research 결과

[research.md](research.md)에 로컬 조사 근거와 대안을 정리했다. 조사 대상은 실제 `PlayerMovement`, `TopDownCameraFollow`, `Player_Actions.inputactions`, SampleScene, 기존 테스트 및 패키지 버전이다.

주요 결정:

1. 재생성·씬 재로드 대신 같은 Rigidbody 상태 초기화.
2. Restart 액션과 기존 SendMessages/해제 관찰 패턴 사용.
3. 낙사 판정을 먼저 수행하고 같은 판정의 복귀 요청 폐기.
4. 기존 접촉 기반 지지 판정을 우회하지 않고 복귀 후 재획득.
5. 명시적 카메라 스냅 및 기존 IMGUI 확장.

## Phase 1: Design

### 입력과 요청 적용

- Player 맵에 Restart Button/R 바인딩을 추가하고 기존 Move/Jump ID·바인딩은 유지한다.
- PlayerMovement의 필수 액션 검증에 Restart를 포함한다. 누락 시 기존 오류·비활성화 방식을 유지한다.
- `OnRestart(InputValue)`는 활성·초점·탈락 상태와 새 누름을 확인해 요청만 예약한다. 생존 중 요청을 저장하지 않는다.
- `InputSystem.onAfterUpdate`의 기존 해제 관찰을 확장한다. 현재 CheckForNeutralInput의 탈락 조기 반환보다 앞에서 Restart 해제 관찰을 수행해야 한다. Button 취소 콜백 누락에도 유지 입력이 재시작으로 오인되지 않아야 한다.
- FixedUpdate의 낙사 검사를 최우선으로 유지한다. 새 탈락 시 예약 요청을 지우고 해당 판정은 탈락 상태로 끝낸다. 이미 탈락한 상태의 새 유효 요청만 복귀시킨다.
- 초점 상실·비활성화 시 요청과 새 입력 준비 상태를 초기화한다. 초점 복귀 자체로 재시작하지 않는다.

### 복귀 처리

- Awake에서 최초 body 위치·회전을 저장한다. 반복 재시작에서 갱신하지 않는다.
- 복귀 시 body 위치·회전과 렌더 Transform을 일치시키고 linearVelocity·angularVelocity를 0으로 만든다. Rigidbody 보간으로 이전 위치가 렌더되지 않는지 통합 검증한다.
- 지지 캐시·grounded·pendingJump·입력 예약과 탈락 플래그를 정리한다. `grounded = true`를 강제하지 않는다. 실제 접촉 갱신 후 지상 조작으로 복귀한다.
- 재시작 전용 경로에서 Move 액션의 현재 값을 다시 읽고 허용한다. 기존 초점 복귀의 waitingForNeutral 동작을 다른 경로에서 변경하지 않는다.
- Jump는 탈락 중 입력을 재실행하지 않으며 해제 후 새 누름을 요구한다.
- 카메라 참조가 있으면 SnapToTarget을 호출한다. 실제 SampleScene에서는 반드시 참조를 연결하고 통합 시험으로 확인한다.

### 카메라와 안내

- TopDownCameraFollow의 초기 배치 계산을 public SnapToTarget에 공유하고 기존 검증·오류 처리를 보존한다. LateUpdate의 일반 보간은 유지한다.
- 기존 OnGUI의 고대비 화면 패널에 R 키 안내를 추가한다. 시작 위치 복귀 후 isEliminated가 false가 되면 기존 표시 조건으로 안내를 제거한다.
- 기본 폰트 한글 지원과 작은 Game 뷰에서의 가독성을 확인한다. 기본 폰트 제약 시 `R: Restart`로 동일한 조작 의미를 전달한다.

### 검증 및 영향 범위

- 신규 RestartTests는 기존 fixture와 동일한 실제 inputactions 자산, 가상 장치, 제어된 물리 스텝으로 비시각 경계를 시험한다. 테스트용 구현 우회나 새 공용 테스트 계층은 추가하지 않는다.
- 신규 RestartSceneTests는 실제 SampleScene을 로드한 뒤 동일 인스턴스로 20회 반복한다. 매 재시작마다 씬을 로드하여 결과를 대체해서는 안 된다.
- 씬 시험에서 위치·방향·속도·실제 지지 재획득, 카메라 스냅·타깃, 객체 개수 불변, 안내 표시·해제를 검증한다. 최초 시작 위치의 안전성도 확인한다.
- 기존 세 테스트 클래스는 유지하여 전부 실행한다. 기존 자산 로딩 fixture는 Restart를 자동 포함한다. 의도적으로 Jump를 누락시키는 테스트와 오류 메시지 기대값은 Restart 검증 추가와 호환되는지 확인해 필요한 부분만 수정한다.
- 기존 ‘입력·시간 경과만으로 생존 복귀 불가’는 R을 누르지 않는 한 그대로 유지된다. 과거 명세·완료 기록은 이번 기능 때문에 소급 변경하지 않는다.
- SC별 대응: SC-001 안내·시간/렌더, SC-002 자세·속도/지지, SC-003 실제 씬 20회, SC-004 입력 유지·연타, SC-005 경계·차단, SC-006 초점·Jump·판정 충돌.

상태·필드·전이는 [data-model.md](data-model.md), 외부 입력·UI·연결 규칙은 [contracts/restart.md](contracts/restart.md), 실행 절차는 [quickstart.md](quickstart.md)에 정의한다.

## Complexity Tracking

정당화할 게이트 위반 없음. 신규 관리자·추상 계층·의존성·네트워크 계약·저장소는 추가하지 않는다.
