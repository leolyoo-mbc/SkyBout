# Tasks: 온라인 2인 방 생성·참가

**Input**: `specs/009-online-room/`의 spec.md, plan.md, research.md, data-model.md, contracts/online-room.md, quickstart.md

**Prerequisites**: 설계 완료. 현재 프로젝트 `D:\Projects\SkyBout`를 직접 수정한다. Unity 6000.3.26f1 및 기존 SampleScene을 유지한다. 헌법은 미작성 템플릿이므로 추가 원칙을 추측하지 않는다.

**Tests**: spec FR-012와 SC-001~007의 검증 요구에 따라 자동 테스트와 실접속 인수를 포함한다. 테스트를 먼저 작성하고 대상 동작 부재로 실패하는지 확인한 뒤 구현한다. 새 SDK 미설치로 인한 컴파일 실패는 동작 테스트의 실패 확인을 대신하지 않는다.

**Organization**: Setup → Foundational → US1(P1) → US2(P1) → US3(P2) → 최종 검증. 체크는 구현 및 해당 검증이 실제 완료됐을 때만 한다.

## Format: `[ID] [P?] [Story] Description`

- `[P]`: 선행 작업 완료 후 같은 단계의 다른 파일 작업과 병렬 가능. 같은 씬·prefab·공유 컨트롤러 변경은 직렬 처리한다.
- `[US1]` / `[US2]` / `[US3]`: spec의 사용자 스토리 번호.
- 경로는 저장소 루트 기준. 새 Unity 자산은 Unity에서 생성·직렬화하고 `.meta`를 함께 관리한다. 생성 `.csproj`/`.slnx`는 수동 수정하지 않는다.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 기존 프로젝트 상태 보존, 의존성 및 테스트 실행 기반 준비.

- [ ] T001 현재 변경을 확인하고 `Assets/Tests/PlayMode/` 전체 기준선 결과와 Unity 버전을 `TestResults/online-room/baseline.md`에 기록한다; 사용자 변경 `SkyBout.slnx`를 되돌리거나 덮어쓰지 않는다.
- [ ] T002 `Packages/manifest.json`에 MPS 2.4.1, NGO 2.13.3, Transport 2.7.4를 추가하고 Unity로 `Packages/packages-lock.json`을 갱신한다; 에디터 업그레이드 없이 컴파일과 실제 의존성/API를 확인한다.
- [ ] T003 `Assets/Scripts/Online/SkyBout.Online.asmdef`와 `Assets/Tests/Online/SkyBout.OnlineTests.asmdef`를 생성해 Runtime 및 설치한 MPS/Core/Auth/NGO/Transport/UI의 실제 어셈블리 이름으로 참조한다; `Assets/Scripts/SkyBout.Runtime.asmdef`에 NGO 의존성을 넣지 않는다.
- [ ] T004 [P] `specs/009-online-room/quickstart.md`에 현재 `ProjectSettings/ProjectSettings.asset`의 Cloud ID 미설정 상태와 사용자 수행 절차를 확인·기록한다; 권한자가 연결할 Cloud 프로젝트/환경·서비스 이용 가능 상태·쿼터/비용 확인 전 실접속 시험은 차단하되 fake 기반 구현은 진행 가능하게 한다.
- [ ] T005 [P] `Assets/Tests/Online/FakeRoomService.cs`와 `Assets/Tests/Online/ManualRoomClock.cs`에 지연·실패·늦은 성공을 제어할 테스트 도구를 작성한다; T003 이후 수행하고 다음 단계 IRoomService 계약에 맞춰 연결한다.

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 모든 스토리의 공통 상태·서비스 경계와 로컬 조작/카메라 경계를 준비한다.

