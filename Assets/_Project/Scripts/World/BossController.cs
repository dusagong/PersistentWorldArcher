using System.Collections;
using System.Collections.Generic;
using PersistentWorld.Combat;
using PersistentWorld.Core;
using UnityEngine;

namespace PersistentWorld.World
{
    // 보스 (기획서 기준)
    // - 일반 패턴 5종(돌진, 탄막, 유도탄, 지진, 디버프 구체)을 patternInterval(3초)마다 랜덤으로 재생한다.
    // - 패턴 사이에는 가까운 플레이어와 거리를 두며 천천히 움직인다.
    // - HP가 phaseTwoHealthRatio 이하가 되면 페이즈 전환 패턴(30초, 조각상 4개, 십자가 안전지대, 즉사기) 후 2페이즈로 들어간다.
    // - 피해가 들어가는 범위는 BossTelegraph로 먼저 빨갛게 보여 준다.
    [RequireComponent(typeof(Health))]
    public sealed class BossController : MonoBehaviour
    {
        private enum Pattern { Charge, Barrage, Homing, Earthquake, Debuff }
        public enum TestStartPhase { Phase1, PhaseTransition, Phase2 }

        private const float PlayerRadius = .3f;

        [Header("테스트")]
        [SerializeField, Tooltip("테스트용. 실제 씬에서는 Phase1로 둘 것. PhaseTransition = 바로 페이즈 전환 시작, Phase2 = 전환 없이 2페이즈부터")]
        private TestStartPhase testStartPhase = TestStartPhase.Phase1;

        [Header("아레나 (보스 이동 가능 범위)")]
        [SerializeField] private Vector2 arenaCenter = Vector2.zero;
        [SerializeField] private Vector2 arenaHalfSize = new Vector2(8.6f, 4.6f);

        [Header("진행")]
        [SerializeField] private float patternInterval = 3f;
        [SerializeField] private float moveSpeed = 2.2f;
        [SerializeField] private float preferredDistance = 4.5f;
        [SerializeField, Range(.1f, .9f)] private float phaseTwoHealthRatio = .5f;
        [SerializeField] private Color phaseTwoColor = new Color(.95f, .1f, .4f);

        [Header("2페이즈 난이도 (돌진 경로 경고 시간은 1·2페이즈 동일)")]
        [SerializeField, Tooltip("2페이즈 패턴 간격(초)")] private float phaseTwoPatternInterval = 2.2f;
        [SerializeField, Tooltip("2페이즈 이동·돌진·탄 속도 배율")] private float phaseTwoSpeedMultiplier = 1.4f;
        [SerializeField, Tooltip("2페이즈 경고 시간 배율 (돌진 경로 제외)")] private float phaseTwoWarnMultiplier = .7f;

        [Header("돌진")]
        [SerializeField] private float chargeWarnTime = .5f;
        [SerializeField, Range(0f, 1f)] private float chargePathAlpha = .4f;
        [SerializeField] private float chargeSpeed = 28f;
        [SerializeField] private float chargeWidth = 1.8f;
        [SerializeField] private int chargeDamage = 22;
        [SerializeField] private int phaseTwoChargeCount = 3;
        [SerializeField, Tooltip("벽에 튕겨 나갈 때 벽면과 이루는 최소 각도. 벽을 타고 미끄러지지 않게 한다.")]
        private float minBounceAngle = 25f;
        [SerializeField, Tooltip("튕길 때 반사 각도에서 이 범위 안의 방향 중 이전 경로와 덜 겹치고 플레이어를 지나가는 쪽을 고른다")]
        private float bounceSearchAngle = 70f;

        [Header("탄막")]
        [SerializeField] private int barrageDamage = 10;
        [SerializeField] private float barrageSpeed = 7.5f;
        [SerializeField] private float barrageWarnTime = .4f;
        [SerializeField] private float barrageVolleyInterval = .45f;
        [SerializeField] private int phaseOneVolleys = 3;
        [SerializeField] private float phaseOneSpread = 20f;
        [SerializeField] private int phaseTwoVolleys = 4;
        [SerializeField] private float phaseTwoSpread = 30f;

