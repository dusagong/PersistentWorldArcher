using System;
using System.Linq;
using PersistentWorld.Player;
using PersistentWorld.UI;
using UnityEngine;

namespace PersistentWorld.Networking
{
    [DefaultExecutionOrder(-100)]
    public sealed class NetworkPartyHud : MonoBehaviour
    {
        public static NetworkPartyHud Instance { get; private set; }
        private NetworkGameWorld world;
        private bool menu, context, partyContext;
        private enum MenuPage { Main, Skills, Class, Tutorial }
        private MenuPage page;
        private ulong target;
        private Vector2 contextAt, scroll;
        private GUIStyle playerNameStyle;
        private CombatHud combatHud;
        private static float Scale => Mathf.Max(.5f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
        private static float Width => Screen.width / Scale;
        private static float Height => Screen.height / Scale;
        private static Vector2 Pointer => new Vector2(Input.mousePosition.x / Scale, Height - Input.mousePosition.y / Scale);
        private float CombatTop => combatHud != null ? combatHud.TopScreenY / Scale : Height - 124;
        internal void BindCombatHud(CombatHud value) => combatHud = value;
        private Rect PartyRect => new Rect(12, 12, 240, 65 + (world.Local == null ? 0 : world.Frame.players.Count(p => p.party == world.Local.party && p.party != 0) * 34));
        private Rect InviteRect => new Rect(Mathf.Max(0, Width - 352),
            Mathf.Max(0, CombatTop - 88), Mathf.Min(Width, 340), 80);
        private Rect MenuRect => new Rect(Width / 2f - 300, Height / 2f - 240, 600, 480);
        private Rect ContextRect => new Rect(Mathf.Clamp(contextAt.x, 0, Width - 190), Mathf.Clamp(contextAt.y, 0, Height - 80), 190, 78);
        public bool MenuOpen => menu;
        public bool BlocksKeyboard => menu || context;
        public bool BlocksPointer
        {
            get
            {
                Vector2 point = Pointer;
                return BlocksKeyboard || PartyRect.Contains(point) ||
                    (world.Local?.area == (int)GameArea.Tutorial && new Rect(12, 220, 300, 330).Contains(point)) ||
                    (world.Frame != null && world.Frame.inviteRemaining > 0 && InviteRect.Contains(point));
            }
        }
        private void Awake() { Instance = this; world = GetComponent<NetworkGameWorld>(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void Send(PartyCommand command, ulong id = 0, int first = 0, int second = 0) => NetworkSandboxPlayer.LocalPlayer?.CommandRpc((byte)command, id, first, second);
        private void Update()
        {
            PlayerFrame me = world.Local;
            if (me == null) { menu = context = false; return; }
            if (ItemWindows.BlocksGameplay) return;
            if (Input.GetKeyDown(KeyCode.I) && me.hp > 0 && world.ClientInventory != null)
            {
                menu = context = false; Send(PartyCommand.Menu); GetComponent<InventoryWindow>()?.OpenOnline(); return;
            }
            if (Input.GetKeyDown(KeyCode.K))
            {
                menu = !(menu && page == MenuPage.Skills); page = MenuPage.Skills; context = false;
                Send(PartyCommand.Menu, first: menu ? 1 : 0);
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                menu = !menu;
                page = MenuPage.Main;
                context = false;
                Send(PartyCommand.Menu, first: menu ? 1 : 0);
            }
            Vector2 point = Pointer;
            if (Input.GetMouseButtonDown(1) && !menu)
            {
                if (me.party != 0 && PartyRect.Contains(point)) { context = partyContext = true; contextAt = point; return; }
                PlayerFrame selected = null; float best = 45;
                foreach (PlayerFrame player in world.Frame.players)
                {
                    if (player.id == me.id || player.area != me.area) continue;
                    Vector3 screen = world.gameCamera.WorldToScreenPoint(player.renderedPosition + Vector3.up * .5f) / Scale;
                    float distance = Vector2.Distance(point, new Vector2(screen.x, Height - screen.y));
                    if (distance < best) { selected = player; best = distance; }
                }
                if (selected != null) { target = selected.id; context = true; partyContext = false; contextAt = point; }
            }
            if (Input.GetMouseButtonDown(0) && context && !ContextRect.Contains(point)) context = false;
        }
        private void OnGUI()
        {
            if ((Application.isBatchMode && !NetworkSandbox.HasArgument("-pwa-capture")) || world.Local == null || ItemWindows.BlocksGameplay) return;
            PlayerFrame me = world.Local;
            Matrix4x4 previous = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1));
            if (!menu && !ItemWindows.BlocksGameplay) DrawPlayerNames(me.area);
            GUILayout.BeginArea(PartyRect, GUI.skin.box);
            if (me.party == 0)
            {
                GUILayout.Label("파티 없음"); GUI.enabled = me.area != (int)GameArea.BossArena;
                if (GUILayout.Button("1인 파티 만들기")) Send(PartyCommand.Create); GUI.enabled = true;
            }
            else
            {
                GUILayout.Label("파티 창 · 우클릭으로 탈퇴");
                foreach (PlayerFrame player in world.Frame.players.Where(p => p.party == me.party))
                {
                    GUILayout.Label((player.leader ? "★ " : "") + player.name + "   HP " + player.hp + "/" + player.maximum);
                    Rect bar = GUILayoutUtility.GetRect(210, 5); Bar(bar, player.hp / (float)player.maximum, Color.green);
                }
            }
            GUILayout.EndArea();
            GUI.Label(new Rect(Mathf.Max(0, Width - 284), 12, 272, 25),
                me.name + " · " + SkillCatalog.ClassName((CharacterClass)me.character) + " · " + (GameArea)me.area);
            GUI.Box(new Rect(Width - 284, 38, 272, 90), "WASD 이동 · 우클릭 평타 · 좌클릭 특수\nQ / E / Shift 스킬 · Space 회피\n1 / 2 포션 · F 상호작용\nK 스킬 · I 인벤토리 · ESC 메뉴");
            if (world.Frame.inviteRemaining > 0)
            {
                GUILayout.BeginArea(InviteRect, GUI.skin.box);
                GUILayout.Label(world.Frame.inviterName + "님의 파티 초대 · " + Mathf.CeilToInt(world.Frame.inviteRemaining) + "초");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("수락")) Send(PartyCommand.Accept);
                if (GUILayout.Button("거절")) Send(PartyCommand.Decline);
                GUILayout.EndHorizontal(); GUILayout.EndArea();
            }
            if (context)
            {
                GUILayout.BeginArea(ContextRect, GUI.skin.box);
                if (partyContext)
                {
                    if (GUILayout.Button(me.area == (int)GameArea.BossArena ? "탈퇴하기 · 마을 귀환" : "탈퇴하기")) { Send(PartyCommand.Leave); context = false; }
                }
                else
                {
                    PlayerFrame selected = world.Frame.players.FirstOrDefault(p => p.id == target);
                    GUI.enabled = selected != null && selected.party == 0 && me.area != (int)GameArea.BossArena;
                    if (GUILayout.Button("파티 초대")) { Send(PartyCommand.Invite, target); context = false; }
                    GUI.enabled = true;
                }
                if (GUILayout.Button("닫기")) context = false;
                GUILayout.EndArea();
            }
            if (!string.IsNullOrEmpty(world.Frame.message)) GUI.Box(new Rect(Width / 2f - 300, 18, 600, 45), world.Frame.message);
            if (me.area == (int)GameArea.BossArena && world.Frame.bossMax > 0)
            {
                float width = Mathf.Min(360, Width - 24);
                var bossBar = new Rect((Width - width) / 2, 70, width, 35);
                GUI.Box(bossBar, $"보스 HP {world.Frame.bossHp}/{world.Frame.bossMax}");
                Bar(new Rect(bossBar.x + 8, bossBar.y + 23, bossBar.width - 16, 7),
                    world.Frame.bossHp / (float)world.Frame.bossMax, new Color(.82f, .18f, .24f));
            }
            if (me.area == (int)GameArea.BossArena && !string.IsNullOrEmpty(world.Frame.bossNotice)) GUI.Box(new Rect(Width / 2f - 240, 110, 480, 70), world.Frame.bossNotice);
            if (me.hp == 0) GUI.Box(new Rect(Width / 2f - 200, Height / 2f - 35, 400, 70), "사망\n" + (me.area == (int)GameArea.BossArena ? "보스전 종료 또는 힐러 부활까지 대기" : "10초 후 마을 귀환"));
            if (!menu && !ItemWindows.BlocksGameplay && !string.IsNullOrEmpty(world.Frame.prompt))
                GUI.Box(new Rect(Width / 2f - 200, CombatTop - 40, 400, 32), world.Frame.prompt);
            if (!menu && me.area == (int)GameArea.Tutorial)
            {
                string Check(int flag) => (world.Frame.tutorial & flag) != 0 ? "완료" : "대기";
                GUI.Box(new Rect(12, 220, 300, 250), "조작 연습 · 피해와 아이템 소비 없음\n" +
                    $"WASD 이동 [{Check(1)}]\n우클릭 평타 [{Check(2)}]\nSpace 회피 [{Check(4)}]\nQ / E 스킬 조준 후 놓기 [{Check(8)}]\n좌클릭 직업 특수 [{Check(16)}]\n앞의 표적을 공격해보세요.");
                if ((CharacterClass)me.character == CharacterClass.Healer)
                    GUI.Label(new Rect(25, 380, 275, 75), $"회복 대상에 힐링 존 [{Check(32)}]\n보라색 대상에 정화 [{Check(64)}]\n쓰러진 대상 옆에서 좌클릭 [{Check(128)}]");
                if (world.Frame.practice != null) foreach (PracticeFrame practice in world.Frame.practice)
                {
                    Vector3 rendered = world.RenderedPosition(practice.visual, practice.position);
                    Vector3 at = world.gameCamera.WorldToScreenPoint(rendered + Vector3.up * .9f) / Scale;
                    string label = practice.kind == 0 ? "회복 연습" : practice.kind == 1 ? "정화 연습" : "부활 연습";
                    GUI.Box(new Rect(at.x - 63, Height - at.y - 18, 126, 38), label + (practice.complete ? " · 완료" : "") + $"\nHP {practice.hp}/{practice.maximum}");
                }
                if (GUI.Button(new Rect(22, 479, 280, 30), "연습 초기화 · HP / MP / 쿨타임")) Send(PartyCommand.ResetPractice);
                if (GUI.Button(new Rect(22, 516, 280, 30), "연습 종료 · 마을로")) Send(PartyCommand.Tutorial);
            }
            if (menu) DrawMenu(me);
            GUI.matrix = previous;
        }

