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
    // 에디터에서는 그림이 실행 중에만 붙으므로, 씬에 배치할 때 보이도록 저장되지 않는 미리보기를 띄운다.
    [DefaultExecutionOrder(-100)]
    [SelectionBase]
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
            public Definition(Sheet move, Sheet attack, int windupEnd, int attackEnd, Sheet? idle = null)
            { Move = move; Attack = attack; WindupEnd = windupEnd; AttackEnd = attackEnd; Idle = idle; }
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
            // 초록 슬라임: 납작하게 움츠림 → 잔상을 남기며 돌진 → 원래 모양으로 복귀
            MonsterArt.Monster01 => new Definition(new Sheet("Move", 6, 6), new Sheet("Attack", 6, 6), 18, 24),
            // 가방 멘 초록 몬스터: 팔을 뒤로 빼며 조준 → 던진 뒤 자세 복귀
            MonsterArt.Monster02 => new Definition(new Sheet("Move", 4, 3), new Sheet("Attack", 6, 6), 18, 36, new Sheet("Idle", 4, 3)),
            // 흰 고양이: 웅크림 → 몸을 펴며 뛰어오름 → 다시 웅크렸다 일어남
            _ => new Definition(new Sheet("Move", 6, 6), new Sheet("Attack", 6, 6), 8, 24)
        };

        private void Awake()
        {
            visual = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
        }

        // 실행 순서를 앞당겨 다른 컴포넌트가 Start에서 스케일·색을 기록하기 전에 바꾼다
        private void Start()
        {
#if UNITY_EDITOR
            ClearPreview();
#endif
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
            if (Mathf.Abs(facing.x) > .05f) visual.flipX = sourceFacesRight ? facing.x < 0f : facing.x > 0f;
        }

#if UNITY_EDITOR
        private const string PreviewName = "Monster Preview (editor only)";

        // 씬에 놓이거나 값이 바뀔 때마다 미리보기를 다시 만든다. 프리팹 파일 자체에는 만들지 않는다.
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += RefreshPreview;
        }

        private void RefreshPreview()
        {
            if (this == null || Application.isPlaying || UnityEditor.EditorUtility.IsPersistent(gameObject)) return;
            ClearPreview();
            Frames preview = Load(art, height);
            if (preview == null) return;
            var obj = new GameObject(PreviewName) { hideFlags = HideFlags.HideAndDontSave };
            obj.transform.SetParent(transform, false);
            // 실행 시 균등하게 바뀌는 스케일을 미리 반영한다
            Vector3 scale = transform.localScale;
            float uniform = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            obj.transform.localScale = new Vector3(uniform / Mathf.Max(.0001f, Mathf.Abs(scale.x)), uniform / Mathf.Max(.0001f, Mathf.Abs(scale.y)), 1f);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = preview.Move[0];
            renderer.color = tint;
            renderer.sortingOrder = 1000 - Mathf.RoundToInt(transform.position.y * 100f);
        }

        private void ClearPreview()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == PreviewName && (child.hideFlags & HideFlags.DontSave) != 0) DestroyImmediate(child.gameObject);
            }
        }
#endif

        private static Frames Load(MonsterArt id, float bodyHeight)
        {
            if (Cache.TryGetValue((id, bodyHeight), out Frames cached)) return cached;
            Definition definition = Define(id);
            Texture2D move = Texture(id, definition.Move);
            if (move == null) return null;
            // 이동 시트 첫 프레임(기본 자세)의 키로 크기를 정한다. 세 시트 모두 같은 배율로 그려져 있어 같은 PPU를 쓴다.
            RectInt first = Bounds(move.GetPixels32(), move, definition.Move, 0, 1);
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

        private static Texture2D Texture(MonsterArt id, Sheet sheet)
        {
            Texture2D texture = Resources.Load<Texture2D>("Monsters/" + id + sheet.Suffix);
            if (texture == null || !texture.isReadable || texture.width % sheet.Columns != 0 || texture.height % sheet.Rows != 0) return null;
            return texture;
        }

        // 모든 프레임을 같은 영역(전체 프레임 알파 경계의 합집합)으로 잘라 위치가 흔들리지 않게 한다.
        // 피벗은 첫 프레임의 가로 중심, 발에서 몸 높이 절반 위로 맞춰 시트가 바뀌어도 기본 자세가 같은 자리에 온다.
        private static Sprite[] Slice(MonsterArt id, Texture2D texture, Sheet sheet, float ppu, int bodyHeight)
        {
            if (texture == null) return null;
            int count = sheet.Columns * sheet.Rows;
            Color32[] pixels = texture.GetPixels32();
            RectInt union = Bounds(pixels, texture, sheet, 0, count);
            RectInt first = Bounds(pixels, texture, sheet, 0, 1);
            var pivot = new Vector2((first.center.x - union.x) / union.width, (first.y + bodyHeight * .5f - union.y) / union.height);
            int width = texture.width / sheet.Columns, cellHeight = texture.height / sheet.Rows;
            var sprites = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                int x = i % sheet.Columns * width, y = (sheet.Rows - 1 - i / sheet.Columns) * cellHeight;
                sprites[i] = Sprite.Create(texture, new Rect(x + union.x, y + union.y, union.width, union.height), pivot, ppu, 0, SpriteMeshType.FullRect);
                sprites[i].name = id + sheet.Suffix + "_" + i.ToString("00");
            }
            return sprites;
        }

        // 셀 안 좌표로 [from, to) 프레임의 알파 경계 합집합
        private static RectInt Bounds(Color32[] pixels, Texture2D texture, Sheet sheet, int from, int to)
        {
            int width = texture.width / sheet.Columns, height = texture.height / sheet.Rows;
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int i = from; i < to; i++)
            {
                int ox = i % sheet.Columns * width, oy = (sheet.Rows - 1 - i / sheet.Columns) * height;
                for (int py = 0; py < height; py++)
                    for (int px = 0; px < width; px++)
                        if (pixels[(oy + py) * texture.width + ox + px].a > 8)
                        { minX = Mathf.Min(minX, px); minY = Mathf.Min(minY, py); maxX = Mathf.Max(maxX, px); maxY = Mathf.Max(maxY, py); }
            }
            return maxX < minX ? new RectInt(0, 0, width, height) : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}