        [Header("유도탄")]
        [SerializeField] private int homingDamage = 15;
        [SerializeField, Tooltip("일반 플레이어 이동 속도와 동일 (ArcherController moveSpeed = 4)")]
        private float homingSpeed = 4f;
        [SerializeField] private float homingLifetime = 5f;
        [SerializeField] private float homingExplosionRadius = 1f;

        [Header("지진")]
        [SerializeField] private float quakeRadius = 4.5f;
        [SerializeField, Tooltip("지진 때 슬라임처럼 뛰어오르는 높이")] private float quakeJumpHeight = 1.6f;
        [SerializeField] private float quakeWarnTime = 1.5f;
        [SerializeField] private int quakeDamage = 18;

        [Header("디버프 구체 (탄막보다 크기 5배, 속도 1.5배 느림)")]
        [SerializeField] private int debuffDamage = 8;
        [SerializeField] private float debuffWarnTime = .6f;

        [Header("페이즈 전환")]
        [SerializeField] private float transitionDuration = 30f;
        [SerializeField] private int statueHealth = 60;
        [SerializeField] private Vector2[] statueOffsets =
        {
            new Vector2(-5.5f, 3.2f), new Vector2(5.5f, 3.2f), new Vector2(-5.5f, -3.2f), new Vector2(5.5f, -3.2f)
        };
        [SerializeField, Tooltip("십자가 중심에서 팔 끝까지 길이")] private float safeZoneArmLength = 1.6f;
        [SerializeField] private float safeZoneArmWidth = 1f;
        [SerializeField] private int instantDeathDamage = 9999999;

        [Header("표시")]
        [SerializeField] private Color telegraphColor = new Color(1f, .12f, .1f);
        [SerializeField] private Color projectileColor = new Color(1f, .25f, .2f);
        [SerializeField] private Color debuffColor = new Color(.65f, .2f, 1f);
        [SerializeField] private Color statueColor = new Color(.55f, .6f, .7f);
        [SerializeField] private Color safeZoneColor = new Color(.3f, .95f, 1f);

        private readonly List<BossTelegraph> telegraphs = new List<BossTelegraph>();
        private readonly List<GameObject> transitionObjects = new List<GameObject>();
        private Health health;
        private Collider2D bodyCollider;
        private bool phaseTwo;
        private bool performing;
        private bool inTransition;
        private bool hasLastPattern;
        private Pattern lastPattern;
        private bool inQuakeJump;
        private Vector3 quakeBaseScale;
        private Vector2 quakeGround;

        // 다른 담당자가 패턴/페이즈 전환 동안 true로 유지할 수 있다. 이 동안 넉백이 막힌다.
        public bool PatternLocked { get; set; }
        public bool IsPerformingPattern => PatternLocked || performing;
        public bool IsPhaseTwo => phaseTwo;
        public bool IsInPhaseTransition => inTransition;
        public string CurrentPattern { get; private set; } = string.Empty;

        private Vector2 Position => transform.position;
        private float SpeedScale => phaseTwo ? phaseTwoSpeedMultiplier : 1f;
        private float Warn(float seconds) => phaseTwo ? seconds * phaseTwoWarnMultiplier : seconds;

        private void Awake()
        {
            BossEncounter.Begin();
            health = GetComponent<Health>();
            bodyCollider = GetComponent<Collider2D>();
        }

        private void OnEnable()
        {
            health.Died += OnDied;
            StartCoroutine(Brain());
        }

        private void OnDisable()
        {
            health.Died -= OnDied;
            StopAllCoroutines();
            Cleanup();
        }

        private void OnDestroy() => Cleanup();

        private void OnDied(Health _)
        {
            StopAllCoroutines();
            Cleanup();
        }

