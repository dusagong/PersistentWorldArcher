using UnityEngine;

namespace PersistentWorld.World
{
    // 위로 떠오르며 사라지는 데미지 숫자.
    public sealed class DamageNumber : MonoBehaviour
    {
        private const float Lifetime = .7f;
        private const float RiseDistance = .8f;
        private static Font font;

        private TextMesh text;
        private TextMesh shadow;
        private Color color;
        private Vector3 start;
        private Vector3 drift;
        private float age;

        public static DamageNumber Spawn(Vector3 position, string value, Color color)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject obj = new GameObject("Damage Number");
            obj.transform.position = position;
            TextMesh mesh = obj.AddComponent<TextMesh>();
            mesh.text = value;
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = .065f;
            mesh.fontStyle = FontStyle.Bold;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 5000;
            DamageNumber number = obj.AddComponent<DamageNumber>();
            number.text = mesh;
            number.shadow = CreateShadow(obj.transform, mesh, renderer.sortingOrder - 1);
            number.color = color;
            number.start = position;
            number.drift = new Vector3(Random.Range(-.25f, .25f), 0f, 0f);
            return number;
        }

        private static TextMesh CreateShadow(Transform parent, TextMesh source, int sortingOrder)
        {
            GameObject obj = new GameObject("Shadow");
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = new Vector3(.04f, -.04f, 0f);
            TextMesh mesh = obj.AddComponent<TextMesh>();
            mesh.text = source.text;
            mesh.font = source.font;
            mesh.fontSize = source.fontSize;
            mesh.characterSize = source.characterSize;
            mesh.fontStyle = source.fontStyle;
            mesh.anchor = source.anchor;
            mesh.alignment = source.alignment;
            mesh.color = new Color(0f, 0f, 0f, .8f);
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = source.font.material;
            renderer.sortingOrder = sortingOrder;
            return mesh;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Lifetime);
            float rise = 1f - (1f - t) * (1f - t);
            transform.position = start + Vector3.up * (RiseDistance * rise) + drift * t;
            float pop = t < .15f ? Mathf.Lerp(1.4f, 1f, t / .15f) : 1f;
            transform.localScale = Vector3.one * pop;
            float alpha = 1f - Mathf.Clamp01((t - .5f) / .5f);
            text.color = new Color(color.r, color.g, color.b, alpha);
            shadow.color = new Color(0f, 0f, 0f, alpha * .8f);
            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
