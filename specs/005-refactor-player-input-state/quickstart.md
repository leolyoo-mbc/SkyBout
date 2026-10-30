# Quickstart: 플레이어 입력·상태 리팩토링 검증

## 현재 상태

2026-10-30: **설계만 완료**. 이번 단계에서 코드·씬·입력 자산은 수정하지 않았으며 컴파일·PlayMode·수동 시험은 실행하지 않았다. 아래는 구현 시 사용할 절차다. 과거 004의 66/66 통과 기록은 이번 실행 결과가 아니다.

계약: [contracts/player-input.md](contracts/player-input.md). 상태와 적용 순서: [data-model.md](data-model.md). 요구사항과 반복 횟수: [spec.md](spec.md)의 SC-001~007.

## 사전 조건

- `D:\Projects\SkyBout`를 Unity 6000.3.26f1으로 열고 기존 패키지 복원을 완료한다. 새 패키지·언어 버전 설정은 추가하지 않는다.
- 사용자 변경 `SkyBout.slnx`를 보존한다.
- 실제 Scene/Game 뷰와 Test Runner를 사용할 수 있어야 한다. 렌더 시험은 headless 결과로 대체하지 않는다.
- 실제 키보드와 가상 장치가 서로 간섭하지 않도록 테스트 소유 actions.devices를 제한하고 시험 종료 후 장치·씬·입력/물리 전역 설정을 복구한다.

## 1. 변경 전 기준선

1. `Window > General > Test Runner > PlayMode`에서 기존 다섯 클래스 전체를 실행한다: MovementAndCameraTests, JumpTests, FallEliminationTests, RestartTests, RestartSceneTests.
2. 실행 시각·Unity 버전·시험 수·통과/실패/건너뜀·로그·결과 경로를 기록한다. 실패가 있으면 원인과 단독 재실행 결과를 구분한다. 임계값을 낮춰 기준선을 만들지 않는다.
3. SampleScene의 Player/Rigidbody/PlayerMovement/카메라 설정과 inputactions의 GUID·액션/바인딩 ID를 보관하여 변경 후 대조한다.
4. 과거 초점 민감성·카메라 프레임 의존 기록과 미완료 수동 검수는 `../004-restart-after-elimination/quickstart.md` 및 tasks.md에서 확인한다.

## 2. 구현 후 임포트·씬 확인

1. `Assets/Player_Actions.inputactions`의 importer에서 Generate C# Class 활성, 클래스 PlayerActions, 출력 `Assets/Scripts/Input/PlayerActions.cs`, namespace 없음인지 확인하고 Apply/Reimport한다.
2. 생성 파일이 SkyBout.Runtime에서 컴파일되고 Console 오류가 없는지 확인한다. 생성 파일을 수동 수정하지 않는다.
3. SampleScene Player에 PlayerInput이 **없고**, PlayerMovement/Rigidbody/Collider/GroundCheck/카메라 참조가 유지되는지 확인한다. Missing Script와 누락 참조가 없어야 한다.
4. 생성 인스턴스와 원본 자산의 이름·ID·타입·바인딩·스킴·initialStateCheck 일치 시험을 실행한다. 불일치 음성 사례가 거부되는지도 확인한다.

PowerShell 정적 확인(저장소 루트 기준):

```powershell
Set-Location 'D:\Projects\SkyBout'
git diff --check
git status --short
Get-ChildItem 'Assets\Scripts' -Recurse -Filter '*.cs' |
  Select-String -Pattern '\bPlayerInput\b|\bInputValue\b|PlayerNotifications|FindAction\('
Get-ChildItem 'Assets\Tests\PlayMode' -Filter '*.cs' |
  Select-String -Pattern 'AddComponent<PlayerInput>|GetComponent<PlayerInput>|PlayerNotifications|SwitchCurrentControlScheme|ActivateInput|DeactivateInput'
Select-String -Path 'Assets\Scenes\SampleScene.unity' -Pattern 'UnityEngine.InputSystem.PlayerInput|2083166631'
```

런타임 검색은 0건, 이전 테스트 구성 호출과 씬 컴포넌트/참조도 0건이 기대값이다. PlayerInput 부재를 검증하는 테스트 자체의 타입 참조는 허용한다. 테스트의 물리/초점 호출용 SendMessage는 제거 대상이 아니다. 신규 생성 파일·폴더 meta와 원본 GUID 보존도 확인한다.

## 3. 자동 PlayMode 회귀

