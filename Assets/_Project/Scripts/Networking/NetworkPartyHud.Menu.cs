using PersistentWorld.Player;
using UnityEngine;

namespace PersistentWorld.Networking
{
    public sealed partial class NetworkPartyHud
    {
        private readonly RpgGuiTheme menuTheme = new RpgGuiTheme();
        private Vector2 menuScroll;
        // 메뉴가 열리거나 페이지가 바뀔 때 통통 튀며 나타나는 연출
        private float menuOpenedAt = -10f, menuPopFrom = .9f;
        private int menuDrawnFrame = -10;
        private MenuPage poppedPage;
        private void ReleaseMenuStyle() => menuTheme.Dispose();
        private void CloseMenu() { menu = false; Send(PartyCommand.Menu); PersistentWorld.UI.UiSound.Close(); }
        private void OpenMenuPage(MenuPage value) { page = value; scroll = menuScroll = Vector2.zero; }
        private static string AccountTitle(bool demo) => demo ? "일회성 데모 계정" : "플레이어 계정";
        private static string AccountNotice(bool demo) => demo ? "로그아웃 시 진행 내용이 삭제됩니다." : "플레이어 코드로 진행 내용이 서버에 저장됩니다.";

        private void DrawNotice(string text, float y, float preferredWidth, Color accent, bool bottomAligned = false)
        {
            float width = Mathf.Min(preferredWidth, Width - 32);
            float height = Mathf.Ceil(menuTheme.Notice.CalcHeight(new GUIContent(text), width - 32)) + 24;
            var rect = new Rect((Width - width) / 2, bottomAligned ? y - height : y, width, height);
            RpgGuiTheme.Panel(rect, accent);
            GUI.Label(new Rect(rect.x + 16, rect.y + 12, width - 32, height - 24), text, menuTheme.Notice);
        }

        private static void DrawHealthGauge(Rect rect, float ratio)
        {
            RpgGuiTheme.Fill(rect, new Color32(14, 24, 20, 255));
            var inside = new Rect(rect.x + 1, rect.y + 1, Mathf.Max(0, rect.width - 2) * Mathf.Clamp01(ratio), Mathf.Max(0, rect.height - 2));
            RpgGuiTheme.Fill(inside, new Color32(101, 166, 125, 255));
            RpgGuiTheme.Border(rect, new Color32(88, 112, 93, 255));
        }

        private void DrawPartyPanel(PlayerFrame me)
        {
            Rect rect = PartyRect;
            RpgGuiTheme.Panel(rect, RpgGuiTheme.Green);
            GUI.BeginGroup(rect);
            GUI.Label(new Rect(14, 14, rect.width - 28, 22), me.party == 0 ? "파티 없음" : "파티 · 우클릭으로 탈퇴", menuTheme.SmallLeft);
            if (me.party == 0)
            {
                GUI.enabled = me.area != (int)GameArea.BossArena;
                if (RpgGuiJuice.Button(new Rect(12, 39, rect.width - 24, 30), "1인 파티 만들기", menuTheme.SmallButton)) Send(PartyCommand.Create);
                GUI.enabled = true;
            }
            else
            {
                int row = 0;
                foreach (PlayerFrame member in world.Frame.players)
                {
                    if (member.party != me.party) continue;
                    float top = 38 + row++ * 42;
                    string name = (member.leader ? "★ " : "") + member.name;
                    float nameWidth = rect.width - 120;
                    while (name.Length > 1 && menuTheme.SmallLeft.CalcSize(new GUIContent(name)).x > nameWidth)
                        name = name.Substring(0, name.Length - (name.EndsWith("…") ? 2 : 1)) + "…";
                    GUI.Label(new Rect(12, top, nameWidth, 21), name, menuTheme.SmallLeft);
                    GUI.Label(new Rect(rect.width - 104, top, 92, 21), $"{member.hp}/{member.maximum}", menuTheme.Muted);
                    DrawHealthGauge(new Rect(12, top + 26, rect.width - 24, 5), member.hp / (float)Mathf.Max(1, member.maximum));
                }
            }
            GUI.EndGroup();
        }

