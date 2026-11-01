# Research: 글라이딩 목표 비행 높이 보정

> **현재 우선 결정 — 사용자 교정**: 목표는2m, 공중 Space 유지 전개/해제 접기/새 누름 재전개다. 아래 최초 연구의 목표0·해제 무시·입력 경계 전개 보존은 당시 가정의 이력이며 현재 계약이 아니다. 최신 근거는 맨 아래 사용자 교정 검증과 `contracts/glide-height.md`를 따른다.

## 범위와 근거

2026-11-01, 현재 프로젝트 코드·씬·설정을 읽고 수직 제어 수치 조사와 생명주기 통합 조사를 병렬 연구했다. 원본 소스·씬은 수정하지 않았다. 미확정 조사 항목은 제어 방식, 기본 목표와 설정, 비활성 구간의 의미, 기존 시험 교체 범위였으며 아래 결정으로 해소했다.

주요 근거:

- `Assets/Scripts/PlayerMovement.cs`: `FootPosition`, `FixedUpdate`, `ApplyGlideVelocity`, `SetState`, `OnDisable`, `RestartAtStartingPosition`, `ValidateSettings`.
- `Assets/Scripts/GroundSupport.cs`: 지지 접촉·법선·분리 거리·발 위치·하강 속도와 짧은 하향 광선의 조합.
- `Assets/Scripts/MovementMath.cs`: 기존 순수 속도 계산 및 Y속도 보존 패턴.
- `Assets/Scenes/SampleScene.unity`: 섬 중심 Y=-1, 크기 Y=2로 표면 Y=0; Player 중심 Y=1, Feet 로컬 Y=-1로 시작 발 Y=0; 낙사 발 높이 -10; 기존 하강 속도 2.
- `ProjectSettings/TimeManager.asset`: fixedDeltaTime=0.02. `ProjectSettings/ProjectSettings.asset`: runInBackground=0.
- `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`: Unity 6000.3.26f1, Input System 1.20.1, URP 17.3.0, Test Framework 1.6.0.

## 1. 수직 제어

**Decision**: 가속도 제한이 있는 임계 감쇠 PD 제어를 기존 `MovementMath`의 순수 함수로 계산한다. 목표 오차를 `e = targetFootY - currentFootY`, 현재 실제 수직 속도를 `v`, 응답 계수를 `w`라 하면 `a = clamp(w²e - 2wv, -aMax, aMax)`, 다음 속도는 `v + a*dt`다. 기본값은 `w=3s⁻¹`, `aMax=10m/s²`다. 새 `CalculateGlideVerticalVelocity` 함수는 위치를 변경하거나 내부 상태를 저장하지 않는다.

**Rationale**: 낮으면 상승·높으면 하강하고 접근할수록 감쇠한다. 전개 순간 Y속도를 초기화하지 않아 첫 틱 변화가 최대 0.2m/s다. 5틱 동안 속도 변화는 이론상 최대 1m/s이며 숨겨진 누적 속도가 없어 충돌 후 실제 Rigidbody 속도로 바로 재계산할 수 있다. 위치 스냅이나 실제 속도의 즉시 상한 처리를 하지 않는다.

**Alternatives considered**:

- 목표 속도 `2e`로 `MoveTowards` 가속도 제한: 수치상 통과하지만 포화 가속과 속도 추종 간 전환이 더 급하다.
- 일정 속도·목표에서 즉시 정지: 전개/도달 연속성 위반.
- 위치 `SmoothDamp`와 직접 위치 대입: 충돌 물리를 우회하고 위치 보정으로 구현하게 된다. 속도에 적용하면 별도의 미분 상태가 추가되어 불필요하다.
- AddForce 기반 자연 비행: 일반적인 물리에는 적합하지만 기존의 작성된 캐릭터 속도 제어와 혼합되며 중력·질량·다른 힘과의 계약 변경이 커진다.
- 실제 Y속도 상한 즉시 clamp: 빠른 전개 속도를 한 번에 자를 수 있어 채택하지 않는다. 별도 수직 최고 속도 설정도 이번 범위에는 도입하지 않는다.

