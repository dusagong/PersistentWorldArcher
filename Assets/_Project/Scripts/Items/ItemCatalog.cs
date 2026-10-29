using System;
using System.Collections.Generic;

namespace PersistentWorld.Items
{
    public enum PlayerClass { Warrior, Archer, Healer }
    public enum ItemCategory { Equipment, Consumable, Material, Quest }
    public enum EquipmentSlot { None, Weapon, Head, Chest, Legs, Hands, Feet }

    public readonly struct ItemAmount
    {
        public readonly string ItemId;
        public readonly int Quantity;
        public ItemAmount(string itemId, int quantity) { ItemId = itemId; Quantity = quantity; }
    }

    public sealed class ItemDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public ItemCategory Category { get; }
        public string IconPath { get; }
        public EquipmentSlot Slot { get; }
        public PlayerClass? RequiredClass { get; }
        public int Attack { get; }
        public int Defense { get; }
        public float MoveSpeed { get; }
        public string SetId { get; }
        public bool IsEquipment => Category == ItemCategory.Equipment;

        public ItemDefinition(string id, string name, ItemCategory category, string iconPath,
            EquipmentSlot slot = EquipmentSlot.None, PlayerClass? requiredClass = null,
            int attack = 0, int defense = 0, float moveSpeed = 0, string setId = null)
        {
            Id = id; Name = name; Category = category; IconPath = iconPath; Slot = slot;
            RequiredClass = requiredClass; Attack = attack; Defense = defense;
            MoveSpeed = moveSpeed; SetId = setId;
        }

        public bool CanEquip(PlayerClass playerClass) => IsEquipment &&
            (!RequiredClass.HasValue || RequiredClass.Value == playerClass);
    }

    public static class ItemCatalog
    {
        public const string MaterialOne = "material_1";
        public const string MaterialTwo = "material_2";
        public const string FieldSet = "field_set";
        private static readonly List<ItemDefinition> items = new List<ItemDefinition>
        {
            Gear("archer_hat", "궁수 모자", "01_Archer", EquipmentSlot.Head, PlayerClass.Archer, 1),
            Gear("healer_hat", "힐러 모자", "01_heal", EquipmentSlot.Head, PlayerClass.Healer, 1),
            Gear("warrior_helmet", "전사 투구", "01_Warrior", EquipmentSlot.Head, PlayerClass.Warrior, 2),
            Gear("chest", "상의", "02", EquipmentSlot.Chest, null, 1, 1),
            Gear("legs", "하의", "03", EquipmentSlot.Legs, null, 0, 1),
            Gear("gloves", "장갑", "04", EquipmentSlot.Hands, null, 1, 1),
            Gear("boots", "신발", "05", EquipmentSlot.Feet, null, 0, 0, 0.2f),
            Gear("greatsword", "양손검", "06_warrior", EquipmentSlot.Weapon, PlayerClass.Warrior, 4),
            Gear("bow", "활", "07_Archer", EquipmentSlot.Weapon, PlayerClass.Archer, 3),
            Gear("grimoire", "마도서", "07_Heal", EquipmentSlot.Weapon, PlayerClass.Healer, 2),
            new ItemDefinition(MaterialOne, "재료 1", ItemCategory.Material, "Items/Materials/material1"),
            new ItemDefinition(MaterialTwo, "재료 2", ItemCategory.Material, "Items/Materials/material12png"),
            new ItemDefinition("health_potion", "HP 회복약", ItemCategory.Consumable, "Items/Consumables/Health"),
            new ItemDefinition("mana_potion", "MP 회복약", ItemCategory.Consumable, "Items/Consumables/Mana")
        };
        private static readonly Dictionary<string, ItemDefinition> byId = BuildIndex();
        public static IReadOnlyList<ItemDefinition> All { get; } = items.AsReadOnly();

        public static bool TryGet(string id, out ItemDefinition item)
        {
            item = null;
            return id != null && byId.TryGetValue(id, out item);
        }

        public static string StartingWeapon(PlayerClass playerClass) => playerClass == PlayerClass.Warrior
            ? "greatsword" : playerClass == PlayerClass.Archer ? "bow" : "grimoire";

        private static ItemDefinition Gear(string id, string name, string icon, EquipmentSlot slot,
            PlayerClass? job, int attack, int defense = 0, float speed = 0) =>
            new ItemDefinition(id, name, ItemCategory.Equipment, "Items/Equipment/" + icon,
                slot, job, attack, defense, speed, FieldSet);

        private static Dictionary<string, ItemDefinition> BuildIndex()
        {
            var result = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            foreach (ItemDefinition item in items) result.Add(item.Id, item);
            return result;
        }
    }
}
