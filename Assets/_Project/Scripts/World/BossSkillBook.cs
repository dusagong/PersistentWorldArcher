using PersistentWorld.Core;
using PersistentWorld.Player;
using UnityEngine;

namespace PersistentWorld.World
{
    public sealed class BossSkillBook : MonoBehaviour, IInteractable
    {
        private bool collected;
        public string Prompt => "보스 스킬 북 획득";
        public static BossSkillBook Spawn(Vector2 position)
        {
            GameObject obj = new GameObject("Boss Skill Book");
            obj.transform.position = position;
            obj.AddComponent<PlaceholderVisual>().Configure(Color.magenta, new Vector2(.35f, .45f));
            obj.AddComponent<BoxCollider2D>().isTrigger = true;
            return obj.AddComponent<BossSkillBook>();
        }

        public void Interact(GameObject actor)
        {
            SkillLoadout loadout = actor.GetComponent<SkillLoadout>();
            if (collected || loadout == null || actor.GetComponent<Combat.Health>()?.IsDead == true) return;
            collected = true;
            loadout.AddBossSkillBook();
            Destroy(gameObject);
        }
    }
}
