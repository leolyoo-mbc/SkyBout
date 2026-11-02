# Quickstart: 온라인 2인 방 검증

이 문서는 구현된 프로토타입의 검증 가이드다. 방 생성 실접속과 로컬 NGO 복제는 확인했지만 다른 회선의 두 기기 인수는 미검증이다. 동작 계약은 [contracts/online-room.md](contracts/online-room.md), 상태는 [data-model.md](data-model.md)를 참고한다.

## 현재 실행 방법

### 권장: 빌드 없이 Multiplayer Play Mode로 2인 테스트

1. `Assets/Scenes/SampleScene.unity`를 연다.
2. **Window > Multiplayer > Play Mode Scenarios**를 열고 **OnlineTwoPlayers**를 선택한다. 준비된 자산은 `Assets/Settings/OnlineTwoPlayers.asset`이며 메인 Editor와 Additional Editor 1개가 같은 SampleScene을 실행한다. 2.0.2의 설치 문서와 달리 현재 에디터의 실제 메뉴 경로는 위와 같다.
3. 상단 Play 버튼 옆 시나리오 선택에서 **OnlineTwoPlayers**를 선택하고 **▶**를 누른다. 일반 기본 Play가 아니라 이 시나리오를 실행한다. 추가 Editor가 처음 준비될 때는 시간이 걸릴 수 있다. Local Instance는 실행 파일 빌드가 필요하므로 여기서는 **Additional Editor**를 사용한다.
4. 메인 게임 창에서 **온라인 방 → 방 만들기 → 코드 복사**, Player 2 창에서 **온라인 방 → 코드 입력 → 코드로 참가**를 누른다.
5. 양쪽 인원 2/2와 각 화면의 두 캐릭터를 확인한다. 창을 번갈아 클릭해 자기 캐릭터만 WASD로 움직이고 상대 화면에도 반영되는지 확인한다. 창 포커스를 잃으면 글라이더가 접히는 현재 게임 규칙은 그대로 적용된다.

Multiplayer Play Mode 2.0.2는 현재 Unity 6000.3.26f1과 호환되는 설치 버전이다. 추가 Editor가 받는 `-name Player2` 등의 인수에서 인증 profile을 분리한다(`room-editor-player2`, `room-editor-player3` 등). 메인 Editor는 기본 `room-editor`를 유지한다. 개발용 `-roomProfile`을 명시하면 그 값이 우선하므로 두 인스턴스에 같은 override를 지정하지 않는다.

이 시나리오에서 두 Editor의 실제 MPS/Relay 코드 참가·2/2·플레이어 객체 2개·독립 이동 복제·정상 퇴장/방 삭제 정리를 검증했다. 가상 키보드 기반 자동 검증이며 사람의 화면 확인이나 다른 인터넷 회선 반복 인수를 뜻하지 않는다. 상세 로컬 기록은 `TestResults/online-room/multiplayer-playmode.md`다. 검증 후 시나리오는 정지했고 Play 설정은 OnlineTwoPlayers로 선택해 두었다. 임시 자동 접속/입력 주입 코드는 제거했다.

### 대안: Editor + Windows 실행 파일

- Unity에서 `Assets/Scenes/SampleScene.unity`를 실행하고 좌상단 **온라인 방**을 누른다. **방 만들기**로 생성한 코드를 다른 플레이어에게 전달하고, 상대는 **코드로 참가**를 선택한다.
- 입장 후 메뉴는 닫히며 자기 캐릭터만 조작한다. 좌상단 온라인 메뉴에서 코드 복사·방 나가기를 할 수 있다. 방장은 나가면 방이 종료된다.
- 기존 오프라인 플레이는 그대로 유지된다. 패널 입력 중 캐릭터 입력은 차단된다. 자기 캐릭터는 파란색, 상대는 주황색이다.
- 개발 빌드의 인증 profile은 기본 `room-player`, Editor는 `room-editor`다. 같은 PC에서 개발 빌드 두 개를 사용할 때 두 번째는 `SkyBout.exe -roomProfile room-player-2`처럼 다른 profile을 지정한다. 공개 일반 빌드는 이 개발 옵션을 사용하지 않는다.
- 생성한 Windows 개발 빌드는 `Builds/OnlineRoom/SkyBout.exe`다. Editor와 이 빌드를 사용하면 서로 다른 개발 인증 profile로 사전 접속 시험을 할 수 있다. 빌드 경고 1건은 기존 Unity Pipeline 도구의 런타임 설정이 없어 Player에서 해당 도구가 비활성화된다는 안내이며, 온라인 게임 기능의 컴파일 오류는 아니다.
- 실제 창 포커스를 유지해야 하는 씬 입력 테스트 중 다른 창을 클릭하면 의도된 포커스 해제 규칙으로 글라이더가 접혀 시험이 실패할 수 있다. 합격 기준을 완화하지 말고 Unity 창을 전면에 유지해 실행한다. 포커스 해제 자체는 별도 테스트에서 검증한다.

