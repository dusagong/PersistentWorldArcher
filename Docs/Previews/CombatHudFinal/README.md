# 2026-11-02 HUD 확인 파일

Windows 탐색기에서 다음 폴더를 열고 `open-preview.html`을 더블클릭한다.

```text
C:\Users\이찬영\Documents\PersistentWorldArcher\Docs\Previews\CombatHudFinal
```

- `hud-1920x1080.png`, `hud-800x600.png` 등: 최신 중앙 고정 HUD의 해상도별 실제 렌더.
- `class-Archer-1920x1080.png`, `class-Warrior-1920x1080.png`, `class-Healer-1920x1080.png`: 스킬을 쓰지 않고 직업만 변경한 직후 화면.
- `cooldown-start.png`, `cooldown-half.png`: 중앙 숫자·테두리의 실제 쿨타임 진행.
- `archer-buff-tooltip.png`: 효과 활성·쿨타임 숫자·효과 막대가 함께 보이는 상태.
- `effect-ended-cooldown-continues.png`: 효과 막대는 사라지고 쿨타임 숫자만 남는 상태.
- `guides-all-1280x480.png` 등: 차징·F·대화 좌표를 같은 방식으로 계산하고 검증용 상자로 그려 겹침을 확인한 화면. 실제 게임 안내의 서체·문구를 캡처한 것은 아니다.
- `mp-spend-*`, `archer-buff-mouse-sequence.png`는 이전 우하단 디자인의 기능 기록이며 최신 외관 확인에는 사용하지 않는다.

전체 화면 PNG는 실제 Unity UI를 격리 배경에 렌더한 검증 화면이다. 실제 필드 배경·Town/Field 전환 플레이 캡처는 아니다. HTML 상세 보기 영역은 원본 PNG의 하단 중앙을 보여주며 원본 이미지를 수정하지 않는다.

HP·MP 바는 기준 240×8px, 라벨·수치는 11px다. 아이콘 기준 56×56px, 간격 12px이며 그림 표시 영역은 50×50px다. HUD 자체 배율은 해상도에 따라 1~1.5로 적용되므로 1280×720에서는 56px, 1920×1080과 4K에서는 84px 아이콘이다. 같은 해상도에서 UI Toolkit reference/fallback DPI 96/144/192를 바꿔도 좌표·크기가 유지되는 것을 검증했다. 실제 여러 PC·OS 배율에서 직접 플레이한 검증과는 구별한다.

MP는 최대 100에서 실제로 소비하며, 대기 없이 초당 2씩 회복한다. 효과 막대는 일석이조 5초·패링 약 0.7초의 실제 값을 읽으며 표시를 늘려 잡지 않는다.

자세한 승인·변경·검증 범위는 `Docs/UI_HUD_PLAN_2026-11-02.md`에 기록한다.
