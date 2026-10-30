using System;
using System.Collections.Generic;
using PersistentWorld.Combat;
using PersistentWorld.Items;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersistentWorld.World
{
    [Serializable]
    public struct DropEntry
    {
        [Tooltip("ItemCatalog의 아이템 ID (예: material_1, material_2, health_potion, mana_potion)")]
        public string itemId;
        [Min(1)] public int quantity;
        [Range(0f, 1f), Tooltip("드롭 확률 (0.35 = 35%)")] public float chance;

        public DropEntry(string itemId, int quantity, float chance)
        {
            this.itemId = itemId;
            this.quantity = quantity;
            this.chance = chance;
        }
    }

    // 몬스터가 죽으면 드롭 표에 따라 아이템을 바닥에 떨어뜨린다. 아이템마다 따로 확률을 굴린다.
    // 드롭 표가 없는 기존 씬의 적(EnemyController)에는 실행 시 종류(일반/레어)에 맞는 기본 표가 자동으로 붙는다.
    [RequireComponent(typeof(Health))]
    public sealed class MonsterDropTable : MonoBehaviour
    {
        public const string HealthPotion = "health_potion";
        public const string ManaPotion = "mana_potion";

        // 기획서 드롭 표
        public static DropEntry[] NormalMonster1 => new[]
        {
            new DropEntry(ItemCatalog.MaterialOne, 1, .5f),
            new DropEntry(HealthPotion, 1, .35f),
            new DropEntry(ManaPotion, 1, .35f)
        };

        public static DropEntry[] NormalMonster2 => new[]
        {
            new DropEntry(ItemCatalog.MaterialTwo, 2, .5f),
            new DropEntry(HealthPotion, 1, .35f),
            new DropEntry(ManaPotion, 1, .35f)
        };

        public static DropEntry[] RareMonster => new[]
        {
            new DropEntry(ItemCatalog.MaterialOne, 4, 1f),
            new DropEntry(HealthPotion, 1, 1f),
            new DropEntry(ManaPotion, 1, 1f)
        };

        [SerializeField] private DropEntry[] drops = NormalMonster1;
        [SerializeField, Tooltip("죽은 자리에서 아이템이 흩어지는 반경")] private float scatterRadius = .7f;

        private Health health;

        public IReadOnlyList<DropEntry> Drops => drops;

        public void Configure(DropEntry[] entries) => drops = (DropEntry[])entries.Clone();

        private void Awake() => health = GetComponent<Health>();
        private void OnEnable() => health.Died += OnDied;
        private void OnDisable() => health.Died -= OnDied;

        private void OnDied(Health dead)
        {
            Vector2 origin = transform.position;
            foreach (DropEntry entry in drops)
            {
                if (entry.quantity < 1 || UnityEngine.Random.value >= entry.chance) continue;
                if (!ItemCatalog.TryGet(entry.itemId, out _))
                {
                    Debug.LogWarning($"드롭 표에 없는 아이템 ID: '{entry.itemId}' ({name})", this);
                    continue;
                }
                DroppedItem.Spawn(origin + UnityEngine.Random.insideUnitCircle * scatterRadius, entry.itemId, entry.quantity, origin);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachToScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToScene();

        private static void AttachToScene()
        {
            foreach (EnemyController enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (enemy.GetComponent<MonsterDropTable>() != null) continue;
                MonsterKind kind = MonsterIdentity.Resolve(enemy.gameObject);
                enemy.gameObject.AddComponent<MonsterDropTable>().Configure(kind == MonsterKind.Rare ? RareMonster : NormalMonster1);
            }
        }
    }
}
