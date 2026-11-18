using System.Collections.Generic;
using UnityEngine;

namespace PersistentWorld.World
{
    // 늑대 보스 그림. Resources/Boss/Wolf/Wolf{시트}.png 격자 시트를 잘라 동작(클립)으로 재생한다.
    // 걷기 시트 한 장은 "출발(0~8) → 반복 걷기(9~25) → 멈춤(26~29)"으로 그려져 있어 세 클립으로 나눠 쓴다.
    // 움직이기 시작하면 출발 → 걷기 반복, 멈추면 멈춤 → 대기로 자연스럽게 넘어간다.
    // 원본 그림은 왼쪽을 보고 있어, 움직이는 방향(없으면 가까운 플레이어)을 보도록 좌우를 뒤집는다.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WolfSpriteAnimator : MonoBehaviour
    {
        private readonly struct Sheet
        {
            public readonly int Columns, Rows;
            public Sheet(int columns, int rows) { Columns = columns; Rows = rows; }
        }

        private readonly struct Clip
        {
            public readonly string Sheet, Next;
            public readonly int From, To; // [From, To] 포함
            public readonly bool Loop;
            public readonly bool FacesRight; // 원본 그림이 오른쪽을 보는 동작 (대부분은 왼쪽)
            public readonly float FrameSeconds;
            public readonly int[] Picks; // 이어지지 않은 그림만 골라 쓸 때 (시트 순번, 0부터)
            public Clip(string sheet, int from, int to, float fps, bool loop, string next = null, bool facesRight = false)
            { Sheet = sheet; From = from; To = to; FrameSeconds = 1f / fps; Loop = loop; Next = next; FacesRight = facesRight; Picks = null; }
            public Clip(string sheet, int[] picks, float fps, bool loop, string next = null)
                : this(sheet, picks[0], picks[picks.Length - 1], fps, loop, next) { Picks = picks; }
            public int Count => Picks != null ? Picks.Length : To - From + 1;
            public int Index(int step) => Picks != null ? Picks[step] : From + step;
        }

        private static readonly Dictionary<string, Sheet> Sheets = new Dictionary<string, Sheet>
        {
            ["Idle"] = new Sheet(6, 4),
            ["Walk"] = new Sheet(6, 5),
            ["Aim"] = new Sheet(6, 2),
            ["Dash"] = new Sheet(6, 3),   // 그림자 참격 (x자 공격)
            ["Grab"] = new Sheet(6, 2),   // 잡는중
            ["Shake"] = new Sheet(6, 2),
            ["Slam"] = new Sheet(6, 2),
            ["Stun"] = new Sheet(6, 3), // 1줄 쓰러짐, 2줄 그로기 반복, 3줄 예전 기절·일어남
            ["Double"] = new Sheet(6, 2),
            ["Slash"] = new Sheet(6, 4),
            ["Claw"] = new Sheet(6, 6),
            ["Leap"] = new Sheet(8, 2),   // 도약: 앞발을 뻗고 날며 입을 벌린다 (덮치기 체공)
            ["Breath"] = new Sheet(8, 4), // 번개 숨결: 몸을 낮춰 힘을 모음(1~16) → 입을 크게 벌림(17~22) → 다물고 돌아옴(23~32)
            ["Spin"] = new Sheet(6, 5),   // 회전 공격: 웅크림(1~6) → 몸을 돌려 회전·바람 궤적(7~24) → 포효하며 회복(25~30)
            ["Roar"] = new Sheet(6, 4),
            ["Howl"] = new Sheet(8, 3),
            ["Mutate"] = new Sheet(8, 5), // 변이 포효: 몸을 낮춰 힘을 모음(1~17) → 고개를 들어 포효(18~32) → 다시 낮춤(33~40)
            ["Jump"] = new Sheet(8, 6),   // 점프 공격: 웅크림(1~12) → 도약·공중(13~28) → 착지(29~36). 37~48은 쓰지 않는다
        };

        // 잡은 사람을 쥔 앞발 위치: 시트 칸 안 픽셀(왼쪽 위 기준). 그림에서 앞발 발톱을 찾아 눈으로 확인한 값
        private static readonly Dictionary<string, Dictionary<int, Vector2Int>> GripPixels = new Dictionary<string, Dictionary<int, Vector2Int>>
        {
            ["Breath"] = new Dictionary<int, Vector2Int> { [12] = new Vector2Int(68, 168), [13] = new Vector2Int(70, 158), [14] = new Vector2Int(71, 147), [15] = new Vector2Int(76, 137), [16] = new Vector2Int(80, 126), [17] = new Vector2Int(81, 112), [18] = new Vector2Int(88, 93), [19] = new Vector2Int(89, 83), [20] = new Vector2Int(85, 86), [21] = new Vector2Int(83, 96), [22] = new Vector2Int(76, 106) }, // 벌린 입 (번개가 나가는 곳). 입 안 붉은 부분을 찾아 확인한 값
            ["Grab"] = new Dictionary<int, Vector2Int> { [4] = new Vector2Int(22, 190), [5] = new Vector2Int(25, 150), [6] = new Vector2Int(24, 137), [7] = new Vector2Int(44, 139), [8] = new Vector2Int(56, 127), [9] = new Vector2Int(66, 98), [10] = new Vector2Int(77, 68), [11] = new Vector2Int(51, 60) },

            ["Shake"] = new Dictionary<int, Vector2Int> { [0] = new Vector2Int(45, 184), [1] = new Vector2Int(51, 157), [2] = new Vector2Int(43, 135), [3] = new Vector2Int(64, 108), [4] = new Vector2Int(64, 86), [5] = new Vector2Int(60, 71), [6] = new Vector2Int(59, 74), [7] = new Vector2Int(70, 66), [8] = new Vector2Int(52, 105), [9] = new Vector2Int(48, 121), [10] = new Vector2Int(43, 154), [11] = new Vector2Int(48, 177) },

            ["Slam"] = new Dictionary<int, Vector2Int> { [0] = new Vector2Int(91, 114), [1] = new Vector2Int(92, 110), [2] = new Vector2Int(93, 80), [3] = new Vector2Int(110, 73), [4] = new Vector2Int(119, 57), [5] = new Vector2Int(122, 50), [6] = new Vector2Int(120, 62), [7] = new Vector2Int(80, 80), [8] = new Vector2Int(60, 154), [9] = new Vector2Int(55, 195), [10] = new Vector2Int(55, 276), [11] = new Vector2Int(55, 276) },
        };

        // Next가 Hold면 마지막 프레임에 멈춰 다음 지시를 기다린다
        private const string Hold = "";

        private static readonly Dictionary<string, Clip> Clips = new Dictionary<string, Clip>
        {
            ["Idle"] = new Clip("Idle", 0, 23, 12f, true),
            ["WalkStart"] = new Clip("Walk", 0, 8, 20f, false, "Walk"),
            ["Walk"] = new Clip("Walk", 9, 25, 20f, true),
            ["WalkStop"] = new Clip("Walk", 26, 29, 20f, false, "Idle"),
            // 잡기 기술(덮치기): 사전 예고(잡기 시작) → 돌진하며 잡기(잡는중) → 흔들기 → 내려찍기
            ["Aim"] = new Clip("Aim", 0, 11, 14f, false, Hold),
            ["GrabFly"] = new Clip("Grab", 3, 3, 12f, false, Hold),   // 잡는중 4번 프레임 (도약 그림이 없을 때)
            ["PounceFly"] = new Clip("Leap", 0, 15, 40f, false, Hold), // 덮치기 체공: 앞발을 뻗고 날며 입을 크게 벌린다
            ["Grab"] = new Clip("Grab", 4, 11, 20f, false, "Shake"),  // 잡은 뒤 5번 프레임부터 끝까지
            ["Slide"] = new Clip("Idle", 0, 0, 12f, false, Hold),     // 빗나가면 대기 자세 그대로 벽까지 미끄러진다
            // 그림자 참격: 준비 → 검은 그림자로 관통 → 반대편에서 뒤돌아봄 → 숨 고르기
            // 시트의 그림자(7~9)부터 끝(17)까지는 원본이 오른쪽(지나온 쪽)을 본다
            ["ShadowWind"] = new Clip("Dash", 0, 6, 18f, false, Hold),
            ["ShadowRun"] = new Clip("Dash", 7, 9, 24f, true, facesRight: true),
            ["ShadowOut"] = new Clip("Dash", 10, 11, 24f, false, "ShadowRecover", facesRight: true),
            ["ShadowRecover"] = new Clip("Dash", 12, 17, 20f, false, "Idle", facesRight: true),
            ["Shake"] = new Clip("Shake", 0, 11, 22f, true),
            ["Slam"] = new Clip("Slam", 0, 11, 20f, false, "Idle"),
            // 그로기: 쓰러짐 → 별이 도는 동안 누워 있음(반복) → 일어남
            ["StunFall"] = new Clip("Stun", 0, 5, 12f, false, "Stun"),
            ["Stun"] = new Clip("Stun", 6, 11, 9f, true),
            ["StunRecover"] = new Clip("Stun", 16, 17, 12f, false, "Idle"),
            // 단독 공격: ~Wind(준비, 천천히, 마지막 프레임에서 멈춤) → 본 동작(타격)
            ["DoubleWind"] = new Clip("Double", 0, 2, 9f, false, Hold),
            ["Double"] = new Clip("Double", 3, 11, 24f, false, "Idle"),
            ["SlashWind"] = new Clip("Slash", 0, 8, 17f, false, Hold),
            ["Slash"] = new Clip("Slash", 9, 17, 28f, false, "Idle"),
            ["ClawWind"] = new Clip("Claw", 0, 11, 16f, false, Hold),
            ["Claw"] = new Clip("Claw", 12, 29, 30f, false, "Idle"),
            // 뇌격 방출 (번개 숨결 시트): 몸을 낮춰 힘을 모으며 입을 벌리고(1~17) → 입에서 번개를 뿜는다(18~22 반복) → 다물고 돌아온다(23~32)
            ["BeamWind"] = new Clip("Breath", 0, 16, 19f, false, Hold),
            ["Beam"] = new Clip("Breath", 17, 21, 10f, true),
            ["BeamRecover"] = new Clip("Breath", 22, 31, 16f, false, "Idle"),
            // 회전: 웅크렸다 돌기 시작(1~8) → 한 바퀴를 빠르게 여러 번 반복 → 포효하며 회복
            // 한 바퀴는 9~24번 중 오른쪽을 보는 그림(13~21)을 반만 남겨 뒤돈 느낌이 아니라 휙 도는 느낌으로: 9,10,11,12,14,16,18,20,22,23,24
            ["SpinWind"] = new Clip("Spin", 0, 7, 17f, false, Hold),
            ["SpinLoop"] = new Clip("Spin", new[] { 8, 9, 10, 11, 13, 15, 17, 19, 21, 22, 23 }, 26f, true),
            ["SpinRecover"] = new Clip("Spin", 24, 29, 18f, false, "Idle"),
            ["RoarWind"] = new Clip("Roar", 0, 7, 17f, false, Hold),
            ["Roar"] = new Clip("Roar", 8, 17, 24f, false, "Idle"),
            ["HowlWind"] = new Clip("Howl", 0, 7, 17f, false, Hold),
            ["Howl"] = new Clip("Howl", 8, 15, 20f, false, "Idle"),

            // 변이 포효 (2페이즈 전환·전멸기)
            ["MutateWind"] = new Clip("Mutate", 0, 16, 14f, false, Hold),           // 서서 몸을 낮추며 힘을 모은다
            ["MutateCrouch"] = new Clip("Mutate", 8, 16, 14f, false, Hold),         // 낮게 웅크린 몇 장만 (짧은 준비)
            ["MutateRoar"] = new Clip("Mutate", 17, 31, 12.5f, false, "MutateRecover"), // 고개를 들어 길게 포효
            ["MutateRecover"] = new Clip("Mutate", 32, 39, 16f, false, "Idle"),
            ["MutateBlast"] = new Clip("Mutate", 17, 31, 18f, false, "MutateSettle"),  // 전멸기: 포효한 뒤
            ["MutateBurst"] = new Clip("Mutate", 17, 27, 24f, false, "MutateSettle"),  // 전멸기 매 턴: 짧게 포효
            ["MutateSettle"] = new Clip("Mutate", 32, 39, 16f, false, Hold),          //  …낮게 웅크린 채 다음 번개를 모은다
            ["JumpWind"] = new Clip("Jump", 0, 11, 18f, false, Hold),   // 몸을 낮춰 웅크린다
            ["JumpLeap"] = new Clip("Jump", 12, 27, 24f, false, Hold),  // 일어서며 도약, 17번부터 공중
            ["JumpLand"] = new Clip("Jump", 28, 35, 20f, false, "Idle"), // 착지 후 자세 회복
            ["JumpCoil"] = new Clip("Jump", 8, 11, 18f, false, Hold),   // 연속 점프: 가장 낮게 웅크린 몇 장만 다시
            ["JumpTouch"] = new Clip("Jump", 28, 30, 20f, false, Hold), // 연속 점프: 닿자마자 다음 점프로
        };

        // 이 속도(유닛/초)보다 빠르게 움직이면 걷는 중, 멈춘 뒤 잠깐은 걷는 중으로 쳐서 깜빡이지 않게 한다
        private const float WalkSpeed = .3f, WalkHold = .15f;

        [SerializeField, Tooltip("몸통 높이(유닛). 첫 프레임의 그림 높이를 이 크기로 맞춘다")] private float bodyHeight = 1.3f;

        private readonly Dictionary<string, Sprite[]> frames = new Dictionary<string, Sprite[]>();
        private readonly Dictionary<string, Dictionary<int, Vector2>> grips = new Dictionary<string, Dictionary<int, Vector2>>(); // 몸 기준 앞발 위치(유닛, 왼쪽을 볼 때)
        private string shownSheet;
        private int shownIndex;
        private SpriteRenderer visual;
        private ActorShadow shadow;
        private float lift;
        private string clip = "Idle";
        private float clipStartedAt, movedAt = -10f;
        private Vector3 lastPosition;
        private bool lookingRight;

        public bool Ready { get; private set; }
        public string CurrentClip => clip;

        private void Awake()
        {
            visual = GetComponent<SpriteRenderer>();
            Texture2D idle = SpriteSheetSlicer.Load("Boss/Wolf/WolfIdle", 6, 4);
            if (idle == null) return;
            RectInt first = SpriteSheetSlicer.FirstFrameBounds(idle, 6, 4);
            float ppu = first.height / Mathf.Max(.01f, bodyHeight);
            foreach (var sheet in Sheets)
            {
                Texture2D texture = SpriteSheetSlicer.Load("Boss/Wolf/Wolf" + sheet.Key, sheet.Value.Columns, sheet.Value.Rows);
                Sprite[] sliced = SpriteSheetSlicer.Slice(texture, sheet.Value.Columns, sheet.Value.Rows, ppu, first.height, "Wolf" + sheet.Key);
                if (sliced != null) frames[sheet.Key] = sliced;
                if (texture != null && GripPixels.TryGetValue(sheet.Key, out var pixels))
                {
                    // 칸 픽셀 → 그림 기준점(첫 장 가로 중심, 발에서 몸통 절반 위) 기준 유닛
                    RectInt own = SpriteSheetSlicer.FirstFrameBounds(texture, sheet.Value.Columns, sheet.Value.Rows);
                    int cellHeight = texture.height / sheet.Value.Rows;
                    var local = new Dictionary<int, Vector2>();
                    foreach (var p in pixels)
                        local[p.Key] = new Vector2((p.Value.x - own.center.x) / ppu, (cellHeight - 1 - p.Value.y - (own.y + first.height * .5f)) / ppu);
                    grips[sheet.Key] = local;
                }
            }
            Ready = frames.ContainsKey("Idle");
        }

        private void Start()
        {
            if (!Ready) return;
            // 자리표시 그림(PlaceholderVisual)이 정한 비균등 크기·색을 되돌린다. 원형 콜라이더는 큰 축을 따르므로 판정 크기는 그대로다.
            Vector3 scale = transform.localScale;
            float uniform = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            transform.localScale = new Vector3(uniform, uniform, scale.z);
            baseScale = transform.localScale;
            visual.color = Color.white;
            WolfRed.Apply(visual); // 2페이즈에 초록이 빨강으로 바뀌는 머티리얼
            lastPosition = transform.position;
            shadow = ActorShadow.Attach(transform, bodyHeight * uniform * 1.3f, bodyHeight * uniform * .5f);
            Play("Idle");
        }

        public void Play(string name)
        {
            if (!Clips.TryGetValue(name, out Clip next) || !frames.ContainsKey(next.Sheet)) name = "Idle";
            clip = name;
            clipStartedAt = Time.time;
        }

        private bool Has(string name) => Clips.TryGetValue(name, out Clip c) && frames.ContainsKey(c.Sheet);

        // 기술 중에는 정해진 방향을 보게 고정한다. null이면 다시 움직임·가까운 플레이어를 따른다.
        private Vector2? faceOverride;
        public void Face(Vector2? direction) => faceOverride = direction;

        // 역경직: 잠깐 동작을 멈춘다 (맞는 순간의 손맛)
        private float freezeUntil;
        public void Freeze(float seconds) => freezeUntil = Mathf.Max(freezeUntil, Time.time + seconds);

        // 늘이고 누르기: 몸의 가로·세로 비율을 목표로 부드럽게 바꾼다 (1,1이 원래 모양)
        private Vector3 baseScale = Vector3.one;
        private Vector2 shape = Vector2.one, shapeTarget = Vector2.one;
        private float shapeSpeed = 18f;
        public void Shape(float sx, float sy, float speed = 18f) { shapeTarget = new Vector2(sx, sy); shapeSpeed = speed; }

        // 지금 그림에서 잡은 사람을 쥔 앞발의 월드 위치 (그 그림에 정보가 없으면 false)
        public bool TryGrip(out Vector2 world)
        {
            world = transform.position;
            if (shownSheet == null || !grips.TryGetValue(shownSheet, out var sheetGrips) || !sheetGrips.TryGetValue(shownIndex, out Vector2 local)) return false;
            if (visual.flipX) local.x = -local.x;
            Vector3 s = transform.localScale;
            world = (Vector2)transform.position + new Vector2(local.x * Mathf.Abs(s.x), local.y * Mathf.Abs(s.y));
            return true;
        }

        // 발바닥 높이(월드 y)
        public float FeetY => transform.position.y - lift - bodyHeight * Mathf.Abs(baseScale.y) * .5f;

        // 그리는 순서 보정 (잡은 사람이 앞발 위로 보이도록 잡는 동안 늑대를 뒤로 미룬다)
        public int SortBias { get; set; }

        // 공중 높이 알리기: 몸(transform)을 이만큼 올려 둔 상태라는 뜻. 그림자와 앞뒤 순서는 바닥 기준으로 둔다.
        // 몸을 실제로 올리고 내리는 건 부르는 쪽이 한다.
        public void SetLift(float height)
        {
            lift = Mathf.Max(0f, height);
            if (shadow != null) shadow.Lift = lift;
        }

        public float Duration(string name) => Clips.TryGetValue(name, out Clip c) ? c.Count * c.FrameSeconds : 0f;
        // 클립 시작부터 frames장째까지 걸리는 시간 (타격 순간을 그림 속도에 맞춘다)
        public float Frames(string name, int frames) => Clips.TryGetValue(name, out Clip c) ? frames * c.FrameSeconds : 0f;

        // ---------- 애니메이션 확인용 (Play 중 Inspector의 ⋮ 메뉴에서 실행) ----------
        [ContextMenu("테스트: 잡기 성공")]
        private void TestGrabHit()
        {
            StopAllCoroutines();
            StartCoroutine(Sequence(("Aim", 1.1f), ("GrabFly", .4f), ("Grab", Duration("Grab") + 1.6f), ("Slam", Duration("Slam") + .3f)));
        }

        [ContextMenu("테스트: 잡기 실패 → 기절")]
        private void TestGrabMiss()
        {
            StopAllCoroutines();
            StartCoroutine(Sequence(("Aim", 1.1f), ("GrabFly", .3f), ("Slide", .8f), ("StunFall", Duration("StunFall") + 2.5f), ("StunRecover", Duration("StunRecover") + .2f)));
        }

        [ContextMenu("테스트: 그림자 참격")]
        private void TestShadowSlash()
        {
            StopAllCoroutines();
            StartCoroutine(Sequence(("ShadowWind", Duration("ShadowWind")), ("ShadowRun", .2f), ("ShadowOut", Duration("ShadowOut") + Duration("ShadowRecover"))));
        }

        [ContextMenu("테스트: 점프 공격")]
        private void TestJump()
        {
            StopAllCoroutines();
            StartCoroutine(JumpTest());
        }

        // 제자리에서 앞으로 몇 칸 뛰어 내려앉는다 (17번 그림에서 떠서 28번 그림 끝에 착지)
        private System.Collections.IEnumerator JumpTest()
        {
            Vector2 forward = lookingRight ? Vector2.right : Vector2.left;
            Play("JumpWind");
            yield return new WaitForSeconds(Duration("JumpWind"));
            Play("JumpLeap");
            yield return new WaitForSeconds(Frames("JumpLeap", 4));
            float air = Duration("JumpLeap") - Frames("JumpLeap", 4), t0 = Time.time, height = .8f, distance = 5.5f; // 낮고 멀리: 덮치듯 깔려 날아간다
            Vector3 ground = transform.position;
            while (Time.time - t0 < air)
            {
                float k = (Time.time - t0) / air;
                float h = Mathf.Sin(k * Mathf.PI) * height;
                ground += (Vector3)(forward * distance * Time.deltaTime / air);
                transform.position = ground + Vector3.up * h;
                SetLift(h);
                yield return null;
            }
            transform.position = ground;
            SetLift(0f);
            CameraFollow.Shake(.3f, .25f);
            Play("JumpLand");
        }

        private System.Collections.IEnumerator Sequence(params (string clip, float seconds)[] steps)
        {
            foreach (var (name, seconds) in steps)
            {
                Play(name);
                yield return new WaitForSeconds(seconds);
            }
            Play("Idle");
        }

        private void Update()
        {
            if (!Ready) return;
            if (Time.time < freezeUntil)
            {
                clipStartedAt += Time.deltaTime; // 멈춘 동안은 동작 시간이 흐르지 않는다
                lastPosition = transform.position;
                return;
            }
            Vector3 delta = transform.position - lastPosition;
            if (faceOverride is Vector2 forced && Mathf.Abs(forced.x) > .001f) lookingRight = forced.x > 0f;
            else if (Mathf.Abs(delta.x) > .0005f) lookingRight = delta.x > 0f;
            else if (NearestPlayerX() is float x) lookingRight = x > transform.position.x;

            if (Time.deltaTime > 0f && delta.magnitude / Time.deltaTime > WalkSpeed) movedAt = Time.time;
            bool moving = Time.time - movedAt < WalkHold;
            Locomotion(moving);

            Clip current = Clips[clip];
            int step = Mathf.FloorToInt((Time.time - clipStartedAt) / current.FrameSeconds);
            if (!current.Loop && step >= current.Count && current.Next != Hold)
            {
                Play(current.Next ?? "Idle");
                current = Clips[clip];
                step = 0;
            }
            int index = current.Index(current.Loop ? step % current.Count : Mathf.Min(step, current.Count - 1));
            visual.sprite = frames[current.Sheet][index];
            shownSheet = current.Sheet;
            shownIndex = index;
            // 원본이 보는 쪽과 봐야 할 쪽이 다르면 뒤집는다
            visual.flipX = lookingRight != current.FacesRight;
            lastPosition = transform.position;
        }

        // 대기·걷기 사이 전환: 출발/멈춤 동작을 끼워 넣는다. 다른 동작(공격 등)을 재생 중이면 건드리지 않는다.
        private void Locomotion(bool moving)
        {
            if (!Has("Walk")) return;
            if (moving)
            {
                if (clip == "Idle") Play(Has("WalkStart") ? "WalkStart" : "Walk");
                else if (clip == "WalkStop") Play("Walk");
            }
            else if (clip == "Walk" || clip == "WalkStart") Play(Has("WalkStop") ? "WalkStop" : "Idle");
        }

        // 발 위치 기준으로 나무·기둥과 앞뒤를 정한다 (WorldDepthSort와 같은 기준)
        private void LateUpdate()
        {
            if (!Ready) return;
            shape = Vector2.Lerp(shape, shapeTarget, 1f - Mathf.Exp(-shapeSpeed * Time.deltaTime));
            transform.localScale = new Vector3(baseScale.x * shape.x, baseScale.y * shape.y, baseScale.z);
            float feet = transform.position.y - lift - bodyHeight * Mathf.Abs(baseScale.y) * .5f;
            visual.sortingOrder = 1000 - Mathf.RoundToInt(feet * 100f) + SortBias;
        }

        private float? NearestPlayerX()
        {
            float best = float.MaxValue; float? result = null;
            foreach (var player in FindObjectsByType<PersistentWorld.Player.ArcherController>(FindObjectsSortMode.None))
            {
                float d = (player.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; result = player.transform.position.x; }
            }
            return result;
        }
    }
}
