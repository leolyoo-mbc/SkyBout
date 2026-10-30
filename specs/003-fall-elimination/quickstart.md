# Quickstart: 추락·낙사 탈락 검증

이 문서는 실행 절차와 이번 단계의 검증 기록이다. 아래 Implementation Evidence에 기록된 항목만 실제 수행 결과이며, 절차만 적힌 항목은 통과 증거가 아니다.

## 사전 조건

- 기존 프로젝트 `D:/Projects/SkyBout`를 Unity Hub로 연다. 새 프로젝트를 만들지 않는다.
- Unity 버전은 `ProjectSettings/ProjectVersion.txt`의 **6000.3.26f1**을 사용한다. 패키지 복원·컴파일을 기다린다. 신규 패키지 설치는 필요 없다.
- 규칙: [화면·입력 계약](contracts/player-fall-elimination.md), [상태·높이 모델](data-model.md).
- 기존 `SkyBout.slnx` 사용자 변경을 보존한다. 과거 단계 가이드의 E: 경로 대신 현재 D: 경로를 사용한다.

## 씬 실행

1. `Assets/Scenes/SampleScene.unity`를 연다.
2. Player에 기존 PlayerMovement, PlayerInput, Rigidbody와 GroundCheckOffset 연결이 유지되었는지 확인한다.
3. Rigidbody 중력 사용·Y 이동 허용·linearDamping 0, 기존 Ground 마스크와 점프 높이 2m, Player/Move·Player/Jump 액션과 SendMessages를 확인한다.
4. Inspector의 `fallDeathHeight`가 -10이며 표면 Y=0보다 낮은지 확인한다. Play 전 안전한 시작 위치는 Player (0,1,0), 발 Y=0이다.
5. Play 후 Game 뷰를 클릭하고 모든 키를 해제한다. 중앙에서 WASD와 Space로 기존 이동·점프·착지를 확인한다.
6. 섬 가장자리까지 걸어가거나 바깥을 향해 점프한다. 아래로 떨어지고 발이 -10 아래로 내려가면 중앙에 `ELIMINATED`가 보여야 한다.
7. WASD·Space를 10초간 반복해도 수평 이동·새 점프·글라이더·생존 복귀가 없어야 한다. 다른 창으로 나갔다 돌아와도 탈락이 유지되어야 한다.
8. 반복 시험은 **Play 중지 후 다시 Play**로 새 인스턴스를 만든다. 게임 내 재시작 기능을 찾거나 추가하지 않는다.

## 자동 PlayMode 시험

### Editor에서 실행

1. Window > General > Test Runner > PlayMode를 연다.
2. `FallEliminationTests`와 기존 `JumpTests`, `MovementAndCameraTests`를 포함해 `SkyBout.PlayModeTests` 전체를 실행한다.
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

## Implementation Evidence — 2026-10-30