- [ ] T006 `Assets/Scripts/Online/RoomConnectionState.cs`에 State 제약 “Offline, Initializing, Creating, Joining, InRoom, Leaving, Failed”, Failure 제약 “InvalidCode, NotFoundOrClosed, Full, Network, Timeout, Configuration, VersionMismatch, HostEnded”를 정의한다; OperationGeneration은 ulong으로 “요청마다 증가. 지난 요청의 완료가 현재 상태를 덮어쓰지 못한다.”를 적용한다.
- [ ] T007 `Assets/Scripts/Online/IRoomService.cs`에 초기화/인증·생성·코드 참가·퇴장·삭제·멤버 제거와 이벤트 계약을 정의한다; LocalPlayerId는 “서비스의 익명 인증 ID. 로그에 토큰을 남기지 않는다.”, SessionId/JoinCode는 “성공한 현재 방만 참조. 코드는 Trim + ToUpperInvariant 후 검증한다.”를 준수하고 T005 fake를 연결한다.
- [ ] T008 [P] `Assets/Tests/Online/LocalControlLifecycleTests.cs`에 네트워크 스폰 전 입력 비활성, 원격 입력 구독 없음, 메뉴 초점 복귀 시 키 재무장, 기존 오프라인 동작 보존과 런타임 카메라 바인딩/해제 테스트를 작성한다.
- [ ] T009 `Assets/Scripts/PlayerMovement.cs`에 네트워크 비의존 로컬 조작 권한·입력 일시 중단·시작 자세 설정·상태 조회/변경 알림 경계를 추가하고 `Assets/Scripts/PlayerInputReader.cs`의 해제·초점 규칙을 유지한다; prefab의 Awake/OnEnable이 권한 확정 전 입력을 구독하지 않도록 하며 원격 kinematic 전환과 ValidateSettings가 충돌하지 않게 한다.
- [ ] T010 `Assets/Scripts/TopDownCameraFollow.cs`에 런타임 대상 바인딩/해제와 명시적 대상 대기 상태를 추가한다; 기존 잘못된 설정 검증은 보존하고 T008을 통과시킨다.
- [ ] T011 `Assets/Scripts/Online/OnlineRoomController.cs`에 T006 상태와 단일 요청 진입 가드·단조 시계·요청 세대를 연결한다; Deadline 제약 “초기화·인증·서비스 요청·접속·스폰을 합해 생성/참가 30초 제한.”과 ConnectedCount 제약 “게임 연결·스폰 완료 기준 0~2. 서비스 예약 인원과 분리한다.”를 적용하고 성공 판정은 로컬 스폰·카메라·인원 준비 후로 제한한다.
- [ ] T012 `Assets/Scripts/Online/UnityRoomService.cs`에 UGS 초기화/익명 인증 및 서비스 이벤트 수명 관리를 구현한다; Cloud 설정 누락을 Configuration으로 분류하고 `Assets/Scripts/Online/OnlineRoomBootstrap.cs`에 서비스 초기화 전 개발용 별도 인증 profile 선택 옵션을 추가한다; 토큰을 로그나 접속 payload에 복사하지 않는다.

**Checkpoint**: SDK 컴파일, 로컬 조작 경계 테스트 및 기존 회귀 통과. T004 외부 설정은 실접속 체크포인트의 선행 조건이지 로컬 코드 작업의 선행 조건은 아니다.

## Phase 3: User Story 1 - 방을 만들고 친구를 초대하기 (Priority: P1)

**Goal**: 방 생성 후 코드·1/2를 표시하고 방장이 기존 맵에서 자기 캐릭터를 조작한다.

**Independent Test**: 한 실행 환경에서 생성→코드 확인→WASD/점프를 수행하며 캐릭터 1개, 자기 카메라, 반복 생성 차단을 확인한다.

### Tests for User Story 1

- [ ] T013 [P] [US1] `Assets/Tests/Online/CreateRoomTests.cs`에 생성 상태 전이·반복 클릭 차단·서비스 가입만으로 성공 표시 금지·로컬 준비 후 코드/1인 표시의 fake 계약 테스트를 작성한다.
- [ ] T014 [P] [US1] `Assets/Tests/Online/HostSpawnTests.cs`에 방장 단일 스폰·온라인 진입 시 오프라인 원본 입력 차단·안전 스폰 자세가 재시작 자세로 저장되는 통합 테스트를 작성한다.

### Implementation for User Story 1

