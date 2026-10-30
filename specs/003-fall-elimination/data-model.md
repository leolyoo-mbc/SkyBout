# Data Model: 추락·낙사 탈락

영구 저장이나 네트워크 모델은 없다. 기존 컴포넌트의 런타임 상태와 Inspector 설정만 확장한다.

## PlayerMovement — 플레이어 생존 상태

| 항목 | 형태 | 소유권·규칙 |
| --- | --- | --- |
| `isEliminated` | 비직렬화 bool, 기본 false | 인스턴스가 생성될 때 생존. 한 번 true가 되면 같은 인스턴스에서 false로 바꾸지 않는다. |
| `IsEliminated` | public 읽기 전용 bool | UI·검증을 위한 관찰용. 상태 변경이나 리셋 API가 아니다. |
| `FootPosition` | 기존 계산 Vector3 | `body.position + (groundCheckTransform.position - transform.position)`. 낙사에는 Y만 사용한다. |
| `grounded` | 기존 bool | 생존 중 실제 지지 여부. 탈락 진입 시 false. |
| `input`, `pendingJump`, 점프 입력 플래그 | 기존 상태 | 탈락 진입 시 초기화. 탈락 중 입력은 동작 요청을 만들지 않는다. |
| `supportingColliders` | 기존 접촉 집합 | 탈락 진입 시 비우고 탈락 후 새 지지 상태를 부여하지 않는다. |

### 상태 전이

1. 새 플레이어 인스턴스 → 생존. 기존 지상·상승·하강 처리는 변경하지 않는다.
2. 생존이며 `FootPosition.y >= fallDeathHeight` → 생존 유지.
3. 생존이며 `FootPosition.y < fallDeathHeight` → 탈락 1회. 이 검사를 물리 틱의 점프·이동 처리보다 먼저 수행한다.
4. 탈락 → 탈락 유지. 수평 속도 0, 플레이 조작 차단, 화면 안내 유지. 수직 중력은 계속 적용될 수 있다.
5. 초점 복귀·키 해제/재누름·높이 설정 변경·컴포넌트 재활성화·발 위치 변경은 부활 조건이 아니다. 실행 종료 후 새 씬 인스턴스에서만 초기 상태를 갖는다.

탈락 진입 처리에서 기존 `ResetJumpInput`·`StopHorizontalMovement`를 재사용한다. 접촉 콜백도 탈락 상태에 새 지지를 추가하지 않도록 한다. 단순 `enabled=false`를 탈락 상태로 사용하지 않는다.

## 낙사 경계 설정

| 항목 | 형태 | 규칙 |
| --- | --- | --- |
| `fallDeathHeight` | SerializeField float | 월드 Y 기준. 기본 -10m. 유한 실수만 허용하며 음수 전용 제한은 두지 않는다. |

- NaN 및 양/음 Infinity는 기존 설정 검증 경로에서 명확한 오류로 처리한다.
- SampleScene의 초기값은 시작 표면 Y=0보다 낮아야 한다. 씬 통합 시험에서 검증한다.
- 경계에 epsilon을 적용하지 않는다. 동일 높이 생존과 엄격한 아래 높이 탈락을 각각 검증한다.
- 물리 적분으로 한 틱에 경계를 건너면 다음 FixedUpdate의 첫 아래 높이 관찰에서 탈락한다. 연속 충돌 트리거는 필요 없다.

## 지지 표면 — 기존 Ground

기존 Ground 레이어·Collider·법선·발 접촉 검사로 구성한다. 새 데이터·태그·경계 Collider는 추가하지 않는다. SampleScene의 Ground는 중심 (0,-1,0), 크기 (32,2,32), 표면 Y=0이다. Player 중심 (0,1,0), GroundCheckOffset 로컬 Y=-1이 기존 관계다.

## 화면 표시 — 파생 상태

별도 생존 상태를 저장하지 않는다. OnGUI가 `IsEliminated`를 보고 화면 중앙 안내를 그린다. 문구는 `ELIMINATED`, 화면 좌표·고대비 배경·읽기 쉬운 글자 크기를 사용한다. 화면 안내는 렌더러, 지면, 카메라 목표의 가시성과 무관하다. 버튼·재시작 상태·세계 공간 표시는 없다.
