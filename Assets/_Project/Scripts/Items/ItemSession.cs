using System.IO;
using System;
using PersistentWorld.Core;
using PersistentWorld.Player;
using PersistentWorld.UI;
using PersistentWorld.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersistentWorld.Items
{
    public sealed class ItemSession : MonoBehaviour
    {
        public static ItemSession Instance { get; private set; }
        public PlayerInventoryProfile Profile { get; private set; }
        public string SavePath { get; private set; }
        public string SaveError { get; private set; }
        public bool CanSave { get; private set; }
        public event Action ProfileChanged;
        public static PlayerClass ToItemClass(CharacterClass type) => type switch
        { CharacterClass.Warrior => PlayerClass.Warrior, CharacterClass.Healer => PlayerClass.Healer, _ => PlayerClass.Archer };
        public static CharacterClass ToCharacterClass(PlayerClass type) => type switch
        { PlayerClass.Warrior => CharacterClass.Warrior, PlayerClass.Healer => CharacterClass.Healer, _ => CharacterClass.Archer };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Instance == null) new GameObject("Player Item Session").AddComponent<ItemSession>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadProfile();
            gameObject.AddComponent<BlacksmithWindow>();
            gameObject.AddComponent<InventoryWindow>();
        }

        private void OnEnable()
        {
            if (Instance != null && Instance != this) return;
            Instance = this;
            // Managed profile/event state is lost during an Editor assembly reload.
            if (Profile == null) LoadProfile();
            Profile.Changed -= Save;
            Profile.Changed += Save;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            BindPlayers();
            Save();
        }

        private void LoadProfile()
        {
            SavePath = Path.Combine(Application.persistentDataPath, "inventory-v1.json");
#if UNITY_EDITOR
            // Batch validation must never read or overwrite the developer's actual profile.
            if (Application.isBatchMode)
            {
                string testPath = System.Environment.GetEnvironmentVariable("PWA_INVENTORY_TEST_SAVE");
                SavePath = !string.IsNullOrEmpty(testPath) ? testPath : Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Validation/inventory-" + Guid.NewGuid().ToString("N") + ".json"));
            }
#endif
            InventoryLoadResult result = InventorySaveStore.Load(SavePath, out var restored, out string error);
            Profile = restored ?? PlayerInventoryProfile.CreateNew();
            CanSave = result != InventoryLoadResult.Failed;
            SaveError = error;
            if (!CanSave) Debug.LogError("Inventory load failed. Existing save is preserved; autosave disabled. " + error);
        }

        private void BindPlayers()
        {
            if (Profile == null) return;
            foreach (ArcherController archer in FindObjectsByType<ArcherController>(FindObjectsSortMode.None))
            {
                if (!archer.CompareTag("Player")) continue;
                PlayerEquipment equipment = archer.GetComponent<PlayerEquipment>();
                if (equipment == null) equipment = archer.gameObject.AddComponent<PlayerEquipment>();
                equipment.Bind(Profile);
                if (archer.Loadout.Class != ToCharacterClass(Profile.Class)) archer.Loadout.SelectClass(ToCharacterClass(Profile.Class));
            }
        }

        private string ClassSavePath(PlayerClass type) => Path.Combine(Path.GetDirectoryName(SavePath), Path.GetFileNameWithoutExtension(SavePath) + "-" + type + ".json");

        public bool SelectClass(PlayerClass type)
        {
            if (Profile.Class == type) return true;
            if (!CanSave || !Enum.IsDefined(typeof(PlayerClass), type)) return false;
            Save();
            if (!string.IsNullOrEmpty(SaveError)) return false;
            InventoryLoadResult result = InventorySaveStore.Load(ClassSavePath(type), out PlayerInventoryProfile next, out string error);
            if (result == InventoryLoadResult.Failed || (next != null && next.Class != type))
            {
                SaveError = result == InventoryLoadResult.Failed ? error : "직업별 저장 파일의 직업 정보가 일치하지 않습니다.";
                Debug.LogWarning("Class inventory was preserved because it could not be read: " + SaveError);
                return false;
            }
            Profile.Changed -= Save;
            Profile = next ?? PlayerInventoryProfile.CreateNew(type);
            Profile.Changed += Save;
            foreach (ArcherController player in FindObjectsByType<ArcherController>(FindObjectsSortMode.None))
                if (player.CompareTag("Player"))
                {
                    PlayerEquipment equipment = player.GetComponent<PlayerEquipment>() ?? player.gameObject.AddComponent<PlayerEquipment>();
                    equipment.Bind(Profile);
                }
            ProfileChanged?.Invoke();
            Save();
            return true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BindPlayers();
            if (scene.name == "Town" && FindFirstObjectByType<BlacksmithNpc>() == null)
            {
                GameObject npc = new GameObject("Blacksmith");
                SceneManager.MoveGameObjectToScene(npc, scene);
                npc.transform.position = new Vector3(-1.5f, -1.5f, 0f);
                npc.AddComponent<PlaceholderVisual>().Configure(new Color(0.7f, 0.45f, 0.25f), new Vector2(0.8f, 1.1f));
                npc.AddComponent<WorldDepthSort>();
                npc.AddComponent<BoxCollider2D>().isTrigger = true;
                npc.AddComponent<BlacksmithNpc>();
            }
        }

        public void Save()
        {
            if (!CanSave || Profile == null || string.IsNullOrWhiteSpace(SavePath)) return;
            if (InventorySaveStore.TrySave(SavePath, Profile, out string error)) SaveError = string.Empty;
            else { SaveError = error; Debug.LogWarning("Inventory could not be saved: " + error); }
            if (!InventorySaveStore.TrySave(ClassSavePath(Profile.Class), Profile, out string classError))
            { SaveError = classError; Debug.LogWarning("Class inventory could not be saved: " + classError); }
        }

        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationQuit() => Save();
        private void OnDisable()
        {
            if (Instance != this) return;
            Save();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Profile != null) Profile.Changed -= Save;
            Instance = null;
        }
    }
}
