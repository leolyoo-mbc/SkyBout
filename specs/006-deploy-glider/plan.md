# Implementation Plan: 공중 글라이더 전개와 이동

**Branch**: `main` | **Date**: 2026-11-01 | **Spec**: [spec.md](spec.md)

**Input**: `specs/006-deploy-glider/spec.md`. 사용자 추가 조건: 현재 디렉터리의 Unity 프로젝트를 확장한다.

## Summary

현재 `Assets/Scenes/SampleScene.unity`와 `PlayerMovement` 기반 조작을 확장한다. 기존 Space Button 이벤트를 지상 점프 또는 공중 글라이더 전개 요청으로 분기하고, 기존 상태 열거형에 `Gliding`을 추가한다. 수평 조작은 `MovementMath`를 그대로 사용한다. 글라이딩에서는 중력을 끄고 수직 속도를 일정한 음수로 제어하며, 착지·탈락·재시작 시 기본 중력과 비전개 표시로 복원한다. 기존 씬 Player에 충돌 없는 날개 모양 자식을 추가해 탑뷰에서 상태를 확인한다.

이번 명령은 설계까지만 수행한다. 런타임·씬·테스트 구현이나 검증 완료를 주장하지 않는다. 작업 분해는 후속 `/speckit.tasks`에서 수행한다.

## Technical Context

**Language/Version**: 기존 Unity C# 컴파일 환경 유지; Unity Editor 6000.3.26f1. 별도 언어 버전 변경 없음.

**Primary Dependencies**: Unity Rigidbody/Physics, Input System 1.20.1, URP 17.3.0, Test Framework 1.6.0. 기존 `PlayerInputActions`, `PlayerInputReader`, `GroundSupport`, `MovementMath`, `TopDownCameraFollow` 재사용. 패키지 추가 없음.

**Storage**: 기존 씬의 직렬화 필드와 메모리 상태만 사용. 저장 파일·DB·서버 없음.

**Testing**: 기존 `SkyBout.PlayModeTests`의 입력·수학·이동·점프·낙사·재시작 시험 전체, 새 `GliderTests`와 `GliderSceneTests`, 실제 Game 뷰 수동 검수. 기존 fixture의 가상 Keyboard, 수동 물리 및 InputSystem 갱신 패턴을 따른다.

**Target Platform**: 현재 Windows Unity Editor의 로컬 키보드 플레이. 별도 빌드 플랫폼 전환 없음.

**Project Type**: 기존 단일 Unity 3D 탑뷰 게임 프로젝트 확장.

**Performance Goals**: 유효 공중 입력 후 0.2초 이내 전개 표시, 기존 물리 간격 0.02초 유지. 물리 틱마다 객체 생성·장치 구독 추가 없음; 전개 반복 시 표시 객체 수 일정.

**Constraints**: 기존 설정·GUID·입력 자산·생성 코드·카메라 보존. `Glide` 액션이나 두 번째 이동 컨트롤러를 추가하지 않는다. 목표 비행 높이 보정, 여러 섬, 온라인 기능 제외. 초기 `glideDescentSpeed`는 2 m/s로 두고 양의 유한값만 허용한다. 자유낙하보다 느린지는 통제된 높이 구간 비교로 검증한다.

**Scale/Scope**: 로컬 플레이어 1명, 기존 섬 1개, 기존 씬 1개. 새 런타임 클래스 없이 기존 컨트롤러에 상태·설정·표시 참조만 확장한다.

## Constitution Check

*GATE: Phase 0 전 검토 및 Phase 1 후 재검토.*

헌법 파일은 미작성 템플릿이므로 예시 원칙을 확정 정책으로 취급하지 않는다. 다음은 명세·저장소 지침·사용자 조건에 따른 계획 게이트다.

| Gate | Phase 0 전 | Phase 1 후 |
| --- | --- | --- |
| 현재 Unity 프로젝트의 기존 씬·조작 확장 | PASS | PASS — 단일 PlayerMovement 소유권 유지 |
| 5단계 범위만 포함 | PASS | PASS — 일정 하강, 높이 보정 없음 |
| 기존 동작 보존 및 명시적 확장만 허용 | PASS | PASS — 공중 Space 무동작만 새 전개로 교체 |
| 기존 코드·사용자 작업 무단 덮어쓰기 금지 | PASS | PASS — 이번 단계는 기능 문서만 작성 |
| 미확정 기술 선택 해소 | 조사 대상으로 식별 | PASS — research.md에 입력·중력·생명주기·표시·검증 결정 기록 |

