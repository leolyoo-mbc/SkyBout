# Quickstart: 기존 Unity 프로젝트의 글라이더 검증

## 상태와 사전 조건

2026-11-01: **현재 프로젝트에 구현 및 자동 검증 완료, 사람의 직접 검수 대기**. 변경 전 전체 PlayMode **84/84 통과(115.05초)**, 변경 후 **111/111 통과(267.93초)**. 실패·건너뜀 0건이다. 기존 84개 시험을 보존하고 새 시험 27개를 추가했으며 공중 Space의 이전 무동작 기대만 전개 규칙으로 갱신했다. 과거 005 결과를 이번 증거로 재사용하지 않았다.

- 구현: 기존 `PlayerMovement.cs`의 Gliding 상태·Space 의도 분기·중력 해제/복원·일정 하강 및 충돌 시 즉시 접기. 기존 입력 자산·생성 코드·수평 계산·접지 판정·카메라·패키지는 그대로다.
- 실제 `SampleScene` Player에 충돌 없는 주황색 날개 자식과 `Assets/Materials/Glider.mat`을 추가했다. 초기 하강 속도는 2 m/s다. 목표 비행 높이 보정은 포함하지 않는다.
- 신규 US1 시험의 예상 실패와 착지 첫 프레임 결함을 실제로 확인한 뒤 구현·수정했다. 전체 GameObject 비활성화 시 Unity가 속도를 초기화할 수 있어 비활성 동안은 논리 전개 보존, 재활성화 후에는 일정 하강 복원을 검증한다. 컴포넌트만 비활성화하거나 초점만 잃으면 물리 하강은 계속된다.
- 실제 카메라 렌더의 전개 표시·첫 착지/복귀 화면과 자연 걷기 이탈→전개→낙사→R 재시작 20회가 통과했다. 동일 6 m 하강 구간 5쌍은 자유낙하 1.10초, 글라이딩 3.02초였다.
- 원본 결과: `TestResults/006-baseline.json`, `006-red.json`, `006-us1-checkpoint.json`, `006-us2-red.json`, `006-us2.json`, `006-us2-jump.json`, `006-us2-movement.json`, `006-us3.json`, `006-final.json`. `.xml`·`.log`는 JSON에서 생성한 보조 보고서이며 네이티브 Test Runner XML 내보내기가 아니다.
- 상세 요구사항 대조: `TestResults/006-verification.md`; 반복 시간: `006-repeat-timings.txt`; 하강 비교: `006-descent-pairs.txt`; 화면: `006-deployed.png`, `006-landed.png`, `006-eliminated-{0,19}.png`, `006-restored-{0,19}.png`.
- **T030 미완료**: 사람이 직접 키보드 조작감·작은 Game 뷰 한글 가독성을 확인하지 않았다. `TestResults/006-manual.md`에 미검증으로 기록했고 자동 렌더 결과로 대체하지 않았다. 아래 절차는 재검증 및 사람 검수 안내다.

- 프로젝트: `E:\projects\SkyBout`, Unity 6000.3.26f1. 기존 패키지 복원 후 `Assets/Scenes/SampleScene.unity`를 연다.
- 새 프로젝트·씬을 만들지 않는다. 기존 Player/섬/Main Camera를 사용한다.
- 구현 후 PlayerMovement의 `glideDescentSpeed` 기본값 2, Player 자식 `gliderVisual` 참조 및 URP 재질을 확인한다. 날개에 Collider/Rigidbody는 없어야 한다.
- Console 컴파일 오류·Missing Script·누락 필수 참조가 없어야 한다.
- 동작·설정 계약: [contracts/player-glider.md](contracts/player-glider.md), 순서: [data-model.md](data-model.md).

## 1. 구현 전 기준선

1. 작업 트리의 사용자 변경과 실제 씬 이동·점프·낙사·카메라 설정, 입력 자산 ID를 기록한다. 임의 초기화하지 않는다.
2. `Window > General > Test Runner > PlayMode`에서 현재 전체 시험을 실행하고 결과를 내보낸다. 입력·수학 테스트를 포함한 실제 수집 목록과 실패·건너뜀을 기록한다.
3. 공중 Space 무동작을 단언하는 시험은 변경 대상 목록을 작성한다. 같은 시험의 추가 공중 점프 금지·키 유지·재시작 기준은 유지한다. 새 전개 기대와의 차이는 FR 번호로 설명한다.

정적 검사:

```powershell
git -C 'E:\projects\SkyBout' diff --check
git -C 'E:\projects\SkyBout' status --short
```

## 2. 구현 후 자동 회귀

Editor Test Runner에서 기존 전체 PlayMode와 신규 `GliderTests`, `GliderSceneTests`를 실행한다. fixture는 가상 장치만 자기 actions.devices에 할당하고 InputSystem 업데이트와 Physics.Simulate 순서를 명시하며 장치·물리·초점 설정을 teardown에서 복원한다.

