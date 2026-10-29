# Implementation Plan: 하늘섬 기본 이동과 탑뷰 카메라

**Branch**: `main` | **Date**: 2026-10-29 | **Spec**: [spec.md](spec.md)

**Input**: `specs/001-island-movement-camera/spec.md`

경로 해석 스크립트의 기능 식별자는 `001-island-movement-camera`이며 실제 Git 브랜치는 `main`이다. 이 계획 작업은 브랜치를 만들거나 바꾸지 않는다.

## Summary

가져온 Unity 프로젝트와 `PlayerMovement`를 활용해 한 명이 하늘섬 하나에서 WASD 이동을 확인하게 한다. dynamic Rigidbody로 중력·충돌을 처리하고 수평 목표 속도를 코드에서 제어한다. 기존 출발/제동 결합과 접지 검사 시점을 수정하고 입력 초점 처리를 보완한다. Main Camera에는 캐릭터만 따라가는 스크립트 하나를 추가한다.

슈퍼바이브의 지상 이동과 시점 느낌을 참고하되 수치를 동일하게 재현한다고 가정하지 않는다. 조작·카메라 값은 Inspector에서 조정한 뒤 플레이하며 결정한다. 별도 설정 UI, 네트워크, 점프·글라이딩·낙사 판정은 만들지 않는다.

## Technical Context

**Language/Version**: C#, Unity `6000.3.26f1`에 포함된 컴파일러/런타임 사용.

**Primary Dependencies**: 기존 URP `17.3.0`, Input System `1.20.1`, Unity 3D Physics, Test Framework `1.6.0`. 새 패키지 추가·기존 패키지 정리는 이 기능에 필요하지 않다.

**Storage**: 씬과 컴포넌트의 직렬화 값. 별도 저장 데이터나 서버 없음.

**Testing**: 소수 Unity PlayMode 회귀 테스트와 Editor Game 뷰에서의 수동 입력·초점·카메라 검증. 테스트용 asmdef와 최소 런타임 asmdef로 테스트가 컴포넌트를 참조하게 한다.

**Target Platform**: 우선 Windows의 Unity Editor Play Mode, 키보드/마우스. Windows Standalone은 실제 OS 초점 문제를 구분해야 할 때 확인하며 배포는 범위 밖이다.

**Project Type**: 기존 3D Unity 게임 프로토타입.

**Performance Goals**: 기존 fixed timestep `.02`초 유지. 물리 단계에서 입력에 따른 이동·접지 검사를 수행하고 렌더 단계에서 카메라를 추적한다. 카메라 보간은 프레임 시간에 따라 계산한다. 특정 FPS나 이동 반응 시간을 근거 없이 합격 기준으로 추가하지 않는다.

**Constraints**: dynamic Rigidbody, 중력·Y 속도 보존, 단일 Ground 레이어, 화면 기준 WASD, 커서 독립 카메라. 섬 경계 이동 제한 없음. 상세 초기값은 조정 가능해야 한다.

**Scale/Scope**: 로컬 플레이어 1명, 하늘섬 1개, 카메라 1개, 기존 씬 1개. 온라인 동기화는 설계·검증하지 않는다.

## Constitution Check

*GATE: Phase 0 이전과 Phase 1 이후에 확인한다.*

`.specify/memory/constitution.md`는 원칙이 채워지지 않은 템플릿이다. 예시 주석을 확정 규칙으로 해석하지 않는다. 이 파일로부터 적용할 수 있는 헌법 게이트는 없으며 헌법을 임의로 작성하지 않는다.

| 확인 항목 | 연구 전 | 설계 후 |
|---|---|---|
| 사용자 지정 Unity/Rigidbody 제약 준수 | 통과 | 통과 |
| 기존 프로젝트·입력·씬 활용 | 통과 | 통과 |
| 섬 경계 제한, 점프·낙사·네트워크 범위 확장 없음 | 통과 | 통과 |
| 임의의 최종 튜닝값/응답 시간 합격 기준 추가 없음 | 통과 | 통과 |
| 구현 결정의 미해결 질문 없음 | 연구에서 해소 | 통과 — research.md에 결정 기록 |

헌법 위반이나 정당화가 필요한 추가 복잡성은 없다.

## Project Structure

### Documentation (this feature)

```text
specs/001-island-movement-camera/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── player-controls.md
└── checklists/requirements.md
```

`tasks.md`는 다음 `$speckit-tasks` 단계에서 생성한다.

### Source Code (repository root)

아래 추가 파일은 구현 단계의 예정 구조이며 이번 계획 단계에서 생성하지 않는다.

```text
Assets/
├── Scripts/
│   ├── PlayerMovement.cs                 # 기존 코드 수정
│   ├── TopDownCameraFollow.cs            # 추가 예정
│   └── SkyBout.Runtime.asmdef            # 테스트 참조를 위한 추가 예정
├── Scenes/SampleScene.unity              # 기존 Ground/Player/Main Camera 수정
├── Player_Actions.inputactions           # 씬 PlayerInput의 Player/Move 재사용
├── InputSystem_Actions.inputactions      # 기존 프로젝트 공용 입력 자산 유지
├── PlayerFriction.physicMaterial         # 기존 마찰 설정 재사용·확인
└── Tests/PlayMode/                       # 추가 예정
    ├── SkyBout.PlayModeTests.asmdef
    └── MovementAndCameraTests.cs
Packages/manifest.json                   # 기존 패키지 유지
ProjectSettings/
├── ProjectVersion.txt
├── TimeManager.asset
└── EditorBuildSettings.asset
```

