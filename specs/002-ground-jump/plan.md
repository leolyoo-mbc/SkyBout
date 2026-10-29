# Implementation Plan: 지상 고정 높이 점프와 착지

**Branch**: `main` | **Date**: 2026-10-29 | **Spec**: [spec.md](spec.md)

**Input**: `specs/002-ground-jump/spec.md`; 사용자 추가 지시: “디렉토리에 있는 유니티 프로젝트에 추가 구현할 거야.”

## Summary

기존 Unity 프로젝트의 `SampleScene`, `PlayerMovement`, Input System 자산과 테스트 어셈블리를 확장한다. 새 프로젝트나 새 이동 프레임워크는 만들지 않는다. 지상에서 받은 Space 누름 한 번을 다음 물리 틱에서 한 번만 소비하고, 목표 높이로부터 계산한 초기 수직 속도를 적용한다. 중력·착지는 동적 Rigidbody와 기존 섬 Collider가 담당한다. 접지 판정을 실제 발아래 지지 접촉으로 강화하고 공중 수평 조작은 지상 설정과 통일한다. 카메라는 기존 구현을 우선 유지한다.

## Technical Context

**Language/Version**: C#, Unity Editor 6000.3.26f1 프로젝트 기본 컴파일러.
**Primary Dependencies**: Unity 3D Physics/PhysX, Input System 1.20.1, URP 17.3.0; 기존 패키지 재사용, 신규 설치 없음.
**Storage**: 영속 저장 없음. 설정은 MonoBehaviour 직렬화 필드와 기존 씬·입력 자산에 보관.
**Testing**: Unity Test Framework 1.6.0, NUnit, 기존 PlayMode 테스트 어셈블리 및 가상 Keyboard 입력. 실제 씬 통합 검사와 수동 플레이 병행.
**Target Platform**: 현재 Windows Unity Editor의 키보드 로컬 플레이 검증. 최종 출시 플랫폼은 이번 범위에서 결정하지 않음.
**Project Type**: 기존 Unity 3D 탑뷰 게임 프로젝트의 증분 기능.
**Validation Scope**: 기존 0.02초 물리 틱을 유지하고 점프 높이 오차 ±5% 등 명세 SC-001~005의 기능 동작을 검증한다. 사용자 요청에 따라 FPS 목표, Profiler 측정 및 메모리 할당 검사는 이번 범위에서 제외한다. 접촉 버퍼 재사용은 구현 방식으로 유지하되 별도의 성능 완료 조건으로 삼지 않는다.
**Constraints**: 동적 Rigidbody, Y축 중력 -9.81, linearDamping 0 및 수직 이동 허용 유지. 수평 속도 갱신 시 Y 보존. 기존 자산 GUID·씬 참조 보존. 추가 점프·입력 예약·글라이더·낙사 복귀 없음.
**Scale/Scope**: 하늘섬 하나, 플레이어 하나, 기존 SampleScene 하나. 변경 중심은 이동 스크립트·입력 자산·씬 설정·PlayMode 테스트.

## Constitution Check

*GATE: Phase 0 이전 및 Phase 1 이후 확인.*

- 헌법 파일은 원칙·버전이 미작성인 템플릿이다. 이를 확정된 규칙으로 해석하지 않으며 정식 헌법 준수를 인증하지 않는다.
- 사전 점검 통과: 기존 프로젝트에 증분 구현하고 명세 범위를 유지한다. 새 네트워크·UI·패키지·저장 시스템을 도입하지 않는다.
- 설계 후 재점검 통과: 기존 Rigidbody/Input System 구조 및 테스트 어셈블리를 재사용하며 명세와 충돌하는 신규 기능은 없다.
- 미해결 기술 질문과 정당화가 필요한 위반은 없다. 헌법 확정은 별도 작업이다.

## Project Structure

### Documentation (this feature)

```text
specs/002-ground-jump/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── player-jump.md
└── checklists/requirements.md
```

`tasks.md`는 /speckit.tasks 단계에서 생성했으며 구현 진행 상태를 관리한다. 최종 실행 결과는 [quickstart.md](quickstart.md)의 Completion Evidence를 참조한다.

### Source Code (repository root)

```text
Assets/
├── Player_Actions.inputactions          # Player/Jump 추가; 기존 Move 보존
├── Scenes/SampleScene.unity             # 기존 플레이어 설정에 점프 값 반영
├── Scripts/
│   ├── PlayerMovement.cs               # 접지·단발 입력·수직 속도·공중 조작
│   ├── TopDownCameraFollow.cs           # 우선 변경 없이 회귀 검증
│   └── SkyBout.Runtime.asmdef           # 유지
└── Tests/PlayMode/
    ├── MovementAndCameraTests.cs        # 공중 조작 기대값 갱신, 기존 회귀 유지
    ├── JumpTests.cs                     # 신규 점프/입력/착지/높이 테스트
    └── SkyBout.PlayModeTests.asmdef      # 유지
```

**Structure Decision**: 단일 기존 Unity 프로젝트에 확장한다. 입력은 PlayerInput SendMessages의 `OnJump(InputValue)`로 수신하고 PlayerMovement가 유일한 속도 작성자가 된다. 별도 점프 컴포넌트를 만들어 수직·수평 속도 쓰기 순서를 경쟁시키지 않는다. 새 파일은 Unity .meta도 함께 관리한다. 사용자의 기존 변경인 `ProjectSettings/EditorBuildSettings.asset`, `SkyBout.slnx`는 덮어쓰거나 되돌리지 않는다.

## Phase 0 — Research

[research.md](research.md)에 기존 코드·씬·패키지 조사와 대안 평가를 기록했다. 접지 방식, 입력 생명주기, 높이 계산, 공중 조작 차이, 테스트 전략을 결정했다. 조사용 하위 에이전트 도구가 없어 독립 조사 항목을 직접 검토했다. 설계에 필요한 미해결 질문은 없다.

## Phase 1 — Design & Validation

- [data-model.md](data-model.md): 설정, 입력 상태, 지지 접촉 및 상태 전이.
- [contracts/player-jump.md](contracts/player-jump.md): 플레이어 입력·Inspector·물리 갱신 계약. 외부 HTTP API는 없다.
- [quickstart.md](quickstart.md): 실제 씬 플레이와 PlayMode 검증 절차.
- 구현 순서: 테스트 픽스처 확장 → 접지 보완 → 입력/점프 추가 → 공중 조작 통일 → 씬 연결 → 회귀 및 실제 플레이.
- 완료 판정은 명세 SC-001~005에 따른다. 이 문서는 설계 산출물이며 구현·최종 검증 결과는 [quickstart.md](quickstart.md)에 별도 기록한다.

## Complexity Tracking

헌법 점검 위반으로 추가하는 구조가 없으므로 별도 예외 승인은 필요하지 않다.
