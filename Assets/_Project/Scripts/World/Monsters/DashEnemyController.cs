using PersistentWorld.Combat;
using UnityEngine;

namespace PersistentWorld.World
{
    // 돌진형 몬스터 (기획서 원안): 플레이어에게 일정 거리까지 접근 → 잠시 멈칫(빨간 경고선) → 짧은 거리 돌진.
    // 부딪히거나 돌진이 끝나면 잠깐 멈춰 서서 반격할 틈이 생긴다.
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class DashEnemyController : MonoBehaviour
    {
        private enum State { Chase, Windup, Dash, Recover }

        [Header("추적")]
        [SerializeField] private float detectionRange = 7f;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float dashTriggerRange = 3f;

        [Header("돌진")]
        [SerializeField] private float windupTime = .5f;
        [SerializeField] private float dashSpeed = 10f;
        [SerializeField] private float dashDuration = .3f;
        [SerializeField] private int dashDamage = 12;
        [SerializeField] private float hitRadius = .8f;
        [SerializeField] private float recoverTime = .7f;
        [SerializeField] private float dashCooldown = 1.8f;

        [Header("경고 표시")]
        [SerializeField] private Color windupTint = new Color(1f, .2f, .2f);
        [SerializeField] private Color warningLineColor = new Color(1f, .15f, .15f, .55f);
        [SerializeField] private float warningLineWidth = .3f;

        private Rigidbody2D body;
        private Health health;
        private SpriteRenderer spriteRenderer;
        private LineRenderer warningLine;
        private Color baseColor;
        private bool baseColorCaptured;
        private Transform player;
        private State state = State.Chase;
        private float stateEndsAt;
        private float dashReadyAt;
        private Vector2 dashDirection;
        private bool hitThisDash;

        public bool IsDashing => state == State.Dash;
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
                if (state == State.Windup || state == State.Dash) EnterRecover();
                return;
            }

            if (health.IsDead)
            {
                if (state != State.Chase) Enter(State.Chase);
                body.linearVelocity = Vector2.zero;
                return;
            }

            switch (state)
            {
                case State.Chase: TickChase(); break;
                case State.Windup: TickWindup(); break;
                case State.Dash: TickDash(); break;
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

            if (distance <= dashTriggerRange && Time.time >= dashReadyAt)
            {
                dashDirection = delta.sqrMagnitude > 0f ? delta.normalized : Vector2.right;
                Enter(State.Windup);
                return;
            }

            body.linearVelocity = distance > 1.2f ? delta.normalized * moveSpeed : Vector2.zero;
        }

        private void TickWindup()
        {
            body.linearVelocity = Vector2.zero;
            UpdateWarningLine();
            if (Time.time >= stateEndsAt) Enter(State.Dash);
        }

        private void TickDash()
        {
            body.linearVelocity = dashDirection * dashSpeed;
            if (!hitThisDash && FindPlayer() &&
                Vector2.Distance(player.position, transform.position) <= hitRadius &&
                player.GetComponent<Health>() is Health target &&
                target.TryDamage(dashDamage, Faction.Enemy, gameObject))
            {
                hitThisDash = true;
                EnterRecover();
                return;
            }

            if (Time.time >= stateEndsAt) EnterRecover();
        }

        private void TickRecover()
        {
            body.linearVelocity = Vector2.zero;
            if (Time.time >= stateEndsAt) Enter(State.Chase);
        }

        private void EnterRecover()
        {
            dashReadyAt = Time.time + dashCooldown;
            Enter(State.Recover);
        }

        private void Enter(State next)
        {
            state = next;
            switch (next)
            {
                case State.Windup: stateEndsAt = Time.time + windupTime; break;
                case State.Dash: stateEndsAt = Time.time + dashDuration; hitThisDash = false; break;
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
            Vector3 end = start + (Vector3)(dashDirection * dashSpeed * dashDuration);
            warningLine.SetPosition(0, start);
            warningLine.SetPosition(1, end);
            if (spriteRenderer != null) warningLine.sortingOrder = spriteRenderer.sortingOrder - 1;
        }

        private LineRenderer CreateWarningLine()
        {
            GameObject lineObject = new GameObject("Dash Warning");
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