| 대상 | 검증 | 횟수/판정 |
| --- | --- | --- |
| SC-001 | 상승·정점·하강·걷기 이탈 전개 | 각 5회, 입력 후 0.2초 이내 관측 |
| SC-001 | 점프 키 유지, 전개 후 재누름 | 각 5회, 자동 전개/추가 점프/접기 0 |
| SC-002 | 동일 높이 구간 자유낙하와 글라이딩 비교 | 5쌍 모두 글라이딩 시간이 김 |
| SC-003 | 8방향·전환·해제 | 각 5회, 기존 수평 기준 유지 |
| SC-004 | 섬 위 전개→착지→새 점프, 옆면 | 10회 정상; 옆면 5회 허위 착지 0 |
| SC-005 | 경계 바로 위·동일·아래 및 아래 진입+입력 | 각 5회, 경계 아래만 탈락, 안내 1초 이내 |
| SC-006 | 전개→낙사→R 복귀 | 같은 실행 20회, 복귀 2초 이내, 중복/잔류 0 |
| SC-007 | 짧은 입력·착지 경계·키 유지·초점·비활성화 | 명세별 각 5회, 요청 누락/재실행 0 |
| SC-008 | 적용 가능한 기존 전체 인수 기준 | 전개 확장 외 기존 단언 보존 |

### 일정 하강의 통제 시험

비충돌 공중 구간에 동일 시작 발 높이 2 m, 초기 Y속도 0, 동일 하단 발 높이 -4 m를 사용한다. 기존 낙사 경계 -10 m보다 위이므로 비교 중 탈락하지 않는다. 한 번은 무전개, 다른 한 번은 시작 시 전개하고 두 시험 모두 XZ 입력은 없다. 0.02초 물리 간격에서 5쌍의 구간 통과 시간을 기록한다. 2 m/s 글라이딩은 약 3초, 자유낙하는 약 1.1초가 예상된다. 수치 예측을 실행 결과로 대신하지 않는다.

이동 중 물리 스텝 후 Y속도가 -2 m/s의 ±0.05 m/s 안에 있는지, 위치 변화가 하강인지 확인한다. 전개 직전/직후 같은 틱의 위치는 변하지 않아야 한다(부동소수점 허용 오차 0.001 m). 일반 추락을 하강 시작 직후의 순간 속도와 비교하지 말고 전체 동일 높이 구간으로 비교한다.

### 선택적 비시각 배치 실행

열린 Editor와 동일 프로젝트를 동시에 배치 실행하지 않는다. Editor를 종료한 뒤 다음 예시를 사용할 수 있다. 버전 경로가 실제 설치와 맞는지 먼저 확인한다.

```powershell
$root = 'E:\projects\SkyBout'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe'
New-Item -ItemType Directory -Force -Path "$root\TestResults" | Out-Null
& $unity -batchmode -nographics -projectPath $root `
  -runTests -testPlatform PlayMode -testFilter GliderTests `
  -testResults "$root\TestResults\006-glider.xml" `
  -logFile "$root\TestResults\006-glider.log"
```

XML의 실제 수집·실행·통과 수를 확인한다. 종료 코드만으로 통과 처리하지 않는다. 위 필터는 전체 회귀 및 렌더 검증을 대체하지 않는다. 새 테스트 클래스가 아직 구현되지 않았다면 0개 수집은 성공이 아니다.

## 3. 기존 SampleScene의 실제 플레이 검수

1. Game 뷰에 초점을 두고 이동·지상 Space 점프를 수행한다. Space 유지로 전개되지 않는지 본다.
2. 점프 키를 놓고 다시 Space를 눌러 날개가 0.2초 이내 보이는지, 추가 상승 없이 완만히 내려가는지 확인한다. 상승·정점·하강에서 반복한다.
3. 전개 중 WASD/대각선 이동·방향 전환·해제를 확인한다. 날개가 플레이어 위치를 가리거나 탑뷰에서 보이지 않는 문제가 없어야 한다.
4. 섬 위로 착지하면 첫 착지 표시 프레임에서 날개가 사라지고 새 Space로 점프할 수 있어야 한다.
5. 걸어서 섬 밖으로 나가 Space 전개 후 계속 내려가 탈락하는지 확인한다. 새 R로 복귀한 첫 화면에는 날개·탈락 안내가 남지 않아야 한다.
6. 전개 상태에서 초점 전환과 PlayerMovement 비활성화/재활성화를 각각 시험한다. 전개는 보존되고 과거 입력은 실행되지 않아야 한다. 비활성화 동안 착지/낙사한 경우 재활성화 시 먼저 정리되어야 한다.
7. 20회 반복 및 작은 Game 뷰의 날개·기존 한글 안내 가독성, 실제 조작감을 확인한다. 뷰 크기와 검수자를 기록한다.

## 4. 증거와 완료 판정

`TestResults/` 아래 기준선/변경 후 결과·로그·씬 스크린샷·하강 비교 표·입력~전개 및 복귀 시간·20회 반복 결과를 별도 파일로 남긴다. 환경, 실행 시각, 실제 수집 수와 실패/건너뜀을 함께 기록한다.

각 SC의 결과를 `통과 / 실패 / 미검증`으로 분리한다. 자동 상태 시험, 실제 렌더 시험, 사람 검수를 구분한다. 실행할 수 없는 항목은 차단 이유를 적고 미검증으로 남긴다. 이번 계획 문서 생성만으로 어떤 구현·검수 항목도 완료 처리하지 않는다.
