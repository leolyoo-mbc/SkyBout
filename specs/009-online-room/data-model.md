# Data Model: 온라인 2인 방

## RoomConnection (각 실행 환경의 비영구 상태)

| Field | Type | Rule |
| --- | --- | --- |
| State | enum | Offline, Initializing, Creating, Joining, InRoom, Leaving, Failed |
| OperationGeneration | ulong | 요청마다 증가. 지난 요청의 완료가 현재 상태를 덮어쓰지 못한다. |
| SessionId / JoinCode | string | 성공한 현재 방만 참조. 코드는 Trim + ToUpperInvariant 후 검증한다. |
| LocalPlayerId | string | 서비스의 익명 인증 ID. 로그에 토큰을 남기지 않는다. |
| IsHost | bool | 생성자는 방장. 다른 참가자가 자동 승계하지 않는다. |
| ConnectedCount | int | 게임 연결·스폰 완료 기준 0~2. 서비스 예약 인원과 분리한다. |
| Deadline | monotonic time | 초기화·인증·서비스 요청·접속·스폰을 합해 생성/참가 30초 제한. |
| Failure | enum + message | InvalidCode, NotFoundOrClosed, Full, Network, Timeout, Configuration, VersionMismatch, HostEnded |

서비스가 방의 존재·가입을 관리하고 호스트의 연결 승인 단계가 최종 2인 제한과 스폰 슬롯을 관리한다. 가입 성공만으로 InRoom을 표시하지 않는다. 로컬 플레이어 스폰, 카메라 바인딩, 현재 인원 수신까지 끝나야 한다.

### Transitions

- Offline/Failed → Initializing → Creating 또는 Joining → InRoom.
- 중간 실패 또는 요청 deadline 도달 → Failed로 먼저 결과를 알리고 롤백/네트워크 정리를 진행한다. 실패 표시가 정리 완료를 기다려 30초를 초과하지 않는다.
- InRoom → Leaving → Offline. 참가자는 자기 멤버십만 제거한다. 방장은 세션 종료 후 네트워크를 종료한다.
- 방장 연결 끊김 → 참가자는 Failed(HostEnded); 참가자 연결 끊김 → 방장은 InRoom(1/2) 유지.
- 참가자 끊김 시 방장은 연결 ID와 서비스 멤버 ID 매핑으로 해당 멤버를 명시 제거한다. 서비스 기본 120초 제거를 기다려 같은 코드 재참가를 막지 않는다. 방장 손실 후 서비스 호스트로 선출된 참가자는 남은 세션을 삭제하고 게임을 종료한다.
- 생성/참가 도중 추가 요청은 거절. 시간 초과는 요청 세대를 무효화한다. 늦게 성공한 서비스 결과는 별도 정리하며 게임을 시작하거나 새 요청의 네트워크를 종료하지 않는다.
- 정리 작업이 남으면 같은 인증 ID의 재가입은 보류하고 정리 중 안내를 표시한다. 이 표시는 실패 안내 후 추가 30초로 제한하며 이후 Failed 상태에서 연결 확인·복구 재시도·오프라인 복귀를 제공한다. 별도 복구 필요 플래그로 새 생성/참가를 막되 오프라인 복귀를 막지 않는다. 복구 재시도는 30초 안에 결과를 알리고, 이전 작업/멤버십의 정리 완료를 확인한 뒤 새 생성/참가를 허용한다. 서비스 장애 중 즉시 원격 자리 삭제는 보장하지 않는다. 늦은 결과 관찰과 정리 책임은 오프라인 복귀 후에도 유지한다.

## RoomSession (서비스 방과 호스트 게임 상태)

| Field | Authority / Validation |
| --- | --- |
| SessionId, JoinCode | 서비스 발급. 최대 플레이어 2, 공개 검색·호스트 이전 비활성. |
| HostPlayerId | 생성자. 호스트 교체 감지 시 방 종료로 처리한다. |
| ProtocolVersion / MapVersion | 생성 시 고정. 접속 승인 전 양쪽 일치 검사. |
| Status | Open, Full, Closing, Closed. Closed는 새 게임 연결을 승인하지 않는다. |
| SpawnSlots | 호스트가 관리하는 0/1 슬롯. 승인 대기까지 예약에 포함한다. |

방장 비정상 종료 직후 서비스 코드가 잠시 남을 수 있다. 살아 있는 호스트로 연결·스폰되지 않으면 참가 실패로 처리하며 성공으로 표시하지 않는다. 서비스 만료/정리 지연을 방이 계속 플레이 가능하다는 뜻으로 해석하지 않는다.

## NetworkPlayer

| Field | Authority / Validation |
| --- | --- |
| NetworkObjectId / OwnerClientId | 호스트가 생성·소유권 배정. 연결당 한 캐릭터. |
| SpawnSlot / SpawnPose | 호스트 배정. 출발 섬의 두 안전 지점. 재참가 시 새 슬롯 사용. |
| Position / Rotation | 소유자 시뮬레이션 결과. 비소유자는 보간 표시만 한다. |
| MotionState | Grounded, Airborne, Gliding, Eliminated. 소유자 쓰기, 전체 읽기. |
| RestartSequence | 소유자가 재시작마다 증가. 위치 순간 복귀 시 보간 이력을 초기화한다. |

늦은 참가자는 이벤트 재생이 아닌 현재 상태의 초기 스냅샷을 받는다. Eliminated와 Gliding은 동시에 참일 수 없다. 재시작은 자기 SpawnPose로만 복귀한다. 신뢰 기반 프로토타입이며 경쟁용 부정행위 방지는 범위 밖이다.

## LocalPlayerBinding

- 입력 구독, 동적 Rigidbody, 탈락 HUD, 카메라 추적은 로컬 소유자에만 바인딩한다.
- 원격 복제본은 입력 구독 없음, 이동 시뮬레이션 없음, kinematic Rigidbody, 상태 시각화만 활성.
- 오프라인 원본과 온라인 캐릭터가 동시에 입력을 받지 않는다.
- 플레이어 간 충돌은 비활성화하고 Ground 충돌은 유지한다. 상대가 발밑 지면으로 판정되거나 서로 밀지 않는다.
- 카메라는 씬에 한 개를 유지한다. prefab은 씬 카메라 참조를 저장하지 않는다.
