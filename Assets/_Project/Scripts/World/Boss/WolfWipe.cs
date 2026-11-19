using System.Collections.Generic;
using PersistentWorld.Combat;
using UnityEngine;

namespace PersistentWorld.World
{
    // 늑대 전멸기(뇌광 폭주)의 부채꼴 경고 조각: 늑대(중심)에서 필드 끝까지 뻗는 조각 하나.
    // 경고 시간 동안 깜빡이며 안쪽에서 바깥으로 진행 표시가 차오르고, FireAll()에 밝게 번쩍인 뒤 사라진다.
    // 위험하다는 느낌: 바닥을 기는 번개, 경계선을 타고 달리는 전류, 낙뢰가 꽂힐 표적 원,
    // 터지기 직전 하늘에서 내려오는 가는 선도 번개. 끝에 가까울수록 잦아진다.
    public sealed class WolfSliceZone : MonoBehaviour
    {
        private const int SortingOrder = -500; // 다른 바닥 경고와 같은 층
        private const int Segments = 24;
        private static readonly List<WolfSliceZone> all = new List<WolfSliceZone>();
        private static Material material;
        private static Sprite reticle;

        private MeshRenderer area, fill;
        private LineRenderer edge;
        private Color color;
        private float startTime, warnTime, inner, reach, firedAt = -1f;
        private float fromDeg, toDeg, nextArc, nextEdge, nextSpark;
        private bool leadersShown;
        private bool plain; // 뇌격 휩쓸기 경고: 표적·번개 장식 없이, 진행 표시가 휩쓸 방향으로 각도째 차오른다
        private bool ghost; // 온라인 서버: 그림 없이 표적 자리만 기억한다 (그림은 각 클라이언트가 신호를 받아 그린다)
        private SpriteRenderer[] marks;
        private const float Visible = 12f; // 효과는 화면에 보이는 거리 안쪽에만

        // 낙뢰가 꽂힐 자리 (경고 동안 표적 원으로 보여 주고, 터질 때 그대로 친다)
        public Vector2[] Targets { get; private set; }

        // 휩쓸기 경고: fromDeg에서 toDeg 쪽으로 진행 표시가 차오른다 (그쪽으로 휩쓴다는 뜻)
        public static WolfSliceZone Sweep(Vector2 center, float fromDeg, float toDeg, float inner, float reach, float warnTime, Color color) =>
            Spawn(center, fromDeg, toDeg, inner, reach, warnTime, color, true);

        // fromDeg~toDeg 부채꼴, 중심에서 inner~reach 사이
        // targets: 낙뢰 자리를 정해서 줄 때 (온라인 클라이언트가 서버와 같은 자리를 쓰도록). null이면 여기서 정한다
        public static WolfSliceZone Spawn(Vector2 center, float fromDeg, float toDeg, float inner, float reach, float warnTime, Color color, bool plain = false, Vector2[] targets = null)
        {
            var obj = new GameObject("Wolf Slice Zone");
            obj.transform.position = center;
            var zone = obj.AddComponent<WolfSliceZone>();
            zone.color = color;
            zone.startTime = Time.time;
            zone.warnTime = Mathf.Max(.05f, warnTime);
            zone.inner = inner;
            zone.reach = reach;
            zone.fromDeg = fromDeg;
            zone.toDeg = toDeg;
            zone.plain = plain;
            zone.ghost = PersistentWorld.Networking.NetworkGameWorld.IsServerWorld;
            if (zone.ghost)
            {
                zone.Targets = plain ? new Vector2[0] : zone.RollTargets(center);
                var args = new List<float> { fromDeg, toDeg, inner, reach, warnTime, color.r, color.g, color.b, color.a, plain ? 1f : 0f, 0f };
                foreach (Vector2 t in zone.Targets) { args.Add(t.x); args.Add(t.y); }
                WolfFxNet.Send(WolfFxNet.Slice, center, Vector2.zero, args.ToArray());
                all.Add(zone);
                return zone;
            }
            zone.area = zone.Fan("Area", fromDeg, toDeg, inner, reach, SortingOrder);
            zone.fill = zone.Fan("Fill", fromDeg, toDeg, inner, reach, SortingOrder + 1);
            // 경계선: 바깥 끝 → 안쪽 → 바깥 끝 (조각 모양이 또렷하게)
            zone.edge = obj.AddComponent<LineRenderer>();
            zone.edge.sharedMaterial = Material;
            zone.edge.useWorldSpace = false;
            zone.edge.widthMultiplier = .09f;
            zone.edge.sortingOrder = SortingOrder + 2;
            zone.edge.positionCount = 3;
            zone.edge.SetPosition(0, Polar(fromDeg, reach));
            zone.edge.SetPosition(1, Vector3.zero);
            zone.edge.SetPosition(2, Polar(toDeg, reach));
            if (plain) { zone.Targets = new Vector2[0]; zone.marks = new SpriteRenderer[0]; }
            else zone.PlaceTargets(targets ?? zone.RollTargets(center));
            all.Add(zone);
            zone.Update();
            return zone;
        }

