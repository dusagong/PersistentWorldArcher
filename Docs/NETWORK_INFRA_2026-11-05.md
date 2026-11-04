# Railway 인프라 준비 및 검증 — 2026-11-05

## 완료 범위

- NGO 2.4.4/Transport 2.6.0 설치, 패키지 잠금 파일과 asmdef 참조 반영.
- 기존 세 게임 씬/Build Settings를 보존한 별도 네트워크 씬, 플레이어 프리팹, 참가 화면.
- 기본 정원 4명, 비공개 참가 코드, 버전 확인, 소유자 입력과 서버 판정 이동.
- 공식 서명된 Unity 6000.0.60f1 Linux Dedicated Server 모듈 설치 및 Linux 서버 빌드.
- Ubuntu + Caddy + Unity 서버 Docker 구성. Railway의 TLS 종료 뒤 WebSocket 프록시와 /health 분기.
- 서버 준비/메인 스레드 heartbeat 기반 readiness, 자식 프로세스 종료 감시.
- Volume 소유권 설정 후 UID 10001로 실행. 실제 게임 저장 기능은 별도다.
- PowerShell 빌드·업로드 준비·배포·두 클라이언트 smoke test 명령 제공.

## 검증 결과

| 검사 | 결과 |
|---|---|
| Windows 실행 파일 빌드 | 통과 |
| Windows 서버 + 별도 클라이언트 2개 | 각 클라이언트에서 자신과 상대의 움직이는 위치 복제 확인 |
| 잘못된 참가 코드 | 연결 거부 확인 |
| Linux Dedicated Server 빌드 | 통과 |
| Docker 이미지 생성 | 통과 |
| Windows 클라이언트 2개 → Caddy → Linux 서버 | 양쪽 `PWA_TWO_CLIENTS_PROBE_OK players=2` |
| Docker 프록시를 통한 잘못된 코드 | `PWA_REJECTION_PROBE_OK` |
| 공개 포트 역할의 /health | HTTP 200, ready=true, players=0, protocol=1 |
| 일반 사용자 /data 쓰기 | `PWA_VOLUME_WRITE_OK` |
| Unity 종료 → 컨테이너 종료 | `PWA_PROCESS_SUPERVISION_OK`, exit 1 |
| PowerShell 스크립트 구문/railway.json | 통과 |

유휴 상태에서 프레임 제한 전 CPU 약 201%가 관찰되어 서버를 30 FPS로 제한했다.
최종 컨테이너의 단일 측정은 CPU 약 1.17%, 메모리 약 81.87 MiB다.
개발 PC Docker 측정이며 실제 서비스 부하/예상 요금이나 보장 성능을 뜻하지 않는다.

원본 로그는 `Library/Validation/Network-20261105-023355/`와
`Library/Validation/RailwayContainer/container-final.log`에 있다. 개인 게임 저장은 사용하지 않았다.
헤드리스 실행 시 그래픽 장치가 없어 출력되는 shader 경고가 있으며, 접속/복제 검사에서 런타임 예외는 발견되지 않았다.
테스트 컨테이너는 종료·삭제했고 재사용 가능한 이미지 `pwa-network-test:local`은 로컬에 남겼다.

## 최초 온라인 배포 보류 사유 (해결됨)

Railway CLI 로그인을 완료했지만 프로젝트 생성 요청이 다음 메시지로 거부됐다:

> Your trial has expired. Please select a plan to continue using Railway.

당시 에이전트가 플랜을 선택하거나 결제를 실행하지 않았다. 이후 사용자가 플랜을 활성화하고 배포를 요청해 아래처럼 완료했다.
GitHub에는 이번 인프라 변경을 아직 푸시하지 않았다.

## 실제 Railway 배포 완료

- 프로젝트: `PersistentWorldArcher` / `47a1c1c3-6eb7-44d1-a4dd-ba1d478437a4`
- 서비스: `pwa-game-server` / `e777e04e-8bde-4329-a652-1f5b0ba76c44`
- 환경: `production` / `eb3146d0-42d8-4eff-b76f-3bd1165344fe`
- 최종 활성 배포: `a8ff8c62-0a4c-4e06-867a-db2ec7d0863d`, 상태 SUCCESS.
- 도메인: `wss://pwa-game-server-production.up.railway.app`
- HTTPS 상태 확인: https://pwa-game-server-production.up.railway.app/health
- 단일 동남아 리전 `asia-southeast1-eqsg3a`, 정원 4명, 자동 절전 해제, /data Volume 연결.
- 비공개 코드는 Railway `PWA_JOIN_CODE`와 무시된 로컬 `Deploy/.local/join-code.txt`에 보관. 값은 문서/로그/Git에 기록하지 않는다.
- 다음 배포: `./Deploy/Railway/Publish.ps1`. 기본 프로젝트/환경/서비스를 명시해 다른 프로젝트와 혼동하지 않게 했다.

최초 외부 접속에서는 Unity 6.0/NGO 2.4의 네이티브 DNS 제한과 Transport의 IP 기반 Host 헤더 때문에 실패했다.
동일한 인증서 검증을 사용하는 수동 WebSocket 요청으로 도메인 Host의 101 응답과 IP Host의 404 응답을 비교해 확인했다.
클라이언트에서 DNS를 비동기로 조회하고, TLS의 원래 도메인을 HTTP Host에 유지하도록 공식 Transport 2.6.0을 임베드했다.
원본 두 소스 파일만 수정했고 LICENSE 및 저작권 표시를 유지했다. 인증서 검증은 끄지 않았다.
수정 목록은 `Packages/com.unity.transport/PWA-PATCH.md`에 기록했다.

빌드된 샌드박스가 공통 인벤토리를 초기화하며 두 프로세스가 같은 로컬 저장을 사용하던 문제도 발견했다.
해당 씬에서는 인벤토리 부팅을 생략하고, 모든 플랫폼의 배치 검증 저장을 격리했다.
수정 후 외부/로컬 검사에서는 인벤토리 저장 충돌 로그가 발생하지 않았다.

최종 배포 확인 후 실제 공개 WSS 주소로 별도 Windows 클라이언트 두 개를 실행했다.
자신/상대의 이동 복제와 잘못된 코드 거부가 모두 통과했다.
로그: `Library/Validation/Network-20261105-025223/`.
수정된 패키지의 로컬 WS 회귀도 `Network-20261105-025117/`에서 통과했다.
이 검증은 같은 PC의 서로 다른 프로세스가 인터넷을 통해 Railway에 접속한 것이며, 서로 다른 PC 두 대 검증은 아니다.

## 남은 검증과 게임 연결

실제 Railway WSS/TLS 경로는 확인했다. 서로 다른 두 PC, 지연과 끊김, 최대 정원/재접속은 추가 확인이 필요하다.
스킬/전투/몬스터/사망/부활/씬 이동/개인별 저장은 네트워크 샌드박스와 아직 연결하지 않았다.

참고: [Railway 배포 구성](https://docs.railway.com/config-as-code/reference),
[Railway health check](https://docs.railway.com/deployments/healthchecks),
[Unity Dedicated Server 빌드](https://docs.unity3d.com/kr/6000.0/Manual/dedicated-server-build.html).
