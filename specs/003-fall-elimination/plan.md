# Implementation Plan: 하늘섬 추락과 낙사 탈락

**Branch**: `main` | **Date**: 2026-10-30 | **Spec**: [spec.md](spec.md)

**Input**: `specs/003-fall-elimination/spec.md` 및 사용자 지침 “현재 디렉토리의 유니티 프로젝트를 확장하는 식으로 구현할 계획”.

**Branch resolution**: setup-plan JSON의 `BRANCH`는 활성 기능 경로에 기반한 `003-fall-elimination`을 반환했으나 실제 `git branch --show-current`는 `main`이다. 브랜치 훅이 없어 현재 브랜치를 유지하며, 기능 경로는 `.specify/feature.json`을 따른다.

## Summary

현재 `D:/Projects/SkyBout`의 Unity 프로젝트를 그대로 확장한다. 기존 `PlayerMovement`가 이미 제공하는 Rigidbody 중력·공중 수평 조작·접촉 기반 접지와 `TopDownCameraFollow`를 재사용한다. PlayerMovement에 월드 발 높이 기준 낙사 경계, 되돌릴 수 없는 탈락 래치, 탈락 후 플레이 입력 차단과 최소 화면 안내를 추가한다. 기존 SampleScene에 초기 낙사 높이를 직렬화하고 PlayMode 회귀·통합 시험을 보강한다.

이번 범위는 **3단계 추락·낙사 탈락만**이다. 게임 내 재시작, 글라이더, 비행 높이 보정, 온라인·전투·자기장은 추가하지 않는다. 신규 프로젝트·패키지·상태 머신·별도 생존 서비스도 만들지 않는다.

## Technical Context

**Language/Version**: C#, 현재 Unity Editor 6000.3.26f1이 제공하는 컴파일 환경. 언어 버전·런타임 설정 변경 없음.

**Primary Dependencies**: 기존 Unity 3D Physics/Rigidbody, Input System 1.20.1, URP 17.3.0, 내장 IMGUI. 패키지 추가·업그레이드 없음.

**Storage**: 런타임 상태는 메모리만 사용. Inspector 설정은 기존 SampleScene에 직렬화. 계정·세이브·네트워크 저장 없음.

**Testing**: Unity Test Framework 1.6.0/NUnit, 기존 `SkyBout.PlayModeTests` 어셈블리. 수동 Physics.Simulate 시험, 실제 씬 자연 수명주기 통합 시험, 실제 Game 뷰 안내 확인.

**Target Platform**: 현재 Windows Editor/데스크톱 키보드 프로토타입. 모바일·Quest·온라인 빌드 변경 없음.

**Project Type**: 기존 단일 Unity 3D 탑뷰 게임 프로젝트 확장.

**Performance Goals**: 첫 번째 경계 아래 FixedUpdate 관찰에서 탈락, 정상 플레이에서 안내 1초 이내. 현재 fixedDeltaTime 0.02s와 중력 (0,-9.81,0)을 유지한다. 별도 FPS·메모리 벤치마크는 이번 범위가 아니다.

**Constraints**: 발 높이에 엄격한 `<` 비교(동일 높이 생존), 탈락 전환 1회, 탈락 후 입력·초점 복귀로 부활 금지. 기존 점프·이동·카메라와 입력 자산 ID·GUID를 보존한다. 기존 `SkyBout.slnx` 사용자 변경은 수정·되돌리지 않는다.

**Scale/Scope**: 로컬 1명, 기존 섬 1개, SampleScene 1개, 생존/탈락 2상태. 현재 경계 기본 -10m; 두 높이(-10/-20)로 조정 검증. 신규 백엔드·외부 API 없음.

## Constitution Check

*GATE: Phase 0 전 및 Phase 1 설계 후 재평가.*

`.specify/memory/constitution.md`는 미작성 템플릿이다. 예시 원칙을 확정된 헌법으로 해석하지 않으며 이 작업에서 헌법을 임의로 작성하지 않는다.

