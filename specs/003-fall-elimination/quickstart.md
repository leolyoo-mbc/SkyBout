# Quickstart: 추락·낙사 탈락 검증

이 가이드는 구현 후 실행할 절차다. 계획 작성 시점에는 신규 기능이나 아래 시험을 실행하지 않았다.

## 사전 조건

- 기존 프로젝트 `D:/Projects/SkyBout`를 Unity Hub로 연다. 새 프로젝트를 만들지 않는다.
- Unity 버전은 `ProjectSettings/ProjectVersion.txt`의 **6000.3.26f1**을 사용한다. 패키지 복원·컴파일을 기다린다. 신규 패키지 설치는 필요 없다.
- 규칙: [화면·입력 계약](contracts/player-fall-elimination.md), [상태·높이 모델](data-model.md).
- 기존 `SkyBout.slnx` 사용자 변경을 보존한다. 과거 단계 가이드의 E: 경로 대신 현재 D: 경로를 사용한다.

## 씬 실행

1. `Assets/Scenes/SampleScene.unity`를 연다.
2. Player에 기존 PlayerMovement, PlayerInput, Rigidbody와 GroundCheckOffset 연결이 유지되었는지 확인한다.
3. Rigidbody 중력 사용·Y 이동 허용·linearDamping 0, 기존 Ground 마스크와 점프 높이 2m, Player/Move·Player/Jump 액션과 SendMessages를 확인한다.
4. 구현 후 Inspector에 추가될 `fallDeathHeight`가 -10이며 표면 Y=0보다 낮은지 확인한다. Play 전 안전한 시작 위치는 Player (0,1,0), 발 Y=0이다.
5. Play 후 Game 뷰를 클릭하고 모든 키를 해제한다. 중앙에서 WASD와 Space로 기존 이동·점프·착지를 확인한다.
6. 섬 가장자리까지 걸어가거나 바깥을 향해 점프한다. 아래로 떨어지고 발이 -10 아래로 내려가면 중앙에 `ELIMINATED`가 보여야 한다.
7. WASD·Space를 10초간 반복해도 수평 이동·새 점프·글라이더·생존 복귀가 없어야 한다. 다른 창으로 나갔다 돌아와도 탈락이 유지되어야 한다.
8. 반복 시험은 **Play 중지 후 다시 Play**로 새 인스턴스를 만든다. 게임 내 재시작 기능을 찾거나 추가하지 않는다.

## 자동 PlayMode 시험

### Editor에서 실행

1. Window > General > Test Runner > PlayMode를 연다.
2. 구현 후 추가할 `FallEliminationTests`와 기존 `JumpTests`, `MovementAndCameraTests`를 포함해 `SkyBout.PlayModeTests` 전체를 실행한다.
3. 실패·오류·경고와 측정 결과를 저장한다. 이전 단계의 37개 통과 기록을 이번 변경의 통과 증거로 재사용하지 않는다.
4. 수동 물리 틱 테스트 외에 실제 SampleScene을 로드해 Update/FixedUpdate/LateUpdate를 직접 호출하지 않는 자연 플레이 루프 시험도 포함해야 한다.

### 선택: Windows PowerShell 배치 실행

같은 프로젝트를 연 Editor를 먼저 종료한다. 실행 파일은 계획 시점에 존재를 확인했으며 아래 시험 명령은 아직 실행하지 않았다.

```powershell
Set-Location 'D:/Projects/SkyBout'
New-Item -ItemType Directory -Force 'TestResults' | Out-Null
& 'C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe' `
  -batchmode -projectPath 'D:/Projects/SkyBout' `
  -runTests -testPlatform PlayMode -assemblyNames SkyBout.PlayModeTests `
  -testResults 'D:/Projects/SkyBout/TestResults/fall-elimination.xml' `
  -logFile 'D:/Projects/SkyBout/TestResults/fall-elimination.log'
```

라이선스/패키지/컴파일 실패를 시험 통과로 간주하지 않는다. XML의 전체 결과와 로그를 확인한다. Game 뷰의 실제 안내 가시성은 배치 시험으로 대체하지 않고 아래 절차로 검증한다.

## 명세별 검증 표본

| 기준 | 실행할 시험 | 기대 결과 |
| --- | --- | --- |
| SC-001 | 네 가장자리 × 걷기/점프 × 각 5회 = 40회 | 전부 추락, 허공 착지·보이지 않는 경계 차단 0회 |
| SC-002 | 발 Y가 경계 바로 위·동일·아래인 조건별 각 5회; 별도 고속 하강 5회 | 위·동일 오탈락 0회, 아래 및 경계 통과 각 5회 탈락 |
| SC-003 | 탈락 10회, 각 판정 후 안내 시간 기록 및 10초 입력 | 안내 1초 이내, 이동 재개·새 점프·자동 복귀 0회 |
| SC-004 | 아직 표면 위에 착지할 수 있는 외측 점프에서 섬 안으로 복귀 5회, 중앙 점프·착지 10회, 섬 밖 Space 반복 5회 | 정상 착지·착지 후 이동과 새 점프, 공중 추가 점프·글라이더 0회 |
| SC-005 | 경계 -10/-20 각각 5회; 추적 카메라·화면 밖 안내 확인 | 해당 경계 아래에서만 총 10회 탈락, 안내 유지 |

- SC-004 복귀 시험은 발이 표면보다 아래로 내려간 뒤 섬 옆면을 뚫고 올라오는 시험이 아니다. 정점 이후라도 발이 표면보다 높은 동안 수평 조작으로 돌아와 착지한다.
- 경계와 정확히 같은 높이 시험은 중력 적분 전 직접 판정을 검사한다. 실제 하강·고속 통과는 별도 Physics.Simulate/자연 루프로 확인한다.
- 추가 방어 시험: NaN/양·음 Infinity 설정 거부, 판정 전 보류된 점프 폐기, 탈락과 접지 동시 조건의 탈락 우선, 탈락 후 높이 조정/초점 복귀/컴포넌트 재활성화에도 생존 복귀 없음.
- 기존 JumpTests의 씬 시험이 추락 후 같은 인스턴스로 옆면 시험을 수행한다. 탈락 뒤 정상 생존을 가정하지 않게 시험을 격리하고, 접지 판정을 약화시키거나 테스트용 부활 API를 추가하지 않는다.

## 안내 가시성 확인

1. 실제 Game 뷰를 16:9 및 4:3으로 각각 설정한다.
2. 생존 시 안내가 없고 탈락 후 중앙 고대비 패널과 `ELIMINATED`가 잘리지 않고 읽히는지 확인한다.
3. 탈락 후 테스트 절차로 플레이어를 카메라 밖에 두거나 잠시 카메라 추적을 멈춘 상태에서도 안내가 남는지 확인한다. 검사 후 카메라 설정을 복구하고 무관한 씬 변경을 저장하지 않는다.
4. 실제 Game 뷰 스크린샷을 로컬 검증 증거로 저장한다. Camera.Render 캡처만으로 OnGUI 표시를 검증하지 않는다.

## 설정 변경과 정리

Play를 멈추고 Inspector의 낙사 높이를 -20으로 변경해 새 실행에서 검증한 뒤 저장할 기본값 -10으로 복원한다. 씬 재실행은 기존 안전한 시작 위치에서 생존 상태여야 한다. 기존 액션·GUID·카메라·패키지·ProjectSettings가 불필요하게 바뀌지 않았는지 diff를 확인한다.

성능 벤치마크, 사람의 주관적 조작감 평가, 재시작·글라이더·온라인 검증은 이번 기능의 완료 증거에 포함하지 않는다.
