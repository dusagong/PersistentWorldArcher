using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PersistentWorld.Combat;
using PersistentWorld.Core;
using PersistentWorld.Items;
using PersistentWorld.Player;
using PersistentWorld.UI;
using PersistentWorld.World;
using UnityEngine;

namespace PersistentWorld.Networking
{
    public enum ItemCommand : byte { Equip, Unequip, Craft, Upgrade, Use }

    public sealed partial class NetworkGameWorld
    {
        [Serializable] private sealed class CharacterSave
        {
            public int version = 1;
            public string[] inventories;
            public SkillLoadout.OnlineState skills;
        }

        private sealed class Character
        {
            public readonly PlayerInventoryProfile[] Inventories = new PlayerInventoryProfile[3];
            private readonly string[] encoded = new string[3];
            public string Path;
            public bool Dirty;
            public SkillLoadout.OnlineState Skills;
            public PlayerInventoryProfile For(CharacterClass type)
            {
                int index = (int)type;
                if (Inventories[index] != null) return Inventories[index];
                PlayerInventoryProfile profile = PlayerInventoryProfile.CreateNew(ItemSession.ToItemClass(type));
                SetProfile(index, profile);
                return profile;
            }
            public void SetProfile(int index, PlayerInventoryProfile profile)
            {
                Inventories[index] = profile;
                profile.Changed += () => { Dirty = true; encoded[index] = null; };
            }
            public string Encode(CharacterClass type) => encoded[(int)type] ??= InventorySaveStore.Encode(For(type));
        }

        private readonly Dictionary<ulong, Character> characters = new Dictionary<ulong, Character>();
        private readonly Dictionary<string, Character> pendingSaves = new Dictionary<string, Character>();
        private string characterDirectory;
        private float nextSave;
        private GameObject onlineBlacksmith;
        private readonly Dictionary<ulong, int> smithRequests = new Dictionary<ulong, int>();
        private string clientInventoryData;
        private int clientSmithRequest;
        private Transform fieldTemplates;
        public PlayerInventoryProfile ClientInventory { get; private set; }
        public event Action ClientInventoryChanged;
        public bool CanUseSmith => Local is PlayerFrame local && local.hp > 0 && local.area == (int)GameArea.Town &&
            Vector2.Distance(local.position, onlineBlacksmith.transform.position) <= 2.5f;

        internal static string HashKey(string key)
        {
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "").ToLowerInvariant();
        }

        private void PrepareProgress(bool server)
        {
            onlineBlacksmith = new GameObject("Blacksmith");
            onlineBlacksmith.transform.SetParent(maps[(int)GameArea.Town], false);
            onlineBlacksmith.transform.position = new Vector3(-1.5f, -1.5f, 0);
            onlineBlacksmith.AddComponent<PlaceholderVisual>().Configure(new Color(.7f, .45f, .25f), new Vector2(.8f, 1.1f));
            if (server)
            {
                string root = Environment.GetEnvironmentVariable("PWA_DATA_DIR");
                if (string.IsNullOrEmpty(root))
                    root = NetworkSandbox.HasArgument("-pwa-party-validation") || NetworkSandbox.HasArgument("-pwa-network-validation")
                        ? Path.Combine(Application.temporaryCachePath, "online-test-" + Guid.NewGuid().ToString("N"))
                        : Application.persistentDataPath;
                characterDirectory = Path.Combine(root, "characters-v1");
            }
            else if (!Application.isBatchMode)
            {
                gameObject.AddComponent<InventoryWindow>();
                gameObject.AddComponent<BlacksmithWindow>();
            }
        }