        public static void FireAll()
        {
            WolfSliceZone any = all.Find(z => z != null);
            if (any != null && any.ghost) WolfFxNet.Send(WolfFxNet.SliceFire, any.transform.position, Vector2.zero);
            foreach (WolfSliceZone zone in all) if (zone != null && zone.firedAt < 0f) zone.firedAt = Time.time;
        }

        public static void ClearAll()
        {
            WolfSliceZone any = all.Find(z => z != null);
            if (any != null && any.ghost) WolfFxNet.Send(WolfFxNet.SliceClear, any.transform.position, Vector2.zero);
            foreach (WolfSliceZone zone in all) if (zone != null) Destroy(zone.gameObject);
            all.Clear();
        }

        private void OnDestroy() => all.Remove(this);

        private void Update()
        {
            if (ghost)
            {
                if (firedAt >= 0f ? Time.time - firedAt > .35f : plain && Time.time - startTime > warnTime) Destroy(gameObject);
                return;
            }
            // 휩쓸기 경고는 다 차면 저절로 사라진다 (온라인 클라이언트는 따로 지우라는 신호를 받지 않는다)
            if (plain && firedAt < 0f && Time.time - startTime > warnTime) { Destroy(gameObject); return; }
            if (firedAt >= 0f)
            {
                // 번쩍인 뒤 빠르게 사라진다
                float k = Mathf.Clamp01((Time.time - firedAt) / .35f);
                Color flash = Color.Lerp(new Color(1f, .9f, .85f), color, k);
                Tint(area, flash, .75f * (1f - k));
                Tint(fill, flash, 0f);
                edge.startColor = edge.endColor = new Color(flash.r, flash.g, flash.b, 1f - k);
                foreach (SpriteRenderer mark in marks) mark.color = new Color(flash.r, flash.g, flash.b, 1f - k);
                if (k >= 1f) Destroy(gameObject);
                return;
            }
            float t = Mathf.Clamp01((Time.time - startTime) / warnTime);
            float phase = (Time.time - startTime) * Mathf.Lerp(3f, 14f, t * t);
            float blink = .5f + .5f * Mathf.Sin(phase * Mathf.PI * 2f);
            Color bright = Color.Lerp(color, new Color(1f, .55f, .3f), blink * .5f);
            Tint(area, bright, Mathf.Lerp(.16f, .34f, blink));
            Tint(fill, new Color(1f, .35f, .2f), .32f);
            edge.startColor = edge.endColor = new Color(bright.r, bright.g, bright.b, Mathf.Lerp(.6f, 1f, blink));
            if (plain)
            {
                // 휩쓸 방향으로 각도째 차오른다
                Reshape(fill, fromDeg, Mathf.Lerp(fromDeg, toDeg, Mathf.Max(.02f, t)));
                return;
            }
            // 진행 표시: 안쪽에서 바깥으로 차오른다
            float r = Mathf.Lerp(inner, reach, t * t) / reach;
            fill.transform.localScale = new Vector3(r, r, 1f);
            Menace(t, bright, blink);
        }

        // 조각 안 임의의 자리 (안쪽 원 바로 밖 ~ 화면에 보이는 거리)
        private Vector2 RandomPoint(float spread = .9f)
        {
            float a = Mathf.Lerp(fromDeg, toDeg, Random.Range(1f - spread, spread));
            float d = Mathf.Lerp(inner + .6f, Visible, Mathf.Sqrt(Random.value));
            return (Vector2)transform.position + (Vector2)Polar(a, d);
        }

        // 낙뢰 자리 6곳: 조각 안에 고르게 (거리를 나눠 하나씩)
        private Vector2[] RollTargets(Vector2 center)
        {
            var targets = new Vector2[6];
            for (int i = 0; i < targets.Length; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, Random.Range(.15f, .85f));
                float d = Mathf.Lerp(inner + 1f, Visible, (i + Random.Range(.15f, .85f)) / targets.Length);
                targets[i] = center + (Vector2)Polar(a, d);
            }
            return targets;
        }

