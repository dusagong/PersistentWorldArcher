using System.Collections.Generic;
using UnityEngine;

namespace PersistentWorld.World
{
    // 내려찍기 같은 강한 충격: 흙덩이·돌 조각이 포물선으로 튀어 오르고, 흙먼지가 퍼지고, 바닥에 패인 자국이 남았다 사라진다.
    // 연출 전용이라 판정은 없다.
    public sealed class GroundBurst : MonoBehaviour
    {
        private sealed class Chunk
        {
            public Transform Body;
            public SpriteRenderer Renderer;
            public Vector2 Ground, Velocity;
            public float Height, Rise, Spin, LandedAt = -1f;
        }

        private sealed class Puff
        {
            public Transform Body;
            public SpriteRenderer Renderer;
            public Vector2 Drift;
            public float Delay, Size;
        }

        private const float Gravity = 26f;
        private const float PuffLife = .8f, CraterLife = 1.6f, ChunkFade = .5f;
        private static readonly Color[] Dirt = { new Color(.45f, .32f, .19f), new Color(.36f, .25f, .15f), new Color(.56f, .43f, .27f), new Color(.3f, .22f, .14f) };
        private static Sprite circle, square;

        private readonly List<Chunk> chunks = new List<Chunk>();
        private readonly List<Puff> puffs = new List<Puff>();
        private SpriteRenderer crater;
        private float startedAt;

        // debris가 있으면 조각 일부를 그 그림(흙빛으로 물들인 돌)으로 쓴다
        public static void Spawn(Vector2 center, float radius, Sprite[] debris = null, int chunkCount = 56, int puffCount = 18) =>
            Build(center, radius, debris, chunkCount, puffCount, Dirt, true, 1f, 1f);

        // 몸이 부서지는 연출: 흙 대신 palette 색 조각이 튀고 바닥 자국은 남기지 않는다. 몸집(radius)에 맞춰 조각 수·힘을 정한다.
        public static void Shatter(Vector2 center, float radius, Color[] palette)
        {
            int chunks = Mathf.Clamp(Mathf.RoundToInt(radius * 44f), 18, 90);
            int puffs = Mathf.Clamp(Mathf.RoundToInt(radius * 12f), 6, 22);
            Build(center, radius, null, chunks, puffs, palette, false, Mathf.Clamp(radius / 1.6f, .5f, 1.1f), Mathf.Clamp(radius, .8f, 1.7f));
        }

        private static void Build(Vector2 center, float radius, Sprite[] debris, int chunkCount, int puffCount, Color[] colors, bool withCrater, float power, float chunkScale)
        {
            var root = new GameObject("Ground Burst").AddComponent<GroundBurst>();
            root.transform.position = center;
            root.startedAt = Time.time;

            if (withCrater)
            {
                root.crater = root.Create("Crater", Circle, center, 3000);
                root.crater.transform.localScale = new Vector3(radius * .9f, radius * .45f, 1f);
                root.crater.color = new Color(.22f, .15f, .09f, .55f);
                root.crater.sortingOrder = -8000;
            }

            for (int i = 0; i < chunkCount; i++)
            {
                // 가운데(주먹이 닿은 곳)에 몰리게 뽑는다
                Vector2 from = center + Random.insideUnitCircle * radius * Mathf.Lerp(.15f, .7f, Random.value * Random.value);
                Vector2 outward = from - center;
                outward = outward.sqrMagnitude > .001f ? outward.normalized : Random.insideUnitCircle.normalized;
                bool useDebris = debris != null && debris.Length > 0 && Random.value < .45f;
                SpriteRenderer r = root.Create("Chunk", useDebris ? debris[Random.Range(0, debris.Length)] : Square, from, 0);
                float size = (useDebris ? Random.Range(.55f, 1f) : Random.Range(.09f, .22f)) * chunkScale;
                r.transform.localScale = Vector3.one * size;
                r.color = useDebris ? Color.Lerp(Color.white, Dirt[0], .45f) : colors[Random.Range(0, colors.Length)];
                root.chunks.Add(new Chunk
                {
                    Body = r.transform, Renderer = r, Ground = from,
                    Velocity = (outward * Random.Range(2f, 6.5f) + Random.insideUnitCircle * .8f) * power,
                    Rise = Random.Range(5f, 11f) * power, Spin = Random.Range(-720f, 720f)
                });
            }

            for (int i = 0; i < puffCount; i++)
            {
                float angle = (360f / puffCount * i + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * .55f);
                Vector2 at = center + dir * radius * Random.Range(.35f, .95f);
                SpriteRenderer r = root.Create("Dust", Circle, at, 0);
                Color dust = Color.Lerp(colors[0], new Color(.62f, .52f, .4f), .6f);
                r.color = new Color(dust.r, dust.g, dust.b, 0f);
                root.puffs.Add(new Puff { Body = r.transform, Renderer = r, Drift = dir * Random.Range(.6f, 1.6f) + Vector2.up * .5f, Delay = Random.Range(0f, .12f), Size = Random.Range(.9f, 1.6f) * chunkScale });
            }
        }

