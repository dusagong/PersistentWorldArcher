using PersistentWorld.Items;
using UnityEditor;
using UnityEngine;

namespace PersistentWorld.Editor
{
    public static class InventoryTools
    {
        private const string GrantMenu = "Tools/Persistent World/Items/Give Test Materials (Play Mode)";

        [MenuItem(GrantMenu)]
        public static void GiveTestMaterials()
        {
            ItemSession session = ItemSession.Instance;
            if (!Application.isPlaying || session == null) return;
            bool added = session.Profile.Inventory.TryExchange(System.Array.Empty<ItemAmount>(), new[]
            {
                new ItemAmount(ItemCatalog.MaterialOne, 99), new ItemAmount(ItemCatalog.MaterialTwo, 99)
            }, out string reason);
            Debug.Log(added ? "Added test materials to the current inventory (autosaved)." : reason);
        }

        [MenuItem(GrantMenu, true)]
        private static bool CanGiveTestMaterials() => Application.isPlaying && ItemSession.Instance != null;

        [MenuItem("Tools/Persistent World/Items/Log Inventory (Play Mode)")]
        public static void LogInventory()
        {
            if (!Application.isPlaying || ItemSession.Instance == null) return;
            PlayerInventoryProfile profile = ItemSession.Instance.Profile;
            Debug.Log($"Inventory {profile.Inventory.UsedSlots}/{profile.Inventory.Capacity}, class {profile.Class}");
            foreach (var pair in profile.Inventory.Counts) Debug.Log($"Owned {pair.Key}: {pair.Value}");
            foreach (var pair in profile.Equipped) Debug.Log($"Equipped {pair.Key}: {pair.Value}");
            Debug.Log("Save: " + ItemSession.Instance.SavePath);
        }
    }
}