        // ---- 오른쪽 위 조작키 안내: 접으면 이름 줄만, 펼치면 키캡 모양으로 조작키를 보여준다 (H 또는 클릭) ----
        private const string GuidePref = "pwa.controlGuideOpen";
        private const float GuideWidth = 300, GuideHeader = 44, GuideRow = 28;
        // 왼쪽 칸 / 오른쪽 칸: 누르는 키들, 설명
        private static readonly (string[] keys, string text)[,] GuideRows =
        {
            { (new[] { "W", "A", "S", "D" }, "이동"), (new[] { "Space" }, "회피") },
            { (new[] { "우클릭" }, "평타"), (new[] { "좌클릭" }, "특수") },
            { (new[] { "Q", "E", "Shift" }, "스킬"), (new[] { "F" }, "상호작용") },
            { (new[] { "1", "2" }, "포션"), (new[] { "K" }, "스킬창") },
            { (new[] { "I" }, "인벤토리"), (new[] { "ESC" }, "메뉴") },
        };
        private bool? guideOpen;
        private GUIStyle keyCapStyle, guideTextStyle, guideTitleStyle;

        private bool GuideOpen
        {
            get
            {
                if (guideOpen == null) guideOpen = PlayerPrefs.GetInt(GuidePref, 1) == 1; // 처음엔 펼쳐서 보여준다
                return guideOpen.Value;
            }
        }
        private Rect GuideRect => new Rect(Mathf.Max(12, Width - GuideWidth - 12), 12, GuideWidth,
            GuideOpen ? GuideHeader + GuideRows.GetLength(0) * GuideRow + 14 : GuideHeader);

        private void ToggleGuide()
        {
            guideOpen = !GuideOpen;
            PlayerPrefs.SetInt(GuidePref, guideOpen.Value ? 1 : 0);
            if (guideOpen.Value) PersistentWorld.UI.UiSound.Open(); else PersistentWorld.UI.UiSound.Close();
        }

        private static string AreaTitle(GameArea area) => area switch
        {
            GameArea.Town => "마을",
            GameArea.Field => "필드",
            GameArea.Field2 => "두 번째 필드",
            GameArea.BossArena => "보스방",
            GameArea.Tutorial => "튜토리얼",
            _ => area.ToString(),
        };

        private void DrawControlGuide(PlayerFrame me)
        {
            EnsureGuideStyles();
            Rect rect = GuideRect;
            bool open = GuideOpen;
            RpgGuiTheme.Panel(rect, RpgGuiTheme.Blue);

            // 머리 줄: 이름 · 직업 · 지역 + 오른쪽에 [H] ▾/▴ (머리 줄 아무 데나 눌러도 접고 펼친다)
            var header = new Rect(rect.x, rect.y, rect.width, GuideHeader);
            float hintWidth = KeyCapWidth("H") + 18;
            GUI.Label(new Rect(rect.x + 14, rect.y + 11, rect.width - 28 - hintWidth, 22),
                me.name + " · " + SkillCatalog.ClassName((CharacterClass)me.character) + " · " + AreaTitle((GameArea)me.area), guideTitleStyle);
            bool hover = header.Contains(Event.current.mousePosition);
            float hx = rect.xMax - 14 - hintWidth;
            DrawKeyCap(new Vector2(hx, rect.y + 11), "H", hover);
            GUI.Label(new Rect(hx + KeyCapWidth("H") + 4, rect.y + 11, 16, 22), open ? "▴" : "▾", guideTextStyle);
            if (!menu && GUI.Button(header, GUIContent.none, GUIStyle.none)) ToggleGuide();
            if (!open) return;

            RpgGuiTheme.Divider(new Rect(rect.x + 14, rect.y + GuideHeader - 4, rect.width - 28, 5), RpgGuiTheme.Blue);
            for (int row = 0; row < GuideRows.GetLength(0); row++)
            {
                float y = rect.y + GuideHeader + 6 + row * GuideRow;
                DrawGuideEntry(new Vector2(rect.x + 14, y), GuideRows[row, 0]);
                DrawGuideEntry(new Vector2(rect.x + 164, y), GuideRows[row, 1]);
            }
        }

