# Persistent World Archer

Unity 2D 협력 RPG의 공용 개발 기반이다. 첫 번째 세로 슬라이스는 궁수 한 명으로 마을, 필드 전투, 보스전을 순서대로 플레이하는 로컬 프로토타입이다. 아트와 최종 밸런스보다 입력, 판정, 씬 흐름과 팀 작업 규칙을 먼저 고정한다.

## 고정 환경

- Unity Editor `6000.0.60f1`
- 대상: Windows PC, 키보드와 마우스
- 외부 코드 편집기: VS Code
- 렌더링: Built-in 2D
- 시작 씬: `Assets/_Project/Scenes/Town.unity`

Unity Hub에서 이 폴더를 열면 필요한 씬이 이미 Build Settings에 등록되어 있다. 처음 열 때 Unity가 `Library`와 IDE 파일을 로컬에서 다시 생성한다.

## 현재 플레이 범위

- 마을: 대화 가능한 NPC와 필드 이동 게이트
- 필드: 궁수 이동, 기본 화살, 일반 몬스터, 보스 방 게이트
- 보스 방: 2페이즈 보스와 필드 복귀 게이트
- 궁수 스킬: Q 2단 발사, E 백스텝 대쉬, 우클릭 집중 사격 강화
- 공용 전투: 체력, 진영, 피해, 투사체, 사망 처리
- 플레이어 에셋: 사용자 제공 8프레임 스프라이트 시트, 이동 애니메이션과 좌우 반전
- 배경: Cainos Pixel Art Top Down - Basic 잔디/돌길 타일맵, 나무, 우물, 소품, 보스 제단
- NPC/적/포탈: 단색 임시 도형 유지

배경 에셋 설치 방법은 [ENVIRONMENT_SETUP.md](Assets/_Project/Editor/ENVIRONMENT_SETUP.md)를 참고하세요.
공개 저장소에는 Asset Store 원본 텍스처를 포함하지 않으므로 팀원도 해당 무료 에셋을 받아야 합니다.

## 조작

| 입력 | 동작 |
|---|---|
| WASD | 이동 |
| 마우스 | 조준 |
| 좌클릭 | 기본 화살 |
| Q | 2단 발사 |
| E | 조준 반대 방향 백스텝 |
| 우클릭 | 집중 사격 강화 |
| F | NPC 대화 또는 게이트 사용 |

## 프로젝트 구조

```text
Assets/_Project/
  Editor/       프로토타입 씬 생성 도구
  Scenes/       Town, Field, BossArena
  Scripts/
    Combat/     체력, 피해, 투사체
    Core/       상호작용과 임시 표시
    Player/     궁수 입력, 이동, 스킬
    World/      적, 보스, NPC, 게이트, 카메라, HUD
```

런타임 코드는 `PersistentWorld.Runtime` 어셈블리로 묶었다. 에디터 도구는 별도 어셈블리이므로 빌드에 포함되지 않는다.

## 팀 작업 규칙

1. `main`은 항상 실행 가능한 상태로 유지한다.
2. 작업 브랜치는 `feature/기능명`, 수정은 `fix/내용` 형식을 사용한다.
3. 한 씬을 여러 명이 동시에 수정하지 않는다. 공용 로직은 프리팹과 스크립트로 분리한다.
4. `.meta` 파일은 반드시 함께 커밋한다.
5. `Library`, `Temp`, `Logs`, IDE 생성 파일과 빌드는 커밋하지 않는다.
6. Pull Request에는 변경 범위, 확인 방법, 영향받는 씬을 적는다.

## 다음 결정 사항

온라인 지속 월드는 아직 구현하지 않았다. 네트워크 패키지와 서버 권한 구조를 팀에서 확정한 뒤 이동, 피해, 몬스터 상태, 리젠과 보상을 서버 권한으로 옮긴다. 장비, 최종 수치, 정식 에셋도 다음 콘텐츠 단계에서 추가한다.

씬을 다시 생성해야 할 때 Unity 메뉴에서 `Tools > Persistent World > Rebuild Prototype Scenes`를 실행한다. 이 명령은 세 프로토타입 씬을 덮어쓰므로 씬을 직접 편집한 뒤에는 실행하지 않는다.

