using System.Collections;
using System.Linq;
using PersistentWorld.Combat;
using PersistentWorld.Player;
using UnityEngine;

namespace PersistentWorld.World
{
    // 늑대 보스의 두뇌 (지금은 혼자 하기 전용).
    // 같은 오브젝트의 BossController는 꺼 둔 채 "보스라는 표시"로만 남긴다(넉백 면역, 보스 피해 판정 등이 그걸 본다).
    //
    // 핵심 기믹 "덮치기"(잡기):
    //  1) 준비: 한 명을 노리고 돌진 경로를 경고한다. 경로는 대상을 따라다니다 돌진 직전에 고정된다.
    //  2) 돌진하며 잡기: 잡는중 4번 프레임으로 날아가 경로 위 사람을 잡는다. 회피(구르기) 중인 사람은 잡히지 않는다.
    //  3) 잡으면: 잡는중 5번~끝(들어 올림) → 잡고 흔들기(여러 번 피해) → 잡고 내려찍기(큰 피해 + 주변 충격파).
    //  4) 빗나가면: 대기 자세 그대로 벽까지 미끄러져 박고 그로기. 그로기 동안은 받는 피해가 두 배.
    // "그림자 참격": 검은 그림자로 경로를 관통하고, 지나간 경로에 X자 발톱 자국이 늦게 터진다.
    [RequireComponent(typeof(Health))]
    public sealed class WolfBossController : MonoBehaviour
    {
        [Header("등장")]
        [SerializeField] private string displayName = "이끼송곳니";
        [SerializeField] private string displayTitle = "썩은 숲의 늑대왕";
        [SerializeField, Tooltip("등장 연출 얼굴 그림 (Resources/Boss/Portraits/ 안의 파일 이름)")] private string portrait = "Wolf";

        [Header("평소 움직임")]
        [SerializeField] private float walkSpeed = 3.4f;
        [SerializeField, Tooltip("이 거리까지 다가간 뒤 멈춘다")] private float keepDistance = 5f;
        [SerializeField, Tooltip("패턴 사이 쉬는 시간")] private float patternInterval = .8f;

        [Header("2페이즈 (붉은 번개)")]
        [SerializeField, Tooltip("체력이 이 비율 이하가 되면 2페이즈로 넘어간다")] private float phaseTwoAt = .5f;
        [SerializeField, Tooltip("초록이 빨강으로 물드는 시간")] private float phaseShiftTime = 1.2f;
        [SerializeField, Tooltip("2페이즈 연계 확률")] private float phaseTwoComboChance = .85f;
        [SerializeField, Tooltip("2페이즈에서 패턴 사이 쉬는 시간 배율")] private float phaseTwoRestScale = .6f;

        [Header("연계")]
        [SerializeField, Tooltip("연계가 있는 패턴 뒤에 바로 다음 패턴으로 이어질 확률")] private float comboChance = .5f;
        [SerializeField, Tooltip("연계로 넘어가기 전 신호(번개가 확 튐) 시간")] private float comboCue = .18f;

        [Header("패턴 고르기")]
        [SerializeField, Tooltip("덮치기를 쓴 뒤 이 시간(초) 동안은 다시 쓰지 않는다")] private float pounceCooldown = 10f;
        [SerializeField, Tooltip("이 시간(초) 동안 덮치기를 안 썼으면 다음 공격은 반드시 덮치기")] private float pounceForceAfter = 20f;
        [SerializeField, Tooltip("근접 공격 전에 이 거리까지 다가간다")] private float meleeRange = 2.4f;

        [Header("단독 공격")]
        [SerializeField] private int doubleDamage = 10;
        [SerializeField] private int slashDamage = 18;
        [SerializeField] private int clawDamage = 28;
        [SerializeField, Tooltip("회전 공격: 몇 바퀴 도는지 (바퀴마다 한 번씩 맞는다)")] private int spinTurns = 3;
        [SerializeField, Tooltip("회전 공격 한 바퀴 피해")] private int spinTurnDamage = 8;
        [SerializeField, Tooltip("회전 공격 반지름")] private float spinRadius = 3.2f;
        [SerializeField, Tooltip("근접 공격: 맞기 이만큼 전에 범위가 고정된다 (그 전엔 대상을 따라간다)")] private float meleeLock = .2f;
        [SerializeField, Tooltip("근접 공격: 휘두를 때 앞으로 박차고 나가는 거리")] private float meleeStep = 1.4f;
        [Header("뇌격 방출")]
        [SerializeField] private int beamDamage = 22;
        [SerializeField, Tooltip("번개 줄기 최대 길이 (벽에서 멈춘다)")] private float beamLength = 16f;
        [SerializeField] private float beamWidth = 1.3f;
        [SerializeField, Tooltip("경고 줄이 고정된 뒤 발사까지 (이때 피해야 한다)")] private float beamLockTime = .3f;
        [SerializeField, Tooltip("번개를 뿜는 시간")] private float beamTime = .45f;
        [SerializeField, Tooltip("2페이즈: 부채꼴로 휩쓸며 뿜는다")] private bool beamSweep = true;
        [SerializeField, Tooltip("휩쓰는 각도 (전체)")] private float beamSweepAngle = 70f;
        [SerializeField, Tooltip("휩쓰는 시간")] private float beamSweepTime = .8f;
        [SerializeField, Tooltip("부채꼴 경고가 차오르는 시간 (이때 휩쓸 반대쪽으로 피한다)")] private float beamFanWarn = .55f;
        [SerializeField] private int roarDamage = 8;
        [SerializeField] private float roarRadius = 5f, roarPush = 3.5f;
        [SerializeField, Tooltip("공포 포효: 반지름 / 느려지는 시간(초, 이동 속도 절반)")] private float howlRadius = 7f, howlSlow = 1.5f;

        [Header("3연속 점프")]
        [SerializeField, Tooltip("착지 자리 범위 피해")] private int jumpDamage = 14;
        [SerializeField] private float jumpRadius = 2f;
        [SerializeField, Tooltip("공중 높이 (낮게 깔려 날아간다)")] private float jumpHeight = .8f;
        [SerializeField, Tooltip("한 번에 뛸 수 있는 최대 거리")] private float jumpMaxDistance = 9f;

        [Header("그림자 참격")]
        [SerializeField, Tooltip("검은 그림자로 경로를 지나가는 시간")] private float shadowDashTime = .14f;
        [SerializeField, Tooltip("늑대가 지나간 뒤 큰 X 잔상이 터지기까지")] private float shadowDelay = .4f;
        [SerializeField, Tooltip("노린 자리를 지나 더 관통하는 거리")] private float shadowPast = 3f;
        [SerializeField] private float shadowWidth = 2.8f;
        [SerializeField, Tooltip("발톱 자국 사이 간격")] private float markSpacing = 2f;
        [SerializeField] private float markRadius = 1.5f;
        [SerializeField] private int markDamage = 16;

        [Header("덮치기 (잡기)")]
        [SerializeField, Tooltip("경로가 대상을 따라다니는 시간")] private float aimTrackTime = .55f;
        [SerializeField, Tooltip("경로가 고정된 뒤 돌진까지 (이때 피해야 한다)")] private float aimLockTime = .3f;
        [SerializeField] private float dashSpeed = 34f;
        [SerializeField] private float slideSpeed = 24f;
        [SerializeField, Tooltip("돌진 중 잔상 간격(초)")] private float afterimageInterval = .025f;
        [SerializeField, Tooltip("노린 자리를 지나 돌진 자세로 더 달리는 거리")] private float shadowOvershoot = 1.5f;
        [SerializeField] private float pathWidth = 1.6f;
        [SerializeField, Tooltip("늑대 앞쪽에서 이 거리 안이면 잡힌다")] private float grabReach = 1.1f;
        [SerializeField] private float shakeTime = 1f;
        [SerializeField] private int shakeHits = 4, shakeDamage = 6;
        [SerializeField] private int slamDamage = 25;
        [SerializeField] private float slamRadius = 2.6f;
        [SerializeField] private int slamSplashDamage = 12;
        [SerializeField, Tooltip("1페이즈 그로기(받는 피해 2배) 시간: 벽에 박음·잡기 풀림")] private float stunTime = 3f;
        [SerializeField, Tooltip("잡고 있는 동안(잡은 순간부터 내려찍기 전까지) 이만큼 피해를 받으면 놓치고 그로기")] private int grabBreakDamage = 150;
        [SerializeField, Tooltip("덮치기 체공 높이 (땅을 박차고 날아오른다)")] private float pounceHeight = .9f;

        [Header("2페이즈 강화")]
        [SerializeField, Tooltip("덮치기가 빗나가면 벽을 박차고 한 번 더 덮친다")] private bool reboundPounce = true;
        [SerializeField, Tooltip("두 번째 덮치기: 경로 추적 시간 (짧다)")] private float reboundTrack = .22f;
        [SerializeField, Tooltip("두 번째 덮치기: 경로 고정 뒤 돌진까지")] private float reboundLock = .2f;
        [SerializeField, Tooltip("2페이즈 그로기 시간: 두 번째 덮치기까지 빗나감·잡기 풀림")] private float reboundStunTime = 2.5f;
        [SerializeField, Tooltip("그림자 참격: X가 터진 뒤 +자 자국이 한 번 더 터진다")] private bool shadowPlus = true;
        [SerializeField, Tooltip("X가 터진 뒤 +자가 터지기까지")] private float shadowPlusDelay = .6f;
        [SerializeField, Tooltip("큰 발톱: 휩쓴 자리 균열에서 늦게 번개가 솟는다")] private bool clawRift = true;
        [SerializeField, Tooltip("균열이 터지기까지")] private float clawRiftDelay = .5f;
        [SerializeField] private float clawRiftLength = 6.5f, clawRiftWidth = 1.4f;
        [SerializeField] private int clawRiftDamage = 18;

        [Header("길찾기")]
        [SerializeField, Tooltip("발밑 길막 판정 반지름 (기둥·사람과 부딪히는 크기)")] private float footRadius = .7f;
        [SerializeField, Tooltip("이만큼(초) 막혀 있으면 장애물을 뛰어넘는다")] private float stuckLeapAfter = .8f;

        [Header("전멸기 (뇌광 폭주)")]
        [SerializeField, Tooltip("2페이즈로 바뀌자마자 쓰는 전멸기. 늑대 중심으로 필드를 조각내 구역별로 번개를 내리꽂는다")] private bool useWipe = true;
        [SerializeField, Tooltip("늑대가 뛰어드는 아레나 가운데 (발 위치)")] private Vector2 wipeCenter = new Vector2(0f, -.5f);
        [SerializeField, Tooltip("필드를 몇 조각으로 나눌지 (짝수)")] private int wipeSlices = 8;
        [SerializeField, Tooltip("몇 턴 동안 쏟아지는지")] private int wipeTurns = 5;
        [SerializeField, Tooltip("첫 턴 경고 시간 (자리를 파악할 여유)")] private float wipeFirstWarn = 2.2f;
        [SerializeField, Tooltip("이후 턴 경고 시간")] private float wipeWarn = 1.6f;
        [SerializeField, Tooltip("터진 뒤 다음 경고까지")] private float wipeGap = .45f;
        [SerializeField, Tooltip("늑대 바로 주변: 모든 조각의 꼭짓점이라 매 턴 함께 터진다")] private float wipeCoreRadius = 1.8f;
        [SerializeField, Tooltip("조각 경고가 뻗는 길이 (필드 전체를 덮게)")] private float wipeReach = 30f;
        [SerializeField, Tooltip("모두 버틴 뒤 과부하 그로기(받는 피해 2배) 시간")] private float wipeGroggy = 3.5f;

        // 경고 색: 1페이즈 연두 → 2페이즈 빨강 (WolfRed 비율만큼)
        private static Color Warning => WolfRed.Tint(new Color(.55f, 1f, .2f), new Color(1f, .28f, .18f));
        private static Color Red(Color green, Color red) => WolfRed.Tint(green, red);
        private Health health;
        private WolfSpriteAnimator art;
        private Collider2D body;      // 맞는 판정 (몸 전체, 늘 트리거: 화살·스킬은 트리거도 맞힌다)
        private CircleCollider2D feet; // 길막 판정 (발밑 작은 원: 기둥·사람과 부딪히는 건 이것만)
        private float blockedFor;      // 걸으려는데 앞으로 못 나아간 시간 (오래 막히면 뛰어넘는다)
        private int slideSide = 1;     // 막혔을 때 비켜 가는 쪽 (번갈아 흔들리지 않게 기억)
        private ArcherController held;
        private Vector2 heldOffset;

        public string DisplayName => displayName;
        public string DisplayTitle => displayTitle;
        public string Portrait => portrait;
        public bool IsPerformingPattern { get; private set; }

        private void Awake()
        {
            health = GetComponent<Health>();
            art = GetComponent<WolfSpriteAnimator>();
            body = GetComponent<Collider2D>();
            if (body != null) body.isTrigger = true;
            var foot = new GameObject("Wolf Feet");
            foot.transform.SetParent(transform, false);
            feet = foot.AddComponent<CircleCollider2D>();
            if (GetComponent<BossController>() is BossController golem) golem.enabled = false;
            var status = GetComponent<CombatStatus>() ?? gameObject.AddComponent<CombatStatus>();
            status.Immovable = true;
            lastPounceAt = Time.time; // 덮치기 시간은 늑대가 나타난 때부터 잰다
            WolfRed.Set(0f); // 늘 초록에서 시작 (전역 값이라 지난 판의 빨강이 남지 않게)
        }

