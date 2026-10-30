# Quickstart: 탈락 후 재시작 검증

이 문서는 구현 및 검증 가이드다. 아래 실행 기록은 실제 실행 결과만 기록한다.

## 실행 기록 — 2026-10-30

- 기준선: Unity Editor MCP PlayMode 52개 중 51개 통과. `MovementAndCameraTests.CameraStartsOnTargetTracksAndIgnoresCursor`의 보간 위치 기대값이 207.0 초과, 실제 206.935196으로 실패했다. 소스 변경 전 해당 시험 단독 재실행은 1/1 통과하여 기존 프레임 시간 의존 불안정으로 기록한다.
- 기존 Unity/C# ignore 설정 확인 완료. 추가 패키지·ignore 파일 변경 불필요.
- US1 RED: 새 시험 5개 중 바인딩 1개 통과, 복귀·카메라 참조·Restart 검증 4개 실패 확인.
- US1 GREEN: Editor를 전경으로 전환한 뒤 새 시험 5/5 통과(76.38초). 동일 SampleScene 인스턴스로 자연 낙사·점프·재시작 20회, 10회 무입력 2초 안정, 화면 밖 안내와 복귀 카메라 확인. 첫 통합 실행은 19번째까지 성공 후 마지막 R 입력에서 실패했고 전경 재실행에서 통과했으므로 초점 민감성은 US2에서 별도로 검증한다.
- 증거: `TestResults/restart-timings.txt`, `TestResults/restart-overlay-0.png`, `TestResults/restart-overlay-9.png`. 기존 액션·바인딩은 변경 전 JSON과 비교하여 모두 보존됨을 확인했다.
- US2 RED: 비시각 시험 12개 중 10개 통과, 초점 전환·비활성화 후 예약 재시작이 남는 사례 2개 실패. 해당 요청과 입력 준비 상태를 생명주기 전환에서 초기화한 뒤 12/12 통과했다. 실제 씬의 초점·Space 유지 시험도 통과했다.
- 최종 전체 PlayMode: **66/66 통과**, 실패·건너뜀 0개, 115.65초. 기존 52개 + 신규 14개(비시각 12개, 씬 통합 2개). 기존 테스트 파일은 수정하거나 검증을 약화하지 않았다.
- 최종 20회 기록: 탈락 판정 관찰부터 첫 안내 프레임까지 최대 0.0025초, R 요청부터 복귀 렌더 확인까지 최대 0.0234초. 측정은 시험에서 관찰한 시간이며 물리 경계 통과의 절대 시각을 계측한 값은 아니다.
- 최종 정적 검증: 기존 액션·바인딩 완전 보존, 입력 ID 중복 없음, 신규 테스트 .meta 존재, 기존 씬은 카메라 참조 1개만 추가, `git diff --check` 통과. `SkyBout.slnx`의 사용자 변경은 보존했다.
- **미완료 수동 검수(T015)**: 실제 사람이 작은 Game 뷰에서 한글 안내 가독성과 키보드 조작감을 확인하는 절차는 실행하지 않았다. 자동 시험은 실제 Game 뷰의 고대비 텍스트 픽셀·화면 밖 안내·복귀 카메라 및 20회 반복을 검증했지만, 픽셀 수 검증이 한글 문자의 정확한 가독성을 보증하지는 않는다. T015는 미완료로 유지한다.

| 기준 | 자동 검증 결과 | 근거 |
| --- | --- | --- |
| SC-001 | 통과 | 씬 안내·복귀 시간 20회, 화면 밖 안내 포함 |
| SC-002 | 통과 | 비시각 10회 및 실제 씬 최초 10회 무입력 2초 안정 |
| SC-003 | 통과 | 같은 씬/플레이어/카메라로 20회 자연 낙사·점프·복귀 |
| SC-004 | 통과 | 생존 R·키 유지·반복 입력 각 5회 |
| SC-005 | 통과 | 복귀 후 경계 위·동일·아래 각 5회, 탈락 중 10초 입력 차단 5회 |
| SC-006 | 통과 | Space 유지·초점·동시 낙사/R 각 5회, 실제 씬 초점 시험 포함 |

## 사전 조건

- 현재 저장소 `D:\Projects\SkyBout`를 Unity **6000.3.26f1**으로 연다.
- 기존 패키지 복원이 완료되어야 한다. 새 패키지는 설치하지 않는다.
- `Assets/Scenes/SampleScene.unity`의 기존 Player, Ground, Main Camera를 사용한다.
- 구현 후 PlayerInput의 Player 맵에 Restart 액션이 있고 PlayerMovement의 cameraFollow가 기존 Main Camera의 TopDownCameraFollow에 연결되어 있는지 확인한다.
- Console에 컴파일·설정 오류가 없어야 한다.
- 상세 규칙은 [입출력 계약](contracts/restart.md), [상태 모델](data-model.md)을 따른다.

## 1. 실제 씬 수동 검증

