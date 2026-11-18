using UnityEngine;

namespace PersistentWorld.World
{
    // 보스 등장 연출: 위아래 검은 띠가 내려오고, 보스 얼굴 클로즈업이 돌·황동 액자에 담겨 미끄러져 들어오며 이름이 뜬다.
    // 화면에만 그리는 연출이다. 혼자 하기는 보스가 깨어날 때, 온라인은 각 클라이언트가 보스방에 들어갈 때 보여 준다.
    // 얼굴 그림은 Resources/Boss/Portraits/<portrait>.png (가로:세로 6:5 고화질). 없으면 골렘 이동 그림에서 머리를 잘라 쓴다.
    // 보스는 이 시간(Duration) 동안 공격하지 않는다.
    public sealed class BossIntro : MonoBehaviour
    {
        public const float Duration = 2.8f;
        // 지금 화면에 등장 연출이 떠 있는지 (혼자 하기 보스는 이게 끝날 때까지 기다린다)
        public static bool Active => FindFirstObjectByType<BossIntro>() != null;
        private static Texture2D sheet;
        private static Rect faceUv;
        private static bool faceReady;
        private static GUIStyle nameStyle, titleStyle;

        private static readonly System.Collections.Generic.Dictionary<string, Texture2D> portraits = new System.Collections.Generic.Dictionary<string, Texture2D>();
        private string bossName, bossTitle;
        private Texture2D portrait;
        // 연출 진행 시간. 실제 시간이 아니라 프레임마다 조금씩(최대 0.05초) 더해서,
        // 장면을 처음 열 때처럼 로딩으로 몇 초 멈춰도 연출이 그 사이에 지나가 버리지 않게 한다.
        private float age;

        public static void Show(string name, string title, string portraitName = null)
        {
            if (Application.isBatchMode) return;
            foreach (BossIntro old in FindObjectsByType<BossIntro>(FindObjectsSortMode.None)) Destroy(old.gameObject);
            var intro = new GameObject("Boss Intro").AddComponent<BossIntro>();
            intro.bossName = name;
            intro.bossTitle = title;
            intro.portrait = Portrait(portraitName);
            PersistentWorld.UI.UiSound.Open();
        }

        private void Update()
        {
            age += Mathf.Min(Time.unscaledDeltaTime, .05f);
            if (age >= Duration) Destroy(gameObject);
        }

        private void OnGUI()
        {
            float t = age;
            if (t >= Duration) return;
            if (Event.current.type != EventType.Repaint) return;
            GUI.depth = -60;
            Ensure();
            float sw = Screen.width, sh = Screen.height, s = sh / 720f;
            float k = Mathf.Min(Ease(t / .35f), Ease((Duration - t) / .4f));

            // 위아래 검은 띠
            float bar = sh * .12f * k;
            Fill(new Rect(0, 0, sw, bar), Color.black);
            Fill(new Rect(0, sh - bar, sw, bar), Color.black);
            Fill(new Rect(0, bar - 2 * s, sw, 2 * s), new Color(.72f, .2f, .14f, k));
            Fill(new Rect(0, sh - bar, sw, 2 * s), new Color(.72f, .2f, .14f, k));

            // 얼굴 클로즈업 액자: 오른쪽에서 미끄러져 들어온다
            float h = 300 * s, w = h * 1.2f;
            float x = Mathf.Lerp(sw + 20 * s, sw * .5f + 30 * s, k);
            var card = new Rect(x, sh * .5f - h * .5f, w, h);
            Frame(card, s, k);
            var inner = new Rect(card.x + 11 * s, card.y + 11 * s, card.width - 22 * s, card.height - 22 * s);
            Fill(inner, new Color(.16f, .06f, .06f, k));
            Fill(new Rect(inner.x, inner.y + inner.height * .55f, inner.width, inner.height * .45f), new Color(0, 0, 0, .35f * k));
            if (portrait != null)
            {
                // 고화질 얼굴 그림: 액자를 꽉 채우도록 맞춘다 (비율이 다르면 가장자리가 잘린다)
                float aspect = portrait.width / (float)portrait.height;
                float fw = inner.width, fh = fw / aspect;
                if (fh < inner.height) { fh = inner.height; fw = fh * aspect; }
                GUI.BeginGroup(inner);
                Color previous = GUI.color; GUI.color = new Color(1, 1, 1, k);
                GUI.DrawTexture(new Rect((inner.width - fw) / 2f, (inner.height - fh) / 2f, fw, fh), portrait);
                GUI.color = previous;
                GUI.EndGroup();
            }
            else if (faceReady)
            {
                float aspect = faceUv.width * sheet.width / (faceUv.height * sheet.height);
                float fw = inner.width, fh = fw / aspect;
                if (fh < inner.height) { fh = inner.height; fw = fh * aspect; }
                var face = new Rect(inner.center.x - fw / 2f, inner.center.y - fh / 2f, fw, fh);
                GUI.BeginGroup(inner);
                Color previous = GUI.color; GUI.color = new Color(1, 1, 1, k);
                GUI.DrawTextureWithTexCoords(new Rect(face.x - inner.x, face.y - inner.y, fw, fh), sheet, faceUv);
                GUI.color = previous;
                GUI.EndGroup();
            }

            // 이름: 왼쪽에서 들어와 액자 왼쪽에 오른쪽 정렬
            nameStyle.fontSize = Mathf.RoundToInt(48 * s);
            titleStyle.fontSize = Mathf.RoundToInt(22 * s);
            float textRight = card.x - 26 * s;
            float slide = (1f - k) * -220 * s;
            var titleRect = new Rect(0, card.center.y - 62 * s, textRight + slide, 30 * s);
            var nameRect = new Rect(0, card.center.y - 30 * s, textRight + slide, 60 * s);
            Shadow(titleRect, bossTitle, titleStyle, k, s);
            Shadow(nameRect, bossName, nameStyle, k, s);
            Fill(new Rect(textRight + slide - 260 * s, card.center.y + 36 * s, 260 * s, 2 * s), new Color(.93f, .78f, .48f, k));

            // 시작 순간 번쩍임
            if (t < .25f) Fill(new Rect(0, 0, sw, sh), new Color(1, 1, 1, .35f * (1f - t / .25f)));
        }