        // 아레나 바닥의 연두빛 장식도 2페이즈에 함께 붉게 물들도록
        // 온라인 서버에서는 그리지 않는다 (각 클라이언트가 늑대 방 지도에 직접 붙인다)
        private void Start()
        {
            if (PersistentWorld.Networking.NetworkGameWorld.Instance != null) return;
            ArenaRedShift.Attach(gameObject.scene);
            WolfBossBar.Show(this); // 혼자 하기 보스 체력바 (온라인은 파티 HUD가 그린다)
        }

        private void OnDestroy() => WolfRed.Set(0f);

        private void OnEnable()
        {
            health.Died += OnDied;
            Health.DamageApplied += OnDamaged;
            Health.DamageDealt += Count;
            StartCoroutine(Brain());
        }

        private void OnDisable()
        {
            health.Died -= OnDied;
            Health.DamageApplied -= OnDamaged;
            Health.DamageDealt -= Count;
            StopAllCoroutines();
            Release();
            EndWipe();
            ClearWarnings();
            Solid(true);
        }

        // ---------- 패턴 하나만 시험 (Play 중 Inspector의 ⋮ 메뉴) ----------
        [ContextMenu("테스트: 3연속 점프")] private void TestJump() => TestPattern("Jump");
        [ContextMenu("테스트: 회전 공격")] private void TestSpin() => TestPattern("Spin");
        [ContextMenu("테스트: 그림자 참격")] private void TestShadowSlash() => TestPattern("ShadowSlash");
        [ContextMenu("테스트: 덮치기")] private void TestPounce() => TestPattern("Pounce");
        [ContextMenu("테스트: 뇌격 방출")] private void TestBeam() => TestPattern("Beam");
        [ContextMenu("테스트: 뇌격 부채꼴 휩쓸기 (2페이즈)")] private void TestBeamSweep() => TestPattern("BeamSweep");
        [ContextMenu("테스트: 연속 할퀴기")] private void TestDouble() => TestPattern("Double");
        [ContextMenu("테스트: 참격")] private void TestSlash() => TestPattern("Slash");
        [ContextMenu("테스트: 큰 발톱")] private void TestClaw() => TestPattern("Claw");
        [ContextMenu("테스트: 바람 포효")] private void TestRoar() => TestPattern("Roar");
        [ContextMenu("테스트: 공포 포효")] private void TestHowl() => TestPattern("Howl");
        [ContextMenu("테스트: 2페이즈 전환")] private void TestPhaseTwo() => TestPattern("Phase");
        [ContextMenu("테스트: 2페이즈 덮치기 (빗나가면 한 번 더)")] private void TestPounce2() => TestPhaseTwoPattern("Pounce");
        [ContextMenu("테스트: 2페이즈 그림자 참격 (X → +)")] private void TestShadow2() => TestPhaseTwoPattern("ShadowSlash");
        [ContextMenu("테스트: 2페이즈 큰 발톱 (균열 폭발)")] private void TestClaw2() => TestPhaseTwoPattern("Claw");

        // 2페이즈 강화 패턴만 보기: 색·전멸기 없이 2페이즈 상태로만 바꿔서 쓴다
        private void TestPhaseTwoPattern(string name)
        {
            if (!Application.isPlaying) return;
            phaseTwo = true;
            WolfRed.Set(1f);
            TestPattern(name);
        }
        [ContextMenu("테스트: 전멸기 (뇌광 폭주)")] private void TestWipe() => TestPattern("Wipe");
        [ContextMenu("테스트: 색 초록으로 되돌리기")] private void TestPhaseReset() { phaseTwo = false; barFill = -1f; WolfRed.Set(0f); }
        [ContextMenu("테스트: 연계 점프→회전")] private void TestComboJump() => TestPattern("Combo:Jump");
        [ContextMenu("테스트: 연계 그림자→뇌격")] private void TestComboShadow() => TestPattern("Combo:ShadowSlash");
        [ContextMenu("테스트: 연계 할퀴기→참격")] private void TestComboDouble() => TestPattern("Combo:Double");
        [ContextMenu("테스트: 연계 바람 포효→뇌격")] private void TestComboRoar() => TestPattern("Combo:Roar");
        [ContextMenu("테스트: 연계 공포 포효→덮치기")] private void TestComboHowl() => TestPattern("Combo:Howl");

        // 하던 일을 멈추고 그 패턴만 한 번 쓴 뒤 평소처럼 돌아간다 (등장 연출 없이)
        public void TestPattern(string name)
        {
            if (!Application.isPlaying || health.IsDead) return;
            StopAllCoroutines();
            Release();
            EndWipe();
            if (airGround is Vector2 ground) { transform.position = ground; airGround = null; }
            art.SetLift(0f);
            health.Invulnerable = false;
            Solid(true);
            StartCoroutine(RunTest(name));
        }

        private IEnumerator RunTest(string name)
        {
            ArcherController target = PickTarget();
            if (name == "Phase")
                yield return PhaseShift();
            else if (name == "Wipe")
            {
                if (!phaseTwo) yield return PhaseShift();
                yield return Wipe();
            }
            else if (target != null && name.StartsWith("Combo:") && System.Enum.TryParse(name.Substring(6), out Attack first))
                yield return PerformWithCombo(first, target, 1f);
            else if (target != null)
            {
                IsPerformingPattern = true;
                switch (name)
                {
                    case "Jump": yield return TripleJump(target); break;
                    case "Spin": yield return Spin(target); break;
                    case "ShadowSlash": yield return ShadowSlash(target); break;
                    case "Pounce": yield return Pounce(target); break;
                    case "Beam": yield return Beam(target); break;
                    case "BeamSweep": yield return Beam(target, true); break;
                    case "Double": yield return DoubleClaw(target); break;
                    case "Slash": yield return Slash(target); break;
                    case "Claw": yield return BigClaw(target); break;
                    case "Roar": yield return WindRoar(); break;
                    case "Howl": yield return Howl(); break;
                }
                art.Face(null);
                IsPerformingPattern = false;
            }
            yield return Loop();
        }

        private void OnDied(Health _)
        {
            StopAllCoroutines();
            // 공중에서 쓰러지면 바닥으로 내려놓는다
            if (airGround is Vector2 ground) { transform.position = ground; airGround = null; }
            art.SetLift(0f);
            Release();
            EndWipe();
            ClearWarnings();
            Solid(true);
        }

        // 쓰러지거나 꺼질 때 바닥에 남은 경고 장판·화살표를 치운다 (안 그러면 그대로 남아 다음 판에도 보인다)
        private void ClearWarnings()
        {
            BossTelegraph.ClearNear(transform.position);
            ChargeWarning.ClearNear(transform.position);
        }

        private IEnumerator Brain()
        {
            if (PersistentWorld.Networking.NetworkGameWorld.Instance == null) BossIntro.Show(displayName, displayTitle, portrait);
            yield return new WaitForSeconds(BossIntro.Duration);
            while (BossIntro.Active) yield return null; // 로딩으로 연출이 늦어졌으면 끝까지 기다린다
            engagedAt = Time.time;
            yield return Loop();
        }

        // 쉬며 거리 맞추기 → 패턴 하나 → 반복
        private IEnumerator Loop()
        {
            while (!health.IsDead)
            {
                // 쉬는 동안 가까운 플레이어 쪽으로 걸어가 거리를 맞춘다
                // 체력이 절반 아래로 떨어지면 붉은 번개 2페이즈로 (패턴 사이에서 한 번)
                if (!phaseTwo && health.Current <= health.Maximum * phaseTwoAt)
                {
                    yield return PhaseShift();
                    if (useWipe) yield return Wipe();
                }
                float until = Time.time + patternInterval * (phaseTwo ? phaseTwoRestScale : 1f);
                blockedFor = 0f;
                while (Time.time < until && !health.IsDead)
                {
                    ArcherController near = Nearest();
                    if (near != null)
                    {
                        Vector2 to = (Vector2)near.transform.position - Feet;
                        if (to.magnitude > keepDistance) Step(to.normalized * walkSpeed * Time.deltaTime);
                        else blockedFor = 0f;
                        if (blockedFor > stuckLeapAfter) yield return LeapOver(near);
                    }
                    yield return null;
                }
                ArcherController target = PickTarget();
                if (target == null) { yield return null; continue; }
                Attack attack = Choose(target);
                yield return PerformWithCombo(attack, target, phaseTwo ? phaseTwoComboChance : comboChance);
            }
        }

        // 패턴 하나를 쓰고, 연계가 있으면 확률(chance)에 따라 쉬지 않고 다음 패턴으로 바로 잇는다 (한 번만)
        private IEnumerator PerformWithCombo(Attack attack, ArcherController target, float chance)
        {
            yield return Perform(attack, target);
            if (health.IsDead || !Combos.TryGetValue(attack, out Attack next) || Random.value >= chance) yield break;
            if (next == Attack.Pounce && PounceCooling) yield break;
            ArcherController followTarget = next == Attack.Spin ? Nearest() : (target != null && !target.GetComponent<Health>().IsDead ? target : PickTarget());
            if (followTarget == null) yield break;
            // 연계 신호: 몸에서 번개가 확 튀고 화면이 살짝 흔들린다
            WolfLightning.Burst((Vector2)transform.position + Vector2.up * .3f, 1.6f, 6, .18f);
            WolfLightning.Sparks((Vector2)transform.position + Vector2.up * .3f, 1.2f, 4);
            Shake(.12f, comboCue);
            art.Freeze(comboCue * .6f);
            yield return new WaitForSeconds(comboCue);
            yield return Perform(next, followTarget);
        }

        // 연계: 앞 패턴이 끝난 자리·상황에 맞물리는 다음 패턴
        private static readonly System.Collections.Generic.Dictionary<Attack, Attack> Combos = new System.Collections.Generic.Dictionary<Attack, Attack>
        {
            [Attack.Jump] = Attack.Spin,          // 마지막 착지 자리에서 바로 휘감는다
            [Attack.ShadowSlash] = Attack.Beam,   // 뚫고 지나간 반대편에서 돌아서서 쏜다
            [Attack.Double] = Attack.Slash,       // 할퀴고 물러난 사람을 초승달로 마무리
            [Attack.Roar] = Attack.Beam,          // 밀려난 사람을 멀리서 저격
            [Attack.Howl] = Attack.Pounce,        // 느려진 사람을 덮친다
        };

        // ---------- 2페이즈 전환 ----------
        // 포효하는 동안(무적) 몸과 번개의 초록이 빨강으로 물들고, 주변에 붉은 낙뢰가 쏟아진다.
        // 이후 연계가 더 자주 이어지고 쉬는 시간이 줄어든다.
        private bool phaseTwo;
        public bool IsPhaseTwo => phaseTwo;

        // 체력바는 페이즈마다 한 줄씩: 1페이즈 줄(전체의 위쪽 절반)을 다 깎으면 전환하며 2페이즈 줄이 꽉 차오른다.
        // 실제 체력은 하나라서 총량은 그대로다 (다시 회복하지 않는다). 전환 중에는 0에서 차오르는 연출만 보여 준다.
        private float barFill = -1f;
        private int PhaseLine => Mathf.RoundToInt(health.Maximum * phaseTwoAt);
        public void BarValues(out int current, out int max)
        {
            int line = PhaseLine;
            if (!phaseTwo) { max = Mathf.Max(1, health.Maximum - line); current = Mathf.Clamp(health.Current - line, 0, max); }
            else if (barFill >= 0f) { max = Mathf.Max(1, line); current = Mathf.RoundToInt(max * barFill); }
            else { max = Mathf.Max(1, line); current = Mathf.Clamp(health.Current, 0, max); }
        }

        private IEnumerator PhaseShift()
        {
            phaseTwo = true;
            IsPerformingPattern = true;
            health.Invulnerable = true;
            // 2페이즈 줄은 비운 채 시작해 포효하는 동안 차오른다. 전환 직전에 줄 아래로 더 깎인 만큼은 채워 꽉 찬 줄로 시작한다
            barFill = 0f;
            if (health.Current < PhaseLine) health.Heal(PhaseLine - health.Current);
            art.Face(null);
            // 변이 포효: 몸을 낮춰 힘을 모으다가(점점 세게 지직거리며) 고개를 들어 길게 울부짖는다
            float wind = art.Duration("MutateWind");
            art.Play("MutateWind");
            StartCoroutine(Crackle(wind + phaseShiftTime, 1.4f, .05f));
            float w0 = Time.time, nextRumble = 0f;
            while (Time.time - w0 < wind)
            {
                float k = (Time.time - w0) / wind;
                if (Time.time >= nextRumble)
                {
                    Shake(Mathf.Lerp(.05f, .25f, k), .12f);
                    WolfLightning.Sparks((Vector2)transform.position + Vector2.up * .3f, Mathf.Lerp(.8f, 1.6f, k), Mathf.RoundToInt(Mathf.Lerp(1f, 4f, k)));
                    nextRumble = Time.time + .1f;
                }
                yield return null;
            }
            art.Play("MutateRoar");
            Flash(.55f);
            Shake(.7f, phaseShiftTime);
            Vector2 center = transform.position;
            WolfLightning.GroundRing(Feet, 4.5f, .3f);
            BossTelegraph.Shockwave(Feet, 6f, Warning);
            // 초록 → 빨강으로 물들며 몸에서 번개가 튄다
            float t0 = Time.time, nextBurst = 0f;
            while (Time.time - t0 < phaseShiftTime)
            {
                WolfRed.Set((Time.time - t0) / phaseShiftTime);
                barFill = Mathf.SmoothStep(0f, 1f, (Time.time - t0) / phaseShiftTime);
                if (Time.time >= nextBurst)
                {
                    WolfLightning.Burst(center + Random.insideUnitCircle * 1.2f + Vector2.up * .4f, Random.Range(1.2f, 2f), 6, .2f);
                    nextBurst = Time.time + .12f;
                }
                yield return null;
            }
            WolfRed.Set(1f);
            barFill = -1f;
            Flash(.45f);
            StartCoroutine(Strikes(Feet, 5f, 7));
            WolfLightning.Erupt(Feet, 5.5f);
            yield return new WaitForSeconds(Mathf.Max(.5f, art.Duration("MutateRecover")));
            health.Invulnerable = false;
            art.Face(null);
            IsPerformingPattern = false;
        }

