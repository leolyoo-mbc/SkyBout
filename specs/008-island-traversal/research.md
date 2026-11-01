# Research: 같은 높이의 섬 사이 이동

## 1. 현재 기반과 최소 변경 지점

**Decision**: `SampleScene`에 정적 섬 두 개를 추가하고 기존 입력·이동·접지·카메라 코드를 재사용한다.

**Rationale**: `PlayerMovement.cs`는 섬 이름이 아니라 `GroundSupport`의 실제 Collider 접촉과 발아래 raycast로 접지를 판단한다. 여러 섬을 지원하기 위한 새 상태나 API가 필요하지 않다. 실제 씬의 `Ground`는 중심 (0,-1,0), 크기 (32,2,32), 상단 Y=0, Ground 레이어 인덱스 3, 비트마스크 8이다. Player 중심 (0,1,0), 발 오프셋 (0,-1,0), 캡슐 높이 2m다.

**Alternatives considered**: IslandManager/동적 생성은 고정 지형 3개에 불필요하다. 별도 시험 씬만 추가하면 사용자가 플레이하는 기존 씬의 기능 확장을 입증하지 못한다. 공통 prefab 도입은 이번 두 지형의 단순 복제보다 변경 범위가 크다.

**Evidence**: `Assets/Scenes/SampleScene.unity`, `Assets/Scripts/PlayerMovement.cs`, `Assets/Scripts/GroundSupport.cs`.

## 2. 점프 거리와 두 종류의 구간

**Decision**: 짧은 간격 4m, 긴 간격 14m를 초기 고정 배치로 사용한다. 운동 수치는 변경하지 않는다.

**Rationale**: 실제 최고 속도 8m/s, 점프 높이 2m, 중력 9.81m/s², fixedDeltaTime 0.02s다. `CalculateJumpSpeed`는 sqrt(2gh)+gΔt/2로 약 6.362m/s를 준다. 같은 높이까지 약 1.28초, 전속력 수평 거리 약 10.2m로 예측된다. 가속 10m/s²에서 전속력까지 약 0.8초·3.2m가 필요하므로 출발선 앞에 4.2m 도움닫기를 둔다. 4m는 여유 있는 점프 구간이며 14m는 캡슐 반경과 이산 물리 오차를 고려해도 도달 거리와 충분히 떨어진다. 감속 50m/s²에서 정지 거리 약 0.64m이므로 목적지 중심 부근에서 제동하면 12m 폭 안에 머물 공간이 있다.

**Alternatives considered**: 긴 간격을 점프 한계인 10m 부근으로 두면 프레임·접촉 오차에 민감하다. 점프 성능 저하로 긴 구간을 만드는 것은 명세 위반이다. 아주 먼 구간은 추가 검증 가치 없이 시야만 나빠진다.

**Evidence**: `Assets/Scripts/MovementMath.cs`, `Assets/Scripts/PlayerMovement.cs`, `ProjectSettings/DynamicsManager.asset`, `ProjectSettings/TimeManager.asset`. 위 수치는 분석 예측이며 실제 이동 시험은 미실행이다.

## 3. 배치와 회귀 시험 보호

**Decision**: A=`Ground` (0,-1,0), B=`IslandShort` (0,-1,26), C=`IslandLong` (26,-1,26). B/C 크기는 (12,2,12), 모든 상단 Y=0이다. 기존 Ground와 Player/Camera 루트는 그대로 둔다.

**Rationale**: A 북쪽 경계 Z=16과 B 남쪽 경계 Z=20 사이가 4m다. B 동쪽 경계 X=6과 C 서쪽 경계 X=20 사이가 14m다. 기존 `JumpTests`, `FallEliminationTests`, `RestartSceneTests`는 X=16 가장자리와 (17.5,1,0) 근처 동쪽 빈 공간에 의존한다. `GliderSceneTests`도 D로 동쪽 이탈 후 실제 낙사한다. 새 섬의 Z 범위는 [20,32]이므로 그 통로를 가로막지 않는다.

**Alternatives considered**: 기존 Ground를 줄이거나 재배치하면 기존 위치·낙사 시험을 깨뜨린다. 모든 섬을 동쪽 일렬로 놓으면 회귀 낙사가 착지로 바뀔 수 있다. 모두 북쪽 일렬로 놓으면 긴 구간의 남쪽 복귀 시야가 부족하다.

## 4. 카메라 가시성

