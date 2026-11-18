using System.Text.RegularExpressions;
using UnityEngine;

namespace PersistentWorld.World
{
    // 보스 경고 띠: "즉사기 12초\n조각상을 모두 부수세요 2/4" 같은 안내를 경고 표지판처럼 보여 준다.
    // 양 끝 위험 줄무늬, 맥동하는 붉은 테두리, 큰 카운트다운 숫자, 조각상 진행 표시(◆).
    // 혼자 하기에서는 BossNoticeView가, 온라인에서는 파티 HUD가 같은 그림으로 그린다.
    public static class BossWarningBanner
    {
        private static Texture2D stripes;
        private static GUIStyle title, goal, number, unit, icon;

        public static void Draw(string notice, float screenWidth, float y)
        {
            if (string.IsNullOrEmpty(notice) || Event.current.type != EventType.Repaint) return;
            Ensure();
            string[] lines = notice.Split('\n');
            string head = lines[0], line = lines.Length > 1 ? lines[1] : "";
            Match secondsMatch = Regex.Match(head, @"(\d+)\s*초");
            int seconds = secondsMatch.Success ? int.Parse(secondsMatch.Groups[1].Value) : -1;
            string heading = Regex.Replace(head, @"\s*\d+\s*초", "").Trim();
            Match progress = Regex.Match(line, @"(\d+)\s*/\s*(\d+)");
            int done = progress.Success ? int.Parse(progress.Groups[1].Value) : -1;
            int total = progress.Success ? int.Parse(progress.Groups[2].Value) : 0;
            string instruction = Regex.Replace(line, @"\s*\d+\s*/\s*\d+", "").Trim();

            float w = Mathf.Min(560, screenWidth - 32), h = 86;
            var r = new Rect((screenWidth - w) / 2f, y, w, h);
            float time = Time.unscaledTime;
            bool urgent = seconds >= 0 && seconds <= 5;
            float pulse = .5f + .5f * Mathf.Sin(time * (urgent ? 14f : 5f));

            // 바탕은 살짝 비치게 해서 띠 뒤의 보스·바닥 경고가 보이게 한다 (글자·테두리·줄무늬는 진하게 그대로)
            // 그림자는 띠 바깥(오른쪽·아래)에만: 바탕이 비쳐도 안쪽에 얼룩이 지지 않게
            Fill(new Rect(r.xMax, r.y + 4, 3, r.height), new Color(0, 0, 0, .3f));
            Fill(new Rect(r.x + 3, r.yMax, r.width - 3, 4), new Color(0, 0, 0, .3f));
            Fill(r, new Color32(24, 10, 12, 170));
            Fill(new Rect(r.x, r.y, r.width, r.height * .5f), new Color(1, .3f, .2f, .05f));
            // 양 끝 위험 줄무늬
            const float band = 26;
            GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y, band, h), stripes, new Rect(time * .6f, 0, band / 16f, h / 16f));
            GUI.DrawTextureWithTexCoords(new Rect(r.xMax - band, r.y, band, h), stripes, new Rect(-time * .6f, 0, band / 16f, h / 16f));
            // 테두리: 바깥 어둠 + 맥동하는 붉은 선 + 안쪽 어둠
            Border(r, new Color32(14, 4, 4, 255), 2);
            Border(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), Color.Lerp(new Color32(120, 22, 18, 255), new Color32(255, 92, 64, 255), pulse), 2);
            Border(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), new Color32(14, 4, 4, 255), 1);

            // 경고 표지(마름모 + !)
            var iconCenter = new Vector2(r.x + band + 30, r.y + h / 2f);
            Matrix4x4 previous = GUI.matrix;
            PersistentWorld.UI.GuiPivot.Rotate(iconCenter, 45f);
            Fill(new Rect(iconCenter.x - 17, iconCenter.y - 17, 34, 34), new Color32(14, 4, 4, 255));
            Fill(new Rect(iconCenter.x - 15, iconCenter.y - 15, 30, 30), Color.Lerp(new Color32(190, 40, 28, 255), new Color32(255, 110, 70, 255), pulse));
            GUI.matrix = previous;
            GUI.Label(new Rect(iconCenter.x - 20, iconCenter.y - 20, 40, 40), "!", icon);

            float textX = r.x + band + 62;
            float numberWidth = seconds >= 0 ? 96 : 0;
            float textWidth = r.xMax - band - numberWidth - textX - 8;
            Shadowed(new Rect(textX, r.y + 10, textWidth, 32), string.IsNullOrEmpty(heading) ? head : heading, title);
            Shadowed(new Rect(textX, r.y + 46, textWidth, 24), instruction, goal);
            if (done >= 0 && total > 0)
            {
                // 조각상 진행: 부순 만큼 황동색 ◆
                float px = textX + goal.CalcSize(new GUIContent(instruction)).x + 14;
                for (int i = 0; i < total; i++)
                {
                    var c = new Vector2(px + i * 18, r.y + 58);
                    previous = GUI.matrix;
                    PersistentWorld.UI.GuiPivot.Rotate(c, 45f);
                    Fill(new Rect(c.x - 6, c.y - 6, 12, 12), new Color32(14, 4, 4, 255));
                    Fill(new Rect(c.x - 4, c.y - 4, 8, 8), i < done ? (Color)new Color32(236, 199, 122, 255) : new Color32(70, 40, 36, 255));
                    GUI.matrix = previous;
                }
            }
            if (seconds >= 0)
            {
                var numberRect = new Rect(r.xMax - band - numberWidth, r.y + 6, numberWidth, 52);
                number.normal.textColor = urgent ? Color.Lerp(Color.white, new Color32(255, 80, 60, 255), pulse) : new Color32(255, 236, 214, 255);
                previous = GUI.matrix;
                if (urgent) PersistentWorld.UI.GuiPivot.Scale(numberRect.center, 1f + .12f * pulse);
                Shadowed(numberRect, seconds.ToString(), number);
                GUI.matrix = previous;
                GUI.Label(new Rect(numberRect.x, r.y + 56, numberWidth, 20), "초", unit);
            }
        }

        private static void Shadowed(Rect rect, string text, GUIStyle style)
        {
            Color c = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, .8f);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), text, style);
            style.normal.textColor = c;
            GUI.Label(rect, text, style);
        }

        private static void Ensure()
        {
            if (stripes != null && title != null) return;
            stripes = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                stripes.SetPixel(x, y, (x + y) % 16 < 8 ? new Color32(206, 46, 32, 255) : new Color32(30, 10, 10, 255));
            stripes.Apply();
            Font body = Resources.Load<Font>("UI/Fonts/Galmuri11");
            Font bold = Resources.Load<Font>("UI/Fonts/Galmuri11-Bold") ?? body;
            title = new GUIStyle(GUI.skin.label) { font = bold, fontSize = 26, alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Overflow, normal = { textColor = new Color32(255, 128, 96, 255) } };
            goal = new GUIStyle(GUI.skin.label) { font = body, fontSize = 17, alignment = TextAnchor.MiddleLeft, wordWrap = false, normal = { textColor = new Color32(238, 226, 206, 255) } };
            number = new GUIStyle(GUI.skin.label) { font = bold, fontSize = 44, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            unit = new GUIStyle(GUI.skin.label) { font = body, fontSize = 13, alignment = TextAnchor.UpperCenter, normal = { textColor = new Color32(220, 190, 180, 255) } };
            icon = new GUIStyle(GUI.skin.label) { font = bold, fontSize = 26, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color32(255, 244, 228, 255) } };
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }

        private static void Border(Rect r, Color color, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), color); Fill(new Rect(r.x, r.yMax - t, r.width, t), color);
            Fill(new Rect(r.x, r.y, t, r.height), color); Fill(new Rect(r.xMax - t, r.y, t, r.height), color);
        }
    }

    // 혼자 하기: 보스의 즉사기 카운트다운 글자(TextMesh)를 화면 위쪽 경고 띠로 그린다.
    // 온라인 서버는 이 글자 내용을 그대로 클라이언트에 보내고, 클라이언트 HUD가 같은 띠를 그린다.
    public sealed class BossNoticeView : MonoBehaviour
    {
        private TextMesh source;

        private void Awake() => source = GetComponent<TextMesh>();

        private void OnGUI()
        {
            if (source == null || PersistentWorld.Networking.NetworkGameWorld.Instance != null) return;
            GUI.depth = -40;
            float scale = Mathf.Max(.5f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            BossWarningBanner.Draw(source.text, Screen.width / scale, 96);
            GUI.matrix = previous;
        }
    }
}
