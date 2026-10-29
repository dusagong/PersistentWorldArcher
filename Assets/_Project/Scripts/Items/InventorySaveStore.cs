using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PersistentWorld.Items
{
    public enum InventoryLoadResult { Missing, Loaded, Failed }

    public static class InventorySaveStore
    {
        [Serializable] private sealed class SavedItem { public string id; public int quantity; }
        [Serializable] private sealed class SavedEquipment { public int slot; public string id; }
        [Serializable] private sealed class SaveData
        {
            public int version;
            public int playerClass;
            public int weaponUpgradeLevel;
            public List<SavedItem> inventory;
            public List<SavedEquipment> equipment;
        }

        public static bool TrySave(string path, PlayerInventoryProfile profile, out string error)
        {
            error = string.Empty;
            if (profile == null)
            {
                error = "저장할 인벤토리가 아직 준비되지 않았습니다.";
                return false;
            }
            string temporary = null;
            try
            {
                var data = new SaveData
                {
                    version = 2, playerClass = (int)profile.Class, weaponUpgradeLevel = profile.WeaponUpgradeLevel,
                    inventory = new List<SavedItem>(), equipment = new List<SavedEquipment>()
                };
                foreach (var pair in profile.Inventory.Counts)
                    data.inventory.Add(new SavedItem { id = pair.Key, quantity = pair.Value });
                foreach (var pair in profile.Equipped)
                    data.equipment.Add(new SavedEquipment { slot = (int)pair.Key, id = pair.Value });
                string absolute = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(absolute));
                temporary = absolute + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
                if (File.Exists(absolute)) File.Replace(temporary, absolute, absolute + ".bak");
                else File.Move(temporary, absolute);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException ||
                exception is ArgumentException || exception is NotSupportedException)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                if (temporary != null && File.Exists(temporary))
                {
                    try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
        }

        public static InventoryLoadResult Load(string path, out PlayerInventoryProfile profile, out string error)
        {
            profile = null;
            error = string.Empty;
            if (!File.Exists(path)) return InventoryLoadResult.Missing;
            try
            {
                SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (data == null || (data.version != 1 && data.version != 2) || data.inventory == null || data.equipment == null ||
                    !Enum.IsDefined(typeof(PlayerClass), data.playerClass))
                    throw new InvalidDataException("지원하지 않거나 올바르지 않은 인벤토리 저장 형식입니다.");
                var restored = new PlayerInventoryProfile((PlayerClass)data.playerClass);
                var inputs = new List<ItemAmount>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (SavedItem item in data.inventory)
                {
                    if (item == null || item.id == null || !seen.Add(item.id))
                        throw new InvalidDataException("저장된 아이템 항목이 중복되거나 비어 있습니다.");
                    inputs.Add(new ItemAmount(item.id, item.quantity));
                }
                if (!restored.Inventory.TryExchange(Array.Empty<ItemAmount>(), inputs, out string reason))
                    throw new InvalidDataException(reason);
                foreach (SavedEquipment item in data.equipment)
                {
                    if (item == null || !restored.RestoreEquipped((EquipmentSlot)item.slot, item.id))
                        throw new InvalidDataException("저장된 장비의 직업 또는 부위가 올바르지 않습니다.");
                }
                if (!restored.RestoreWeaponUpgrade(data.version == 1 ? 0 : data.weaponUpgradeLevel))
                    throw new InvalidDataException("저장된 무기 강화 단계가 올바르지 않습니다.");
                if (data.version == 1) restored.EnsureStartingWeapon();
                profile = restored;
                return InventoryLoadResult.Loaded;
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException ||
                exception is ArgumentException || exception is NotSupportedException)
            {
                error = exception.Message;
                return InventoryLoadResult.Failed;
            }
        }
    }
}