| 점검 | Phase 0 전 | Phase 1 후 |
| --- | --- | --- |
| 사용자 확정 범위 A(3단계만) 준수 | PASS | PASS — 재시작·글라이더 제외 |
| 기존 프로젝트·패턴 재사용 | PASS | PASS — PlayerMovement, SampleScene, 기존 어셈블리 사용 |
| 기존 사용자 변경 보존 | PASS | PASS — 계획 문서만 작성, SkyBout.slnx 비수정 |
| 새 분기·경계·기존 기능 회귀 검증 계획 | PASS | PASS — 아래 시험 매핑 및 quickstart 작성 |
| 불필요한 계층·외부 의존성 없음 | PASS | PASS — 별도 상태 서비스·UI 패키지·이벤트 버스 없음 |

확정 헌법 위반 없음. 모든 기술적 불확실성은 [research.md](research.md)에 근거와 함께 해결했다. 런타임 시험 통과를 뜻하지는 않는다.

## Project Structure

### Documentation (this feature)

```text
specs/003-fall-elimination/
├── spec.md
├── checklists/requirements.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/player-fall-elimination.md
└── tasks.md  # 다음 /speckit.tasks 단계에서 생성; 이번에는 생성하지 않음
```

### Source Code (repository root)

```text
Assets/
├── Scenes/SampleScene.unity             # 기존 Player에 낙사 높이 저장
├── Scripts/
│   ├── PlayerMovement.cs                # 낙사·상태·입력 차단·안내 확장
│   ├── TopDownCameraFollow.cs           # 재사용, 변경 계획 없음
│   └── SkyBout.Runtime.asmdef           # 유지
├── Player_Actions.inputactions          # Move/Jump 유지, 변경 계획 없음
└── Tests/PlayMode/
    ├── FallEliminationTests.cs          # 구현 단계에서 새로 추가
    ├── FallEliminationTests.cs.meta     # Unity 생성 메타 보존
    ├── JumpTests.cs                     # 기존 회귀·필요 시 탈락 후 시험 격리
    ├── MovementAndCameraTests.cs        # 기존 회귀 검증
    └── SkyBout.PlayModeTests.asmdef      # 유지
Packages/manifest.json                   # 유지
ProjectSettings/                         # 유지
```

**Structure Decision**: 별도 프로젝트나 게임 계층을 만들지 않는다. 현재 발 위치·입력·물리를 함께 소유하는 PlayerMovement 안에 최소 기능을 모으고 새 테스트 파일 하나로 기능 검증을 분리한다. 기존 테스트 픽스처 전체를 공통 기반으로 리팩터링하지 않는다.

## Phase 0 — Research 완료

[research.md](research.md)에 코드·씬·패키지·설정 조사와 선택 이유/대안을 기록했다.

- 기존 Rigidbody 수직 중력과 실제 지지 접촉 판정을 유지한다.
- 기존 FootPosition 계산과 물리 틱을 사용한다. 고속 경계 통과를 놓치는 Trigger 기반 판정은 추가하지 않는다.
- 탈락을 컴포넌트 비활성화와 구분한다. SendMessages 입력 진입점과 물리 로직을 모두 차단한다.
- 기존 UI가 없는 프로토타입에서는 OnGUI 중앙 안내 하나로 화면 계약을 충족한다. 추후 HUD·다국어로 전환할 시점은 `shortcut:` 주석으로 명시한다.
- 테스트가 이미 탈락한 인스턴스로 정상 플레이를 기대하지 않도록 기존 호출부 영향을 검토했다.

## Phase 1 — Design 완료

### 상태와 실행 순서

상세 정의: [data-model.md](data-model.md).