        private void Cleanup()
        {
            ClearTelegraphs();
            if (inQuakeJump)
            {
                transform.localScale = quakeBaseScale;
                transform.position = quakeGround;
                inQuakeJump = false;
            }
            foreach (GameObject obj in transitionObjects)
                if (obj != null) Destroy(obj);
            transitionObjects.Clear();
            performing = false;
            inTransition = false;
            CurrentPattern = string.Empty;
            if (bodyCollider != null) bodyCollider.isTrigger = false;
        }

        // ---------- 진행 ----------

        private IEnumerator Brain()
        {
            yield return new WaitForSeconds(1f);
            if (testStartPhase == TestStartPhase.Phase2)
            {
                phaseTwo = true;
                if (GetComponent<SpriteRenderer>() is SpriteRenderer sprite) sprite.color = phaseTwoColor;
            }
            else if (testStartPhase == TestStartPhase.PhaseTransition)
            {
                yield return PhaseTransition();
            }
            float nextPatternAt = Time.time;
            while (!health.IsDead)
            {
                if (!phaseTwo && health.Current <= health.Maximum * phaseTwoHealthRatio)
                {
                    yield return PhaseTransition();
                    nextPatternAt = Time.time + 1f;
                    continue;
                }

                if (Time.time < nextPatternAt || AlivePlayers().Count == 0)
                {
                    Drift();
                    yield return null;
                    continue;
                }

                Pattern next = ChoosePattern();
                lastPattern = next;
                hasLastPattern = true;
                nextPatternAt = Time.time + (phaseTwo ? phaseTwoPatternInterval : patternInterval);
                CurrentPattern = next.ToString();
                performing = true;
                yield return Run(next);
                performing = false;
                CurrentPattern = string.Empty;
                nextPatternAt = Mathf.Max(nextPatternAt, Time.time + .4f);
            }
        }

        private Pattern ChoosePattern()
        {
            Pattern next;
            do next = (Pattern)Random.Range(0, 5);
            while (hasLastPattern && next == lastPattern);
            return next;
        }

        private IEnumerator Run(Pattern pattern)
        {
            switch (pattern)
            {
                case Pattern.Charge: return Charge();
                case Pattern.Barrage: return Barrage();
                case Pattern.Homing: return Homing();
                case Pattern.Earthquake: return Earthquake();
                default: return DebuffOrb();
            }
        }

        // 패턴 사이: 가까운 플레이어와 거리를 두고 옆으로 천천히 돈다
        private void Drift()
        {
            Transform nearest = NearestPlayer();
            if (nearest == null) return;
            if (GetComponent<CombatStatus>() is CombatStatus status && status.IsDisplaced) return;
            Vector2 away = Position - (Vector2)nearest.position;
            if (away.sqrMagnitude < .01f) away = Vector2.up;
            away.Normalize();
            Vector2 goal = (Vector2)nearest.position + away * preferredDistance + Vector2.Perpendicular(away) * Mathf.Sin(Time.time * .8f) * 1.5f;
            transform.position = Vector2.MoveTowards(Position, ClampToArena(goal), moveSpeed * SpeedScale * Time.deltaTime);
        }

        // ---------- 패턴 ----------

