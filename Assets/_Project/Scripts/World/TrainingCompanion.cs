using PersistentWorld.Combat;
using PersistentWorld.Networking;
using UnityEngine;

namespace PersistentWorld.World
{
    public enum TrainingPurpose { Healing, Cleanse, Revive }

    [RequireComponent(typeof(Health), typeof(CombatStatus))]
    public sealed class TrainingCompanion : MonoBehaviour
    {
        public ulong Owner { get; private set; }
        public TrainingPurpose Purpose { get; private set; }
        public bool Initializing { get; private set; }
        public bool Completed { get; private set; }
        private Health health;
        private CombatStatus status;
        private SpriteRenderer visual;
        private int initialHealth;

        public void Configure(ulong owner, TrainingPurpose purpose, Sprite sprite)
        {
            Owner = owner; Purpose = purpose; Initializing = true;
            health = GetComponent<Health>(); status = GetComponent<CombatStatus>();
            visual = GetComponent<SpriteRenderer>(); visual.sprite = sprite;
            health.Configure(Faction.Player, 100, false); status.Immovable = true;
            if (purpose == TrainingPurpose.Healing) health.TryDamage(70, Faction.Enemy);
            if (purpose == TrainingPurpose.Revive) health.TryDamage(100, Faction.Enemy);
            if (purpose == TrainingPurpose.Cleanse)
            { status.ApplyVulnerability(86400); status.ApplySlow(86400); }
            initialHealth = health.Current;
            health.Changed += Changed;
            Initializing = false;
            UpdateVisual();
        }

        private void Changed(int current, int maximum)
        {
            if (Initializing || Completed) return;
            if ((Purpose == TrainingPurpose.Healing && current > initialHealth) ||
                (Purpose == TrainingPurpose.Revive && current > 0)) Complete();
        }
        private void Update()
        {
            if (health == null) return;
            if (!Completed && Purpose == TrainingPurpose.Cleanse && !status.Vulnerable && status.MovementMultiplier == 1) Complete();
            UpdateVisual();
        }
        private void Complete()
        {
            Completed = true;
            NetworkGameWorld.Instance?.RecordPractice(Owner, Purpose == TrainingPurpose.Healing ? 32 : Purpose == TrainingPurpose.Cleanse ? 64 : 128);
        }
        private void UpdateVisual()
        {
            visual.color = Completed ? new Color(.65f, 1, .7f) : health.IsDead ? new Color(.35f, .35f, .42f, .75f) :
                Purpose == TrainingPurpose.Cleanse ? new Color(.8f, .5f, 1) : new Color(.7f, .85f, 1);
            visual.sortingOrder = 1000 - Mathf.RoundToInt(transform.position.y * 100);
        }
        private void OnDestroy() { if (health != null) health.Changed -= Changed; }
    }
}
