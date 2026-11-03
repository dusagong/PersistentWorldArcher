# 몬스터 에셋 연결 — 2026-11-03

원본: 사용자가 전달한 `MonsterAsset` 폴더(Monster01~03). 투명 배경 PNG 시트를 가로세로 절반(최근접 보간)으로 줄여 `Assets/_Project/Resources/Monsters`에 넣었다. 원본 3840×2160을 그대로 쓰면 2048 제한에서 셀 경계가 어긋나고 메모리도 커서, 절반 크기로 줄여 셀(320×180)이 정확히 나뉘게 했다. GIF와 512px 단일 그림은 참고용이라 넣지 않았다.

| 몬스터 | 프리팹 | 컨트롤러 | 이동 | 대기 | 공격 (준비 / 공격 / 회복 프레임) |
|---|---|---|---|---|---|
| Monster01 초록 슬라임 | DashMonster | 돌진 | Monster01Move 6×6 | 이동 반복 | Monster01Attack 6×6: 0–17 / 18–23 / 24–35 |
| Monster02 가방 멘 초록 몬스터 | NormalMonster2 | 원거리 | Monster02Move 4×3 | Monster02Idle 4×3 | Monster02Attack 6×6: 0–17 / 18–35 (던진 뒤 0.5초) |
| Monster03 흰 고양이 | NormalMonster1, RareMonster | 점프 | Monster03Move 6×6 | 이동 반복 | Monster03Attack 6×6: 0–7 / 8–23 / 24–35 |

- `MonsterSpriteAnimator`가 시트를 잘라 재생한다. 각 컨트롤러는 `IMonsterAnimationSource`로 현재 단계(이동·준비·공격·회복)와 진행도, 바라보는 방향을 알려 주고, 공격 시트 구간을 그 진행도에 맞춰 넘긴다. 판정·타이밍은 바꾸지 않았다.
- 이동·대기는 80ms 간격이다. 모든 프레임을 시트 전체 알파 경계로 같은 크기로 잘라 위치가 흔들리지 않는다. 앞뒤 정렬은 프리팹에 있던 WorldDepthSort가 맡는다.
- 크기: `height`(스케일 1일 때 몸 높이, 현재 0.75) × 프리팹 스케일. 비균등 스케일(NormalMonster2 0.6×0.9)은 큰 축으로 균등하게 바꿔 원형 콜라이더 크기를 유지한다.
- 프리팹 SpriteRenderer에는 이동 시트 첫 프레임을 잘라 만든 `Monster0NPreview` 스프라이트를 넣어 편집 화면·썸네일에서도 보인다. 실행하면 애니메이터가 시트 프레임으로 바꾼다.
- 레어는 NormalMonster1 변형이라 그림을 물려받고 `tint`만 금색(1, 0.82, 0.45)으로 덮어쓴다.
- 점프 몬스터는 그림이 움츠림·도약을 표현하므로 기존 도형용 늘리기·누르기를 끈다. 준비 동작의 붉은 색조는 유지한다.
- 피격 번쩍임은 그림 모양 흰 실루엣으로, 사망 잔상은 네모 대신 그림 모양으로 남는다.

현재 Field 씬의 적은 예전 `EnemyController` 도형이며, 이 프리팹들은 `Assets/_Sandbox/MonsterTest`에만 배치되어 있다. 정식 씬 배치는 별도 작업이다.
