using UnityEngine;

namespace PersistentWorld.World
{
    // 보스 공격 전 바닥에 깔리는 빨간 경고 범위.
    // 경고 시간 동안 깜빡이며(끝에 가까울수록 빨라짐) 안쪽 진행 표시가 차오르고,
    // Fire()가 호출되면 밝게 번쩍인 뒤 사라진다.
    public sealed class BossTelegraph : MonoBehaviour
    {
        private const float ImpactTime = .18f;
        private const int SortingOrder = -500;

        private static Sprite square;
        private static Sprite framedSquare;
        private static Sprite circle;

        private SpriteRenderer area;
        private SpriteRenderer progress;
        private Color color;
        private float startTime;
        private float warnTime;
        private bool isCircle;
        private Vector2 rectSize;
        private float firedAt = -1f;
        private float steadyAlpha = -1f;
        private float impactTime = ImpactTime;
        private bool expanding;
        private float expandDiameter;

        public static BossTelegraph Circle(Vector2 center, float radius, float warnTime, Color color)
        {
            BossTelegraph telegraph = Create("Telegraph Circle", color, warnTime, CircleSprite, true, Vector2.zero);
            telegraph.transform.position = center;
            telegraph.transform.localScale = Vector3.one * radius * 2f;
            return telegraph;
        }

        public static BossTelegraph Rect(Vector2 start, Vector2 end, float width, float warnTime, Color color)
        {
            Vector2 delta = end - start;
            Vector2 size = new Vector2(Mathf.Max(.01f, delta.magnitude), width);
            BossTelegraph telegraph = Create("Telegraph Rect", color, warnTime, FramedSquareSprite, false, size);
            telegraph.transform.position = (start + end) * .5f;
            telegraph.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            return telegraph;
        }

        // 깜빡이지 않고 고정된 투명도로 표시 (안쪽 면 기준 alpha, 예: .4 = 투명도 40%)
        public BossTelegraph Steady(float alpha)
        {
            steadyAlpha = Mathf.Clamp01(alpha);
            return this;
        }

        // 중심에서 바깥으로 퍼지며 사라지는 충격파 링 (경고 없이 바로 표시)
        public static BossTelegraph Shockwave(Vector2 center, float radius, Color color)
        {
            BossTelegraph telegraph = Circle(center, radius, .05f, color);
            telegraph.expanding = true;
            telegraph.expandDiameter = radius * 2f;
            telegraph.impactTime = .4f;
            telegraph.Fire();
            return telegraph;
        }

        public void Fire()
        {
            if (firedAt >= 0f) return;
            firedAt = Time.time;
            progress.enabled = false;
        }

        public void Cancel()
        {
            if (this != null) Destroy(gameObject);
        }

        private static BossTelegraph Create(string name, Color color, float warnTime, Sprite sprite, bool isCircle, Vector2 rectSize)
        {
            GameObject obj = new GameObject(name);
            BossTelegraph telegraph = obj.AddComponent<BossTelegraph>();
            telegraph.color = color;
            telegraph.warnTime = Mathf.Max(.05f, warnTime);
            telegraph.startTime = Time.time;
            telegraph.isCircle = isCircle;
            telegraph.rectSize = rectSize;
            telegraph.area = obj.AddComponent<SpriteRenderer>();
            telegraph.area.sprite = sprite;
            if (!isCircle)
            {
                telegraph.area.drawMode = SpriteDrawMode.Sliced;
                telegraph.area.size = rectSize;
            }
            telegraph.area.sortingOrder = SortingOrder;

            GameObject progressObject = new GameObject("Progress");
            progressObject.transform.SetParent(obj.transform, false);
            telegraph.progress = progressObject.AddComponent<SpriteRenderer>();
            telegraph.progress.sprite = isCircle ? sprite : SquareSprite;
            telegraph.progress.sortingOrder = SortingOrder + 1;
            telegraph.Update();
            return telegraph;
        }

        private void Update()
        {
            if (firedAt >= 0f)
            {
                float k = Mathf.Clamp01((Time.time - firedAt) / impactTime);
                if (expanding) transform.localScale = Vector3.one * expandDiameter * Mathf.Lerp(.35f, 1.15f, 1f - (1f - k) * (1f - k));
                Color flash = Color.Lerp(new Color(1f, .85f, .8f), color, k);
                area.color = new Color(flash.r, flash.g, flash.b, .85f * (1f - k));
                if (k >= 1f) Destroy(gameObject);
                return;
            }

            float t = Mathf.Clamp01((Time.time - startTime) / warnTime);
            // 깜빡임: 처음엔 느리게, 발동 직전엔 빠르게
            float phase = (Time.time - startTime) * Mathf.Lerp(4f, 18f, t * t);
            float blink = .5f + .5f * Mathf.Sin(phase * Mathf.PI * 2f);
            Color bright = Color.Lerp(color, new Color(1f, .55f, .3f), blink * .6f);
            area.color = steadyAlpha >= 0f
                ? new Color(color.r, color.g, color.b, Mathf.Min(1f, steadyAlpha / .45f))
                : new Color(bright.r, bright.g, bright.b, Mathf.Lerp(.35f, .8f, blink));
            progress.color = new Color(1f, .35f, .2f, steadyAlpha >= 0f ? .2f : .45f);
            if (isCircle)
            {
                progress.transform.localScale = Vector3.one * t;
                progress.transform.localPosition = Vector3.zero;
            }
            else
            {
                progress.transform.localScale = new Vector3(rectSize.x * t, rectSize.y, 1f);
                progress.transform.localPosition = new Vector3(-rectSize.x * .5f + rectSize.x * t * .5f, 0f, 0f);
            }
        }

        private static Sprite SquareSprite
        {
            get
            {
                if (square != null) return square;
                Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                square = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(.5f, .5f), 1f);
                return square;
            }
        }

        // 테두리 3px은 진하고 안쪽은 옅은 9-slice 사각형 (테두리 두께 약 0.08 유닛)
        private static Sprite FramedSquareSprite
        {
            get
            {
                if (framedSquare != null) return framedSquare;
                const int size = 16;
                const int border = 3;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < border || y < border || x >= size - border || y >= size - border;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, edge ? 1f : .45f));
                }
                texture.Apply();
                framedSquare = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 38f, 0,
                    SpriteMeshType.FullRect, new Vector4(border, border, border, border));
                return framedSquare;
            }
        }

        private static Sprite CircleSprite
        {
            get
            {
                if (circle != null) return circle;
                const int size = 128;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                float r = size * .5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r)) / r;
                    // 안쪽은 옅게, 테두리는 진하게
                    float a = d > 1f ? 0f : d > .93f ? 1f : .45f;
                    if (d > .97f && d <= 1f) a = Mathf.Lerp(1f, 0f, (d - .97f) / .03f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                texture.Apply();
                circle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
                return circle;
            }
        }
    }
}
