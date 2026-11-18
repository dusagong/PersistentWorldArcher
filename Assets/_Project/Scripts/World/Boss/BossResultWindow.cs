using PersistentWorld.Networking;
using PersistentWorld.Player;
using PersistentWorld.UI;
using UnityEngine;

namespace PersistentWorld.World
{
    // 보스 토벌 결과창(딜량표). 순위, 직업, 보스에게 준 피해와 비중 막대, 초당 피해, 받은 피해, 쓰러진 횟수.
    // 막대는 순서대로 차오르고 숫자는 올라가며 세어진다. 닫기 버튼이나 ESC로 닫는다.
    // 온라인은 파티 HUD가, 혼자 하기는 BossResultView가 같은 그림으로 그린다.
    internal static class BossResultWindow
    {
        private const float RowHeight = 50, Top = 128, Bottom = 70;
        private static GUIStyle rank, name, sub, number, header, barText;

        internal static Rect Bounds(BossResultFrame result, float width, float height)
        {
            int count = result?.rows?.Length ?? 0;
            float w = Mathf.Min(680, width - 24), h = Top + Mathf.Max(1, count) * RowHeight + Bottom;
            return new Rect((width - w) / 2f, Mathf.Max(12, (height - h) / 2f - 20), w, h);
        }

        // 닫기를 누르면 true
        internal static bool Draw(BossResultFrame result, float age, ulong me, float width, float height, RpgGuiTheme theme)
        {
            Ensure();
            Rect r = Bounds(result, width, height);
            RpgGuiTheme.Fill(new Rect(0, 0, width, height), new Color(0, 0, 0, .5f * RpgGuiJuice.FadeIn(age)));
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = previous * RpgGuiJuice.Pop(r.center, age, .85f);
            bool close = false;
            try
            {
                RpgGuiTheme.Panel(r, RpgGuiTheme.Gold);
                BossResultRow[] rows = result.rows ?? new BossResultRow[0];
                int total = 0, top = 1;
                foreach (BossResultRow row in rows) { total += row.damage; top = Mathf.Max(top, row.damage); }
                float seconds = Mathf.Max(1f, result.seconds);

                GUI.Label(new Rect(r.x, r.y + 18, r.width, 30), "토벌 완료", theme.Heading);
                string time = Mathf.FloorToInt(result.seconds / 60f) + ":" + Mathf.FloorToInt(result.seconds % 60f).ToString("00");
                GUI.Label(new Rect(r.x, r.y + 50, r.width, 22), $"{result.boss}  ·  소요 시간 {time}  ·  파티 총 피해 {total:N0}", theme.Muted);
                RpgGuiTheme.Divider(new Rect(r.x + 24, r.y + 80, r.width - 48, 5), RpgGuiTheme.Gold);

                // 열: 순위 | 이름 | 피해량 막대 | 초당 피해 | 받은 피해 | 쓰러짐
                float x = r.x + 24, w = r.width - 48;
                float deathsX = x + w - 52, takenX = deathsX - 86, dpsX = takenX - 86, barX = x + 196, barW = dpsX - 10 - barX;
                float hy = r.y + 92;
                GUI.Label(new Rect(x, hy, 34, 22), "순위", header);
                GUI.Label(new Rect(x + 44, hy, 140, 22), "이름", header);
                GUI.Label(new Rect(barX, hy, barW, 22), "보스에게 준 피해", header);
                GUI.Label(new Rect(dpsX, hy, 80, 22), "초당 피해", header);
                GUI.Label(new Rect(takenX, hy, 80, 22), "받은 피해", header);
                GUI.Label(new Rect(deathsX, hy, 52, 22), "쓰러짐", header);

                for (int i = 0; i < rows.Length; i++)
                {
                    BossResultRow row = rows[i];
                    var rr = new Rect(x - 6, r.y + Top + i * RowHeight, w + 12, RowHeight - 6);
                    float t = Ease((age - .3f - i * .14f) / .8f);
                    bool mine = row.id == me;
                    Color tint = ClassColor(row.character);
                    RpgGuiTheme.Fill(rr, mine ? new Color(.77f, .59f, .27f, .16f) : new Color(1, 1, 1, i % 2 == 0 ? .04f : .015f));
                    if (mine) RpgGuiTheme.Border(rr, new Color(.92f, .78f, .48f, .7f));

                    // 순위 (1등은 황동색)
                    rank.normal.textColor = i == 0 && row.damage > 0 ? new Color32(236, 199, 122, 255) : new Color32(190, 180, 160, 255);
                    GUI.Label(new Rect(x, rr.y, 34, rr.height), (i + 1).ToString(), rank);

                    // 직업 표지 + 이름
                    var chip = new Rect(x + 44, rr.y + (rr.height - 28) / 2f, 28, 28);
                    RpgGuiTheme.Fill(chip, new Color32(23, 19, 15, 255));
                    RpgGuiTheme.Fill(new Rect(chip.x + 2, chip.y + 2, 24, 24), Color.Lerp(tint, Color.black, .35f));
                    RpgGuiTheme.Fill(new Rect(chip.x + 2, chip.y + 2, 24, 2), tint);
                    string className = SkillCatalog.ClassName((CharacterClass)row.character);
                    GUI.Label(chip, className.Substring(0, 1), barText);
                    float nameX = chip.xMax + 8;
                    GUI.Label(new Rect(nameX, rr.y + 4, barX - nameX - 6, 22), row.name, name);
                    GUI.Label(new Rect(nameX, rr.y + 24, barX - nameX - 6, 16), className, sub);
                    if (i == 0 && row.damage > 0 && rows.Length > 1)
                    {
                        float nameWidth = Mathf.Min(name.CalcSize(new GUIContent(row.name)).x, barX - nameX - 46);
                        Badge(new Rect(nameX + nameWidth + 6, rr.y + 7, 34, 16), "MVP");
                    }

                    // 피해 막대: 1등 기준 길이, 차오르면서 숫자가 올라간다
                    var bar = new Rect(barX, rr.y + 10, barW, rr.height - 20);
                    RpgGuiTheme.Fill(bar, new Color32(18, 16, 14, 255));
                    RpgGuiTheme.Border(bar, new Color32(23, 19, 15, 255));
                    float fill = (bar.width - 4) * row.damage / top * t;
                    if (fill > 0)
                    {
                        RpgGuiTheme.Fill(new Rect(bar.x + 2, bar.y + 2, fill, bar.height - 4), Color.Lerp(tint, Color.black, .2f));
                        RpgGuiTheme.Fill(new Rect(bar.x + 2, bar.y + 2, fill, 2), Color.Lerp(tint, Color.white, .35f));
                    }
                    int shown = Mathf.RoundToInt(row.damage * t);
                    float share = total > 0 ? row.damage * 100f / total : 0f;
                    barText.alignment = TextAnchor.MiddleLeft;
                    GUI.Label(new Rect(bar.x + 8, bar.y, bar.width - 16, bar.height), shown.ToString("N0"), barText);
                    barText.alignment = TextAnchor.MiddleRight;
                    GUI.Label(new Rect(bar.x + 8, bar.y, bar.width - 16, bar.height), (share * t).ToString("0.0") + "%", barText);
                    barText.alignment = TextAnchor.MiddleCenter;

                    GUI.Label(new Rect(dpsX, rr.y, 80, rr.height), Mathf.RoundToInt(row.damage / seconds * t).ToString("N0"), number);
                    GUI.Label(new Rect(takenX, rr.y, 80, rr.height), Mathf.RoundToInt(row.taken * t).ToString("N0"), number);
                    number.normal.textColor = row.deaths > 0 ? (Color)new Color32(222, 120, 110, 255) : new Color32(238, 230, 212, 255);
                    GUI.Label(new Rect(deathsX, rr.y, 52, rr.height), row.deaths.ToString(), number);
                    number.normal.textColor = new Color32(238, 230, 212, 255);
                }

                float by = r.yMax - Bottom + 14;
                if (RpgGuiJuice.Button(new Rect(r.center.x - 90, by, 180, 38), "닫기", theme.Primary)) close = true;
                GUI.Label(new Rect(r.center.x + 100, by, r.xMax - r.center.x - 124, 38), "ESC", theme.Muted);
            }
            finally { GUI.matrix = previous; }
            return close;
        }