        private void DrawPlayerNames(int area)
        {
            playerNameStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                richText = false
            };
            foreach (PlayerFrame player in world.Frame.players)
            {
                if (player.area != area || player.hp <= 0 || string.IsNullOrEmpty(player.name)) continue;
                Vector3 screen = world.gameCamera.WorldToScreenPoint(player.renderedPosition + Vector3.up * .8f) / Scale;
                if (screen.z <= 0 || screen.x < 0 || screen.x > Width || screen.y < 0 || screen.y > Height) continue;
                float width = Mathf.Max(40, playerNameStyle.CalcSize(new GUIContent(player.name)).x + 8);
                GUI.Box(new Rect(screen.x - width / 2, Height - screen.y - 19, width, 19), player.name, playerNameStyle);
                if (world.Local.party != 0 && player.party == world.Local.party)
                {
                    Rect bar = new Rect(screen.x - 40, Height - screen.y + 2, 80, 13);
                    Bar(bar, player.hp / (float)Mathf.Max(1, player.maximum), new Color(.25f, .85f, .35f));
                    GUI.Label(bar, $"{player.hp}/{player.maximum}", new GUIStyle(playerNameStyle) { fontSize = 10, normal = { background = null } });
                }
            }
        }
        private void DrawMenu(PlayerFrame me)
        {
            GUILayout.BeginArea(MenuRect, GUI.skin.box);
            if (page == MenuPage.Main)
            {
                if (GUILayout.Button("장비 · 인벤토리") && me.hp > 0 && world.ClientInventory != null)
                {
                    menu = false;
                    Send(PartyCommand.Menu);
                    GetComponent<InventoryWindow>()?.OpenOnline();
                }
                if (GUILayout.Button("스킬")) { page = MenuPage.Skills; scroll = Vector2.zero; }
                if (GUILayout.Button("직업 선택")) { page = MenuPage.Class; scroll = Vector2.zero; }
                if (GUILayout.Button("튜토리얼 · 조작 연습")) page = MenuPage.Tutorial;
                NetworkSandbox login = GetComponent<NetworkSandbox>();
                GUILayout.Label(login.IsDemo ? "일회성 데모 계정 · 로그아웃 시 진행 삭제" : "플레이어 코드로 진행 내용이 서버에 저장됩니다.");
                if (!login.IsDemo && GUILayout.Button("내 플레이어 코드 복사")) GUIUtility.systemCopyBuffer = login.PlayerCode;
                GameAudio.DrawSettings();
                if (GUILayout.Button("로그아웃")) { menu = context = false; login.Logout(); }
                if (GUILayout.Button("닫기")) { menu = false; Send(PartyCommand.Menu); }
                GUILayout.EndArea();
                return;
            }
            if (page == MenuPage.Tutorial)
            {
                GUILayout.Label("조작 튜토리얼", new GUIStyle(GUI.skin.label) { fontSize = 22 });
                GUILayout.Label("WASD로 이동하고 마우스로 조준합니다.\n우클릭: 평타 / 좌클릭: 직업 특수 스킬\nSpace: 회피 / Q·E·Shift: 장착한 스킬\n차징·범위 스킬은 키를 누른 채 조준하고 놓으면 사용됩니다.\n1·2: 장착한 포션 (각 10초 쿨타임)\nF: 가까운 NPC·포탈 상호작용\nK: 스킬 / I: 인벤토리 / ESC: 메뉴\n다른 플레이어 우클릭: 파티 초대\n파티 창 우클릭: 파티 탈퇴\n힐러 부활은 사망한 아군이 가까이 있어야 사용할 수 있습니다.");
                GUI.enabled = me.hp > 0 && me.area != (int)GameArea.BossArena;
                if (GUILayout.Button(me.area == (int)GameArea.Tutorial ? "연습 종료" : "안전한 연습장에서 직접 해보기", GUILayout.Height(40)))
                { menu = false; Send(PartyCommand.Menu); Send(PartyCommand.Tutorial); }
                GUI.enabled = true;
                if (GUILayout.Button("뒤로")) page = MenuPage.Main;
                GUILayout.EndArea(); return;
            }
            GUILayout.Label(page == MenuPage.Skills ? "스킬" : "직업 선택");
            bool allowed = me.area == (int)GameArea.Town && me.hp > 0;
            scroll = GUILayout.BeginScrollView(scroll); GUI.enabled = allowed;
            if (page == MenuPage.Skills) DrawSkills(me);
            else
            {
                foreach (CharacterClass type in Enum.GetValues(typeof(CharacterClass)))
                    if (GUILayout.Button(SkillCatalog.ClassName(type) + "로 변경")) Send(PartyCommand.ChangeClass, first: (int)type);
            }
            GUI.enabled = true; GUILayout.EndScrollView();
            if (!allowed) GUILayout.Label("스킬 교체와 직업 변경은 마을에서 할 수 있습니다.");
            if (GUILayout.Button("뒤로")) page = MenuPage.Main;
            if (GUILayout.Button("닫기")) { menu = false; Send(PartyCommand.Menu); }
            GUILayout.EndArea();
        }
        private void DrawSkills(PlayerFrame me)
        {
            if (!me.bossLearned && me.books > 0 && GUILayout.Button("보스 스킬 북 사용")) Send(PartyCommand.LearnBossSkill);
            if (me.bossLearned && GUILayout.Button("Shift · 보스 스킬 장착")) Send(PartyCommand.Equip, first: 2, second: (int)SkillId.BossBlast);
            for (int i = 0; i < me.slots.Length; i++)
                if (GUILayout.Button((i == 0 ? "Q " : i == 1 ? "E " : "Shift ") + SkillCatalog.Name((SkillId)me.slots[i]) + " 해제")) Send(PartyCommand.Equip, first: i, second: 0);
            foreach (SkillId skill in SkillCatalog.ForClass((CharacterClass)me.character))
            {
                GUILayout.Label(SkillCatalog.Name(skill) + " · " + SkillCatalog.Description(skill)); GUILayout.BeginHorizontal();
                if (GUILayout.Button("Q 장착")) Send(PartyCommand.Equip, first: 0, second: (int)skill);
                if (GUILayout.Button("E 장착")) Send(PartyCommand.Equip, first: 1, second: (int)skill);
                GUILayout.EndHorizontal();
            }
        }
        private static void Bar(Rect rect, float ratio, Color color)
        {
            Color previous = GUI.color; GUI.color = Color.black; GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color; GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height), Texture2D.whiteTexture); GUI.color = previous;
        }
    }
}
