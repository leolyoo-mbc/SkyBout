# Issue tracker: GitHub

대상 저장소: leolyoo-mbc/SkyBout
이슈 작업에는 gh CLI를 사용한다.

- 등록: gh issue create --title "..." --body "..."
- 조회: gh issue view <번호> --json number,title,body,labels,comments
- 목록: gh issue list --state open
- 댓글: gh issue comment <번호> --body "..."
- 라벨 추가: gh issue edit <번호> --add-label "..."
- 라벨 제거: gh issue edit <번호> --remove-label "..."
- 종료: 먼저 이유를 댓글로 남기고 gh issue close <번호>를 실행한다.

등록 전 gh repo view --json nameWithOwner로 대상을 확인한다.
이 저장소의 작업을 스킬 원본 저장소에 등록하지 않는다.

작업 의존 관계는 GitHub 기본 이슈 의존 관계를 우선 사용한다.
지원하지 않으면 본문에 "Blocked by: #번호"로 기록한다.
하위 작업 연결을 지원하지 않으면 "Part of #번호"로 기록한다.

스킬이 명세나 작업을 게시하라고 하면 GitHub 이슈로 등록한다.

## Pull requests as a triage surface

**PRs as a request surface: no.**