- T001: 연결된 Editor의 프로젝트 `D:/Projects/SkyBout/Assets`, Unity `6000.3.26f1`, 비재생·SampleScene 저장 상태를 확인했다. CLI는 1.0.0-beta.15로 갱신했다. 선택적인 Unity 에이전트 스킬 설치 안내는 확인했으나 추가 설치는 하지 않았다.
- 최초 사용자 변경은 `SkyBout.slnx`뿐이다. 보존 기준 53개 파일의 SHA-256을 `TestResults/fall-elimination-baseline-hashes.json`에 저장했다. SkyBout.slnx: `dc2464a787b54eb71de24680204643c6ab6909e2ef93d62f2ff755e708a2d102`, Packages/manifest.json: `cdd00c7f8876e6f1f6b5046e5a49320924fbf05f9a7504141a7a8d6ecdf96ca5`.
- `.gitignore`에 Library/Temp/Logs/UserSettings/Obj/bin, IDE 생성물과 TestResults 제외가 있다. 프로젝트 Packages는 소스이므로 .NET packages 일반 규칙으로 무시하지 않는다. Docker·ESLint·Prettier·Terraform·Helm용 설정이 없어 추가 ignore 파일은 만들지 않았다.
- 기준 시험의 첫 MCP 호출은 400/타임아웃 및 중복 실행으로 Test Runner가 중단되어 유효 결과가 없었다. 비재생·씬 비수정 상태를 확인하고 cancel_tests 후 CLI로 단일 비동기 실행을 다시 요청했다. 인프라 실패와 기능 시험 실패를 구분한다.
- T002: 단일 재실행 기준 시험 **37 passed / 0 failed / 0 skipped**, 20.34초. 결과: `TestResults/fall-elimination-baseline.json`.
- T003: 신규 픽스처 단독 **1 passed**, 가상 입력·실제 접지 초기화 확인. 새 `.meta`는 Unity가 생성했다.
- T004~T006: 새 추락 시험을 포함한 전체 **41 passed / 0 failed**, 20.67초. 네 가장자리 걷기/점프 40회, 공중 복귀 착지 5회·중앙 점프 10회·공중 Space 5회 통과. 기존 런타임 변경 없이 중력·접지·공중 조작을 그대로 재사용한다. 결과: `TestResults/fall-elimination-us1.json`.
- CLI는 이전 Pipeline 명령 이름 사용 안내를 표시했다. 프로젝트 의존성 보존을 위해 Pipeline 업그레이드는 하지 않았다. run_tests에는 filter_type을 명시해 첫 호출의 400을 피했다.
- T007~T008: 기능 구현 전 **4 passed / 10 failed**. 실패는 fallDeathHeight/IsEliminated 부재이며 `TestResults/fall-elimination-red.json`에 저장했다.
- T009~T013: 발 높이 엄격 비교·탈락 래치·입력/접촉 차단·비유한 높이 진단·OnGUI 안내를 기존 컴포넌트에 추가했다. 새 기능 시험 **14 passed / 0 failed** (`TestResults/fall-elimination-green.json`). 두 경계 각각 위/동일/아래 5회, 고속 통과 5회, 탈락 10회×10초 입력과 비부활 검사 통과.
- T013: 연결된 Editor에서 SerializedObject로 낙사 높이를 저장했다. SampleScene diff는 `fallDeathHeight: -10` 한 줄뿐이며 기존 연결을 보존했다.
- T014: 기존 JumpTests 씬 시험은 발이 표면 아래로 조금 내려가자마자 관찰을 중지하고 이후 벽 접촉 위치의 발 Y=-1.8을 사용한다. 새 기본 경계 -10보다 높으므로 탈락 후 생존을 가정하는 구간이 없으며 기존 시험 수정은 불필요하다. 최종 전체 회귀에서 다시 확인한다.
- T015: 실제 씬을 새로 로드하며 10회 자연 추락·첫 GUI 프레임 캡처·입력 차단을 검사하고 11번째 로드에서 안전한 생존 시작을 확인하는 통합 시험을 추가했다. Update/FixedUpdate/LateUpdate 직접 호출 없이 진행하며 마지막 표본은 카메라 추적을 멈추고 플레이어를 화면 밖으로 옮겨 안내 유지를 검사한다.
- 통합 캡처를 위해 Game 뷰를 활성화하자 기존 신규 픽스처의 입력 시험 4개가 Editor 초점 정책 때문에 실패했다. Input System 자체 InputTestFixture 패턴과 같이 테스트에서만 IgnoreFocus/AllDeviceInputAlwaysGoesToGameView를 설정하고 종료 시 복원한 뒤 기능 시험 **15 passed**를 확인했다. 런타임 초점 규칙은 바꾸지 않았다.
- 화면 밖 시험 첫 시도는 Rigidbody 보간으로 이동 직후 Transform이 이전 프레임에 남아 실패했다. 테스트 순간 이동 시 물리 위치와 Transform을 함께 동기화하도록 수정했다. 4:3 통합 재실행 **1 passed / 0 failed** (`TestResults/fall-elimination-4x3.json`), 10개 표본 모두 실제 GUI의 흰 글자·검은 배경 픽셀을 확인했다.
- 별도 비동기 4:3 화면 요청은 리사이즈 직후 GUI가 그려지기 전 캡처되어 증거로 채택하지 않았다. 대신 자연 루프에서 WaitForEndOfFrame 이후 ScreenCapture로 검증한다. CLI screenshot 구현은 Camera.Render를 사용하므로 IMGUI 증거로 사용하지 않는다.
- 4:3 Game 뷰는 실제 513×385로 실행했다. `TestResults/fall-overlay-513x385-0.png`~`-9.png` 및 `fall-overlay-timings-4x3.txt`가 유효한 증거다. 임시 4:3 프리셋을 제거하고 기존 16:9 선택을 복원했으며, 검사 전용 카메라 비활성화는 Play 종료로 복원했다. 씬은 비수정 상태다.

