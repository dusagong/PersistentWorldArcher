using PersistentWorld.Combat;
using UnityEngine;

namespace PersistentWorld.World
{
    // 일반 몬스터1 / 레어 몬스터: 슬라임처럼 다가와서 움츠렸다가(착지 지점 빨간 원 경고)
    // 포물선으로 뛰어올라 플레이어가 있던 자리에 내려찍는다. 착지 후 잠깐 멈춰 반격할 틈이 생긴다.
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class SlimeJumpEnemyController : MonoBehaviour
    {
        private enum State { Chase, Windup, Airborne, Recover }

        [Header("추적")]
        [SerializeField] private float detectionRange = 8f;
        [SerializeField] private float moveSpeed = 1.8f;
        [SerializeField] private float jumpTriggerRange = 5f;
        [SerializeField] private float stopDistance = 1.6f;

        [Header("점프 공격")]
        [SerializeField] private float windupTime = .6f;
        [SerializeField] private float maxJumpDistance = 5.5f;
        [SerializeField] private float jumpDuration = .5f;
        [SerializeField] private float jumpHeight = 1.4f;
        [SerializeField] private int landingDamage = 14;
        [SerializeField] private float landingRadius = .9f;
        [SerializeField] private float recoverTime = .8f;
        [SerializeField] private float jumpCooldown = 2f;

        [Header("표시")]
        [SerializeField] private Color windupTint = new Color(1f, .35f, .3f);
        [SerializeField] private Color warningColor = new Color(1f, .12f, .1f);

        private static Sprite circleSprite;

        private Rigidbody2D body;
        private Health health;
        private Collider2D bodyCollider;
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer warning;
        private SpriteRenderer shadow;
        private Transform player;
        private State state = State.Chase;
        private float stateStartedAt;
        private float jumpReadyAt;
        private Vector2 jumpStart;
        private Vector2 jumpEnd;
        private Vector3 baseScale;
        private Color baseColor;
        private bool visualsCaptured;

        public bool IsWindingUp => state == State.Windup;
        public bool IsAirborne => state == State.Airborne;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            bodyCollider = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            jumpReadyAt = Time.time + .5f;
        }

        private void Start()
        {
            baseScale = transform.localScale;
            if (spriteRenderer != null) baseColor = spriteRenderer.color;
            visualsCaptured = true;
            warning = CreateCircle("Jump Warning", -400);
            shadow = CreateCircle("Jump Shadow", -401);
            shadow.color = new Color(0f, 0f, 0f, .35f);
        }

        private void OnDestroy()
        {
            if (warning != null) Destroy(warning.gameObject);
            if (shadow != null) Destroy(shadow.gameObject);
        }