**Decision**: 기존 pitch=55°, distance=18, FOV=60°, sharpness=10을 유지하고 긴 구간을 X축으로 배치한다. 16:9 화면에서 출발 가장자리 접근·비행·착지의 양방향 가시성을 측정한다.

**Rationale**: `TopDownCameraFollow`는 yaw=0인 원근 카메라다. 남북 가시 범위가 비대칭으로 지상에서 대략 +Z 23m, -Z 9m여서 북쪽 14m 구간은 돌아오는 방향에 취약하다. X축은 좌우 대칭이며 16:9에서 플레이어 부근 지면의 가로 반폭은 약 18m다. 출발선에서 긴 구간 목적지 가까운 가장자리까지 14.8m이므로 배치로 해결할 여지가 있다. 추적 지연·점프·실제 렌더 영향은 실제 viewport 시험으로 확인한다. 이는 아직 시야 통과 결과가 아니다.

**Alternatives considered**: 자동 줌·섬 중심 추적은 기존 추적 규칙을 변경한다. Camera 컴포넌트의 FOV만 수정하면 follow가 LateUpdate마다 덮어쓴다. 처음부터 거리 확대는 불필요한 사용자 경험 변경이므로 채택하지 않는다.

## 5. 최신 글라이더 조작과 착지

**Decision**: 지상 새 Space로 점프하고 해제 후 공중에서 새 Space 유지로 비행한다. 목적지 중심 부근에서 이동 입력을 놓고 감속한 뒤 Space를 해제해 착지한다.

**Rationale**: 기존 목표는 발 월드 Y=2m, 섬 상단은 Y=0이다. 섬 위에서 Space를 유지하면 떠 있는 것이 정상이며 자동 착지를 기대하면 잘못된 검수가 된다. 수렴 계수 3, 수직 보정 가속도 10m/s², 키 해제 중력 복원과 실제 지지 착지를 그대로 사용한다.

**Alternatives considered**: 새 섬 위 자동 접기·목표를 지면으로 변경하기는 선행 사용자 교정 및 FR-006~007과 충돌한다. 점프 누름 유지로 자동 전개하는 조작도 채택하지 않는다.

## 6. 테스트와 결과 신뢰성

**Decision**: 기존 `SkyBout.PlayModeTests`의 씬 로드·가상 Keyboard 패턴을 재사용한다. 이동 중 상태를 강제로 바꾸거나 힘/속도를 주입하지 않는다. 독립 시험 초기 배치에는 실제 접촉 안정화 후 명시한 도움닫기를 사용한다. 연속 이동은 최초 시작점부터 입력만 사용한다.

**Rationale**: 실제 DynamicUpdate 입력 → FixedUpdate 물리 → 충돌/다음 접지 판정 순서를 거쳐야 한다. 목적지 Collider에 발아래 raycast가 맞고 Grounded 상태·낮은 수직 속도가 여러 물리 틱 유지되는지 확인해야 출발 섬 재착지를 성공으로 오인하지 않는다. 씬 소유권·입력 장치·초점 설정을 finally/teardown으로 복구한다. 기존 시험은 시작 상태를 스스로 복원한다고 가정하지 말고 새 시험도 매 사례 초기 상태를 격리한다.

**Alternatives considered**: 순수 수학 예측만으로 실제 씬 통과를 선언하거나 IsGliding만으로 렌더 상태를 증명하는 방식은 부적합하다. 프로덕션 public API 확장은 필요하지 않다.

**Evidence**: `Assets/Tests/PlayMode/GliderSceneTests.cs`, `RestartSceneTests.cs`, `MovementAndCameraTests.cs`. `unity test --help`에서 PlayMode/filter/output 옵션을 확인했다. Editor가 이미 프로젝트를 열었으면 GUI Test Runner를 사용해 이중 프로세스 충돌을 피한다. CLI는 설치된 최신 배포판을 사용하고 Editor는 프로젝트 지정 버전을 유지한다.

## Research closure

배치·물리·입력·카메라·시험 경계의 미해결 설계 질문은 없다. 실제 물리/시야/사람 검수는 구현 후 수행할 검증이지 설계에서 이미 통과한 결과가 아니다. 공식 API 변경이나 새 의존성이 없어 외부 API 조사는 필요하지 않았으며 저장소의 현재 소스·설정과 CLI 도움말을 1차 근거로 사용했다.