- [ ] T015 [US1] `Assets/Scripts/Online/UnityRoomService.cs`에 `SessionOptions { MaxPlayers=2, IsPrivate=true }.WithRelayNetwork()` 기반 생성과 `session.Code` 반환을 구현한다; RoomSession 제약 “서비스 발급. 최대 플레이어 2, 공개 검색·호스트 이전 비활성.”을 적용하되 서비스 호스트 자동 선출을 비활성화할 수 없으면 US3의 종료 처리로 대체하고 별도 Relay 할당/StartHost 중복 호출을 금지한다.
- [ ] T016 [US1] `Assets/Scripts/Online/NetworkPlayer.cs`에 방장 스폰과 로컬 소유자 바인딩을 구현한다; NetworkObjectId/OwnerClientId 제약 “호스트가 생성·소유권 배정. 연결당 한 캐릭터.”와 IsHost 제약 “생성자는 방장. 다른 참가자가 자동 승계하지 않는다.”를 게임 로직에 적용한다.
- [ ] T017 [US1] 기존 씬 Player에서 `Assets/Prefabs/NetworkPlayer.prefab`을 만들고 발·날개·물리·Ground 참조 및 NetworkObject를 설정한다; 입력은 기본 비활성, 씬 카메라 참조 없음, 원본 이동 수치와 목표 발 높이 2m를 보존한다.
- [ ] T018 [US1] `Assets/Scripts/Online/OnlineRoomBootstrap.cs`에 오프라인 원본 비활성화, 두 스폰 자세 제공, 로컬 카메라 바인딩과 실패 시 복원을 연결한다; `Assets/Scenes/SampleScene.unity`에 NetworkManager/UnityTransport와 prefab 등록·스폰 지점을 추가하고 기존 섬 배치는 바꾸지 않는다.
- [ ] T019 [US1] `Assets/Scripts/Online/OnlineRoomPanel.cs`에 진입·생성·진행·코드·인원·역할 표시를 구현하고 `Assets/Scenes/SampleScene.unity`에 UI를 연결한다; 패널 입력 중 이동/점프/R을 차단하고 닫을 때 새 입력 규칙을 복원한다.
- [ ] T020 [US1] `Assets/Scripts/Online/OnlineRoomController.cs`에 생성 결과와 게임 준비 이벤트를 합성하고 중복 요청/중복 바인딩을 막아 T013~T014를 통과시킨다; 코드 복사/표시가 준비되지 않은 상태를 성공으로 표시하지 않는다.
- [ ] T021 [US1] `TestResults/online-room/us1.md`에 실제 서비스 방 생성·단일 캐릭터 이동·카메라·생성 시간과 반복 클릭 결과를 기록한다; T004 Cloud 연결이 준비되지 않으면 차단 상태로 남기고 완료 체크하지 않는다.

**Checkpoint**: US1은 한 명만으로 데모 가능하다. 단, 두 사람이 만나는 전체 기능의 완료는 아니다.

## Phase 4: User Story 2 - 코드로 참가해 같은 맵에서 만나기 (Priority: P1)

**Goal**: 두 플레이어가 같은 맵에서 서로의 동작을 보고 자기 캐릭터만 조작한다.

**Independent Test**: US1 방에 다른 회선의 B가 참가해 2/2와 서로의 캐릭터를 확인하고 이동·점프·글라이더·낙사/R을 번갈아 수행한다.

### Tests for User Story 2

- [ ] T022 [P] [US2] `Assets/Tests/Online/JoinRoomTests.cs`에 공백/대소문자 정규화·코드 참가·로컬 준비 후 성공·기존 소속 중 참가 거절 테스트를 작성한다.
- [ ] T023 [P] [US2] `Assets/Tests/Online/PlayerReplicationTests.cs`에 호스트의 참가자 복제본 입력 비활성·동적 물리 소유자만 실행·후발 참가 현재 상태·자기 카메라·자기 재시작/원격 텔레포트 테스트를 작성한다.

### Implementation for User Story 2

