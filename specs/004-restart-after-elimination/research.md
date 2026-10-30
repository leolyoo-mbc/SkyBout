# Research: 기존 Unity 프로젝트의 탈락 후 재시작

## 조사 범위 및 결과

현재 프로젝트의 실제 소스·입력 자산·씬·PlayMode 테스트를 직접 조사했다. 별도 조사 에이전트 도구는 제공되지 않아 로컬 조사로 수행했다. 미해결 NEEDS CLARIFICATION 없음. 외부 라이브러리 추가나 버전 변경은 필요하지 않다.

## 1. 기존 프로젝트 확장

- **Decision**: Unity 6000.3.26f1, C#, Input System 1.20.1, Test Framework 1.6.0을 그대로 사용한다. `Assets/Scenes/SampleScene.unity`와 기존 스크립트를 확장한다.
- **Rationale**: `PlayerMovement`가 이동·점프·접촉 지지·낙사·IMGUI 탈락 안내를 이미 담당한다. 재시작은 이 상태의 명시적인 초기화이므로 별도 관리 계층이 필요하지 않다.
- **Alternatives considered**: 새 프로젝트, GameManager, 재시작 전용 서비스는 단일 플레이어 상태 복귀에 불필요하다. 씬 재로드는 입력 유지·카메라 복귀·씬 등록 상태를 함께 다뤄야 하며 기존 인스턴스를 유지하는 것보다 변경 범위가 크다.

## 2. 입력과 판정 순서

- **Decision**: `Player_Actions.inputactions`의 Player 맵에 Button 액션 Restart와 `<Keyboard>/r` 바인딩을 추가한다. 기존 PlayerInput SendMessages 방식을 따라 `OnRestart(InputValue)`에서 요청하고 FixedUpdate에서 적용한다.
- **Rationale**: 현재 Move/Jump와 같은 입력 경로를 사용하여 테스트용 가상 키보드와 실사용 경로가 일치한다. 생존 중에는 요청을 저장하지 않는다. FixedUpdate는 기존 낙사 판정을 가장 먼저 수행하고 새 탈락 시 재시작 요청을 폐기하여 같은 판정에서 생존 복귀하지 않는다.
- **Alternatives considered**: `Keyboard.current` 직접 폴링은 PlayerInput의 장치·액션 제어를 우회한다. 콜백 안에서 즉시 순간이동하면 물리 판정 순서가 모호해진다.
- **주의**: 기존 코드는 Button 액션의 SendMessages 취소 콜백에 의존하지 않고 `InputSystem.onAfterUpdate`에서 Jump 해제를 관찰한다. Restart도 이 패턴을 재사용해 유지 입력과 초점 복귀 시 새 입력을 구분한다. 탈락 전 키 유지, 같은 입력 갱신에서 press/release, 비활성화 및 초점 전환을 가상 장치로 검증한다. Restart가 없는 입력 자산은 Start에서 기존과 같은 오류 후 비활성화 방식으로 거부한다.

## 3. 복귀 상태와 접촉

- **Decision**: Awake에서 최초 Rigidbody 위치·회전을 저장한다. 재시작은 동일 Rigidbody를 그 위치·회전으로 옮기고 선속도·각속도·탈락·점프 예약·지지 캐시를 초기화한다. 실제 물리 접촉으로 지상 상태를 다시 획득한다.
- **Rationale**: 기존 `HasGroundSupport`는 접촉 집합과 현재 레이캐스트를 함께 요구한다. `grounded = true`만 강제하면 실제 표면이 없는 상태에서도 점프하는 회귀를 만들 수 있다. 복귀는 안전한 시작 표면에서 다음 물리 접촉 처리를 거쳐 완료된다.
- **Alternatives considered**: 플레이어 삭제·재생성은 카메라 타깃과 입력 장치를 다시 연결해야 한다. 낙하 속도를 유지한 순간이동은 즉시 재추락을 유발한다.
- **주의**: 재시작 전용 경로에서는 유지 중인 Move 액션 값을 다시 읽는다. 기존 OnEnable/초점 복귀의 중립 입력 대기 정책을 전역으로 제거하지 않는다. Jump는 해제를 관찰한 뒤 새 입력에만 허용한다. 최초 위치가 유효한 섬 표면이고 낙사 높이 위라는 전제는 SampleScene 및 통합 시험에서 확인한다.

## 4. 탑뷰 복귀

- **Decision**: `TopDownCameraFollow`에 `SnapToTarget()`을 추가하고 초기 카메라 배치와 재시작에서 공유한다. PlayerMovement에 직렬화된 카메라 참조를 추가하여 SampleScene에서 기존 Main Camera를 연결한다.
- **Rationale**: 현재 LateUpdate 보간만 사용하면 깊이 추락한 카메라가 시작 위치로 늦게 따라온다. 명시적인 스냅은 순간이동 직후 시점을 보장하고 평소 보간은 그대로 유지한다. 참조가 없는 카메라 비포함 단위 fixture는 허용하되 실제 씬의 연결은 통합 테스트로 보장한다.
- **Alternatives considered**: 매 프레임 거리 임계값을 검사하는 자동 순간이동 감지는 기존 이동 카메라까지 바꾼다. 매번 씬을 검색하거나 이벤트 버스를 추가할 이유도 없다.
- **주의**: Rigidbody 보간이 활성화되어 있으므로 물리 위치와 Transform이 같은 복귀 위치를 보도록 순간이동 순서를 정하고, 실제 렌더 프레임에서도 카메라가 이전 위치를 읽지 않는지 시험한다.

## 5. 화면 안내와 회귀 검증

- **Decision**: 기존 OnGUI 고대비 패널을 재사용해 ELIMINATED와 `R: 재시작`을 표시한다. 한글 렌더링이 불가능한 기본 폰트 환경에서는 읽을 수 있는 `R: Restart`로 같은 조작 의미를 제공하고 실제 표시를 검증한다. 새 HUD 패키지나 메뉴는 추가하지 않는다.
- **Rationale**: 화면 밖에서도 읽을 수 있는 기존 안내 구조가 이미 있다. 안내의 핵심은 R 키와 재시작 의미를 알리는 것이며 로컬 프로토타입 전체 UI 개편이 아니다.
- **Alternatives considered**: 새 Canvas·버튼·확인창은 요구하지 않은 기능이다.
- **검증**: `RestartTests`에서 새 동작을 검증하고 기존 MovementAndCameraTests, JumpTests, FallEliminationTests를 함께 실행한다. 기존 ‘R 없이 생존 복귀 불가’ 시험은 유지하고, 잘못된 액션 자산의 오류 메시지 기대값만 필요한 경우 갱신한다. SampleScene을 매회 다시 로드하지 않는 20회 반복 통합 시험과 Game 뷰 안내·카메라 관찰을 추가한다.