        private void DrawGuideEntry(Vector2 at, (string[] keys, string text) entry)
        {
            float x = at.x;
            foreach (string key in entry.keys) x += DrawKeyCap(new Vector2(x, at.y), key, false) + 2;
            GUI.Label(new Rect(x + 4, at.y, 120, 22), entry.text, guideTextStyle);
        }

        private float KeyCapWidth(string key) => Mathf.Max(22, Mathf.Ceil(keyCapStyle.CalcSize(new GUIContent(key)).x) + 10);

        // 키보드 키 모양: 검은 테두리 + 밝은 윗면 + 아래쪽 두꺼운 그림자 턱
        private float DrawKeyCap(Vector2 at, string key, bool lit)
        {
            float w = KeyCapWidth(key);
            var r = new Rect(at.x, at.y, w, 22);
            RpgGuiTheme.Fill(r, new Color32(23, 19, 15, 255));
            RpgGuiTheme.Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), new Color32(120, 112, 100, 255)); // 턱
            Color top = lit ? (Color)new Color32(236, 214, 160, 255) : new Color32(204, 194, 172, 255);
            RpgGuiTheme.Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 5), top);
            RpgGuiTheme.Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, 1), new Color(1, 1, 1, .35f)); // 윗면 반짝
            GUI.Label(new Rect(r.x, r.y, r.width, r.height - 3), key, keyCapStyle);
            return w;
        }

        private void EnsureGuideStyles()
        {
            if (keyCapStyle != null && keyCapStyle.font == menuTheme.SmallLeft.font) return;
            keyCapStyle = new GUIStyle(menuTheme.SmallLeft) { fontSize = 12, alignment = TextAnchor.MiddleCenter, normal = { textColor = RpgGuiTheme.Ink } };
            guideTextStyle = new GUIStyle(menuTheme.SmallLeft) { fontSize = 13, normal = { textColor = new Color32(214, 204, 184, 255) } };
            guideTitleStyle = new GUIStyle(menuTheme.SmallLeft) { fontSize = 14, normal = { textColor = RpgGuiTheme.Text } };
        }

        private void DrawMenu(PlayerFrame me)
        {
            menuTheme.Ensure();
            NetworkSandbox login = GetComponent<NetworkSandbox>();
            bool main = page == MenuPage.Main, demo = login.IsDemo;
            float scale = RpgGuiTheme.ScaleFor(Screen.width, Screen.height);
            Rect bounds = RpgGuiTheme.Bounds(Screen.width, Screen.height, main ? 400 : 620, main ? (demo ? 560 : 600) : 560);
            if (Time.frameCount - menuDrawnFrame > 2) { menuOpenedAt = Time.unscaledTime; menuPopFrom = .9f; poppedPage = page; PersistentWorld.UI.UiSound.Open(); }
            else if (page != poppedPage) { menuOpenedAt = Time.unscaledTime; menuPopFrom = .96f; poppedPage = page; }
            menuDrawnFrame = Time.frameCount;
            float popAge = Time.unscaledTime - menuOpenedAt;
            Matrix4x4 previousMatrix = GUI.matrix; GUISkin previousSkin = GUI.skin;
            bool previousEnabled = GUI.enabled;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1)); GUI.skin = menuTheme.Skin;
            try
            {
                RpgGuiTheme.Fill(new Rect(0, 0, Screen.width / scale, Screen.height / scale), new Color(0, 0, 0, .36f));
                GUI.matrix *= RpgGuiJuice.Pop(bounds.center, popAge, menuPopFrom);
                GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * RpgGuiJuice.FadeIn(popAge));
                Color accent = page == MenuPage.Skills ? RpgGuiTheme.Blue : page == MenuPage.Class ? RpgGuiTheme.Green : RpgGuiTheme.Gold;
                RpgGuiTheme.Panel(bounds, accent);
                GUI.BeginGroup(bounds);
                string heading = main ? "메뉴" : page == MenuPage.Skills ? "스킬" : page == MenuPage.Class ? "직업 선택" : "조작 튜토리얼";
                GUI.Label(new Rect(42, 13, bounds.width - 84, 38), heading, menuTheme.Heading);
                RpgGuiTheme.Fill(new Rect(bounds.width / 2 - Mathf.Min(82, bounds.width / 3), 29, 4, 4), accent);
                RpgGuiTheme.Fill(new Rect(bounds.width / 2 + Mathf.Min(78, bounds.width / 3), 29, 4, 4), accent);
                Rect close = new Rect(bounds.width - 48, 12, 32, 32);
                if (RpgGuiJuice.Button(close, GUIContent.none, menuTheme.CloseButton)) CloseMenu();
                for (int pixel = -4; pixel <= 4; pixel++)
                {
                    RpgGuiTheme.Fill(new Rect(close.center.x + pixel - 1, close.center.y + pixel - 1, 2, 2), RpgGuiTheme.Text);
                    RpgGuiTheme.Fill(new Rect(close.center.x + pixel - 1, close.center.y - pixel - 1, 2, 2), RpgGuiTheme.Text);
                }
                GUILayout.BeginArea(new Rect(22, 62, bounds.width - 44, Mathf.Max(1, bounds.height - 142)));
                menuScroll = GUILayout.BeginScrollView(menuScroll, false, false);
                if (main) DrawMainMenu(login);
                else DrawMenuPage(me);
                GUILayout.EndScrollView(); GUILayout.EndArea();
                float footer = bounds.height - 72;
                if (main)
                {
                    if (RpgGuiJuice.Button(new Rect(22, footer, 112, 38), "로그아웃")) { menu = context = false; login.Logout(); }
                    if (RpgGuiJuice.Button(new Rect(146, footer, bounds.width - 168, 38), "게임으로 돌아가기", menuTheme.Primary)) CloseMenu();
                }
                else
                {
                    if (RpgGuiJuice.Button(new Rect(22, footer, 112, 38), "뒤로")) OpenMenuPage(MenuPage.Main);
                    if (RpgGuiJuice.Button(new Rect(146, footer, bounds.width - 168, 38), "게임으로 돌아가기", menuTheme.Primary)) CloseMenu();
                }
                GUI.Label(new Rect(22, bounds.height - 30, bounds.width - 44, 20), "ESC 닫기", menuTheme.Muted);
                GUI.EndGroup();
            }
            finally { GUI.enabled = previousEnabled; GUI.skin = previousSkin; GUI.matrix = previousMatrix; GUI.color = previousColor; }
        }

        private void DrawMainMenu(NetworkSandbox login)
        {
            if (RpgGuiJuice.LayoutButton("장비 · 인벤토리") && world.Local.hp > 0 && world.ClientInventory != null)
            { menu = false; Send(PartyCommand.Menu); GetComponent<PersistentWorld.UI.InventoryWindow>()?.OpenOnline(); }
            if (RpgGuiJuice.LayoutButton("스킬")) OpenMenuPage(MenuPage.Skills);
            if (RpgGuiJuice.LayoutButton("직업 선택")) OpenMenuPage(MenuPage.Class);
            if (RpgGuiJuice.LayoutButton("튜토리얼 · 조작 연습")) OpenMenuPage(MenuPage.Tutorial);
            GUILayout.Space(8);
            RpgGuiTheme.Divider(GUILayoutUtility.GetRect(1, 5, GUILayout.ExpandWidth(true)), RpgGuiTheme.Gold);
            GUILayout.Space(8); GUILayout.Label("사운드");
            float value = VolumeControl("BGM", GameAudio.MusicVolume);
            if (!Mathf.Approximately(value, GameAudio.MusicVolume)) GameAudio.MusicVolume = value;
            value = VolumeControl("효과음", GameAudio.EffectVolume);
            if (!Mathf.Approximately(value, GameAudio.EffectVolume)) GameAudio.EffectVolume = value;
            GUILayout.Space(6);
            RpgGuiTheme.Divider(GUILayoutUtility.GetRect(1, 5, GUILayout.ExpandWidth(true)), RpgGuiTheme.Gold);
            GUILayout.Space(8);
            GUILayout.Label(AccountTitle(login.IsDemo), menuTheme.Center);
            GUILayout.Label(AccountNotice(login.IsDemo), menuTheme.Muted);
            if (!login.IsDemo && RpgGuiJuice.LayoutButton("내 플레이어 코드 복사", menuTheme.SmallButton)) GUIUtility.systemCopyBuffer = login.PlayerCode;
        }

        private float VolumeControl(string label, float value)
        {
            Rect line = GUILayoutUtility.GetRect(1, 24, GUILayout.ExpandWidth(true));
            GUI.Label(new Rect(line.x, line.y, line.width - 56, line.height), label);
            GUI.Label(new Rect(line.xMax - 56, line.y, 56, line.height), $"{value * 100:0}%", menuTheme.Center);
            Rect track = GUILayoutUtility.GetRect(1, 20, GUILayout.ExpandWidth(true));
            RpgGuiTheme.Fill(new Rect(track.x, track.y + 3, track.width, 9), new Color32(20, 20, 20, 255));
            RpgGuiTheme.Border(new Rect(track.x, track.y + 3, track.width, 9), new Color32(100, 93, 78, 255));
            RpgGuiTheme.Fill(new Rect(track.x + 4, track.y + 5, (track.width - 8) * value, 5), RpgGuiTheme.Gold);
            return GUI.HorizontalSlider(track, value, 0, 1);
        }

        private void DrawMenuPage(PlayerFrame me)
        {
            if (page == MenuPage.Tutorial)
            {
                GUILayout.Label("WASD로 이동하고 마우스로 조준합니다.\n우클릭: 평타 / 좌클릭: 직업 특수 스킬\nSpace: 회피 / Q·E·Shift: 장착한 스킬\n차징·범위 스킬은 키를 누른 채 조준하고 놓으면 사용됩니다.\n1·2: 장착한 포션 (각 10초 쿨타임)\nF: 가까운 NPC·포탈 상호작용\nK: 스킬 / I: 인벤토리 / ESC: 메뉴\n다른 플레이어 우클릭: 파티 초대\n파티 창 우클릭: 파티 탈퇴\n힐러 부활은 사망한 아군이 가까이 있어야 사용할 수 있습니다.");
                GUILayout.Space(14);
                GUI.enabled = me.hp > 0 && me.area != (int)GameArea.BossArena;
                if (RpgGuiJuice.LayoutButton(me.area == (int)GameArea.Tutorial ? "연습 종료" : "안전한 연습장에서 직접 해보기"))
                { menu = false; Send(PartyCommand.Menu); Send(PartyCommand.Tutorial); }
                GUI.enabled = true; return;
            }
            bool allowed = me.area == (int)GameArea.Town && me.hp > 0;
            if (!allowed) GUILayout.Label("스킬 교체와 직업 변경은 마을에서 할 수 있습니다.", menuTheme.Muted);
            GUI.enabled = allowed;
            if (page == MenuPage.Skills) DrawSkills(me);
            else
                foreach (CharacterClass type in System.Enum.GetValues(typeof(CharacterClass)))
                {
                    GUILayout.BeginVertical(menuTheme.Card);
                    GUILayout.Label(SkillCatalog.ClassName(type), menuTheme.Center);
                    if (RpgGuiJuice.LayoutButton(SkillCatalog.ClassName(type) + "로 변경")) Send(PartyCommand.ChangeClass, first: (int)type);
                    GUILayout.EndVertical(); GUILayout.Space(8);
                }
            GUI.enabled = true;
        }
    }
}
