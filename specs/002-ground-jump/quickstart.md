# Quickstart: 기존 Unity 프로젝트의 점프 검증

기존 Unity 프로젝트에 구현된 지상 점프의 실행·재검증 가이드다. 최종 자동 검증과 실제 씬 카메라 캡처 확인 결과는 아래에 기록했다.

## Prerequisites

- 이 저장소 루트 E:/projects/SkyBout를 Unity Hub에서 기존 프로젝트로 연다. 새 프로젝트를 만들지 않는다.
- ProjectSettings/ProjectVersion.txt에 지정된 Unity 6000.3.26f1을 사용한다.
- 기존 패키지 복원과 컴파일 완료를 기다린다. 신규 패키지 설치는 필요 없다.
- 기존 사용자 변경인 EditorBuildSettings.asset 및 SkyBout.slnx를 초기화하지 않는다.
- 모델 및 동작 규칙: [data-model.md](data-model.md), [입력 계약](contracts/player-jump.md).

## Setup and Run

1. Assets/Scenes/SampleScene.unity를 연다.
2. Player의 기존 PlayerMovement, PlayerInput, Rigidbody와 발 위치 자식을 확인한다.
3. PlayerInput은 기존 Player_Actions 및 Player 맵/SendMessages를 사용해야 한다.
4. 점프 높이 2m, Ground 마스크, Rigidbody 중력 사용·비키네마틱·수직 이동 허용·linearDamping 0을 확인한다.
5. Play를 누르고 Game 뷰를 클릭한다. 처음 누르고 있던 키를 모두 해제한 뒤 중앙에서 검증한다.

## Automated PlayMode Tests

이번 단계는 점프와 기존 이동·카메라의 기능 동작만 검증한다. FPS·Profiler·메모리 할당 성능 검사는 수행하지 않는다.

1. Window > General > Test Runner를 열고 PlayMode를 선택한다.
2. JumpTests와 MovementAndCameraTests를 모두 실행한다.
3. 실패/오류 로그를 확인하고 결과를 저장한다. 입력/Physics 설정을 바꾸는 픽스처가 종료 시 원상 복구하는지도 확인한다.
4. 실제 SampleScene을 로드하는 통합 시험은 MonoBehaviour 생명주기를 자연스럽게 실행해야 한다. 단위형 수동 Step 시험과 혼동하지 않는다.

예정 검증군:
- 중앙 20회 점프·착지·재점프.
- 2m/4m 각각에서 짧은 누름·긴 누름·정지·이동 조건별 5회 정점 측정, 목표 대비 ±5%.
- 상승·정점·하강 재입력, 착지까지 홀드, 착지 직전 누름 각각 5회: 추가/예약 점프 0회.
- 한 입력 업데이트에 짧은 누름·해제가 모두 발생해도 유효 지상 누름은 1회 실행. 한 입력 이후 여러 물리 틱에서도 중복 실행 없음.
- 지상에서 요청을 받은 직후 지지 상실 시 소비 시점에 폐기.
- 가장자리 이탈, 옆면/트리거 접촉, 발보다 먼 지면은 접지 아님.
- 8방향 이동, 공중 방향 변경과 감속, Y 속도 보존.
- 초점 상실 시 수평 정지와 Y 유지, 복귀 홀드 차단, 해제/새 누름 허용.
- 잘못된 높이·마스크·Jump 누락·Rigidbody 설정은 진단 오류.
- 실제 씬 연결과 재로드 후 지상 시작, 점프 전 구간 카메라 가시성/커서 무관 동작.

## Manual End-to-End

아래 표는 사용자가 키보드로 직접 재확인할 때의 절차다. 이번 구현에서는 가상 키보드를 통한 자동 PlayMode 시험과 실제 씬 카메라 캡처 검토로 기능을 검증했다. 사람의 수동 조작 및 주관적인 조작감 튜닝은 수행하지 않았다.

