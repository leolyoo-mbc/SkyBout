# Quickstart: 섬 사이 이동 검증

## 전제와 현재 상태

이 문서는 구현된 섬 배치와 이동 시험을 다시 실행하는 검증 가이드다. SampleScene에 IslandShort/IslandLong이 추가되어 있고, 신규 자동 시험은 `Assets/Tests/PlayMode/IslandTraversalSceneTests.cs`에 있다. 자동 결과와 사람 관찰은 별도로 기록하며, 사람이 확인하지 않은 항목을 완료로 간주하지 않는다.

- 프로젝트: `E:\projects\SkyBout`, Editor: `ProjectSettings/ProjectVersion.txt`의 6000.3.26f1.
- 설치된 최신 Unity CLI를 사용한다. CLI 갱신은 필요할 때 환경 담당자가 수행하며 프로젝트 Editor 버전을 올리지는 않는다.
- 미저장 사용자 씬 변경을 확인하고 보존한다. 기존 실행 중인 테스트와 Play Mode를 종료한 뒤 검증한다.
- 기본 Game 뷰 1280×720, 16:9, timeScale=1. 기존 fixedDeltaTime=0.02를 유지한다.
- [data-model.md](data-model.md)의 배치·출발점과 [조작/씬 계약](contracts/island-traversal.md)을 기준으로 한다.

## 1. 자동 시험

프로젝트 루트에서 결과 폴더를 준비한다.

```powershell
New-Item -ItemType Directory -Path 'E:\projects\SkyBout\TestResults' -Force
unity test 'E:\projects\SkyBout' --mode PlayMode --filter IslandTraversalSceneTests --output 'E:\projects\SkyBout\TestResults\008-traversal.xml'
unity test 'E:\projects\SkyBout' --mode PlayMode --output 'E:\projects\SkyBout\TestResults\008-playmode-all.xml'
```

`unity test`는 배치 Editor 프로세스를 사용하므로 같은 프로젝트가 이미 Editor에서 열려 있으면 해당 창의 **Window → General → Test Runner → PlayMode**를 사용한다. 신규 클래스만 실행한 뒤 전체 PlayMode를 실행하고 결과 XML을 위 경로에 저장한다. 작업 중인 Editor를 강제 종료하거나 두 프로세스가 같은 프로젝트를 동시에 열게 하지 않는다.

열린 Editor의 Pipeline으로 자동 실행할 때는 `unity command run_tests --mode playmode --filter IslandTraversalSceneTests --async_tests true`를 한 번 호출하고 `unity command test_status --json`으로 완료를 확인한다. 요청 오류가 나도 실제 실행 여부를 먼저 확인하며 실행 중에 두 번째 run_tests를 보내지 않는다. 이 경로는 배치 Editor를 새로 열지 않는다. NUnit XML은 Test Runner의 Save Results 또는 실행 중 등록한 `TestRunnerApi.SaveResultToFile` 콜백으로 저장한다.

스토리별 시험 메서드는 `US1_`(지형·짧은 점프), `US2_`(긴 구간·높이 유지·카메라·연속 이동), `US3_`(낙사·재시작·입력 경계)로 시작한다. Pipeline 필터는 예를 들어 `--filter IslandTraversalSceneTests.US1_`를 사용하고 결과를 `TestResults/008-us1.xml`에 저장한다. 같은 방식으로 US2/US3 결과를 `008-us2.xml`, `008-us3.xml`에 보존한다. 구현 전 기준선은 `008-baseline-playmode.xml`, 환경·기존 실패 기록은 `008-baseline.md`다. 시각 검수는 그래픽을 켠 16:9 1280×720 렌더로 수행한다.

실제로 검증한 자동 실행 경로는 **열린 그래픽 Editor의 Pipeline/Test Runner**다. 위 배치 CLI 경로의 CI 실행은 별도 미검증이며, `WaitForEndOfFrame`·화면 캡처를 사용하는 시험이 있으므로 배치/무그래픽 결과를 시각 검수와 동등하게 취급하지 않는다.

기대 결과:

- 신규 씬 시험은 실제 SampleScene의 섬 배치, 짧은 점프/긴 점프 실패, 글라이딩 왕복과 접기 착지, 연속 이동과 재시작을 검증한다.
- 기존 동쪽 이탈·낙사·점프·재시작·입력·목표 높이·카메라 회귀가 모두 유지된다.
- 새 클래스가 아직 없다면 필터 결과 0건은 통과가 아니라 **미구현/미검증**이다. XML에 해당 클래스와 실제 실행 건수가 있는지 확인한다.
- 렌더 검증은 그래픽이 있는 Editor에서 실행한다. `-nographics` 결과로 시각 검수 통과를 주장하지 않는다.

## 2. 실제 조작과 목적지 착지

`Assets/Scenes/SampleScene.unity`를 열고 Play한다. A는 기존 Ground, 북쪽 B는 IslandShort, B 동쪽 C는 IslandLong이다.

