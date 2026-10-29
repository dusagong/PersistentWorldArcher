using UnityEngine;

namespace PersistentWorld.Combat
{
    public sealed class BasicAttackEffect : MonoBehaviour
    {
        public const float FrameDuration = .06f;
        public const float ReleaseDelay = FrameDuration * 3;
        private static Sprite[] frames;
        private static Sprite arrow;
        private Transform owner;
        private Health health;
        private SpriteRenderer visual;
        private Vector2 direction;
        private float startedAt;
        public Transform Owner => owner;

        public static bool LoadArt()
        {
            if (frames != null && arrow != null) return true;
            Texture2D bowTexture = Resources.Load<Texture2D>("Effects/BasicAttackBow");
            Texture2D arrowTexture = Resources.Load<Texture2D>("Effects/BasicAttackArrow");
            if (bowTexture == null || arrowTexture == null) return false;
            frames = new Sprite[4];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = Sprite.Create(bowTexture, new Rect((i % 2) * 627, (1 - i / 2) * 627, 627, 627), new Vector2(.5f, .5f), 750f, 0, SpriteMeshType.FullRect);
                frames[i].name = "BasicBow_" + i;
            }
            arrow = Sprite.Create(arrowTexture, new Rect(0, 0, 1254, 1254), new Vector2(.87f, .85f), 1500f, 0, SpriteMeshType.FullRect);
            arrow.name = "BasicArrow";
            return true;
        }

        public static BasicAttackEffect PlayCast(Transform caster, Vector2 aim)
        {
            if (!LoadArt()) return null;
            GameObject obj = new GameObject("Basic Attack Bow");
            BasicAttackEffect effect = obj.AddComponent<BasicAttackEffect>();
            effect.owner = caster;
            effect.health = caster.GetComponent<Health>();
            effect.direction = aim;
            effect.startedAt = Time.time;
            effect.visual = obj.AddComponent<SpriteRenderer>();
            effect.visual.sprite = frames[0];
            obj.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg - 45f);
            effect.Update();
            return effect;
        }

        public static void AttachArrow(Projectile projectile)
        {
            if (!LoadArt()) return;
            projectile.name = "Basic Attack Arrow";
            projectile.GetComponent<SpriteRenderer>().enabled = false;
            projectile.transform.localScale = Vector3.one;
            GameObject obj = new GameObject("Arrow Visual");
            obj.transform.SetParent(projectile.transform, false);
            obj.transform.localRotation = Quaternion.Euler(0, 0, -45f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = arrow;
            renderer.sortingOrder = 1100;
        }

        private void Update()
        {
            int frame = Mathf.FloorToInt((Time.time - startedAt) / FrameDuration);
            if (owner == null || health == null || health.IsDead || !owner.gameObject.activeInHierarchy || frame >= 4)
            {
                Destroy(gameObject);
                return;
            }
            transform.position = owner.position + Vector3.up * .35f + (Vector3)direction * .3f;
            visual.sprite = frames[frame];
            visual.sortingOrder = 1100 - Mathf.RoundToInt(transform.position.y * 100f);
        }
    }
}