        // ---------- 전멸기: 뇌광 폭주 ----------
        // 2페이즈로 바뀌자마자 늑대가 아레나 가운데로 뛰어들어 바닥을 내리찍고, 필드 전체가 늑대 중심의 부채꼴 조각으로 갈라진다.
        // 매 턴 몇 조각에 경고가 차오른 뒤 낙뢰가 쏟아진다. 맞으면 즉사(구르기로도 못 피한다). 안전한 조각으로 옮겨 다니며 버틴다.
        // 턴: 반쪽 → 반대쪽 반 → 한 칸 건너 → 두 조각만 안전 → 한 조각만 안전. 끝까지 버티면 늑대가 과부하로 긴 그로기.
        private int wipeTurn;

        private IEnumerator Wipe()
        {
            IsPerformingPattern = true;
            health.Invulnerable = true;
            yield return LeapTo(wipeCenter);
            Vector2 center = Feet;
            int n = Mathf.Max(4, wipeSlices / 2 * 2);
            float width = 360f / n, offset = width * .5f; // 조각이 위·아래·좌·우·대각선을 향하게
            wipeTurn = 0;

            // 변이 포효로 바닥을 갈라 조각 경계를 보여 준다
            art.Face(null);
            art.Play("MutateCrouch");
            StartCoroutine(Crackle(art.Duration("MutateCrouch"), 1.2f, .06f));
            Shake(.15f, art.Duration("MutateCrouch"));
            yield return new WaitForSeconds(art.Duration("MutateCrouch"));
            art.Play("MutateBlast");
            Flash(.4f);
            Shake(.5f, .6f);
            WolfLightning.GroundRing(center, 4f, .3f);
            for (int i = 0; i < n; i++)
            {
                float a = (offset + i * width) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                for (float d = wipeCoreRadius; d < 14f; d += 3.5f)
                    WolfLightning.Bolt(center + dir * d, center + dir * (d + 3.5f), .16f, .45f, .5f);
            }
            WolfWipeBanner.Show(() => $"뇌광 폭주\n안전한 구역으로 피하세요 {wipeTurn}/{wipeTurns}");
            yield return new WaitForSeconds(.4f);
            health.Invulnerable = false;

            bool[] previous = null;
            for (int turn = 0; turn < wipeTurns && !health.IsDead; turn++)
            {
                bool[] burst = WipePattern(turn, n, previous);
                previous = burst;
                float warn = turn == 0 ? wipeFirstWarn : wipeWarn;
                Color color = Warning;
                var zones = new System.Collections.Generic.List<WolfSliceZone>();
                for (int i = 0; i < n; i++)
                    if (burst[i]) zones.Add(WolfSliceZone.Spawn(center, offset + i * width, offset + (i + 1) * width, wipeCoreRadius, wipeReach, warn, color));
                BossTelegraph core = BossTelegraph.Circle(center, wipeCoreRadius, warn, color);
                if (!art.CurrentClip.StartsWith("Mutate")) art.Play("MutateCrouch"); // 이미 웅크려 있으면 그대로
                StartCoroutine(Crackle(warn, 1.3f, .08f));
                // 땅이 점점 세게 울린다 (터지기 직전이 가장 세다)
                float warnStart = Time.time, nextRumble = 0f;
                while (Time.time - warnStart < warn)
                {
                    float k = (Time.time - warnStart) / warn;
                    if (Time.time >= nextRumble) { Shake(Mathf.Lerp(.03f, .2f, k * k), .12f); nextRumble = Time.time + .1f; }
                    yield return null;
                }

                // 낙뢰: 경고한 조각 전부 + 늑대 주변. 구르기 중이어도 맞는다
                art.Play("MutateBurst");
                WolfSliceZone.FireAll();
                core.Fire();
                Flash(.45f);
                Shake(.75f, .45f);
                WolfLightning.GroundRing(center, wipeCoreRadius, .2f);
                foreach (WolfSliceZone zone in zones) StartCoroutine(SliceStrikes(zone.Targets));
                var doomed = Players().Where(p => InBurst(p.transform.position, center, burst, offset, width)).ToList();
                foreach (ArcherController p in doomed) WolfLightning.Strike(p.transform.position, 6.5f, 1.2f);
                // 막 맞아 잠깐 무적인 사람도 놓치지 않게 잠시 동안 계속 시도한다
                float until = Time.time + .4f;
                while (Time.time < until)
                {
                    foreach (ArcherController p in doomed) Hurt(p, 9999, 1f, Vector2.zero);
                    yield return null;
                }
                wipeTurn = turn + 1;
                yield return new WaitForSeconds(wipeGap);
                if (Players().Length == 0) break; // 모두 쓰러졌으면 끝
            }
            WolfWipeBanner.Hide();
            if (!health.IsDead && Players().Length > 0) yield return Overload();
            else if (!health.IsDead) { art.Play("MutateRecover"); yield return new WaitForSeconds(art.Duration("MutateRecover")); }
            EndWipe();
            art.Face(null);
            IsPerformingPattern = false;
        }

        // 이번 턴에 터질 조각. previous = 앞 턴에 터진 조각 (안전 조각은 되도록 방금 터진 쪽에서 골라 매번 움직이게 한다)
        private static bool[] WipePattern(int turn, int n, bool[] previous)
        {
            var burst = new bool[n];
            if (turn == 0)
            {
                int start = Random.Range(0, n);
                for (int k = 0; k < n / 2; k++) burst[(start + k) % n] = true;
            }
            else if (turn == 1)
            {
                for (int i = 0; i < n; i++) burst[i] = !previous[i];
            }
            else if (turn == 2)
            {
                int parity = Random.Range(0, 2);
                for (int i = 0; i < n; i++) burst[i] = i % 2 == parity;
            }
            else
            {
                int safe = Mathf.Max(1, 2 - (turn - 3)); // 두 조각 → 한 조각
                var pool = Enumerable.Range(0, n).Where(i => previous == null || previous[i]).OrderBy(_ => Random.value).ToList();
                if (pool.Count < safe) pool = Enumerable.Range(0, n).OrderBy(_ => Random.value).ToList();
                for (int i = 0; i < n; i++) burst[i] = true;
                for (int k = 0; k < safe; k++) burst[pool[k]] = false;
            }
            return burst;
        }

        private bool InBurst(Vector2 p, Vector2 center, bool[] burst, float offset, float width)
        {
            Vector2 v = p - center;
            if (v.magnitude <= wipeCoreRadius) return true;
            float angle = Mathf.Repeat(Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - offset, 360f);
            return burst[Mathf.Clamp((int)(angle / width), 0, burst.Length - 1)];
        }

        // 한 조각의 표적 원마다 낙뢰가 꽂힌다
        private IEnumerator SliceStrikes(Vector2[] targets)
        {
            for (int k = 0; k < targets.Length; k++)
            {
                Vector2 at = targets[k];
                WolfLightning.Strike(at, 6f, 1f);
                if (k % 3 == 0) WolfLightning.Erupt(at, 2.6f, false);
                yield return new WaitForSeconds(Random.Range(.02f, .06f));
            }
        }

        private void EndWipe()
        {
            WolfWipeBanner.Hide();
            WolfSliceZone.ClearAll();
        }

        // 끝까지 버팀: 몸에 쌓인 번개가 터지며 쓰러져 긴 그로기
        private IEnumerator Overload()
        {
            float groggyEnd = Time.time + wipeGroggy;
            WolfWipeBanner.Show(() => $"과부하 {Mathf.Max(0, Mathf.CeilToInt(groggyEnd - Time.time))}초\n늑대가 쓰러졌습니다! 지금 공격하세요");
            art.Face(null);
            Flash(.5f);
            Shake(.6f, .5f);
            WolfLightning.Burst((Vector2)transform.position + Vector2.up * .4f, 2.4f, 10, .3f);
            WolfLightning.GroundRing(Feet, 3.5f, .25f);
            StartCoroutine(Strikes(Feet, 2.5f, 4));
            var status = GetComponent<CombatStatus>();
            status.ApplyVulnerability(wipeGroggy);
            art.Play("StunFall");
            StartCoroutine(Crackle(wipeGroggy, 1.2f, .12f));
            yield return new WaitForSeconds(wipeGroggy);
            WolfWipeBanner.Hide(); // 그로기가 끝날 때까지 알림 유지
            art.Play("StunRecover");
            yield return new WaitForSeconds(art.Duration("StunRecover") + .2f);
            status.Cleanse();
            art.Play("Idle");
        }

        // 크게 뛰어 footSpot(발 위치)에 착지한다 (피해 없음)
        private IEnumerator LeapTo(Vector2 footSpot)
        {
            Vector2 start = transform.position;
            Vector2 land = footSpot + Vector2.up * (transform.position.y - art.FeetY);
            if ((land - start).sqrMagnitude < .25f) yield break;
            Solid(false);
            float crouch = art.Duration("JumpCoil"), takeoff = art.Frames("JumpLeap", 4), air = art.Duration("JumpLeap") - takeoff;
            art.Face(land - start);
            art.Play("JumpCoil");
            StartCoroutine(Crackle(crouch, 1f));
            yield return new WaitForSeconds(crouch);
            art.Play("JumpLeap");
            yield return new WaitForSeconds(takeoff);
            Vector2 dir = (land - start).normalized;
            LaunchBurst(start, dir);
            float t0 = Time.time;
            while (Time.time - t0 < air)
            {
                float k = (Time.time - t0) / air, h = Mathf.Sin(k * Mathf.PI) * jumpHeight * 1.8f;
                Vector2 ground = Vector2.Lerp(start, land, k);
                airGround = ground;
                transform.position = ground + Vector2.up * h;
                art.SetLift(h);
                yield return null;
            }
            transform.position = land;
            airGround = null;
            art.SetLift(0f);
            Solid(true);
            Shake(.35f, .25f);
            WolfLightning.GroundRing(Feet, 2.5f);
            WolfImpactFx.Crack(land, 1.2f);
            Dust(land + Vector2.down * .4f, 1.4f);
            art.Play("JumpLand");
            yield return new WaitForSeconds(art.Duration("JumpLand"));
        }

        // 패턴 하나 실행 (근접 공격은 먼저 다가간다)
        private IEnumerator Perform(Attack attack, ArcherController target)
        {
            last = attack;
            if (attack == Attack.Pounce) lastPounceAt = Time.time;
            IsPerformingPattern = true;
            switch (attack)
            {
                case Attack.Pounce: yield return Pounce(target); break;
                case Attack.Double: yield return Approach(target, meleeRange); yield return DoubleClaw(target); break;
                case Attack.Slash: yield return Approach(target, meleeRange); yield return Slash(target); break;
                case Attack.Claw: yield return Approach(target, meleeRange); yield return BigClaw(target); break;
                case Attack.Spin: yield return Approach(target, meleeRange); yield return Spin(target); break;
                case Attack.Beam: yield return Beam(target); break;
                case Attack.Roar: yield return WindRoar(); break;
                case Attack.Howl: yield return Howl(); break;
                case Attack.ShadowSlash: yield return ShadowSlash(target); break;
                case Attack.Jump: yield return TripleJump(target); break;
            }
            art.Face(null);
            IsPerformingPattern = false;
        }

        // ---------- 패턴 고르기 ----------
        private enum Attack { Pounce, ShadowSlash, Double, Slash, Claw, Spin, Beam, Roar, Howl, Jump }
        private Attack last = Attack.Pounce;
        private float lastPounceAt = -999f;
        private bool PounceCooling => Time.time - lastPounceAt < pounceCooldown;

        // 가까우면 할퀴기 계열, 중간이면 전진·포효·그림자 참격, 멀면 원거리 압박.
        // 덮치기는 쓴 뒤 pounceCooldown초 동안 쉬고, pounceForceAfter초 동안 안 썼으면 반드시 쓴다.
        private Attack Choose(ArcherController target)
        {
            if (Time.time - lastPounceAt >= pounceForceAfter) return Attack.Pounce;
            float distance = Vector2.Distance(transform.position, target.transform.position);
            (Attack, int)[] options = distance <= 4f
                ? new[] { (Attack.Double, 3), (Attack.Slash, 3), (Attack.Claw, 2), (Attack.Spin, 2), (Attack.ShadowSlash, 2), (Attack.Roar, 1), (Attack.Jump, 1) }
                : distance <= 8f
                    ? new[] { (Attack.Beam, 3), (Attack.ShadowSlash, 3), (Attack.Jump, 3), (Attack.Roar, 2), (Attack.Howl, 1), (Attack.Pounce, 2), (Attack.Double, 1) }
                    : new[] { (Attack.Pounce, 2), (Attack.ShadowSlash, 3), (Attack.Jump, 3), (Attack.Beam, 4), (Attack.Howl, 1) };
            var pool = options.Where(o => o.Item1 != last && !(o.Item1 == Attack.Pounce && PounceCooling)).ToArray();
            int roll = Random.Range(0, pool.Sum(o => o.Item2));
            foreach (var (attack, weight) in pool) { if (roll < weight) return attack; roll -= weight; }
            return pool[0].Item1;
        }

