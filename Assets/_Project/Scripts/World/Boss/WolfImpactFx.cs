using UnityEngine;

namespace PersistentWorld.World
{
    // 늑대 이동기의 속도감·타격감 연출 (보이기만 한다, 판정 없음).
    // 속도선, 바닥 긁힘 자국, 바닥 균열, 크게 맞았을 때 화면 가장자리 붉은 테두리. (소리는 나중에 따로 넣는다)
    public static class WolfImpactFx
    {
        private static Sprite streak, scrape, crack;

        // 속도선: 진행 방향으로 길게 뻗은 빛줄기가 뒤로 흩날리며 사라진다
        public static void SpeedLine(Vector2 at, Vector2 direction, Color color)
        {
            if (streak == null) streak = MakeStreak();
            var obj = new GameObject("Wolf Speed Line");
            obj.transform.position = at;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            obj.transform.localScale = new Vector3(Random.Range(1.2f, 2.4f), Random.Range(.6f, 1f), 1f);
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = streak; sr.sortingOrder = 2600; sr.color = color;
            obj.AddComponent<Fader>().Begin(Random.Range(.1f, .18f), 1f, -direction * 3f);
        }

        // 지나간 자리에 남는 발톱 긁힘 자국 (몇 초 뒤 사라진다).
        // 짐승이 마구 할퀴고 간 느낌이 나도록 자리·각도·크기·좌우를 매번 크게 흩뜨리고, 가끔 겹쳐 긁힌 자국을 하나 더 남긴다.
        public static void Scrape(Vector2 at, Vector2 direction)
        {
            if (scrape == null) scrape = Load("Boss/Wolf/WolfScrape", 32f);
            if (scrape == null) return;
            Vector2 forward = direction.sqrMagnitude > .001f ? direction.normalized : Vector2.left;
            Vector2 side = new Vector2(-forward.y, forward.x);
            float heading = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            int marks = Random.value < .35f ? 2 : 1;
            for (int i = 0; i < marks; i++)
            {
                var obj = new GameObject("Wolf Scrape");
                obj.transform.position = at + side * Random.Range(-.5f, .5f) + forward * Random.Range(-.35f, .35f);
                obj.transform.rotation = Quaternion.Euler(0f, 0f, heading - 60f + Random.Range(-55f, 55f));
                float size = i == 0 ? 1f : .7f;
                obj.transform.localScale = new Vector3(Random.Range(.55f, 1.3f), Random.Range(.6f, 1.15f), 1f) * size;
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = scrape; sr.sortingOrder = -86;
                sr.flipX = Random.value < .5f; sr.flipY = Random.value < .3f;
                sr.color = new Color(1f, 1f, 1f, Random.Range(.5f, .85f));
                obj.AddComponent<Fader>().Begin(Random.Range(2.4f, 3.4f), 1f, Vector2.zero, .7f);
            }
        }

        // 내려찍은 자리의 바닥 균열
        public static void Crack(Vector2 at, float size)
        {
            if (crack == null) crack = Load("Boss/DoomCrack", 32f);
            if (crack == null) return;
            var obj = new GameObject("Wolf Slam Crack");
            obj.transform.position = at;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            obj.transform.localScale = Vector3.one * size * .4f; // 균열 그림이 커서 줄인다 (그대로면 화면을 가로지르는 뿌리처럼 보인다)
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = crack; sr.sortingOrder = -84; sr.color = new Color(.25f, .3f, .2f, .9f);
            obj.AddComponent<Fader>().Begin(1.8f, 1f, Vector2.zero, .6f);
        }

        private static Sprite Load(string path, float ppu)
        {
            Texture2D tex = Resources.Load<Texture2D>(path);
            return tex == null ? null : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), ppu);
        }

        private static Sprite MakeStreak()
        {
            const int w = 64, h = 6;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float along = x / (float)(w - 1);                  // 앞(오른쪽)이 밝고 꼬리로 갈수록 옅다
                float across = 1f - Mathf.Abs((y + .5f) / h * 2f - 1f);
                tex.SetPixel(x, y, new Color(1, 1, 1, along * along * across));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(1f, .5f), 32f);
        }

        // 서서히 옅어지며(필요하면 움직이며) 사라진다
        private sealed class Fader : MonoBehaviour
        {
            private SpriteRenderer sr; private float life, age, hold; private Color start; private Vector3 drift;
            public void Begin(float seconds, float alpha, Vector2 velocity, float holdRatio = 0f)
            { sr = GetComponent<SpriteRenderer>(); start = sr.color; start.a *= alpha; life = seconds; drift = velocity; hold = holdRatio; }
            private void Update()
            {
                age += Time.deltaTime;
                if (age >= life) { Destroy(gameObject); return; }
                transform.position += drift * Time.deltaTime;
                float t = age / life, k = t < hold ? 1f : 1f - (t - hold) / Mathf.Max(.01f, 1f - hold);
                sr.color = new Color(start.r, start.g, start.b, start.a * k);
            }
        }
    }

    // 크게 맞았을 때 화면 가장자리가 잠깐 붉어진다 (혼자 하기 화면 연출)
    public sealed class HurtVignette : MonoBehaviour
    {
        private static HurtVignette instance;
        private static Texture2D ring;
        private float strength;

        public static void Flash(float amount)
        {
            if (Application.isBatchMode) return;
            if (instance == null)
            {
                var obj = new GameObject("Hurt Vignette");
                instance = obj.AddComponent<HurtVignette>();
            }
            instance.strength = Mathf.Max(instance.strength, Mathf.Clamp01(amount));
        }

        private void Update() => strength = Mathf.MoveTowards(strength, 0f, Time.deltaTime * 1.6f);

        private void OnGUI()
        {
            if (strength <= .01f || Event.current.type != EventType.Repaint) return;
            if (ring == null) ring = MakeRing();
            GUI.depth = -30;
            Color previous = GUI.color;
            GUI.color = new Color(.85f, .05f, .05f, strength);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), ring, ScaleMode.StretchToFill);
            GUI.color = previous;
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        private static Texture2D MakeRing()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;           // 가운데 0, 모서리 1
                float a = Mathf.Clamp01((d - .45f) / .55f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
            tex.Apply();
            return tex;
        }
    }
}