        internal bool ReserveCharacter(ulong id, string key, out string reason)
        {
            reason = "캐릭터 키가 올바르지 않습니다.";
            if (!Guid.TryParseExact(key, "N", out Guid token) || token == Guid.Empty) return false;
            string path = Path.Combine(characterDirectory, HashKey(token.ToString("N")) + ".json");
            foreach (Character active in characters.Values)
            {
                if (active.Path != path) continue;
                reason = "이미 이 캐릭터가 접속 중입니다.";
                return false;
            }

            if (pendingSaves.TryGetValue(path, out Character pending))
            {
                pendingSaves.Remove(path);
                characters.Add(id, pending);
                return true;
            }
            try
            {
                var character = new Character { Path = path };
                if (File.Exists(path))
                {
                    var save = JsonUtility.FromJson<CharacterSave>(File.ReadAllText(path));
                    if (save == null || save.version != 1 || save.inventories == null || save.inventories.Length != 3 ||
                        save.skills == null || !Enum.IsDefined(typeof(CharacterClass), save.skills.selected))
                        throw new InvalidDataException("Invalid online character record.");
                    character.Skills = save.skills;
                    for (int i = 0; i < save.inventories.Length; i++)
                    {
                        if (string.IsNullOrEmpty(save.inventories[i])) continue;
                        PlayerInventoryProfile profile = InventorySaveStore.Decode(save.inventories[i]);
                        if (profile.Class != ItemSession.ToItemClass((CharacterClass)i)) throw new InvalidDataException("Invalid saved class.");
                        character.SetProfile(i, profile);
                    }
                }
                characters.Add(id, character);
                return true;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                Debug.LogWarning("Online character could not be loaded: " + error.GetType().Name);
                reason = "캐릭터 저장을 읽지 못했습니다. 기존 저장은 보존됩니다.";
                return false;
            }
        }

        private void BindCharacter(ulong id, ArcherController actor)
        {
            Character character = characters[id];
            if (character.Skills != null) actor.Loadout.ImportOnline(character.Skills);
            actor.GetComponent<PlayerEquipment>().Bind(character.For(actor.Loadout.Class));
            actor.Loadout.Changed += () =>
            {
                character.Skills = actor.Loadout.ExportOnline();
                character.Dirty = true;
            };
            character.Skills = actor.Loadout.ExportOnline();
        }

        private static bool SaveCharacter(Character character)
        {
            if (!character.Dirty) return true;
            var save = new CharacterSave { skills = character.Skills, inventories = new string[3] };
            for (int i = 0; i < save.inventories.Length; i++)
                if (character.Inventories[i] != null) save.inventories[i] = InventorySaveStore.Encode(character.Inventories[i]);
            if (!InventorySaveStore.TryWrite(character.Path, JsonUtility.ToJson(save), out string error))
            {
                Debug.LogWarning("Online character save failed: " + error);
                return false;
            }
            character.Dirty = false;
            return true;
        }

        internal void ReleaseCharacter(ulong id)
        {
            smithRequests.Remove(id);
            if (characters.Remove(id, out Character character) && !SaveCharacter(character))
                pendingSaves[character.Path] = character;
        }

        private void SaveProgress()
        {
            foreach (Character character in characters.Values) SaveCharacter(character);
            if (pendingSaves.Count == 0) return;
            var saved = new List<string>();
            foreach (var pending in pendingSaves)
                if (SaveCharacter(pending.Value)) saved.Add(pending.Key);
            foreach (string path in saved) pendingSaves.Remove(path);
        }

        private void OnApplicationQuit()
        {
            if (characterDirectory != null) SaveProgress();
        }

        internal void ChangeOnlineClass(ulong id, ArcherController actor, CharacterClass type)
        {
            actor.Loadout.SelectClass(type);
            actor.GetComponent<PlayerEquipment>().Bind(characters[id].For(type));
        }