1. SampleScene에서 Play를 시작하고 Game 뷰에 초점을 둔다.
2. 최초 위치와 탑뷰를 확인하고 WASD 이동·Space 점프·착지를 확인한다.
3. 섬 밖으로 걸어 나가 탈락한다. 1초 이내에 탈락 안내와 R 키 안내가 읽히는지 확인한다.
4. 모든 키를 놓은 후 R을 한 번 누른다. 2초 이내에 최초 위치에서 생존하고 안내가 사라지는지 확인한다. 카메라도 즉시 플레이어와 주변 섬을 보여야 한다.
5. 입력 없이 2초간 대기한다. 이전 낙하 속도로 재추락하거나 자동 점프하지 않아야 한다.
6. 이동·점프·착지 후 다시 낙사한다. Play를 중지하지 않고 위 과정을 **20회** 반복한다. 플레이어·카메라·안내가 중복되지 않아야 한다.
7. 별도 10회 표본에서 탈락 안내 시간, R 입력부터 조작 가능까지 걸린 시간, 무입력 2초 안정성을 기록한다(SC-001~003).

## 2. 입력 경계 검증

다음 항목을 각각 **5회** 확인한다.

- 생존 중 지상과 공중에서 R을 눌러도 최초 위치로 돌아가지 않는다.
- R을 누른 채 낙사해도 탈락 상태를 유지하고, 놓았다가 다시 누르면 복귀한다.
- 탈락 후 R을 길게 누르거나 연타해도 한 번만 복귀한다.
- 탈락 중 Space를 누른 채 R로 복귀해도 자동 점프하지 않는다. Space를 놓았다가 새로 누르면 점프한다.
- 탈락 전부터 WASD를 유지하고 R로 복귀하면 현재 방향으로 이동할 수 있다.
- 탈락 상태에서 Game 뷰 초점을 잃었다가 복귀해도 자동 재시작하지 않는다. 초점 복귀 후 키 해제·새 R 입력으로 정상 재시작한다.
- 탈락 후 WASD·Space만 입력하며 10초를 기다려도 복귀하지 않는다.

동일 물리 판정의 낙사·R 입력 충돌과 낙사 경계 바로 위·동일·아래 조건은 아래 자동 시험으로 재현한다.

## 3. Unity Test Runner

1. `Window > General > Test Runner > PlayMode`를 연다.
2. `RestartTests`와 `RestartSceneTests`를 실행한다.
3. 기존 `MovementAndCameraTests`, `JumpTests`, `FallEliminationTests`도 실행하여 회귀가 없는지 확인한다.
4. 새 시험의 필수 검증:
   - 시작 위치·회전·선속도·각속도 초기화, 접촉 재획득, 이동·새 점프 허용.
   - 생존 R 무시, 유지·연타, 초점 전환, 점프 해제 요구, 유지 WASD 재개.
   - 낙사와 요청 충돌 시 탈락 우선, 이후 새 R 입력만 복귀.
   - 복귀 후 낙사 경계 위·동일·아래 각각 5회 판정, 탈락 중 조작 차단.
   - 같은 SampleScene 인스턴스에서 20회 복귀; 카메라 타깃·배치, 플레이어·카메라 개수 불변.
   - Restart 액션 누락 시 명확한 오류와 비활성화. 기존 설정 오류 검증 유지.
5. 안내·카메라 렌더 시험은 Game 뷰를 보이게 하고 수행한다. 기존 FallEliminationTests의 WaitForEndOfFrame/스크린샷 시험은 headless 배치 결과로 대체하지 않는다.

## 4. 배치 실행 — 비시각 RestartTests

열려 있는 Unity Editor를 종료한 뒤 저장소 루트에서 PowerShell로 실행한다. GUI 검증은 이 명령과 별도로 수행한다. 기록된 최종 실행은 아래 배치 명령이 아니라 열려 있는 Unity Editor의 MCP Test Runner로 수행했다.

```powershell
New-Item -ItemType Directory -Force 'D:\Projects\SkyBout\TestResults' | Out-Null
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe'
& $unity -batchmode -nographics `
  -projectPath 'D:\Projects\SkyBout' `
  -runTests -testPlatform PlayMode -testFilter RestartTests `
  -testResults 'D:\Projects\SkyBout\TestResults\restart.xml' `
  -logFile 'D:\Projects\SkyBout\TestResults\restart.log'
```

XML에 시험이 실제 수집·실행되어 모두 통과했는지 확인한다. 종료 코드만으로 성공을 판정하지 않는다. 렌더링을 요구하는 RestartSceneTests와 기존 시각 통합 시험은 Editor Test Runner에서 실행한다.

## 판정 기준

[spec.md](spec.md)의 SC-001~006을 모두 충족하고 기존 세 테스트 클래스가 통과해야 완료다. 안내 문자의 실제 가독성은 Game 뷰에서 확인하고, 물리 경계·요청 순서 검증은 가상 키보드를 사용하는 자동 시험 결과를 남긴다.