## Completion Evidence — Final

- T016~T018: 최종 전체 PlayMode **52 passed / 0 failed / 0 skipped**, 38.08초. 기존 37개와 신규 15개 모두 통과했다. 결과: `TestResults/fall-elimination-final.json`. C# 컴파일 오류·경고 0개이며 최종 Editor의 compilationFailed=false도 확인했다.
- 16:9 실제 Game 뷰(513×289)에서 자연 낙사 10회 모두 흰 문구·어두운 중앙 패널 픽셀을 검사했다. 탈락 관찰→첫 표시 프레임은 **0.0052~0.0076초**로 모두 1초 이내다. 증거: `TestResults/fall-overlay-513x289-0.png`~`-9.png`, `fall-overlay-timings.txt`.
- 4:3 실제 Game 뷰(513×385)에서도 10회 모두 통과했으며 표시 시간은 **0.0044~0.0055초**였다. 각 비율의 마지막 표본은 플레이어를 카메라 밖에 두고 패널·문구가 유지됨을 확인했다. 두 비율의 최종 화면 캡처도 확인했다.
- 생존 화면 `TestResults/fall-alive-16x9.png`에는 중앙 탈락 패널이 없음을 픽셀 검사로 확인했다. `fall-offscreen-16x9.png`에서도 화면 밖 플레이어와 무관한 안내를 확인했다. 이는 자동 입력·실제 화면 캡처 검증이며 사람이 키보드로 플레이한 기록은 아니다.

| 명세 기준 | 최종 결과 |
| --- | --- |
| SC-001 | 네 가장자리 걷기/점프 각 5회, 총 40회 추락; 허공 착지·경계 차단 0회 |
| SC-002 | -10/-20 각각 위·동일·아래 각 5회, 총 30회 경계 확인 및 고속 통과 5회; 오탈락·누락 0회 |
| SC-003 | 탈락 10회×10초 반복 입력 차단, 래치 유지·보류 점프 폐기; 실제 안내 10회 모두 1초 이내 |
| SC-004 | 공중 복귀 착지 5회·중앙 점프 10회·공중 Space 5회 및 기존 이동/점프/카메라 37개 회귀 통과 |
| SC-005 | 두 높이 각각 아래 진입 5회, 실제 카메라 연결·씬 재로드 생존 시작, 16:9/4:3 안내·화면 밖 유지 통과 |

- T019: 초기 보존 대상 **53개 파일 SHA-256 전부 동일**. SkyBout.slnx 사용자 변경, 기존 입력 자산·메타·어셈블리·카메라 코드·Packages/manifest.json·ProjectSettings를 보존했다. 신규 시험 메타 GUID 중복 없음, `git diff --check` 통과. 줄바꿈 변환 안내는 있었지만 보존 대상의 실제 바이트는 동일하다.
- 씬 diff는 `fallDeathHeight: -10` 1줄이다. 런타임 변경은 기존 PlayerMovement 확장뿐이며 신규 런타임 컴포넌트·패키지는 없다.
- 최종 Editor: 비재생, 저장된 `Assets/Scenes/SampleScene.unity`, dirty=false, Player (0,1,0), 낙사 높이 -10, 기존 카메라 활성, 기존 16:9 프리셋(index 1). 임시 4:3 검증 프리셋은 제거했다.
- `tasks.md` 19개 작업 완료. 사람의 수동 조작감 평가·성능 벤치마크·플레이어 빌드는 수행하지 않았다. 재시작·글라이더·온라인은 제외했다. 탈락 이후 수직 중력은 유지하므로 장시간 관전/추락체 정리는 후속 재시작·관전 단계에서 다룬다.
