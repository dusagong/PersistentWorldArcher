using PersistentWorld.Combat;
using PersistentWorld.Items;
using PersistentWorld.UI;
using UnityEngine;

namespace PersistentWorld.Player
{
    public static class PrototypeManaBalance
    {
        public const float Maximum = 100, RecoveryDelay = 0, RecoveryPerSecond = 2, DodgeCost = 5;
        public static float Cost(SkillId id) => id switch
        {
            SkillId.Repulse or SkillId.Whirlwind => 15,
            SkillId.ArrowRain or SkillId.HealingZone => 25,
            SkillId.Snipe or SkillId.Excalibur or SkillId.Judgment => 30,
            SkillId.Cleanse => 20, SkillId.ComboSlash => 6, SkillId.BossBlast => 50,
            _ => 0
        };
        public static float SpecialCost(CharacterClass type) => type switch
        {
            CharacterClass.Warrior => 10, CharacterClass.Healer => 40, _ => 30
        };
    }

    internal sealed class PlayerManaState
    {
        public float Current = PrototypeManaBalance.Maximum;
        public float RecoveryAt;
        public bool RefillOnTownReturn;
        public void Refill()
        {
            Current = PrototypeManaBalance.Maximum;
            RecoveryAt = Time.time;
            RefillOnTownReturn = false;
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerMana : MonoBehaviour, IManaSource
    {
        private PlayerManaState state;
        private Health health;
        public float Current => state.Current;
        public float Maximum => PrototypeManaBalance.Maximum;

        private void Awake()
        {
            health = GetComponent<Health>();
            state = CompareTag("Player") && ItemSession.Instance != null
                ? ItemSession.Instance.ManaState : new PlayerManaState();
            health.Died += OnDeath;
        }

        private void OnDestroy() { if (health != null) health.Died -= OnDeath; }
        private void OnDeath(Health _) => state.RefillOnTownReturn = true;

        private void Update()
        {
            if (health.IsDead) return;
            state.RefillOnTownReturn = false;
            if (Current >= Maximum) return;
            float recoveringSeconds = Mathf.Max(0, Time.time - Mathf.Max(Time.time - Time.deltaTime, state.RecoveryAt));
            state.Current = Mathf.Min(Maximum, Current + recoveringSeconds * PrototypeManaBalance.RecoveryPerSecond);
        }

        public bool CanAfford(float amount) => amount >= 0 && Current >= amount;
        public bool TrySpend(float amount)
        {
            if (health.IsDead || !CanAfford(amount)) return false;
            if (amount == 0) return true;
            state.Current -= amount;
            state.RecoveryAt = Time.time + PrototypeManaBalance.RecoveryDelay;
            return true;
        }
    }
}
