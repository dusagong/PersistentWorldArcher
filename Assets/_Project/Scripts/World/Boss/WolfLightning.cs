using UnityEngine;

namespace PersistentWorld.World
{
    // 늑대의 초록 번개 (보이기만 하는 연출, 판정 없음).
    // 그림은 부드러운 고해상도(흰 심 + 연두 몸통 + 빛 번짐)이고, 모두 같은 5프레임 흐름이다:
    //   1 예비(시작점에 빛방울·짧은 스파크) → 2 임팩트(가장 굵은 메인 줄기, 큰 번개는 이때 화면을 한 번 번쩍)
    //   → 3 변형(완전히 다른 지그재그로 다시 그려 파지직) → 4 분열(끊어진 점선 잔류 전기) → 5 소멸(작은 입자 몇 개)
    // 밑에 연두빛 번짐을 더해 빛나 보이게 한다. 그림 한 장씩 넘기는 것이라 선을 매번 다시 그리는 것보다 가볍다.
    // 그림: Resources/Boss/Wolf/WolfBoltStrike(내리꽂기) · WolfBoltClaw(발톱 세 줄) · WolfBoltBurst(사방 폭발) · WolfSpark(짧은 스파크)
    public static class WolfLightning
    {
        public static readonly Color GlowColor = new Color(.35f, 1f, .12f, .38f);
        private static readonly Color RedGlow = new Color(1f, .22f, .1f, .38f);
        private static Color CurrentGlow => WolfRed.Tint(GlowColor, RedGlow);
        private const int Order = 2650; // 캐릭터 위

        private static Sheet strike, claw, burst, spark;
        private static Sprite glow;
        private static Material additive;

        // 그림 한 장의 정보: 칸 크기, 프레임 시간, 기준점(픽셀), 기준 길이(픽셀)
        private sealed class Sheet
        {
            public Sprite[] Frames;
            public float[] Seconds;
            public float Reach; // 이 그림에서 "길이 1"에 해당하는 픽셀 수 (크기 맞추기용)
            public float Total;
        }

        // a에서 b로 한 줄기: 짧은 스파크를 이어 붙인다
        public static void Bolt(Vector2 a, Vector2 b, float width = .12f, float life = .2f, float jag = .3f)
        {
            if (WolfFxNet.Send(WolfFxNet.Bolt, a, b, width, life, jag)) return;
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < .05f) return;
            int pieces = Mathf.Clamp(Mathf.CeilToInt(length / .9f), 1, 8);
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            for (int i = 0; i < pieces; i++)
            {
                Vector2 at = a + d * ((i + .5f) / pieces);
                Spawn(Spark(), at, angle + Random.Range(-12f, 12f), length / pieces * 1.15f, 1f, life, Random.value < .5f, 0f);
            }
        }

        // ---- 받은 이펙트 그림 (Resources/Boss/Wolf/WolfFx*.png): 그림은 오른쪽을 향해 그려져 있다 ----
        // 발톱 세 줄 할퀴기 (연속 할퀴기). length: 할퀸 자국 폭(유닛)
        public static void Claw(Vector2 center, Vector2 dir, float length = 2.2f, float spread = .45f, float life = .22f)
        {
            if (WolfFxNet.Send(WolfFxNet.Claw, center, dir, length, spread, life)) return;
            Facing(Fx("WolfFxClaw"), center, dir, length * 1.25f, life);
        }

        // 큰 발톱 네 줄 (큰 발톱 휩쓸기)
        public static void BigClaw(Vector2 center, Vector2 dir, float width)
        {
            if (WolfFxNet.Send(WolfFxNet.BigClaw, center, dir, width)) return;
            Facing(Fx("WolfFxBigClaw"), center, dir, width, .22f);
        }

        // 초승달 참격
        public static void Crescent(Vector2 center, Vector2 dir, float width)
        {
            if (WolfFxNet.Send(WolfFxNet.Crescent, center, dir, width)) return;
            Facing(Fx("WolfFxCrescent"), center, dir, width, .22f);
        }

        // 휘감는 번개 호 (회전 공격)
        public static void Arc(Vector2 center, Vector2 dir, float width)
        {
            if (WolfFxNet.Send(WolfFxNet.Arc, center, dir, width)) return;
            Facing(Fx("WolfFxArc"), center, dir, width, .22f);
        }

