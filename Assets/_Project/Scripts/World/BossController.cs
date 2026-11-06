using System.Collections;
using System.Collections.Generic;
using PersistentWorld.Combat;
using PersistentWorld.Core;
using UnityEngine;

namespace PersistentWorld.World
{
    // 보스 (기획서 기준)
    // - 일반 패턴 5종(돌진, 탄막, 유도탄, 지진, 디버프 구체)을 patternInterval(3초)마다 랜덤으로 재생한다.
    //   돌진은 chargeMinDistance보다 멀리 있는 플레이어가 있을 때(chargePriority 확률로 우선), 지진은 quakeTriggerDistance 안으로 붙었을 때만 고른다.
    // - 패턴 사이에는 가까운 플레이어와 거리를 두며 천천히 움직인다.
    // - HP가 phaseTwoHealthRatio 이하가 되면 페이즈 전환 패턴(20초, 조각상 4개, 십자가 안전지대, 즉사기) 후 2페이즈로 들어간다.
    // - 피해가 들어가는 범위는 BossTelegraph로 먼저 빨갛게 보여 준다.
    // - BossSpriteAnimator가 붙어 있으면 패턴 타이밍에 맞춰 그림 동작을 지시하고 탄 그림을 바꾼다. 판정·타이밍은 그림과 무관하다.
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
        [SerializeField, Tooltip("이 거리보다 멀리 있는 플레이어가 있을 때만 돌진하고, 그중 한 명을 노린다")]
        private float chargeMinDistance = 5.5f;
        [SerializeField, Range(0f, 1f), Tooltip("멀리 있는 플레이어가 있을 때 다른 패턴보다 돌진을 먼저 고를 확률 (직전 패턴이 돌진이면 적용 안 함)")]
        private float chargePriority = .7f;
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
        [SerializeField, Tooltip("플레이어가 이 거리 안으로 붙었을 때만 지진을 쓴다. 평소 유지 거리(preferredDistance)보다 작게 둘 것")]
        private float quakeTriggerDistance = 3f;
        [SerializeField, Tooltip("지진 때 슬라임처럼 뛰어오르는 높이")] private float quakeJumpHeight = 1.6f;
        [SerializeField] private float quakeWarnTime = 1.5f;
        [SerializeField] private int quakeDamage = 18;
        [SerializeField, Tooltip("내려찍을 때 흙과 함께 튀는 돌 조각 그림 (비우면 흙덩이만)")] private Sprite[] quakeDebrisSprites;
        [SerializeField, Tooltip("내려찍을 때 화면 흔들림 세기 (0이면 끔)")] private float quakeShake = .45f;

        [Header("디버프 구체 (탄막보다 크기 5배, 속도 1.5배 느림)")]
        [SerializeField] private int debuffDamage = 8;
        [SerializeField] private float debuffWarnTime = .6f;

        [Header("페이즈 전환")]
        [SerializeField] private float transitionDuration = 20f;
        [SerializeField] private int statueHealth = 60;
        [SerializeField] private Vector2[] statueOffsets =
        {
            new Vector2(-5.5f, 3.2f), new Vector2(5.5f, 3.2f), new Vector2(-5.5f, -3.2f), new Vector2(5.5f, -3.2f)
        };
        [SerializeField, Tooltip("십자가 중심에서 팔 끝까지 길이")] private float safeZoneArmLength = 1.6f;
        [SerializeField] private float safeZoneArmWidth = 1f;
        [SerializeField] private int instantDeathDamage = 9999999;
        [SerializeField, Tooltip("기둥 그림 (비우면 네모 도형). 발 위치가 피벗인 스프라이트")] private Sprite statueSprite;
        [SerializeField, Tooltip("부서진 기둥 그림 (비우면 부서질 때 사라진다)")] private Sprite statueBrokenSprite;
        [SerializeField, Tooltip("기둥 룬 빛 그림 (선택)")] private Sprite statueGlowSprite;
        [SerializeField] private Vector2 statueGlowOffset = new Vector2(.02f, .19f);
        [SerializeField] private Color statueGlowColor = new Color(0f, .875f, 1f);
        [SerializeField, Tooltip("기둥이 땅에서 솟아오르는 시간")] private float statueRiseTime = .7f;
        [SerializeField, Tooltip("페이즈 전환 동안 불러내는 잡몹 프리팹 (비우면 소환 안 함). 전환이 끝나면 사라진다")] private GameObject[] minionPrefabs;
        [SerializeField] private float minionFirstDelay = 2f;
        [SerializeField] private float minionInterval = 5f;
        [SerializeField] private int minionsPerWave = 2;
        [SerializeField, Tooltip("동시에 살아 있을 수 있는 잡몹 수")] private int maxMinions = 5;
        [SerializeField, Tooltip("잡몹이 나타나기 전 바닥 경고 시간")] private float minionWarnTime = .8f;

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
        private BossSpriteAnimator art;
        private bool phaseTwo;
        private bool performing;
        private bool inTransition;
        private bool hasLastPattern;
        private Pattern lastPattern;
        private bool inQuakeJump;
        private int summoning;
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
        private bool Animated => art != null && art.Ready;

