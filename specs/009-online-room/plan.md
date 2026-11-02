# Implementation Plan: 온라인 2인 방 생성·참가

**Branch**: `main` | **Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

**Input**: `specs/009-online-room/spec.md` 및 현재 디렉터리의 Unity 프로젝트를 직접 수정한다는 사용자 제약.

**Branch note**: setup-plan 출력의 BRANCH는 기능 식별자 `009-online-room`이다. 실제 Git 브랜치는 `git branch --show-current`로 확인한 `main`이며 새 브랜치를 생성하지 않았다.

## Summary

기존 `Assets/Scenes/SampleScene.unity`의 하늘섬·이동·글라이더를 유지하며 온라인 방 패널, 네트워크 플레이어 prefab과 세션 생명주기를 추가한다. Unity Multiplayer Services Sessions가 익명 인증 후 비공개 2인 방과 Relay 연결을 구성하고 NGO가 플레이어를 복제한다. 새 Unity 프로젝트·별도 서버·자동 매칭은 만들지 않는다.

소유자 권한 이동으로 현재 Rigidbody 기반 `PlayerMovement`의 입력/접지/글라이딩 규칙을 보존한다. 호스트가 스폰·정원·슬롯을 관리하고 각 소유자만 이동을 시뮬레이션한다. 원격은 kinematic 복제본이다. 경쟁용 보안보다 기존 조작 보존을 우선하는 명시적 프로토타입 선택이다.

## Technical Context

**Language/Version**: C#, Unity Editor `6000.3.26f1`의 지원 컴파일러/API 수준 유지.

**Primary Dependencies**: 기존 Input System 1.20.1, URP 17.3.0, Test Framework 1.6.0, uGUI 2.0.0. 추가 계획: `com.unity.services.multiplayer` 2.4.1, `com.unity.netcode.gameobjects` 2.13.3, `com.unity.transport` 2.7.4. 인증 3.8.0/Core 1.18.0은 MPS 의존성으로 해결한다. NGO 3.1.0은 Unity 6000.7을 요구하므로 사용하지 않는다. 실제 설치·lockfile·컴파일 검증은 구현 단계에 한다.

**Storage**: UGS 임시 세션 및 인증 로컬 캐시. 게임 저장 DB 없음.

**Testing**: 기존 `Assets/Tests/PlayMode` 회귀, fake 기반 요청/오류 테스트, NGO 다중 인스턴스 통합, Windows 빌드 2개/정원 시험 시 3개와 다른 인터넷 회선 수동 인수 시험.

**Target Platform**: 우선 Windows Standalone 개발 빌드 및 Windows Unity Editor. 다른 배포 플랫폼은 이번 검증 대상이 아니다.

**Project Type**: 기존 Unity 3D 데스크톱 게임에 통합.

**Performance Goals**: 정상 조건의 생성/참가 각각 15초 이내, 전체 실패 확정 30초 이내, 상대 동작 표시 p95 0.5초 이내. NGO tick 초기 30Hz와 원격 보간을 출발점으로 측정한다.

**Constraints**: RTT ≤100ms/손실 ≤1%에서 정상 성능 시험. 끊김 감지 ≤30초, 확정 후 로컬 정리 ≤5초. 2인 정원, 포트 포워딩 불필요, 기존 목표 발 높이 2m와 Space 유지 규칙 보존. 가입·장비 설정 UI 추가 없음.

**Scale/Scope**: 방당 2명, 하나의 기존 맵, 참가 코드, 방장 종료 시 방 종료. 서버 부정행위 방지·호스트 이전·플레이어 간 물리 상호작용 제외.

## Constitution Check

*Phase 0 이전*: PASS. `.specify/memory/constitution.md`는 미작성 템플릿으로 확정 원칙을 추측하지 않았다. `docs/agents/domain.md`와 `issue-tracker.md`를 확인했으며 용어집/ADR은 없다. 별도 프로젝트 없이 기존 Unity 파일 구조를 사용한다.

*Phase 1 이후*: PASS. 입력/카메라 소유권·정원·중복 요청·오류 롤백과 기존 규칙 회귀를 설계에 포함했다. 미해결 설계 질문 없음. 아래 외부 환경 준비는 실제 실행 게이트이며 설계 결정 미정과 구별한다.

**실행 전 필수 게이트**: 현재 `ProjectSettings/ProjectSettings.asset`의 `cloudProjectId`, `projectName`, `organizationId`는 비어 있다. 권한 있는 사용자가 Unity Cloud 프로젝트를 연결하고 대상 환경의 Authentication/Multiplayer 서비스 이용 가능 상태·비용/쿼터를 확인해야 한다. 에이전트가 계정·결제·클라우드 리소스를 임의 생성하지 않는다. 미설정 상태에서 fake 통과나 LAN 시연을 인터넷 인수 통과로 보고하지 않는다.

## Project Structure

### Documentation (this feature)

```text
specs/009-online-room/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── checklists/requirements.md
└── contracts/online-room.md
```

`tasks.md`는 다음 `/speckit.tasks` 단계에서 생성한다.

### Source Code (repository root, planned changes)

