using PersistentWorld.World;
using PersistentWorld.Items;
using UnityEngine;

namespace PersistentWorld.Combat
{
    public sealed class HealingZone : MonoBehaviour
    {
        private Health caster;
        private float endsAt;
        private float nextTick;
        public static HealingZone Spawn(Vector2 position, Health source, float seconds)
        {
            GameObject obj = new GameObject("Healing Zone");
            obj.transform.position = position;
            HealingZone zone = obj.AddComponent<HealingZone>();
            zone.caster = source;
            zone.endsAt = Time.time + seconds;
            zone.nextTick = Time.time + 1;
            SkillIndicator indicator = SkillIndicator.Create(Color.green, seconds);
            indicator.Circle(position, 2, 2);
            return zone;
        }

        private void Update()
        {
            // Five discrete ticks at seconds 1..5; damage cannot duplicate on extra colliders.
            while (nextTick <= endsAt && Time.time >= nextTick)
            {
                nextTick += 1;
                foreach (Health target in UnityEngine.Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
                {
                    if (target.IsDead || Vector2.Distance(transform.position, target.transform.position) > 2) continue;
                    if (target.Faction == Faction.Player) target.Heal(Mathf.RoundToInt(target.Maximum * .2f));
                    else if (target.GetComponent<BossController>() != null)
                        target.TryDamage(Mathf.RoundToInt((10 + (caster != null ? caster.GetComponent<PlayerEquipment>()?.Bonuses.Attack ?? 0 : 0)) * (caster != null ? caster.GetComponent<CombatStatus>()?.OutgoingMultiplier ?? 1 : 1)), Faction.Player, caster != null ? caster.gameObject : null);
                }
            }
            if (Time.time >= endsAt) Destroy(gameObject);
        }
    }
}
