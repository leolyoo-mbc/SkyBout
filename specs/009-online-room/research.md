# Research: 기존 Unity 프로젝트 온라인 2인 방

**Date**: 2026-11-02

## 1. 인터넷 방 구성과 버전

**Decision**: MPS Sessions 2.4.1 + NGO 2.13.3 + Unity Transport 2.7.4, Relay 기반 client-server host. 익명 인증은 UI 없이 수행한다. 공식 레지스트리 선언상 Unity 6000.3과 호환되며 실제 프로젝트 설치 검증은 별도다.

**Rationale**: 공유 코드와 NAT 통과를 제공하며 별도 서버나 장비 설정이 필요 없다. MPS가 Lobby heartbeat·Relay·NGO 시작을 조율한다. `session.Code`를 공유하고 별도 Relay 코드를 중복 생성하지 않는다. NGO 레지스트리 최신 3.1.0은 Unity 6000.7 이상이므로 이 프로젝트에 설치하지 않는다.

**Alternatives considered**: 수동 Lobby+Relay SDK는 중복 생명주기 관리가 필요하다. 직접 IP/포트는 인터넷·장비 무설정 요구에 부적합하다. 전용 서버는 이번 2인 프로토타입에 과도하다. 분산 권한/호스트 이전은 요구 범위 밖이다.

Sources:
- https://docs.unity.com/en-us/mps-sdk/sessions-overview
- https://docs.unity.com/en-us/mps-sdk/create-session
- https://docs.unity.com/en-us/mps-sdk/join-session
- https://packages.unity.com/com.unity.services.multiplayer
- https://packages.unity.com/com.unity.netcode.gameobjects
- https://packages.unity.com/com.unity.transport

## 2. Rigidbody 권한

**Decision**: 호스트 스폰 + 소유자 이동 시뮬레이션 + 소유자 권한 NetworkTransform + 지속 상태 복제. 원격 물리는 kinematic으로 두고 PlayerMovement 입력/FixedUpdate를 실행하지 않는다. NetworkRigidbody 자동 구성에 의존하기보다는 소유권과 물리 전환을 명시적으로 관리하여 기존 ValidateSettings와의 충돌을 피한다.

**Rationale**: 현재 PlayerMovement는 입력 수신 시 접지 상태로 점프/전개를 결정하고 자체 Rigidbody·초점·키 해제 상태를 관리한다. 호스트 권한으로 변경하면 원격 입력 처리·예측·초점 전달까지 광범위하게 재설계해야 한다. 기존 조작 보존을 우선하고 플레이어 간 충돌을 제외하는 이번 범위에서는 소유자 시뮬레이션이 더 작다.

**Alternatives considered**: 공식 물리 가이드의 서버 권한 NetworkRigidbody는 경쟁 게임의 더 좋은 출발점이지만 현재 단계에는 조작 지연과 변경 범위가 커진다. 두 쪽 동적 물리 동시 실행은 이중 중력·충돌 불일치를 만들므로 거절한다. 향후 전투 도입 시 권한 선택을 재검토해야 한다.

Sources:
- https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/advanced-topics/physics.html
- 로컬 근거: `Assets/Scripts/PlayerMovement.cs`, `PlayerInputReader.cs`, `TopDownCameraFollow.cs`

## 3. 종료·서비스 정리와 재참가

**Decision**: 정상 참가자 `LeaveAsync()`, 정상 방장 `AsHost().DeleteAsync()`. 예상치 못한 방장 끊김 시 모든 참가자의 게임을 종료하고 서비스 자동 호스트 선출을 세션 계속으로 사용하지 않는다. 새 서비스 호스트가 된 참가자는 남은 세션도 삭제한다. 참가자 연결이 끊기면 방장이 서비스의 해당 멤버를 명시적으로 제거하며 슬롯을 반환한다.