        private SpriteRenderer Create(string name, Sprite sprite, Vector2 at, int order)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            obj.transform.position = at;
            var r = obj.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        private void Update()
        {
            float age = Time.time - startedAt;
            float dt = Time.deltaTime;
            bool alive = crater != null && age < CraterLife;

            if (crater != null) crater.color = new Color(crater.color.r, crater.color.g, crater.color.b, .55f * (1f - Mathf.Clamp01(age / CraterLife)));

            foreach (Chunk c in chunks)
            {
                if (c.LandedAt < 0f)
                {
                    c.Ground += c.Velocity * dt;
                    c.Rise -= Gravity * dt;
                    c.Height += c.Rise * dt;
                    c.Body.Rotate(0f, 0f, c.Spin * dt);
                    if (c.Height <= 0f) { c.Height = 0f; c.LandedAt = Time.time; }
                }
                else
                {
                    float fade = (Time.time - c.LandedAt) / ChunkFade;
                    Color color = c.Renderer.color;
                    c.Renderer.color = new Color(color.r, color.g, color.b, 1f - Mathf.Clamp01(fade));
                }
                c.Body.position = c.Ground + Vector2.up * c.Height;
                // 그림자 쪽(땅 위치) 기준으로 앞뒤를 정한다
                c.Renderer.sortingOrder = 1000 - Mathf.RoundToInt(c.Ground.y * 100f) + 2;
                if (c.LandedAt < 0f || Time.time - c.LandedAt < ChunkFade) alive = true;
            }

            foreach (Puff p in puffs)
            {
                float t = (age - p.Delay) / PuffLife;
                if (t < 0f) continue;
                if (t < 1f) alive = true;
                t = Mathf.Clamp01(t);
                p.Body.position += (Vector3)(p.Drift * dt * (1f - t));
                p.Body.localScale = Vector3.one * p.Size * Mathf.Lerp(.35f, 1.4f, Mathf.Sqrt(t));
                p.Renderer.color = new Color(p.Renderer.color.r, p.Renderer.color.g, p.Renderer.color.b, .75f * (1f - t));
                p.Renderer.sortingOrder = 1000 - Mathf.RoundToInt(p.Body.position.y * 100f) + 50;
            }

            if (!alive) Destroy(gameObject);
        }

        private static Sprite Square
        {
            get
            {
                if (square != null) return square;
                var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels);
                texture.Apply();
                square = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4f);
                return square;
            }
        }

        // 가장자리가 부드러운 원 (지름 1유닛)
        internal const string CircleSpriteName = "Ground Burst Soft Circle";
        internal static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                float r = size * .5f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r)) / r;
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 2.5f)));
                    }
                texture.Apply();
                circle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
                circle.name = CircleSpriteName;
                return circle;
            }
        }
    }
}