## 1. 준비

1. 현재 프로젝트 `D:\Projects\SkyBout`를 Unity `6000.3.26f1`로 연다. 새 Unity 프로젝트를 만들거나 에디터를 업그레이드하지 않는다.
2. Project Settings > Services에서 올바른 Unity Cloud 프로젝트를 확인한다. 초기 설계 시 Cloud ID는 비어 있었지만 구현 시 이미 연결된 것을 확인했고 이를 변경하지 않았다. 실제 익명 인증·Relay 방 생성은 성공했다. 다른 실행 환경도 같은 Cloud 프로젝트/환경을 사용하며 Relay 쿼터/비용은 사용자가 확인한다.
3. 구현 단계에서 MPS 2.4.1, NGO 2.13.3, Transport 2.7.4 설치 및 lockfile 생성을 확인한다. 컴파일 오류가 있으면 시험을 진행하지 않는다.
4. `Assets/Scenes/SampleScene.unity`를 열고 NetworkManager/UnityTransport, 등록된 NetworkPlayer prefab, 두 안전 스폰 지점, 온라인 UI 참조를 확인한다. 맵/프로토콜 버전이 양쪽 같아야 한다.
5. 일상적인 2인 개발 시험은 위 Multiplayer Play Mode 시나리오를 사용하며 빌드는 필요 없다. 배포/별도 회선 인수 때는 Windows Standalone 개발 빌드를 생성하고 Build Profiles에 기존 SampleScene이 활성화되어 있는지 확인한다. 두 PC는 별도 인터넷 회선을 사용한다. 같은 PC의 추가 Editor나 두 프로세스 시험은 해당 외부 회선 인수를 대신하지 않는다.
6. 각 프로세스에 서로 다른 익명 인증 ID를 사용한다. 같은 PC 시험은 서비스 초기화 전 별도 인증 profile을 선택하는 개발 옵션을 사용하고 토큰을 공유하지 않는다.

## 2. 자동 회귀

Editor의 Window > General > Test Runner에서 PlayMode 전체를 실행한다. 기존 Movement/Camera, Jump, FallElimination, Restart, InputReader, Glider, GlideHeight, IslandTraversal 시험을 유지한다. 온라인 테스트도 실행하되 서비스 없는 fake 시험과 실제 SDK/연결 시험 결과를 구분한다.

Editor를 닫은 자동화 환경에서는 설치된 Unity Editor 경로를 환경 변수에 설정한 뒤 PowerShell로 실행할 수 있다:

```powershell
& $env:UNITY_EDITOR_PATH -batchmode -projectPath 'D:\Projects\SkyBout' -runTests -testPlatform PlayMode -testResults 'D:\Projects\SkyBout\TestResults\online-room-playmode.xml' -logFile 'D:\Projects\SkyBout\TestResults\online-room-playmode.log'
```

`UNITY_EDITOR_PATH`는 `6000.3.26f1/Editor/Unity.exe`의 실제 경로로 지정한다. 같은 프로젝트를 연 Editor와 batchmode를 동시에 실행하지 않는다. 결과 XML의 테스트 수·실패·건너뜀과 콘솔 오류를 확인한다.

필수 자동 시험: 빈 코드/대소문자·공백, 요청 중복, 정원 경합, 타임아웃/늦은 성공 정리, 원격 입력 비활성, 후발 참가 상태, 재시작 자기 소유권, 종료 멱등성. fake 테스트만으로 Relay 실접속 완료를 주장하지 않는다.

## 3. 생성·참가·조작