        // 돌진: 1페이즈 1회 / 2페이즈 3회(벽에 튕겨 나감). 돌진 전(chargeWarnTime)에 모든 경로를 투명도 40% 빨간색으로 보여 준다.
        private IEnumerator Charge()
        {
            Transform target = RandomPlayer();
            if (target == null) yield break;
            int count = phaseTwo ? phaseTwoChargeCount : 1;
            var points = new List<Vector2> { Position };
            Vector2 dir = (Vector2)target.position - Position;
            dir = dir.sqrMagnitude > .01f ? dir.normalized : Vector2.right;
            for (int i = 0; i < count; i++)
            {
                Vector2 end = CastToWall(points[points.Count - 1], dir, out Vector2 normal);
                points.Add(end);
                if (i < count - 1) dir = ChooseBounce(end, dir, normal, points);
            }

            var paths = new List<BossTelegraph>();
            for (int i = 0; i < count; i++)
                paths.Add(Rect(points[i], points[i + 1], chargeWidth, chargeWarnTime).Steady(chargePathAlpha));
            yield return new WaitForSeconds(chargeWarnTime);

            if (bodyCollider != null) bodyCollider.isTrigger = true;
            for (int i = 0; i < count && !health.IsDead; i++)
            {
                Fire(paths[i]);
                var hit = new HashSet<Health>();
                Vector2 end = points[i + 1];
                while (Vector2.Distance(Position, end) > .05f && !health.IsDead)
                {
                    transform.position = Vector2.MoveTowards(Position, end, chargeSpeed * SpeedScale * Time.deltaTime);
                    Vector2 now = Position;
                    HitPlayers(p => Vector2.Distance(p, now) <= chargeWidth * .5f + PlayerRadius, chargeDamage, hit);
                    yield return null;
                }
                yield return new WaitForSeconds(.12f);
            }
            if (bodyCollider != null) bodyCollider.isTrigger = false;
        }

        // 탄막: 랜덤 플레이어 1명에게 3발(가운데, 양옆 ±각도)씩 연속 발사. 1페이즈 3회 ±20도 / 2페이즈 4회 ±30도
        private IEnumerator Barrage()
        {
            Transform target = RandomPlayer();
            int volleys = phaseTwo ? phaseTwoVolleys : phaseOneVolleys;
            float spread = phaseTwo ? phaseTwoSpread : phaseOneSpread;
            for (int v = 0; v < volleys && !health.IsDead; v++)
            {
                if (target == null || target.GetComponent<Health>().IsDead) target = RandomPlayer();
                if (target == null) yield break;
                Vector2 aim = (Vector2)target.position - Position;
                float baseAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
                float warn = Warn(v == 0 ? barrageWarnTime : barrageWarnTime * .6f);
                var lines = new List<BossTelegraph>();
                var dirs = new List<Vector2>();
                foreach (float offset in new[] { -spread, 0f, spread })
                {
                    Vector2 d = Direction(baseAngle + offset);
                    dirs.Add(d);
                    lines.Add(Rect(Position, Position + d * 7f, .22f, warn));
                }
                yield return new WaitForSeconds(warn);
                foreach (BossTelegraph line in lines) Fire(line);
                foreach (Vector2 d in dirs) Shoot(d, barrageSpeed * SpeedScale, 2.5f, barrageDamage, projectileColor);
                yield return new WaitForSeconds(barrageVolleyInterval / SpeedScale);
            }
        }

        // 유도탄: 1페이즈 랜덤 1명에게 1개 / 2페이즈 모든 플레이어에게 1개씩. 부딪히거나 5초 뒤 폭발
        private IEnumerator Homing()
        {
            var targets = phaseTwo ? AlivePlayers() : new List<Transform> { RandomPlayer() };
            float warn = Warn(.5f);
            BossTelegraph cast = Circle(Position, 1f, warn);
            yield return new WaitForSeconds(warn);
            Fire(cast);
            foreach (Transform target in targets)
                if (target != null)
                    BossHomingMissile.Spawn(Position, target, gameObject, homingDamage, homingSpeed, homingLifetime,
                        homingExplosionRadius, projectileColor, telegraphColor);
            yield return new WaitForSeconds(.5f);
        }