        private static Texture2D Portrait(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!portraits.TryGetValue(name, out Texture2D texture))
                portraits[name] = texture = Resources.Load<Texture2D>("Boss/Portraits/" + name);
            return texture;
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        private static void Frame(Rect r, float s, float alpha)
        {
            Color outline = new Color32(23, 19, 15, 255), stone = new Color32(110, 102, 92, 255), light = new Color32(140, 132, 118, 255);
            Color brass = new Color32(196, 150, 70, 255), brassLight = new Color32(236, 199, 122, 255);
            outline.a = stone.a = light.a = brass.a = brassLight.a = alpha;
            Fill(new Rect(r.x + 4 * s, r.y + 6 * s, r.width, r.height), new Color(0, 0, 0, .55f * alpha));
            Fill(r, outline);
            Fill(new Rect(r.x + 2 * s, r.y + 2 * s, r.width - 4 * s, r.height - 4 * s), stone);
            Fill(new Rect(r.x + 2 * s, r.y + 2 * s, r.width - 4 * s, 2 * s), light);
            Fill(new Rect(r.x + 9 * s, r.y + 9 * s, r.width - 18 * s, r.height - 18 * s), outline);
            float cap = 18 * s;
            foreach (Vector2 c in new[] { new Vector2(r.x, r.y), new Vector2(r.xMax - cap, r.y), new Vector2(r.x, r.yMax - cap), new Vector2(r.xMax - cap, r.yMax - cap) })
            {
                Fill(new Rect(c.x, c.y, cap, cap), outline);
                Fill(new Rect(c.x + 2 * s, c.y + 2 * s, cap - 4 * s, cap - 4 * s), brass);
                Fill(new Rect(c.x + 2 * s, c.y + 2 * s, cap - 4 * s, 2 * s), brassLight);
                Fill(new Rect(c.x + cap / 2f - 2 * s, c.y + cap / 2f - 2 * s, 4 * s, 4 * s), outline);
            }
        }

        private static void Shadow(Rect rect, string text, GUIStyle style, float alpha, float s)
        {
            Color c = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, .85f * alpha);
            GUI.Label(new Rect(rect.x + 3 * s, rect.y + 3 * s, rect.width, rect.height), text, style);
            style.normal.textColor = new Color(c.r, c.g, c.b, alpha);
            GUI.Label(rect, text, style);
            style.normal.textColor = c;
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }

        private static void Ensure()
        {
            if (nameStyle != null) return;
            Font body = Resources.Load<Font>("UI/Fonts/Galmuri11");
            Font bold = Resources.Load<Font>("UI/Fonts/Galmuri11-Bold");
            if (bold == null) bold = body;
            nameStyle = new GUIStyle(GUI.skin.label) { font = bold, alignment = TextAnchor.MiddleRight, wordWrap = false, clipping = TextClipping.Overflow, normal = { textColor = new Color32(240, 212, 138, 255) } };
            titleStyle = new GUIStyle(GUI.skin.label) { font = body, alignment = TextAnchor.MiddleRight, wordWrap = false, clipping = TextClipping.Overflow, normal = { textColor = new Color32(214, 196, 170, 255) } };
            // 보스 기본 자세 첫 프레임에서 머리 쪽(위 45%, 가운데 70%)을 잘라 얼굴로 쓴다
            sheet = SpriteSheetSlicer.Load("Boss/BossMove", 6, 4);
            if (sheet == null) return;
            RectInt b = SpriteSheetSlicer.FirstFrameBounds(sheet, 6, 4);
            int cellHeight = sheet.height / 4;
            float fw = b.width * .7f, fh = b.height * .45f;
            float fx = b.x + (b.width - fw) / 2f, fy = 3 * cellHeight + b.y + b.height - fh;
            faceUv = new Rect(fx / sheet.width, fy / sheet.height, fw / sheet.width, fh / sheet.height);
            faceReady = true;
        }
    }
}
