실제 PC 온라인 RPG HUD 레퍼런스
검색·확인: 2026-11-02

폴더:
C:\Users\이찬영\Documents\PersistentWorldArcher\Docs\Previews\HudReferences

먼저 열 파일: reference-index.html
원본 이미지도 각각 더블클릭해서 열 수 있습니다.
원본을 자르거나 임의로 다시 배치한 시안이 아닙니다.

[우선 참고: 메이플스토리]
파일: maplestory-official-full.png
출처: https://maplestory.nexon.com/Guide/N23GameInformation/Articles/440
넥슨 공식 인터페이스 가이드의 전체 화면입니다.
하단 중앙에는 HP·MP 두 줄, 우하단에는 작은 정사각 퀵슬롯이 정렬돼 있습니다.
2D 캐릭터·인벤토리 중심 PC MMORPG라는 점과 HP/MP 분리 요구에 잘 맞는 참고입니다.
횡스크롤 시점 자체를 우리 게임에 적용하자는 뜻은 아닙니다.

[시점·전투 참고: 로스트아크]
파일: lostark-player-training.png
출처: https://www.inven.co.kr/board/lostark/5558/61052
인벤 게시물에 첨부된 실제 수련장 영상 캡처입니다. 공식 이미지는 아닙니다.
HP·자원과 스킬을 하단 중앙의 작은 전투 HUD에 모읍니다.
스킬 아이콘의 기준선과 간격이 일정하며 화면 중앙의 전투 시야를 확보합니다.
2023년 게시물 이미지이므로 최신 패치의 세부 UI를 보증하는 자료는 아닙니다.

[PC MMORPG 기본형 비교: 파이널판타지14]
파일: ffxiv-official-pc.jpg
출처: https://na.finalfantasyxiv.com/game_manual/view/
Square Enix 공식 매뉴얼의 HUD 설명 이미지입니다.
하단 중앙의 정렬된 핫바와 그 아래 HP·MP 파라미터 바를 확인할 수 있습니다.
많은 스킬 줄이나 공식 그림의 색상을 그대로 추가하자는 제안은 아닙니다.

[추가 비교: 던전앤파이터 전투 집중 모드]
파일: dfo-official-battle-focus.png
출처: https://www.dfoneople.com/news/updates/4890/Character-and-Systems/System
Neople 공식 안내 이미지입니다. 정통 MMORPG 대표로 분류하려는 목적은 아닙니다.
전투 집중 모드는 구형 HP/MP 구슬을 막대로 바꿉니다.
이 이미지에서는 HP는 하단 중앙, MP는 그 오른쪽에 나뉘어 있습니다.

[우리 게임에 적용할 배치 제안 — 아직 미승인·미구현]
메이플스토리의 'HP/MP 중앙 + 작은 정렬 퀵슬롯 우하단' 구조를 우선 참고합니다.
1280x720 기준 초안 치수 제안:
- HP/MP: 하단 중앙, 폭 약 180px, 빨강 HP와 파랑 MP를 위아래로 배치.
- 스킬: 우하단, 24x24px 정사각 아이콘 5개를 가로 일렬로 정렬.
- 간격: 4px, 아이콘 행 전체 너비 136px. 작은 단축키만 아래에 표시.
- '준비', 남은 초, 스킬 이름 상시 표시는 제거.
- 쿨타임: 요청대로 테두리가 채워지는 방식. 해당 게임의 숫자 표시를 그대로 따오지 않음.
- 큰 배경 판, 엇갈린 배치, 흩어진 원형 버튼은 사용하지 않음.

위 치수는 우리 게임용 제안이며 원본 게임에서 측정한 공식 규격이 아닙니다.
게임 수치·스킬 동작·MP 코어·아이템 표시 코드는 이번 조사에서 변경하지 않았습니다.
이전 HudLayoutReview 시안은 사용자 검토에서 거절됐으며 채택안이 아닙니다.
