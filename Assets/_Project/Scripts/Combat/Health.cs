using System;
using UnityEngine;

namespace PersistentWorld.Combat
{
    public sealed class Health : MonoBehaviour
    {
        [SerializeField] private Faction faction = Faction.Neutral;
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField] private bool destroyOnDeath = true;

        public event Action<int, int> Changed;
        public event Action<Health> Died;

        public Faction Faction => faction;
        public int Current { get; private set; }
        public int Maximum => maxHealth;
        public bool IsDead => Current <= 0;
        public float InvulnerableUntil { get; private set; }

        private void Awake()
        {
            Current = maxHealth;
        }

        public void Configure(Faction newFaction, int newMaximum, bool shouldDestroyOnDeath = true)
        {
            faction = newFaction;
            maxHealth = Mathf.Max(1, newMaximum);
            destroyOnDeath = shouldDestroyOnDeath;
            Current = maxHealth;
        }

        public bool TryDamage(int amount, Faction sourceFaction)
        {
            if (IsDead || Time.time < InvulnerableUntil || amount <= 0 || sourceFaction == faction || sourceFaction == Faction.Neutral)
            {
                return false;
            }

            CombatStatus status = GetComponent<CombatStatus>();
            if (status != null && status.Vulnerable) amount = Mathf.RoundToInt(amount * 2f);
            Current = Mathf.Max(0, Current - amount);
            Changed?.Invoke(Current, maxHealth);
            if (Current == 0)
            {
                Died?.Invoke(this);
                if (destroyOnDeath)
                {
                    Destroy(gameObject, 0.05f);
                }
            }

            return true;
        }

        public void KeepOnDeath() => destroyOnDeath = false;

        public bool Heal(int amount)
        {
            if (IsDead || amount <= 0) return false;
            Current = Mathf.Min(maxHealth, Current + amount);
            Changed?.Invoke(Current, maxHealth);
            return true;
        }

        public bool Revive(float healthFraction = .7f, float immunitySeconds = 3f)
        {
            if (!IsDead) return false;
            Current = Mathf.Clamp(Mathf.RoundToInt(maxHealth * healthFraction), 1, maxHealth);
            InvulnerableUntil = Time.time + Mathf.Max(0, immunitySeconds);
            GetComponent<CombatStatus>()?.ResetState();
            Changed?.Invoke(Current, maxHealth);
            return true;
        }
    }
}

