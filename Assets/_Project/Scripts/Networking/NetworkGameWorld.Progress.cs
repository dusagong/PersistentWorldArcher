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
    public enum ItemCommand : byte { Equip, Unequip, Craft, Upgrade, Use, AssignPotion, UsePotion }

    public sealed partial class NetworkGameWorld
    {
        [Serializable] private sealed class CharacterSave
        {
            public int version = 1;
            public string name;
            public string[] inventories;
            public SkillLoadout.OnlineState skills;
            public string[] potionSlots;
            public long[] potionReadyTicks;
        }

        private sealed class Character
        {
            public readonly PlayerInventoryProfile[] Inventories = new PlayerInventoryProfile[3];
            private readonly string[] encoded = new string[3];
            public string Path;
            public string Name;
            public bool Dirty;
            public bool Demo, Forget;
            public double Expires;
            public CharacterClass InitialClass;
            public SkillLoadout.OnlineState Skills;
            public readonly string[] PotionSlots = new string[2];
            public readonly long[] PotionReadyTicks = new long[2];
            public float PotionRemaining(int slot) =>
                (float)Math.Max(0, (PotionReadyTicks[slot] - DateTime.UtcNow.Ticks) / (double)TimeSpan.TicksPerSecond);
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
        private readonly Dictionary<string, Character> demoSaves = new Dictionary<string, Character>();
        private string characterDirectory;
        private float nextSave;
        private GameObject onlineBlacksmith;
        private readonly Dictionary<ulong, int> smithRequests = new Dictionary<ulong, int>();
        private string clientInventoryData;
        private int clientSmithRequest;
        private Transform fieldTemplates;
        public PlayerInventoryProfile ClientInventory { get; private set; }
        public event Action ClientInventoryChanged;
        internal Vector2 BlacksmithPosition => onlineBlacksmith.transform.position;
        public bool CanUseSmith => Local is PlayerFrame local && local.hp > 0 && local.area == (int)GameArea.Town &&
            Vector2.Distance(local.position, onlineBlacksmith.transform.position) <= 2.5f;

        internal static string HashKey(string key)
        {
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "").ToLowerInvariant();
        }

        private void PrepareProgress(bool server)
        {
            Transform town = maps[(int)GameArea.Town];
            BlacksmithNpc smith = town.GetComponentInChildren<BlacksmithNpc>(true);
            if (smith != null) onlineBlacksmith = smith.gameObject;
            else
            {
                onlineBlacksmith = new GameObject("Blacksmith");
                onlineBlacksmith.transform.SetParent(town, false);
                onlineBlacksmith.transform.position = new Vector3(-1.5f, -1.5f, 0);
                onlineBlacksmith.AddComponent<PlaceholderVisual>().Configure(new Color(.7f, .45f, .25f), new Vector2(.8f, 1.1f));
            }
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

        internal bool ReserveCharacter(ulong id, string key, out string reason, string mode = "resume", int selected = 0)
        {
            reason = "캐릭터 키가 올바르지 않습니다.";
            if (!Guid.TryParseExact(key, "N", out Guid token) || token == Guid.Empty) return false;
            string path = Path.Combine(characterDirectory, HashKey(token.ToString("N")) + ".json");
            if (!Enum.IsDefined(typeof(CharacterClass), selected) ||
                (mode != "resume" && mode != "restore" && mode != "create" && mode != "demo")) return false;
            foreach (Character active in characters.Values)
            {
                if (active.Path != path) continue;
                reason = "이미 이 캐릭터가 접속 중입니다.";
                return false;
            }

            if (demoSaves.TryGetValue(path, out Character demo) && demo.Expires > Time.unscaledTimeAsDouble && mode == "demo")
            {
                demoSaves.Remove(path); characters.Add(id, demo); return true;
            }
            if (mode == "demo" && File.Exists(path)) return false;
            if (mode == "restore" && !File.Exists(path) && !pendingSaves.ContainsKey(path))
            { reason = "플레이어 코드를 찾을 수 없습니다. 코드를 확인해주세요."; return false; }
            if (mode == "create" && (File.Exists(path) || pendingSaves.ContainsKey(path)))
            { reason = "이미 사용 중인 코드입니다. 코드로 로그인해주세요."; return false; }
            if (mode != "demo" && pendingSaves.TryGetValue(path, out Character pending))
            {
                pendingSaves.Remove(path);
                characters.Add(id, pending);
                return true;
            }
            try
            {
                var character = new Character { Path = path, Demo = mode == "demo", InitialClass = (CharacterClass)selected, Dirty = true };
                if (!character.Demo && File.Exists(path))
                {
                    var save = JsonUtility.FromJson<CharacterSave>(File.ReadAllText(path));
                    if (save == null || save.version != 1 || save.inventories == null || save.inventories.Length != 3 ||
                        save.skills == null || !Enum.IsDefined(typeof(CharacterClass), save.skills.selected))
                        throw new InvalidDataException("Invalid online character record.");
                    character.Skills = save.skills;
                    character.Name = save.name;
                    for (int slot = 0; slot < character.PotionSlots.Length; slot++)
                    {
                        if (save.potionSlots != null && slot < save.potionSlots.Length && IsPotion(save.potionSlots[slot]))
                            character.PotionSlots[slot] = save.potionSlots[slot];
                        if (save.potionReadyTicks != null && slot < save.potionReadyTicks.Length)
                            character.PotionReadyTicks[slot] = Math.Max(0, Math.Min(save.potionReadyTicks[slot], DateTime.UtcNow.AddSeconds(10).Ticks));
                    }
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
            else actor.Loadout.SelectClass(character.InitialClass);
            actor.GetComponent<PlayerEquipment>().Bind(character.For(actor.Loadout.Class));
            actor.Loadout.Changed += () =>
            {
                character.Skills = actor.Loadout.ExportOnline();
                character.Dirty = true;
            };
            character.Skills = actor.Loadout.ExportOnline();
        }

        internal string CharacterName(ulong id, string requested)
        {
            Character character = characters[id];
            if (!string.IsNullOrEmpty(requested)) { character.Name = requested; character.Dirty = true; }
            return string.IsNullOrEmpty(character.Name) ? "Player " + id : character.Name;
        }

        private static bool SaveCharacter(Character character)
        {
            if (character.Demo || !character.Dirty) return true;
            var save = new CharacterSave { name = character.Name, skills = character.Skills, inventories = new string[3],
                potionSlots = character.PotionSlots, potionReadyTicks = character.PotionReadyTicks };
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
            if (!characters.Remove(id, out Character character)) return;
            if (character.Demo)
            {
                if (!character.Forget) { character.Expires = Time.unscaledTimeAsDouble + 90; demoSaves[character.Path] = character; }
            }
            else if (!SaveCharacter(character)) pendingSaves[character.Path] = character;
        }

        private void SaveProgress()
        {
            var expired = new List<string>();
            foreach (var demo in demoSaves) if (demo.Value.Expires <= Time.unscaledTimeAsDouble) expired.Add(demo.Key);
            foreach (string key in expired) demoSaves.Remove(key);
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
            Character character = characters[id];
            PlayerInventoryProfile profile = character.For(actor.Loadout.Class);
            if (Parties.Members[id].Area == GameArea.Tutorial)
            { Message(id, "연습장에서는 아이템을 소비하거나 장비를 변경하지 않습니다."); return; }
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
                    slot = Array.IndexOf(character.PotionSlots, item);
                    if (slot < 0) { Message(id, "포션을 1번 또는 2번에 먼저 장착하세요."); return; }
                    UsePotion(actor, character, profile, slot);
                    return;
                case ItemCommand.AssignPotion:
                    if ((uint)slot >= character.PotionSlots.Length || !IsPotion(item)) return;
                    if (character.PotionSlots[slot] == item) character.PotionSlots[slot] = null;
                    else if (profile.Inventory.GetCount(item) > 0) character.PotionSlots[slot] = item;
                    else return;
                    character.Dirty = true;
                    return;
                case ItemCommand.UsePotion:
                    UsePotion(actor, character, profile, slot);
                    return;
                default: return;
            }
            Message(id, success ? "아이템을 반영했습니다." : string.IsNullOrEmpty(reason) ? "보유하지 않은 아이템입니다." : reason);
        }

        internal static bool IsPotion(string item) => item == MonsterDropTable.HealthPotion || item == MonsterDropTable.ManaPotion;

        private static void UsePotion(ArcherController actor, Character character, PlayerInventoryProfile profile, int slot)
        {
            if ((uint)slot >= character.PotionSlots.Length || character.PotionRemaining(slot) > 0) return;
            string item = character.PotionSlots[slot];
            bool heal = item == MonsterDropTable.HealthPotion;
            if (!IsPotion(item) || (heal ? actor.Health.Current >= actor.Health.Maximum : actor.Mana.Current >= actor.Mana.Maximum) ||
                !profile.Inventory.RemoveItem(item, 1)) return;
            if (heal) actor.Health.Heal(30);
            else actor.Mana.Restore(30);
            character.PotionReadyTicks[slot] = DateTime.UtcNow.AddSeconds(10).Ticks;
            character.Dirty = true;
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
            var enemy = Instantiate(template, maps[(int)template.area], true);
            BindFieldEnemy(enemy, template);
            enemy.SetSimulation(true);
        }
    }
}