        private void Awake()
        {
            BossEncounter.Begin();
            health = GetComponent<Health>();
            bodyCollider = GetComponent<Collider2D>();
            art = GetComponent<BossSpriteAnimator>();
        }

        private void Animate(BossClip clip, float duration, Vector2 facing = default)
        {
            if (Animated) art.Play(clip, duration, facing);
        }

        // 그림 위에서는 원래 2페이즈 색을 그대로 곱하면 너무 어두워져 옅게 섞는다
        private void TintPhaseTwo()
        {
            if (GetComponent<SpriteRenderer>() is SpriteRenderer sprite)
                sprite.color = Animated ? Color.Lerp(Color.white, phaseTwoColor, .35f) : phaseTwoColor;
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
            summoning = 0;
            performing = false;
            inTransition = false;
            if (health != null) health.Invulnerable = false;
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
                TintPhaseTwo();
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

                // 패턴 뒤 원래 자세로 돌아오는 동작이 끝날 때까지는 제자리에 서 있고 다음 패턴도 미룬다
                bool returning = Animated && art.IsReturning;
                if (returning || Time.time < nextPatternAt || AlivePlayers().Count == 0)
                {
                    if (!returning) Drift();
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

        // 멀리 떨어진 플레이어가 있으면 chargePriority 확률로 돌진을 먼저 고른다 (연속 돌진은 제외).
        // 그 외에는 거리 조건을 만족하는 패턴 중에서 직전 패턴을 빼고 고른다. 탄막·유도탄·디버프는 항상 후보라 비지 않는다.
        private Pattern ChoosePattern()
        {
            bool chargedLast = hasLastPattern && lastPattern == Pattern.Charge;
            if (!chargedLast && CanUse(Pattern.Charge) && Random.value < chargePriority) return Pattern.Charge;

            var options = new List<Pattern>();
            for (int i = 0; i < 5; i++)
                if (CanUse((Pattern)i)) options.Add((Pattern)i);
            if (hasLastPattern && options.Count > 1) options.Remove(lastPattern);
            return options[Random.Range(0, options.Count)];
        }

        private bool CanUse(Pattern pattern)
        {
            switch (pattern)
            {
                case Pattern.Charge: return PlayersFartherThan(chargeMinDistance).Count > 0;
                case Pattern.Earthquake:
                    Transform nearest = NearestPlayer();
                    return nearest != null && Vector2.Distance(nearest.position, Position) <= quakeTriggerDistance;
                default: return true;
            }
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

        // 돌진: 멀리 있는 플레이어 중 한 명에게 1페이즈 1회 / 2페이즈 3회(벽에 튕겨 나감).
        // 돌진 전(chargeWarnTime)에 모든 경로를 투명도 40% 빨간색으로 보여 준다.
        private IEnumerator Charge()
        {
            List<Transform> far = PlayersFartherThan(chargeMinDistance);
            if (far.Count == 0) yield break;
            Transform target = far[Random.Range(0, far.Count)];
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
            Animate(BossClip.ChargeWindup, chargeWarnTime, points[1] - points[0]);
            yield return new WaitForSeconds(chargeWarnTime);

            if (bodyCollider != null) bodyCollider.isTrigger = true;
            for (int i = 0; i < count && !health.IsDead; i++)
            {
                Fire(paths[i]);
                var hit = new HashSet<Health>();
                Vector2 end = points[i + 1];
                Animate(BossClip.ChargeRun, .5f, end - points[i]);
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
            Animate(BossClip.ChargeEnd, .6f);
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
                // 첫 발 전에는 대포로 변신하고, 다음 발부터는 대포 상태로 조준한다
                Animate(v == 0 ? BossClip.CannonForm : BossClip.CannonHold, v == 0 ? warn : .32f, aim);
                yield return new WaitForSeconds(warn);
                foreach (BossTelegraph line in lines) Fire(line);
                foreach (Vector2 d in dirs) UseArt(Shoot(d, barrageSpeed * SpeedScale, 2.5f, barrageDamage, projectileColor), Animated ? art.BarrageBall : null);
                float interval = barrageVolleyInterval / SpeedScale;
                Animate(BossClip.CannonFire, interval, aim);
                yield return new WaitForSeconds(interval);
            }
            Animate(BossClip.CannonReturn, .9f);
        }

        // 탄 판정은 그대로 두고 막대 도형 대신 그림을 똑바로 세워 보여 준다
        private static void UseArt(Projectile projectile, Sprite sprite)
        {
            if (sprite == null) return;
            projectile.transform.localScale = Vector3.one;
            projectile.transform.rotation = Quaternion.identity;
            SpriteRenderer renderer = projectile.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 1500;
        }

        // 유도탄: 1페이즈 랜덤 1명에게 1개 / 2페이즈 모든 플레이어에게 1개씩. 부딪히거나 5초 뒤 폭발
        private IEnumerator Homing()
        {
            var targets = phaseTwo ? AlivePlayers() : new List<Transform> { RandomPlayer() };
            float warn = Warn(.5f);
            BossTelegraph cast = Circle(Position, 1f, warn);
            Transform first = targets.Count > 0 ? targets[0] : null;
            Vector2 aim = first != null ? (Vector2)first.position - Position : Vector2.zero;
            Animate(BossClip.CannonForm, warn, aim);
            yield return new WaitForSeconds(warn);
            Fire(cast);
            Animate(BossClip.CannonFire, .5f, aim);
            foreach (Transform target in targets)
                if (target != null)
                {
                    BossHomingMissile missile = BossHomingMissile.Spawn(Position, target, gameObject, homingDamage, homingSpeed, homingLifetime,
                        homingExplosionRadius, projectileColor, telegraphColor);
                    if (Animated && art.HomingBall != null)
                    {
                        SpriteRenderer renderer = missile.GetComponent<SpriteRenderer>();
                        renderer.sprite = art.HomingBall;
                        renderer.color = Color.white;
                    }
                }
            yield return new WaitForSeconds(.5f);
            Animate(BossClip.CannonReturn, .9f);
        }

        // 지진: 보스 주변 일정 범위 안의 캐릭터에게 피해 (1·2페이즈 동일). 플레이어가 quakeTriggerDistance 안으로 붙었을 때만 고른다.
        // 경고 앞부분은 납작하게 움츠리고, 뒷부분은 슬라임처럼 뛰어올라 경고가 끝나는 순간 내려찍는다.
        private IEnumerator Earthquake()
        {
            float warn = Warn(quakeWarnTime);
            Vector2 center = Position;
            BossTelegraph area = Circle(center, quakeRadius, warn);
            if (Animated)
            {
                yield return QuakeSlam(warn, center, area);
                yield break;
            }
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
            SlamImpact(center);

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

        // 그림이 있을 때의 지진: 뛰어오르는 대신 경고 동안 두 주먹을 들어 올리고, 경고가 끝나는 순간 내려찍는다.
        private IEnumerator QuakeSlam(float warn, Vector2 center, BossTelegraph area)
        {
            art.Play(BossClip.QuakeWindup, warn);
            float start = Time.time;
            while (Time.time < start + warn && !health.IsDead) yield return null;
            if (health.IsDead) yield break;
            Fire(area);
            BossTelegraph.Shockwave(center, quakeRadius, telegraphColor);
            HitPlayers(p => Vector2.Distance(p, center) <= quakeRadius + PlayerRadius, quakeDamage);
            SlamImpact(center);
            art.Play(BossClip.QuakeSlam, .35f);
            yield return new WaitForSeconds(.35f);
            art.Play(BossClip.QuakeRecover, .6f);
        }

        // 내려찍은 자리: 흙·돌 조각이 튀고 흙먼지가 퍼지며 화면이 흔들린다 (연출 전용)
        private void SlamImpact(Vector2 center)
        {
            GroundBurst.Spawn(center, quakeRadius, quakeDebrisSprites);
            if (quakeShake > 0f) CameraFollow.Shake(quakeShake, .4f);
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
            Animate(BossClip.CannonForm, warn, dir);
            yield return new WaitForSeconds(warn);
            Fire(line);
            Animate(BossClip.CannonFire, .5f, dir);
            Projectile orb = Shoot(dir, barrageSpeed * SpeedScale / 1.5f, 3.5f, debuffDamage, debuffColor);
            orb.HitRadius *= 5f;
            orb.InflictsVulnerability = true;
            if (Animated && art.DebuffRock != null)
            {
                UseArt(orb, art.DebuffRock[0]);
                orb.gameObject.AddComponent<SpriteFrameLoop>().Play(art.DebuffRock);
            }
            else orb.GetComponent<PlaceholderVisual>().Configure(debuffColor, new Vector2(.65f, .65f));
            yield return new WaitForSeconds(.3f);
            Animate(BossClip.CannonReturn, .9f);
        }

        // 페이즈 전환: 20초 동안 보스 정지. 조각상 4개를 모두 부수면 그중 1곳에 십자가 안전지대가 생긴다.
        // 20초 뒤 즉사기 발동, 십자가 위에 있는 플레이어만 살아남는다.
        private IEnumerator PhaseTransition()
        {
            phaseTwo = true;
            inTransition = true;
            performing = true;
            health.Invulnerable = true; // 페이즈 전환 동안 보스는 피해를 받지 않는다
            CurrentPattern = "PhaseTransition";
            ClearTelegraphs();
            TintPhaseTwo();
            Animate(BossClip.Stand, 1f);

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
            float nextWaveAt = Time.time + minionFirstDelay;
            var minions = new List<GameObject>();
            while (Time.time < endsAt)
            {
                if (health.IsDead) yield break;

                // 기둥을 부수는 동안 잡몹이 계속 나와 방해한다. 즉사기 3초 전부터는 더 부르지 않는다.
                minions.RemoveAll(m => m == null);
                if (minionPrefabs != null && minionPrefabs.Length > 0 && Time.time >= nextWaveAt && endsAt - Time.time > 3f + minionWarnTime)
                {
                    nextWaveAt = Time.time + minionInterval;
                    for (int i = 0; i < minionsPerWave && minions.Count + summoning < maxMinions; i++)
                        StartCoroutine(SummonMinion(minionPrefabs[Random.Range(0, minionPrefabs.Length)], MinionSpot(), minions));
                }
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
            health.Invulnerable = false;
            CurrentPattern = string.Empty;
            if (Animated) art.PlayMove();
            yield return new WaitForSeconds(1f);
        }

        // ---------- 페이즈 전환 오브젝트 ----------

        // position은 기둥 몸통 가운데. 그림이 있으면 땅에서 솟아오르고, 부서지면 부서진 기둥으로 남는다.
        private Health CreateStatue(Vector2 position)
        {
            GameObject statue = new GameObject("Boss Statue");
            Health statueHealthComponent;
            if (statueSprite == null)
            {
                statue.transform.position = position;
                statue.AddComponent<PlaceholderVisual>().Configure(statueColor, new Vector2(.9f, 1.4f));
                statue.AddComponent<BoxCollider2D>().size = Vector2.one;
                statue.AddComponent<WorldDepthSort>();
                statueHealthComponent = statue.AddComponent<Health>();
                statueHealthComponent.Configure(Faction.Enemy, statueHealth);
            }
            else
            {
                Vector2 size = statueSprite.bounds.size;
                statue.transform.position = position - Vector2.up * size.y * .5f;
                SpriteRenderer body = statue.AddComponent<SpriteRenderer>();
                body.sprite = statueSprite;
                SpriteRenderer glow = null;
                if (statueGlowSprite != null)
                {
                    var glowObject = new GameObject("Glow");
                    glowObject.transform.SetParent(statue.transform, false);
                    glowObject.transform.localPosition = new Vector3(statueGlowOffset.x, statueGlowOffset.y, -.5f);
                    glow = glowObject.AddComponent<SpriteRenderer>();
                    glow.sprite = statueGlowSprite;
                    glow.color = statueGlowColor;
                }
                var box = statue.AddComponent<BoxCollider2D>();
                box.size = new Vector2(size.x * .9f, size.y * .9f);
                box.offset = Vector2.up * size.y * .5f;
                statue.AddComponent<WorldDepthSort>();
                statueHealthComponent = statue.AddComponent<Health>();
                statueHealthComponent.Configure(Faction.Enemy, statueHealth, false);
                statueHealthComponent.Died += _ =>
                {
                    box.enabled = false;
                    if (glow != null) glow.enabled = false;
                    if (statueBrokenSprite != null) body.sprite = statueBrokenSprite;
                    else body.enabled = false;
                };
                StartCoroutine(RiseStatue(statue.transform));
                ActorShadow.Attach(statue.transform, size.x * 1.25f, 0f, false);
            }
            statue.AddComponent<CombatStatus>().Immovable = true; // 넉백 스킬에 밀리지 않게
            CombatFeedback.Attach(statueHealthComponent);
            if (statue.GetComponent<CombatFeedback>() is CombatFeedback feedback) feedback.ShowHealthAlways(1.2f, .18f, true);
            transitionObjects.Add(statue);
            return statueHealthComponent;
        }

        // 플레이어와 너무 가깝지 않은 아레나 안 임의 지점
        private Vector2 MinionSpot()
        {
            Vector2 half = arenaHalfSize - Vector2.one;
            Vector2 spot = arenaCenter;
            for (int i = 0; i < 12; i++)
            {
                spot = arenaCenter + new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
                Transform nearest = null;
                float best = float.MaxValue;
                foreach (Transform p in AlivePlayers())
                {
                    float d = Vector2.Distance(p.position, spot);
                    if (d < best) { best = d; nearest = p; }
                }
                if (nearest == null || best >= 3f) break;
            }
            return spot;
        }

        // 바닥 경고 뒤에 잡몹이 흙먼지와 함께 튀어나온다. 전환이 끝나면 transitionObjects와 함께 사라진다.
        private IEnumerator SummonMinion(GameObject prefab, Vector2 spot, List<GameObject> minions)
        {
            summoning++; // 경고 중인 것도 세어 최대치를 넘지 않게 한다
            BossTelegraph warning = Circle(spot, .8f, minionWarnTime);
            yield return new WaitForSeconds(minionWarnTime);
            summoning--;
            Fire(warning);
            if (!inTransition) yield break;
            GameObject minion = Instantiate(prefab, spot, Quaternion.identity);
            minions.Add(minion);
            transitionObjects.Add(minion);
            if (minion.GetComponent<Health>() is Health minionHealth) CombatFeedback.Attach(minionHealth);
            GroundBurst.Spawn(spot, 1.2f, quakeDebrisSprites, 18, 8);
        }

        // 납작한 상태에서 흔들리며 길어져 살짝 넘쳤다가 제 크기로 선다. 발밑에 먼지 고리를 퍼뜨린다.
        private IEnumerator RiseStatue(Transform statue)
        {
            Vector3 basePosition = statue.position;
            BossTelegraph.Shockwave(basePosition, 1.1f, statueColor);
            float start = Time.time;
            while (statue != null && Time.time < start + statueRiseTime)
            {
                float t = (Time.time - start) / statueRiseTime;
                float height = t < .8f ? Mathf.Lerp(0f, 1.08f, t / .8f) : Mathf.Lerp(1.08f, 1f, (t - .8f) / .2f);
                statue.localScale = new Vector3(1f, Mathf.Max(.01f, height), 1f);
                statue.position = basePosition + Vector3.right * Mathf.Sin(Time.time * 70f) * .04f * (1f - t);
                yield return null;
            }
            if (statue == null) yield break;
            statue.localScale = Vector3.one;
            statue.position = basePosition;
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
                if (player.GetComponent<Health>() is Health h && !h.IsDead && PersistentWorld.Networking.NetworkGameWorld.SameArea(player.transform.position, Position)) result.Add(player.transform);
            return result;
        }

        private Transform RandomPlayer()
        {
            List<Transform> players = AlivePlayers();
            return players.Count == 0 ? null : players[Random.Range(0, players.Count)];
        }

        private List<Transform> PlayersFartherThan(float distance)
        {
            List<Transform> players = AlivePlayers();
            players.RemoveAll(p => Vector2.Distance(p.position, Position) <= distance);
            return players;
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
