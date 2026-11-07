using System;
using System.Linq;
using PersistentWorld.Player;
using UnityEngine;

namespace PersistentWorld.Networking
{
    [DefaultExecutionOrder(-100)]
    public sealed class NetworkPartyHud : MonoBehaviour
    {
        public static NetworkPartyHud Instance { get; private set; }
        private NetworkGameWorld world;
        private bool menu, context, partyContext;
        private enum MenuPage { Main, Skills, Class }
        private MenuPage page;
        private ulong target;
        private Vector2 contextAt, scroll;
        private Rect PartyRect => new Rect(12, 12, 240, 65 + (world.Local == null ? 0 : world.Frame.players.Count(p => p.party == world.Local.party && p.party != 0) * 34));
        private Rect HealthRect => new Rect(Screen.width - 332, Screen.height - 124, 320, 112);
        private Rect InviteRect => new Rect(Screen.width - 352, Screen.height - 212, 340, 80);
        private Rect MenuRect => new Rect(Screen.width / 2f - 300, Screen.height / 2f - 240, 600, 480);
        private Rect ContextRect => new Rect(Mathf.Clamp(contextAt.x, 0, Screen.width - 190), Mathf.Clamp(contextAt.y, 0, Screen.height - 80), 190, 78);
        public bool BlocksInput
        {
            get
            {
                Vector2 point = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                return menu || context || PartyRect.Contains(point) || HealthRect.Contains(point) ||
                    (world.Frame != null && world.Frame.inviteRemaining > 0 && InviteRect.Contains(point));
            }
        }
        private void Awake() { Instance = this; world = GetComponent<NetworkGameWorld>(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void Send(PartyCommand command, ulong id = 0, int first = 0, int second = 0) => NetworkSandboxPlayer.LocalPlayer?.CommandRpc((byte)command, id, first, second);
        private void Update()
        {
            PlayerFrame me = world.Local;
            if (me == null) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                menu = !menu;
                page = MenuPage.Main;
                context = false;
                Send(PartyCommand.Menu, first: menu ? 1 : 0);
            }
            Vector2 point = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (Input.GetMouseButtonDown(1) && !menu)
            {
                if (me.party != 0 && PartyRect.Contains(point)) { context = partyContext = true; contextAt = point; return; }
                PlayerFrame selected = null; float best = 45;
                foreach (PlayerFrame player in world.Frame.players)
                {
                    if (player.id == me.id || player.area != me.area) continue;
                    Vector3 screen = world.gameCamera.WorldToScreenPoint(player.position + Vector2.up * .5f);
                    float distance = Vector2.Distance(point, new Vector2(screen.x, Screen.height - screen.y));
                    if (distance < best) { selected = player; best = distance; }
                }
                if (selected != null) { target = selected.id; context = true; partyContext = false; contextAt = point; }
            }
            if (Input.GetMouseButtonDown(0) && context && !ContextRect.Contains(point)) context = false;
        }
        private void OnGUI()
        {
            if ((Application.isBatchMode && !NetworkSandbox.HasArgument("-pwa-capture")) || world.Local == null) return;
            PlayerFrame me = world.Local;
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
            GUILayout.BeginArea(HealthRect, GUI.skin.box);
            GUILayout.Label(me.name + " · " + SkillCatalog.ClassName((CharacterClass)me.character) + " · " + (GameArea)me.area);
            GUILayout.Label("HP " + me.hp + "/" + me.maximum + "  MP " + me.mp.ToString("0"));
            Rect hp = GUILayoutUtility.GetRect(0, 8, GUILayout.ExpandWidth(true)); Bar(hp, me.hp / (float)me.maximum, Color.red);
            Rect mp = GUILayoutUtility.GetRect(0, 5, GUILayout.ExpandWidth(true)); Bar(mp, me.mp / 100, Color.cyan);
            GUILayout.Label("Q " + SkillCatalog.Name((SkillId)me.slots[0]) + "  E " + SkillCatalog.Name((SkillId)me.slots[1]) + " | F 상호작용 · ESC 스킬/직업");
            GUILayout.EndArea();
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
            if (!string.IsNullOrEmpty(world.Frame.message)) GUI.Box(new Rect(Screen.width / 2f - 300, 18, 600, 45), world.Frame.message);
            if (me.area == (int)GameArea.BossArena) GUI.Box(new Rect(Screen.width / 2f - 180, 70, 360, 35), "보스 HP " + world.Frame.bossHp + "/" + world.Frame.bossMax);
            if (me.area == (int)GameArea.BossArena && !string.IsNullOrEmpty(world.Frame.bossNotice)) GUI.Box(new Rect(Screen.width / 2f - 240, 110, 480, 70), world.Frame.bossNotice);
            if (me.hp == 0) GUI.Box(new Rect(Screen.width / 2f - 200, Screen.height / 2f - 35, 400, 70), "사망\n" + (me.area == (int)GameArea.BossArena ? "보스전 종료 또는 힐러 부활까지 대기" : "10초 후 마을 귀환"));
            if (menu) DrawMenu(me);
        }
        private void DrawMenu(PlayerFrame me)
        {
            GUILayout.BeginArea(MenuRect, GUI.skin.box);
            if (page == MenuPage.Main)
            {
                if (GUILayout.Button("스킬")) { page = MenuPage.Skills; scroll = Vector2.zero; }
                if (GUILayout.Button("직업 선택")) { page = MenuPage.Class; scroll = Vector2.zero; }
                if (GUILayout.Button("닫기")) { menu = false; Send(PartyCommand.Menu); }
                GUILayout.EndArea();
                return;
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