```text
Assets/
├── Scenes/SampleScene.unity             # 기존 섬 유지, bootstrap/UI/스폰 지점 추가
├── Prefabs/NetworkPlayer.prefab         # 기존 Player 외형·물리 설정에서 추출
├── Scripts/
│   ├── PlayerMovement.cs               # 로컬 권한/시작 자세/상태 조회 경계
│   ├── PlayerInputReader.cs            # 기존 입력 해제·초점 규칙 유지
│   ├── TopDownCameraFollow.cs          # 런타임 소유자 바인딩/해제
│   ├── SkyBout.Runtime.asmdef          # 기존 이동 코어 유지
│   └── Online/
│       ├── SkyBout.Online.asmdef       # Runtime + NGO + MPS/Core/Auth/Transport + UI 참조
│       ├── IRoomService.cs
│       ├── UnityRoomService.cs
│       ├── OnlineRoomController.cs
│       ├── OnlineRoomBootstrap.cs
│       ├── OnlineRoomPanel.cs
│       ├── NetworkPlayer.cs
│       └── OwnerNetworkTransform.cs
└── Tests/
    ├── PlayMode/                        # 기존 회귀 유지
    └── Online/                          # 별도 test asmdef, 상태 머신·스폰/소유권 테스트
Packages/manifest.json                  # 호환 릴리스 고정
Packages/packages-lock.json            # Unity가 재생성
ProjectSettings/                        # 필요 서비스 연결·네트워크 레이어 설정
```

**Structure Decision**: 네트워크 SDK 의존성은 하위 `SkyBout.Online` 어셈블리에 격리하고 기존 이동 코어는 NGO에 의존시키지 않는다. 생성 `.csproj`/`.slnx`를 수동 편집하지 않는다. 기존 사용자 변경 `SkyBout.slnx`를 덮어쓰거나 되돌리지 않는다.

## Integration Design

1. **세션**: 초기화→익명 로그인→`SessionOptions { MaxPlayers=2, IsPrivate=true }.WithRelayNetwork()`로 생성, `session.Code`로 참가. Sessions 통합이 NGO를 시작하므로 직접 Relay 할당이나 중복 `StartHost/StartClient`를 호출하지 않는다.
2. **오프라인 보존**: 기본 씬 로컬 Player와 회귀 흐름은 유지한다. 온라인 패널 진입 때 입력을 일시 중단하고 온라인 진입 전 원본을 비활성화한다. 온라인 prefab만 스폰한다. 실패/퇴장 시 모든 네트워크 객체 정리 후 오프라인 원본·카메라를 복원한다.
3. **입력 생명주기**: 기존 `Awake`가 입력 자산을 만들고 `OnEnable`이 즉시 구독하므로 단순 `OnNetworkSpawn`에서 원격 스크립트를 끄는 방식은 금지한다. 기본 비조작 상태의 prefab과 명시적 권한 바인딩으로 초기 한 프레임 입력 누출도 막는다. 기존 순수 로컬 fixture는 기본 오프라인 모드를 유지한다.
4. **스폰/재시작**: 호스트가 승인 대기 슬롯까지 예약하고 스폰 자세를 초기화한 다음 조작을 허용한다. 기존 Awake에서 저장하던 startingPosition을 명시적으로 갱신한다. NetworkTransform 소유자 권한과 상태 NetworkVariable을 사용하며 재시작 텔레포트를 별도 검증한다. 원격은 `PlayerMovement` 시뮬레이션 없이 표시만 한다.
5. **카메라/충돌**: 씬 카메라 1개를 로컬 소유자에 바인딩한다. 원격에서 기존 카메라 검증 오류가 발생하지 않도록 미바인딩 대기 상태를 추가한다. Player 대 Player 충돌을 끄되 Ground 충돌은 유지한다.
6. **장애 정리**: 요청 세대, 취소, 30초 deadline과 늦은 완료 정리. 정상 방장 종료는 `AsHost().DeleteAsync()`. 자동 서비스 호스트 선출은 게임 지속으로 해석하지 않고 기존 호스트 변경/연결 끊김을 감지해 종료한다. 참가자 끊김은 호스트가 해당 서비스 멤버를 제거하여 120초 기본 제거 시간을 기다리지 않게 한다. API와 권한은 설치한 SDK에서 컴파일 검증한다.
7. **실패 후 복구 UI**: 요청 결과는 30초 안에 표시하고 원격 정리 완료를 기다려 실패 표시를 늦추지 않는다. 실패 후 정리 표시는 추가 30초로 제한하며 이후 연결 확인·복구 재시도·오프라인 복귀를 제공한다. 복구 재시도도 30초 안에 결과를 알린다. 원격 정리가 확인되기 전 새 접속은 차단하지만 로컬 정리 후 오프라인 복귀는 허용한다. 미정리 플래그와 늦은 작업 관찰 책임은 복귀 후에도 유지한다.
8. **코드 오류 안내**: 빈 입력은 코드 입력을 안내한다. 잘못된 코드와 종료된 방은 NotFoundOrClosed와 “코드가 잘못되었거나 종료된 방입니다”로 통일한다. 정원 초과·통신 실패와는 구별한다.

## Validation and Delivery Gates

- SDK 설치 후 Unity 컴파일 오류 0, 새 로그 오류 0을 확인한다. 패키지에 따른 Unity Editor 업그레이드는 하지 않는다.
- 기존 이동/카메라/점프/낙사/재시작/글라이더/섬 이동 PlayMode 테스트 전체를 먼저 기준선으로 실행하고 통합 후 재실행한다.
- fake: 입력 검증, 정원 경합, 요청 중복, 타임아웃 후 늦은 성공, 정리 중 재시도, 상태 전이, 오류 분류.
- 실제 NGO: 원격 입력 구독 0, 연결당 캐릭터 1개, 늦은 참가 현재 상태, 자기 재시작, 재참가 슬롯 반환, 호스트 종료.
- 실제 서비스: `quickstart.md`와 spec SC-001~007을 수행한다. 서비스 기본 120초 제거와 Relay TTL을 명세의 연결 감지 SLA로 오인하지 않는다. 정리 지연이 수용 기준을 깨면 완료가 아니라 실패로 기록한다.
- 이 단계에서는 코드/씬/패키지를 수정하지 않았고 실행 시험도 하지 않았다.
