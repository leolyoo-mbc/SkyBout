# Research: 기존 Unity 프로젝트의 추락·낙사 확장

## 조사 범위와 방법

현재 프로젝트의 런타임 코드, 모든 해당 호출부·PlayMode 테스트, SampleScene 직렬화, 패키지·물리·시간 설정을 읽어 결정했다. 병렬 하위 에이전트 실행 도구는 제공되지 않아 직접 조사했다. Editor 실행이나 신규 기능 시험은 하지 않았으며 이전 단계 문서의 통과 기록을 이번 단계의 실행 결과로 간주하지 않는다.

## 1. 기존 프로젝트와 물리 재사용

- **Decision**: Unity 6000.3.26f1, C#, 기존 `SkyBout.Runtime`/`SkyBout.PlayModeTests` 어셈블리와 `Assets/Scenes/SampleScene.unity`를 확장한다. 새 프로젝트·패키지는 만들지 않는다.
- **Rationale**: `PlayerMovement.FixedUpdate`는 수평 속도만 조절해 Y 중력을 보존한다. `HasGroundSupport`는 실제 접촉, 발 위치 재검사, 표면 법선을 결합하며 옆면·트리거·허공을 배제한다. 이미 추락·공중 조작의 기반이 있으므로 새로운 중력이나 접지 판정을 만들 필요가 없다.
- **Alternatives considered**: CharacterController 전환, 추락 전용 컨트롤러, 바닥 킬 트리거는 기존 점프 동작 변경 또는 불필요한 씬 의존성을 추가하므로 제외.
- **Evidence**: `Assets/Scripts/PlayerMovement.cs`, `Assets/Tests/PlayMode/JumpTests.cs`, `Assets/Tests/PlayMode/MovementAndCameraTests.cs`.

## 2. 낙사 높이와 판정 시점

- **Decision**: `PlayerMovement`에 직렬화된 유한 실수 `fallDeathHeight`를 추가한다. 기본값은 월드 Y -10m. 기존 `FootPosition.y`와 엄격한 `<` 비교를 `FixedUpdate`의 이동·점프·접지 갱신보다 앞에서 수행한다. 동일 높이는 생존이다.
- **Rationale**: 발 위치는 Rigidbody 위치와 자식 GroundCheckOffset의 상대 오프셋을 사용하므로 기존 물리/보간 규칙과 일치한다. SampleScene의 표면 Y=0, 플레이어 중심 Y=1, 발 오프셋 Y=-1이므로 -10은 표면 아래이며 테스트·튜닝하기 충분하다. 별도 트리거가 없어 고속으로 경계를 지나쳐도 다음 물리 판정에서 감지된다.
- **Alternatives considered**: 플레이어 중심·렌더러 경계 기준은 명세의 발 위치와 불일치. Update/충돌 콜백만 사용하는 판정은 물리 틱이나 지면 유무에 종속된다. 부동소수점 오차 허용 범위는 정확히 같은 높이와 아래를 구분하는 계약을 바꾸므로 사용하지 않는다.
- **Validation**: NaN 및 양/음의 Infinity는 기존 설정 진단 흐름으로 거부한다. 음수만 허용하지는 않는다(양수 표면을 사용하는 씬도 가능). SampleScene 통합 검증에서 설정 높이가 시작 표면보다 낮음을 확인한다. 경계 단위 시험은 중력 적분 전에 직접 물리 로직을 한 번 호출해 정확한 동일 높이 조건을 검사하고, 실제 하강 시험은 별도로 수행한다.

## 3. 상태 소유권과 입력 차단

