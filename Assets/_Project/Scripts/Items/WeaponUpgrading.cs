using System;
using System.Collections.Generic;

namespace PersistentWorld.Items
{
    public static class WeaponUpgrading
    {
        public const int MaximumLevel = 3;
        private static readonly IReadOnlyList<ItemAmount>[] costs =
        {
            Array.AsReadOnly(new[] { new ItemAmount(ItemCatalog.MaterialOne, 5), new ItemAmount(ItemCatalog.MaterialTwo, 5) }),
            Array.AsReadOnly(new[] { new ItemAmount(ItemCatalog.MaterialOne, 7), new ItemAmount(ItemCatalog.MaterialTwo, 7) }),
            Array.AsReadOnly(new[] { new ItemAmount(ItemCatalog.MaterialOne, 10), new ItemAmount(ItemCatalog.MaterialTwo, 10) })
        };

        public static IReadOnlyList<ItemAmount> MaterialsFor(int targetLevel)
        {
            if (targetLevel < 1 || targetLevel > MaximumLevel) throw new ArgumentOutOfRangeException(nameof(targetLevel));
            return costs[targetLevel - 1];
        }
    }
}
