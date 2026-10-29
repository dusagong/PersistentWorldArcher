using UnityEngine;

namespace PersistentWorld.Combat
{
    // Temporary geometry; replace through class presentation assets when art arrives.
    public sealed class SkillIndicator : MonoBehaviour
    {
        private static Material material;
        private LineRenderer line;
        private float expiresAt;
        public static SkillIndicator Create(Color color, float lifetime = 0)
        {
            if (material == null) material = new Material(Shader.Find("Sprites/Default"));
            GameObject obj = new GameObject("Skill Range");
            SkillIndicator indicator = obj.AddComponent<SkillIndicator>();
            indicator.line = obj.AddComponent<LineRenderer>();
            indicator.line.sharedMaterial = material;
            indicator.line.startColor = indicator.line.endColor = color;
            indicator.line.startWidth = indicator.line.endWidth = .045f;
            indicator.line.sortingOrder = 2000;
            indicator.expiresAt = lifetime > 0 ? Time.time + lifetime : float.PositiveInfinity;
            return indicator;
        }

        public void Circle(Vector2 center, float radiusX, float radiusY)
        {
            line.loop = true;
            line.positionCount = 64;
            for (int i = 0; i < 64; i++)
            {
                float a = i * Mathf.PI * 2 / 64;
                line.SetPosition(i, center + new Vector2(Mathf.Cos(a) * radiusX, Mathf.Sin(a) * radiusY));
            }
        }

        public void Sector(Vector2 center, Vector2 direction, float radius, float angle)
        {
            line.loop = true;
            line.positionCount = 34;
            line.SetPosition(0, center);
            for (int i = 0; i <= 32; i++)
                line.SetPosition(i + 1, center + (Vector2)(Quaternion.Euler(0, 0, -angle / 2 + angle * i / 32) * direction) * radius);
        }

        public void Beam(Vector2 start, Vector2 end, float width)
        {
            line.loop = false;
            line.positionCount = 2;
            line.startWidth = line.endWidth = width;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private void Update() { if (Time.time >= expiresAt) Destroy(gameObject); }
    }
}