- **Decision**: 생존 상태는 기존 `PlayerMovement`가 `isEliminated` 불리언으로 소유한다. 외부 관찰용 읽기 전용 `IsEliminated`를 제공한다. 진입 시 기존 입력·보류 점프·지지 캐시를 비우고 수평 속도를 0으로 만든다. 이후 물리 틱과 Move/Jump 입력 진입점을 탈락 상태로 차단한다. OnEnable/초점 복귀에서 생존으로 초기화하지 않는다.
- **Rationale**: 입력 처리와 발 위치가 같은 컴포넌트에 있어 실행 순서 의존성과 새 연결을 피한다. SendMessages를 사용하는 현재 PlayerInput에서는 컴포넌트 비활성화 하나만 입력 차단으로 간주하지 않는다. 상태 래치를 유지하면 높이 변경·이동·초점 복귀·재활성화만으로 부활하지 않는다. 새 씬 인스턴스 생성만 초기 생존 상태를 만든다.
- **Alternatives considered**: 별도 PlayerLife/이벤트 버스/상태 머신과 컴포넌트 간 실행 순서 설정은 두 상태에 과하다. GameObject 파괴는 안내·카메라 참조를 깨뜨리므로 제외. PlayerInput 전체 비활성화는 향후 UI까지 영향을 줄 수 있어 요구하지 않는다.
- **Limit**: 탈락 후 물리적 수직 하강과 기존 카메라 추적은 유지한다. 장시간 방치하는 관전 세션이나 추락체 정리는 범위 밖이며 재시작/관전 설계 시 다시 검토한다.

## 4. 탈락 안내

- **Decision**: 기존 UI가 없는 단일 플레이어 프로토타입이므로 같은 컴포넌트의 `OnGUI`에서 화면 중앙 고대비 패널과 `ELIMINATED` 문구를 표시한다. 버튼·재시작·새 Canvas·폰트 자산은 추가하지 않는다.
- **Rationale**: 내장 IMGUI 모듈이 이미 설치되어 있으며 텍스트 안내 하나에 추가 씬 연결이 필요 없다. 화면 좌표이므로 플레이어 위치·카메라 가시성과 독립적이다. 영문 문구는 별도 한글 폰트 의존성을 만들지 않는다. 화면 크기에 맞춘 중앙 배치와 충분한 글자 크기·대비를 검증한다.
- **Alternatives considered**: uGUI/TMP는 설치 여부와 별개로 Canvas·텍스트·참조·폰트 구성이 늘어난다. 월드 공간 문구는 플레이어가 화면 밖일 때의 안내를 보장하지 못한다. Console 로그만으로는 플레이 화면 안내가 아니다.
- **Limit**: 로컬 1인 프로토타입용 안내다. 실제 HUD나 다국어가 필요해지면 해당 UI 체계로 옮긴다. 구현 시 이 제한은 `shortcut:` 주석으로 남긴다.

## 5. 검증과 기존 호출부 영향

- **Decision**: `FallEliminationTests.cs`를 기존 PlayMode 어셈블리에 추가하고, 기존 두 테스트군도 함께 실행한다. 수동 Physics.Simulate 기반 경계 시험과 실제 씬·자연스러운 플레이 루프 통합 시험을 분리한다.
- **Rationale**: 기존 테스트는 Reflection으로 필드에 접근하고 FixedUpdate를 SendMessage로 호출한다. 기존 필드명과 입력 액션을 유지하면 영향이 최소화된다. 기존 JumpTests의 실제 씬 시험은 추락 후 옆면 위치로 순간 이동하므로, 탈락 래치를 추가한 뒤 이미 탈락한 인스턴스를 생존으로 간주하지 않도록 추락 깊이 제한 또는 새 씬 인스턴스로 격리해야 한다.
- **Alternatives considered**: 이번 기능만 단독 실행하면 이동·점프 회귀를 놓친다. 수동 틱 시험만으로는 OnGUI 표시와 자연스러운 수명주기를 입증하지 못한다. 공용 테스트 프레임워크 재구성은 요구하지 않는다.
- **Evidence**: 패키지 Input System 1.20.1, Test Framework 1.6.0, URP 17.3.0. 물리 중력 (0,-9.81,0), fixedDeltaTime 0.02s. Unity Editor 실행 파일이 현재 Windows 기본 설치 경로에 존재한다.

## 해결 상태

미해결 기술 질문 없음. 상태·화면 계약은 [contracts/player-fall-elimination.md](contracts/player-fall-elimination.md), 검증 방법은 [quickstart.md](quickstart.md)에 정리했다. 계획 단계에서는 패키지 설치, 소스·씬 변경 및 성능 시험을 실행하지 않는다.