### 수학 시뮬레이션 결과 — Unity 검증 아님

연구 에이전트가 JavaScript에서 `v[n+1]=controller(y[n],v[n],0.02)`, `y[n+1]=y[n]+v[n+1]*0.02`로 15초간 비충돌 이산 적분했다. 목표 대비 오프셋 0/±1/±3m의 정지 5조건과 ±1/±3m에서 초기 속도 ±5m/s의 8조건, 총 13개 고유 조건을 확인했다. 명세의 반복 5회 Unity 시험을 실행한 것은 아니다.

| 기본 PD 조건 | ±0.1m에 진입 후 계속 머무는 시간 |
| --- | --- |
| 목표와 동일, v=0 | 0초 |
| ±1m, v=0 | 1.30초 |
| ±3m, v=0 | 1.88초 |
| ±1m, 목표에서 멀어지는 5m/s | 2.20초 |
| ±3m, 목표에서 멀어지는 5m/s | 2.60초 |
| ±1m, 목표로 향하는 5m/s | 1.28초 |
| ±3m, 목표로 향하는 5m/s | 1.58초 |

최악 수렴 시간 2.60초, 진입 후 10초 높이 폭 0.09968m 이하, 반대편 0.1m 초과 진입 최대 1회였다. 5~15초 높이 폭은 최대 약 0.0003073m였다. 목표 Y=10에서 float32로 반올림한 별도 계산도 최악 2.60초였으며 5틱 속도 차이 최대 1.00000024는 부동소수점 오차였다. 구현 시험은 작은 수치 허용 오차를 별도로 명시한다.

초기 속도 5m/s의 최소 정지 거리는 가속도 10m/s²에서 약 1.25m이므로 가까운 목표에서 한 번 지나치는 것을 완전히 금지하지 않는다. 이것이 명세의 최대 1회 통과 허용과 일치한다. 이 계산은 실제 Unity 적분·충돌·수면·발 Transform·표시·조작감 검증을 대신하지 않는다. 계산 출력은 본 문서 요약이며 Unity 원본 시험 결과 파일은 아직 없다.

## 2. 목표 높이와 착지

**Decision**: 기본 목표 발 월드 Y는 `0m`로 한다. 씬과 기존 시험 fixture 모두 섬/바닥 표면이 Y=0이며 Player 중심은 발보다 1m 높다. 목표는 낙사 경계 -10m보다 높다. 전개 위치·지형·최초 시작 자세로 목표를 다시 계산하지 않는다.

**Rationale**: 섬 표면 위 공통 비행 높이를 제공하면서 기존 점프 후 하강 착지 경로와 정합된다. 목표보다 위의 별도 지지 표면으로 하강하는 시험은 착지 우선순위를 독립 검증한다.

**Alternatives considered**: 중심 Y를 목표로 사용하면 1m 오차가 생긴다. 전개 높이를 목표로 사용하면 낮으면 상승·높으면 하강의 공통 기준을 잃는다. 섬 위의 더 높은 목표는 기존 착지 대신 공중 체공을 유발한다. 표면 아래 목표는 착지를 쉽게 하지만 플레이어가 기본적으로 섬 아래를 비행하는 의미 변경이므로 이번 기본값으로 채택하지 않는다.

**Landing risk and verification gate**: 목표 ±0.1m 도달은 착지가 아니다. `GroundSupport`는 실제 접촉 법선 ≥0.7, 분리 거리/발 높이 차이 ≤0.03m 및 하향 광선을 요구한다. 임계 감쇠 접근 중 실제 물리 접촉이 생성되는지 기존 SampleScene과 fixture에서 반드시 검증한다. 속도가 작아졌다는 이유로 시험을 종료하지 말고 실제 지상 상태·날개 닫힘·새 점프를 기다린다. 실패하면 구현을 완료 처리하지 않으며 근접만으로 접지시키거나 목표를 몰래 표면 아래로 바꾸지 않는다. 기본 목표는 확정 설계이며 실제 접촉은 구현 검증 게이트다.

## 3. 기존 컨트롤러 통합과 충돌