        // 바닥에 원을 그리며 터지는 번개 (착지·포효). at = 발밑, radius = 원 반지름
        public static void GroundRing(Vector2 at, float radius, float flash = 0f)
        {
            if (WolfFxNet.Send(WolfFxNet.GroundRing, at, Vector2.zero, radius, flash)) return;
            Spawn(Fx("WolfFxRing"), at, 0f, radius * 2.2f, 1f, .22f, Random.value < .5f, 0f, false, flash);
        }

        // 땅에서 솟구치는 번개 폭발 (내려찍기·벽 충돌). big이면 큰 것
        public static void Erupt(Vector2 at, float width, bool big = true)
        {
            if (WolfFxNet.Send(WolfFxNet.Erupt, at, Vector2.zero, width, big ? 1f : 0f)) return;
            Spawn(Fx(big ? "WolfFxErupt" : "WolfFxErupt2"), at, 0f, width, 1f, .22f, Random.value < .5f, 0f, false, big ? .2f : 0f);
        }

        // 바닥에서 비스듬히 튀는 작은 번개 (박차고 나갈 때)
        public static void GroundSpark(Vector2 at, Vector2 dir, float width)
        {
            if (WolfFxNet.Send(WolfFxNet.GroundSpark, at, dir, width)) return;
            Spawn(Fx("WolfFxSpark"), at, 0f, width, 1f, .22f, dir.x < 0f, 0f);
        }

        // 방향을 보는 그림: 왼쪽이면 좌우를 뒤집고, 위아래로는 조금만 기울인다
        private static void Facing(Sheet sheet, Vector2 center, Vector2 dir, float width, float life)
        {
            if (dir.sqrMagnitude < .001f) dir = Vector2.left;
            bool left = dir.x < 0f;
            float tilt = Mathf.Clamp(Mathf.Atan2(dir.y, Mathf.Abs(dir.x)) * Mathf.Rad2Deg, -35f, 35f);
            Spawn(sheet, center, left ? -tilt : tilt, width, 1f, life, left, 0f);
        }

        // center에서 사방으로 뻗는 번개
        public static void Burst(Vector2 center, float radius, int count = 6, float life = .22f)
        {
            if (WolfFxNet.Send(WolfFxNet.Burst, center, Vector2.zero, radius, count, life)) return;
            Spawn(BurstSheet(), center, 0f, radius, 1f, life, Random.value < .5f, radius * 1.4f, Random.value < .5f, radius >= 2.4f ? .15f : 0f);
        }