        internal void ItemAction(ulong id, ItemCommand command, string item, int slot)
        {
            if (!manager.IsServer || !Actors.TryGetValue(id, out ArcherController actor) || actor.Health.IsDead) return;
            PlayerInventoryProfile profile = characters[id].For(actor.Loadout.Class);
            string reason = "";
            bool success;
            switch (command)
            {
                case ItemCommand.Equip:
                    success = profile.TryEquip(item, out reason);
                    break;
                case ItemCommand.Unequip:
                    success = profile.TryUnequip((EquipmentSlot)slot, out reason);
                    break;
                case ItemCommand.Craft:
                case ItemCommand.Upgrade:
                    if (!NearSmith(id, actor, 2.5f)) { Message(id, "대장장이 가까이에서 제작할 수 있습니다."); return; }
                    if (command == ItemCommand.Upgrade) success = profile.TryUpgradeWeapon(out reason);
                    else if (ItemCatalog.TryGet(item, out ItemDefinition definition) && definition.CanEquip(profile.Class))
                        success = EquipmentCrafting.TryCraft(profile.Inventory, item, out reason);
                    else { success = false; reason = "현재 직업의 제작 장비가 아닙니다."; }
                    break;
                case ItemCommand.Use:
                    if (item == MonsterDropTable.HealthPotion && actor.Health.Current < actor.Health.Maximum)
                    {
                        success = profile.Inventory.RemoveItem(item, 1);
                        if (success) actor.Health.Heal(30);
                    }
                    else if (item == MonsterDropTable.ManaPotion && actor.Mana.Current < actor.Mana.Maximum)
                    {
                        success = profile.Inventory.RemoveItem(item, 1);
                        if (success) actor.Mana.Restore(30);
                    }
                    else success = false;
                    reason = "지금 사용할 수 없는 소모품입니다.";
                    break;
                default: return;
            }
            Message(id, success ? "아이템을 반영했습니다." : string.IsNullOrEmpty(reason) ? "보유하지 않은 아이템입니다." : reason);
        }

        private bool NearSmith(ulong id, ArcherController actor, float radius) =>
            Parties.Members[id].Area == GameArea.Town && Vector2.Distance(actor.transform.position, onlineBlacksmith.transform.position) <= radius;

        private void AwardBoss(Health defeated)
        {
            foreach (PartyMember member in Parties.Group(Parties.BossParty))
            {
                if (member.Area != GameArea.BossArena || !Actors.TryGetValue(member.Id, out ArcherController actor)) continue;
                if (actor.Loadout.BossLearned || actor.Loadout.BossSkillBooks > 0) continue;
                actor.Loadout.AddBossSkillBook();
                SaveCharacter(characters[member.Id]);
                Message(member.Id, "보스 스킬 북을 획득했습니다.");
            }
        }

        private void ApplyInventory(string encoded)
        {
            if (clientInventoryData == encoded) return;
            ClientInventory = string.IsNullOrEmpty(encoded) ? null : InventorySaveStore.Decode(encoded);
            clientInventoryData = encoded;
            ClientInventoryChanged?.Invoke();
        }

        private void PrepareFieldEnemy(ServerWorldEntity enemy)
        {
            if (enemy.GetComponent<Health>() == null) return;
            if (fieldTemplates == null)
            {
                var templates = new GameObject("Field respawn templates");
                templates.SetActive(false);
                templates.transform.SetParent(transform, false);
                fieldTemplates = templates.transform;
            }
            var template = Instantiate(enemy, fieldTemplates, true);
            BindFieldEnemy(enemy, template);
        }

        private void BindFieldEnemy(ServerWorldEntity enemy, ServerWorldEntity template)
        {
            if (!enemy.TryGetComponent(out MonsterDropTable table))
            {
                table = enemy.gameObject.AddComponent<MonsterDropTable>();
                MonsterKind kind = MonsterIdentity.Resolve(enemy.gameObject);
                table.Configure(kind == MonsterKind.Rare ? MonsterDropTable.RareMonster :
                    enemy.GetComponent<RangedEnemyController>() != null ? MonsterDropTable.NormalMonster2 : MonsterDropTable.NormalMonster1);
            }
            enemy.GetComponent<Health>().Died += _ => StartCoroutine(RespawnFieldEnemy(enemy, template));
        }

        private IEnumerator RespawnFieldEnemy(ServerWorldEntity previous, ServerWorldEntity template)
        {
            yield return new WaitForSeconds(20);
            if (previous != null) Destroy(previous.gameObject);
            var enemy = Instantiate(template, maps[(int)GameArea.Field], true);
            BindFieldEnemy(enemy, template);
            enemy.SetSimulation(true);
        }
    }
}
