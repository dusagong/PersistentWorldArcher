using System;
using System.Collections.Generic;

namespace PersistentWorld.Items
{
    public sealed class EquipmentRecipe
    {
        public string ResultId { get; }
        public IReadOnlyList<ItemAmount> Materials { get; }
        public EquipmentRecipe(string resultId, int materialOne, int materialTwo)
        {
            ResultId = resultId;
            Materials = Array.AsReadOnly(new[]
            {
                new ItemAmount(ItemCatalog.MaterialOne, materialOne),
                new ItemAmount(ItemCatalog.MaterialTwo, materialTwo)
            });
        }
    }

    public static class EquipmentCrafting
    {
        private static readonly List<EquipmentRecipe> recipes = BuildRecipes();
        public static IReadOnlyList<EquipmentRecipe> All { get; } = recipes.AsReadOnly();

        public static EquipmentRecipe Find(string resultId) => recipes.Find(r => r.ResultId == resultId);
        public static bool CanCraft(Inventory inventory, string resultId, out string reason)
        {
            EquipmentRecipe recipe = Find(resultId);
            if (recipe == null) { reason = "장비 제작법이 없습니다."; return false; }
            return inventory.CanExchange(recipe.Materials, new[] { new ItemAmount(resultId, 1) }, out reason);
        }

        public static bool TryCraft(Inventory inventory, string resultId, out string reason)
        {
            EquipmentRecipe recipe = Find(resultId);
            if (recipe == null) { reason = "장비 제작법이 없습니다."; return false; }
            return inventory.TryExchange(recipe.Materials, new[] { new ItemAmount(resultId, 1) }, out reason);
        }

        private static List<EquipmentRecipe> BuildRecipes()
        {
            // Prototype material costs, tuned separately from the PDF's fixed equipment rules.
            var result = new List<EquipmentRecipe>();
            foreach (ItemDefinition item in ItemCatalog.All)
            {
                if (!item.IsEquipment) continue;
                int first, second;
                switch (item.Slot)
                {
                    case EquipmentSlot.Weapon: first = 8; second = 4; break;
                    case EquipmentSlot.Head: first = 4; second = 2; break;
                    case EquipmentSlot.Chest: first = 6; second = 3; break;
                    case EquipmentSlot.Legs: first = 4; second = 2; break;
                    case EquipmentSlot.Hands: first = 3; second = 2; break;
                    default: first = 3; second = 1; break;
                }
                result.Add(new EquipmentRecipe(item.Id, first, second));
            }
            return result;
        }
    }
}
