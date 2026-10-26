using UnityEngine;

namespace PersistentWorld.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(ArcherController))]
    public sealed class ArcherSpriteAnimator : MonoBehaviour
    {
        private const string ResourcePath = "Characters/ArcherRun";
        private const int Columns = 4;
        private const int Rows = 2;
        private const int FrameCount = Columns * Rows;
        private const int AlphaThreshold = 8;
        private const int Padding = 8;
        private const float PixelsPerUnit = 180f;

        [SerializeField] private float framesPerSecond = 10f;

        private static Texture2D cachedTexture;
        private static Sprite[] cachedFrames;

        private SpriteRenderer spriteRenderer;
        private Rigidbody2D body;
        private ArcherController controller;
        private int frameIndex;
        private float frameTimer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            controller = GetComponent<ArcherController>();
            cachedFrames = GetOrCreateFrames();

            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 10;
            SetFrame(0);
        }

        private void Update()
        {
            if (cachedFrames == null || cachedFrames.Length == 0)
            {
                return;
            }

            if (Mathf.Abs(controller.AimDirection.x) > 0.01f)
            {
                spriteRenderer.flipX = controller.AimDirection.x < 0f;
            }

            if (body.linearVelocity.sqrMagnitude < 0.01f)
            {
                frameTimer = 0f;
                frameIndex = 0;
                SetFrame(frameIndex);
                return;
            }

            frameTimer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
            if (frameTimer < frameDuration)
            {
                return;
            }

            frameTimer %= frameDuration;
            frameIndex = (frameIndex + 1) % cachedFrames.Length;
            SetFrame(frameIndex);
        }

        private void SetFrame(int index)
        {
            if (cachedFrames != null && index >= 0 && index < cachedFrames.Length)
            {
                spriteRenderer.sprite = cachedFrames[index];
            }
        }

        private static Sprite[] GetOrCreateFrames()
        {
            Texture2D texture = Resources.Load<Texture2D>(ResourcePath);
            if (texture == null)
            {
                Debug.LogError($"Missing player sprite sheet at Resources/{ResourcePath}.");
                return null;
            }

            if (cachedTexture == texture && cachedFrames != null && cachedFrames.Length == FrameCount)
            {
                return cachedFrames;
            }

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (UnityException exception)
            {
                Debug.LogError("The player sprite sheet must have Read/Write enabled. " + exception.Message);
                return null;
            }

            int cellWidth = texture.width / Columns;
            int cellHeight = texture.height / Rows;
            Sprite[] frames = new Sprite[FrameCount];

            for (int index = 0; index < FrameCount; index++)
            {
                int column = index % Columns;
                int rowFromTop = index / Columns;
                int cellX = column * cellWidth;
                int cellY = (Rows - 1 - rowFromTop) * cellHeight;
                Rect rect = FindVisibleRect(pixels, texture.width, cellX, cellY, cellWidth, cellHeight);

                Sprite frame = Sprite.Create(
                    texture,
                    rect,
                    new Vector2(0.5f, 0.03f),
                    PixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);
                frame.name = $"ArcherRun_{index:00}";
                frames[index] = frame;
            }

            cachedTexture = texture;
            cachedFrames = frames;
            return cachedFrames;
        }

        private static Rect FindVisibleRect(
            Color32[] pixels,
            int textureWidth,
            int cellX,
            int cellY,
            int cellWidth,
            int cellHeight)
        {
            int minX = cellWidth;
            int minY = cellHeight;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < cellHeight; y++)
            {
                int sourceY = cellY + y;
                for (int x = 0; x < cellWidth; x++)
                {
                    Color32 pixel = pixels[sourceY * textureWidth + cellX + x];
                    if (pixel.a <= AlphaThreshold)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return new Rect(cellX, cellY, cellWidth, cellHeight);
            }

            int left = Mathf.Max(cellX, cellX + minX - Padding);
            int bottom = Mathf.Max(cellY, cellY + minY - Padding);
            int right = Mathf.Min(cellX + cellWidth, cellX + maxX + Padding + 1);
            int top = Mathf.Min(cellY + cellHeight, cellY + maxY + Padding + 1);
            return new Rect(left, bottom, right - left, top - bottom);
        }
    }
}