**Structure Decision**: 기존 단일 컴포넌트에 입력·이동 책임을 유지하고 카메라 책임만 별도 컴포넌트로 둔다. 별도 서비스, 이동 상태 클래스 계층, 튜닝 자산 시스템, 설정 UI를 추가하지 않는다. asmdef는 테스트 참조에 필요한 최소 범위로 둔다. 스크립트 GUID와 기존 씬 참조를 보존하고 필드 변경 시 씬의 직렬화 값을 함께 이관한다.

## Phase 0: Research Decisions

[research.md](research.md)에 근거·대안·한계를 정리했다. 주요 결정은 다음과 같다.

1. `PlayerInput → OnMove → 입력 캐시 → FixedUpdate` 흐름 유지.
2. 같은 물리 단계에서 접지 검사 후 수평 속도를 계산하고 Y는 보존.
3. 최고 속도, 출발 가속, 정지 감속, 방향 전환 가속을 분리. 정지 상태에 전환/제동 배율이 적용되던 문제 제거.
4. 초점 상실 시 입력·수평 속도 초기화, 복귀 후 중립 확인과 새 입력 요구.
5. Rigidbody interpolation 유지, `LateUpdate`에서 캐릭터의 표시 위치를 카메라가 추적.

## Phase 1: Design

### Movement and input

`OnMove(InputValue)`에서 벡터 값만 복사한다. 씬 PlayerInput이 참조하는 `Assets/Player_Actions.inputactions`의 `Player/Move`를 사용한다. 공용 `InputSystem_Actions.inputactions`로 참조를 바꾸지 않는다. 카메라 yaw가 0이므로 월드 X/Z를 화면 좌우/위아래로 사용한다. WASD 반대 방향 상쇄는 기존 조합 입력으로 처리하고 대각선 크기는 제한한다. 접지 여부는 물리 단계마다 Ground만 대상으로 확인하며 Trigger는 무시한다.

지상에서는 목표 수평 속도에 가속도 × fixedDeltaTime 이하만큼 접근한다. 출발, 정지/감속, 진행 방향 전환을 구분하고 같은 물리 단계에 제어를 중복 적용하지 않는다. 기존의 `moveForce`, `brakeMultiplier`를 단위가 분명한 가속·감속·전환 값으로 이관한다. 현재 값에서 환산한 초기값은 연구 문서에 기록했으며 플레이로 변경할 수 있다.

초점 상실·비활성화에서는 입력을 비우고 수평 이동을 멈춘다. 초점 복귀 때 계속 눌린 키가 동기화돼도 바로 움직이지 않도록 중립 후 새 입력을 요구한다. disable/enable에도 오래된 입력을 재사용하지 않는다. 필수 참조 누락은 명확한 오류와 컴포넌트 비활성화로 처리하고 Gizmo는 null 참조로 예외를 내지 않는다.

### Camera and scene

`TopDownCameraFollow`는 플레이어 Transform, pitch, distance, FOV, followSharpness를 갖는다. 현재 perspective 방식과 yaw/roll 0을 유지한다. 초기 위치는 즉시 맞추고 이후 렌더 프레임마다 캐릭터 표시 위치를 추적한다. 마우스 위치를 읽거나 카메라 제어에 사용하지 않는다.

Ground는 유한한 평탄 윗면과 두께를 가진 단일 섬으로 구성한다. 기존 Ground 오브젝트·레이어를 활용하고 단순한 박스 Mesh/Collider로 바꾸거나 기존 윗면에 두께를 더하는 가장 작은 편집을 선택한다. 캐릭터, 섬 표면, 섬 밖 공간이 구별돼야 한다. 유효한 구도에서 중앙·가장자리 이동 중 캐릭터 전체가 보이도록 거리/FOV/추적 반응을 조정한다. 숨은 벽, 경계 clamp, Y 고정은 추가하지 않는다.

값 검증·관계·상태는 [data-model.md](data-model.md), 플레이어와 Inspector 사용 계약은 [contracts/player-controls.md](contracts/player-controls.md)에 정의한다.

### Validation

- PlayMode 테스트는 출발 가속과 제동값의 독립성, 단일/대각선 최고 수평 속도, 접지 전환과 Y 보존, 캐릭터 추적과 커서 독립성을 검증한다. 분기 회귀를 잡는 작은 테스트로 유지하며 구현을 그대로 복제한 테스트는 만들지 않는다.
- 입력은 실제 PlayerInput/Move 경로를 사용해 가상 키보드 이벤트로 구동한다. 기존 Keyboard&Mouse 스킴은 두 장치를 요구하므로 테스트에서 가상 마우스도 제공한다. 가상 장치·오브젝트·입력 설정은 테스트 종료 시 복원한다. 운영 코드에 테스트 전용 API를 추가하지 않는다.
- 초점 전환은 실제 Game 뷰/다른 창을 이용해 확인한다. 자동 테스트의 수명주기 호출만으로 실제 OS 초점 검증을 대체하지 않는다.
- 자동 비교의 부동소수점 허용치는 개발용 수치 검사에만 사용한다. 이전에 제거한 5%·0.1초를 게임의 합격 기준으로 되살리지 않는다.
- Inspector에서 각 값을 변경하고 효과를 비교한 뒤 선택한 값을 Edit Mode에서 저장한다. Play Mode 변경은 기본적으로 저장되지 않는다는 점을 가이드에 알린다.

실행 순서와 SC-001~005의 확인 방법은 [quickstart.md](quickstart.md)에 있다. 이 단계에서 게임 코드·씬 변경이나 테스트 실행은 하지 않는다.
