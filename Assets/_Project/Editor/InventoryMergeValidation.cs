using System;
using System.IO;
using PersistentWorld.Combat;
using PersistentWorld.Items;
using PersistentWorld.Player;
using PersistentWorld.UI;
using PersistentWorld.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace PersistentWorld.Editor
{
    public static class InventoryMergeValidation
    {
        private static int checks;
        private static void Require(bool result, string description)
        {
            if (!result) throw new InvalidOperationException("Inventory integration: " + description);
            checks++;
        }

        public static void RunPure()
        {
            checks = 0;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Validation"));
            Directory.CreateDirectory(folder);
            foreach (PlayerClass type in Enum.GetValues(typeof(PlayerClass)))
            {
                PlayerInventoryProfile profile = PlayerInventoryProfile.CreateNew(type);
                Require(profile.GetEquipped(EquipmentSlot.Weapon) == ItemCatalog.StartingWeapon(type) && profile.Inventory.UsedSlots == 0, "starting weapon " + type);
                Require(profile.Inventory.AddItem(ItemCatalog.MaterialOne, 99) && profile.Inventory.AddItem(ItemCatalog.MaterialTwo, 99), "test materials");
                foreach (ItemDefinition item in ItemCatalog.All)
                {
                    if (!item.CanEquip(type) || item.Slot == EquipmentSlot.Weapon) continue;
                    Require(EquipmentCrafting.TryCraft(profile.Inventory, item.Id, out _), "craft " + item.Id);
                    Require(!EquipmentCrafting.TryCraft(profile.Inventory, item.Id, out _) && profile.TryEquip(item.Id, out _), "duplicate guard and equip");
                    Require(!EquipmentCrafting.TryCraft(profile.Inventory, item.Id, out _), "equipped duplicate guard");
                }
                Require(profile.TryUpgradeWeapon(out _) && profile.TryUpgradeWeapon(out _) && profile.TryUpgradeWeapon(out _) && !profile.TryUpgradeWeapon(out _), "upgrade cap");
                Require(profile.Inventory.GetCount(ItemCatalog.MaterialOne) == 52 && profile.Inventory.GetCount(ItemCatalog.MaterialTwo) == 77, "exact armor and upgrade costs");
                EquipmentBonuses stats = profile.GetBonuses();
                Require(stats.SetPieces == 6 && stats.Defense == 3 && Mathf.Approximately(stats.MoveSpeed, .2f), "six-piece stats");
                int expectedAttack = type == PlayerClass.Warrior ? 11 : type == PlayerClass.Archer ? 9 : 8;
                Require(stats.Attack == expectedAttack, "full-set attack incl upgrade");
                string path = Path.Combine(folder, "review-" + type + "-" + Guid.NewGuid().ToString("N") + ".json");
                Require(InventorySaveStore.TrySave(path, profile, out _), "save");
                Require(InventorySaveStore.Load(path, out PlayerInventoryProfile restored, out _) == InventoryLoadResult.Loaded && restored.WeaponUpgradeLevel == 3 && restored.GetBonuses().Attack == expectedAttack && restored.Inventory.GetCount(ItemCatalog.MaterialOne) == 52, "roundtrip " + type);
                File.WriteAllText(path, "{ corrupt save");
                Require(InventorySaveStore.Load(path, out _, out _) == InventoryLoadResult.Failed && File.ReadAllText(path) == "{ corrupt save", "corrupt save preserved");
            }
            var full = PlayerInventoryProfile.CreateNew();
            Require(full.Inventory.AddItem("bow", 40) && !full.TryUnequip(EquipmentSlot.Weapon, out _) && full.GetEquipped(EquipmentSlot.Weapon) == "bow" && full.Inventory.UsedSlots == 40, "full bag unequip rollback");
            var stacks = new Inventory();
            Require(stacks.AddItem(ItemCatalog.MaterialOne, 100) && stacks.UsedSlots == 2, "99 stack boundary");
            var sixPieces = new EquipmentBonuses(0, 3, 0, 6);
            var attacker = new DamageContext(Faction.Player, equipment: sixPieces);
            Require(EquipmentDamage.Resolve(100, attacker, MonsterKind.Normal, default) == 120, "normal set bonus");
            Require(EquipmentDamage.Resolve(100, attacker, MonsterKind.Rare, default) == 100, "rare excludes normal set bonus");
            Require(EquipmentDamage.Resolve(100, attacker, MonsterKind.Boss, default) == 130, "boss attack set bonus");
            Require(EquipmentDamage.Resolve(100, new DamageContext(Faction.Enemy, MonsterKind.Boss), MonsterKind.None, sixPieces) == 78, "boss reduction after defense");
            Debug.Log("INVENTORY_MERGE_MODEL_OK checks=" + checks);
        }

        public static void CheckRuntime(ArcherController player)
        {
            ItemSession session = ItemSession.Instance;
            Require(session != null && session.SavePath.Contains("Validation") && session.CanSave, "isolated batch profile");
            foreach (CharacterClass type in Enum.GetValues(typeof(CharacterClass)))
            {
                player.Loadout.SelectClass(type);
                Require(session.Profile.Class == ItemSession.ToItemClass(type) && player.HasWeapon, "class profile mapped " + type);
            }
            player.Loadout.SelectClass(CharacterClass.Archer);
            PlayerInventoryProfile profile = session.Profile;
            Require(profile.TryUnequip(EquipmentSlot.Weapon, out _) && !player.TryBasicAttack(), "unarmed attack blocked");
            Require(profile.TryEquip("bow", out _), "re-equip weapon");
            profile.Inventory.AddItem(ItemCatalog.MaterialOne, 99);
            profile.Inventory.AddItem(ItemCatalog.MaterialTwo, 99);
            Require(profile.TryUpgradeWeapon(out _), "runtime upgrade");
            Projectile projectile = Projectile.Spawn(Vector2.zero, Vector2.right, 13, Faction.Player, player.gameObject, Color.white);
            BasicAttackEffect.AttachArrow(projectile);
            Require(projectile.GetComponent<TrailRenderer>() != null && projectile.UpgradeLevel == 1 && projectile.transform.GetChild(0).GetComponent<SpriteRenderer>().color == WeaponUpgradeVisuals.ColorFor(1), "upgrade tint reaches actual arrow sprite");
            UnityEngine.Object.Destroy(projectile.gameObject);
            player.Loadout.SelectClass(CharacterClass.Warrior);
            player.Loadout.SelectClass(CharacterClass.Archer);
            Require(session.Profile.WeaponUpgradeLevel == 1 && session.Profile.Inventory.GetCount(ItemCatalog.MaterialOne) == 94, "class switch preserves inventory and upgrades");
            foreach (ItemDefinition item in ItemCatalog.All)
                if (item.CanEquip(PlayerClass.Archer) && item.Slot != EquipmentSlot.Weapon)
                    Require(EquipmentCrafting.TryCraft(session.Profile.Inventory, item.Id, out _) && session.Profile.TryEquip(item.Id, out _), "runtime armor equip");
            GameObject normal = new GameObject("Inventory validation normal");
            normal.AddComponent<MonsterIdentity>().Configure(MonsterKind.Normal);
            Health normalHealth = normal.AddComponent<Health>(); normalHealth.Configure(Faction.Enemy, 100, false);
            GameObject boss = new GameObject("Inventory validation boss");
            boss.AddComponent<MonsterIdentity>().Configure(MonsterKind.Boss);
            Health bossHealth = boss.AddComponent<Health>(); bossHealth.Configure(Faction.Enemy, 100, false);
            Require(normalHealth.TryDamage(10, Faction.Player, player.gameObject) && normalHealth.Current == 88, "real health normal set damage");
            Require(bossHealth.TryDamage(10, Faction.Player, player.gameObject) && bossHealth.Current == 87, "real health boss set damage");
            Require(player.Health.TryDamage(20, Faction.Enemy, boss) && player.Health.Current == 86, "real defense and boss reduction");
            player.GetComponent<CombatStatus>().ApplyVulnerability();
            Require(player.Health.TryDamage(10, Faction.Enemy, boss) && player.Health.Current == 72, "debuff composes with equipment defense");
            player.GetComponent<CombatStatus>().Cleanse(); player.Health.Heal(100);
            UnityEngine.Object.Destroy(normal); UnityEngine.Object.Destroy(boss);
            InventoryWindow inventory = session.GetComponent<InventoryWindow>();
            player.SetMenuOpen(true);
            inventory.Open(player.gameObject);
            Require(InventoryWindow.IsOpen && !player.MenuOpen && !player.TryBasicAttack() && !player.TryBackstep(), "inventory blocks combat and closes skill menu");
            UIDocument inventoryDoc = null;
            foreach (UIDocument document in session.GetComponentsInChildren<UIDocument>())
                if (document.name == "Inventory UI") inventoryDoc = document;
            Require(inventoryDoc != null && inventoryDoc.rootVisualElement.Q("bag-grid").childCount == 40, "actual UI forty cells");
            Require(inventoryDoc.rootVisualElement.Q("equipment-weapon").Q<UpgradeItemIcon>() != null, "shared equipped icon renderer");
            inventory.Close();
            BlacksmithNpc npc = UnityEngine.Object.FindFirstObjectByType<BlacksmithNpc>();
            session.GetComponent<BlacksmithWindow>().Open(player.gameObject, npc.transform);
            Require(BlacksmithWindow.IsOpen && !player.TryBasicAttack(), "blacksmith blocks gameplay");
            session.GetComponent<BlacksmithWindow>().Close();
            Debug.Log("INVENTORY_MERGE_RUNTIME_OK checks=" + checks);
        }
    }
}