        // 지진: 보스 주변 일정 범위 안의 캐릭터에게 피해 (1·2페이즈 동일)
        // 경고 앞부분은 납작하게 움츠리고, 뒷부분은 슬라임처럼 뛰어올라 경고가 끝나는 순간 내려찍는다.
        private IEnumerator Earthquake()
        {
            float warn = Warn(quakeWarnTime);
            Vector2 center = Position;
            BossTelegraph area = Circle(center, quakeRadius, warn);
            quakeBaseScale = transform.localScale;
            quakeGround = center;
            inQuakeJump = true;

            float crouch = warn * .45f;
            float start = Time.time;
            while (Time.time < start + crouch && !health.IsDead)
            {
                SetSquash(1f + .3f * (Time.time - start) / crouch);
                yield return null;
            }

            float air = warn - crouch;
            start = Time.time;
            while (Time.time < start + air && !health.IsDead)
            {
                float k = (Time.time - start) / air;
                transform.position = center + Vector2.up * (4f * quakeJumpHeight * k * (1f - k));
                SetSquash(k < .5f ? .78f : .9f); // 올라갈 땐 길쭉하게
                yield return null;
            }

            transform.position = center;
            Fire(area);
            BossTelegraph.Shockwave(center, quakeRadius, telegraphColor);
            HitPlayers(p => Vector2.Distance(p, center) <= quakeRadius + PlayerRadius, quakeDamage);

            start = Time.time;
            while (Time.time < start + .25f)
            {
                SetSquash(Mathf.Lerp(1.45f, 1f, (Time.time - start) / .25f)); // 착지 충격으로 납작했다가 복구
                yield return null;
            }
            transform.localScale = quakeBaseScale;
            inQuakeJump = false;
            yield return new WaitForSeconds(.1f);
        }

        // amount > 1: 납작하게(가로로 넓게), < 1: 길쭉하게
        private void SetSquash(float amount) =>
            transform.localScale = new Vector3(quakeBaseScale.x * amount, quakeBaseScale.y / amount, quakeBaseScale.z);

        // 디버프 구체: 탄막보다 5배 크고 1.5배 느림. 맞으면 피해 + 10초간 받는 피해 2배, 주는 피해 1.5배
        private IEnumerator DebuffOrb()
        {
            Transform target = RandomPlayer();
            if (target == null) yield break;
            Vector2 aim = (Vector2)target.position - Position;
            Vector2 dir = aim.sqrMagnitude > .01f ? aim.normalized : Vector2.right;
            float warn = Warn(debuffWarnTime);
            BossTelegraph line = Track(BossTelegraph.Rect(Position, Position + dir * 8f, .9f, warn, debuffColor));
            yield return new WaitForSeconds(warn);
            Fire(line);
            Projectile orb = Shoot(dir, barrageSpeed * SpeedScale / 1.5f, 3.5f, debuffDamage, debuffColor);
            orb.HitRadius *= 5f;
            orb.InflictsVulnerability = true;
            orb.GetComponent<PlaceholderVisual>().Configure(debuffColor, new Vector2(.65f, .65f));
            yield return new WaitForSeconds(.3f);
        }

        // 페이즈 전환: 30초 동안 보스 정지. 조각상 4개를 모두 부수면 그중 1곳에 십자가 안전지대가 생긴다.
        // 30초 뒤 즉사기 발동, 십자가 위에 있는 플레이어만 살아남는다.
        private IEnumerator PhaseTransition()
        {
            phaseTwo = true;
            inTransition = true;
            performing = true;
            CurrentPattern = "PhaseTransition";
            ClearTelegraphs();
            if (GetComponent<SpriteRenderer>() is SpriteRenderer sprite) sprite.color = phaseTwoColor;

            var statues = new List<Health>();
            var statuePositions = new List<Vector2>();
            foreach (Vector2 offset in statueOffsets)
            {
                Vector2 pos = ClampToArena(arenaCenter + offset);
                statuePositions.Add(pos);
                statues.Add(CreateStatue(pos));
            }

            TextMesh countdown = CreateLabel();
            GameObject safeZone = null;
            Vector2 safeCenter = Vector2.zero;
            BossTelegraph finalWarning = null;
            float endsAt = Time.time + transitionDuration;
            while (Time.time < endsAt)
            {
                if (health.IsDead) yield break;
                int broken = 0;
                foreach (Health statue in statues)
                    if (statue == null || statue.IsDead) broken++;

                if (safeZone == null && broken == statues.Count && statuePositions.Count > 0)
                {
                    safeCenter = statuePositions[Random.Range(0, statuePositions.Count)];
                    safeZone = CreateSafeZone(safeCenter);
                }

                float remaining = endsAt - Time.time;
                if (finalWarning == null && remaining <= 3f)
                {
                    Vector2 half = arenaHalfSize + Vector2.one * 1.5f;
                    finalWarning = Rect(arenaCenter - new Vector2(half.x, 0f), arenaCenter + new Vector2(half.x, 0f), half.y * 2f, remaining);
                }

                // 보스가 화면 밖에 있어도 보이도록 화면 위쪽 가운데에 고정
                Camera cam = Camera.main;
                countdown.transform.position = cam != null
                    ? (Vector2)cam.ViewportToWorldPoint(new Vector3(.5f, .8f, 0f))
                    : Position + Vector2.up * 1.6f;
                countdown.text = safeZone != null
                    ? $"즉사기 {Mathf.CeilToInt(remaining)}초\n십자가 마법진 위로!"
                    : $"즉사기 {Mathf.CeilToInt(remaining)}초\n조각상 {broken}/{statues.Count}";
                yield return null;
            }

            Fire(finalWarning);
            foreach (Transform player in AlivePlayers())
            {
                bool safe = safeZone != null && InsideCross(player.position, safeCenter);
                if (!safe) player.GetComponent<Health>().TryDamage(instantDeathDamage, Faction.Enemy, gameObject);
            }

            yield return new WaitForSeconds(.8f);
            foreach (GameObject obj in transitionObjects)
                if (obj != null) Destroy(obj);
            transitionObjects.Clear();
            inTransition = false;
            performing = false;
            CurrentPattern = string.Empty;
            yield return new WaitForSeconds(1f);
        }

