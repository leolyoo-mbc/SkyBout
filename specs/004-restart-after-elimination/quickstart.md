# Quickstart: 탈락 후 재시작 검증

이 문서는 구현 후 실행할 검증 가이드다. 현재 계획 단계이므로 새 Restart 테스트와 재시작 동작은 아직 구현되지 않았다.

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
2. 구현에서 추가할 `RestartTests`와 `RestartSceneTests`를 실행한다.
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

열려 있는 Unity Editor를 종료한 뒤 저장소 루트에서 PowerShell로 실행한다. GUI 검증은 이 명령과 별도로 수행한다. 아래 테스트 클래스는 구현 단계에서 추가할 이름이다.

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
