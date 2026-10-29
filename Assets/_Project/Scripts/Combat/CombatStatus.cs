using PersistentWorld.Core;
using PersistentWorld.World;
using UnityEngine;

namespace PersistentWorld.Combat
{
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(Health))]
    public sealed class CombatStatus : MonoBehaviour
    {
        private float vulnerableUntil;
        private float displacedUntil;
        private Vector2 displacementVelocity;
        public bool Vulnerable => Time.time < vulnerableUntil;
        public bool IsDisplaced => Time.time < displacedUntil;
        public float OutgoingMultiplier => Vulnerable ? 1.5f : 1f;
        public void ApplyVulnerability(float seconds = 10f) => vulnerableUntil = Time.time + Mathf.Max(0, seconds);
        public void Cleanse() => vulnerableUntil = 0;
        public void ResetState() { vulnerableUntil = displacedUntil = 0; }

        public bool Knockback(Vector2 direction, float distance, float duration = .2f)
        {
            BossController boss = GetComponent<BossController>();
            Health health = GetComponent<Health>();
            if (health == null || health.IsDead || (boss != null && boss.IsPerformingPattern)) return false;
            displacedUntil = Time.time + duration;
            displacementVelocity = direction.normalized * distance / duration;
            return true;
        }

        private void FixedUpdate()
        {
            if (!IsDisplaced || GetComponent<Health>().IsDead) return;
            Vector2 movement = displacementVelocity * Time.fixedDeltaTime;
            float distance = movement.magnitude;
            foreach (RaycastHit2D hit in Physics2D.CircleCastAll(transform.position, .25f, movement.normalized, distance))
                if (hit.collider.GetComponentInParent<WorldObstacle>() != null) distance = Mathf.Min(distance, Mathf.Max(0, hit.distance - .01f));
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null) { body.linearVelocity = Vector2.zero; body.MovePosition(body.position + movement.normalized * distance); }
            else transform.position += (Vector3)(movement.normalized * distance);
        }
    }
}
