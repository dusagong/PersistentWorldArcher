using UnityEngine;

namespace PersistentWorld.World
{
    // 적이 죽을 때: 흰 실루엣으로 번쩍인 뒤 몸이 납작하게 주저앉아 사라지고, 몸 색 조각과 먼지가 튄다.
    // 조각 색은 그림에서 뽑고, 몸집이 크면(보스) 조각이 많아지며 화면도 흔들린다.
    public sealed class DeathBurst : MonoBehaviour
    {
        private const float Lifetime = .42f;
        private const float FlashTime = .12f;
        private const float BigBodySize = 2f;
        private const int PlaceholderTextureSize = 4;
        private static readonly Color Stone = new Color(.52f, .52f, .54f);

        private SpriteRenderer body;
        private SpriteRenderer flash;
        private Vector3 baseScale;
        private Vector3 basePosition;
        private float halfHeight;
        private Color bodyColor;
        private float age;

        // leaveGhost: 몸이 사라지는 대상만 잔상을 남긴다 (부서져도 남는 기둥은 조각만 튄다)
        public static void Spawn(SpriteRenderer source, bool leaveGhost)
        {
            Bounds bounds = source.bounds;
            float size = Mathf.Max(bounds.size.x, bounds.size.y);
            GroundBurst.Shatter(bounds.center, size * .55f, Palette(source));
            if (size >= BigBodySize) CameraFollow.Shake(.5f, .6f);
            if (!leaveGhost) return;

            var root = new GameObject("Death Burst").AddComponent<DeathBurst>();
            root.basePosition = source.transform.position;
            root.transform.position = root.basePosition;
            root.baseScale = source.transform.lossyScale;
            root.halfHeight = bounds.extents.y;
            root.bodyColor = source.color;
            root.body = root.Layer("Body", source, source.color, source.sortingOrder + 2);
            root.flash = root.Layer("Flash", source, Color.white, source.sortingOrder + 3);
            if (CombatFeedback.SilhouetteMaterial != null) root.flash.sharedMaterial = CombatFeedback.SilhouetteMaterial;
            root.transform.localScale = root.baseScale;
        }

        private SpriteRenderer Layer(string name, SpriteRenderer source, Color color, int order)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.flipX = source.flipX;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        // 그림에서 불투명한 점을 뽑아 조각 색으로 쓴다. 단색 도형은 도형 색, 읽을 수 없는 그림은 돌색이다.
        private static Color[] Palette(SpriteRenderer source)
        {
            Sprite sprite = source.sprite;
            if (sprite == null || sprite.texture.width <= PlaceholderTextureSize) return new[] { source.color };
            if (!sprite.texture.isReadable) return new[] { Stone, Stone * .8f };
            Rect rect = sprite.rect;
            var colors = new System.Collections.Generic.List<Color>();
            for (int i = 0; i < 80 && colors.Count < 12; i++)
            {
                Color c = sprite.texture.GetPixel((int)Random.Range(rect.xMin, rect.xMax), (int)Random.Range(rect.yMin, rect.yMax));
                if (c.a > .8f) colors.Add(c * source.color);
            }
            return colors.Count > 0 ? colors.ToArray() : new[] { source.color };
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Lifetime);
            // 발 위치를 지키며 납작해진다
            float squash = Mathf.Lerp(1f, .15f, t * t);
            transform.localScale = new Vector3(baseScale.x * Mathf.Lerp(1f, 1.35f, t), baseScale.y * squash, baseScale.z);
            transform.position = basePosition + Vector3.down * halfHeight * (1f - squash);
            body.color = new Color(bodyColor.r, bodyColor.g, bodyColor.b, 1f - t);
            flash.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(age / FlashTime));
            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