| 작업 | 기대 결과 |
| --- | --- |
| 중앙에서 Space 1회 | 상승→정점→하강→착지 |
| 착지 후 해제하고 다시 누름 | 같은 높이로 새 점프 |
| 이륙부터 착지 뒤까지 홀드 | 한 번만 점프 |
| 공중에서 반복 누름 | 추가 상승 및 글라이더 없음 |
| W/A/S/D 및 4개 대각선으로 점프 | 기존 화면 기준 조작 유지, 섬 안에 착지 가능 |
| 공중 방향 변경 및 키 해제 | 설정한 방향 전환과 감속 적용 |
| 중앙·가장자리에서 점프 관찰 | 캐릭터 전체와 주변 섬 표면 가시 |
| 점프 중 커서 이동 | 커서 때문에 카메라가 변하지 않음 |
| 섬 밖으로 점프 | 발밑 표면 없으면 계속 하강, 허공 착지/점프 없음 |
| 옆면 접촉 | 착지로 판정하지 않음 |
| 다른 창 이동 후 복귀 | 새 Space 누름 없이 점프 없음 |
| Play 중지 후 다시 시작 | 기존 안전한 지상 위치에서 시작 |

섬 밖 검증 뒤에는 Play를 중지하고 재실행한다. 낙사 복귀는 아직 범위가 아니다. 정점 수치는 테스트가 측정하며 사용자에게 화면에서 수동 거리 계산을 요구하지 않는다.

## Height Tuning

Play를 중지하고 PlayerMovement의 jumpHeight를 2에서 4로 바꿔 다시 실행한다. 각 높이에서 위 자동 측정 및 가시성 검증을 반복한다. 검사 후 저장할 기본값을 결정한다. 전용 게임 설정 화면은 만들지 않는다.

## Implementation Progress — 2026-10-29

- T001: 기존 Unity 6000.3.26f1 Editor 연결 확인. CLI 1.0.0-beta.14 최신 버전 확인. Unity/C# `.gitignore` 필수 생성물 제외 규칙 확인; 추가 도구용 ignore 파일은 해당 없음.
- 기존 사용자 변경: `ProjectSettings/EditorBuildSettings.asset`, `SkyBout.slnx`. 초기 SHA-256은 각각 `348ba1eca4b0698957f264f877f76227b78583acd3138fd5fc85be92f158e261`, `4d6c6d17fe94a8094863a436143e4baa47aa8dc1845a8322b0b7cbacfb4fd6e5`.
- `Packages/manifest.json` 초기 SHA-256: `cb8b975c38e2683412d6f80b434d0ef3eb8daab46cdcf859847d2224ac9bac6c`.
- T002: 연결된 Editor에서 `run_tests`로 기존 MovementAndCameraTests 6개 실행, **6 passed / 0 failed**. 기능 변경 전 기준 결과.

- T003~T005: 새 접지 시험에서 기존 코드의 근접 지면 오판을 확인(1 failed), Collision.GetContacts 기반 지지 보완 후 전체 8개 **8 passed / 0 failed**. 기존 지상 시험의 초기 정착 처리를 접지 변경과 함께 선반영했다.

- T006: 점프 구현 전 새 시험 4개가 액션/필드/이륙 부재로 실패함을 확인했다.
- T007~T013: 기존 입력 자산·SampleScene에 점프 연결. JumpTests **19 passed / 0 failed**. 중앙 20회 재점프, 2m/4m 각각 짧은 누름·긴 누름·정지·이동 조건별 5회(총 40회) 정점 오차 ±5% 통과. 잘못된 설정·필수 액션 누락 진단도 통과했다.

- T014~T019: JumpTests **26 passed / 0 failed**. 공중 상승·정점·하강·착지 직전 입력 및 지속 홀드 각각 5회에서 자동/추가 점프 0회 확인. 짧은 입력 단발 소비, 지지 상실 시 폐기, 초점·비활성화 경계 통과.

