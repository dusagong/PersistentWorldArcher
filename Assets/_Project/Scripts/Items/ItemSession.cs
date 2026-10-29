using System.IO;
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
                if (!string.IsNullOrEmpty(testPath)) SavePath = testPath;
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
                PlayerEquipment equipment = archer.GetComponent<PlayerEquipment>();
                if (equipment == null) equipment = archer.gameObject.AddComponent<PlayerEquipment>();
                equipment.Bind(Profile);
            }
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