1. 기존 다섯 클래스와 신규 PlayerInputRefactorTests를 모두 실행한다. 기존 동작 검증을 삭제하거나 완화하지 않는다.
2. 생성 클래스 기반 fixture, Enum 관찰, 원본/생성 계약 검증으로 이행한 기존 시험도 실행한다. 단순 시험 개수가 아닌 요구사항별 검증 보존을 대조한다.
3. 빠른 Space/R press-release는 InputSystem 이벤트 갱신과 물리 갱신 사이에 재현한다. 유효 요청은 한 번 실행하고 공중/생존 등 무효 요청은 미래에 실행하지 않아야 한다.
4. 반대 이동 키 유지, 상태별 초점·비활성화 전환은 spec 횟수대로 확인한다. 활성화 전환 20회 후 새 입력이 한 번만 실행되어야 한다.
5. 기존 RestartSceneTests의 실제 씬 20회 반복에서 같은 플레이어·카메라 개수, 자세·속도·지지 재획득·유지 입력·카메라 스냅을 확인한다.
6. 첫 복귀 렌더의 안내 제거는 상태 조회뿐 아니라 실제 안내 영역의 화면 결과를 확인한다. 탈락 화면과 복귀 화면을 비교하되 배경의 어두운 부분을 안내로 오인하지 않게 한다.

### 비시각 배치 실행 예시

열려 있는 Editor를 먼저 종료한다. PowerShell에서 실행한다. 이 예시는 비시각 RestartTests만 실행하며 전체 검증을 대신하지 않는다.

```powershell
Set-Location 'D:\Projects\SkyBout'
New-Item -ItemType Directory -Force 'TestResults' | Out-Null
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe'
& $unity -batchmode -nographics `
  -projectPath 'D:\Projects\SkyBout' `
  -runTests -testPlatform PlayMode -testFilter RestartTests `
  -testResults 'D:\Projects\SkyBout\TestResults\input-refactor-restart.xml' `
  -logFile 'D:\Projects\SkyBout\TestResults\input-refactor-restart.log'
```

XML에서 시험이 실제 수집되고 실행되었는지 확인한다. 프로세스 종료 코드만으로 통과 판정하지 않는다. 전체 회귀와 시각 통합은 Editor Test Runner에서 수행한다.

## 4. 실제 Game 뷰 수동 검수

1. 기존 SampleScene에서 Game 뷰에 초점을 두고 WASD·기존 방향키의 8방향 이동, 정지, 방향 전환과 Space 점프·착지를 확인한다.
2. Space를 유지하거나 공중에서 반복 입력해도 추가 점프하지 않아야 한다. 섬 옆면은 착지가 아니어야 한다.
3. 자연 낙사 후 화면 밖에서도 탈락·R 안내가 보이고 한글이 작은 뷰에서 읽히는지 확인한다. 실제 뷰 크기와 잘림 여부를 기록한다.
4. 새 R로 복귀한 첫 화면에 안내가 사라지고 탑뷰가 시작 위치를 보여야 한다. 입력을 놓고 2초 안정성을 확인한다.
5. `이동 → 점프·착지 → 낙사 → 재시작`을 같은 실행에서 20회 수행한다. 반복마다 새 플레이어나 카메라가 생기지 않아야 한다.
6. 생존 R, 탈락 전 R 유지, 탈락 중 유지 Space/WASD, 초점 복귀·재활성화 경계를 각각 확인한다. spec의 정량 경계 시험은 자동 결과와 연결한다.
7. 사람의 가독성·조작감 확인은 자동 픽셀 검사로 대체하지 않는다. 접근할 수 없으면 미완료와 차단 사유를 기록한다.

## 5. 증거 기록 양식

| 구분 | 이번 실행 상태 | 남길 근거 |
| --- | --- | --- |
| 변경 전 전체 기준선 | 미실행 | 시각/버전/시험 수/XML·로그/기존 실패 |
| 생성 자산 계약·음성 사례·컴파일 | 미실행 | 원본 비교 결과/컴파일 로그 |
| PlayerInput 완전 제거·기존 설정 보존 | 미실행 | 정적 검색/씬 diff/설정·GUID 대조 |
| SC-001 이동·점프·카메라 | 미실행 | 8방향/점프 표본·높이/기존 시험 |
| SC-002 낙사 경계·조작 차단 | 미실행 | 경계 각 5회/10초 차단 5회 |
| SC-003 동일 실행 20회·안내/복귀 시간 | 미실행 | 20회 기록/첫 복귀 렌더 증거 |
| SC-004 복귀 안정·유지 입력 | 미실행 | 무입력 10회/유지 입력 5회 |
| SC-005 오재시작·충돌 방지 | 미실행 | 사례별 5회 |
| SC-006 초점·재활성화 단일 처리 | 미실행 | 상태별 5회/활성화 전환 20회 |
| SC-007 기존 기준 보존·빠른 입력·반대 키 | 미실행 | 기준선 대조/사례별 5회/미검증 목록 |
| 사람의 작은 Game 뷰 가독성·조작감 | 미실행 | 뷰 크기/검수자 확인/20회 조작 기록 |

004의 T015/T017/T018은 이번 설계에서 완료 표시하지 않는다. 동일 사항을 실제로 검증하면 이번 증거에 연결하고, 과거 완료 기록을 소급해서 바꾸지 않는다. 최종 판정은 적용 가능한 기존 인수 기준과 이번 SC-001~007을 만족하는지로 하며 미검증을 성공으로 합산하지 않는다.
