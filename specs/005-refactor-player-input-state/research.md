# Research: 플레이어 입력·상태 처리 리팩토링

**Date**: 2026-10-30
**Status**: 설계에 필요한 미확정 사항 해소. 구현·실행 검증은 하지 않음.

## 조사 방법과 근거

별도 연구 에이전트 실행 도구가 제공되지 않아 아래 조사 항목을 로컬 소스·설정·패키지 구현에서 직접 확인했다.

- 생성 코드와 어셈블리: `Assets/Player_Actions.inputactions`, `.meta`, `Assets/Scripts/SkyBout.Runtime.asmdef`, Input System 패키지의 `InputSystem/Editor/AssetImporter/InputActionCodeGenerator.cs`.
- 이벤트와 수명: 같은 패키지의 `InputSystem/Runtime/Actions/InputAction.cs`. 액션은 기본적으로 일치하는 모든 장치를 관찰하고, 이벤트는 입력 갱신 중 발생하며, 동일 프레임 내 값 변경도 관찰한다.
- 상태·물리·입력 경계: `Assets/Scripts/PlayerMovement.cs`와 기존 5개 PlayMode 테스트 클래스.
- 씬·검증: `Assets/Scenes/SampleScene.unity`, `specs/004-restart-after-elimination/{tasks,quickstart}.md`.
- 환경: Unity 6000.3.26f1, Input System 1.20.1. 생성된 런타임 csproj는 C# 9.0이므로 C# 8.0 switch expression·tuple pattern·when guard를 지원한다. 언어 버전을 낮추지 않는다.

## 1. 생성 클래스는 기존 런타임 어셈블리 안에 배치

**Decision**: 기존 `Player_Actions.inputactions`의 Generate C# Class를 활성화하고 클래스 이름은 `PlayerActions`, 경로는 `Assets/Scripts/Input/PlayerActions.cs`, namespace는 비워 둔다. 생성 파일과 Unity `.meta`를 버전 관리한다. 액션 자산 GUID, 맵·액션·바인딩 ID, 이름·타입·initialStateCheck와 제어 스킴은 유지한다.

**Rationale**: 생성기는 액션 JSON을 포함하는 생성자, typed accessor, bindingMask, devices, Dispose를 제공한다. 자산 옆인 Assets 루트에 생성하면 Assembly-CSharp에 들어가 SkyBout.Runtime에서 참조할 수 없다. Scripts 아래면 기존 asmdef와 Unity.InputSystem 참조를 그대로 사용한다.

**Alternatives considered**: 루트 생성 후 asmdef 변경은 불필요한 참조 구조 변경이다. 생성 파일 수동 편집은 재생성 시 유실된다. 별도 수제 입력 래퍼는 자동 생성 클래스와 중복된다.

## 2. PlayerInput 완전 제거와 직접 이벤트 구독

**Decision**: PlayerMovement가 Awake에서 `new PlayerActions()`를 생성한다. OnEnable에서 Move의 performed/canceled, Jump와 Restart의 performed/canceled 및 필요한 onAfterUpdate 관찰을 구독하고 Player 맵을 활성화한다. OnDisable에서는 먼저 구독을 해제하고 맵을 비활성화한 뒤 요청·입력을 정리한다. OnDestroy에서 Dispose한다. 생성 실패·설정 검증 실패 경로에서도 부분 초기화에 안전한 해제가 필요하다.

**Rationale**: PlayerInput 컴포넌트, 필드·GetComponent, InputValue 콜백과 문자열 FindAction을 제거할 수 있다. 구독 해제 후 Disable하면 Disable로 발생하는 canceled가 키 해제나 새 요청으로 오인되지 않는다. 같은 인스턴스를 재활성화하며 중복 구독을 막는다.

**Alternatives considered**: PlayerInput과 생성 클래스를 함께 활성화하면 입력 소유자가 둘이 된다. 생성 인터페이스는 started까지 전달하므로 이번 세 액션은 직접 performed/canceled를 구독하는 편이 단순하다. 이벤트마다 익명 람다를 새로 만드는 방식은 해제 실수 위험이 있어 명명 메서드를 사용한다.

## 3. 이벤트 전환 후에도 물리 요청과 실제 키 해제 관찰 유지

**Decision**: performed는 조건에 맞는 점프·재시작만 bool 요청으로 보관하고 FixedUpdate에서 소비한다. canceled는 누름 상태·이동 값을 정리하되, 초점·비활성화 취소를 실제 키 해제로 간주하지 않는다. onAfterUpdate에서 활성 액션의 개별 controls.IsActuated를 검사하는 기존 중립·해제 확인을 유지한다. 각 버튼의 held/ready 정보는 필요성을 시험으로 확인하며 우선 보존한다.

**Rationale**: 빠른 press/release가 물리 갱신 전에 끝나도 유효 요청은 남아야 한다. canceled에서 pending을 지우면 유실된다. 반대 이동 키는 벡터가 0이어도 눌린 상태다. 기존 코드의 onAfterUpdate는 SendMessages의 Button 취소 누락 보완뿐 아니라 초점 복귀·초기 유지 입력 방지 역할도 가진다.