위반이나 예외 승인 필요 사항 없음. 도메인 지침을 확인했으며 용어집·ADR은 현재 없다.

## Project Structure

### Documentation (this feature)

```text
specs/006-deploy-glider/
├── spec.md
├── checklists/requirements.md
├── plan.md
├── research.md
├── data-model.md
├── contracts/player-glider.md
└── quickstart.md
```

`tasks.md`는 이 단계에서 생성하지 않는다.

### Source Code (repository root)

```text
Assets/
├── Player_Actions.inputactions                 # 보존
├── Scripts/
│   ├── PlayerMovement.cs                      # 상태·요청·수직 제어·표시 확장
│   ├── PlayerInputReader.cs                   # 기존 Space 이벤트/해제 정책 유지
│   ├── GroundSupport.cs                       # 실제 지지 판정 재사용
│   ├── MovementMath.cs                        # 수평/점프 계산 보존
│   ├── TopDownCameraFollow.cs                 # 보존
│   └── Input/PlayerInputActions.cs             # 생성 코드 보존
├── Scenes/SampleScene.unity                   # Player에 비충돌 시각 자식 추가
├── Materials/Glider.mat                       # 신규 시각 재질과 meta
└── Tests/PlayMode/
    ├── GliderTests.cs                         # 신규 동작·상태 경계
    ├── GliderSceneTests.cs                    # 신규 실제 씬·표시·반복
    └── 기존 *Tests.cs                         # 회귀, 공중 Space 기대만 명시 갱신
```

**Structure Decision**: `PlayerMovement`가 지상·공중·글라이딩·탈락과 Rigidbody의 유일한 소유자로 남는다. 기존 `.asmdef`를 사용하며 네트워크 계층·범용 FSM·별도 캐릭터 프리팹은 도입하지 않는다. 날개는 Player의 자식 GameObject로 저장하며 MeshRenderer만 사용하고 Collider/Rigidbody는 추가하지 않는다. 새 재질·테스트의 `.meta`는 Unity가 생성하고 기존 GUID는 유지한다.

## Phase 0 — Research Outcome

[research.md](research.md)에 현재 코드와 Unity 6000.3 문서 근거, 결정 이유, 대안 및 회귀 위험을 기록한다. 핵심 결정은 Space 입력 시 의도를 확정하고 실행 틱에서 다시 검증하며, 무효 요청을 다른 동작으로 바꾸지 않는 것이다. 일정 하강을 위해 `useGravity`의 소유권을 컨트롤러에 명시하고 생명주기에서도 보존한다.

## Phase 1 — Design Outcome

- 상태·전이·요청 데이터와 물리 순서: [data-model.md](data-model.md).
- 사용자 조작·설정·표시·생명주기 계약: [contracts/player-glider.md](contracts/player-glider.md).
- 구현 후 실행할 기준선·회귀·수동 검증: [quickstart.md](quickstart.md).
- `FixedUpdate`의 수평 입력 차단 조기 반환보다 먼저 글라이딩 수직 제어를 처리한다.
- 전개 취소 시 기본 중력을 복원한다. 비활성화 자체는 취소가 아니므로 공중 전개 상태와 일정 하강을 유지하고 재활성화 후 지지·낙사를 먼저 재검사한다.
- 자동 시험과 사람의 시각·조작감 검수 결과를 분리한다. 과거 문서의 통과 수를 이번 실행 결과로 재사용하지 않는다.

## Complexity Tracking

게이트 위반 없음. 새 패키지·상태 프레임워크·서비스 계층 없이 기존 모듈을 확장한다.

## Execution Context

setup-plan 결과의 `BRANCH`는 `006-deploy-glider`였다. `common.ps1`은 실제 Git 브랜치가 아니라 기능 디렉터리 이름으로 해당 값을 대체한다. `git branch --show-current`로 확인한 실제 브랜치는 `main`이며 전환·생성하지 않았다. 경로는 `E:/projects/SkyBout`를 기준으로 확인했다. PowerShell 실행 정책으로 직접 호출이 차단되어, 해당 자식 프로세스에만 `-ExecutionPolicy Bypass`를 적용해 준비 스크립트를 실행했다. 영구 정책은 변경하지 않았다.