        // ---------- 페이즈 전환 오브젝트 ----------

        private Health CreateStatue(Vector2 position)
        {
            GameObject statue = new GameObject("Boss Statue");
            statue.transform.position = position;
            statue.AddComponent<PlaceholderVisual>().Configure(statueColor, new Vector2(.9f, 1.4f));
            statue.AddComponent<BoxCollider2D>().size = Vector2.one;
            statue.AddComponent<WorldDepthSort>();
            Health statueHealthComponent = statue.AddComponent<Health>();
            statueHealthComponent.Configure(Faction.Enemy, statueHealth);
            CombatFeedback.Attach(statueHealthComponent);
            transitionObjects.Add(statue);
            return statueHealthComponent;
        }

        private GameObject CreateSafeZone(Vector2 center)
        {
            GameObject root = new GameObject("Safe Cross");
            root.transform.position = center;
            float length = safeZoneArmLength * 2f;
            CreateBar(root.transform, new Vector2(length, safeZoneArmWidth));
            CreateBar(root.transform, new Vector2(safeZoneArmWidth, length));
            transitionObjects.Add(root);
            return root;
        }

        private void CreateBar(Transform parent, Vector2 size)
        {
            GameObject bar = new GameObject("Bar");
            bar.transform.SetParent(parent, false);
            bar.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = bar.AddComponent<SpriteRenderer>();
            renderer.sprite = CombatFeedback.Square;
            renderer.color = new Color(safeZoneColor.r, safeZoneColor.g, safeZoneColor.b, .5f);
            renderer.sortingOrder = -450;
        }

        private bool InsideCross(Vector2 point, Vector2 center)
        {
            Vector2 d = point - center;
            float half = safeZoneArmWidth * .5f;
            return (Mathf.Abs(d.x) <= safeZoneArmLength && Mathf.Abs(d.y) <= half) ||
                   (Mathf.Abs(d.y) <= safeZoneArmLength && Mathf.Abs(d.x) <= half);
        }

        private TextMesh CreateLabel()
        {
            GameObject obj = new GameObject("Phase Countdown");
            TextMesh text = obj.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font;
            text.fontSize = 64;
            text.characterSize = .07f;
            text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 5000;
            transitionObjects.Add(obj);
            return text;
        }

        // ---------- 도우미 ----------

        private List<Transform> AlivePlayers()
        {
            var result = new List<Transform>();
            foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
                if (player.GetComponent<Health>() is Health h && !h.IsDead) result.Add(player.transform);
            return result;
        }

