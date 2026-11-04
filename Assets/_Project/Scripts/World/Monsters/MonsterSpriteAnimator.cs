using System.Collections.Generic;
using UnityEngine;

namespace PersistentWorld.World
{
    public enum MonsterArt { Monster01, Monster02, Monster03 }
    public enum MonsterAnimPhase { Move, Windup, Attack, Recover }

    // 몬스터 컨트롤러가 현재 행동 단계와 바라보는 방향을 알려 준다.
    public interface IMonsterAnimationSource
    {
        MonsterAnimPhase Phase { get; }
        float PhaseProgress { get; } // 현재 단계 안의 진행도 0~1
        Vector2 Facing { get; }
    }

    // Resources/Monsters/{Art}Move·Idle·Attack 시트를 잘라 컨트롤러 단계에 맞춰 재생한다.
    // 공격 시트는 준비·공격·회복 구간으로 나눠 각 단계 진행도에 맞춰 넘긴다.
    // 크기는 이동 시트 첫 프레임 키가 height × 스케일(월드 단위)이 되도록 맞춘다.
    // 비균등 스케일은 큰 축으로 균등하게 바꾼다. 원형 콜라이더는 큰 축을 따르므로 판정 크기는 그대로다.
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MonsterSpriteAnimator : MonoBehaviour
    {
        private readonly struct Sheet
        {
            public readonly string Suffix;
            public readonly int Columns, Rows;
            public Sheet(string suffix, int columns, int rows) { Suffix = suffix; Columns = columns; Rows = rows; }
        }

        private readonly struct Definition
        {
            public readonly Sheet Move, Attack;
            public readonly Sheet? Idle;
            public readonly int WindupEnd, AttackEnd; // 공격 시트에서 준비 [0, WindupEnd), 공격 [WindupEnd, AttackEnd), 회복 [AttackEnd, 끝)
            public readonly bool AttackMirrored; // 공격 시트가 이동 시트와 반대쪽을 보고 그려졌는지
            public Definition(Sheet move, Sheet attack, int windupEnd, int attackEnd, Sheet? idle = null, bool attackMirrored = false)
            { Move = move; Attack = attack; WindupEnd = windupEnd; AttackEnd = attackEnd; Idle = idle; AttackMirrored = attackMirrored; }
        }

        private sealed class Frames
        {
            public Sprite[] Move, Idle, Attack;
        }

        private const float FrameDuration = .08f;
        private static readonly Dictionary<(MonsterArt, float), Frames> Cache = new Dictionary<(MonsterArt, float), Frames>();

        [SerializeField] private MonsterArt art = MonsterArt.Monster01;
        [SerializeField, Tooltip("스케일 1일 때 이동 시트 첫 프레임 몸 높이(월드 단위)")] private float height = 1f;
        [SerializeField] private Color tint = Color.white;
        [SerializeField, Tooltip("원본 그림이 오른쪽을 보고 있으면 체크")] private bool sourceFacesRight = true;

        private SpriteRenderer visual;
        private Rigidbody2D body;
        private IMonsterAnimationSource source;
        private Frames frames;
        private float loopTimer;

        public bool Ready => frames != null;

        private static Definition Define(MonsterArt id) => id switch
        {
            // 초록 슬라임: 납작하게 움츠림 → 잔상을 남기며 돌진 → 원래 모양으로 복귀. 이동은 왼쪽, 돌진은 오른쪽을 보고 그려져 있다.
            MonsterArt.Monster01 => new Definition(new Sheet("Move", 6, 6), new Sheet("Attack", 6, 6), 18, 24, attackMirrored: true),
            // 가방 멘 초록 몬스터: 팔을 뒤로 빼며 조준 → 던진 뒤 자세 복귀
            MonsterArt.Monster02 => new Definition(new Sheet("Move", 4, 3), new Sheet("Attack", 6, 6), 18, 36, new Sheet("Idle", 4, 3)),
            // 흰 고양이: 웅크림 → 몸을 펴며 뛰어오름 → 다시 웅크렸다 일어남. 공격 시트가 이동 시트와 반대쪽을 보고 그려져 있다.
            _ => new Definition(new Sheet("Move", 6, 6), new Sheet("Attack", 6, 6), 8, 24, attackMirrored: true)
        };

        private void Awake()
        {
            visual = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
        }

        // 실행 순서를 앞당겨 다른 컴포넌트가 Start에서 스케일·색을 기록하기 전에 바꾼다
        private void Start()
        {
            source = GetComponent<IMonsterAnimationSource>();
            frames = Load(art, height);
            if (frames == null) return;
            Vector3 scale = transform.localScale;
            float uniform = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            transform.localScale = new Vector3(uniform, uniform, scale.z);
            visual.color = tint;
            visual.sprite = frames.Move[0];
        }

        private void Update()
        {
            if (frames == null) return;
            MonsterAnimPhase phase = source != null ? source.Phase : MonsterAnimPhase.Move;
            if (phase == MonsterAnimPhase.Move)
            {
                bool moving = body != null && body.linearVelocity.sqrMagnitude > .01f;
                Sprite[] loop = !moving && frames.Idle != null ? frames.Idle : frames.Move;
                loopTimer += Time.deltaTime;
                visual.sprite = loop[Mathf.FloorToInt(loopTimer / FrameDuration) % loop.Length];
            }
            else
            {
                Definition definition = Define(art);
                int start = phase == MonsterAnimPhase.Windup ? 0 : phase == MonsterAnimPhase.Attack ? definition.WindupEnd : definition.AttackEnd;
                int end = phase == MonsterAnimPhase.Windup ? definition.WindupEnd : phase == MonsterAnimPhase.Attack ? definition.AttackEnd : frames.Attack.Length;
                end = Mathf.Clamp(end, start + 1, frames.Attack.Length);
                int index = start + (int)(Mathf.Clamp01(source.PhaseProgress) * (end - start));
                visual.sprite = frames.Attack[Mathf.Clamp(index, 0, end - 1)];
                loopTimer = 0f;
            }

            Vector2 facing = source != null ? source.Facing : body != null ? body.linearVelocity : Vector2.zero;
            bool facesRight = sourceFacesRight ^ (phase != MonsterAnimPhase.Move && Define(art).AttackMirrored);
            if (Mathf.Abs(facing.x) > .05f) visual.flipX = facesRight ? facing.x < 0f : facing.x > 0f;
        }

        private static Frames Load(MonsterArt id, float bodyHeight)
        {
            if (Cache.TryGetValue((id, bodyHeight), out Frames cached)) return cached;
            Definition definition = Define(id);
            Texture2D move = Texture(id, definition.Move);
            if (move == null) return null;
            // 이동 시트 첫 프레임(기본 자세)의 키로 크기를 정한다. 세 시트 모두 같은 배율로 그려져 있어 같은 PPU를 쓴다.
            RectInt first = SpriteSheetSlicer.FirstFrameBounds(move, definition.Move.Columns, definition.Move.Rows);
            float ppu = first.height / Mathf.Max(.01f, bodyHeight);
            var result = new Frames
            {
                Move = Slice(id, move, definition.Move, ppu, first.height),
                Attack = Slice(id, Texture(id, definition.Attack), definition.Attack, ppu, first.height)
            };
            if (definition.Idle.HasValue) result.Idle = Slice(id, Texture(id, definition.Idle.Value), definition.Idle.Value, ppu, first.height);
            if (result.Attack == null) return null;
            Cache[(id, bodyHeight)] = result;
            return result;
        }

        private static Texture2D Texture(MonsterArt id, Sheet sheet) =>
            SpriteSheetSlicer.Load("Monsters/" + id + sheet.Suffix, sheet.Columns, sheet.Rows);

        private static Sprite[] Slice(MonsterArt id, Texture2D texture, Sheet sheet, float ppu, int bodyHeight) =>
            SpriteSheetSlicer.Slice(texture, sheet.Columns, sheet.Rows, ppu, bodyHeight, id + sheet.Suffix);
    }
}
