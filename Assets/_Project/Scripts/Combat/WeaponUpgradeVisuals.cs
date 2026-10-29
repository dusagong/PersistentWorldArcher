using UnityEngine;

namespace PersistentWorld.Combat
{
    public static class WeaponUpgradeVisuals
    {
        public static Color ColorFor(int level) => level == 1 ? new Color(0.85f, 0.94f, 1f)
            : level == 2 ? new Color(0.2f, 0.75f, 1f) : new Color(1f, 0.7f, 0.15f);

        // Shared by projectile-based classes. Melee attacks can use ColorFor for their own effect.
        public static void ApplyProjectile(GameObject projectile, int level)
        {
            if (level < 1 || level > 3) return;
            var renderer = projectile.GetComponent<SpriteRenderer>();
            if (renderer == null) return;
            Color color = ColorFor(level);
            renderer.color = color;
            var trail = projectile.AddComponent<TrailRenderer>();
            trail.sharedMaterial = renderer.sharedMaterial;
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0);
            trail.startWidth = 0.035f + level * 0.025f;
            trail.endWidth = 0;
            trail.time = 0.07f + level * 0.06f;
            trail.minVertexDistance = 0.03f;
            trail.sortingLayerID = renderer.sortingLayerID;
            trail.sortingOrder = renderer.sortingOrder + 1;
        }
    }
}