1. 직렬화 float `fallDeathHeight`(-10)와 비직렬화 `isEliminated`, 읽기 전용 `IsEliminated`를 기존 컴포넌트에 추가한다. 높이는 NaN/Infinity를 거부하되 양수·0 자체를 거부하지 않는다. SampleScene의 초기 경계가 시작 표면 아래인 것은 씬 통합 시험으로 보장한다.
2. FixedUpdate 가장 앞에서 탈락 래치와 `FootPosition.y < fallDeathHeight`를 확인한다. 경계 아래면 점프·접지·이동 계산보다 탈락을 우선한다.
3. 탈락 진입 한 번에 기존 입력 초기화, 수평 정지, 보류 점프 폐기와 지지 캐시 초기화를 수행한다. 이후 수평 플레이 동작은 실행하지 않는다. 기존 수직 중력은 유지한다.
4. OnMove/OnJump에서 탈락 상태를 방어한다. 접촉 갱신으로 탈락 플레이어가 다시 지상 상태를 얻지 않게 한다. 기존 활성 여부·초점·키 해제 규칙도 보존한다.
5. OnEnable/초점 복귀/위치·설정 변경에서 래치를 초기화하지 않는다. 부활/재시작 API는 만들지 않는다.
6. OnGUI에서 탈락 상태일 때만 화면 좌표의 중앙 고대비 안내 `ELIMINATED`를 그린다. 플레이어와 카메라가 어디에 있든 안내가 유지되며 버튼은 없다. 동일 컴포넌트를 탈락 처리 때문에 비활성화하지 않는다.
7. 기존 씬·입력·스크립트 GUID를 유지하며 낙사 높이만 명시적으로 저장한다. `m_EditorClassIdentifier` 표기만 보고 기존 스크립트를 교체하지 않고 Unity의 실제 GUID 연결·컴파일을 확인한다.

### 인터페이스 계약

[contracts/player-fall-elimination.md](contracts/player-fall-elimination.md)에 사용자 입력·높이 비교·상태 유지·탈락 화면을 정의했다. 외부 네트워크 API·저장소 계약은 필요 없다.

### 검증 설계와 명세 연결

| 요구사항 | 검증 |
| --- | --- |
| FR-001~003 | 네 가장자리 걷기/점프 이탈 총 40회, 옆면·허공 비접지, 표면 위 복귀 착지 5회, 기존 이동·점프·카메라 전체 회귀 |
| FR-004~005 | 경계 위·동일·아래 각 5회; 고속 하강 5회. 직접 틱 판정과 실제 적분 시험 분리 |
| FR-006 | 10회 탈락, 각 10초 입력 차단; 탈락 직전 보류 점프, 반복 판정, 재활성화·위치 변경·초점 복귀 후 래치 유지 |
| FR-007 | 실제 Game 뷰에서 1초 이내 안내, 16:9/4:3, 화면 밖 플레이어에도 유지. 상태 시험과 화면 캡처를 구분 |
| FR-008 | -10/-20 각각 5회, 비유한 값 거부, SampleScene 시작 표면보다 낮은 초기값 검증 |
| FR-009~010 | 중앙 10회 점프·착지, 공중 Space 5회, 기존 초점·카메라 시험; 새 실행 시 생존, 게임 내 재시작 없음 |

기존 `JumpTests.SampleSceneRunsJumpMovementCameraAndReloadWithNaturalLifecycle`는 추락 후 같은 플레이어를 옆면으로 옮긴다. 새 탈락 판정이 앞선 구간에서 발생했다면 그 뒤 접지 시험은 새 씬 인스턴스로 분리하거나 생존 범위 안에서만 수행한다. 테스트를 위해 런타임 리셋 API를 추가하지 않는다. 테스트의 가상 장치·전역 입력/물리 모드는 종료 시 복구한다.

실행 명령·표본·기대 결과는 [quickstart.md](quickstart.md)를 따른다. 계획 단계에서는 테스트를 실행하지 않았으며 통과 결과는 구현 단계에서 기록한다.

### 설계 후 게이트

상단 Constitution Check를 다시 평가해 모두 통과했다. 미해결 질문·정당화가 필요한 복잡성 위반 없음. 다음 단계는 `/speckit.tasks`이며 이 명령은 Phase 1 설계 산출물 생성에서 종료한다.