- [ ] T024 [US2] `Assets/Scripts/Online/UnityRoomService.cs`에 `JoinSessionByCodeAsync`와 현재 멤버 이벤트를 연결하고 `Assets/Scripts/Online/OnlineRoomPanel.cs`에 코드 입력/참가를 추가한다; 자체 추측 코드 길이를 강제하지 않고 `session.Code`를 사용한다.
- [ ] T025 [US2] `Assets/Scripts/Online/RoomConnectionApproval.cs`에 버전 검사·중복 접속 거절·슬롯 예약을 구현한다; ProtocolVersion/MapVersion 제약 “생성 시 고정. 접속 승인 전 양쪽 일치 검사.”, SpawnSlots 제약 “호스트가 관리하는 0/1 슬롯. 승인 대기까지 예약에 포함한다.”, Status 제약 “Open, Full, Closing, Closed. Closed는 새 게임 연결을 승인하지 않는다.”를 적용한다.
- [ ] T026 [US2] `Assets/Scripts/Online/NetworkPlayer.cs`와 `Assets/Scripts/Online/OnlineRoomBootstrap.cs`에 승인된 슬롯의 단일 스폰과 인원 동기화를 연결한다; SpawnSlot/SpawnPose 제약 “호스트 배정. 출발 섬의 두 안전 지점. 재참가 시 새 슬롯 사용.”을 적용하고 기존 방장 위치는 유지한다.
- [ ] T027 [P] [US2] `Assets/Scripts/Online/OwnerNetworkTransform.cs`에 소유자 권한 복제를 구현한다; Position/Rotation 제약 “소유자 시뮬레이션 결과. 비소유자는 보간 표시만 한다.”를 적용하고 초기 30Hz에서 지연 측정 가능한 설정을 제공한다; T025 이후 T026과 다른 파일에서 병렬 가능하다.
- [ ] T028 [US2] `Assets/Scripts/Online/NetworkPlayer.cs`에 MotionState 제약 “Grounded, Airborne, Gliding, Eliminated. 소유자 쓰기, 전체 읽기.”와 “Eliminated와 Gliding은 동시에 참일 수 없다.”를 적용한다; 원격 날개/탈락 표시를 초기 스냅샷과 변경 이벤트 양쪽에서 갱신한다.
- [ ] T029 [US2] `Assets/Scripts/Online/NetworkPlayer.cs`에 RestartSequence 제약 “소유자가 재시작마다 증가. 위치 순간 복귀 시 보간 이력을 초기화한다.”를 구현한다; `Assets/Scripts/PlayerMovement.cs`의 자기 SpawnPose·속도·입력·전개 초기화 및 자기 탈락 HUD만 유지하도록 연결한다.
- [ ] T030 [US2] `Assets/Prefabs/NetworkPlayer.prefab`, `ProjectSettings/TagManager.asset`, `ProjectSettings/DynamicsManager.asset`에 플레이어 간 충돌 차단/Ground 충돌 유지와 자기·상대 시각 구분을 설정한다; 씬 카메라 1개만 로컬 소유자에 연결되도록 `Assets/Scenes/SampleScene.unity`를 검증한다.
- [ ] T031 [US2] `Assets/Tests/Online/PlayerReplicationTests.cs`와 `Assets/Tests/Online/JoinRoomTests.cs`를 실제 NGO 다중 인스턴스로 통과시키고 결과를 `TestResults/online-room/us2-integration.md`에 기록한다; 같은 PC 테스트는 서로 다른 인증 profile을 사용한다.
- [ ] T032 [US2] `TestResults/online-room/us2-internet.md`에 SC-001~004의 두 회선 생성/참가 10회, 방장 이동 후 참가 5회, 각 동작 10회, 각 낙사/R 5회와 RTT/손실·양쪽 영상/시간을 기록한다; p95 0.5초·생성/참가 15초 기준과 기존 목표 높이 2m/Space 유지 규칙을 확인한다.

**Checkpoint**: 두 사람이 만나는 핵심 P1 수직 기능 완료. 오류 복구/수명 관리까지 완료되기 전 전체 배포 완료로 보지 않는다.

## Phase 5: User Story 3 - 실패·퇴장 후 다시 연결하기 (Priority: P2)

**Goal**: 오류 안내, 참가자 퇴장 후 같은 코드 재참가, 방장 종료 시 전체 종료, 유령 자리 없는 재시도.

**Independent Test**: 잘못된 코드·정원·마지막 자리 경합·통신 실패·참가자 끊김·방장 끊김을 발생시킨 뒤 메뉴/재참가를 검증한다.

