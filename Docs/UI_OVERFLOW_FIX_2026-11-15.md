# UI 표시 네 항목 수정 — 2026-11-15

## 승인 범위와 수정 내용

1. 툴팁 글자 넘침: 수동 텍스트 측정으로 높이를 고정하던 코드를 제거했다. UI Toolkit 자동 너비·높이 및 최대 너비로 실제 줄바꿈에 맞춰 배치한다. GeometryChangedEvent에서 실제 박스 크기로 위치를 다시 보정하고, 텍스트가 박스 밖에 그려지지 않도록 overflow를 제한한다.
2. 퀘스트 탭에서 장착 영역 확대: 하단 상세·메시지 영역을 display:none으로 제거하지 않고 visibility:hidden으로 가린다. 탭 전환 때 해당 영역의 배치 공간을 유지한다.
3. 대장장이 닫기 버튼: 폭 116 / 높이 40 기준, 줄바꿈 금지 및 세로 패딩으로 '닫기 (ESC)' 한 줄 공간을 확보한다.
4. 연습장 안내 여백: 고정 높이 250을 제거하고 현재 글꼴·너비로 CalcHeight를 계산한다. 힐러 안내도 같은 상자 안에 포함하고, 기존 초기화·종료 버튼을 상자 바로 아래로 배치한다. 포인터 차단 영역은 실제 표시 영역과 맞춘다.

## 변경 파일

- `Assets/_Project/Scripts/UI/TooltipLayout.cs`
- `Assets/_Project/Scripts/UI/InventoryWindow.cs`
- `Assets/_Project/Scripts/UI/CombatHud.cs`
- `Assets/_Project/Resources/UI/BlacksmithStyles.uss`
- `Assets/_Project/Scripts/Networking/NetworkPartyHud.cs`

장착·해제·드래그·제작·강화·저장·로그인·전투 수치와 기존 버튼 명령은 변경하지 않았다. 추가 기능이나 다른 UI 재설계는 하지 않았다.

## 확인 / 미완료 / 사용자 할 일

- 런타임 C# 어셈블리를 Unity 참조·컴파일 설정으로 컴파일했다. 결과는 `Library/Validation/UiOverflow-20261115/compile.txt`에 보관한다.
- `git diff --check`를 수행했다. 긴 플레이 검증·Windows 빌드는 실행하지 않았다.
- 실제 화면에서 첫 hover/다른 설명으로 이동, 퀘스트 전환, 대장장이 닫기 버튼, 직업별 연습장 안내 표시 재확인은 미완료다. 코드 컴파일이 실제 표시 검증을 의미하지 않는다.
- Unity 재컴파일 후 해당 네 화면만 확인하면 된다. 이전 exe에는 다시 빌드해야 적용된다.
- 수정 전 파일도 같은 검증 폴더에 보관했다. 커밋·푸시는 하지 않았다.
- 라우팅: 별도 위임 없이 현재 담당 모델이 제한된 표시 코드만 직접 수정했다.
