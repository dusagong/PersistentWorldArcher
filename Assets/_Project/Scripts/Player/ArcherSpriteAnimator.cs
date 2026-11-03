using System.Collections.Generic;
using UnityEngine;

namespace PersistentWorld.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(ArcherController))]
    public sealed class ArcherSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private float framesPerSecond = 12.5f;
        private static readonly Dictionary<CharacterClass, Sprite[]> Runs = new Dictionary<CharacterClass, Sprite[]>();
        private static readonly Dictionary<CharacterClass, Sprite[]> Dashes = new Dictionary<CharacterClass, Sprite[]>();
        private SpriteRenderer visual;
        private Rigidbody2D body;
        private ArcherController controller;
        private Sprite[] runFrames, dashFrames;
        private CharacterClass loadedClass = (CharacterClass)(-1);
        private float frameTimer, dashStartedAt;
        private int frameIndex;
        private bool playingDash;
        private const float DashDuration = .6f;

        private void Awake()
        {
            visual = GetComponent<SpriteRenderer>(); body = GetComponent<Rigidbody2D>();
            controller = GetComponent<ArcherController>();
            RefreshClass();
        }

        public bool RefreshClass()
        {
            if (controller == null) controller = GetComponent<ArcherController>();
            if (visual == null) visual = GetComponent<SpriteRenderer>();
            // Unity may awaken the animator before the controller creates its runtime loadout.
            if (controller == null || controller.Loadout == null) return false;
            CharacterClass type = controller.Loadout.Class;
            runFrames = RunFrames(type); dashFrames = DashFrames(type);
            if (runFrames == null || dashFrames == null) return false;
            loadedClass = type; playingDash = false; frameIndex = 0; frameTimer = 0;
            visual.color = Color.white; visual.sprite = runFrames[0];
            return true;
        }

        private void OnEnable()
        {
            controller = GetComponent<ArcherController>();
            controller.BackstepStarted += PlayDash;
        }
        private void OnDisable() { controller.BackstepStarted -= PlayDash; playingDash = false; }
        private void PlayDash()
        {
            if (dashFrames == null) return;
            dashStartedAt = Time.time; playingDash = true;
            visual.flipX = controller.AimDirection.x < 0;
            visual.sprite = dashFrames[0];
        }
        private void Update()
        {
            if (loadedClass != controller.Loadout.Class && !RefreshClass()) return;
            if (playingDash)
            {
                float progress = (Time.time - dashStartedAt) / DashDuration;
                if (progress < 1)
                {
                    visual.sprite = dashFrames[Mathf.Min((int)(progress * dashFrames.Length), dashFrames.Length - 1)];
                    return;
                }
                playingDash = false;
            }
            if (Mathf.Abs(controller.AimDirection.x) > .01f) visual.flipX = controller.AimDirection.x < 0;
            if (body.linearVelocity.sqrMagnitude < .01f)
            { frameTimer = 0; frameIndex = 0; visual.sprite = runFrames[0]; return; }
            frameTimer += Time.deltaTime;
            float interval = 1 / Mathf.Max(1, framesPerSecond);
            if (frameTimer >= interval)
            { frameIndex = (frameIndex + Mathf.FloorToInt(frameTimer / interval)) % runFrames.Length; frameTimer %= interval; }
            visual.sprite = runFrames[frameIndex];
        }

        public static Sprite[] RunFrames(CharacterClass type)
        {
            if (Runs.TryGetValue(type, out Sprite[] frames)) return frames;
            int columns = type == CharacterClass.Warrior ? 11 : 4;
            int rows = type == CharacterClass.Warrior ? 1 : type == CharacterClass.Healer ? 3 : 2;
            Texture2D texture = Resources.Load<Texture2D>("Characters/" + type + "Run");
            if (texture == null || !texture.isReadable) return null;
            int width = texture.width / columns, height = texture.height / rows;
            Color32[] pixels = texture.GetPixels32();
            frames = new Sprite[columns * rows];
            float ppu = PixelsPerUnit(texture, columns);
            for (int i = 0; i < frames.Length; i++)
            {
                int x = i % columns * width, y = (rows - 1 - i / columns) * height;
                int minX = x + width, minY = y + height, maxX = x - 1, maxY = y - 1;
                for (int py = y; py < y + height; py++)
                    for (int px = x; px < x + width; px++)
                        if (pixels[py * texture.width + px].a > 8)
                        { minX = Mathf.Min(minX, px); minY = Mathf.Min(minY, py); maxX = Mathf.Max(maxX, px); maxY = Mathf.Max(maxY, py); }
                Rect rect;
                if (maxX < minX) rect = new Rect(x, y, width, height);
                else
                {
                    minX = Mathf.Max(x, minX - 8); minY = Mathf.Max(y, minY - 8);
                    maxX = Mathf.Min(x + width - 1, maxX + 8); maxY = Mathf.Min(y + height - 1, maxY + 8);
                    rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                }
                frames[i] = Sprite.Create(texture, rect, new Vector2(.5f, .03f), ppu, 0, SpriteMeshType.FullRect);
                frames[i].name = type + "Run_" + i.ToString("00");
            }
            Runs[type] = frames;
            return frames;
        }

        public static Sprite[] DashFrames(CharacterClass type)
        {
            if (Dashes.TryGetValue(type, out Sprite[] frames)) return frames;
            int columns = type == CharacterClass.Healer ? 6 : 4;
            int rows = type == CharacterClass.Healer ? 4 : 3;
            Texture2D texture = Resources.Load<Texture2D>("Characters/" + type + "Dash");
            if (texture == null) return null;
            int width = texture.width / columns, height = texture.height / rows;
            float ppu = PixelsPerUnit(texture, columns);
            frames = new Sprite[columns * rows];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = Sprite.Create(texture, new Rect(i % columns * width, (rows - 1 - i / columns) * height, width, height), new Vector2(.5f, .06f), ppu, 0, SpriteMeshType.FullRect);
                frames[i].name = type + "Dash_" + i.ToString("00");
            }
            Dashes[type] = frames;
            return frames;
        }

        private static float PixelsPerUnit(Texture2D texture, int columns)
        {
            Texture2D archer = Resources.Load<Texture2D>("Characters/ArcherRun");
            // Preserve the original archer's world size, including its existing import downscale.
            return 300f * texture.width / (columns * 640f) * 2560f / archer.width;
        }
    }
}
