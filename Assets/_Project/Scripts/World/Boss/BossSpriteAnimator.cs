using System.Collections.Generic;
using UnityEngine;

namespace PersistentWorld.World
{
    public enum BossClip { Move, Stand, ChargeWindup, ChargeRun, ChargeEnd, CannonForm, CannonHold, CannonFire, CannonReturn, QuakeWindup, QuakeSlam, QuakeRecover }

    // Resources/Boss 시트를 잘라 BossController가 지시하는 동작을 재생한다. 지시가 없으면 이동 시트를 반복한다.
    // 돌진(Pattern01) / 탄막·유도탄·디버프는 대포 변신(Pattern02·03·05) / 지진은 내려찍기(Pattern04).
    // 크기는 이동 시트 첫 프레임 키가 bodyHeight × 스케일이 되도록 맞추고, 모든 보스 시트에 같은 배율을 쓴다.
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BossSpriteAnimator : MonoBehaviour
    {
        private readonly struct Clip
        {
            public readonly string Sheet;
            public readonly int[] Frames;
            public readonly bool Loop, ReturnsToMove;
            public readonly bool? FacesRight; // null이면 정면 그림이라 좌우를 바꾸지 않는다
            public Clip(string sheet, int[] frames, bool? facesRight, bool loop = false, bool returnsToMove = false)
            { Sheet = sheet; Frames = frames; FacesRight = facesRight; Loop = loop; ReturnsToMove = returnsToMove; }
        }

        private const float MoveFrameDuration = .08f;
        private static readonly Dictionary<string, (int Columns, int Rows)> Sheets = new Dictionary<string, (int, int)>
        {
            { "Move", (6, 4) }, { "Charge", (6, 6) }, { "Cannon", (6, 6) }, { "Quake", (6, 6) }
        };

        [SerializeField, Tooltip("스케일 1일 때 이동 시트 첫 프레임 몸 높이(월드 단위)")] private float bodyHeight = 1.4f;
        [SerializeField, Tooltip("탄막 쇠구슬 지름(월드 단위)")] private float barrageBallSize = .35f;
        [SerializeField, Tooltip("디버프 바위 지름(월드 단위)")] private float debuffRockSize = .75f;

        private readonly Dictionary<string, Sprite[]> frames = new Dictionary<string, Sprite[]>();
        private SpriteRenderer visual;
        private Clip current;
        private bool playingClip;
        private float clipStartedAt, clipDuration;
        private float moveTimer;
        private Vector3 lastPosition;
        private bool lookingRight;

        public bool Ready { get; private set; }
        public Sprite BarrageBall { get; private set; }
        public Sprite HomingBall { get; private set; } // 원래 유도탄 크기(스케일)에서 그대로 보이도록 1유닛 크기
        public Sprite[] DebuffRock { get; private set; }

        private static Clip Define(BossClip clip) => clip switch
        {
            BossClip.Stand => new Clip("Quake", new[] { 0 }, null, loop: true),
            BossClip.ChargeWindup => new Clip("Charge", Range(0, 18), true),
            BossClip.ChargeRun => new Clip("Charge", Range(18, 28), true, loop: true),
            BossClip.ChargeEnd => new Clip("Charge", Range(28, 36), true, returnsToMove: true),
            BossClip.CannonForm => new Clip("Cannon", Range(0, 18), true),
            BossClip.CannonHold => new Clip("Cannon", Range(18, 22), true, loop: true),
            BossClip.CannonFire => new Clip("Cannon", Range(22, 30), true),
            // 연기가 걷힌 뒤 변신 프레임을 거꾸로 돌려 골렘으로 돌아온다
            BossClip.CannonReturn => new Clip("Cannon", Concat(Range(30, 36), Reverse(Range(0, 13))), true, returnsToMove: true),
            BossClip.QuakeWindup => new Clip("Quake", Range(0, 14), null),
            BossClip.QuakeSlam => new Clip("Quake", Range(14, 24), null),
            BossClip.QuakeRecover => new Clip("Quake", Range(24, 36), null, returnsToMove: true),
            _ => new Clip("Move", Range(0, 24), false, loop: true)
        };

        private void Awake()
        {
            visual = GetComponent<SpriteRenderer>();
            Load();
        }

        private void Start()
        {
            if (!Ready) return;
            // PlaceholderVisual이 Awake에서 정한 비균등 스케일을 균등하게. 원형 콜라이더는 큰 축을 따르므로 판정 크기는 그대로다.
            Vector3 scale = transform.localScale;
            float uniform = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            transform.localScale = new Vector3(uniform, uniform, scale.z);
            visual.color = Color.white;
            lastPosition = transform.position;
            PlayMove();
        }

        // duration: 반복 동작은 한 바퀴, 그 외는 전체 재생 시간. facing이 있으면 그 방향을 보게 뒤집는다.
        public void Play(BossClip clip, float duration, Vector2 facing = default)
        {
            if (!Ready) return;
            current = Define(clip);
            playingClip = clip != BossClip.Move;
            clipStartedAt = Time.time;
            clipDuration = Mathf.Max(.01f, duration);
            Face(facing);
            Update();
        }

        public void PlayMove() => Play(BossClip.Move, 1f);

        private void Update()
        {
            if (!Ready) return;
            if (!playingClip)
            {
                Vector3 delta = transform.position - lastPosition;
                if (Mathf.Abs(delta.x) > .0005f) Face(delta);
                moveTimer += Time.deltaTime;
                Sprite[] move = frames["Move"];
                visual.sprite = move[Mathf.FloorToInt(moveTimer / MoveFrameDuration) % move.Length];
            }
            else
            {
                float progress = (Time.time - clipStartedAt) / clipDuration;
                if (!current.Loop && progress >= 1f && current.ReturnsToMove) { PlayMove(); return; }
                int index = current.Loop
                    ? Mathf.FloorToInt(progress * current.Frames.Length) % current.Frames.Length
                    : Mathf.Min(Mathf.FloorToInt(Mathf.Clamp01(progress) * current.Frames.Length), current.Frames.Length - 1);
                visual.sprite = frames[current.Sheet][current.Frames[index]];
            }
            lastPosition = transform.position;
        }

        // 발 위치 기준으로 나무·기둥과 앞뒤를 정한다 (WorldDepthSort와 같은 기준)
        private void LateUpdate()
        {
            if (!Ready) return;
            float feet = transform.position.y - bodyHeight * Mathf.Abs(transform.localScale.y) * .5f;
            visual.sortingOrder = 1000 - Mathf.RoundToInt(feet * 100f);
        }

        // 바라보는 방향은 기억해 두고, 시트마다 원본 방향이 달라도 같은 쪽을 보게 뒤집는다
        private void Face(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) > .0001f) lookingRight = direction.x > 0f;
            visual.flipX = current.FacesRight is bool facesRight && facesRight != lookingRight;
        }

        private void Load()
        {
            Texture2D moveTexture = SpriteSheetSlicer.Load("Boss/BossMove", 6, 4);
            if (moveTexture == null) return;
            RectInt first = SpriteSheetSlicer.FirstFrameBounds(moveTexture, 6, 4);
            float ppu = first.height / Mathf.Max(.01f, bodyHeight);
            foreach (var sheet in Sheets)
            {
                Texture2D texture = SpriteSheetSlicer.Load("Boss/Boss" + sheet.Key, sheet.Value.Columns, sheet.Value.Rows);
                Sprite[] sliced = SpriteSheetSlicer.Slice(texture, sheet.Value.Columns, sheet.Value.Rows, ppu, first.height, "Boss" + sheet.Key);
                if (sliced == null) return;
                frames[sheet.Key] = sliced;
            }
            BarrageBall = Single("Boss/BossBarrageBall", barrageBallSize);
            HomingBall = Single("Boss/BossHomingBall", 1f);
            Texture2D rock = SpriteSheetSlicer.Load("Boss/BossDebuffRock", 6, 6);
            if (rock != null)
            {
                int rockHeight = SpriteSheetSlicer.FirstFrameBounds(rock, 6, 6).height;
                DebuffRock = SpriteSheetSlicer.Slice(rock, 6, 6, rockHeight / debuffRockSize, rockHeight, "BossDebuffRock");
            }
            Ready = true;
        }

        // 한 장짜리 그림을 알파 경계로 잘라 가운데 피벗, 지름 size로 만든다
        private static Sprite Single(string path, float size)
        {
            Texture2D texture = SpriteSheetSlicer.Load(path, 1, 1);
            if (texture == null) return null;
            int height = SpriteSheetSlicer.FirstFrameBounds(texture, 1, 1).height;
            return SpriteSheetSlicer.Slice(texture, 1, 1, height / size, height, texture.name)[0];
        }

        private static int[] Range(int from, int to)
        {
            var result = new int[to - from];
            for (int i = 0; i < result.Length; i++) result[i] = from + i;
            return result;
        }

        private static int[] Reverse(int[] values)
        {
            System.Array.Reverse(values);
            return values;
        }

        private static int[] Concat(int[] a, int[] b)
        {
            var result = new int[a.Length + b.Length];
            a.CopyTo(result, 0);
            b.CopyTo(result, a.Length);
            return result;
        }
    }
}
