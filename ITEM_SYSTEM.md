# 인벤토리·장비 시스템 사용 및 연결

## 실행 흐름

Unity `6000.0.60f1`에서 Town을 Play한다. 시작 위치 왼쪽 아래 `(-1.5, -1.5)`에 임시 네모 대장장이가 실행 시 생성된다. 가까이에서 `F`를 누르면 제작 화면이 열리고, `ESC` 또는 닫기로 닫힌다. 제작 화면이 열린 동안 궁수의 이동·기본 공격·스킬 입력은 막힌다.

대장장이 화면은 현재 직업이 장착할 수 있는 장비 6개를 표시한다. 각 행에 받은 장비 아이콘, 능력치, 제작 재료, 보유 개수, 제작·장착·해제 버튼을 둔다.

`ESC → 장비 · 인벤토리`로 통합 창을 연다. 왼쪽은 기존 캐릭터 그림과 장비 6부위, 오른쪽은 장비·소모품·재료·퀘스트 탭과 40칸 가방이다. 왼쪽 스테이터스 창은 없다. 가방 아이템을 선택하면 아래에 이름·착용 부위·직업 제한·능력치가 표시되고 장비는 장착 버튼으로 착용한다. 장착 슬롯을 선택하면 해제할 수 있다. 다른 직업 장비와 같은 장비의 중복 장착은 버튼을 비활성화하고 이유를 표시한다. 해제 시 가방이 가득 차면 기존 장착 상태를 유지하고 사유를 표시한다.

분류 탭은 같은 40칸 가방을 종류별로 보여준다. 탭마다 별도 40칸을 제공하지 않는다. 재료·소모품은 최대 99개씩 나눠 표시하며, 장착 장비는 가방에서 제외한다. 회복약 사용 효과는 담당자 연결 범위라 여기서는 보유 정보만 표시한다. 별도 자동 정렬·자동 장착·드래그 기능은 추가하지 않았다.

`ESC`와 닫기는 게임으로 돌아간다. 제작 창과 인벤토리는 동시에 열리지 않고, 메뉴와 창이 열린 동안 궁수 이동·공격·스킬 입력을 막는다. 창을 닫는 클릭이 바로 공격으로 이어지지 않도록 닫은 프레임도 막는다. 게임 전체 시간을 정지시키지는 않는다. 캐릭터 사망이나 씬 이동으로 기존 플레이어가 없어지면 창을 닫는다. 스킬창 메뉴 연결은 스킬 담당과 별도 연결할 항목이다.

아이템은 자동 지급되지 않는다. 드롭 담당이 아래 획득 API를 연결하기 전에는 Editor Play 모드의 `Tools > Persistent World > Items > Give Test Materials (Play Mode)`로 각 재료를 99개씩 추가해 제작을 확인한다. 이 메뉴는 현재 로컬 프로필에 반영되고 자동 저장된다. 빌드에는 이 Editor 도구가 포함되지 않는다.

## 아이템 ID와 데이터

`ItemCatalog`에 장비 10개, 재료 2개, 회복약 2개가 등록되어 있다. 표시 이름과 이미지 파일명이 달라져도 저장 및 드롭 연결에는 안정적인 ID를 사용한다.

| 종류 | ID |
|---|---|
| 재료 1 | `material_1` (`ItemCatalog.MaterialOne`) |
| 재료 2 | `material_2` (`ItemCatalog.MaterialTwo`) |
| HP·MP 회복약 | `health_potion`, `mana_potion` |
| 직업별 모자 | `archer_hat`, `healer_hat`, `warrior_helmet` |
| 공용 방어구 | `chest`, `legs`, `gloves`, `boots` |
| 직업별 무기 | `greatsword`, `bow`, `grimoire` |

아이콘은 `Assets/_Project/Resources/Items/`의 제공 PNG를 사용한다. 추가 다운로드나 이미지 생성은 필요 없다.

## 드롭/획득 담당 연결

드롭 확률과 바닥 아이템 생성은 재현 담당이다. 획득이 확정될 때 찬영 Inventory에 ID와 개수를 전달한다.

```csharp
using PersistentWorld.Items;

Inventory inventory = ItemSession.Instance.Profile.Inventory;
bool accepted = inventory.AddItem(ItemCatalog.MaterialOne, 1);
// accepted가 true일 때만 해당 바닥 아이템의 획득을 완료한다.
// false이면 가방에 반영되지 않았으므로 바닥 아이템을 유지한다.
```

`GetCount(id)`는 보유 수량, `HasItem(id, quantity)`는 충분한지 검사, `RemoveItem(id, quantity)`는 차감한다. 반환된 보유 목록은 읽기 전용 스냅샷이다. 수량의 원본은 Inventory 하나다.

40칸 제한이며 장비는 개당 한 칸, 재료·소모품은 ID별로 99개마다 한 칸을 사용한다. 예를 들어 재료 100개는 두 칸이다. 장착한 장비는 별도 6개 슬롯으로 이동하므로 가방 칸에서 빠진다.

## 장비와 제작

```csharp
PlayerInventoryProfile profile = ItemSession.Instance.Profile;
bool crafted = EquipmentCrafting.TryCraft(profile.Inventory, "bow", out string reason);
bool equipped = profile.TryEquip("bow", out reason);
bool removed = profile.TryUnequip(EquipmentSlot.Weapon, out reason);
EquipmentBonuses stats = profile.GetBonuses();
```

