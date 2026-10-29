using UnityEngine;

namespace PersistentWorld.Combat
{
    // Visual only: the existing two projectiles remain responsible for damage.
    public sealed class SkillOneEffect : MonoBehaviour
    {
        public const float FrameDuration = .08f;
        public const int FrameCount = 6;
        private static Texture2D cachedTexture;
        private static Sprite[] frames;
        private SpriteRenderer spriteRenderer;
        private float startedAt;

        public static SkillOneEffect Spawn(Vector2 origin, Vector2 direction)
        {
            if (!LoadFrames()) return null;
            GameObject obj = new GameObject("Skill 1 - Arrow Volley");
            obj.transform.position = origin;
            // The supplied art points left; rotate that axis toward the aim direction.
            obj.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sortingOrder = 1100 - Mathf.RoundToInt(origin.y * 100f);
            SkillOneEffect effect = obj.AddComponent<SkillOneEffect>();
            effect.spriteRenderer = renderer;
            effect.startedAt = Time.time;
            return effect;
        }

        private void Update()
        {
            int frame = Mathf.FloorToInt((Time.time - startedAt) / FrameDuration);
            if (frame >= FrameCount)
            {
                Destroy(gameObject);
                return;
            }
            spriteRenderer.sprite = frames[frame];
        }

        private static bool LoadFrames()
        {
            Texture2D texture = Resources.Load<Texture2D>("Effects/SkillOneVolley");
            if (texture == null || texture.width != 1026 || texture.height != 684)
            {
                Debug.LogError("SkillOneVolley requires the original transparent 1026x684 sheet.");
                return false;
            }
            if (texture == cachedTexture && frames != null) return true;
            frames = new Sprite[FrameCount];
            int width = texture.width / 3;
            int height = texture.height / 2;
            for (int i = 0; i < FrameCount; i++)
            {
                Rect rect = new Rect(i % 3 * width, (1 - i / 3) * height, width, height);
                frames[i] = Sprite.Create(texture, rect, new Vector2(310f / 342f, .5f), 100f, 0, SpriteMeshType.FullRect);
                frames[i].name = $"SkillOneVolley_{i:00}";
            }
            cachedTexture = texture;
            return true;
        }
    }
}
