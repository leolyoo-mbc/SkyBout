# Implementation Plan: 같은 높이의 하늘섬 사이 점프·글라이딩 이동

**Branch**: `main` | **Date**: 2026-11-01 | **Spec**: [spec.md](spec.md)

**Input**: `specs/008-island-traversal/spec.md`

## Summary

기존 `SampleScene`의 시작 섬·플레이어·카메라를 보존하고 정적 섬 두 개를 추가한다. A–B는 북쪽 4m 간격, B–C는 동쪽 14m 간격으로 배치한다. 기존 점프·비행 높이·수평 성능을 변경하지 않고 실제 씬 통합 시험으로 점프 왕복, 글라이딩 왕복과 해제 착지, 연속 이동 및 낙사 재시작을 검증한다. 새 런타임 관리자나 조작은 추가하지 않는다.

이 명령의 범위는 Phase 1 설계까지다. 씬·런타임 코드·테스트를 구현하거나 실제 플레이 검증 통과를 선언하지 않는다.

## Technical Context

**Language/Version**: 기존 Unity C#, Unity Editor 6000.3.26f1. 버전 변경 없음.

**Primary Dependencies**: 기존 Rigidbody/Physics, Input System 1.20.1, URP 17.3.0, Test Framework 1.6.0. 신규 패키지 없음.

**Storage**: `Assets/Scenes/SampleScene.unity`에 정적 지형 직렬화. 테스트 결과는 `TestResults/008-*`. DB·저장 게임 없음.

**Testing**: `SkyBout.PlayModeTests`에 실제 씬을 로드하는 `IslandTraversalSceneTests` 추가. 가상 Keyboard 입력, 실제 Rigidbody/접촉 판정, 목적지 Collider 확인, viewport 검증 및 사람이 관찰하는 Game 뷰 검수. 기존 전체 PlayMode 회귀 유지.

**Target Platform**: Windows Unity Editor, 로컬 키보드. 기본 화면 검수는 기존 렌더 시험과 같은 16:9, 1280×720 Game 뷰. 플랫폼 전환 없음.

**Project Type**: 기존 단일 Unity 3D 탑뷰 게임 확장.

**Performance Goals**: 기존 fixedDeltaTime=0.02s 및 수평 최고 속도 8m/s 유지. 목표 발 높이 2±0.1m를 10초 유지하고 해제 후 3초 내 착지, R 후 2초 내 복귀. 정적 섬 추가로 물리 틱별 할당·입력 구독을 늘리지 않는다.

**Constraints**: Ground의 이름·형상·GUID와 Player/Camera 루트·시작 자세 보존. 기존 X>16, Z≈0 동쪽 낙사 시험 통로를 비워둔다. 점프 높이 2m, 가속 10m/s², 감속/회전 50m/s², 목표 비행 발 Y=2m, 낙사 발 Y<-10m 및 카메라 추적 규칙 보존. 플레이어 순간 이동은 시험의 독립 초기 배치에만 허용하며 이동 성공 증거로 사용하지 않는다.

**Scale/Scope**: 총 섬 3개, 플레이어 1명, 짧은/긴 왕복 구간 각 하나. 절차적 생성·체크포인트·온라인·새 아트 제외.

## Constitution Check

*GATE: Phase 0 전 검토 및 Phase 1 후 재검토.*

헌법은 미작성 템플릿이다. 예시 원칙을 확정 규칙으로 적용하지 않으며, 아래 게이트는 명세와 저장소 지침에 근거한다. 도메인 지침을 읽었고 용어집·ADR은 없으므로 빈 문서를 생성하지 않는다.

| Gate | Phase 0 전 | Phase 1 후 |
| --- | --- | --- |
| 현재 프로젝트·기존 씬 확장 | PASS | PASS — SampleScene에 섬 두 개 추가 설계 |
| 기존 조작·물리·시작 위치 보존 | PASS | PASS — 런타임 스크립트 변경 불필요 |
| 점프용/비행용 구간 차이 검증 | 조사 대상 식별 | PASS — 4m/14m 및 양방향 대비 시험 확정 |
| 카메라 목적지 가시성 | 조사 대상 식별 | PASS — 긴 구간 X축 배치와 양방향 viewport/관찰 검수 |
| 기존 낙사 시험 공간 보존 | 조사 대상 식별 | PASS — 새 섬은 Z=26에 배치 |
| 복잡도·범위 제한 | PASS | PASS — 새 관리자·패키지·프리팹 시스템 없음 |
| 예측·실제 검증 구분 | PASS | PASS — 현재는 계산/소스 조사, 플레이 결과 미검증 |
| 미해결 설계 질문 없음 | 조사 진행 | PASS — research.md에 결정·근거·대안 기록 |

위반 및 예외 승인 필요 사항 없음. 설계 이후에도 모든 게이트를 통과한다.