        private void FixedUpdate()
        {
            if (health.IsDead)
            {
                body.linearVelocity = Vector2.zero;
                HideMarkers();
                return;
            }

            if (state != State.Airborne && GetComponent<CombatStatus>() is CombatStatus status && status.IsDisplaced)
            {
                if (state == State.Windup) Enter(State.Recover);
                return;
            }

            switch (state)
            {
                case State.Chase: TickChase(); break;
                case State.Windup: TickWindup(); break;
                case State.Airborne: TickAirborne(); break;
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

            if (distance <= jumpTriggerRange && Time.time >= jumpReadyAt)
            {
                jumpStart = body.position;
                float landDistance = Mathf.Min(maxJumpDistance, Mathf.Max(0f, distance - landingRadius * .5f));
                jumpEnd = jumpStart + delta.normalized * landDistance;
                Enter(State.Windup);
                return;
            }

            // 점프 대기 중에는 너무 붙지 않고 거리를 둔다
            if (distance <= stopDistance)
            {
                body.linearVelocity = Vector2.zero;
                SetSquash(1f);
                return;
            }

            // 통통 튀며 다가가는 느낌
            float hop = Mathf.Abs(Mathf.Sin(Time.time * 7f));
            body.linearVelocity = delta.normalized * moveSpeed * Mathf.Lerp(.5f, 1.4f, hop);
            SetSquash(1f + .08f * (1f - hop));
        }

        private void TickWindup()
        {
            body.linearVelocity = Vector2.zero;
            float t = Elapsed / windupTime;
            SetSquash(1f + .35f * t); // 점점 납작하게 움츠림
            float blink = .5f + .5f * Mathf.Sin(Elapsed * Mathf.Lerp(8f, 30f, t));
            warning.color = new Color(warningColor.r, warningColor.g, warningColor.b, Mathf.Lerp(.25f, .7f, blink));
            if (Elapsed >= windupTime) Enter(State.Airborne);
        }

        private void TickAirborne()
        {
            float t = Mathf.Clamp01(Elapsed / jumpDuration);
            Vector2 ground = Vector2.Lerp(jumpStart, jumpEnd, t);
            float height = 4f * jumpHeight * t * (1f - t);
            body.linearVelocity = Vector2.zero;
            body.MovePosition(ground + Vector2.up * height);
            shadow.transform.position = ground;
            float shadowScale = landingRadius * 2f * Mathf.Lerp(.6f, 1f, 1f - height / Mathf.Max(.01f, jumpHeight));
            shadow.transform.localScale = new Vector3(shadowScale, shadowScale * .5f, 1f);
            SetSquash(.85f); // 공중에서는 길쭉하게
            warning.color = new Color(warningColor.r, warningColor.g, warningColor.b, .6f);
            if (t >= 1f) Land();
        }

        private void TickRecover()
        {
            body.linearVelocity = Vector2.zero;
            float t = Mathf.Clamp01(Elapsed / recoverTime);
            SetSquash(Mathf.Lerp(1.3f, 1f, t)); // 착지 충격으로 납작했다가 복구
            if (Elapsed >= recoverTime) Enter(State.Chase);
        }

        private void Land()
        {
            body.position = jumpEnd;
            if (FindPlayer() && Vector2.Distance(player.position, jumpEnd) <= landingRadius + .3f)
                player.GetComponent<Health>()?.TryDamage(landingDamage, Faction.Enemy, gameObject);
            Enter(State.Recover);
        }

        private void Enter(State next)
        {
            if (next == State.Recover) jumpReadyAt = Time.time + jumpCooldown;
            state = next;
            stateStartedAt = Time.time;
            if (bodyCollider != null) bodyCollider.isTrigger = next == State.Airborne;
            if (spriteRenderer != null && visualsCaptured) spriteRenderer.color = next == State.Windup ? windupTint : baseColor;

            bool showLanding = next == State.Windup || next == State.Airborne;
            warning.enabled = showLanding;
            shadow.enabled = next == State.Airborne;
            if (showLanding)
            {
                warning.transform.position = jumpEnd;
                warning.transform.localScale = Vector3.one * landingRadius * 2f;
            }
            if (next == State.Chase) SetSquash(1f);
        }

        private float Elapsed => Time.time - stateStartedAt;

        private void HideMarkers()
        {
            if (warning != null) warning.enabled = false;
            if (shadow != null) shadow.enabled = false;
        }

        // amount > 1: 납작하게(가로로 넓게), < 1: 길쭉하게
        private void SetSquash(float amount)
        {
            if (!visualsCaptured) return;
            transform.localScale = new Vector3(baseScale.x * amount, baseScale.y / amount, baseScale.z);
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

        private static SpriteRenderer CreateCircle(string name, int sortingOrder)
        {
            GameObject obj = new GameObject(name);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = CircleSprite;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = false;
            return renderer;
        }

        private static Sprite CircleSprite
        {
            get
            {
                if (circleSprite != null) return circleSprite;
                const int size = 64;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                float r = size * .5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r)) / r;
                    float a = d > 1f ? 0f : d > .88f ? 1f : .5f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                texture.Apply();
                circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
                return circleSprite;
            }
        }
    }
}
