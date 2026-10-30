# Contract: 플레이어 입력·상태·씬 연결

## 1. 사용자 입력과 표시

| 입력/상황 | 허용 조건 | 결과 |
| --- | --- | --- |
| WASD·기존 방향키 | 생존·초점 있음·중립 대기 해제 | 기존 화면 기준 수평 이동 |
| Space 새 누름 | 생존·초점 있음·키 해제 준비·현재 실제 지지 | 다음 물리 갱신에서 한 번 점프 |
| R 새 누름 | 이미 탈락·활성·초점 있음·키 해제 준비 | 다음 물리 갱신에서 한 번 복귀 |
| 생존 R·공중 Space | 허용 조건 불충족 | 무시하며 미래 요청으로 보관하지 않음 |
| 탈락 상태 | 유효한 R 없음 | 이동·점프 조작 차단, 탈락·R 안내 지속 |
| 초점 상실·비활성화 | 모든 상태 | 수평 입력·예약 정리, 초점 복귀만으로 재실행 안 함 |

낙사 판정이 새 R 요청보다 우선한다. 재시작 후 유지 WASD는 재개하고 유지 Space는 해제 후 새 누름을 요구한다. 자세·속도 복원, 카메라 즉시 복귀와 첫 복귀 렌더의 안내 제거를 유지한다. 자세한 전이·필드는 [data-model.md](../data-model.md)를 따른다.

## 2. 런타임 소유권

- PlayerMovement만 생성된 PlayerActions 인스턴스를 소유한다. PlayerInput, InputValue, SendMessages 기반 입력 수신과 PlayerInput 문자열 액션 조회는 제거한다.
- OnEnable: named performed/canceled 메서드와 onAfterUpdate를 한 번 구독하고 Player 맵을 Enable한다.
- OnDisable: 이벤트·관찰 구독 해제 후 Player 맵 Disable, 예약·입력·지지 정리. OnDestroy: 생성 자원 Dispose.
- 물리 변경은 기존처럼 FixedUpdate에서 수행한다. canceled는 유효한 짧은 입력 요청을 잃게 만들지 않아야 한다.
- 활성 control의 실제 중립·해제를 확인한다. 값이 0인 것과 키가 해제된 것을 동일시하지 않는다.
- `public bool IsEliminated`는 읽기 전용으로 유지한다. 새 입력 주입용 공개 API나 상태 setter는 만들지 않는다.

## 3. 생성 자산 계약

- Source: `Assets/Player_Actions.inputactions`; GUID `f0a2cf4a96d9f914da69d6bb017890e3` 유지.
- Generate C# Class: enabled; class `PlayerActions`; path `Assets/Scripts/Input/PlayerActions.cs`; namespace 없음.
- 기존 Player/Move, Player/Jump, Player/Restart 이름·ID·타입·바인딩과 initialStateCheck, 제어 스킴 보존. 불필요한 `Assets/InputSystem_Actions.inputactions`는 변경하지 않는다.
- 생성 코드는 재생성 가능한 산출물이며 손으로 수정하지 않는다. 원본과 생성 인스턴스의 일치를 시험한다.
- 생성 경로는 SkyBout.Runtime.asmdef 아래여야 한다. 새 asmdef·패키지·언어 버전 재정의는 요구하지 않는다.
- 런타임 mask는 Keyboard&Mouse. 기존 비키보드 바인딩은 자산에 보존하되 이번 기능에서 자동 장치 전환을 구현하지 않는다.

## 4. 씬 마이그레이션 계약

- `Assets/Scenes/SampleScene.unity` Player에서 PlayerInput 컴포넌트를 실제 삭제하고 컴포넌트 목록 참조도 제거한다. 비활성 상태로 남기지 않는다.
- 현재 PlayerInput 로컬 fileID는 `2083166631`이며 삭제 전 참조 검색으로 다른 연결이 없는지 확인한다.
- PlayerMovement, Rigidbody, Collider, GroundCheck와 cameraFollow 참조, 기존 GameObject·Transform 식별자 및 직렬화 설정은 유지한다. Missing Script·dangling reference가 없어야 한다.
- 플레이어와 카메라를 새로 만들지 않으며 사용자 설정을 재입력하게 하지 않는다.
- `TopDownCameraFollow` 구현은 원칙적으로 변경하지 않는다. 기존 SnapToTarget을 사용한다.

## 5. 테스트 이행 계약

- 기존 다섯 클래스: MovementAndCameraTests, JumpTests, FallEliminationTests, RestartTests, RestartSceneTests. PlayerInput 생성·설정·진단 및 실제 씬의 GetComponent<PlayerInput>를 모두 제거한다.
- 가상 Keyboard/Mouse와 InputSystem 이벤트로 실제 생성 인스턴스를 구동한다. 테스트 소유 인스턴스의 devices를 제한하고 teardown에서 장치·전역 입력/물리 모드·씬을 복원한다.
- 실제 씬 통합 시 fixture 입력 맵을 비활성화하여 두 플레이어 입력이 경쟁하지 않게 한다. 씬 플레이어의 생성 인스턴스를 가상 장치에 연결하며 활성 맵의 devices 변경이 만드는 취소·재동기화 이후 중립을 확인한다.
- 테스트의 FixedUpdate/OnApplicationFocus/LateUpdate 호출을 위한 MonoBehaviour.SendMessage는 입력 라우팅 SendMessages와 별개다. 이 호출을 제거하기 위한 별도 실행 계층은 만들지 않는다.
- 삭제된 grounded 조회는 Enum 관찰과 실제 지지·속도 결과로 바꾼다. pending 요청·초점·키 해제 조건의 검증은 유지한다.
- 과거 PlayerInput의 누락 액션 오류 시험은 원본/생성 계약의 누락·불일치 검증으로 이행한다. 비교 검증이 잘못된 원본·생성 조합을 실제로 거부하는 음성 사례도 포함하며, 더 이상 존재하지 않는 PlayerInput 오류 문자열을 보존하지 않는다.
- 신규 `PlayerInputRefactorTests.cs`에는 생성 자산 일치·PlayerInput 부재·재활성화 20회 후 단일 처리·짧은 입력·반대 이동 키 중립 경계를 검증한다. 기존 클래스의 동등 시험을 재사용할 수 있고 중복 시험은 만들지 않는다.
- 첫 복귀 렌더 안내 제거는 실제 표시 영역을 비교하여 검증한다. 상태 bool만으로 표시 제거를 통과시키지 않는다.

## 6. 호환 범위

기존 플레이 입력·물리·공개 IsEliminated는 유지한다. PlayerInput 기반 내부 함수, private grounded/isEliminated, 외부 주입 액션 자산 의존은 의도적으로 폐기한다. 다중 사용자 페어링·리바인딩 UI·게임패드 자동 전환은 이번 범위가 아니다. 기존 004 단계의 미완료 검수는 이 계약 작성만으로 완료되지 않는다.