**Decision**: 기존 상태·입력 흐름을 그대로 사용하고 `ApplyGlideVelocity`에서 Y 계산만 교체한다. 낙사 검사→탈락/재시작→접지→점프→전개→수직 보정→수평 조작 순서를 유지한다. `SetState`가 중력과 표시의 유일한 소유자로 남는다.

**Rationale**: 보정을 `CanMove` 검사 앞에 두어 무입력·초점 상실 중에도 활성 물리에서는 계속 보정된다. 동적 Rigidbody 충돌을 그대로 사용하고 충돌 해결 후 실제 속도를 다음 틱 입력으로 받는다. 목표 위치로 Transform/Rigidbody를 옮기지 않으며 장애물 관통을 유발하는 보조 경로를 만들지 않는다.

**Alternatives considered**: 별도 글라이더 컨트롤러는 속도·중력·상태 소유권을 중복한다. 틱별 목표 위치 이동은 충돌·착지 순서를 우회한다. 목표 근접을 지상으로 처리하면 허공/옆면 허위 착지가 생긴다.

현재 씬은 discrete collision detection이다. 기존 속도·지형에서 관통이 없는지 검증하고 전역 Physics 설정이나 collision detection mode를 근거 없이 변경하지 않는다. 충돌이 막고 있는 동안 수렴 시간은 적용하지 않으며 장애물을 제거한 후 실제 위치·속도로 보정을 재개한다.

## 4. 생명주기

**Decision**:

| 경계 | 비활성/초점 상실 중 | 복귀 |
| --- | --- | --- |
| 초점 상실 | 입력 요청·수평 조작 정리. 시뮬레이션이 진행되는 동안 활성 컨트롤러가 보정 계속 | 같은 목표, 기존 키 해제 후 새 입력 |
| 컴포넌트 비활성 | 전개·목표·중력 꺼짐 유지. FixedUpdate가 없으므로 보정은 중지하나 동적 몸체는 마지막 Y속도로 충돌 물리 진행 | 낙사·지지 먼저 재평가 후 실제 속도에서 보정 재개 |
| 전체 GameObject 비활성 | 논리 전개·목표 유지, 몸체/Collider 비활성으로 물리 진행 없음 | 속도 유지 자체는 가정하지 않고 실제 값에서 재개 |

**Rationale**: 명세의 ‘수직 이동 계속’과 ‘복귀 후 수렴’을 단일 소유자 구조에서 일관되게 해석한다. 컴포넌트 중지 중에는 능동 수렴을 보장하지 않는다. 상승이면 계속 상승, 하강이면 계속 하강, 정지면 머무를 수 있다. `runInBackground=0`이므로 초점 상실의 실제 벽시계 시간 동안 물리가 항상 진행한다고 보장하지 않는다.

컴포넌트 비활성 중 충돌 콜백이 오더라도 기존 가드는 접촉 수집을 건너뛴다. 활성화 시 `WakeUp()`과 접촉 재수집을 유지하고 필요한 물리 단계를 거친다. 경계 아래로 이동한 경우 첫 복귀 틱의 낙사 판정이 보정보다 우선한다. 일반 비활성화에서 중력을 켜거나 별도 백그라운드 제어기를 만들지 않는다. 파괴 시에는 기존처럼 중력을 복원하고 표시를 닫는다.

**Alternatives considered**: 비활성 시 강제 정지·중력 복원은 기존 수직 움직임 정책을 바꾼다. 두 번째 제어기로 능동 수렴을 유지하면 비활성 의미와 단일 물리 소유권을 깨뜨린다. 비활성 시간의 비행을 복귀 시 재생하면 위치 도약과 탈락 구제가 생길 수 있다.

## 5. 설정과 마이그레이션

**Decision**: `glideDescentSpeed`를 제거하고 `glideTargetFootHeight=0`, `glideHeightResponse=3`, `glideVerticalAcceleration=10`을 명시적으로 직렬화한다. 이전 속도를 목표 높이로 `FormerlySerializedAs` 매핑하지 않는다. 다른 기존 설정·GUID는 유지한다.

