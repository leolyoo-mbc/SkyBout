# Implementation Plan: 글라이딩 목표 비행 높이 보정

**Branch**: `main` | **Date**: 2026-11-01 | **Spec**: [spec.md](spec.md)

**Input**: `specs/007-glide-height-control/spec.md`. 현재 디렉터리의 기존 Unity 프로젝트를 확장한다.

## Summary

기존 `PlayerMovement`의 `Gliding` 상태와 입력·접지·탈락 흐름을 유지하면서 일정 하강을 목표 발 높이로 수렴하는 가속도 제한 수직 제어로 교체한다. 전개 직전의 실제 수직 속도를 시작값으로 사용하고, 위치 이동 없이 Rigidbody의 Y속도만 제어한다. `MovementMath`에 순수 계산 함수를 추가하여 수학 검증과 실제 물리 검증을 분리한다.

사용자 교정 반영 후 기본 목표는 발 월드 Y=2m(현재 섬 표면0, 점프2), 응답3s⁻¹, 최대 보정 가속도10m/s²다. 공중 새 Space를 누르는 동안만 전개하고 해제 시 접기/중력 복원, 새 누름으로 재전개한다. 가속도는 `clamp(응답²×높이오차 - 2×응답×현재Y속도, ±최대가속도)`로 계산한다. 원래 계획의 목표0·해제 무시·입력 경계에서 전개 보존은 이번 사용자 교정으로 대체한다.

이번 명령은 Phase 1 설계까지다. 런타임 코드·씬·테스트를 구현하거나 검증 통과를 선언하지 않는다. 작업 분해와 구현은 후속 `/speckit.tasks`, `/speckit.implement` 단계다.

## Technical Context

**Language/Version**: 기존 Unity C# 환경, Unity Editor 6000.3.26f1. 언어·Editor 버전을 변경하지 않는다.

**Primary Dependencies**: 기존 Rigidbody/Physics, Input System 1.20.1, URP 17.3.0, Test Framework 1.6.0. `PlayerInputReader`, `GroundSupport`, `MovementMath`, 기존 입력 자산·생성 코드·날개 표시·카메라를 보존한다. 신규 패키지 없음.

**Storage**: 기존 씬의 직렬화 설정과 런타임 Rigidbody/상태. 저장 파일·DB·서버 없음.

**Testing**: 기존 `SkyBout.PlayModeTests`, 신규 `GlideHeightMathTests`와 `GlideHeightTests`, 기존 `GliderTests` 및 `GliderSceneTests` 갱신, 실제 Game 뷰 사람 검수. 수학 예측과 Unity 시험 결과를 별도 기록한다.

**Target Platform**: 현재 Windows Unity Editor에서 로컬 키보드 플레이. 빌드 플랫폼 전환 없음.

**Project Type**: 기존 단일 Unity 3D 탑뷰 게임 확장.

**Performance Goals**: 기존 물리 간격 0.02초, 시험 범위에서 5초 이내 목표 ±0.1m 도달 후 10초 유지, 안정 높이 폭 ≤0.1m. 비충돌 보정 가속도 ≤10m/s²로 0.1초당 속도 변화 ≤1m/s. 물리 틱마다 객체 생성·추가 입력 구독 없음.

**Constraints**: 기존 GUID·입력·수평 이동·카메라·지지와 낙사 기준 보존. 위치 보간/순간 이동으로 목표에 맞추지 않는다. 별도 비행 컨트롤러·FSM·이동 씬·섬 추가 없음. 상수 하강 필드는 다른 단위의 설정으로 자동 매핑하지 않는다.

**Scale/Scope**: 기존 SampleScene, 섬 1개, 로컬 플레이어 1명. 고정 월드 목표 발 높이 하나. 목표 UI·지형 추종·새 키·연료·비행 시간 제한·수동 접기·네트워크는 제외한다.

## Constitution Check

*GATE: Phase 0 전 검토 및 Phase 1 후 재검토.*

헌법 파일은 미작성 템플릿이다. 예시의 TDD 등은 확정 원칙으로 간주하지 않으며 아래는 사용자 명세와 저장소 지침에서 도출한 게이트다.

| Gate | Phase 0 전 | Phase 1 후 |
| --- | --- | --- |
| 현재 프로젝트·기존 씬 이어서 개발 | PASS | PASS — 기존 PlayerMovement와 SampleScene만 확장 |
| 목표 발 높이와 전개 수직 연속성 명확화 | 조사 대상 식별 | PASS — research.md 및 계약에 설정·계산·검수 조건 확정 |
| 탈락·착지·수평 조작 우선순위 보존 | PASS | PASS — 기존 상태 소유권·갱신 순서 보존 |
| 5단계와 충돌하는 요구의 명시적 교체 | PASS | PASS — 일정 하강·자연 낙사 시험만 새 수렴 계약으로 교체 |
| 초점/비활성 생명주기 구분 | 조사 대상 식별 | PASS — 실행 중/중지 중/복귀 후 동작을 구분 |
| 사용자 변경 보존, 설계 범위 준수 | PASS | PASS — 기능 문서만 생성, 런타임·씬 미변경 |
| 미해결 설계 질문 없음 | 연구 단계에서 확인 | PASS — research.md에 결정·대안·근거 기록 |

도메인 지침을 확인했다. 현재 용어집·ADR은 없으며 빈 문서를 만들지 않는다. 게이트 위반·예외 승인 필요 사항 없음.