        // 근접 공격 전: 대상 근처까지 걸어간다 (너무 오래 쫓지는 않는다)
        private IEnumerator Approach(ArcherController target, float range)
        {
            float until = Time.time + 2.2f;
            blockedFor = 0f;
            while (Time.time < until && target != null && !target.GetComponent<Health>().IsDead)
            {
                Vector2 to = (Vector2)target.transform.position - Feet;
                if (to.magnitude <= range) yield break;
                Step(to.normalized * walkSpeed * 1.6f * Time.deltaTime);
                if (blockedFor > stuckLeapAfter * .6f) { yield return LeapOver(target); yield break; }
                yield return null;
            }
        }

        // ---------- 단독 공격 ----------
        // ---------- 근접 공격 공통 ----------
        // 판정·경고는 바닥(발 높이)에서 잰다 (사람 위치도 발 기준)
        private Vector2 Feet => new Vector2(transform.position.x, art.FeetY);
        private Vector2 FootAim(ArcherController target, Vector2 fallback) =>
            target != null && !target.GetComponent<Health>().IsDead ? Aim(Feet, target) : fallback;
        private float StepReach(Vector2 dir, float distance) =>
            Mathf.Min(distance, Mathf.Max(0f, Vector2.Distance(transform.position, WallPoint(transform.position, dir)) - .2f));
        private const float StepTime = .09f;

        // 휘두르는 순간 앞으로 박차고 나간다 (벽 앞에서 멈춘다)
        private IEnumerator StepIn(Vector2 dir, float distance)
        {
            Vector2 from = transform.position, to = from + dir * distance;
            if (distance < .05f) yield break;
            art.Face(dir);
            StretchFor(dir, .22f);
            Dust(Feet - dir * .3f, 1.1f);
            Solid(false);
            float t0 = Time.time;
            while (Time.time - t0 < StepTime)
            {
                transform.position = Vector2.Lerp(from, to, (Time.time - t0) / StepTime);
                yield return null;
            }
            transform.position = to;
            Solid(true);
            Unstretch();
        }

        // 연속 할퀴기: 앞쪽 사각 범위를 두 번. 범위는 대상을 따라오다 맞기 직전 고정되고, 매번 박차고 나가며 벤다
        private IEnumerator DoubleClaw(ArcherController target)
        {
            Vector2 dir = FootAim(target, Vector2.left);
            float step = StepReach(dir, meleeStep);
            Vector2 a = Feet + dir * (step + .3f), b = Feet + dir * (step + 3.2f);
            float wind = art.Duration("DoubleWind");
            BossTelegraph first = BossTelegraph.Rect(a, b, 2.4f, wind + StepTime, Warning);
            art.Face(dir);
            art.Play("DoubleWind");
            StartCoroutine(Crackle(wind, 1f));
            float end = Time.time + wind, lockAt = end - meleeLock;
            while (Time.time < end)
            {
                if (Time.time < lockAt)
                {
                    dir = FootAim(target, dir); step = StepReach(dir, meleeStep);
                    a = Feet + dir * (step + .3f); b = Feet + dir * (step + 3.2f);
                    first.Reshape(a, b, 2.4f); art.Face(dir);
                }
                yield return null;
            }
            art.Play("Double");
            yield return StepIn(dir, step);
            first.Fire();
            WolfLightning.Claw((a + b) * .5f + Vector2.up * .5f, dir, 2.4f, .45f);
            HitArea(p => InRect(p, a, b, 2.4f), doubleDamage, .25f);

            // 2타: 다시 대상을 잰다 (조금 짧게 박찬다)
            float again = Mathf.Max(.2f, art.Frames("Double", 6) - StepTime);
            Vector2 dir2 = FootAim(target, dir);
            float step2 = StepReach(dir2, meleeStep * .7f);
            Vector2 a2 = Feet + dir2 * (step2 + .3f), b2 = Feet + dir2 * (step2 + 3.2f);
            BossTelegraph second = BossTelegraph.Rect(a2, b2, 2.4f, again + StepTime, Warning);
            end = Time.time + again; lockAt = end - meleeLock * .6f;
            while (Time.time < end)
            {
                if (Time.time < lockAt)
                {
                    dir2 = FootAim(target, dir2); step2 = StepReach(dir2, meleeStep * .7f);
                    a2 = Feet + dir2 * (step2 + .3f); b2 = Feet + dir2 * (step2 + 3.2f);
                    second.Reshape(a2, b2, 2.4f); art.Face(dir2);
                }
                yield return null;
            }
            yield return StepIn(dir2, step2);
            second.Fire();
            WolfLightning.Claw((a2 + b2) * .5f + Vector2.up * .5f, new Vector2(dir2.x, -dir2.y), 2.4f, .45f);
            HitArea(p => InRect(p, a2, b2, 2.4f), doubleDamage, .25f);
            yield return new WaitForSeconds(Mathf.Max(0f, art.Duration("Double") - art.Frames("Double", 6) - StepTime));
        }

        // 원 범위 근접 공격 공통: 준비 동안 원이 대상 쪽 앞을 따라오다 고정 → 박차고 나가며 휘두른다
        private IEnumerator CircleStrike(ArcherController target, string windClip, string clip, int hitFrame, float ahead, float radius, float stepScale,
                                          System.Action<Vector2, Vector2> effect, int damage, float shake, float crackleRadius, float crackleEvery)
        {
            Vector2 dir = FootAim(target, Vector2.left);
            float step = StepReach(dir, meleeStep * stepScale);
            Vector2 center = Feet + dir * (step + ahead);
            float wind = art.Duration(windClip), hit = art.Frames(clip, hitFrame);
            BossTelegraph zone = BossTelegraph.Circle(center, radius, wind + Mathf.Max(hit, StepTime), Warning);
            art.Face(dir);
            art.Play(windClip);
            StartCoroutine(Crackle(wind, crackleRadius, crackleEvery));
            float end = Time.time + wind, lockAt = end - meleeLock;
            while (Time.time < end)
            {
                if (Time.time < lockAt)
                {
                    dir = FootAim(target, dir); step = StepReach(dir, meleeStep * stepScale);
                    center = Feet + dir * (step + ahead);
                    zone.transform.position = center; art.Face(dir);
                }
                yield return null;
            }
            art.Play(clip);
            float t0 = Time.time;
            yield return StepIn(dir, step);
            float wait = hit - (Time.time - t0);
            if (wait > 0f) yield return new WaitForSeconds(wait);
            zone.Fire();
            effect(center, dir);
            HitArea(p => Vector2.Distance(p, center) <= radius, damage, shake);
            yield return new WaitForSeconds(Mathf.Max(0f, art.Duration(clip) - Mathf.Max(hit, StepTime)));
        }

        // 참격: 앞쪽 바닥을 초승달로 쓸어낸다
        private IEnumerator Slash(ArcherController target) =>
            CircleStrike(target, "SlashWind", "Slash", 2, 1.6f, 2.1f, 1f,
                (c, d) => WolfLightning.Crescent(c + Vector2.up * .5f, d, 4.4f), slashDamage, .3f, 1f, .12f);

        // 큰 발톱 휩쓸기: 일어서서 앞쪽을 넓게. 경고가 길고 피해가 크다
        private IEnumerator BigClaw(ArcherController target) =>
            CircleStrike(target, "ClawWind", "Claw", 3, 1.1f, 3f, .8f,
                (c, d) =>
                {
                    WolfLightning.BigClaw(c + Vector2.up * .6f, d, 5.4f);
                    WolfLightning.Erupt(c, 3.2f, false);
                    if (phaseTwo && clawRift) StartCoroutine(ClawRift(c, d));
                }, clawDamage, .45f, 1.3f, .08f);

        // 2페이즈: 휩쓴 자리를 가로질러 땅이 갈라지고, 잠시 뒤 균열을 따라 번개가 솟는다 (지연 폭발)
        private IEnumerator ClawRift(Vector2 center, Vector2 dir)
        {
            Vector2 side = new Vector2(-dir.y, dir.x);
            Vector2 a = center - side * clawRiftLength * .5f + dir * .3f, b = center + side * clawRiftLength * .5f + dir * .3f;
            float burstAt = Time.time + clawRiftDelay;
            BossTelegraph zone = BossTelegraph.Rect(a, b, clawRiftWidth, clawRiftDelay, Warning);
            WolfSlashMark.Spawn(a, b, clawRiftWidth * 1.2f, burstAt);
            for (int i = 0; i < 4; i++) WolfImpactFx.Crack(Vector2.Lerp(a, b, (i + Random.Range(.2f, .8f)) / 4f), Random.Range(.7f, 1f));
            float nextSpark = 0f;
            while (Time.time < burstAt)
            {
                if (Time.time >= nextSpark) { WolfLightning.Sparks(Vector2.Lerp(a, b, Random.value), .4f, 1); nextSpark = Time.time + .05f; }
                yield return null;
            }
            zone.Fire();
            Shake(.4f, .25f);
            EruptAlong(a, b);
            WolfLightning.Erupt(center, 3.6f, false);
            foreach (ArcherController p in Players())
            {
                if (p.IsDashing) continue;
                Vector2 pos = p.transform.position;
                if (Vector2.Distance(pos, ClosestOnSegment(a, b, pos)) <= clawRiftWidth * .5f) Hurt(p, clawRiftDamage, .4f, Vector2.zero);
            }
        }

        // 회전 베기: 준비 동안 대상 쪽으로 파고들고, 두 번째 회전은 대상 쪽으로 미끄러지며 돈다
        private IEnumerator Spin(ArcherController target)
        {
            // 웅크렸다가 팽이처럼 여러 바퀴 돌며 대상 쪽으로 파고든다. 바퀴마다 바람 궤적이 휘감는 14번 그림(한 바퀴의 5번째)에서 맞는다
            float wind = art.Duration("SpinWind"), turn = art.Duration("SpinLoop"), hitAt = art.Frames("SpinLoop", 4);
            BossTelegraph zone = BossTelegraph.Circle(Feet, spinRadius, wind + hitAt, Warning);
            art.Play("SpinWind");
            StartCoroutine(Crackle(wind, 1.1f));
            yield return SlideToward(target, zone, wind, wind, 1.2f, 4f);
            art.Play("SpinLoop");
            StartCoroutine(Crackle(spinTurns * turn, 1.3f, .05f));
            float since = hitAt; // 첫 바퀴: 돌기 시작해 첫 타격까지
            for (int i = 0; i < spinTurns && !health.IsDead; i++)
            {
                if (i > 0) zone = BossTelegraph.Circle(Feet, spinRadius, turn, Warning);
                yield return SlideToward(target, zone, since, since - meleeLock * .6f, 1.6f, 6f);
                zone.Fire();
                Vector2 at = Feet;
                WolfLightning.Arc((Vector2)transform.position, i % 2 == 0 ? Vector2.right : Vector2.left, 6f);
                WolfLightning.GroundRing(at, spinRadius * (i == spinTurns - 1 ? 1f : .8f));
                Shake(.12f, .1f);
                HitArea(p => Vector2.Distance(p, at) <= spinRadius, spinTurnDamage, .25f);
                since = turn;
            }
            // 마지막 바퀴를 마저 돌고 포효하며 멈춘다
            yield return new WaitForSeconds(turn - hitAt);
            art.Play("SpinRecover");
            yield return new WaitForSeconds(art.Duration("SpinRecover"));
        }

        // seconds 동안 대상 쪽으로 미끄러져 다가가며(최대 maxDistance, 초당 speed) 원 경고를 따라 옮긴다. lockAfter 뒤로는 멈춘다
        private IEnumerator SlideToward(ArcherController target, BossTelegraph zone, float seconds, float lockAfter, float maxDistance, float speed)
        {
            float t0 = Time.time, moved = 0f;
            while (Time.time - t0 < seconds)
            {
                if (Time.time - t0 < lockAfter && moved < maxDistance && target != null && !target.GetComponent<Health>().IsDead)
                {
                    Vector2 to = (Vector2)target.transform.position - Feet;
                    if (to.magnitude > .4f)
                    {
                        float d = Mathf.Min(speed * Time.deltaTime, maxDistance - moved, to.magnitude - .4f);
                        Vector2 before = transform.position;
                        Step(to.normalized * d);
                        moved += Vector2.Distance(before, transform.position);
                    }
                    zone.transform.position = Feet;
                }
                yield return null;
            }
        }

