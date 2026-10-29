using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PersistentWorld.Items
{
    public sealed class Inventory
    {
        public const int DefaultCapacity = 40;
        public const int DefaultStackLimit = 99;
        private Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        public int Capacity { get; }
        public int StackLimit { get; }
        public event Action Changed;
        public int UsedSlots => CountSlots(counts);
        // Return a snapshot so callers cannot modify the source of truth.
        public IReadOnlyDictionary<string, int> Counts =>
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(counts));

        public Inventory(int capacity = DefaultCapacity, int stackLimit = DefaultStackLimit)
        {
            if (capacity < 1 || stackLimit < 1) throw new ArgumentOutOfRangeException();
            Capacity = capacity;
            StackLimit = stackLimit;
        }

        public int GetCount(string itemId) => itemId != null && counts.TryGetValue(itemId, out int n) ? n : 0;
        public bool HasItem(string itemId, int quantity) => quantity > 0 && GetCount(itemId) >= quantity;
        public bool AddItem(string itemId, int quantity) =>
            TryExchange(Array.Empty<ItemAmount>(), new[] { new ItemAmount(itemId, quantity) }, out _);
        public bool RemoveItem(string itemId, int quantity) =>
            TryExchange(new[] { new ItemAmount(itemId, quantity) }, Array.Empty<ItemAmount>(), out _);

        public bool CanExchange(IReadOnlyList<ItemAmount> costs, IReadOnlyList<ItemAmount> rewards, out string reason) =>
            TryPrepare(costs, rewards, out _, out reason);

        public bool TryExchange(IReadOnlyList<ItemAmount> costs, IReadOnlyList<ItemAmount> rewards, out string reason)
        {
            if (!TryPrepare(costs, rewards, out var next, out reason)) return false;
            counts = next;
            Changed?.Invoke();
            return true;
        }

        private bool TryPrepare(IReadOnlyList<ItemAmount> costs, IReadOnlyList<ItemAmount> rewards,
            out Dictionary<string, int> next, out string reason)
        {
            next = new Dictionary<string, int>(counts, StringComparer.Ordinal);
            reason = string.Empty;
            if (!Aggregate(costs, out var required) || !Aggregate(rewards, out var granted))
            {
                reason = "아이템 또는 수량이 올바르지 않습니다.";
                return false;
            }
            foreach (var pair in required)
            {
                int have = GetCount(pair.Key);
                if (have < pair.Value) { reason = "재료 또는 보유 아이템이 부족합니다."; return false; }
                if (have == pair.Value) next.Remove(pair.Key);
                else next[pair.Key] = have - pair.Value;
            }
            foreach (var pair in granted)
            {
                next.TryGetValue(pair.Key, out int have);
                if ((long)have + pair.Value > int.MaxValue)
                { reason = "보유 수량의 한도를 초과합니다."; return false; }
                next[pair.Key] = have + pair.Value;
            }
            if (CountSlots(next) > Capacity)
            { reason = "인벤토리 공간이 부족합니다."; return false; }
            return true;
        }

        private static bool Aggregate(IReadOnlyList<ItemAmount> entries, out Dictionary<string, int> sums)
        {
            sums = new Dictionary<string, int>(StringComparer.Ordinal);
            if (entries == null) return false;
            foreach (ItemAmount entry in entries)
            {
                if (!ItemCatalog.TryGet(entry.ItemId, out _) || entry.Quantity < 1) return false;
                sums.TryGetValue(entry.ItemId, out int previous);
                if ((long)previous + entry.Quantity > int.MaxValue) return false;
                sums[entry.ItemId] = previous + entry.Quantity;
            }
            return true;
        }

        private int CountSlots(Dictionary<string, int> source)
        {
            long slots = 0;
            foreach (var pair in source)
            {
                ItemCatalog.TryGet(pair.Key, out ItemDefinition item);
                slots += item.IsEquipment ? pair.Value : ((long)pair.Value + StackLimit - 1) / StackLimit;
            }
            return (int)Math.Min(int.MaxValue, slots);
        }
    }
}