## Project Structure

### Documentation (this feature)

```text
specs/007-glide-height-control/
├── spec.md
├── checklists/requirements.md
├── plan.md
├── research.md
├── data-model.md
├── contracts/glide-height.md
└── quickstart.md
```

`tasks.md`는 이번 단계에서 생성하지 않는다.

### Source Code (repository root)

```text
Assets/
├── Scripts/
│   ├── PlayerMovement.cs             # 수직 적용·설정 검증 교체
│   ├── MovementMath.cs               # 순수 목표 높이 속도 계산 추가
│   ├── GroundSupport.cs              # 지지 판정 보존
│   ├── PlayerInputReader.cs          # 입력/해제 정책 보존
│   └── TopDownCameraFollow.cs        # 보존
├── Scenes/SampleScene.unity          # 기존 Player의 비행 설정만 갱신
└── Tests/PlayMode/
    ├── GlideHeightMathTests.cs       # 신규 계산·수렴·가속도 한도
    ├── GlideHeightTests.cs           # 신규 실제 물리/상태 경계
    ├── GliderTests.cs                # 상수 하강 기대만 명시적 교체
    ├── GliderSceneTests.cs           # 실제 씬/렌더/반복 기대 확장
    └── 기존 *Tests.cs               # 비전개 회귀 보존
```

**Structure Decision**: `PlayerMovement`가 상태·입력 요청·Rigidbody의 단일 소유자로 남는다. `MovementMath`는 상태 없는 다음 속도 계산만 담당한다. 새 런타임 컴포넌트·패키지·프리팹·물리 전역 스케줄러 없이 기존 경계를 유지한다. 새 테스트 `.meta`만 Unity가 생성하며 기존 GUID는 변경하지 않는다.

## Phase 0 — Research Outcome

[research.md](research.md)에 제어 방식의 수치 검토, 실제 씬 기준 높이, 입력·착지·충돌·생명주기·설정 마이그레이션과 공식 Unity 문서 근거를 기록한다. 미확정 사항은 모두 해당 문서의 결정으로 해소한다. 수학 시뮬레이션 결과는 구현 후 Unity 물리·실제 씬·사람 검수를 대체하지 않는다.

## Phase 1 — Design Outcome

- [data-model.md](data-model.md): 설정·발 높이·속도·상태 전이와 물리 갱신 순서.
- [contracts/glide-height.md](contracts/glide-height.md): 사용자 조작, Inspector 설정, 수직 제어와 생명주기 계약.
- [quickstart.md](quickstart.md): 기준선, 자동 회귀, 실제 씬/사람 검수와 증거 기록.
- 보정은 기존 `CanMove` 조기 반환보다 먼저 수행한다. Space 유지 중 WASD 무입력은 보정을 중단하지 않는다. Space 해제/초점 상실/비활성은 전개 자체를 종료한다.
- 충돌 해결 후의 실제 Y속도를 다음 틱 입력으로 사용하며, 숨겨진 수직 누적 속도나 목표 위치 스냅을 두지 않는다.
- 초점 상실, 컴포넌트 비활성화, 전체 GameObject 비활성화는 구별하며 복귀 후 먼저 지지·낙사를 재평가한다.
- 이전 문서를 덮어쓰지 않고 이번 계약으로 교체되는 시험 단언을 명시한다. 이전 기능의 미완료 사람 검수도 자동 완료 처리하지 않는다.

### 사용자 교정 구현

`PlayerInputReader`에 `JumpReleased`와 유효 `IsJumpHeld`를 추가하고 `PlayerMovement.CancelGlide`가 날개/상태/중력과 전개 요청을 정리한다. 지상 짧은 점프 요청은 유지한다. 입력 자산/키를 새로 만들지 않는다. 실제 씬에는 이미 사용자 지정2가 저장되어 있어 그 값을 보존하고 코드 기본값도2로 맞췄다. 새 회귀 시험은 해제/낙하/재전개, 해제된 공중 탭의 비전개, 입력 경계 자동 재전개 금지, 실제2m 유지/해제 착지/자연 낙사10회를 포함한다.

## Complexity Tracking

게이트 위반 없음. 기존 두 모듈에 한정된 수직 제어 확장으로 별도 계층·상태 프레임워크를 도입하지 않는다.

## Execution Context

`setup-plan.ps1 -Json` 결과는 `FEATURE_SPEC=E:\projects\SkyBout\specs\007-glide-height-control\spec.md`, `IMPL_PLAN=E:\projects\SkyBout\specs\007-glide-height-control\plan.md`, `FEATURE_DIR=E:\projects\SkyBout\specs\007-glide-height-control`, `BRANCH=007-glide-height-control`이었다. 마지막 값은 공통 스크립트가 기능 디렉터리에서 도출한 식별자이며 실제 Git 브랜치는 `main`이다. 브랜치를 생성/전환하지 않았다. 준비 스크립트는 자식 PowerShell에만 `-ExecutionPolicy Bypass`를 적용해 실행했고 영구 정책은 변경하지 않았다.

Unity 읽기 전용 상태 조회에서 프로젝트 경로와 버전 일치, Play Mode 정지·컴파일 중 아님을 확인했다. 계획 작성을 위해 Play Mode를 시작하거나 시험·씬 변경을 실행하지 않았다. `.specify/feature.json`의 기능 경로를 사용하며 현재 Git 브랜치 이름에 의존하지 않는다.