        // 뇌격 방출: 뒷발로 일어서 포효하며 번개를 모으는 동안 경고 줄이 대상을 따라오다 고정되고,
        // 팔을 앞으로 뻗는 순간 손끝에서 벽까지 번개 줄기가 뻗어 나간다. 멀리서 활을 쏘는 사람을 견제하는 원거리 공격.
        private IEnumerator Beam(ArcherController target, bool forceSweep = false)
        {
            if (forceSweep || (phaseTwo && beamSweep)) { yield return BeamSweep(target); yield break; }
            Vector2 start = transform.position;
            Vector2 dir = Toward(target);
            float wind = art.Duration("BeamWind"), track = Mathf.Max(0f, wind - beamLockTime);
            Vector2 end = BeamEnd(start, dir);
            art.Face(dir);
            art.Play("BeamWind");
            StartCoroutine(Crackle(wind, 1.2f, .08f));
            // ① 추적: 경고 줄이 대상을 따라온다
            BossTelegraph path = BossTelegraph.Rect(start, end, beamWidth, wind, Warning);
            float until = Time.time + track;
            while (Time.time < until && target != null && !target.GetComponent<Health>().IsDead)
            {
                dir = Toward(target);
                end = BeamEnd(start, dir);
                path.Reshape(start, end, beamWidth);
                art.Face(dir);
                yield return null;
            }
            // ② 고정: 줄이 밝게 번쩍이며 멈춘다. 이때 옆으로 빠지거나 구르기로 넘어야 한다
            path.Cancel();
            BossTelegraph locked = BossTelegraph.Rect(start, end, beamWidth, beamLockTime, Red(new Color(.95f, 1f, .6f), new Color(1f, .75f, .6f)));
            Shake(.05f, beamLockTime);
            yield return new WaitForSeconds(beamLockTime);
            locked.Fire();

            // ③ 발사: 크게 벌린 입에서 번개가 줄기째 지직거리며 뿜어진다. 한 사람은 한 번만 맞는다
            art.Play("Beam");
            Shake(.35f, beamTime);
            var struck = new System.Collections.Generic.HashSet<ArcherController>();
            float t0 = Time.time, nextBolt = 0f;
            bool firstBolt = true; // 첫 줄기만 화면을 번쩍
            Vector2 lastPaw = start + dir * .9f + Vector2.up * .4f;
            while (Time.time - t0 < beamTime)
            {
                Vector2 paw = art.TryGrip(out Vector2 grip) ? grip : lastPaw;
                lastPaw = paw;
                if (Time.time >= nextBolt)
                {
                    // 입 높이 그대로 바닥 판정과 나란히 그린다 (입가에서 번개가 튄다)
                    Vector2 lift = new Vector2(0f, paw.y - start.y);
                    WolfLightning.Beam(paw, end + lift, .12f, firstBolt ? .3f : 0f);
                    firstBolt = false;
                    if (Random.value < .5f) WolfLightning.Beam(paw, end + lift + Random.insideUnitCircle * .35f, .1f);
                    WolfLightning.Burst(end + lift * .3f, 1f, 4, .14f);
                    WolfLightning.Sparks(paw, .35f, 2);
                    nextBolt = Time.time + .06f;
                }
                foreach (ArcherController p in Players())
                {
                    if (struck.Contains(p) || p.IsDashing) continue;
                    Vector2 pos = p.transform.position;
                    if (Vector2.Distance(pos, ClosestOnSegment(start, end, pos)) > beamWidth * .5f) continue;
                    if (Hurt(p, beamDamage, .5f, dir * .9f)) { struck.Add(p); art.Freeze(.05f); }
                }
                yield return null;
            }
            art.Play("BeamRecover");
            yield return new WaitForSeconds(art.Duration("BeamRecover"));
        }

        // 2페이즈 뇌격: 대상을 노리다 멈추면 부채꼴 경고가 휩쓸 방향으로 차오르고,
        // 입에서 뿜은 번개 줄기가 부채꼴을 한쪽 끝에서 반대쪽 끝까지 훑는다. 휩쓸기 시작하는 쪽 바깥이나 줄기 뒤로 돌아 피한다.
        private IEnumerator BeamSweep(ArcherController target)
        {
            Vector2 start = transform.position;
            Vector2 dir = Toward(target);
            float wind = art.Duration("BeamWind");
            art.Face(dir);
            art.Play("BeamWind");
            StartCoroutine(Crackle(wind + beamFanWarn, 1.2f, .07f));
            // ① 추적: 가운데 줄이 대상을 따라온다
            BossTelegraph path = BossTelegraph.Rect(start, BeamEnd(start, dir), beamWidth, wind, Warning);
            float until = Time.time + wind;
            while (Time.time < until && target != null && !target.GetComponent<Health>().IsDead)
            {
                dir = Toward(target);
                path.Reshape(start, BeamEnd(start, dir), beamWidth);
                art.Face(dir);
                yield return null;
            }
            path.Cancel();
            // ② 부채꼴 경고: 휩쓸 방향(무작위 쪽)으로 차오른다
            float aim = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, side = Random.value < .5f ? 1f : -1f;
            float from = aim - side * beamSweepAngle * .5f, to = aim + side * beamSweepAngle * .5f;
            WolfSliceZone fan = WolfSliceZone.Sweep(start, from, to, .6f, beamLength, beamFanWarn, Warning);
            Shake(.06f, beamFanWarn);
            yield return new WaitForSeconds(beamFanWarn);
            if (fan != null) Destroy(fan.gameObject);

            // ③ 휩쓸기: 줄기가 부채꼴을 훑는다. 한 사람은 한 번만 맞는다 (구르기 중이면 안 맞는다)
            art.Play("Beam");
            Shake(.3f, beamSweepTime);
            var struck = new System.Collections.Generic.HashSet<ArcherController>();
            float t0 = Time.time, nextBolt = 0f, previous = from;
            bool firstBolt = true;
            Vector2 lastPaw = start + dir * .9f + Vector2.up * .4f;
            while (Time.time - t0 < beamSweepTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, (Time.time - t0) / beamSweepTime);
                float angle = Mathf.Lerp(from, to, k);
                Vector2 d = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Vector2 end = BeamEnd(start, d);
                art.Face(d);
                Vector2 paw = art.TryGrip(out Vector2 grip) ? grip : lastPaw;
                lastPaw = paw;
                if (Time.time >= nextBolt)
                {
                    Vector2 lift = new Vector2(0f, paw.y - start.y);
                    WolfLightning.Beam(paw, end + lift, .1f, firstBolt ? .3f : 0f);
                    firstBolt = false;
                    WolfLightning.Burst(end + lift * .3f, .9f, 4, .12f);
                    WolfLightning.Sparks(paw, .35f, 2);
                    // 훑고 지나간 바닥이 지직거린다
                    WolfLightning.GroundSpark(Vector2.Lerp(start, end, Random.Range(.3f, .9f)) + Vector2.down * .3f, d, 1.4f);
                    nextBolt = Time.time + .05f;
                }
                float reach = Vector2.Distance(start, end);
                foreach (ArcherController p in Players())
                {
                    if (struck.Contains(p) || p.IsDashing) continue;
                    Vector2 v = (Vector2)p.transform.position - start;
                    // 지금 줄기에 닿았거나, 이번 프레임에 줄기가 그 사람을 훑고 지나갔으면 맞는다
                    bool touching = Vector2.Distance(p.transform.position, ClosestOnSegment(start, end, p.transform.position)) <= beamWidth * .5f;
                    float pa = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg, swept = Mathf.DeltaAngle(previous, angle), toP = Mathf.DeltaAngle(previous, pa);
                    bool passed = v.magnitude <= reach && Mathf.Abs(swept) > .01f && Mathf.Sign(toP) == Mathf.Sign(swept) && Mathf.Abs(toP) <= Mathf.Abs(swept);
                    if ((touching || passed) && Hurt(p, beamDamage, .5f, d * .9f)) { struck.Add(p); art.Freeze(.04f); }
                }
                previous = angle;
                yield return null;
            }
            art.Play("BeamRecover");
            yield return new WaitForSeconds(art.Duration("BeamRecover"));
        }

        private Vector2 BeamEnd(Vector2 from, Vector2 dir) =>
            from + dir * Mathf.Min(beamLength, Vector2.Distance(from, WallPoint(from, dir)));

        // 바람 포효: 넓은 원 안을 밀어내고 피해 (구르기로 통과하면 안 맞는다)
        private IEnumerator WindRoar()
        {
            Vector2 center = transform.position;
            float wind = art.Duration("RoarWind"), hit = art.Frames("Roar", 1);
            BossTelegraph zone = BossTelegraph.Circle(center, roarRadius, wind + hit, Warning);
            art.Play("RoarWind");
            StartCoroutine(Crackle(wind, 1.2f, .1f));
            yield return new WaitForSeconds(wind);
            art.Play("Roar");
            yield return new WaitForSeconds(hit);
            zone.Fire();
            BossTelegraph.Shockwave(center, roarRadius, Warning);
            WolfLightning.GroundRing(new Vector2(center.x, art.FeetY), roarRadius, .2f);
            Shake(.3f, .4f);
            foreach (ArcherController p in Players())
            {
                Vector2 away = (Vector2)p.transform.position - center;
                if (p.IsDashing || away.magnitude > roarRadius) continue;
                p.GetComponent<Health>().TryDamage(roarDamage, Faction.Enemy, gameObject);
                p.GetComponent<CombatStatus>()?.Knockback(away.sqrMagnitude < .01f ? Vector2.right : away.normalized, roarPush, .25f);
            }
            yield return new WaitForSeconds(art.Duration("Roar") - hit);
        }

        // 공포 포효: 주변 플레이어가 잠시 느려진다 (피해 없음)
        private IEnumerator Howl()
        {
            Vector2 center = transform.position;
            float wind = art.Duration("HowlWind"), hit = art.Frames("Howl", 1);
            BossTelegraph zone = BossTelegraph.Circle(center, howlRadius, wind + hit, Red(new Color(.6f, .9f, .5f), new Color(.95f, .55f, .5f))).Steady(.18f);
            art.Play("HowlWind");
            yield return new WaitForSeconds(wind);
            art.Play("Howl");
            yield return new WaitForSeconds(hit);
            zone.Fire();
            BossTelegraph.Shockwave(center, howlRadius, Red(new Color(.6f, .9f, .5f), new Color(.95f, .55f, .5f)));
            StartCoroutine(Strikes(center, howlRadius * .85f, 5));
            Shake(.2f, .6f);
            foreach (ArcherController p in Players())
                if (Vector2.Distance(p.transform.position, center) <= howlRadius) p.GetComponent<CombatStatus>()?.ApplySlow(howlSlow);
            yield return new WaitForSeconds(art.Duration("Howl") - hit);
        }

        // 3연속 점프: 낮고 멀리 덮치듯 뛰어 착지 자리에 범위 피해. 세 번을 매번 다른 사람에게 (혼자면 같은 사람에게 세 번).
        // 웅크리는 동안 착지 경고가 대상을 따라다니다 뛰는 순간 그 자리에 고정된다.
        // 첫 점프만 길게 웅크리고, 2·3번째는 닿자마자 짧게 다시 웅크려 연달아 뛴다.
        private Vector2? airGround;
        private IEnumerator TripleJump(ArcherController first)
        {
            ArcherController[] order = Players().OrderBy(p => p == first ? 0 : 1).ThenBy(_ => Random.value).ToArray();
            if (order.Length == 0) yield break;
            Solid(false);
            for (int i = 0; i < 3 && !health.IsDead; i++)
            {
                ArcherController target = order[i % order.Length];
                if (target == null || target.GetComponent<Health>().IsDead) target = Nearest();
                if (target == null) break;
                string coil = i == 0 ? "JumpWind" : "JumpCoil";
                float crouch = art.Duration(coil), takeoff = art.Frames("JumpLeap", 4), air = art.Duration("JumpLeap") - takeoff;
                Vector2 start = transform.position;
                Vector2 land = LandingFor(start, target.transform.position);
                BossTelegraph zone = BossTelegraph.Circle(land, jumpRadius, crouch + takeoff + air, Warning);
                art.Play(coil);
                StartCoroutine(Crackle(crouch, 1f));
                float until = Time.time + crouch;
                while (Time.time < until)
                {
                    if (target != null) { land = LandingFor(start, target.transform.position); zone.transform.position = land; }
                    if ((land - start).sqrMagnitude > .01f) art.Face(land - start);
                    yield return null;
                }
                // 뛰는 순간 착지 자리가 고정된다
                art.Play("JumpLeap");
                yield return new WaitForSeconds(takeoff);
                Vector2 dir = (land - start).sqrMagnitude > .001f ? (land - start).normalized : (Vector2.left);
                LaunchBurst(start, dir);
                float t0 = Time.time, nextLine = 0f;
                while (Time.time - t0 < air)
                {
                    float k = (Time.time - t0) / air, h = Mathf.Sin(k * Mathf.PI) * jumpHeight;
                    Vector2 ground = Vector2.Lerp(start, land, k);
                    airGround = ground;
                    transform.position = ground + Vector2.up * h;
                    art.SetLift(h);
                    if (Time.time >= nextLine) { SpeedLines(dir, Red(new Color(.9f, 1f, .75f, .55f), new Color(1f, .8f, .75f))); nextLine = Time.time + .05f; }
                    yield return null;
                }
                transform.position = land;
                airGround = null;
                art.SetLift(0f);
                // 착지: 범위 피해 + 번개 + 바닥 균열·흙먼지
                zone.Fire();
                HitArea(p => Vector2.Distance(p, land) <= jumpRadius, jumpDamage, .35f);
                Shake(.3f, .2f);
                WolfLightning.GroundRing(new Vector2(land.x, art.FeetY), jumpRadius);
                WolfImpactFx.Crack(land, 1f);
                Dust(land + Vector2.down * .4f, 1.2f);
                bool last = i == 2;
                art.Play(last ? "JumpLand" : "JumpTouch");
                yield return new WaitForSeconds(art.Duration(last ? "JumpLand" : "JumpTouch"));
            }
            art.SetLift(0f);
            Solid(true);
        }

        // aim 쪽으로 뛸 착지 자리: 최대 거리와 벽 앞에서 멈춘다
        private Vector2 LandingFor(Vector2 from, Vector2 aim)
        {
            Vector2 d = aim - from;
            float distance = d.magnitude;
            if (distance < .01f) return from;
            Vector2 dir = d / distance;
            distance = Mathf.Min(distance, jumpMaxDistance, Vector2.Distance(from, WallPoint(from, dir)));
            return from + dir * distance;
        }

        // 범위 안의 사람(구르기 중 제외)에게 피해. 맞은 수를 돌려준다
        private int HitArea(System.Func<Vector2, bool> inside, int damage, float shake)
        {
            int count = 0;
            foreach (ArcherController p in Players())
            {
                if (p.IsDashing || !inside(p.transform.position)) continue;
                if (p.GetComponent<Health>().TryDamage(damage, Faction.Enemy, gameObject)) count++;
            }
            if (count > 0) Shake(shake, .2f);
            return count;
        }