- T020~T023: 공중 조작 시험 2개가 기존 0.3배 가속/감속 미적용 때문에 실패함을 확인한 뒤 공통 수평 조작으로 변경했다. 이후 전체 **35 passed / 0 failed**. 실제 Rigidbody 2m/4m 점프의 캐릭터 경계 8개 꼭짓점 및 지면 ±1m 네 지점 가시성·커서 무관 시험 통과. 카메라 코드·설정 변경 없음.
- T026 통합 시험 첫 실행은 8방향 이동/카메라 검증 후 가장자리에서 고정 90스텝을 기다리는 조건이 4m 점프 비행 시간보다 짧아 실패했다. 추락 여부를 제한된 최대 200스텝 안에서 관찰하도록 시험 대기 조건을 보완했다(런타임 오류로 기록하지 않음).

## Completion Evidence — Final

연결된 Unity 6000.3.26f1에서 최종 PlayMode **37 passed / 0 failed / 0 skipped**. 컴파일 오류·경고 0개. 결과는 `TestResults/ground-jump-final.json`에 저장했다(로컬 생성물, Git 제외).

| 기준 | 결과 및 증거 |
| --- | --- |
| SC-001 | 중앙 20회 연속 점프·착지·재점프 통과. 착지 실패·관통·공중 정지 0회 |
| SC-002 | 2m/4m 각각 4조건×5회=총 40회. 표시 정밀도 4자리에서 2.0000m 및 4.0000m, 전부 ±5% 이내. 실제 씬 자동 생명주기에서도 8방향 2m 점프와 정지 4m 점프가 각각 2.0000m/4.0000m로 측정됨 |
| SC-003 | 상승·정점·하강 재입력, 지속 홀드, 착지 직전 누름 각각 5회. 추가/자동 점프 0회. 짧은 입력 1회 소비 및 지지 상실 시 폐기 통과 |
| SC-004 | 8방향 점프·공중 방향 전환·해제 감속·착지 후 이동·Y 보존 통과. 2m/4m 전 구간에서 캐릭터 경계 8개 꼭짓점과 지면 ±1m 네 지점 화면 투영 통과. 커서 위치 비교 시험 통과. 실제 씬 캡처도 확인했으며 카메라 변경 불필요 |
| SC-005 | 테스트 지형 및 실제 씬의 가장자리/옆면 비접지, 공중 점프 차단, 초점 복귀 홀드 차단, 씬 재로드 후 안전한 지상 시작 통과 |

- 실제 씬 통합 시험은 Update/FixedUpdate/LateUpdate를 직접 호출하지 않고 자연스러운 플레이 루프로 실행했으며 Time.frameCount 증가를 확인했다.
- 로컬 측정 기록: `Temp/ground-jump-height-2m.txt`, `Temp/ground-jump-height-4m.txt`, `Temp/ground-jump-scene-measurements.txt`.
- 카메라 화면 캡처: `Temp/ground-jump-2m.png`, `Temp/ground-jump-4m.png`. 캐릭터 전체·주변 섬 표면의 실제 렌더링을 검토했다. Temp 파일은 재생성 가능한 로컬 생성물이다.
- T027: 기존 Player 맵/Move ID와 설정·기존 바인딩·컨트롤 스킴을 원본과 대조해 보존을 확인했다. 런타임/테스트 asmdef와 기존 메타 참조도 유지했다.
- 기존 사용자 변경 EditorBuildSettings.asset·SkyBout.slnx와 Packages/manifest.json은 초기 SHA-256과 동일하다. Editor가 재작성한 무관한 ProjectSettings 파일의 줄바꿈 변경은 정리했다.
- 성능 테스트는 요청대로 미실행·범위 제외. 글라이더·낙사 탈락/복귀·온라인은 구현하지 않았다.
- 사람의 수동 키보드 플레이와 주관적 조작감 평가는 미실행이며 위 자동 검증과 구분한다.

### CLI 재실행

실행 중인 이 프로젝트의 Editor가 준비된 상태에서 다음 명령을 사용한다.

```bash
unity command run_tests --mode playmode --filter SkyBout.PlayModeTests --filter_type assembly --async_tests true --project-path E:/projects/SkyBout --caller plugin --skill unity-cli --format json
unity command test_status --project-path E:/projects/SkyBout --caller plugin --skill unity-cli --format json
```

`test_status`가 completed가 되면 summary와 실패 목록을 확인한다.
