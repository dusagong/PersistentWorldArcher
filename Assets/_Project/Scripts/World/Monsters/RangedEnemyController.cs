using PersistentWorld.Combat;
using UnityEngine;

namespace PersistentWorld.World
{
    // 일반 몬스터2 (원거리형): 일반 몬스터1보다 먼 거리를 유지하며 짧게 조준한 뒤 투척물을 던진다.
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class RangedEnemyController : MonoBehaviour
    {
        [Header("이동")]
        [SerializeField] private float detectionRange = 9f;
        [SerializeField] private float preferredRange = 6.5f;
        [SerializeField] private float retreatRange = 4.5f;
        [SerializeField] private float moveSpeed = 2f;

        [Header("사격")]
        [SerializeField] private int projectileDamage = 6;
        [SerializeField] private float projectileSpeed = 8f;
        [SerializeField] private float projectileLifetime = 1.3f;
        [SerializeField] private float fireInterval = 2f;
        [SerializeField] private float aimTime = .35f;
        [SerializeField] private Color projectileColor = new Color(1f, .55f, .1f);
        [SerializeField] private Color aimTint = new Color(1f, 1f, .4f);

        private Rigidbody2D body;
        private Health health;
        private SpriteRenderer spriteRenderer;
        private Color baseColor;
        private bool baseColorCaptured;
        private Transform player;
        private float fireReadyAt;
        private float aimEndsAt = -1f;

        public bool IsAiming => aimEndsAt >= 0f;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            fireReadyAt = Time.time + fireInterval * .5f;
        }

        private void FixedUpdate()
        {
            if (GetComponent<CombatStatus>() is CombatStatus status && status.IsDisplaced)
            {
                CancelAim();
                return;
            }

            if (health.IsDead)
            {
                CancelAim();
                body.linearVelocity = Vector2.zero;
                return;
            }

            if (!FindPlayer())
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 delta = player.position - transform.position;
            float distance = delta.magnitude;

            if (IsAiming)
            {
                body.linearVelocity = Vector2.zero;
                if (Time.time >= aimEndsAt) Fire(delta);
                return;
            }

            if (distance > detectionRange)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            if (distance < retreatRange) body.linearVelocity = -delta.normalized * moveSpeed;
            else if (distance > preferredRange) body.linearVelocity = delta.normalized * moveSpeed;
            else body.linearVelocity = Vector2.zero;

            if (Time.time >= fireReadyAt && distance <= preferredRange + 1f) BeginAim();
        }

        private bool FindPlayer()
        {
            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                player = playerObject == null ? null : playerObject.transform;
            }

            return player != null && player.GetComponent<Health>() is Health target && !target.IsDead;
        }

        private void BeginAim()
        {
            aimEndsAt = Time.time + aimTime;
            SetTint(true);
        }

        private void Fire(Vector2 delta)
        {
            CancelAim();
            fireReadyAt = Time.time + fireInterval;
            Vector2 direction = delta.sqrMagnitude > 0f ? delta.normalized : Vector2.right;
            Projectile.Spawn(transform.position, direction, projectileDamage, Faction.Enemy, gameObject,
                projectileColor, projectileSpeed, projectileLifetime);
        }

        private void CancelAim()
        {
            if (!IsAiming) return;
            aimEndsAt = -1f;
            SetTint(false);
        }

        private void SetTint(bool aiming)
        {
            if (spriteRenderer == null) return;
            if (!baseColorCaptured)
            {
                baseColor = spriteRenderer.color;
                baseColorCaptured = true;
            }

            spriteRenderer.color = aiming ? aimTint : baseColor;
        }
    }
}