        internal static Color ClassColor(int character) => (CharacterClass)character switch
        {
            CharacterClass.Warrior => RpgGuiTheme.Red,
            CharacterClass.Healer => RpgGuiTheme.Blue,
            _ => RpgGuiTheme.Green
        };

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        private static void Badge(Rect rect, string text)
        {
            RpgGuiTheme.Fill(rect, new Color32(23, 19, 15, 255));
            RpgGuiTheme.Fill(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), new Color32(196, 150, 70, 255));
            RpgGuiTheme.Fill(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, 1), new Color32(236, 199, 122, 255));
            Color c = sub.normal.textColor; TextAnchor a = sub.alignment;
            sub.normal.textColor = new Color32(30, 22, 11, 255); sub.alignment = TextAnchor.MiddleCenter;
            GUI.Label(rect, text, sub);
            sub.normal.textColor = c; sub.alignment = a;
        }

        private static void Ensure()
        {
            if (rank != null) return;
            Font body = Resources.Load<Font>("UI/Fonts/Galmuri11");
            Font bold = Resources.Load<Font>("UI/Fonts/Galmuri11-Bold") ?? body;
            GUIStyle Make(Font font, int size, TextAnchor anchor, Color color) => new GUIStyle(GUI.skin.label)
            {
                font = font, fontSize = size, alignment = anchor, wordWrap = false, clipping = TextClipping.Clip,
                padding = new RectOffset(), margin = new RectOffset(), normal = { textColor = color }
            };
            rank = Make(bold, 22, TextAnchor.MiddleCenter, new Color32(190, 180, 160, 255));
            name = Make(bold, 16, TextAnchor.MiddleLeft, new Color32(238, 230, 212, 255));
            sub = Make(body, 12, TextAnchor.MiddleLeft, new Color32(170, 160, 142, 255));
            number = Make(body, 15, TextAnchor.MiddleRight, new Color32(238, 230, 212, 255));
            header = Make(body, 12, TextAnchor.MiddleLeft, new Color32(170, 160, 142, 255));
            barText = Make(bold, 14, TextAnchor.MiddleCenter, new Color32(250, 244, 230, 255));
        }
    }

    // 혼자 하기: 보스가 쓰러지면 결과창을 띄운다. 열려 있는 동안은 캐릭터 조작을 막는다(ItemWindows).
    public sealed class BossResultView : MonoBehaviour
    {
        public static bool IsOpen => current != null;
        private static BossResultView current;
        private BossResultFrame result;
        private float openedAt;
        private RpgGuiTheme theme;

        internal static void ShowOffline(BossResultFrame result)
        {
            if (current != null) Destroy(current.gameObject);
            var root = new GameObject("Boss Result");
            current = root.AddComponent<BossResultView>();
            current.result = result;
            current.openedAt = Time.unscaledTime;
            UiSound.Open();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private void OnGUI()
        {
            if (result == null) return;
            theme ??= new RpgGuiTheme();
            theme.Ensure();
            GUI.depth = -50;
            float scale = Mathf.Max(.5f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
            Matrix4x4 previous = GUI.matrix;
            GUISkin previousSkin = GUI.skin;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.skin = theme.Skin;
            bool close = BossResultWindow.Draw(result, Time.unscaledTime - openedAt, 0, Screen.width / scale, Screen.height / scale, theme);
            GUI.skin = previousSkin;
            GUI.matrix = previous;
            if (close) Close();
        }

        private void Close()
        {
            if (current != this) return;
            UiSound.Close();
            ItemWindows.ConsumeClose();
            current = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
            theme?.Dispose();
        }
    }
}