검증은 `Awake`와 실행 중 `OnValidate`에서 기존 방식대로 한다. 목표/낙사 높이는 유한하고 목표가 낙사 높이보다 커야 한다. 응답 계수와 그 제곱·두 배는 양의 유한값이어야 하며 가속도는 `0 < 값 ≤ 10`의 유한값이어야 한다. 기존 중력·동적 몸체·Y 잠금 해제·제로 damping 검증도 유지한다. 잘못된 설정은 오류 로그 후 컴포넌트 비활성화하며 자동 수정하지 않는다. 비활성화가 몸체를 안전하게 멈춘다는 의미는 아니다.

**Rationale**: 속도·높이·응답·가속도는 다른 단위다. 기본 조합에서만 명세의 수렴 시간을 수학적으로 확인했으므로 Inspector 튜닝은 재검증한다. 응답 파생 계수 검증은 유한 입력의 제곱 overflow도 놓치지 않는다.

**Alternatives considered**: 기존 값 2를 목표 높이로 재사용하면 잘못된 Y=2 목표가 된다. 기존 하강 속도 필드를 사용하지 않은 채 남기면 Inspector 계약이 모호해진다. 새 ScriptableObject나 전역 설정 자산은 단일 Player 범위에 불필요하다.

## 6. 시험 마이그레이션

**Decision**: 기존 입력·전개·표시·수평 조작·접지·탈락·재시작 단언은 보존하고 다음 상수 하강 전제만 교체한다.

| 파일/기존 항목 | 이번 대체 |
| --- | --- |
| `GliderTests`: -2m/s 단언, 무조건 하강 | 가속도 제한과 목표 방향/수렴; 입력/상태 단언은 유지 |
| 같은 6m 구간 자유낙하 대비 시간 | SC-001~SC-003 수렴/유지/속도 연속성 |
| `InvalidDescentSpeedIsRejected` | 목표 유한성/낙사 관계, 응답 파생값, 가속도 범위 검증 |
| Y속도가 -0.1 이상이면 착지한 것으로 간주하는 루프 | 실제 Grounded·표시 제거 후 접촉/중력/새 점프 검사 |
| 비활성 동안 항상 -2m/s 하강 | 초점 보정/컴포넌트 관성 이동/전체 몸체 정지 구분 |
| 비활성 중 고정 시간으로 착지·낙사 가정 | 명시적 시작 하강 속도와 충분한 공간/시간을 설정하고 복귀 판정 검사 |
| `GliderSceneTests`: 걷기 이탈→전개→필연적 낙사 | 실제 이탈·전개→목표 수렴/유지→시험용 경계 아래 이동→탈락·R |

**Rationale**: 목표 높이 유지가 성공하면 기존 낙사 대기 루프는 끝나지 않는다. 시험 목적을 삭제하지 말고 새 수직 계약과 기존 탈락 규칙을 각각 검증한다. 실제 씬 날개 렌더·카메라·동일 인스턴스 재시작도 유지한다.

**Alternatives considered**: 기존 시험 전부 삭제·상수 값만 느슨하게 변경·타임아웃 증가로 통과시키는 접근은 회귀 보장을 잃는다.

## 공식 문서와 적용 한계

- [Unity 6000.3 Rigidbody.linearVelocity](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rigidbody-linearVelocity.html): 월드 공간 속도, 동적 몸체에서의 사용. 일반적으로 직접 속도 대입보다 힘을 권장한다. 여기서는 기존 캐릭터의 작성된 속도 제어에 한정하고 Y변화량을 제한한다.
- [Behaviour.enabled](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Behaviour-enabled.html): 비활성 Behaviour는 일반 갱신을 실행하지 않는다.
- [GameObject.SetActive](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GameObject.SetActive.html): 전체 비활성화는 몸체·Collider·스크립트에도 적용된다.
- [MonoBehaviour.OnCollisionStay](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.OnCollisionStay.html): 비활성 Behaviour에도 충돌 메시지가 갈 수 있지만 수면 몸체에는 Stay가 전달되지 않는다.
- [Mathf.MoveTowards](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mathf.MoveTowards.html), [Mathf.SmoothDamp](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mathf.SmoothDamp.html): 대안 비교에 사용한 공식 API 정의. 실제 프로젝트는 6000.3을 유지한다.