1. WASD로 A 북쪽 가장자리 안쪽에 접근한다. W 이동 중 Space를 새로 눌러 점프하고 공중에서 다시 누르지 않는다. B 착지 후 걷기·새 점프를 확인한다. S로 A에 돌아오는 시험을 포함해 방향별 5회 수행한다.
2. B의 동쪽 도움닫기 시작점에서 D로 전속력에 도달하고 출발선에서 점프만 한다. C 표면에 착지하지 못하는지 확인한다. C→B도 A 키 방향으로 같은 조건에서 각각 5회 확인한다. 독립 시험 초기 배치는 테스트 fixture에서만 준비할 수 있으며 이동 구간을 순간 이동으로 대체하지 않는다.
3. 같은 긴 구간에서 점프 후 Space를 해제하고 공중에서 새로 눌러 유지한다. 목적지 중심 부근까지 이동하고 WASD를 먼저 놓아 제동한다. Space를 놓아 내려앉고 걷기·새 점프를 확인한다. 양방향 각 5회 수행한다.
4. 목적지 위에서 WASD만 놓고 Space를 유지한다. 발 높이가 안정된 후 2±0.1m를 10초 유지하는지 측정하고 Space를 놓아 3초 안에 실제 착지하는지 5회 확인한다. 떠 있는 동안 자동 접기를 기대하지 않는다.
5. 최초 시작 위치부터 A→B→C→B→A를 3회 이어간다. 이 시험은 위치/속도/상태 주입 없이 실제 입력만 사용한다.

반복 수와 출발 좌표는 자동 씬 시험으로 재현하고, 사람이 확인한 시도도 별도 기록한다. 장거리 실패 시험 뒤에는 실제 낙사·R 복귀를 사용하거나 독립 시험 환경을 새로 준비하며 디버그 배치와 실제 주행 증거를 구별한다.

## 3. 카메라·충돌·실패 복구

- 짧은 구간 W/S와 긴 구간 D/A에서 최종 가장자리 접근·비행·착지를 각각 캡처한다. 플레이어·목적지 가장자리·빈 공간을 식별할 수 있어야 한다. 정확한 viewport 검사 지점은 계약을 따른다.
- 옆면 접촉만으로 Grounded가 되지 않고 틈 위에서 걸을 수 없으며 섬 표면을 관통하지 않는지 확인한다.
- B 또는 C에서 출발하여 빈 공간 위에서 Space를 놓고 실제 낙사한다. 안내 후 새 R로 A에 돌아와 새 점프·전개를 수행하는 과정을 10회 반복한다. 첫 5회는 초기와 같은 섬 배치도 확인한다.
- R 유효 입력부터 복귀까지 ≤2초, 시작 위치/방향 동일, 과거 전개·속도·유지 입력 자동 실행 없음이 기준이다. 초점 전환 시 접기와 해제 후 새 입력 요구도 확인한다.

## 4. 결과 기록

`TestResults/008-validation.md`에 다음을 기록한다.

- Editor/CLI 버전, 화면 크기·화면비, 실행 날짜, 실제 씬 배치, 테스트 XML 경로.
- SC-001~SC-007별 **통과 / 실패 / 미검증**, 실제 시도 수와 실패 조건.
- `008-traversal-attempts.csv`와 `008-camera-*.png` 경로. 기록 필드는 data-model을 따른다.
- 관찰자가 짧은 점프·긴 글라이딩·접기 착지의 차이를 구별할 수 있었는지와 시야 불편 여부.
- 사람이 관찰하지 않았으면 SC-007의 관찰 부분을 미검증으로 남긴다. XML 통과로 이를 대체하지 않는다.

시야 또는 구간 검증 실패 시 이동 수치를 바꿔 숨기지 않는다. 배치/시험 입력 오류를 우선 조사하고 계약 변경이 필요하면 명세와 계획을 먼저 재검토한다.

## 구현 및 검증 기록 (2026-11-02)

- 씬 배치는 data-model의 좌표 그대로 적용했다. 기존 Ground/Player/Camera와 런타임 이동·입력·카메라 스크립트는 보존했다.
- 기존 PlayMode 기준선: 159/159 통과 (`TestResults/008-baseline-playmode.xml`).
- 스토리별 실제 시험: US1 3/3, US2 5/5, US3 2/2 통과 (`008-us1.xml`, `008-us2.xml`, `008-us3.xml`).
- 신규 클래스 전체 재실행: 10/10 통과, 389.13초 (`TestResults/008-traversal.xml`). 이어 기존/신규 전체 PlayMode 169/169 통과, 723.74초 (`TestResults/008-playmode-all.xml`). 기존 기준선 159개 누락·실패는 0개다. 자세한 결과는 `TestResults/008-validation.md`에 기록했다.
- 실제 출발 좌표·속도·결과는 `008-traversal-attempts.csv`, 양방향 장면은 `008-camera-us1-*.png`/`008-camera-us2-*.png`, 탈락·복귀 화면은 `008-camera-us3-*.png`다. 경로는 모두 `TestResults/` 아래다.
- 카메라 경로 시험은 1280×720, 16:9 렌더 기준이며, 다른 화면비의 플레이 가시성은 미검증이다. 탈락/복귀의 화면 전체 캡처는 실행 당시 Game 뷰의 실제 크기다.
- 사람의 직접 플레이·가시성 확인은 아직 미검증이다. 관찰자 결과가 없으면 T021 및 최종 인수 마무리를 완료로 표시하지 않는다.
