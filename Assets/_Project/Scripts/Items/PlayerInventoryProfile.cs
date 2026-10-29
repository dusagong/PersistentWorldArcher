using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PersistentWorld.Items
{
    public readonly struct EquipmentBonuses
    {
        public readonly int Attack;
        public readonly int Defense;
        public readonly float MoveSpeed;
        public readonly int SetPieces;
        public EquipmentBonuses(int attack, int defense, float speed, int setPieces)
        { Attack = attack; Defense = defense; MoveSpeed = speed; SetPieces = setPieces; }
    }

    public sealed class PlayerInventoryProfile
    {
        private readonly Dictionary<EquipmentSlot, string> equipped = new Dictionary<EquipmentSlot, string>();
        public PlayerClass Class { get; }
        public Inventory Inventory { get; }
        public event Action Changed;
        public IReadOnlyDictionary<EquipmentSlot, string> Equipped =>
            new ReadOnlyDictionary<EquipmentSlot, string>(equipped);

        public PlayerInventoryProfile(PlayerClass playerClass = PlayerClass.Archer,
            int capacity = PersistentWorld.Items.Inventory.DefaultCapacity,
            int stackLimit = PersistentWorld.Items.Inventory.DefaultStackLimit)
        {
            if (!Enum.IsDefined(typeof(PlayerClass), playerClass)) throw new ArgumentOutOfRangeException(nameof(playerClass));
            Class = playerClass;
            Inventory = new Inventory(capacity, stackLimit);
            Inventory.Changed += () => Changed?.Invoke();
        }

        public string GetEquipped(EquipmentSlot slot) => equipped.TryGetValue(slot, out string id) ? id : null;

        public bool TryEquip(string itemId, out string reason)
        {
            reason = string.Empty;
            if (!ItemCatalog.TryGet(itemId, out ItemDefinition item) || !item.CanEquip(Class))
            { reason = "현재 직업이 장착할 수 없는 장비입니다."; return false; }
            string previous = GetEquipped(item.Slot);
            if (previous == itemId) return true;
            if (!Inventory.HasItem(itemId, 1)) { reason = "보유하지 않은 장비입니다."; return false; }
            var returned = previous == null ? Array.Empty<ItemAmount>() : new[] { new ItemAmount(previous, 1) };
            // Update both sides before publishing Inventory.Changed (autosave sees a complete swap).
            equipped[item.Slot] = itemId;
            if (Inventory.TryExchange(new[] { new ItemAmount(itemId, 1) }, returned, out reason)) return true;
            if (previous == null) equipped.Remove(item.Slot);
            else equipped[item.Slot] = previous;
            return false;
        }

        public bool TryUnequip(EquipmentSlot slot, out string reason)
        {
            string previous = GetEquipped(slot);
            if (previous == null) { reason = "장착된 장비가 없습니다."; return false; }
            equipped.Remove(slot);
            if (Inventory.TryExchange(Array.Empty<ItemAmount>(), new[] { new ItemAmount(previous, 1) }, out reason)) return true;
            equipped[slot] = previous;
            return false;
        }

        public EquipmentBonuses GetBonuses()
        {
            int attack = 0, defense = 0, setPieces = 0;
            float speed = 0;
            foreach (string id in equipped.Values)
            {
                if (!ItemCatalog.TryGet(id, out ItemDefinition item)) continue;
                attack += item.Attack;
                defense += item.Defense;
                speed += item.MoveSpeed;
                if (item.SetId == ItemCatalog.FieldSet) setPieces++;
            }
            return new EquipmentBonuses(attack, defense, speed, setPieces);
        }

        internal bool RestoreEquipped(EquipmentSlot slot, string itemId)
        {
            if (!ItemCatalog.TryGet(itemId, out ItemDefinition item) || item.Slot != slot ||
                !item.CanEquip(Class) || equipped.ContainsKey(slot)) return false;
            equipped.Add(slot, itemId);
            return true;
        }
    }
}
