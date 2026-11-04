using PersistentWorld.Player;
using UnityEngine;

namespace PersistentWorld.Combat
{
    // A single original sprite rotates around its handle; damage remains in PlayerSkills.
    public sealed class SwordSwingEffect : MonoBehaviour
    {
        public const float Duration = .24f / 1.5f;
        public const float Arc = 120f;
        private static Sprite sword;
        private Transform owner;
        private ArcherController controller;
        private SpriteRenderer visual;
        private Vector2 direction;
        private float startedAt;
        public Transform Owner => owner;
        public float SwingAngle { get; private set; }

        public static SwordSwingEffect Play(ArcherController caster, Vector2 aim)
        {
            if (sword == null)
            {
                Sprite source = Resources.Load<Sprite>("Items/Equipment/06_warrior");
                if (source == null) return null;
                // Make a separate runtime sprite so the inventory icon retains its original pivot/scale.
                sword = Sprite.Create(source.texture, source.rect, new Vector2(16f / 512f, 64f / 512f), 460f, 0, SpriteMeshType.FullRect);
                sword.name = "Warrior Sword - Handle Pivot";
            }
            GameObject obj = new GameObject("Warrior Basic Sword Swing");
            obj.transform.localScale = Vector3.one * 1.5f;
            SwordSwingEffect effect = obj.AddComponent<SwordSwingEffect>();
            effect.owner = caster.transform;
            effect.controller = caster;
            effect.direction = aim.normalized;
            effect.startedAt = Time.time;
            effect.visual = obj.AddComponent<SpriteRenderer>();
            effect.visual.sprite = sword;
            effect.UpdatePose(0);
            return effect;
        }

        private void Update()
        {
            float progress = (Time.time - startedAt) / Duration;
            if (owner == null || controller == null || !controller.isActiveAndEnabled || controller.Health.IsDead || controller.Loadout.Class != CharacterClass.Warrior || progress >= 1)
            {
                Destroy(gameObject);
                return;
            }
            UpdatePose(progress);
        }

        private void UpdatePose(float progress)
        {
            SwingAngle = Mathf.Lerp(Arc / 2, -Arc / 2, Mathf.SmoothStep(0, 1, progress));
            transform.position = owner.position + Vector3.up * .35f + (Vector3)direction * .1f;
            // The source blade points northeast, 45 degrees from the positive X axis.
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + SwingAngle - 45);
            visual.sortingOrder = 1100 - Mathf.RoundToInt(owner.position.y * 100);
        }
    }
}
