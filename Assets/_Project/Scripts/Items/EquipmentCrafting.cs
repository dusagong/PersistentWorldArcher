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
            if (materialOne < 0 || materialTwo < 0 || (materialOne == 0 && materialTwo == 0))
                throw new ArgumentOutOfRangeException(nameof(materialOne), "제작 재료는 한 종류 이상 필요합니다.");
            var materials = new List<ItemAmount>();
            if (materialOne > 0) materials.Add(new ItemAmount(ItemCatalog.MaterialOne, materialOne));
            if (materialTwo > 0) materials.Add(new ItemAmount(ItemCatalog.MaterialTwo, materialTwo));
            Materials = materials.AsReadOnly();
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
            if (inventory == null) { reason = "인벤토리가 준비되지 않았습니다."; return false; }
            if (inventory.HasItem(resultId, 1) || (inventory.Owner != null && inventory.Owner.OwnsEquipment(resultId)))
            { reason = "이미 보유한 장비입니다."; return false; }
            return inventory.CanExchange(recipe.Materials, new[] { new ItemAmount(resultId, 1) }, out reason);
        }

        public static bool TryCraft(Inventory inventory, string resultId, out string reason)
        {
            if (!CanCraft(inventory, resultId, out reason)) return false;
            EquipmentRecipe recipe = Find(resultId);
            return inventory.TryExchange(recipe.Materials, new[] { new ItemAmount(resultId, 1) }, out reason);
        }

        private static List<EquipmentRecipe> BuildRecipes()
        {
            // Design requirement: every armor piece uses five MaterialOne only.
            var result = new List<EquipmentRecipe>();
            foreach (ItemDefinition item in ItemCatalog.All)
            {
                if (!item.IsEquipment || item.Slot == EquipmentSlot.Weapon) continue;
                result.Add(new EquipmentRecipe(item.Id, 5, 0));
            }
            return result;
        }
    }
}
