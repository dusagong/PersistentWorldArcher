# Railway 멀티플레이 인프라

협동 빌드는 Town/Field/Field2/BossArena/Tutorial과 실제 전투·파티를 실행한다. [협동 플레이 안내](../../Docs/COOPERATIVE_PLAY.md)와 [피드백별 체크리스트](../../Docs/FEEDBACK_CHECKLIST_2026-11-13.md)를 참고한다.
Netcode for GameObjects 2.4.4 + Unity Transport 2.6.0을 Unity 6000.0.60f1에 고정했다.

## 현재 서버

- 접속 주소: `wss://pwa-game-server-production.up.railway.app`
- 상태 확인: https://pwa-game-server-production.up.railway.app/health
- 관리: https://railway.com/project/47a1c1c3-6eb7-44d1-a4dd-ba1d478437a4
- 서비스: `pwa-game-server`, 환경: `production`, 정원: 4명.
- 단일 동남아 리전, 자동 절전 해제, `/data` Volume, 실패 시 재시작 최대 5회.
- 참가 코드는 Git에서 제외된 `Deploy/.local/join-code.txt` 및 Railway 변수 `PWA_JOIN_CODE`에 있다. 팀 내부에만 전달한다.
- 로컬 설정 `Deploy/.local/server.json`에는 주소와 프로젝트/서비스 ID만 들어 있다.

Unity 6.0 클라이언트는 DNS를 먼저 조회한다. 인증서 검증과 SNI에는 원래 도메인을 유지한다.
Transport 원본의 네이티브 WebSocket Host 헤더가 IP를 사용하여 Railway에서 404가 발생하므로,
`Packages/com.unity.transport`에 공식 2.6.0을 임베드하고 Host 헤더만 TLS 도메인을 따르도록 보정했다.
수정 내역은 패키지의 `PWA-PATCH.md`에 있다. Library 캐시를 직접 수정하지 않는다.
샌드박스는 오프라인 인벤토리를 초기화하지 않으며, 배치 실행의 저장 파일도 별도로 격리한다.

## 구성

클라이언트 WSS → Railway HTTPS/TLS → Caddy(`PORT`, 기본 8080) → Unity WebSocket(7777).
`GET /health`는 Caddy를 거쳐 서버의 loopback 상태 포트 8081로 전달된다.
Unity가 접속을 받고 메인 스레드 heartbeat가 정상일 때만 200을 반환한다.
컨테이너는 Caddy 또는 Unity 중 하나가 종료되면 함께 종료되어 Railway가 재시작한다.

- 기본 정원 4명. 입력은 각 소유자만 전송하고 위치는 서버가 속도/범위를 제한해 계산한다.
- 참가 코드 및 프로토콜 확인 후 플레이어를 생성한다. 공개 계정 인증을 대신하는 코드는 아니며 팀 테스트용이다.
- 외부 접속에는 `wss://`를 사용하고 인증서는 OS 신뢰 저장소로 검증한다. 인증서 검증을 끄지 않는다.
- 배포용 실행 파일은 Linux Dedicated Server/Mono 빌드다. Railway에서 Unity Editor를 실행하지 않는다.
- 재배포 시 연결이 잠시 끊긴다. 클라이언트가 같은 코드로 자동 재접속한다. 재배포·서버 재시작을 넘는 데모 진행 복구는 보장하지 않는다.

## 최초 1회

1. Railway 계정의 활성 플랜/크레딧을 확인한다. 2026-11-05 사용자가 플랜을 활성화해 배포를 진행했다.
2. `npx @railway/cli login`으로 로그인한다.
3. 전용 프로젝트 `PersistentWorldArcher`와 빈 서비스 `pwa-game-server`를 만든다. 다른 과목/기존 서비스에 배포하지 않는다.
4. 서비스 변수를 설정한다. 비밀 값은 CLI `variable set --stdin` 또는 Railway 대시보드에서 입력하고 Git에 저장하지 않는다.

| 변수 | 값 |
|---|---|
| PORT | 8080 |
| PWA_GAME_PORT | 7777 |
| PWA_HEALTH_PORT | 8081 |
| PWA_MAX_PLAYERS | 4 |
| PWA_JOIN_CODE | 팀에만 전달할 무작위 12~64자 코드 |
| PWA_DATA_DIR | /data |

