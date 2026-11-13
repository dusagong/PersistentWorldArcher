using PersistentWorld.Combat;
using UnityEngine;

namespace PersistentWorld.World
{
    [RequireComponent(typeof(Health))]
    public sealed class TrainingDummy : MonoBehaviour
    {
        private Health health;
        private float lastHit;
        private void Awake() { health = GetComponent<Health>(); health.KeepOnDeath(); GetComponent<CombatStatus>().Immovable = true; }
        private void OnEnable() { if (health == null) health = GetComponent<Health>(); health.Changed += Changed; }
        private void OnDisable() { if (health != null) health.Changed -= Changed; }
        private void Changed(int current, int maximum) => lastHit = Time.time;
        private void Update()
        {
            if (Time.time - lastHit < 3) return;
            if (health.IsDead) health.Revive(1, 0);
            else if (health.Current < health.Maximum) health.Heal(health.Maximum);
        }
    }
}
