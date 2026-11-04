# 2026-11-04 main → cy 병합

## 범위와 복구 기준

- 사용자 승인: 최신 main과 cy의 두 기능을 보존해 병합한다. 추가 지시 **“ㄴㄴ cy에 합쳐”**에 따라 현재 cy에 병합하고 cy만 게시한다. main은 변경하지 않는다.
- 병합 전 cy: `67efa37dc55f058ba17806c9b1d319cbbf1812f3` (`HUD 배치 조정`).
- 받아온 main: `eabee71` (`Merge pull request #4 from dusagong/feature/boss-patterns`).
- 기존 미커밋 Effects meta 6개는 별도 백업했다. 이전 시안·레퍼런스와 PDF를 포함한 기존 로컬 파일 22개의 SHA-256을 기록하고 병합 후 일치를 확인했다. 이 파일들은 병합 커밋에 추가하지 않는다.
- 백업과 상세 검증 자료: `Library/Validation/MergeMain-20261104/`.

## 병합 내용

- main의 새 직업 캐릭터·스킬 이펙트, 보스 패턴·BossArena 변경을 받아왔다.
- cy의 HP·MP·스킬 HUD 배치, 실제 쿨타임·효과 시간 표시, MP 소모·즉시 초당 2 회복, 직업 변경 시 아이콘 갱신, 인벤토리 반응형·아이콘 표시를 보존했다.
- 직접 해결한 충돌은 `PlayerSkills.cs` 한 곳이다. `SpecialActiveDuration = specialEndsAt - Time.time` 기록을 남기고 main의 `ClassSheetEffect` 패링·궁수 특수 이펙트 분기를 연결했다. MP 소모와 HUD 쿨타임 기록도 유지한다.
- 자동 병합된 `ArcherController.cs`는 MP 초기화·회피 비용과 새 직업 애니메이션 갱신을 함께 유지한다. `InventoryWindow.cs`는 기존 반응형 가방·퀘스트 표시와 직업별 대기 이미지 선택을 함께 유지한다.
- 공통 수정 3개 파일을 별도로 비교했다. main만 수정한 55개 경로와 cy만 수정한 100개 경로는 각각 원본 Git blob과 일치한다. 임의 밸런스 변경이나 추가 리팩터링은 하지 않았다.

## 검증

- 작업 폴더의 미커밋 meta를 섞지 않도록 병합된 Git 인덱스를 기존 격리 Unity 복사본에 내보내 검증했다. 프로젝트 설정·패키지를 추가하지 않았다.
- Unity 6000.0.60f1 컴파일 및 Play Mode 실행. `SPLIT_HUD_OK checks=1246`: 9종 해상도의 최신 하단 HUD·중앙 정렬, 안내/툴팁 비겹침, 실제 HP·MP, 쿨타임·효과 분리, MP 소모·회복, 장착 제한과 메뉴 동작이 통과했다.
- `PERSISTENT_WORLD_VALIDATION_OK`: 기존 씬 구성·리소스·피해 규칙 검사 통과. 원본 Town·Field·BossArena를 직접 로드하고 세 씬의 누락 스크립트가 0임을 확인했다.
- main의 `ClassArtValidation.CheckDefinitions/Runtime` 실행. `CLASS_ART_VALIDATION_OK checks=34`: 3직업 달리기·회피 프레임, 대기 이미지와 새 효과 시트·애니메이션 수명 검사 통과.
- `MERGE_SKILL_INTEGRATION_OK checks=27`: 직업별 인벤토리 대기 이미지, 궁수 특수의 새 이펙트+30 MP+5초 효과/10초 쿨타임, 패링 이펙트+10 MP+0.7초, 실제 원형베기 이펙트+15 MP, 연속베기 이펙트+6/12/18 MP, 엑스칼리버 차징 취소 무소모·발사 시 30 MP, 힐러 평타 무료·정화·부활·힐링 존·심판의 새 이펙트와 기존 MP 정책을 확인했다.
- 검증용 `MergeSkillIntegrationProbe.cs`는 `Library/Validation/` 및 격리 복사본에만 있다. 테스트 준비에서 MP·쿨타임을 초기화한 것은 격리 검증 코드이며 실제 게임의 값과 초기화 동작을 변경하지 않는다.
- 실제 다른 PC·Windows Player 빌드·사람의 원본 씬 통합 플레이·보스 전 패턴의 동적 회귀까지 확인한 것은 아니다. 보스 코드와 씬은 main의 원본 blob을 그대로 보존했다.

직접 병합·검증했으며, 이번 병합의 게시 대상은 cy다. 이전 날짜의 UI 작업 기록은 수정하지 않는다.