5. Volume을 `/data`에 연결한다. 컨테이너는 볼륨 루트 소유자를 실행 사용자로 맞춘 뒤 일반 사용자로 Unity/Caddy를 실행한다.
6. 자동 절전(Serverless)은 끄고 복제본 1개로 운영한다. 가까운 배포 리전을 선택한다.
7. 배포 완료 후 Public Networking에서 도메인을 만들고 대상 포트를 8080으로 지정한다.
8. `https://생성된도메인/health`에서 `ready: true`를 확인한다.

영구 플레이어의 직업별 장비·인벤토리·강화·스킬·포션 장착과 재사용 대기시간은 `/data/characters-v1`에 저장한다. 기존 Volume을 유지하며 배포한다. 필드 드롭·몬스터 위치·진행 중인 보스전은 서버 재시작 시 초기화한다. 데모 계정은 디스크에 저장하지 않는다.

## 빌드와 배포

Unity Hub에 현재 버전의 **Linux Dedicated Server Build Support**가 필요하다.
이 PC에는 공식 서명된 Unity 서버 모듈을 설치했다. Unity Editor는 빌드 전에 닫는다.

```powershell
# 프로젝트 루트에서 실행. Linux 서버 빌드, 업로드용 폴더 준비, Railway 업로드.
./Deploy/Railway/Publish.ps1

# 다른 프로젝트로 배포할 때만 ID를 명시한다.
# ./Deploy/Railway/Publish.ps1 -ProjectId '다른 프로젝트 ID'
```

Unity 빌드가 이미 최신이면 `-SkipBuild`를 지정한다. 실제 배포 성공 여부는 Railway에서 확인한다.
배포 대상은 **Builds/Railway**만이다. Unity 소스/개인 파일을 업로드하지 않는다.
GitHub에 소스만 푸시하는 것으로 자동 Unity 빌드가 되지 않는다. GitHub Actions 자동화는 후속 작업이다.

## 클라이언트

Unity 메뉴 `Tools > Persistent World > Multiplayer > Build Cooperative Windows Client`로 빌드한다.
`Builds/CooperativeWindows/PwaCoop.exe` 실행 → 서버 주소 `wss://생성된도메인` → 참가 코드 → 접속.
피드백 반영 빌드는 서버와 클라이언트 모두 **프로토콜 6**이다. 서버 업데이트 시 팀원에게 새 Windows 폴더 전체를 배포한다.

새 플레이어는 닉네임과 팀 참가 코드를 입력하고 직업을 선택한다. ESC 메뉴의 플레이어 코드 복사로 복구 코드를 보관한다.
다른 PC에서는 동일한 서버의 플레이어 코드로 로그인한다. 팀 참가 코드와 개인 플레이어 코드는 별개다.
데모 계정은 디스크에 저장하지 않으며 명시적으로 로그아웃하면 삭제한다. 전송 끊김의 자동 재접속만 90초 동안 메모리에서 복구한다.

## 검증

```powershell
# Windows 테스트 빌드가 있는 상태에서: 별도 서버 + 클라이언트 2개 + 잘못된 코드 거부
./Deploy/Test-Network.ps1

# 배포된 서버 검증. PWA_JOIN_CODE를 현재 프로세스 환경에 안전하게 설정한 뒤 실행.
./Deploy/Test-Network.ps1 -Endpoint 'wss://생성된도메인'
```

검증 로그는 `Library/Validation/Network-*`에 보관한다. 게임 플레이어의 개인 저장 파일을 사용하지 않는다.
Windows 두 프로세스 접속 통과는 실제 두 PC/인터넷/지연 환경 검증을 대신하지 않는다.

## 저장 및 배포 설정

캐릭터별 저장은 Railway 볼륨의 `/data/characters-v1`에 기록된다. 재배포할 때 기존 볼륨을 연결한다.

설정 파일은 배포 시의 값일 뿐 대시보드 설정을 수정하지 않는다. 배포 목록의 실제 `serviceManifest`에서 싱가포르 단일 복제본, 절전 해제, `/health`, 재시작 정책과 `/data` 마운트를 확인한다. 새 서비스의 기본 리전은 운영 서비스와 다를 수 있다. [Railway 설정 문서](https://docs.railway.com/config-as-code/reference), [리전 문서](https://docs.railway.com/deployments/regions).

협동 서버는 시각 상태 전송에 Renderer 메타데이터를 사용하므로 추가 Dedicated Server Optimizations를 끄고 빌드한다. 클라이언트 화면을 서버에서 렌더링하지 않는다.
