# 협동 파티·전투 검증 — 2026-11-06

## 통과

- 파티 상태 모델 33개: `unity-party-model.log`, `PARTY_MODEL_VALIDATION_OK checks=33`.
- Windows 서버 + Windows 클라이언트 4개: `Library/Validation/Party-20261106-012023`, 통합 31개, 각 클라이언트 HP 전송 확인.
- Linux Dedicated Server Docker + Windows 클라이언트 4개: `Library/Validation/Party-20261106-012558`, 통합 31개, 각 클라이언트 HP 전송 확인. 실제 Town/보스 캐릭터와 배경을 카메라 캡처로 확인했다.
- Linux 컨테이너 + Windows 클라이언트 2개 이동/잘못된 코드 거부: `Library/Validation/Network-20261106-012417`.
- 기존 씬/전사 모션 회귀: `unity-cooperative-regression.log`, `PERSISTENT_WORLD_VALIDATION_OK`, `SWORD_SWING_VALIDATION_OK checks=46`.
- 푸시 전 원격 `85e9d10`의 사망 표시를 통합하고 텍스처 레지스트리를 68개로 갱신했다. 재빌드 후 Windows 4인 검사 `Party-20261106-012930`, Linux 4인 검사 `Party-20261106-013029` 모두 31개 통과했다.

통합 검사는 원본 20 피해 탄환을 패링해 100으로 바꾸고 실제 300 HP 표적을 200으로 만드는 것까지 검사한다. 무파티 입장을 거부하고 명시적으로 만든 1인 파티는 입장을 허용한다. 다른 파티의 입장 차단, 룸 반환, 필드 스킬 교체 거부, 실제 필드/보스 AI도 포함한다.

## 빌드 주의와 남은 확인

Linux 일반 Player의 헤드리스 실행은 초기화 중 네이티브 충돌이 발생했다. 전용 서버 타깃으로 변경하고 추가 Dedicated Server Optimizations를 끄면 정상 실행된다. 이 설정을 빌드 도구에 반영했다. 서버는 화면을 그리지 않고 클라이언트 표시용 Renderer 메타데이터를 읽는다. Null GPU의 미지원 셰이더 메시지는 남지만 상태 확인과 실제 4인 전투 검사를 통과했다.

모두 같은 PC의 다중 프로세스/로컬 Docker 검사다. 실제 인터넷에서 서로 다른 PC 4명의 지연·장시간 플레이는 추가 확인해야 한다. Railway 운영 재배포는 소스 푸시와 별도로 진행한다. 재배포 시 프로토콜 2 클라이언트를 팀원에게 함께 배포한다.