1. A에서 온라인 패널 → 방 만들기. 코드와 1/2, 로컬 캐릭터 1개를 확인한다. 클릭부터 완료까지 시간을 잰다.
2. A를 시작 위치에 두고 B에서 코드 참가. 양쪽 2/2, 겹치지 않는 두 캐릭터와 서로 화면 안 표시를 확인한다. B의 참가 시간도 잰다.
3. A/B를 바꾸어 10회 반복한다. 각 생성/참가 15초 이하, 잘못된 성공·중복 캐릭터 0회가 목표다.
4. 별도 5회는 A가 다른 섬으로 이동한 뒤 B가 참가한다. A 위치는 유지하고 B는 출발 섬에서 시작하며 A의 현재 전개/탈락 상태를 올바르게 본다.
5. 각각 WASD, 점프, 공중 새 Space 유지 전개, 해제 접기를 10회씩 수행한다. RTT ≤100ms, 손실 ≤1% 조건을 측정·기록하며 양쪽 녹화로 상대 반영 지연을 비교한다. 95% 이상 0.5초 이내가 목표다.
6. 자기 카메라·입력·탈락 안내만 제어되는지 확인한다. 각자 낙사→R→이동/점프/글라이딩 5회씩 시험하고 상대 상태 초기화가 없는지 확인한다.

## 4. 오류·정원·퇴장

- 빈 코드, 없는 코드, 종료된 코드, 가득 찬 방, 인터넷 차단을 각각 3회. 요청 30초 이내 실패 안내와 기존 플레이어 보존을 확인한다. 빈 입력은 코드 입력 안내, 없는 코드/종료된 코드는 동일한 “코드가 잘못되었거나 종료된 방입니다”를 확인한다. 정리 완료 후 접속 재시도를 확인한다.
- 세 번째 실행 환경 C를 준비해 B/C가 마지막 자리에 동시에 참가하는 시험 5회. 한 명만 성공하고 하나의 플레이어만 스폰되어야 한다.
- B 정상 퇴장 및 B 프로세스 강제 종료를 각각 5회. A 1/2와 원격 캐릭터 제거, 서비스 자리 반환 후 같은 코드 재참가를 확인한다. 120초 서비스 기본 제거를 기다리는 구현은 불합격이다.
- A 정상 퇴장 및 A 프로세스 강제 종료를 각각 5회. B에게 종료 안내, 메뉴 복귀, 이전 코드로 게임 연결 성공 불가를 확인한다. 자동 호스트 승계로 게임이 남으면 불합격이다.
- 끊김 발생부터 확정까지 30초 이하, 확정 이후 인원·캐릭터·안내 반영 5초 이하를 잰다.
- 생성/참가 중 네트워크 지연으로 deadline을 넘긴 후 연결을 복구한다. 늦은 완료가 새 방/캐릭터를 만들지 않고 정리되며 재시도할 수 있어야 한다.
- 원격 정리가 끝나지 않는 상황을 별도로 3회 시험한다. 요청 후 30초 안에 실패를 표시하고, 실패 안내 후 추가 30초 안에 정리 대기를 복구 안내로 바꿔 연결 확인·복구 재시도·오프라인 복귀를 제공해야 한다. 각 복구 시도는 30초 안에 결과를 알린다. 미정리 중 새 접속은 차단하지만 로컬 정리 후 오프라인 복귀는 가능해야 한다. 서비스 복구 후 이전 작업/멤버십 정리를 확인해 정상 참가할 수 있는지 검증한다. 복귀 후 늦은 결과가 온라인 게임을 다시 시작하거나 원본 캐릭터를 건드리면 실패다.

## 5. 결과 기록

SC-001~007별 환경, 빌드/패키지 버전, RTT/손실, 시행 수, 성공 수, 시간 분포, 녹화/로그 경로, 실패 사유를 `TestResults` 하위 실행별 보고서에 기록한다. 참가 코드·인증 토큰 등은 공유 보고서에서 가린다. 두 시험자에게 안내만으로 생성→만남→퇴장→재참가를 완료하고 자기 캐릭터/방 상태를 구별할 수 있었는지 확인한다.

서비스 미설정, 외부 회선 부재, 미실행 항목은 **미검증**으로 적는다. 서비스/패키지/컴파일 또는 수용 기준 실패가 남으면 구현 완료·온라인 검증 완료로 보고하지 않는다.
