# 전 직업 에셋 연결 — 2026-11-03

원본: 사용자가 전달한 `Chracter_Asset.zip`. PNG를 그대로 복사하고 Unity에서 프레임을 분할한다. GIF는 프레임 수·80ms 지연 확인용이다. `motivate`, `reference`, `Original` 중 중복 참고 그림은 플레이 시트로 오인하지 않고 유지했다. 기존 궁수 일반 공격·이동·회피·스킬 에셋은 보존하고 새 PNG 17개를 추가했다. 원본 ZIP은 사용자 파일 위치에 유지하며, 압축 해제 검토본과 분석용 이미지는 Git에서 제외되는 Library 폴더에 둔다.

궁수 기존 파일 8개를 SHA-256으로 대조해 평타·이동·회피·스킬1/2·저격 시전 시트 7개가 같은 파일임을 확인했다. 저격 비행 시트만 압축 안의 최신 원본으로 갱신했으며 3×3 격자·좌향·원래 GUID를 유지한다.

## 캐릭터

| 직업 | 이동 원본 | Unity 격자(열×행)·프레임 | 회피 원본 | 격자·프레임 |
|---|---|---|---|---|
| 궁수 | 기존 ArcherRun 유지 | 4×2·8 | 기존 ArcherDash 유지 | 4×3·12 |
| 전사 | Warrior/Move/move_04_rm_1x11.png | 11×1·11 | Warrior/dash/02_rm__3x4.png | 4×3·12 |
| 힐러 | heal/Move/02_rm_3x4.png | 4×3·12 | heal/dash/02_composed_4x6.png | 6×4·24 |

파일명의 숫자는 행×열 순서다. 전사 이동 7040×360, 힐러 회피 3840×1440을 원본 해상도로 임포트한다. 캐릭터 크기는 기존 궁수 크기에 맞추며 실제 Collider와 이동/회피 거리는 바꾸지 않는다. 회피 이미지 전체 재생은 기존 0.6초로 통일하고 이동 FPS는 컴포넌트 설정을 따른다. 이동은 알파 경계를 읽어 분할, 회피는 회전 중 흔들리지 않도록 셀 전체와 공통 피벗을 사용한다.

전사·힐러 Original의 투명 대기 이미지는 각각 WarriorInventoryIdle/HealerInventoryIdle로 복사해 인벤토리 초상화에 연결했다. 기존 ArcherInventoryIdle도 유지한다.

## 이펙트

아래는 Resources/Effects 이름이며 모두 80ms 간격이다.

| 이름 | 원본 경로(압축 내부) | 열×행 | 연결 |
|---|---|---|---|
| ArcherSpecial | Archer/Skill/Unipue_skill/05_rm_3x3.png | 3×3 | 일석이조 5초 반복, 몸 중심 추적·캐릭터 뒤 렌더링 |
| WarriorWhirlwind | Warrior/Skill/Skill01/01_rm_4x4.png | 4×4 | 원형베기 준비 후 한 번 |
| WarriorCombo | Warrior/Skill/Skill02/03_rm_2x4.png | 4×2 | 연속베기 각 타격, 단계별 크기 증가 |
| WarriorCharge | Warrior/Skill/skill03_1/01_rm_3x4.png | 4×3 | 엑스칼리버 차징 동안 반복 |
| WarriorRelease | Warrior/Skill/skill03_2/01_rm_2x2.png | 2×2 | 차징 해제 후 한 번 |
| WarriorParry | Warrior/Skill/Unipue_skill/01_composed_2x2.png | 2×2 | 패링 0.7초 반복, 조준 추적 |
| HealerBolt | heal/Attack/01_rm_1x3.png | 3×1 | 실제 평타 투사체를 따라 반복 |
| HealerHealingZone | heal/Skill/skill1/02_rm_2x4.png | 4×2 | 바닥에 고정, 5초 반복 |
| HealerCleanse | heal/Skill/skill2/01_rm_4x4.png | 4×4 | 디버프 해제 시 한 번 |
| HealerJudgment | heal/Skill/skill3/01_rm_3x4.png | 4×3 | 누르는 동안 반복, 조준·벽까지 거리 추적 |
| HealerRevive | heal/Skill/UniqueSkill/3aa8b6ac-e45b-40a7-b20a-f4abaab5018a_composed_4x4-color-removed.png | 4×4 | 실제 부활 성공 대상에 한 번 |

평타 검은 앞서 연결한 `Items/Equipment/06_warrior`를 재사용한다. 새 파일과 SHA-256이 동일하며 원본 아이콘을 수정하지 않는다. 이펙트 재생은 추가 피해를 생성하지 않고 실제 공격·치유·부활은 기존 판정을 사용한다. 차징/레이저 취소 시 연출도 정리한다. 버프 연출은 메뉴를 열어도 남아 있고 실제 종료·직업 변경·사망 시 제거한다.

## 검증

`PersistentWorld.Editor.ClassGameplayValidation.RunBatch`에서 기존 전투·장비·사망 검사와 함께 세 직업 이동·회피 전체 프레임, 원본 크기·초상화, 모든 이펙트 프레임·반복·수명 및 실제 스킬 이미지 연결을 검사한다. 궁수 버프 전용 검사는 `ArcherPresentationValidation.RunBatch`, 검 휘두르기는 `SwordSwingValidation.RunBatch`다.

통합 검증 결과: 에셋 55항목, 전투·스킬·사망 67항목, 장비·인벤토리 100항목을 통과했고 런타임 예외가 없는 것을 확인했다.