### Tests for User Story 3

- [ ] T033 [P] [US3] `Assets/Tests/Online/RoomFailureTests.cs`에 빈/없는/종료 코드·정원·버전 불일치·통신 오류 분류와 마지막 자리 동시 승인 시 한 명만 성공하는 테스트를 작성한다.
- [ ] T034 [P] [US3] `Assets/Tests/Online/RoomCleanupTests.cs`에 요청 30초 내 실패 표시·실패 후 추가 30초 정리 대기 상한·이후 복구 안내/오프라인 복귀·복구 재시도 30초 제한·늦은 생성/참가 성공 정리·미정리 중 새 접속 차단·서비스 복구 후 정상 재시도·종료 이벤트 중복 테스트를 작성한다.
- [ ] T035 [P] [US3] `Assets/Tests/Online/RoomDisconnectTests.cs`에 참가자 서비스 멤버 제거와 슬롯 반환·호스트 삭제·호스트 손실 후 서비스 재선출 시 종료·스폰 중 호스트 종료 테스트를 작성한다.

### Implementation for User Story 3

- [ ] T036 [US3] `Assets/Scripts/Online/UnityRoomService.cs`에 설치 SDK 예외/연결 거절 이유를 Failure 분류로 매핑하고 `Assets/Scripts/Online/OnlineRoomPanel.cs`에 원인별 안내·정리 중 안내·복구 재시도/오프라인 복귀를 연결한다; 빈 입력은 코드 입력을 안내하고 잘못된 코드/종료된 방은 NotFoundOrClosed와 “코드가 잘못되었거나 종료된 방입니다”로 통일한다; T033에 두 경우의 동일 메시지 검증을 포함한다.
- [ ] T037 [US3] `Assets/Scripts/Online/OnlineRoomController.cs`에 요청 세대 무효화·전체 30초 deadline·취소·늦은 결과 관찰/멱등 롤백을 완성한다; 실패는 정리를 기다리지 않고 표시하며 정리 안내는 실패 후 추가 30초로 제한한다. 이후 연결 확인·30초 제한의 복구 재시도·오프라인 복귀를 제공한다. 미정리 중 새 접속은 차단하되 오프라인 복귀를 허용하고 정리 책임/플래그를 유지한다; 정리 확인 후 접속 재시도를 허용하며 이전 작업이 새 연결을 시작/종료하지 못하게 한다.
- [ ] T038 [US3] `Assets/Scripts/Online/UnityRoomService.cs`에 참가자 LeaveAsync·방장 AsHost().DeleteAsync·역할별 멤버 제거를 구현한다; Deleted/RemovedFromSession/SessionHostChanged/연결 종료를 구독·해제하며 SDK 자동 네트워크 종료와 수동 종료가 중복 부작용을 만들지 않게 한다.
- [ ] T039 [US3] `Assets/Scripts/Online/RoomConnectionApproval.cs`와 `Assets/Scripts/Online/UnityRoomService.cs`에 SDK 확인 멤버와 client ID의 매핑을 연결한다; 참가자 끊김 시 호스트가 해당 멤버를 명시 제거하고 예약 슬롯·객체를 반환하여 120초 기본 제거를 기다리지 않고 같은 코드 재참가를 허용한다; 임의로 전달받은 남의 ID는 제거하지 않는다.
- [ ] T040 [US3] `Assets/Scripts/Online/OnlineRoomController.cs`에 원래 호스트 손실 즉시 게임 종료와 새 서비스 호스트가 된 잔여 참가자의 세션 삭제를 연결한다; 살아 있는 NGO 호스트가 없는 방의 참가를 성공 처리하거나 호스트 이전으로 게임을 이어가지 않는다.
- [ ] T041 [US3] `Assets/Scripts/Online/OnlineRoomBootstrap.cs`와 `Assets/Scenes/SampleScene.unity`에 연결 감지 30초 이하 transport 설정, 확정 후 5초 이내 캐릭터/인원 정리·오프라인 원본/카메라 복원·종료 안내를 연결한다; 앱 quit의 비동기 완료를 보장으로 사용하지 않는다.
- [ ] T042 [US3] `Assets/Tests/Online/RoomFailureTests.cs`, `Assets/Tests/Online/RoomCleanupTests.cs`, `Assets/Tests/Online/RoomDisconnectTests.cs`를 통과시키고 `TestResults/online-room/us3-automated.md`에 결과와 미검증 서비스 경로를 기록한다.
- [ ] T043 [US3] `TestResults/online-room/us3-internet.md`에 SC-005~006의 각 오류 3회·정리 지연 복구 3회·동시 참가 5회·참가자 정상/비정상 퇴장 각각 5회와 재참가·방장 정상/비정상 종료 각각 5회를 기록한다; 코드 오류/종료 방 동일 안내, 요청 후 30초 실패 표시, 실패 후 추가 30초 이내 복구 안내, 복구 시도 30초 결과, 오프라인 복귀 및 서비스 복구 후 재참가를 확인한다; 끊김 확정 30초 및 확정 후 5초 반영 기준 위반은 실패로 남긴다.