전사·궁수·힐러의 무기와 모자는 직업 전용이고 나머지는 공용이다. 현재 실제 캐릭터 연결은 기존 궁수에 적용된다. 프로필과 장착 규칙 자체는 `PlayerClass` 세 종류를 지원하며, 다른 직업의 실제 컨트롤러가 추가되면 같은 프로필을 `PlayerEquipment.Bind(profile)`로 연결한다. 직업 선택/변경 화면은 구현하지 않는다.

장비 제작법은 `EquipmentCrafting`의 부위별 프로토타입 비용을 사용한다. 재료 검사·차감·결과 지급을 한 번의 데이터 교체로 처리한다. 재료가 줄면서 비는 칸도 최종 공간 검사에 반영하고, 결과를 받을 수 없으면 재료가 차감되지 않는다. 장비 교체도 인벤토리와 장착 상태를 모두 갱신한 후 변경 이벤트를 발행한다.

별도 아이템 조합, 재료 변환, 포션 제작 조합법은 사용자 결정으로 보류했다. 장비 제작법만 등록한다.

## 전투 담당 연결

장비 공격력은 기본 공격력에 한 번 더하고, 대상별 세트 배율은 피해를 실제 적용할 때 처리한다. 현재 궁수는 이 경로를 사용한다. 다른 직업 스킬도 원래 공격력/스킬 배율에 장비 공격력을 어떻게 반영할지 전투 담당과 맞춰 한 번만 계산한다.

```csharp
// sourceActor에는 실제 공격자 오브젝트를 전달한다.
targetHealth.TryDamage(finalAttackDamage, Faction.Player, sourceActor);
// 기존 Projectile.Spawn은 공격자의 장비/몬스터 정보를 생성 시 캡처한다.
```

기존 2인수 `TryDamage`도 호환을 위해 유지하지만 공격자 정보가 없어 세트 효과의 대상을 판정할 수 없다. 장비 효과를 반영하는 공격은 공격자 또는 `DamageContext`를 전달한다. 투사체는 발사자가 사라져도 생성 시점의 공격 장비와 보스 여부를 유지한다.

몬스터는 `MonsterIdentity.Configure(MonsterKind.Normal/Rare/Boss)`로 종류를 명시할 수 있다. 기존 보스 컨트롤러와 기존 Enemy/Rare Enemy 이름은 호환 경로로 판별한다. 새 몬스터는 이름에 의존하지 않고 종류를 지정한다.

3세트는 Normal만 +20%, 5세트는 Boss가 주는 피해 -20%, 6세트는 Boss에게 주는 피해 +30%다. 추천 방어 계산은 방어력 차감 후 보스 피해 감소를 적용하고 마지막에 반올림하며 최소 피해는 1이다. 능력치·방어 공식·반올림·제작 비용은 프로토타입 값이며 PDF 필수 수치와 구분한다.

## 저장

아이템 세션은 씬 간 유지된다. 보유/장착 상태 변경마다 `Application.persistentDataPath/inventory-v1.json`에 자동 저장하며, 종료·일시 중단 시에도 저장을 시도한다. 기존 파일 갱신 시 `.bak`으로 직전 저장을 남긴다. 임시 파일을 완성한 뒤 교체해 쓰는 도중 중단될 위험을 줄인다.

로드 시 형식 버전·아이템 ID·수량·칸 수·장착 직업/부위를 검사한다. 손상되거나 지원하지 않는 저장이면 원본을 보존하고 그 실행의 자동 저장을 중지하며 Console에 원인을 표시한다. 저장 실패는 제작 화면에도 표시한다. 서버 로그인/계정 저장은 구현하지 않는다.

## 검증 기록과 도구 위치

사용자 요청에 따라 검증 스크립트와 격리된 저장 데이터는 별도 프로젝트 복사본에만 둔다. 원본에 검증용 코드나 테스트 데이터를 추가하지 않는다. 원본의 `Tools > Persistent World > Items > Log Inventory (Play Mode)`는 현재 보유/장착 상태와 저장 경로를 Console에서 확인하는 개발 도구다. 재료 지급 메뉴는 앞서 설명한 대로 개발용으로 유지한다.

Unity `6000.0.60f1` 복사본에서 데이터·40칸·99개 중첩·제작 원자성·세 직업 장착 제한·세트 효과·저장 검사를 통과했다. Town 제작 → Field → BossArena → Town 흐름과 별도 프로세스의 저장 복원도 통과했다. 새 통합 UI는 40칸 배치, 기존 캐릭터 그림, 네 가지 탭, 버튼 이벤트를 통한 장착/해제, 다른 직업 장비 거부, 가방이 가득 찬 상태의 해제 거부, 창의 동시 열림 방지를 확인했다. 캡처를 열어 실제 배치도 확인했다.

로그 표식은 `INVENTORY_VALIDATION_OK`, `INVENTORY_UI_VALIDATION_OK`, `INVENTORY_PLAY_VALIDATION_OK`, `INVENTORY_RESTART_VALIDATION_OK`다. 이번 로그와 캡처는 임시 프로젝트 `PWA-UIValidation-69dcf294d3654c05a72128f8bb8f7a04/Logs`에 있다. 원본 저장 데이터는 사용하지 않았다.

이 결과는 실제 UI 이벤트와 상호작용 API를 사용하는 자동 검증이다. 실제 키보드 F/ESC와 마우스 조작을 통한 수동 플레이, Windows Player 빌드는 별도 확인 항목이다. 별도 아이템 조합과 스킬 담당 메뉴 연결은 남아 있으며 PDF 전체를 100% 완료했다고 표시하지 않는다. 시작 무기 지급·무기 강화는 이 저장 지점 다음 변경으로 분리한다.