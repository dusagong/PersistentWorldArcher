using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using PersistentWorld.Networking;

namespace PersistentWorld.World
{
    // 보스방 분위기: 바닥의 빛 웅덩이가 일렁이고, 빛 입자와 안개가 화면 주변을 떠다닌다. 2페이즈가 되면 빛이 붉게 변한다.
    // 연출 전용이라 판정이 없고, 배경 색은 씬의 오브젝트에 칠해 두어 캐릭터와 경고 표시 색은 그대로 남는다.
    public sealed class ArenaAtmosphere : MonoBehaviour
    {
        [System.Serializable]
        private struct LightPool
        {
            public Vector2 position;
            public float radius;
            public Color color;
        }

        private sealed class Mote
        {
            public Transform Body;
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Age, Life, Size, Sway;
            public Color Color;
        }

        private sealed class Pool
        {
            public SpriteRenderer Renderer;
            public Color Color;
            public float Phase;
        }

        private sealed class Fog
        {
            public Transform Body;
            public Vector2 Velocity;
        }

        private const int GroundOrder = -7800;
        private const int AirOrder = 3000;

        [SerializeField] private LightPool[] lights;
        [SerializeField] private int moteCount = 46;
        [SerializeField] private int fogCount = 7;
        [SerializeField, FormerlySerializedAs("emberColor")] private Color moteColor = new Color(1f, .62f, .25f);
        [SerializeField, FormerlySerializedAs("spiritColor")] private Color moteAccentColor = new Color(.35f, .9f, 1f);
        [SerializeField, Tooltip("입자가 오르내리는 속도. 음수면 아래로 내려온다")] private float moteRise = .5f;
        [SerializeField] private Color phaseTwoColor = new Color(1f, .16f, .12f);
        [SerializeField, Range(0f, 1f)] private float fogAlpha = .07f;

        private static Sprite soft;
        private readonly List<Pool> pools = new List<Pool>();
        private readonly List<Mote> motes = new List<Mote>();
        private readonly List<Fog> fogs = new List<Fog>();
        private BossController boss;
        private float rage;

        private void Start()
        {
            if (NetworkGameWorld.Instance != null && !NetworkGameWorld.Instance.gameCamera.enabled)
            { enabled = false; return; }
            boss = FindFirstObjectByType<BossController>();
            if (lights != null)
                foreach (LightPool light in lights)
                {
                    Vector2 offset = NetworkGameWorld.Instance == null ? Vector2.zero : NetworkGameWorld.Origin(GameArea.BossArena);
                    SpriteRenderer r = Create("Light Pool", light.position + offset, GroundOrder);
                    r.transform.localScale = new Vector3(light.radius * 2f, light.radius * 1.5f, 1f);
                    pools.Add(new Pool { Renderer = r, Color = light.color, Phase = Random.value * 6f });
                }
            for (int i = 0; i < moteCount; i++)
            {
                var mote = new Mote { Renderer = Create("Mote", Vector2.zero, AirOrder) };
                mote.Body = mote.Renderer.transform;
                Respawn(mote);
                mote.Age = Random.value * mote.Life;
                motes.Add(mote);
            }
            for (int i = 0; i < fogCount; i++)
            {
                SpriteRenderer r = Create("Fog", ViewPoint(1.2f), AirOrder - 1);
                r.transform.localScale = new Vector3(Random.Range(9f, 15f), Random.Range(3f, 5f), 1f);
                r.color = new Color(.62f, .7f, .9f, fogAlpha);
                fogs.Add(new Fog { Body = r.transform, Velocity = new Vector2(Random.Range(.12f, .35f), Random.Range(-.03f, .03f)) });
            }
        }

        private SpriteRenderer Create(string name, Vector2 at, int order)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            obj.transform.position = at;
            var r = obj.AddComponent<SpriteRenderer>();
            r.sprite = Soft;
            r.sortingOrder = order;
            return r;
        }

        private void Update()
        {
            bool phaseTwo = NetworkGameWorld.Instance != null
                ? NetworkGameWorld.Instance.Frame?.bossPhaseTwo == true
                : boss != null && boss.IsPhaseTwo;
            float target = phaseTwo ? 1f : 0f;
            rage = Mathf.MoveTowards(rage, target, Time.deltaTime * .6f);

            foreach (Pool pool in pools)
            {
                // 두 파형을 섞어 불빛처럼 불규칙하게 일렁인다
                float flicker = .82f + .12f * Mathf.Sin(Time.time * 2.3f + pool.Phase) + .06f * Mathf.Sin(Time.time * 7.1f + pool.Phase * 2f);
                Color c = Color.Lerp(pool.Color, new Color(phaseTwoColor.r, phaseTwoColor.g, phaseTwoColor.b, pool.Color.a), rage);
                c.a *= flicker;
                pool.Renderer.color = c;
            }

            foreach (Mote mote in motes)
            {
                mote.Age += Time.deltaTime;
                if (mote.Age >= mote.Life) Respawn(mote);
                float t = mote.Age / mote.Life;
                mote.Body.position += (Vector3)(mote.Velocity + Vector2.right * Mathf.Sin(Time.time * 1.7f + mote.Sway) * .25f) * Time.deltaTime;
                mote.Body.localScale = Vector3.one * mote.Size;
                Color c = Color.Lerp(mote.Color, phaseTwoColor, rage);
                c.a = Mathf.Sin(t * Mathf.PI) * .9f; // 서서히 나타났다 사라진다
                mote.Renderer.color = c;
            }

            Camera cam = Camera.main;
            if (cam == null) return;
            float halfWidth = cam.orthographicSize * cam.aspect + 9f;
            foreach (Fog fog in fogs)
            {
                fog.Body.position += (Vector3)fog.Velocity * Time.deltaTime;
                // 화면을 벗어나면 반대쪽에서 다시 들어온다
                if (fog.Body.position.x > cam.transform.position.x + halfWidth)
                    fog.Body.position = new Vector3(cam.transform.position.x - halfWidth, ViewPoint(1f).y, 0f);
                else if (fog.Body.position.x < cam.transform.position.x - halfWidth)
                    fog.Body.position = new Vector3(cam.transform.position.x + halfWidth, ViewPoint(1f).y, 0f);
            }
        }

        private void Respawn(Mote mote)
        {
            bool main = Random.value < .65f;
            mote.Color = main ? moteColor : moteAccentColor;
            mote.Age = 0f;
            mote.Life = Random.Range(2.5f, 6f);
            mote.Size = Random.Range(.05f, main ? .12f : .2f);
            mote.Sway = Random.value * 6f;
            mote.Velocity = new Vector2(Random.Range(-.15f, .15f), moteRise * Random.Range(.5f, 1.6f));
            mote.Body.position = ViewPoint(1.1f);
        }

        // 카메라가 보는 범위 안의 임의 지점 (scale로 범위를 넓힌다)
        private static Vector2 ViewPoint(float scale)
        {
            Camera cam = Camera.main;
            if (cam == null) return Vector2.zero;
            float h = cam.orthographicSize * scale, w = h * cam.aspect;
            return (Vector2)cam.transform.position + new Vector2(Random.Range(-w, w), Random.Range(-h, h));
        }

        private static Sprite Soft
        {
            get
            {
                if (soft != null) return soft;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                float r = size * .5f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r)) / r);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, (1f - d) * (1f - d)));
                    }
                texture.Apply();
                soft = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
                return soft;
            }
        }
    }
}