**Alternatives considered**: 모든 bool 삭제나 canceled만으로 해제 허용은 안전장치를 잃는다. 이벤트에서 Rigidbody를 직접 변경하면 기존 물리 적용 순서가 달라진다. Jump는 공중에서 받은 요청을 예약하지 않는다.

## 4. 단일 동작 Enum과 C# 8.0 패턴 매칭

**Decision**: `PlayerState { Grounded, Airborne, Eliminated }`를 PlayerMovement 안에 두고 하나의 state 필드로 grounded/isEliminated를 대체한다. `IsEliminated`는 state에서 계산한다. 초점·중립 대기·예약·키 해제·진단 정보는 동작 상태와 독립적이므로 남긴다. FixedUpdate는 낙사 우선 판정 후 state와 요청·초점에 대한 tuple switch/when 패턴으로 경로를 구분하고, 이동 감속률 선택에는 switch expression을 사용한다.

**Rationale**: 지상·공중·탈락은 서로 배타적이지만 공중 상태와 초점 상실은 동시에 가능하다. 상승·하강 세분화는 현재 요구에 필요 없다. 기존 HasGroundSupport의 실접촉·현재 기하 검사와 점프 반 스텝 중력 보정은 그대로 둔다.

**Alternatives considered**: 모든 입력 조합까지 Enum에 포함하면 상태가 폭증한다. [Flags] Enum은 불가능한 조합을 허용한다. 상태 클래스/FSM 프레임워크는 세 상태에 과하다.

## 5. 장치 선택과 자산 보존

**Decision**: 현재 범위인 로컬 키보드 플레이를 명시적으로 유지하도록 생성 인스턴스의 bindingMask에 Keyboard&Mouse 그룹을 적용한다. 자산에 이미 존재하는 다른 장치 바인딩은 삭제하지 않는다. 가상 장치 시험은 해당 인스턴스의 devices를 테스트 장치로 제한하여 실제 키보드 간섭을 피한다. 새 InputUser 페어링·자동 스킴 전환 계층은 만들지 않는다.

**Rationale**: PlayerInput 제거 후 mask 없이 활성화하면 자산의 게임패드·XR·조이스틱 바인딩이 동시에 작동할 수 있다. 현재 명세는 키보드 플레이만 보증한다. 기존 키보드의 WASD·방향키를 함께 유지한다.

**Alternatives considered**: 모든 바인딩 무조건 활성화는 입력 범위를 넓힌다. 다중 플레이어·장치 자동 전환은 별도 요구가 있을 때 설계한다.

## 6. 씬 및 테스트를 동시에 이행

**Decision**: SampleScene의 PlayerInput 컴포넌트와 GameObject 컴포넌트 목록의 해당 참조를 함께 제거한다. PlayerMovement와 기존 직렬화 설정·카메라 참조는 유지한다. 다섯 테스트 클래스의 PlayerInput 생성·활성화·스킴 전환·진단 의존을 생성 인스턴스 기반으로 갱신한다. 물리 스텝용 SendMessage는 입력 전달 방식이 아니므로 이번 제거 범위 밖이다.

**Rationale**: 검색 결과 기존 네 fixture와 실제 씬 시험이 PlayerInput을 사용하고, grounded/pendingJump 등을 reflection으로 읽는다. 이전 내부 필드가 사라지면 상태 관찰도 함께 갱신해야 한다. 런타임에 테스트만을 위한 공용 입력 서비스나 setter는 추가하지 않는다.

**Alternatives considered**: 호환용 grounded/isEliminated bool이나 비활성 PlayerInput을 남기는 것은 요청 취지에 맞지 않는다. 기존 입력 누락 시험을 단순 삭제하는 대신 생성 자산 계약 검증으로 이전한다.

## 7. 검증 기준선과 변경된 실패 조건

**Decision**: 구현 직전 기존 전체 시험을 재실행하여 기준선과 불안정 시험을 기록한다. 과거 기록은 66/66 통과이나 이번 실행으로 간주하지 않는다. 기존 Move/Jump/Restart 누락 시 PlayerInput 오류 시험은 원본 자산과 생성 인스턴스의 맵·액션·타입·ID·바인딩 일치 및 재생성 후 컴파일 검증으로 대체한다. null PlayerInput 설정 검사만 제거하고 나머지 물리·probe·수치 검증은 유지한다.

**Rationale**: 생성형 코드에서는 typed accessor가 계약을 고정하므로 문자열 조회 누락 경로는 사라진다. 대신 자산 변경 후 생성 파일이 낡은 결함을 직접 잡아야 한다. 실제 동작 검증은 기존과 동일하게 남긴다. 004의 T015/T017/T018과 초점 민감·카메라 시간 의존 기록은 별도로 추적한다.

**Alternatives considered**: 테스트 개수만 맞추기 위한 죽은 런타임 검증은 불필요하다. 기존 미완료 수동 검수나 첫 복귀 렌더 안내 제거를 자동 통과로 기록해서는 안 된다.