        private static bool InRect(Vector2 p, Vector2 a, Vector2 b, float width)
        {
            Vector2 ab = b - a; float len = ab.magnitude;
            if (len < .01f) return false;
            Vector2 d = ab / len; float along = Vector2.Dot(p - a, d);
            return along >= 0f && along <= len && Mathf.Abs(Vector2.Dot(p - a, new Vector2(-d.y, d.x))) <= width * .5f;
        }

        private Vector2 Toward(ArcherController target) => Aim(transform.position, target);

        // ---------- 그림자 참격 ----------
        // 준비 → 검은 그림자로 관통(0.2초) → 반대편에서 뒤돌아봄 → 지나간 경로에 X자 발톱 자국이 늦게(0.4초) 터진다.
        // 늑대를 피했어도 자국이 터지기 전에 경로 밖으로 나가야 한다.
        private IEnumerator ShadowSlash(ArcherController target)
        {
            Vector2 start = transform.position;
            Vector2 dir = Aim(start, target);
            float toWall = Vector2.Distance(start, WallPoint(start, dir));
            float length = Mathf.Min(toWall, Vector2.Distance(start, target.transform.position) + shadowPast);
            Vector2 end = start + dir * length;
            art.Face(dir);
            float wind = art.Duration("ShadowWind");
            // 큰 X는 바닥(발 높이)에 긋는다: 몸 가운데 기준 좌표를 발밑으로 내린다 (사람 위치도 발 기준)
            Vector2 foot = new Vector2(0f, art.FeetY - transform.position.y);
            XShape x = MakeX(start + foot, end + foot, dir);
            // 준비 동작이 시작될 때부터 터질 때까지 X 두 줄 범위를 바닥에 경고한다
            float untilBurst = wind + shadowDashTime + .07f + shadowDelay;
            BossTelegraph pathZone = BossTelegraph.Rect(x.A0, x.A1, shadowWidth, untilBurst, Warning);
            BossTelegraph crossZone = BossTelegraph.Rect(x.C0, x.C1, shadowWidth, untilBurst, Warning);
            art.Play("ShadowWind");
            StartCoroutine(Crackle(wind, 1f));
            yield return new WaitForSeconds(wind);

            // 관통: 검은 그림자, 짙은 잔상
            Solid(false);
            art.Play("ShadowRun");
            LaunchBurst(start, dir);
            StretchFor(dir, .35f);
            float t0 = Time.time, nextGhost = 0f, nextLine = 0f;
            while (Time.time - t0 < shadowDashTime)
            {
                transform.position = Vector2.Lerp(start, end, (Time.time - t0) / shadowDashTime);
                if (Time.time >= nextGhost) { Afterimage(.6f, new Color(.08f, .12f, .06f)); nextGhost = Time.time + afterimageInterval; }
                if (Time.time >= nextLine)
                {
                    SpeedLines(dir, new Color(.15f, .2f, .12f, .85f));
                    if (Random.value < .4f) WolfLightning.Sparks((Vector2)transform.position - dir * 1.2f + Vector2.up * .3f, .4f, 1);
                    nextLine = Time.time + .02f;
                }
                yield return null;
            }
            transform.position = end;
            Solid(true);
            Unstretch();

            // 반대편에서 지나온 쪽을 돌아본다. 지나온 길에 큰 X 잔상이 남고, 잠시 뒤 한꺼번에 터진다
            art.Face(-dir);
            art.Play("ShadowOut");
            StartCoroutine(BigX(x, pathZone, crossZone));
            yield return new WaitForSeconds(art.Duration("ShadowOut") + art.Duration("ShadowRecover"));
        }

        // 큰 X의 두 획 (바닥 좌표): A = 돌진한 길, C = 그 가운데를 비스듬히 가로지르는 획
        private struct XShape { public Vector2 A0, A1, C0, C1, Mid; }

        private XShape MakeX(Vector2 a0, Vector2 a1, Vector2 dir)
        {
            Vector2 mid = (a0 + a1) * .5f;
            // 진행 방향에서 63도 돌려 위에서 아래로 내리긋는다 (왼쪽으로 달렸으면 오른쪽 위 → 왼쪽 아래)
            Vector2 cross = (Vector2)(Quaternion.Euler(0f, 0f, dir.x < 0f ? 63f : -63f) * dir);
            float half = Mathf.Clamp(Vector2.Distance(a0, a1) * .28f, 2.4f, 3.6f);
            return new XShape { A0 = a0, A1 = a1, C0 = mid - cross * half, C1 = mid + cross * half, Mid = mid };
        }

        // ① 돌진한 길에 세 갈래 발톱 자국 ② 돌아보는 순간 가로지르는 발톱 자국 → 큰 X.
        // 자국은 바닥에 남아 있다가 shadowDelay 뒤 틈에서 번개가 한꺼번에 솟구친다. 두 획 중 하나라도 걸치면 한 번 맞는다.
        private IEnumerator BigX(XShape x, BossTelegraph pathZone, BossTelegraph crossZone)
        {
            float burstAt = Time.time + shadowDelay;
            float width = shadowWidth * 1.15f;
            WolfSlashMark.Spawn(x.A0, x.A1, width, burstAt);
            yield return new WaitForSeconds(.07f);
            WolfSlashMark.Spawn(x.C0, x.C1, width, burstAt);
            Shake(.18f, .12f);
            for (int i = 0; i < 4; i++) Dust(Vector2.Lerp(x.C0, x.C1, Random.value), Random.Range(.9f, 1.3f)); // 긁힐 때 흙먼지

            while (Time.time < burstAt) yield return null;
            pathZone.Fire();
            crossZone.Fire();
            // 폭발: 크게 흔들고 화면 번쩍, 교차점에서 큰 분출, 두 획을 따라 땅에서 번개가 줄지어 솟구친다
            Shake(.75f, .45f);
            Flash(.45f);
            WolfLightning.Erupt(x.Mid, 5f);
            EruptAlong(x.A0, x.A1);
            EruptAlong(x.C0, x.C1);
            GroundBurst.Rubble(x.Mid, 1.8f, Debris, 26, 12, 1.4f, 1f);
            float half = shadowWidth * .5f;
            bool hit = false;
            foreach (ArcherController p in Players())
            {
                if (p.IsDashing) continue;
                Vector2 pos = p.transform.position;
                bool onPath = Vector2.Distance(pos, ClosestOnSegment(x.A0, x.A1, pos)) <= half;
                bool onCross = Vector2.Distance(pos, ClosestOnSegment(x.C0, x.C1, pos)) <= half;
                if ((onPath || onCross) && Hurt(p, markDamage, .6f, Vector2.zero)) hit = true;
            }
            if (phaseTwo && shadowPlus) StartCoroutine(BigPlus(x));
            if (hit) yield return HitStop(.1f);
        }

        // 2페이즈: X가 터진 자리에 +자 발톱 자국이 한 번 더 긋고 터진다. +의 두 획은 X의 빈틈을 정확히 지나간다
        private IEnumerator BigPlus(XShape x)
        {
            Vector2 a = (x.A1 - x.A0).normalized, c = (x.C1 - x.C0).normalized;
            // X의 두 획 사이(빈틈) 방향 두 개 = 서로 수직인 + 두 획
            Vector2 p = (a + c).normalized, q = new Vector2(-p.y, p.x);
            float half = Mathf.Clamp(Vector2.Distance(x.A0, x.A1) * .4f, 3f, 5f);
            Vector2 p0 = x.Mid - p * half, p1 = x.Mid + p * half, q0 = x.Mid - q * half, q1 = x.Mid + q * half;
            float burstAt = Time.time + shadowPlusDelay;
            BossTelegraph z1 = BossTelegraph.Rect(p0, p1, shadowWidth, shadowPlusDelay, Warning);
            BossTelegraph z2 = BossTelegraph.Rect(q0, q1, shadowWidth, shadowPlusDelay, Warning);
            float width = shadowWidth * 1.15f;
            yield return new WaitForSeconds(.12f);
            WolfSlashMark.Spawn(p0, p1, width, burstAt);
            Shake(.15f, .1f);
            yield return new WaitForSeconds(.07f);
            WolfSlashMark.Spawn(q0, q1, width, burstAt);
            Shake(.15f, .1f);
            while (Time.time < burstAt) yield return null;
            z1.Fire(); z2.Fire();
            Shake(.6f, .35f);
            Flash(.35f);
            WolfLightning.Erupt(x.Mid, 4.2f);
            EruptAlong(p0, p1);
            EruptAlong(q0, q1);
            GroundBurst.Rubble(x.Mid, 1.4f, Debris, 18, 8, 1.2f, .9f);
            float halfWidth = shadowWidth * .5f;
            foreach (ArcherController pl in Players())
            {
                if (pl.IsDashing) continue;
                Vector2 pos = pl.transform.position;
                bool onP = Vector2.Distance(pos, ClosestOnSegment(p0, p1, pos)) <= halfWidth;
                bool onQ = Vector2.Distance(pos, ClosestOnSegment(q0, q1, pos)) <= halfWidth;
                if (onP || onQ) Hurt(pl, markDamage, .6f, Vector2.zero);
            }
        }