**Rationale**: MPS의 호스트 나가기는 방 삭제와 다르며 세션 호스트 선출은 NGO 게임 상태 이전과 다르다. 기본 disconnected-player 제거/host migration은 120초 이상이므로 명세의 30초 연결 감지와 즉시 재참가를 서비스 기본값만으로 달성할 수 없다. 서비스 멤버와 연결 ID 매핑은 승인된 자기 플레이어 정보로 구성하고 클라이언트가 남의 멤버 ID를 지정해 제거하게 하지 않는다.

**Alternatives considered**: `StopNetworkAsync()`만 호출하면 방이 남는다. 기본 자동 제거를 기다리면 빈자리 재참가가 지연된다. 크래시 때 비동기 quit 처리 완료를 기대할 수 없다.

Sources:
- https://docs.unity.com/en-us/mps-sdk/leave-a-session
- https://docs.unity.com/en-us/mps-sdk/delete-a-session
- https://docs.unity.com/en-us/mps-sdk/session-host-migration
- https://docs.unity.com/en-us/mps-sdk/config-options
- https://docs.unity3d.com/Packages/com.unity.services.multiplayer@2.4/api/Unity.Services.Multiplayer.ISession.html

## 4. 타임아웃·늦은 완료

**Decision**: 시작부터 30초 단일 요청 예산, 세대 번호와 취소 토큰, 멱등 정리. SDK 작업의 늦은 완료도 관찰해 세션을 닫거나 나간다. 새 접속 재시도는 이전 정리가 끝나야 허용한다. 다만 실패 표시는 원격 정리를 기다리지 않으며, 실패 후 정리 안내는 추가 30초로 제한한다. 이후 연결 확인·30초 제한의 복구 재시도·오프라인 복귀를 제공하고, 늦은 작업 정리 책임을 유지한다. 서비스 장애 중 즉시 원격 자리 삭제는 보장하지 않는다.

**Rationale**: SDK 취소 API가 있어도 백엔드 예약의 원자적 롤백을 보장하지 않는다. 단순 UI timeout 뒤 새 요청을 시작하면 유령 자리·중복 세션·새 연결 종료를 만들 수 있다. deadline에는 인증과 게임 스폰도 포함한다.

**Alternatives considered**: `Task.WhenAny` 후 원래 작업을 버리는 방식은 누수 위험. 무한 대기는 명세 위반.

Source: https://docs.unity3d.com/Packages/com.unity.services.multiplayer@2.4/api/Unity.Services.Multiplayer.IMultiplayerService.html

## 5. 기존 씬 통합과 실행 전제

**Decision**: 기존 SampleScene 섬과 오프라인 Player를 보존하고 별도 online prefab을 추가한다. 온라인 진입 동안 원본만 비활성화한다. 패키지 의존성은 별도 하위 asmdef에 격리한다. 검증 플랫폼은 우선 Windows다.

**Rationale**: 프로젝트에는 SampleScene 하나와 이동 회귀 테스트가 있다. PlayerMovement는 활성화 즉시 키보드를 구독하고 Awake에 재시작 자세를 저장한다. 카메라도 직렬화된 단일 Player 참조를 검증하므로 런타임 바인딩이 필요하다. 프로젝트의 Cloud ID는 비어 있어 실제 UGS 환경 준비는 외부 실행 게이트다.

**Alternatives considered**: 새 Unity 프로젝트 또는 맵 복제는 사용자의 직접 통합 요구와 회귀 보존에 불리하다. 테스트용 가짜 방만 제공하면 인터넷 요구를 충족하지 못한다.

Sources:
- https://docs.unity.com/en-us/mps-sdk/install-and-upgrade
- https://docs.unity.com/en-us/mps-sdk/init-ugs
- https://docs.unity.com/en-us/mps-sdk/tutorials/build-your-first-session
- `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/ProjectSettings.asset`, `Assets/Scenes/SampleScene.unity`

## Research status

설계 선택은 해결됐다. 실제 SDK 설치/컴파일·Cloud 연결·다른 회선 시험은 아직 수행하지 않았다. Cloud 이용 권한/쿼터/비용 및 공개 배포 시 인증 관련 고지 의무는 배포 전 확인한다.
