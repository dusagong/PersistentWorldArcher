using UnityEngine;

namespace PersistentWorld.Combat
{
    public sealed class PowerShotEffect : MonoBehaviour
    {
        public const int FrameCount = 9;
        public const float FrameDuration = .08f;
        public const float ReleaseDelay = 7f * FrameDuration;
        private static Sprite[] castFrames;
        private static Sprite[] flightFrames;
        private static Texture2D castTexture;
        private static Texture2D flightTexture;
        private SpriteRenderer spriteRenderer;
        private float startedAt;
        private Transform caster;
        private Vector2 offset;
        public bool IsFlight { get; private set; }

        public static PowerShotEffect PlayCast(Transform owner, Vector2 direction)
        {
            if (!LoadFrames()) return null;
            GameObject obj = new GameObject("Skill 3 - Bow Release");
            Vector2 offset = Vector2.up * .35f + direction * .35f;
            obj.transform.position = (Vector2)owner.position + offset;
            // The source bow launches diagonally down-left, unlike the left-facing flight art.
            obj.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 135f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = castFrames[0];
            PowerShotEffect effect = obj.AddComponent<PowerShotEffect>();
            effect.spriteRenderer = renderer;
            effect.startedAt = Time.time;
            effect.caster = owner;
            effect.offset = offset;
            return effect;
        }

        public static bool AttachFlight(Projectile projectile)
        {
            if (!LoadFrames()) return false;
            projectile.name = "Skill 3 - Power Arrow";
            // Undo the small placeholder rectangle's non-uniform scale.
            projectile.transform.localScale = Vector3.one;
            SpriteRenderer renderer = projectile.GetComponent<SpriteRenderer>();
            renderer.color = Color.white;
            renderer.sprite = flightFrames[0];
            renderer.flipX = true;
            PowerShotEffect effect = projectile.gameObject.AddComponent<PowerShotEffect>();
            effect.spriteRenderer = renderer;
            effect.startedAt = Time.time;
            effect.IsFlight = true;
            return true;
        }

        private void Update()
        {
            int frame = Mathf.FloorToInt((Time.time - startedAt) / FrameDuration);
            if (!IsFlight)
            {
                if (caster == null || caster.GetComponent<Health>().IsDead || frame >= FrameCount)
                {
                    Destroy(gameObject);
                    return;
                }
                transform.position = (Vector2)caster.position + offset;
            }
            spriteRenderer.sprite = IsFlight ? flightFrames[frame % FrameCount] : castFrames[frame];
            spriteRenderer.sortingOrder = 1100 - Mathf.RoundToInt(transform.position.y * 100f);
        }

        private static bool LoadFrames()
        {
            Texture2D cast = Resources.Load<Texture2D>("Effects/SkillThreeCast");
            Texture2D flight = Resources.Load<Texture2D>("Effects/SkillThreeFlight");
            if (cast == null || flight == null || cast.width != 1254 || cast.height != 1254 || flight.width != 1026 || flight.height != 1026)
            {
                Debug.LogError("Skill 3 requires the original 1254x1254 cast and 1026x1026 flight sheets.");
                return false;
            }
            if (castTexture != cast || castFrames == null)
            {
                castFrames = Slice(cast, 418, new Vector2(158f / 414f, 156f / 414f), 210f, "SkillThreeCast");
                castTexture = cast;
            }
            if (flightTexture != flight || flightFrames == null)
            {
                flightFrames = Slice(flight, 342, new Vector2(13f / 338f, .5f), 150f, "SkillThreeFlight");
                flightTexture = flight;
            }
            return true;
        }

        private static Sprite[] Slice(Texture2D texture, int cell, Vector2 pivot, float pixelsPerUnit, string prefix)
        {
            Sprite[] result = new Sprite[FrameCount];
            for (int i = 0; i < FrameCount; i++)
            {
                // Two-pixel inset excludes the opaque green sheet guides without changing the source file.
                Rect rect = new Rect(i % 3 * cell + 2, (2 - i / 3) * cell + 2, cell - 4, cell - 4);
                result[i] = Sprite.Create(texture, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                result[i].name = $"{prefix}_{i:00}";
            }
            return result;
        }
    }
}
