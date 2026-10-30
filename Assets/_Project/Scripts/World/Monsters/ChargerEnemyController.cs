using PersistentWorld.Combat;
using UnityEngine;

namespace PersistentWorld.World
{
    // 돌진형 일반 몬스터: 다가온 뒤 멈춰서 돌진 경로를 경고하고, 직선으로 빠르게 돌진한다.
    // 돌진이 끝나면 잠깐 멈춰 서므로 플레이어가 반격할 틈이 생긴다.
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class ChargerEnemyController : MonoBehaviour
    {
        private enum State { Chase, Windup, Charge, Recover }

        [Header("추적")]
        [SerializeField] private float detectionRange = 8f;
        [SerializeField] private float moveSpeed = 1.8f;
        [SerializeField] private float chargeTriggerRange = 5f;

        [Header("돌진")]
        [SerializeField] private float windupTime = .7f;
        [SerializeField] private float chargeSpeed = 9f;
        [SerializeField] private float chargeDuration = .7f;
        [SerializeField] private int chargeDamage = 14;
        [SerializeField] private float hitRadius = .9f;
        [SerializeField] private float recoverTime = 1f;
        [SerializeField] private float chargeCooldown = 2.5f;

        [Header("경고 표시")]
        [SerializeField] private Color windupTint = new Color(1f, .2f, .2f);
        [SerializeField] private Color warningLineColor = new Color(1f, .15f, .15f, .45f);
        [SerializeField] private float warningLineWidth = .35f;

        private Rigidbody2D body;
        private Health health;
        private SpriteRenderer spriteRenderer;
        private LineRenderer warningLine;
        private Color baseColor;
        private bool baseColorCaptured;
        private Transform player;
        private State state = State.Chase;
        private float stateEndsAt;
        private float chargeReadyAt;
        private Vector2 chargeDirection;
        private bool hitThisCharge;

        public bool IsCharging => state == State.Charge;
        public bool IsWindingUp => state == State.Windup;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            warningLine = CreateWarningLine();
        }

        private void FixedUpdate()
        {
            if (GetComponent<CombatStatus>() is CombatStatus status && status.IsDisplaced)
            {
                if (state == State.Windup || state == State.Charge) EnterRecover();
                return;
            }

            if (health.IsDead)
            {
                if (state != State.Chase) EnterState(State.Chase);
                body.linearVelocity = Vector2.zero;
                return;
            }

            switch (state)
            {
                case State.Chase: TickChase(); break;
                case State.Windup: TickWindup(); break;
                case State.Charge: TickCharge(); break;
                case State.Recover: TickRecover(); break;
            }
        }

        private void TickChase()
        {
            if (!FindPlayer())
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 delta = player.position - transform.position;
            float distance = delta.magnitude;
            if (distance > detectionRange)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            if (distance <= chargeTriggerRange && Time.time >= chargeReadyAt)
            {
                chargeDirection = delta.sqrMagnitude > 0f ? delta.normalized : Vector2.right;
                EnterState(State.Windup);
                return;
            }

            body.linearVelocity = delta.normalized * moveSpeed;
        }

        private void TickWindup()
        {
            body.linearVelocity = Vector2.zero;
            UpdateWarningLine();
            if (Time.time >= stateEndsAt) EnterState(State.Charge);
        }

        private void TickCharge()
        {
            body.linearVelocity = chargeDirection * chargeSpeed;
            if (!hitThisCharge && FindPlayer() &&
                Vector2.Distance(player.position, transform.position) <= hitRadius &&
                player.GetComponent<Health>() is Health target &&
                target.TryDamage(chargeDamage, Faction.Enemy, gameObject))
            {
                hitThisCharge = true;
                // 부딪히면 돌진을 끝내 플레이어를 멀리 밀어내지 않는다.
                EnterRecover();
                return;
            }

            if (Time.time >= stateEndsAt) EnterRecover();
        }

        private void TickRecover()
        {
            body.linearVelocity = Vector2.zero;
            if (Time.time >= stateEndsAt) EnterState(State.Chase);
        }

        private void EnterRecover()
        {
            chargeReadyAt = Time.time + chargeCooldown;
            EnterState(State.Recover);
        }

        private void EnterState(State next)
        {
            state = next;
            switch (next)
            {
                case State.Windup: stateEndsAt = Time.time + windupTime; break;
                case State.Charge: stateEndsAt = Time.time + chargeDuration; hitThisCharge = false; break;
                case State.Recover: stateEndsAt = Time.time + recoverTime; break;
            }

            SetTint(next == State.Windup);
            warningLine.enabled = next == State.Windup;
            if (next == State.Windup) UpdateWarningLine();
            if (next == State.Recover || next == State.Chase) body.linearVelocity = Vector2.zero;
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

        private void UpdateWarningLine()
        {
            Vector3 start = transform.position;
            Vector3 end = start + (Vector3)(chargeDirection * chargeSpeed * chargeDuration);
            warningLine.SetPosition(0, start);
            warningLine.SetPosition(1, end);
            if (spriteRenderer != null) warningLine.sortingOrder = spriteRenderer.sortingOrder - 1;
        }

        private LineRenderer CreateWarningLine()
        {
            GameObject lineObject = new GameObject("Charge Warning");
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = line.endWidth = warningLineWidth;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = warningLineColor;
            line.enabled = false;
            return line;
        }

        private void SetTint(bool windup)
        {
            if (spriteRenderer == null) return;
            if (!baseColorCaptured)
            {
                baseColor = spriteRenderer.color;
                baseColorCaptured = true;
            }

            spriteRenderer.color = windup ? windupTint : baseColor;
        }
    }
}