        // a → b 를 따라 땅에서 번개가 솟구친다. 줄지어 서 보이지 않게 간격·옆으로 벗어남·크기를 흩뜨린다
        private static void EruptAlong(Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < .1f) return;
            Vector2 side = new Vector2(-d.y, d.x) / length;
            int n = Mathf.Max(2, Mathf.RoundToInt(length / 1.6f));
            for (int i = 0; i < n; i++)
            {
                float t = Mathf.Clamp01((i + Random.Range(.15f, .85f)) / n);
                Vector2 at = a + d * t + side * Random.Range(-.45f, .45f);
                WolfLightning.Erupt(at, Random.Range(1.6f, 2.9f), false);
            }
        }

        // ---------- 덮치기 (잡기) ----------
        // rebound: 2페이즈에서 벽을 박차고 다시 덮치는 두 번째 덮치기 (경고가 짧다)
        private IEnumerator Pounce(ArcherController target, bool rebound = false)
        {
            IsPerformingPattern = true;
            Vector2 start = transform.position;
            // 경로·잡기 판정은 바닥(발 높이)에서 잰다. 몸은 그 위를 나란히 날아간다
            Vector2 foot = new Vector2(0f, art.FeetY - transform.position.y);
            Vector2 direction = Aim(start + foot, target);
            Vector2 wall = WallPoint(start, direction);

            // 1) 준비 ① 조준: 늑대 발밑에서 노린 사람 쪽으로 돌진 경로(장판 + 초록 화살표)가 뻗어 그 사람을 따라 돈다
            //    (각 PC가 화면에 보이는 늑대·사람 위치로 직접 그린다)
            art.Play("Aim");
            float track = rebound ? reboundTrack : aimTrackTime, lockTime = rebound ? reboundLock : aimLockTime;
            StartCoroutine(Crackle(track + lockTime, 1.1f, .1f));
            WolfAimPath.Show(this, target, track, pathWidth, Vector2.Distance(start, wall));
            float trackUntil = Time.time + track;
            while (Time.time < trackUntil && target != null && !target.GetComponent<Health>().IsDead)
            {
                direction = Aim(start + foot, target);
                art.Face(direction);
                yield return null;
            }
            // ② 고정: 경로가 그 자리에 멈춰 깔리고 화살표가 빨라지며 깜빡인다. 이때 옆으로 빠지거나 구르기로 뚫어야 한다
            wall = WallPoint(start, direction);
            BossTelegraph path = BossTelegraph.Rect(start + foot, wall + foot, pathWidth, lockTime, Warning).Steady(.4f);
            ChargeWarning.Spawn(start + foot, wall + foot, pathWidth, lockTime, true);
            Shake(.06f, lockTime);
            yield return new WaitForSeconds(lockTime);
            path.Fire();

            // 2) 돌진하며 잡기: 앞발을 뻗고 날아간다. 날아가는 동안은 몸으로 사람을 밀어내지 않는다(잡기 판정으로만 처리)
            Solid(false);
            // 앞발을 뻗고 땅을 박차 날아오른다 (도약 시트)
            art.Play("PounceFly");
            yield return Launch(direction);
            // 박차고 나가는 순간 발밑에서 번개가 터진다 (경로 번개는 달리는 동안 뒤에 남는다)
            WolfLightning.GroundSpark(new Vector2(transform.position.x, art.FeetY), -direction, 2.6f);
            // 노린 자리를 지나고도 조금 더 달린 뒤에야 대기 자세로 미끄러진다
            float targetDistance = target != null ? Vector2.Distance(start + foot, target.transform.position) + shadowOvershoot : 0f;
            float nextDust = 0f, nextLine = 0f;
            Vector2 lastScrape = transform.position;
            float scrapeGap = Random.Range(.5f, 1.5f);
            ArcherController caught = null;
            bool sliding = false;
            // ground: 땅 위 위치(판정 기준). 날아가는 동안 몸은 그 위로 포물선을 그린다
            Vector2 ground = transform.position;
            float flight = Mathf.Max(.5f, targetDistance);
            while (true)
            {
                float travelled = Vector2.Distance(start, ground);
                float remaining = Vector2.Distance(ground, wall);
                if (remaining < .05f) break;
                // 노린 자리를 지나도록 아무도 못 잡았으면 착지해 대기 자세로 벽까지 미끄러진다
                if (!sliding && travelled > targetDistance)
                {
                    sliding = true;
                    art.Play("Slide");
                    Dust(ground + Vector2.down * .5f, 1.3f);
                    Shake(.15f, .1f);
                }
                float speed = sliding ? slideSpeed : dashSpeed;
                Vector2 from = ground;
                Vector2 to = Vector2.MoveTowards(from, wall, speed * Time.deltaTime);
                // 빠르게 움직여 한 프레임에 사람을 건너뛰지 않도록, 이번에 지나간 구간 전체로 잡기 판정
                if (!sliding) caught = Catch(from + foot, to + foot, direction);
                if (caught != null)
                {
                    ground = ClosestOnSegment(from + foot + direction * .6f, to + foot + direction * .6f, caught.transform.position) - direction * .6f - foot;
                    break;
                }
                ground = to;
                float h = sliding ? 0f : Mathf.Sin(Mathf.Clamp01(Vector2.Distance(start, ground) / flight) * Mathf.PI) * pounceHeight;
                airGround = h > 0f ? ground : (Vector2?)null;
                transform.position = ground + Vector2.up * h;
                art.SetLift(h);
                bool low = h < .2f;
                // 발밑 흙먼지(땅에 붙어 있을 때만), 속도선, 바닥 긁힘 자국
                if (low && Time.time >= nextDust) { Dust(ground - direction * .5f + Vector2.down * .5f, sliding ? .7f : 1f); nextDust = Time.time + .06f; }
                if (!sliding && Time.time >= nextLine)
                {
                    SpeedLines(direction, Red(new Color(.9f, 1f, .75f, .7f), new Color(1f, .8f, .75f)));
                    WolfLightning.Bolt((Vector2)transform.position - direction * 1.5f + Vector2.up * .3f, (Vector2)transform.position + Vector2.up * .3f, .09f, .15f, .45f);
                    nextLine = Time.time + .08f;
                }
                if (low && Vector2.Distance(lastScrape, ground) > scrapeGap)
                {
                    WolfImpactFx.Scrape(new Vector2(ground.x, art.FeetY), direction);
                    lastScrape = ground;
                    scrapeGap = Random.Range(.5f, 1.5f); // 긁힘 간격도 들쭉날쭉
                }
                yield return null;
            }
            // 땅으로 내려놓는다
            transform.position = ground;
            airGround = null;
            art.SetLift(0f);

            Solid(true);
            Unstretch();
            if (caught != null) yield return GrabOrBreak(caught, direction);
            else if (!rebound && phaseTwo && reboundPounce && !health.IsDead)
            {
                // 2페이즈: 벽을 박차고 돌아서 한 번 더 덮친다 (그로기 없이)
                yield return WallKick(direction);
                ArcherController next = target != null && !target.GetComponent<Health>().IsDead ? target : PickTarget();
                if (next != null) { yield return Pounce(next, true); yield break; }
                yield return WallStun(direction, GroggyTime);
            }
            else yield return WallStun(direction, GroggyTime);
            art.Face(null);
            IsPerformingPattern = false;
        }

        // 그로기 시간: 1페이즈 stunTime, 2페이즈 reboundStunTime
        private float GroggyTime => phaseTwo ? reboundStunTime : stunTime;

        // 잡고 있는 동안 받은 피해 (잡은 순간부터 내려찍기 직전까지만 센다)
        private int grabDamage;
        private bool countingGrab;
        private void OnDamaged(Health h, int amount) { if (countingGrab && h == health) grabDamage += amount; }

        // 잡기: 동료가 잡힌 사람을 구하려고 grabBreakDamage 이상 때리면 놓치고 쓰러진다
        private IEnumerator GrabOrBreak(ArcherController victim, Vector2 direction)
        {
            grabDamage = 0;
            countingGrab = true;
            IEnumerator sequence = GrabSequence(victim, direction);
            bool broke = false;
            while (sequence.MoveNext())
            {
                if (countingGrab && grabDamage >= grabBreakDamage) { broke = true; break; }
                yield return sequence.Current;
            }
            if (!broke && countingGrab && grabDamage >= grabBreakDamage) broke = true;
            countingGrab = false;
            if (broke) yield return GrabBreak(direction);
        }

        // 잡기가 풀림: 잡힌 사람을 앞발 아래 땅에 떨어뜨리고, 번개가 터지며 쓰러진다
        private IEnumerator GrabBreak(Vector2 direction)
        {
            if (held != null)
            {
                heldOffset = new Vector2(heldOffset.x, art.FeetY - transform.position.y);
                PlaceHeld();
                ArcherController freed = held;
                Release();
                freed.GetComponent<CombatStatus>()?.Knockback(direction, 1.2f, .15f);
            }
            Unstretch();
            Flash(.3f);
            Shake(.45f, .3f);
            HitSpark.Spawn(transform.position + Vector3.up * .4f, 2.2f, 0, 3000);
            WolfLightning.Burst((Vector2)transform.position + Vector2.up * .4f, 2.2f, 10, .25f);
            yield return HitStop(.1f);
            yield return Groggy(GroggyTime);
        }

        // 쓰러져 seconds 동안 그로기 (받는 피해 2배). 쓰러지는 동작도 그 시간 안에 들어간다
        private IEnumerator Groggy(float seconds)
        {
            var status = GetComponent<CombatStatus>();
            status.ApplyVulnerability(seconds);
            art.Play("StunFall");
            StartCoroutine(Crackle(seconds, .9f, .3f)); // 그로기 동안 몸에서 스파크가 튄다
            yield return new WaitForSeconds(seconds);
            art.Play("StunRecover");
            yield return new WaitForSeconds(art.Duration("StunRecover") + .2f);
            status.Cleanse();
            art.Play("Idle");
        }

        // 벽을 박차고 튀어 나온다: 부딪힌 충격은 그대로, 쓰러지지 않고 곧장 돌아선다
        private IEnumerator WallKick(Vector2 direction)
        {
            Crash(direction);
            yield return HitStop(.1f);
            Unstretch();
            Vector2 at = new Vector2(transform.position.x, art.FeetY);
            WolfLightning.GroundSpark(at, -direction, 3f);
            WolfLightning.Burst((Vector2)transform.position + direction * .6f + Vector2.up * .3f, 1.8f, 7, .2f);
            art.Shape(1.15f, .82f, 30f); // 벽에 붙어 움츠렸다가 튀어 나간다
            yield return new WaitForSeconds(.08f);
        }

        private IEnumerator GrabSequence(ArcherController victim, Vector2 direction)
        {
            Shake(.28f, .22f);
            HitSpark.Spawn(victim.transform.position + Vector3.up * .5f, 1.5f, 0, 3000);
            WolfLightning.Burst((Vector2)victim.transform.position + Vector2.up * .4f, 1.5f, 6);
            Hold(victim, new Vector2(direction.x >= 0 ? .9f : -.9f, 0f));
            if (PersistentWorld.Networking.NetworkGameWorld.Instance == null) HurtVignette.Flash(.45f);
            yield return HitStop(.12f);
            art.Play("Grab");
            float grabTime = art.Duration("Grab"), t0 = Time.time;
            Vector2 low = heldOffset, high = new Vector2(direction.x >= 0 ? .55f : -.55f, 1.5f);
            while (Time.time - t0 < grabTime)
            {
                // 들어 올리는 앞발을 따라간다 (그림 정보가 없으면 위로 부드럽게)
                heldOffset = art.TryGrip(out Vector2 paw) ? InPaw(paw) : Vector2.Lerp(low, high, (Time.time - t0) / grabTime);
                yield return null;
            }
            // 흔들기: 들어 올린 채 좌우로 흔들며 여러 번 피해
            art.Play("Shake");
            for (int i = 0; i < shakeHits; i++)
            {
                float until = Time.time + shakeTime / shakeHits;
                while (Time.time < until)
                {
                    heldOffset = art.TryGrip(out Vector2 paw) ? InPaw(paw) : high + new Vector2(Mathf.Sin(Time.time * 28f) * .18f, 0f);
                    yield return null;
                }
                if (held != null && Hurt(held, shakeDamage, .3f, Vector2.zero))
                {
                    art.Freeze(.05f);
                    Shake(.12f, .08f);
                    // 물고 흔들 때 늑대와 잡힌 사람 사이로 번개가 흐른다
                    WolfLightning.Bolt((Vector2)transform.position + Vector2.up * .6f, (Vector2)held.transform.position + Vector2.up * .3f, .12f, .16f, .5f);
                    WolfLightning.Sparks((Vector2)held.transform.position + Vector2.up * .3f, .5f, 3);
                }
            }
            // 내리꽂기: 충격 순간에 땅으로
            art.Play("Slam");
            // 앞발이 땅에 닿는 11번 그림이 충격 순간
            float slamTime = art.Duration("Slam"), impact = art.Frames("Slam", 10);
            t0 = Time.time;
            while (Time.time - t0 < impact)
            {
                heldOffset = art.TryGrip(out Vector2 paw) ? InPaw(paw) : Vector2.Lerp(high, high + Vector2.up * .4f, Mathf.Sin((Time.time - t0) / impact * Mathf.PI * .5f));
                yield return null;
            }
            // 충격: 잡힌 사람을 앞발 아래 바닥에 내려놓는다 (발이 바닥에 닿게)
            float pawX = art.TryGrip(out Vector2 down) ? down.x : transform.position.x + (direction.x >= 0 ? 1f : -1f);
            countingGrab = false; // 내려찍는 순간부터는 풀 수 없다
            Vector2 ground = new Vector2(pawX, art.FeetY) - (Vector2)transform.position;
            heldOffset = ground;
            PlaceHeld();
            Vector2 at = (Vector2)transform.position + ground;
            ArcherController victimNow = held;
            Release();
            if (victimNow != null) Hurt(victimNow, slamDamage, .8f, Vector2.zero);
            Shake(.5f, .45f);
            HitSpark.Spawn(at, 2.2f, 0, 3000);
            BossTelegraph.Shockwave(at, slamRadius, Warning);
            WolfLightning.Strike(at, 6.5f, 1.4f);
            WolfLightning.Erupt(at, slamRadius * 2f);
            WolfImpactFx.Crack(at, 1.4f);
            GroundBurst.Rubble(at, 1.6f, Debris, 22, 10, 1.4f, .9f);
            foreach (ArcherController other in Players().Where(p => p != victimNow))
                if (Vector2.Distance(other.transform.position, at) <= slamRadius && !other.IsDashing)
                    Hurt(other, slamSplashDamage, .4f, ((Vector2)other.transform.position - at).normalized * .8f);
            yield return HitStop(.14f);
            yield return new WaitForSeconds(slamTime - impact + .3f);
            art.Play("Idle");
        }

        // 벽에 쾅: 크게 흔들리고 충격이 터진다
        private void Crash(Vector2 direction)
        {
            Vector2 crash = (Vector2)transform.position + direction * .8f;
            Shake(.6f, .5f);
            HitSpark.Spawn(crash + Vector2.up * .4f, 2.6f, 0, 3000);
            BossTelegraph.Shockwave(crash, 1.8f, new Color(.85f, .85f, .75f));
            WolfLightning.Erupt(new Vector2(crash.x, art.FeetY), 3.4f, false);
            WolfImpactFx.Crack(crash, 1.1f);
            GroundBurst.Rubble(crash, 1.4f, Debris, 20, 8, 1.2f, .8f);
            art.Shape(.8f, 1.12f, 30f);   // 부딪혀 찌그러졌다가
        }

        private IEnumerator WallStun(Vector2 direction, float seconds)
        {
            Crash(direction);
            yield return HitStop(.12f);
            Unstretch();
            yield return Groggy(seconds);
        }

        // ---------- 잡은 사람 붙잡기 ----------
        private void Hold(ArcherController victim, Vector2 offset)
        {
            held = victim; heldOffset = offset;
            victim.AcceptLocalInput = false;
            art.SortBias = -400; // 잡힌 사람이 앞발 위로 보이게 늑대를 뒤에 그린다
        }

        private void Release()
        {
            art.SortBias = 0;
            if (held == null) return;
            held.AcceptLocalInput = true;
            held = null;
        }

        // 잡힌 사람의 허리가 앞발에 오게 (사람 위치는 발밑 기준)
        private Vector2 InPaw(Vector2 paw) => paw + Vector2.down * .55f - (Vector2)transform.position;

        private void LateUpdate()
        {
            PlaceHeld();
            // 발 판정은 늘 발밑에 (몸 크기·늘이기와 상관없이 월드 기준 크기 유지)
            if (feet != null && art != null)
            {
                feet.transform.position = FootCenter(transform.position);
                float scale = Mathf.Max(.01f, Mathf.Abs(feet.transform.lossyScale.x));
                feet.radius = footRadius / scale;
            }
        }

        private void Solid(bool solid) { if (feet != null) feet.isTrigger = !solid; }
        private bool Mine(Collider2D c) => c == body || c == feet;
        // 몸 위치(bodyPos)일 때 발 판정 원의 중심 (발바닥보다 조금 위)
        private Vector2 FootCenter(Vector2 bodyPos) => new Vector2(bodyPos.x, bodyPos.y + (art.FeetY - transform.position.y) + footRadius * .5f);

        private void PlaceHeld()
        {
            if (held == null) return;
            if (held.GetComponent<Health>().IsDead) { Release(); return; }
            Vector2 at = (Vector2)transform.position + heldOffset;
            held.transform.position = at;
            if (held.GetComponent<Rigidbody2D>() is Rigidbody2D rb) { rb.position = at; rb.linearVelocity = Vector2.zero; }
        }

        // ---------- 이동기 연출 도우미 ----------
        private Sprite[] Debris => GetComponent<BossController>() is BossController golem ? golem.QuakeDebris : null;

        // 진행 방향으로 몸을 길게 늘인다 (가로로 달리면 가로로, 세로로 달리면 세로로)
        private void StretchFor(Vector2 dir, float amount)
        {
            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y)) art.Shape(1f + amount, 1f - amount * .45f, 22f);
            else art.Shape(1f - amount * .45f, 1f + amount, 22f);
        }

        private void Unstretch() => art.Shape(1f, 1f, 14f);

        // 출발: 납작하게 웅크렸다가 흙먼지를 터뜨리며 튀어 나간다
        private IEnumerator Launch(Vector2 dir)
        {
            art.Shape(1.14f, .8f, 32f);
            yield return new WaitForSeconds(.07f);
            LaunchBurst(transform.position, dir);
            StretchFor(dir, .28f);
        }

        private void LaunchBurst(Vector2 at, Vector2 dir)
        {
            for (int i = 0; i < 8; i++)
                Dust(at + Vector2.down * .55f - dir * Random.Range(.1f, .7f) + Random.insideUnitCircle * .35f, Random.Range(1f, 1.5f));
            Shake(.22f, .15f);
        }

        private void SpeedLines(Vector2 dir, Color color)
        {
            Vector2 side = new Vector2(-dir.y, dir.x);
            for (int i = 0; i < 2; i++)
                WolfImpactFx.SpeedLine((Vector2)transform.position + side * Random.Range(-.9f, .9f) + Vector2.up * Random.Range(-.2f, .8f) - dir * Random.Range(.4f, 1.2f), dir, color);
        }

        // 기술 준비 중 몸 주변이 지직거린다
        private IEnumerator Crackle(float seconds, float radius, float interval = .12f)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !health.IsDead)
            {
                WolfLightning.Sparks((Vector2)transform.position + Vector2.up * .25f, radius, 1);
                yield return new WaitForSeconds(interval);
            }
        }

        // 주변 여기저기에 번개가 차례로 내리꽂힌다 (장식)
        private IEnumerator Strikes(Vector2 center, float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 at = center + Random.insideUnitCircle * radius;
                WolfLightning.Strike(at, 6f, 1f);
                yield return new WaitForSeconds(Random.Range(.06f, .14f));
            }
        }

        // 역경직: 늑대 동작을 잠깐 멈춘다 (그동안 늑대 두뇌도 기다린다)
        private IEnumerator HitStop(float seconds)
        {
            art.Freeze(seconds);
            yield return new WaitForSeconds(seconds);
        }

        // 피해 + 맞은 느낌: 화면 붉은 테두리(혼자 하기), 살짝 밀림. 잡혀 있는 사람은 밀지 않는다
        private bool Hurt(ArcherController p, int damage, float vignette, Vector2 push)
        {
            if (p == null || !p.GetComponent<Health>().TryDamage(damage, Faction.Enemy, gameObject)) return false;
            if (vignette > 0f && PersistentWorld.Networking.NetworkGameWorld.Instance == null) HurtVignette.Flash(vignette);
            if (push.sqrMagnitude > .0001f && p != held) p.GetComponent<CombatStatus>()?.Knockback(push.normalized, push.magnitude, .12f);
            return true;
        }

        // ---------- 도움 함수 ----------
        private static Vector2 Aim(Vector2 from, ArcherController target)
        {
            Vector2 d = (Vector2)target.transform.position - from;
            return d.sqrMagnitude < .01f ? Vector2.left : d.normalized;
        }

        // 돌진 잔상: 지금 그림을 복사해 연두빛으로 남기고 금방 사라지게 한다 (보이기만 하는 장식)
        private void Afterimage(float alpha) => Afterimage(alpha, Red(new Color(.35f, 1f, .3f), new Color(1f, .3f, .25f)));

        private void Afterimage(float alpha, Color tint)
        {
            if (WolfFxNet.Send(WolfFxNet.Afterimage, transform.position, Vector2.zero, alpha, tint.r, tint.g, tint.b)) return;
            var source = GetComponent<SpriteRenderer>();
            if (source == null || source.sprite == null) return;
            var ghost = new GameObject("Wolf Afterimage");
            ghost.transform.SetPositionAndRotation(transform.position, transform.rotation);
            ghost.transform.localScale = transform.lossyScale;
            var sr = ghost.AddComponent<SpriteRenderer>();
            sr.sprite = source.sprite; sr.flipX = source.flipX;
            sr.sortingLayerID = source.sortingLayerID; sr.sortingOrder = source.sortingOrder - 1;
            sr.color = new Color(tint.r, tint.g, tint.b, alpha);
            WolfRed.Apply(sr);
            ghost.AddComponent<Fade>().Begin(.22f);
        }

        // 흙먼지 한 덩이: 부드러운 원이 퍼지며 사라진다 (보이기만 하는 장식)
        private static Sprite puff;
        internal static void Dust(Vector2 at, float size)
        {
            if (WolfFxNet.Send(WolfFxNet.Dust, at, Vector2.zero, size)) return;
            if (puff == null)
            {
                const int n = 32;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
                }
                tex.Apply();
                puff = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);
            }
            var obj = new GameObject("Wolf Dust");
            obj.transform.position = at + Random.insideUnitCircle * .2f;
            obj.transform.localScale = Vector3.one * size * Random.Range(.6f, .9f);
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = puff; sr.sortingOrder = -70; sr.color = new Color(.55f, .5f, .42f, .45f);
            obj.AddComponent<Fade>().Begin(.45f, 1.8f);
        }

        private sealed class Fade : MonoBehaviour
        {
            private SpriteRenderer sr; private float life, age, grow = 1f; private Color start; private Vector3 baseScale;
            public void Begin(float seconds, float growTo = 1f) { sr = GetComponent<SpriteRenderer>(); start = sr.color; life = seconds; grow = growTo; baseScale = transform.localScale; }
            private void Update()
            {
                age += Time.deltaTime;
                float k = 1f - age / life;
                if (k <= 0f) { Destroy(gameObject); return; }
                sr.color = new Color(start.r, start.g, start.b, start.a * k * k);
                if (grow != 1f) transform.localScale = baseScale * Mathf.Lerp(1f, grow, 1f - k);
            }
        }
        // 입(늑대 앞쪽)이 from→to로 지나가는 동안 닿은 사람. 회피(구르기) 중이면 잡히지 않는다.
        private ArcherController Catch(Vector2 from, Vector2 to, Vector2 direction)
        {
            Vector2 a = from + direction * .6f, b = to + direction * .6f;
            return Players().Where(p => !p.IsDashing)
                .Select(p => (p, d: Vector2.Distance(p.transform.position, ClosestOnSegment(a, b, p.transform.position))))
                .Where(x => x.d <= grabReach).OrderBy(x => Vector2.Distance(a, x.p.transform.position)).Select(x => x.p).FirstOrDefault();
        }

        private static Vector2 ClosestOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return a + ab * t;
        }

        // 돌진 방향으로 처음 부딪히는 벽·기둥(부서지지 않는 장애물) 바로 앞
        private Vector2 WallPoint(Vector2 from, Vector2 direction)
        {
            // 발 높이에서 찾는다 (기둥 판정은 밑동에 있다). 돌려주는 건 몸 위치
            Vector2 foot = FootCenter(from);
            foreach (RaycastHit2D hit in Physics2D.CircleCastAll(foot, footRadius * .85f, direction, 60f).OrderBy(h => h.distance))
            {
                if (Mine(hit.collider) || hit.collider.isTrigger) continue;
                if (hit.collider.GetComponentInParent<WorldObstacle>() == null || hit.collider.GetComponentInParent<Health>() != null) continue;
                return from + direction * Mathf.Max(0f, hit.distance - .05f);
            }
            return from + direction * 20f;
        }

        private bool Blocked(Vector2 bodyPos) =>
            Physics2D.OverlapCircleAll(FootCenter(bodyPos), footRadius).Any(c => !Mine(c) && !c.isTrigger && c.GetComponentInParent<WorldObstacle>() != null);

        // 걷기 한 걸음: 막히면 장애물을 따라 옆으로 비켜 간다. 앞으로 거의 못 나아가면 막힌 시간이 쌓인다
        private void Step(Vector2 delta)
        {
            float length = delta.magnitude;
            if (length < 1e-5f) return;
            Vector2 from = transform.position;
            Vector2 moved = Vector2.zero;
            if (!Blocked(from + delta)) moved = delta;
            else
            {
                // 진행 방향에서 조금씩 더 꺾어 가며 빈 쪽을 찾는다 (지난번에 비켜 간 쪽 먼저)
                foreach (float angle in new[] { 35f, 60f, 85f })
                {
                    foreach (int side in new[] { slideSide, -slideSide })
                    {
                        Vector2 d = (Vector2)(Quaternion.Euler(0f, 0f, angle * side) * delta);
                        if (Blocked(from + d)) continue;
                        moved = d; slideSide = side;
                        break;
                    }
                    if (moved != Vector2.zero) break;
                }
            }
            if (moved != Vector2.zero) transform.position = from + moved;
            // 원래 가려던 쪽으로 30%도 못 나아갔으면 막힌 것
            blockedFor = Vector2.Dot(moved, delta / length) < length * .3f ? blockedFor + Time.deltaTime : 0f;
        }

        // 오래 막혔으면 대상 쪽으로 장애물을 뛰어넘는다 (대상 바로 앞, 빈 자리에 착지)
        private IEnumerator LeapOver(ArcherController target)
        {
            blockedFor = 0f;
            if (target == null) yield break;
            Vector2 from = Feet, aim = target.transform.position;
            Vector2 dir = (aim - from).sqrMagnitude > .01f ? (aim - from).normalized : Vector2.left;
            float distance = Mathf.Clamp(Vector2.Distance(from, aim) - 2.2f, 2.5f, jumpMaxDistance);
            Vector2 bodyOffset = (Vector2)transform.position - Feet;
            for (int i = 0; i <= 10; i++)
            {
                Vector2 spot = from + dir * distance * (1f - i * .08f);
                if (Blocked(spot + bodyOffset)) continue;
                yield return LeapTo(spot);
                yield break;
            }
        }

        // 살아 있는 사람 중 늑대와 같은 구역에 있는 사람만 (온라인은 마을·필드·보스방이 한 장면에 나란히 있다)
        private ArcherController[] Players() =>
            FindObjectsByType<ArcherController>(FindObjectsSortMode.None)
                .Where(p => p.isActiveAndEnabled && !p.GetComponent<Health>().IsDead && PersistentWorld.Networking.NetworkGameWorld.SameArea(p.transform.position, transform.position)).ToArray();

        // 화면 흔들림·번쩍임: 온라인 서버면 늑대 방 사람들에게 신호로 보낸다
        private void Shake(float magnitude, float duration)
        {
            if (!WolfFxNet.Send(WolfFxNet.Shake, transform.position, Vector2.zero, magnitude, duration)) CameraFollow.Shake(magnitude, duration);
        }

        private void Flash(float amount)
        {
            if (!WolfFxNet.Send(WolfFxNet.Flash, transform.position, Vector2.zero, amount)) ScreenFlash.Show(amount);
        }

        // ---------- 딜량표 (온라인 보스 처치 결과) ----------
        private sealed class Tally { public int Dealt, Taken, Deaths; }
        private readonly System.Collections.Generic.Dictionary<ArcherController, Tally> tallies = new System.Collections.Generic.Dictionary<ArcherController, Tally>();
        private float engagedAt = -1f;

        private void Count(Health target, int amount, GameObject source)
        {
            if (health == null || amount <= 0) return;
            if (target == health)
            {
                var attacker = source != null ? source.GetComponentInParent<ArcherController>() : null;
                if (attacker != null) TallyOf(attacker).Dealt += amount;
                return;
            }
            if (engagedAt < 0 || health.IsDead || target.Faction != Faction.Player) return;
            var player = target.GetComponent<ArcherController>();
            if (player == null || !PersistentWorld.Networking.NetworkGameWorld.SameArea(player.transform.position, transform.position)) return;
            Tally tally = TallyOf(player);
            tally.Taken += amount;
            if (target.IsDead) tally.Deaths++;
        }

        private Tally TallyOf(ArcherController player)
        {
            if (!tallies.TryGetValue(player, out Tally tally)) tallies[player] = tally = new Tally();
            return tally;
        }

        public PersistentWorld.Networking.BossResultFrame BuildResult(System.Func<ArcherController, (ulong id, string name)> identify,
            System.Collections.Generic.IEnumerable<ArcherController> participants = null)
        {
            if (participants != null) foreach (var player in participants) if (player != null) TallyOf(player);
            var rows = new System.Collections.Generic.List<PersistentWorld.Networking.BossResultRow>();
            foreach (var pair in tallies)
            {
                if (pair.Key == null) continue;
                (ulong id, string name) = identify(pair.Key);
                rows.Add(new PersistentWorld.Networking.BossResultRow
                {
                    id = id, name = name, character = (int)pair.Key.Loadout.Class,
                    damage = pair.Value.Dealt, taken = pair.Value.Taken, deaths = pair.Value.Deaths
                });
            }
            return new PersistentWorld.Networking.BossResultFrame
            {
                boss = displayName, seconds = engagedAt < 0 ? 0f : Time.time - engagedAt,
                rows = rows.OrderByDescending(r => r.damage).ToArray()
            };
        }

        private ArcherController Nearest() =>
            Players().OrderBy(p => Vector2.Distance(p.transform.position, transform.position)).FirstOrDefault();

        private ArcherController PickTarget()
        {
            ArcherController[] all = Players();
            return all.Length == 0 ? null : all[Random.Range(0, all.Length)];
        }
    }
}