**Checkpoint**: 정상·장애·재시도 모두 검증. 서비스 장애로 원격 정리가 지연되면 그 사실을 기록하고 해당 인수 작업은 완료 처리하지 않는다.

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: 전체 회귀·수용 기준 추적·사용자 안내와 최종 완료 판정.

- [ ] T044 `Assets/Tests/PlayMode/` 및 `Assets/Tests/Online/` 전체를 재실행하고 `TestResults/online-room/final-regression.md`에 기준선 대비 실패/건너뜀·새 콘솔 오류·플레이어 입력 누출·네트워크 정리 후 잔여 객체를 기록한다; 필요 수정은 원인 파일에서 수행하고 기존 테스트를 삭제해 통과시키지 않는다.
- [ ] T045 [P] `specs/009-online-room/quickstart.md`를 실제 설치 API/서비스 환경·UI 진입·개발 인증 profile 사용법과 일치시키고 `specs/009-online-room/contracts/online-room.md`의 정리/제약과 구현 차이가 없는지 확인한다; T044의 테스트 실행과 병렬 가능하다.
- [ ] T046 `TestResults/online-room/usability.md`에 두 시험자가 안내만으로 생성→만남→퇴장→재참가를 각자 1회 완료하고 자기 캐릭터/현재 방 상태를 구별하는 SC-007 확인 결과를 기록한다.
- [ ] T047 `TestResults/online-room/acceptance.md`에 FR-001~012→T013~T046/SC-001~007 추적표, 실측 시간 분포·영상/로그 경로·서비스 버전·실패/미검증 사유를 정리한다; 공유 기록에서 코드/인증 정보를 가리고 fake/LAN 결과를 인터넷 인수로 대체하지 않는다.
- [ ] T048 `specs/009-online-room/tasks.md`와 `specs/009-online-room/plan.md`의 실제 완료/차단 상태를 갱신한다; `TestResults/online-room/acceptance.md`에서 Cloud 설정·다른 회선 시험·컴파일·수용 기준 실패가 남으면 구현/온라인 검증 완료를 주장하지 않는다.

## Dependencies & Execution Order

### Phase Dependencies

```text
Setup T001~T005 → Foundational T006~T012 → US1 T013~T021
                                              ↓
                                        US2 T022~T032
                                              ↓
                                        US3 T033~T043
                                              ↓
                                        Polish T044~T048

T004 외부 Cloud 준비 ─→ T021, T032, T043, T046 실제 서비스 시험
```

- Setup: T001 기준선 후 T002 패키지, T003 어셈블리. T004는 독립 환경 문서 확인, T005는 T003 후 테스트 도구 준비.
- Foundational: T006→T007→T011/T012. T008 테스트 작성→T009/T010 코어 변경 순서. T011은 상태/계약을 사용하고 T012는 SDK를 사용하므로 두 파일 구현은 선행 계약 이후 분리 가능하다.
- US1은 기반 완료 후 단독 구현/데모 가능. T013/T014→T015/T016→T017/T018→T019/T020→T021. 방장 종료 등 완성된 복구는 US3에서 제공한다.
- US2는 US1 방 생성/씬 통합에 의존한다. T022/T023→T024/T025→T026/T027→T028/T029/T030→T031/T032. 서로 다른 슬롯/후발 상태를 별도 검증한다.
- US3는 US1/US2의 서비스/연결/스폰을 확장한다. T033~T035→T036~T041→T042/T043. 오류·정리는 별도로 시험 가능하지만 실제 동작 기반은 공유하므로 스토리 전체를 병렬 구현하지 않는다.
- 최종 검증은 모든 스토리 통합 후 수행한다. 실제 실행에 필요한 외부 준비가 안 되면 로컬 구현과 자동 테스트까지 진행하고 해당 실접속 체크포인트에서 차단을 보고한다.