        private Transform RandomPlayer()
        {
            List<Transform> players = AlivePlayers();
            return players.Count == 0 ? null : players[Random.Range(0, players.Count)];
        }

        private Transform NearestPlayer()
        {
            Transform best = null;
            float bestDistance = float.MaxValue;
            foreach (Transform player in AlivePlayers())
            {
                float d = Vector2.Distance(player.position, Position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = player;
                }
            }
            return best;
        }

        private void HitPlayers(System.Predicate<Vector2> inside, int damage, HashSet<Health> alreadyHit = null)
        {
            foreach (Transform player in AlivePlayers())
            {
                Health h = player.GetComponent<Health>();
                if (alreadyHit != null && alreadyHit.Contains(h)) continue;
                if (!inside(player.position)) continue;
                if (h.TryDamage(damage, Faction.Enemy, gameObject)) alreadyHit?.Add(h);
            }
        }

        private Projectile Shoot(Vector2 direction, float speed, float lifetime, int damage, Color color) =>
            Projectile.Spawn(Position, direction, damage, Faction.Enemy, gameObject, color, speed, lifetime);

        // 벽·구조물(WorldObstacle)에 닿을 때까지의 끝 지점과 벽면 방향
        private Vector2 CastToWall(Vector2 start, Vector2 dir, out Vector2 normal)
        {
            float radius = bodyCollider != null ? Mathf.Min(bodyCollider.bounds.extents.x, bodyCollider.bounds.extents.y) * .8f : .7f;
            RaycastHit2D best = default;
            float bestDistance = float.MaxValue;
            foreach (RaycastHit2D hit in Physics2D.CircleCastAll(start, radius, dir, 40f))
            {
                if (hit.collider == null || hit.collider.isTrigger || hit.collider == bodyCollider) continue;
                if (hit.collider.GetComponentInParent<WorldObstacle>() == null) continue;
                if (hit.distance < .05f) continue;
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    best = hit;
                }
            }

            if (best.collider != null)
            {
                normal = best.normal;
                return start + dir * best.distance;
            }
            return RayToArena(start, dir, out normal);
        }

        // 벽에 맞고 튕겨 나가는 방향. 벽면에 너무 붙어 미끄러지면 최소 각도만큼 벽에서 떨어지게 꺾는다.
        private Vector2 Bounce(Vector2 dir, Vector2 normal)
        {
            Vector2 next = Vector2.Reflect(dir, normal).normalized;
            float fromWall = 90f - Vector2.Angle(next, normal);
            if (fromWall < minBounceAngle)
            {
                float side = Mathf.Sign(normal.x * next.y - normal.y * next.x);
                if (side == 0f) side = 1f;
                next = Quaternion.Euler(0f, 0f, side * (90f - minBounceAngle)) * normal;
            }
            return next;
        }

