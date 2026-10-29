using PersistentWorld.World;
using UnityEngine;

namespace PersistentWorld.Combat
{
    public sealed class MonsterIdentity : MonoBehaviour
    {
        [SerializeField] private MonsterKind kind = MonsterKind.Normal;
        public MonsterKind Kind => kind;
        public void Configure(MonsterKind value) => kind = value;

        public static MonsterKind Resolve(GameObject actor)
        {
            if (actor == null) return MonsterKind.None;
            MonsterIdentity identity = actor.GetComponentInParent<MonsterIdentity>();
            if (identity != null) return identity.Kind;
            if (actor.GetComponentInParent<BossController>() != null) return MonsterKind.Boss;
            EnemyController enemy = actor.GetComponentInParent<EnemyController>();
            if (enemy == null) return MonsterKind.None;
            // Compatibility with the existing serialized prototype's "Rare Enemy" object.
            return enemy.name.IndexOf("Rare", System.StringComparison.OrdinalIgnoreCase) >= 0
                ? MonsterKind.Rare : MonsterKind.Normal;
        }
    }
}