        // 둘레를 감싸는 번개 고리: 둘레를 따라 스파크를 띄엄띄엄 놓는다
        public static void Ring(Vector2 center, float radius, int pieces = 10, float life = .25f)
        {
            if (WolfFxNet.Send(WolfFxNet.Ring, center, Vector2.zero, radius, pieces, life)) return;
            int count = Mathf.Clamp(pieces / 2 + 1, 4, 9);
            float start = Random.Range(0f, 360f);
            float piece = 2f * Mathf.PI * radius / count * .7f;
            for (int i = 0; i < count; i++)
            {
                float a = (start + i * 360f / count) * Mathf.Deg2Rad;
                Vector2 at = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * .7f) * radius;
                float tangent = Mathf.Atan2(Mathf.Cos(a) * .7f, -Mathf.Sin(a)) * Mathf.Rad2Deg;
                Spawn(Spark(), at, tangent + Random.Range(-10f, 10f), piece, 1f, life, Random.value < .5f, 0f);
            }
        }

        // 하늘에서 내리꽂히는 낙뢰 (받은 그림: 땅에 꽂히며 사방으로 튄다). at = 땅에 닿는 자리
        public static void Strike(Vector2 at, float height = 6f, float splash = 1.6f)
        {
            if (WolfFxNet.Send(WolfFxNet.Strike, at, Vector2.zero, height, splash)) return;
            Spawn(Fx("WolfFxStrike"), at, 0f, splash * 3.4f, 1f, .22f, Random.value < .5f, 0f, false, .3f);
        }

        // from에서 to까지 길게 뻗는 번개 줄기: 내리꽂는 번개 그림을 눕혀 길이에 맞춘다 (한 장으로 끊김 없는 줄기)
        public static void Beam(Vector2 from, Vector2 to, float life = .12f, float flash = 0f)
        {
            if (WolfFxNet.Send(WolfFxNet.Beam, from, to, life, flash)) return;
            Vector2 d = from - to;
            float length = d.magnitude;
            if (length < .1f) return;
            // 그림의 위쪽(하늘)이 from, 바닥 기준점이 to를 향하게 눕힌다
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
            Spawn(StrikeSheet(), to, angle, length, 1f, life, Random.value < .5f, 0f, false, flash);
        }

        // 몸 주변에서 지직거리는 짧은 스파크
        public static void Sparks(Vector2 center, float radius, int count = 2)
        {
            if (WolfFxNet.Send(WolfFxNet.Sparks, center, Vector2.zero, radius, count)) return;
            for (int i = 0; i < count; i++)
                Spawn(Spark(), center + Random.insideUnitCircle * radius, Random.Range(0f, 360f), Random.Range(.4f, .7f), 1f, .14f, Random.value < .5f, 0f);
        }

        // 그림 하나를 띄운다. length: 그림의 기준 길이가 이만큼(유닛) 되게, thick: 굵기 배율,
        // life: 원래 길이 대비 재생 시간(기본 .22 기준), glowSize: 밑에 깔 빛 번짐 크기(0이면 없음)
        // flash: 임팩트 프레임(2번째)이 뜨는 순간 화면을 이만큼 번쩍 (0이면 없음)
        private static void Spawn(Sheet sheet, Vector2 at, float angle, float length, float thick, float life, bool flipX, float glowSize, bool flipY = false, float flash = 0f)
        {
            if (sheet == null) return;
            var obj = new GameObject("Wolf Lightning");
            obj.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, angle));
            float scale = length / sheet.Reach;
            obj.transform.localScale = new Vector3(scale, scale * Mathf.Max(.5f, thick), 1f);
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sortingOrder = Order;
            WolfRed.Apply(sr);
            sr.flipX = flipX;
            sr.flipY = flipY;

            SpriteRenderer halo = null;
            if (glowSize > 0f && Glow() != null && Additive() != null)
            {
                // 빛 번짐은 따로 둔다 (번개의 회전·찌그러짐을 따라가지 않고 늘 둥글게)
                var g = new GameObject("Wolf Lightning Glow");
                g.transform.position = at;
                g.transform.localScale = Vector3.one * glowSize;
                halo = g.AddComponent<SpriteRenderer>();
                halo.sprite = glow;
                halo.sharedMaterial = additive;
                halo.color = CurrentGlow;
                halo.sortingOrder = Order - 1;
            }
            obj.AddComponent<Player>().Begin(sheet, sr, halo, Mathf.Max(.3f, life / .22f), flash);
        }

        // 프레임을 넘기고, 빛 번짐은 처음에 확 밝았다 옅어진다
        private sealed class Player : MonoBehaviour
        {
            private Sheet sheet; private SpriteRenderer sr, halo; private float speed, age, flash;

            public void Begin(Sheet s, SpriteRenderer renderer, SpriteRenderer glowRenderer, float stretch, float flashStrength)
            {
                sheet = s; sr = renderer; halo = glowRenderer; speed = 1f / stretch; flash = flashStrength;
                Update();
            }

            private void OnDestroy() { if (halo != null) Destroy(halo.gameObject); }

            private void Update()
            {
                float t = age;
                age += Time.deltaTime * speed;
                if (t >= sheet.Total) { Destroy(gameObject); return; }
                for (int i = 0; i < sheet.Frames.Length; i++)
                {
                    if (t < sheet.Seconds[i])
                    {
                        sr.sprite = sheet.Frames[i];
                        if (i >= 1 && flash > 0f) { ScreenFlash.Show(flash); flash = 0f; } // 임팩트 순간 한 번
                        break;
                    }
                    t -= sheet.Seconds[i];
                }
                if (halo != null)
                {
                    float k = age / sheet.Total;
                    float a = k < .25f ? 1f : 1f - (k - .25f) / .75f;
                    Color g = CurrentGlow;
                    halo.color = new Color(g.r, g.g, g.b, g.a * Mathf.Clamp01(a) * (Random.value < .12f ? .5f : 1f));
                }
            }
        }

        // ---- 그림 읽기 ----

        private static readonly System.Collections.Generic.Dictionary<string, Sheet> fx = new System.Collections.Generic.Dictionary<string, Sheet>();

        // 받은 이펙트: 칸 크기, 기준점(바닥 효과는 바닥선, 나머지는 가운데), 가장 큰 프레임의 폭(px)
        private static Sheet Fx(string name)
        {
            if (fx.TryGetValue(name, out Sheet s)) return s;
            s = name switch
            {
                "WolfFxSpark" => Load(name, 404, 197, new Vector2(.469f, .020f), 355f, .03f, .05f, .06f, .07f, .08f),
                "WolfFxRing" => Load(name, 404, 191, new Vector2(.467f, .031f), 329f, .03f, .05f, .06f, .07f, .08f),
                "WolfFxStrike" => Load(name, 404, 202, new Vector2(.460f, .035f), 282f, .03f, .05f, .06f, .07f, .08f),
                "WolfFxArc" => Load(name, 404, 189, new Vector2(.470f, .492f), 320f, .03f, .05f, .06f, .07f, .08f),
                "WolfFxErupt" => Load(name, 404, 245, new Vector2(.491f, .135f), 321f, .03f, .05f, .06f, .07f, .08f),
                "WolfFxClaw" => Load(name, 368, 145, new Vector2(.530f, .476f), 234f, .03f, .03f, .04f, .04f, .05f, .06f, .06f, .07f),
                "WolfFxCrescent" => Load(name, 368, 144, new Vector2(.511f, .479f), 260f, .03f, .03f, .04f, .04f, .05f, .06f, .06f, .07f),
                "WolfFxBigClaw" => Load(name, 368, 133, new Vector2(.511f, .523f), 224f, .03f, .03f, .04f, .04f, .05f, .06f, .06f, .07f),
                "WolfFxErupt2" => Load(name, 368, 157, new Vector2(.508f, .134f), 246f, .03f, .03f, .04f, .04f, .05f, .06f, .06f, .07f),
                _ => null,
            };
            fx[name] = s;
            return s;
        }

        private static Sheet StrikeSheet() =>
            strike ??= Load("WolfBoltStrike", 192, 384, new Vector2(.5f, 20f / 384f), 354f, .03f, .05f, .05f, .06f, .07f);

        private static Sheet ClawSheet() =>
            claw ??= Load("WolfBoltClaw", 256, 256, new Vector2(.5f, .5f), 210f, .03f, .05f, .05f, .06f, .07f);

        private static Sheet BurstSheet() =>
            burst ??= Load("WolfBoltBurst", 256, 256, new Vector2(.5f, .5f), 112f, .03f, .05f, .05f, .06f, .07f);

        private static Sheet Spark() =>
            spark ??= Load("WolfSpark", 64, 64, new Vector2(.5f, .5f), 48f, .03f, .04f, .04f, .05f, .05f);

        private static Sheet Load(string name, int w, int h, Vector2 pivot, float reach, params float[] seconds)
        {
            Texture2D tex = Resources.Load<Texture2D>("Boss/Wolf/" + name);
            if (tex == null) return null;
            var s = new Sheet { Frames = new Sprite[seconds.Length], Seconds = seconds, Reach = reach / 32f };
            for (int i = 0; i < seconds.Length; i++)
            {
                s.Frames[i] = Sprite.Create(tex, new Rect(i * w, 0, w, h), pivot, 32f, 0, SpriteMeshType.FullRect);
                s.Total += seconds[i];
            }
            return s;
        }

        // 부드러운 둥근 빛 (가운데 밝고 바깥으로 옅어짐), 지름 1유닛
        private static Sprite Glow()
        {
            if (glow != null) return glow;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
            tex.Apply();
            glow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);
            return glow;
        }

        private static Material Additive()
        {
            if (additive != null) return additive;
            additive = Resources.Load<Material>("Boss/Wolf/WolfGlowAdditive");
            return additive;
        }
    }

    // 화면 전체를 아주 짧게(1~2프레임) 연두빛 흰색으로 번쩍 (번개 임팩트)
    public sealed class ScreenFlash : MonoBehaviour
    {
        private static ScreenFlash instance;
        private float strength;

        public static void Show(float amount)
        {
            if (Application.isBatchMode) return;
            if (instance == null) instance = new GameObject("Screen Flash").AddComponent<ScreenFlash>();
            instance.strength = Mathf.Max(instance.strength, Mathf.Clamp01(amount));
        }

        private void Update() => strength = Mathf.MoveTowards(strength, 0f, Time.unscaledDeltaTime * 6f);

        private void OnGUI()
        {
            if (strength <= .01f || Event.current.type != EventType.Repaint) return;
            GUI.depth = -40;
            Color previous = GUI.color;
            Color c = WolfRed.Tint(new Color(.88f, 1f, .72f), new Color(1f, .78f, .72f));
            GUI.color = new Color(c.r, c.g, c.b, strength);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void OnDestroy() { if (instance == this) instance = null; }
    }
}