        // 다음 돌진 방향: 반사 각도 주변 후보 중 이전 경로와 가장 덜 겹치고, 플레이어를 지나가는 방향을 고른다
        private Vector2 ChooseBounce(Vector2 from, Vector2 dir, Vector2 normal, List<Vector2> path)
        {
            Vector2 reflected = Bounce(dir, normal);
            Vector2 best = reflected;
            float bestScore = float.MinValue;
            float minLeave = Mathf.Sin(minBounceAngle * Mathf.Deg2Rad);
            List<Transform> players = AlivePlayers();
            // 1차: 반사 각도 ±bounceSearchAngle / 2차(모서리 등에서 1차가 모두 막힌 경우): 벽에서 멀어지는 모든 방향
            for (int pass = 0; pass < 2 && bestScore < 0f; pass++) // 1차 후보가 전부 겹치면 2차로 넓혀 찾는다
            for (float offset = pass == 0 ? -bounceSearchAngle : -180f; offset <= (pass == 0 ? bounceSearchAngle : 180f) + .01f; offset += pass == 0 ? 10f : 15f)
            {
                Vector2 candidate = (Quaternion.Euler(0f, 0f, offset) * reflected).normalized;
                if (Vector2.Dot(candidate, normal) < (pass == 0 ? minLeave : .1f)) continue; // 벽을 타고 미끄러지는 방향 제외
                Vector2 end = CastToWall(from, candidate, out _);
                if (Vector2.Distance(from, end) < 2.5f) continue; // 너무 짧은 돌진 제외

                float cover = 0f;
                for (int k = 1; k <= 6; k++)
                    cover += Mathf.Min(DistanceToPath(Vector2.Lerp(from, end, k / 6f), path), 4f);
                cover /= 6f;

                float playerBonus = 0f;
                foreach (Transform player in players)
                    if (DistanceToSegment(player.position, from, end) <= chargeWidth * .5f + 1f) playerBonus = 1.5f;

                // 이전 경로와 거의 겹치면(평균 거리 2.0 미만) 크게 감점. 플레이어 보너스는 새 공간을 덮을 때만 준다.
                bool overlaps = cover < 2f;
                float score = cover * 2f + (overlaps ? -5f : playerBonus) - Mathf.Abs(offset) * .01f - pass * 1f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            // 그래도 갈 곳이 없으면 아레나 중앙 쪽으로
            if (bestScore == float.MinValue)
            {
                Vector2 toCenter = arenaCenter - from;
                return toCenter.sqrMagnitude > .01f ? toCenter.normalized : reflected;
            }
            return best;
        }

        private static float DistanceToPath(Vector2 point, List<Vector2> path)
        {
            float min = float.MaxValue;
            for (int i = 0; i < path.Count - 1; i++) min = Mathf.Min(min, DistanceToSegment(point, path[i], path[i + 1]));
            return min;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(point, a + ab * t);
        }

        private Vector2 RayToArena(Vector2 start, Vector2 dir, out Vector2 normal)
        {
            Vector2 min = arenaCenter - arenaHalfSize;
            Vector2 max = arenaCenter + arenaHalfSize;
            float t = 40f;
            normal = -dir;
            if (dir.x > .0001f && (max.x - start.x) / dir.x < t) { t = (max.x - start.x) / dir.x; normal = Vector2.left; }
            if (dir.x < -.0001f && (min.x - start.x) / dir.x < t) { t = (min.x - start.x) / dir.x; normal = Vector2.right; }
            if (dir.y > .0001f && (max.y - start.y) / dir.y < t) { t = (max.y - start.y) / dir.y; normal = Vector2.down; }
            if (dir.y < -.0001f && (min.y - start.y) / dir.y < t) { t = (min.y - start.y) / dir.y; normal = Vector2.up; }
            return start + dir * Mathf.Max(0f, t);
        }

        private Vector2 ClampToArena(Vector2 point) => new Vector2(
            Mathf.Clamp(point.x, arenaCenter.x - arenaHalfSize.x, arenaCenter.x + arenaHalfSize.x),
            Mathf.Clamp(point.y, arenaCenter.y - arenaHalfSize.y, arenaCenter.y + arenaHalfSize.y));

        private BossTelegraph Circle(Vector2 center, float radius, float warn) =>
            Track(BossTelegraph.Circle(center, radius, warn, telegraphColor));

        private BossTelegraph Rect(Vector2 start, Vector2 end, float width, float warn) =>
            Track(BossTelegraph.Rect(start, end, width, warn, telegraphColor));

        private BossTelegraph Track(BossTelegraph telegraph)
        {
            telegraphs.Add(telegraph);
            return telegraph;
        }

        private void Fire(BossTelegraph telegraph)
        {
            if (telegraph == null) return;
            telegraphs.Remove(telegraph);
            telegraph.Fire();
        }

        private void ClearTelegraphs()
        {
            foreach (BossTelegraph telegraph in telegraphs)
                if (telegraph != null) telegraph.Cancel();
            telegraphs.Clear();
        }

        private static Vector2 Direction(float degrees) =>
            new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
    }
}
