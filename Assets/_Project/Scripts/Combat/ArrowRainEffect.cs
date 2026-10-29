using System.Collections.Generic;
using PersistentWorld.Core;
using UnityEngine;

namespace PersistentWorld.Combat
{
    public sealed class ArrowRainEffect : MonoBehaviour
    {
        public const int FrameCount = 9;
        public const float FrameDuration = .08f;
        private const float RadiusX = 1.3f;
        private const float RadiusY = .45f;
        private static Texture2D cachedTexture;
        private static Sprite[] frames;
        private SpriteRenderer spriteRenderer;
        private float startedAt;
        private int damage;
        private bool impacted;

        public static ArrowRainEffect Spawn(Vector2 position, int damage)
        {
            if (!LoadFrames()) return null;
            GameObject obj = new GameObject("Skill 2 - Arrow Rain");
            obj.transform.position = position;
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sortingOrder = 1100 - Mathf.RoundToInt(position.y * 100f);
            ArrowRainEffect effect = obj.AddComponent<ArrowRainEffect>();
            effect.spriteRenderer = renderer;
            effect.startedAt = Time.time;
            effect.damage = damage;
            return effect;
        }

        private void Update()
        {
            int frame = Mathf.FloorToInt((Time.time - startedAt) / FrameDuration);
            // Frame 3 is the first ground impact. Damage is applied once per target.
            if (frame >= 3 && !impacted)
            {
                impacted = true;
                ApplyImpact();
            }
            if (frame >= FrameCount)
            {
                Destroy(gameObject);
                return;
            }
            spriteRenderer.sprite = frames[frame];
        }

        private void ApplyImpact()
        {
            Vector2 center = transform.position;
            HashSet<Health> damaged = new HashSet<Health>();
            foreach (Collider2D collider in Physics2D.OverlapBoxAll(center, new Vector2(RadiusX * 2f, RadiusY * 2f), 0f))
            {
                Health health = collider.GetComponentInParent<Health>();
                if (health == null || health.Faction != Faction.Enemy || damaged.Contains(health)) continue;
                Vector2 offset = collider.ClosestPoint(center) - center;
                if (offset.x * offset.x / (RadiusX * RadiusX) + offset.y * offset.y / (RadiusY * RadiusY) > 1f) continue;
                damaged.Add(health);
                health.TryDamage(damage, Faction.Player);
            }
        }

        private static bool LoadFrames()
        {
            Texture2D texture = Resources.Load<Texture2D>("Effects/SkillTwoArrowRain");
            if (texture == null || texture.width != 1026 || texture.height != 1026)
            {
                Debug.LogError("SkillTwoArrowRain requires the original transparent 1026x1026 sheet.");
                return false;
            }
            if (texture == cachedTexture && frames != null) return true;
            frames = new Sprite[FrameCount];
            const int cell = 342;
            for (int i = 0; i < FrameCount; i++)
            {
                Rect rect = new Rect(i % 3 * cell, (2 - i / 3) * cell, cell, cell);
                // Anchor the ring center to the target, rather than the falling arrows.
                frames[i] = Sprite.Create(texture, rect, new Vector2(.5f, 117f / cell), 80f, 0, SpriteMeshType.FullRect);
                frames[i].name = $"SkillTwoArrowRain_{i:00}";
            }
            cachedTexture = texture;
            return true;
        }
    }
}