### Parallel Opportunities / Examples per User Story

병렬 표시는 선행 계약과 테스트 기반이 준비됐다는 조건이며 별도 에이전트 실행 지시는 아니다.

- **US1**: T013 `CreateRoomTests.cs`와 T014 `HostSpawnTests.cs` 테스트 작성은 서로 다른 파일이므로 병렬 가능. 이후 공유 씬·컨트롤러 변경은 직렬.
- **US2**: T022 `JoinRoomTests.cs`와 T023 `PlayerReplicationTests.cs` 병렬 작성. T025 후 T027 `OwnerNetworkTransform.cs` 작성은 T026 `NetworkPlayer.cs`/bootstrap 스폰 연결과 병렬 가능하고 복제 통합 전 합친다.
- **US3**: T033 `RoomFailureTests.cs`, T034 `RoomCleanupTests.cs`, T035 `RoomDisconnectTests.cs` 병렬 작성. 구현은 공유 `UnityRoomService.cs`/컨트롤러를 변경하므로 직렬.
- **Cross-cutting**: T044 자동 회귀 실행과 T045 문서 동기화 병렬 가능. T047 최종 보고서는 모든 시험 결과가 모인 뒤 작성한다.

## Implementation Strategy

### MVP First

1. Setup/기반 완료 후 US1을 구현해 한 명이 코드가 있는 방에서 플레이하는 최소 데모를 검증한다.
2. 사용자의 핵심 목표인 **두 플레이어 만남 MVP는 US1+US2**다. US1만으로 전체 기능을 완료했다고 하지 않는다.
3. US3의 정상 퇴장·장애 정리·재시도와 최종 검증까지 완료해야 이번 명세 전체 완료다.

### Incremental Delivery

기존 오프라인 회귀 → 방장 단일 플레이 → 두 명 실접속/복제 → 실패·퇴장·재참가 → 다른 회선 반복 인수 순서로 전달한다. 단계별 성공은 다음 단계 완료를 뜻하지 않는다. 미설정 서비스나 외부 환경 부재는 완료 체크 대신 차단 사유로 기록한다.

## Coverage & Validation Summary

| 요구사항 | 주요 작업 |
| --- | --- |
| FR-001 생성/표시 | T013, T015, T019~T021 |
| FR-002 인터넷 코드 참가 | T012, T024, T032 |
| FR-003 정원/중복 | T016, T025~T026, T033, T039, T043 |
| FR-004 동일 맵/안전 스폰 | T014, T017~T018, T026, T030~T032 |
| FR-005 자기 입력/카메라/HUD | T008~T010, T016, T023, T029~T031 |
| FR-006 상대 동작/후발 상태 | T023, T027~T029, T031~T032 |
| FR-007 기존 조작/자기 재시작 | T009, T017, T029, T032, T044 |
| FR-008 오류/30초 | T006, T011, T033~T034, T036~T037, T043 |
| FR-009 반복/유령 자리 방지 | T011, T013, T020, T022, T034, T037~T039 |
| FR-010 참가자 정리/재참가 | T035, T038~T039, T041~T043 |
| FR-011 방장 종료 | T035, T038, T040~T043 |
| FR-012 증거/정직한 완료 | T001, T021, T031~T032, T042~T048 |

총 48개: Setup 5, Foundational 7, US1 9, US2 11, US3 11, Polish 5. 모든 실행 작업은 체크박스·순차 ID·해당 스토리 라벨·명시적 파일 경로를 가진다. 이 목록 생성 자체는 구현이나 테스트 실행을 뜻하지 않는다.