        private void PlaceTargets(Vector2[] targets)
        {
            Targets = targets;
            marks = new SpriteRenderer[Targets.Length];
            for (int i = 0; i < Targets.Length; i++)
            {
                var obj = new GameObject("Strike Mark");
                obj.transform.SetParent(transform, false);
                obj.transform.position = Targets[i];
                marks[i] = obj.AddComponent<SpriteRenderer>();
                marks[i].sprite = Reticle;
                marks[i].sortingOrder = SortingOrder + 3;
            }
        }

        private void Menace(float t, Color bright, float blink)
        {
            // 표적 원: 크게 떴다가 조여 들며 진해진다
            for (int i = 0; i < marks.Length; i++)
            {
                float size = Mathf.Lerp(2.4f, 1.1f, 1f - (1f - t) * (1f - t)) * (1f + .06f * Mathf.Sin(Time.time * 24f + i));
                marks[i].transform.localScale = Vector3.one * size;
                marks[i].transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * Mathf.Lerp(40f, 260f, t) + i * 30f);
                Color c = leadersShown ? Color.Lerp(bright, Color.white, .7f) : bright;
                marks[i].color = new Color(c.r, c.g, c.b, Mathf.Lerp(.5f, 1f, t) * Mathf.Lerp(.75f, 1f, blink));
            }
            // 바닥을 기는 번개
            if (Time.time >= nextArc)
            {
                Vector2 p = RandomPoint();
                float a = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                WolfLightning.Bolt(p, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * .6f) * Random.Range(1.6f, 3f), .12f, .16f, .6f);
                if (Random.value < .5f) WolfLightning.Sparks(p, .5f, 2);
                nextArc = Time.time + Mathf.Lerp(.16f, .035f, t);
            }
            // 경계선을 타고 바깥으로 달리는 전류
            if (Time.time >= nextEdge)
            {
                float side = Random.value < .5f ? fromDeg : toDeg;
                float d = Mathf.Lerp(inner, Visible - 1.5f, Random.value);
                WolfLightning.Bolt((Vector2)transform.position + (Vector2)Polar(side, d), (Vector2)transform.position + (Vector2)Polar(side, d + 1.8f), .1f, .16f, .35f);
                nextEdge = Time.time + Mathf.Lerp(.3f, .08f, t);
            }
            // 표적에서 튀는 불꽃
            if (Time.time >= nextSpark)
            {
                WolfLightning.Sparks(Targets[Random.Range(0, Targets.Length)], .35f, 1);
                nextSpark = Time.time + Mathf.Lerp(.25f, .08f, t);
            }
            // 터지기 직전: 하늘에서 표적으로 가는 선도 번개가 먼저 내려온다
            if (!leadersShown && (1f - t) * warnTime <= .3f)
            {
                leadersShown = true;
                foreach (Vector2 target in Targets)
                    WolfLightning.Bolt(target + new Vector2(Random.Range(-1f, 1f), 7f), target, .05f, .3f, .7f);
            }
        }

        private static void Tint(MeshRenderer renderer, Color c, float alpha)
        {
            c.a = alpha;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", c);
            renderer.SetPropertyBlock(block);
        }

        private MeshRenderer Fan(string name, float fromDeg, float toDeg, float inner, float reach, int order)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= Segments; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, i / (float)Segments);
                vertices.Add(Polar(a, inner));
                vertices.Add(Polar(a, reach));
                if (i == 0) continue;
                int b = i * 2;
                triangles.AddRange(new[] { b - 2, b - 1, b, b, b - 1, b + 1 });
            }
            var mesh = new Mesh { name = "Wolf Slice" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material;
            renderer.sortingOrder = order;
            return renderer;
        }

        // 부채꼴 모양만 다시 잡는다 (꼭짓점 수는 그대로)
        private void Reshape(MeshRenderer renderer, float from, float to)
        {
            Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var vertices = new Vector3[(Segments + 1) * 2];
            for (int i = 0; i <= Segments; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)Segments);
                vertices[i * 2] = Polar(a, inner);
                vertices[i * 2 + 1] = Polar(a, reach);
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        // 조준 고리: 굵은 테두리 + 네 방향 눈금 + 가운데 점 (돌아가며 조여 든다)
        internal static Sprite Reticle
        {
            get
            {
                if (reticle != null) return reticle;
                const int size = 96;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                float r = size * .5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 v = new Vector2(x + .5f - r, y + .5f - r);
                    float d = v.magnitude / r;
                    float a = 0f;
                    if (d > .8f && d < .95f) a = 1f;                                   // 바깥 고리
                    else if (d < .8f) a = .16f;                                         // 옅은 안쪽
                    if (d < .14f) a = 1f;                                               // 가운데 점
                    bool tick = (Mathf.Abs(v.x) < 2.5f || Mathf.Abs(v.y) < 2.5f) && d > .45f && d < .8f;
                    if (tick) a = .9f;                                                  // 눈금
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                texture.Apply();
                reticle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
                reticle.name = "Wolf Strike Reticle";
                return reticle;
            }
        }

        private static Vector3 Polar(float deg, float radius) =>
            new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad), 0f) * radius;

        private static Material Material
        {
            get
            {
                if (material == null) material = new Material(Shader.Find("Sprites/Default")) { name = "Wolf Slice Zone" };
                return material;
            }
        }
    }

    // 혼자 하기 늑대 체력바: 화면 위쪽 가운데. 페이즈마다 한 줄씩 (2페이즈로 넘어가며 다시 꽉 차오른다)
    public sealed class WolfBossBar : MonoBehaviour
    {
        private WolfBossController wolf;
        private Health health;
        private static Texture2D white;

        public static void Show(WolfBossController target)
        {
            if (Application.isBatchMode || target == null) return;
            var bar = new GameObject("Wolf Boss Bar").AddComponent<WolfBossBar>();
            bar.wolf = target;
            bar.health = target.GetComponent<Health>();
        }

        private void OnGUI()
        {
            if (wolf == null || health == null) { Destroy(gameObject); return; }
            if (health.IsDead || BossIntro.Active || Event.current.type != EventType.Repaint) return;
            if (white == null) white = Texture2D.whiteTexture;
            wolf.BarValues(out int current, out int max);
            float scale = Mathf.Max(.5f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Mathf.Min(420f, Screen.width / scale - 32f);
            var box = new Rect((Screen.width / scale - width) / 2f, 18f, width, 50f);
            Color red = WolfRed.Tint(new Color(.45f, .85f, .25f), new Color(.9f, .2f, .18f));
            Fill(box, new Color(.06f, .05f, .05f, .82f));
            Fill(new Rect(box.x, box.y, box.width, 2f), red);
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 15 };
            style.normal.textColor = Color.white;
            string phase = wolf.IsPhaseTwo ? " · 광폭화" : "";
            GUI.Label(new Rect(box.x, box.y + 4f, box.width, 22f), $"{wolf.DisplayName}{phase}  {current}/{max}", style);
            var track = new Rect(box.x + 12f, box.y + 32f, box.width - 24f, 9f);
            Fill(track, new Color(0f, 0f, 0f, .7f));
            Fill(new Rect(track.x, track.y, track.width * Mathf.Clamp01(current / (float)max), track.height), red);
            GUI.matrix = previous;
        }

        private static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }
    }

    // 전멸기 진행 중 화면 위쪽 경고 띠 (혼자 하기). 글은 늑대가 매 프레임 알려 준다
    public sealed class WolfWipeBanner : MonoBehaviour
    {
        private static WolfWipeBanner instance;
        private System.Func<string> text;
        private static System.Func<string> source;
        // 지금 띄울 글 (없으면 null). 온라인 서버는 이 글을 늑대 보스방 화면 정보에 실어 보낸다
        public static string Current => source?.Invoke();

        public static void Show(System.Func<string> text)
        {
            source = text;
            if (Application.isBatchMode || PersistentWorld.Networking.NetworkGameWorld.Instance != null) return;
            var source_ = text;
            if (instance == null) instance = new GameObject("Wolf Wipe Banner").AddComponent<WolfWipeBanner>();
            instance.text = source_;
        }

        public static void Hide()
        {
            source = null;
            if (instance != null) Destroy(instance.gameObject);
            instance = null;
        }

        private void OnGUI()
        {
            if (text == null || PersistentWorld.Networking.NetworkGameWorld.Instance != null) return;
            GUI.depth = -40;
            float scale = Mathf.Max(.5f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            BossWarningBanner.Draw(text(), Screen.width / scale, 96);
            GUI.matrix = previous;
        }

        private void OnDestroy() { if (instance == this) instance = null; }
    }
}
