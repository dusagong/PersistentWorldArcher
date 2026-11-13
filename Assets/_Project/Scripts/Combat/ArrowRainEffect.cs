using System.Collections.Generic;
using PersistentWorld.Core;
using UnityEngine;

namespace PersistentWorld.Combat
{
    public sealed class ArrowRainEffect : MonoBehaviour
    {
        public const int FrameCount = 9;
        public const float FrameDuration = .08f;
        public const float RadiusX = 2f, RadiusY = .8f, Duration = 3f, Range = 8f;
        private static Texture2D cachedTexture;
        private static Sprite[] frames;
        private SpriteRenderer spriteRenderer;
        private float startedAt;
        private int damage;
        private DamageContext damageContext;
        private float nextImpact = FrameDuration * 3;

        public static ArrowRainEffect Spawn(Vector2 position, int damage, GameObject source = null)
        {
            if (!LoadFrames()) return null;
            GameObject obj = new GameObject("Skill 2 - Arrow Rain");
            obj.transform.position = position;
            obj.transform.localScale = new Vector3(RadiusX / 1.3f, RadiusY / .45f, 1);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sortingOrder = 1100 - Mathf.RoundToInt(position.y * 100f);
            ArrowRainEffect effect = obj.AddComponent<ArrowRainEffect>();
            effect.spriteRenderer = renderer;
            effect.startedAt = Time.time;
            effect.damage = damage;
            effect.damageContext = DamageContext.From(Faction.Player, source);
            return effect;
        }

        private void Update()
        {
            float elapsed = Time.time - startedAt;
            int frame = Mathf.FloorToInt(elapsed / FrameDuration) % FrameCount;
            if (elapsed >= nextImpact && elapsed < Duration)
            {
                nextImpact += .5f;
                ApplyImpact();
            }
            if (elapsed >= Duration)
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
                health.TryDamage(Mathf.Max(1, Mathf.RoundToInt(damage / 3f)), damageContext);
                if (health.GetComponent<PersistentWorld.World.BossController>() == null && !health.IsDead)
                    (health.GetComponent<CombatStatus>() ?? health.gameObject.AddComponent<CombatStatus>()).ApplySlow(.65f);
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
