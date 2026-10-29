using PersistentWorld.Combat;
using UnityEngine;

namespace PersistentWorld.World
{
    [RequireComponent(typeof(Health))]
    public sealed class BossController : MonoBehaviour
    {
        [SerializeField] private float attackInterval = 2.5f;
        [SerializeField] private int projectileDamage = 12;

        private Health health;
        private float attackReadyAt;
        private bool phaseTwo;
        private float patternEndsAt;
        // Teammates can hold this true throughout a boss pattern/phase transition.
        public bool PatternLocked { get; set; }
        public bool IsPerformingPattern => PatternLocked || Time.time < patternEndsAt;

        private void Awake()
        {
            BossEncounter.Begin();
            health = GetComponent<Health>();
        }

        private void Update()
        {
            if (health.IsDead)
            {
                return;
            }

            if (!phaseTwo && health.Current <= health.Maximum / 2)
            {
                phaseTwo = true;
                attackInterval = 1.4f;
            }

            if (Time.time >= attackReadyAt)
            {
                FireRing(phaseTwo ? 12 : 8);
                attackReadyAt = Time.time + attackInterval;
            }
        }

        private void FireRing(int projectileCount)
        {
            patternEndsAt = Time.time + .6f;
            for (int index = 0; index < projectileCount; index++)
            {
                float angle = 360f * index / projectileCount;
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
                Projectile.Spawn(transform.position, direction, projectileDamage, Faction.Enemy, gameObject, new Color(1f, 0.25f, 0.2f), 3.5f, 3f);
            }
        }
    }
}