## Project Structure

### Documentation (this feature)

```text
specs/008-island-traversal/
├── spec.md
├── checklists/requirements.md
├── plan.md
├── research.md
├── data-model.md
├── contracts/island-traversal.md
└── quickstart.md
```

`tasks.md`는 후속 `/speckit.tasks`에서 생성한다.

### Source Code (repository root)

```text
Assets/
├── Scenes/SampleScene.unity               # 정적 IslandShort/IslandLong 추가
├── Scripts/
│   ├── PlayerMovement.cs                 # 변경 없음
│   ├── GroundSupport.cs                  # 변경 없음
│   ├── MovementMath.cs                   # 변경 없음
│   ├── PlayerInputReader.cs               # 변경 없음
│   └── TopDownCameraFollow.cs             # 변경 없음
└── Tests/PlayMode/
    ├── IslandTraversalSceneTests.cs       # 신규 실제 씬 통합 시험
    ├── IslandTraversalSceneTests.cs.meta  # Unity 생성 GUID
    ├── SkyBout.PlayModeTests.asmdef       # 기존 어셈블리 재사용
    └── 기존 *Tests.cs                    # 회귀 기준 보존
TestResults/                              # 구현 후 실제 증거; 계획 단계 미생성
```

**Structure Decision**: 씬의 정적 Collider가 지형을 소유하고 `PlayerMovement`가 상태와 물리를 계속 단독 소유한다. 경로 정의와 보고 데이터는 테스트/문서에만 두며 런타임 DTO·IslandManager·새 입력 API를 만들지 않는다. 테스트 보조 함수는 신규 시험 클래스 안에서만 공유하고 기존 전체 시험을 리팩터링하지 않는다.

## Phase 0 — Research Outcome

[research.md](research.md)에 실제 직렬화 값, 점프 거리 예측, 기존 시험 의존성, ㄱ자 배치 선택과 카메라 위험을 기록했다. 연구 에이전트의 읽기 전용 코드/씬 조사를 사용하고 기존 소스와 CLI 도움말로 교차 확인했다. 미확정 설계 질문은 남지 않는다. 수치 예측은 Unity 충돌·입력 타이밍 또는 사람이 보는 플레이의 통과 증거가 아니다.

## Phase 1 — Design Outcome

- [data-model.md](data-model.md): 정확한 섬 범위, 검수 출발점, 런타임 상태 재사용과 시험 기록 형식.
- [contracts/island-traversal.md](contracts/island-traversal.md): 씬·사용자 조작·목적지 착지·카메라 및 재시작 계약.
- [quickstart.md](quickstart.md): 실행 전제, 회귀 명령, 단계별 인수 시험과 미검증 기록 방식.
- 카메라 설정은 기본적으로 변경하지 않는다. 긴 구간을 X축으로 놓아 남북 비대칭 시야 문제를 피한다. 지원 화면에서 실제 viewport 검증이 실패하면 먼저 동일 높이·4m/14m 간격과 동쪽 낙사 통로를 유지하는 배치 조정으로 해결한다. 카메라 동작 변경이 필요하면 별도 범위 결정 없이 몰래 변경하지 않는다.
- 카메라 시야 문제로 섬 배치를 조정하면 `data-model.md`의 좌표, 시험의 도움닫기 시작점·출발선·제동 위치, 좌표 검증값을 함께 갱신한다. 같은 높이, 4m/14m 간격, 기존 낙사 시험 공간과 이동 성능은 유지한다.
- 새 테스트는 목적지 실제 지지와 안정 착지를 확인하여 출발 섬 재착지·옆면 접촉·높이만 일치하는 결과를 성공으로 오인하지 않는다.

## Complexity Tracking

게이트 위반 없음. 정적 씬 확장과 한 개의 통합 시험 클래스로 충분하다.

## Execution Context

`setup-plan.ps1 -Json`의 결과: `FEATURE_SPEC=E:\projects\SkyBout\specs\008-island-traversal\spec.md`, `IMPL_PLAN=E:\projects\SkyBout\specs\008-island-traversal\plan.md`, `FEATURE_DIR=E:\projects\SkyBout\specs\008-island-traversal`, `BRANCH=008-island-traversal`. 마지막 값은 기능 디렉터리에서 도출된 식별자이고 실제 Git 브랜치는 `main`이다. 브랜치를 생성하거나 전환하지 않았다.

직접 스크립트 실행은 실행 정책으로 차단되어 자식 PowerShell에만 `-ExecutionPolicy Bypass`를 적용해 실행했다. 영구 정책은 바꾸지 않았다. `.specify/feature.json`으로 활성 기능을 해석한다. 계획 단계에서는 런타임/씬 변경, Play Mode 실행, 테스트 실행을 하지 않았다.