설계 질문은 모두 해소됐다. 실제 접촉·물리 적분·렌더·사람 조작감은 구현 단계의 필수 검증이며 아직 통과했다고 주장하지 않는다.

## Implementation verification — 2026-11-01

- 위 연구 시점 예측과 별도로 실제 구현 후 전체 PlayMode **156/156 통과(333.27초)**, 기준선111/111 통과(268.2초)를 확보했다. 원본은 `TestResults/007-final.json`, `007-baseline.json`이다.
- 목표0/응답3/가속도10 및 설계 계산을 그대로 구현했다. 실제 Rigidbody45회에서도 최악2.60초 수렴, 첫10초 높이 폭 최대0.09967929m였다. `TestResults/007-height-summary.csv`와 틱별 샘플 참조.
- 우려했던 표면Y=0 실제 착지는 기존 접촉 판정을 바꾸지 않아도 성공했다. 실제 씬의 Grounded/날개 닫힘/중력 복원/새 점프를 확인했다. 목표를 하향하거나 근접 접지를 추가하지 않았다.
- 컴포넌트 비활성 시 관성 이동, 전체 GameObject 비활성 시 시뮬레이션 제외 및 재개는 계약대로다. Unity의 비활성 Rigidbody.position이0을 반환하여 시험 관측은 비활성 Transform 위치와 복귀 후 실제 물리 값으로 구분했다. 런타임 우회는 추가하지 않았다.
- 실제 씬10회 반복에 필요한 시간 때문에 testcase 제한만180→300초로 명시했다. 수렴5초·유지10초·복귀2초·안내1초의 합격 기준은 완화하지 않았다.
- 기존 Jump/Fall/Restart 시험에 포함된 전개 직후 -2m/s 전제와 높인 경계 fixture의 목표 설정도 좁게 갱신했다. 변경 내역은 `TestResults/007-test-migration.md` 참조.
- 직접 사람 검수는 **미검증**, T027 미완료다. 상세 FR/SC 대조는 `TestResults/007-verification.md`, 재실행 안내는 [quickstart.md](quickstart.md). 수학·자동 물리·렌더 증거를 사람 검수로 대신하지 않는다.

## 사용자 교정 검증 — Space 유지와 높이2m

- 실제 회귀 `HoldingSpaceGlidesReleaseClosesAndFreshPressRedeploysFiveTimes`에서 해제 후 IsGliding=true 실패를 먼저 확인했다(`TestResults/007-hold-red.json`). 원인은 해제/유지 입력 계약이 빠진 latch 전개였고 날개만의 표시 문제가 아니었다.
- `JumpReleased`와 유효 `IsJumpHeld`로 해제 시 전개 요청/상태를 정리한다. 지상 짧은 탭 점프는 취소하지 않는다. 해제 시 위치/속도 초기화 없이 중력만 복원하고 공중 새 누름으로 재전개한다.
- 초점/비활성 입력 경계에서도 전개를 취소하여 해제 이벤트를 놓친 상태의 유령 체공을 막는다. 복귀 후 기존 키 해제 규칙을 만족하는 새 누름이 필요하다. 이는 이전 전개 보존 결정의 명시적 교체다.
- 사용자가 씬에 설정한 목표2m를 보존했고 코드 기본값도2m로 맞췄다. 현재 섬 표면0에서 점프 높이2와 같지만 다른 점프 설정에 자동 연동하는 새 기능은 추가하지 않았다.
- 최신 전체 시험 **159/159 통과, 실패/건너뜀0, 335.17초**. 실제 씬은 Space 유지 중2m/10초 체공, 해제 후 실제 낙하/착지/새 점프, 자연 낙사/R 재시작10회를 검증했다. 원본 `TestResults/007-hold-final.json`, 세부 `007-hold-verification.md`.
- 독립 원점 fixture는 목표0을 명시해 수학/장애물 시험 조건을 유지하며 실제 게임 기본값과 구분한다. 사람 직접 조작감 검수는 여전히 미검증이다.
