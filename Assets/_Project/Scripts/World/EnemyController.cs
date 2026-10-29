using PersistentWorld.Combat;
using UnityEngine;

namespace PersistentWorld.World
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class EnemyController : MonoBehaviour
    {
        [SerializeField] private float detectionRange = 5f;
        [SerializeField] private float attackRange = 1f;
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private int contactDamage = 8;
        [SerializeField] private float attackInterval = 1.5f;

        private Rigidbody2D body;
        private Health health;
        private Transform player;
        private float attackReadyAt;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
        }

        private void FixedUpdate()
        {
            if (health.IsDead)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                player = playerObject == null ? null : playerObject.transform;
            }

            if (player == null)
            {
                return;
            }

            Vector2 delta = player.position - transform.position;
            float distance = delta.magnitude;
            if (distance > detectionRange)
            {
                body.linearVelocity = Vector2.zero;
            }
            else if (distance > attackRange)
            {
                body.linearVelocity = delta.normalized * moveSpeed;
            }
            else
            {
                body.linearVelocity = Vector2.zero;
                TryAttack();
            }
        }

        private void TryAttack()
        {
            if (Time.time < attackReadyAt || player == null)
            {
                return;
            }

            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth != null)
            {
                playerHealth.TryDamage(contactDamage, Faction.Enemy, gameObject);
                attackReadyAt = Time.time + attackInterval;
            }
        }
    }
}

