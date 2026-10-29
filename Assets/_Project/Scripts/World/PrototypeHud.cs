using PersistentWorld.Player;
using PersistentWorld.Items;
using PersistentWorld.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersistentWorld.World
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        private ArcherController player;
        private int tab;
        private Vector2 scroll;
        private string message = "";
        private static readonly string[] Keys = { "Q", "E", "Shift" };

        private void OnGUI()
        {
            if (ItemWindows.BlocksGameplay) return;
            if (player == null) player = FindFirstObjectByType<ArcherController>();
            if (player == null) return;
            SkillLoadout loadout = player.Loadout;
            GUI.Box(new Rect(16, 16, 320, 165), SkillCatalog.ClassName(loadout.Class));
            GUI.Label(new Rect(30, 45, 290, 22), $"HP {player.Health.Current} / {player.Health.Maximum}");
            for (int i = 0; i < 3; i++)
                GUI.Label(new Rect(30, 67 + i * 22, 290, 22), $"{Keys[i]} {SkillCatalog.Name(loadout.Slots[i])}  {Cooldown(player.Skills.Cooldown(loadout.Slots[i]))}");
            GUI.Label(new Rect(30, 133, 290, 22), $"Space 회피 {Cooldown(player.BackstepRemaining)} | 좌클릭 {Cooldown(player.Skills.SpecialRemaining)}");
            GUI.Label(new Rect(Screen.width - 370, 18, 360, 90), "WASD 이동 | 우클릭 평타 | 좌클릭 직업 특수\nQ/E 장착 스킬 | Space 회피 | Shift 보스 스킬\nESC 메뉴 | F 상호작용");
            if (player.Health.IsDead)
            {
                GUI.Box(new Rect(Screen.width / 2f - 240, Screen.height / 2f - 65, 480, 130), "사망");
                string status = player.WaitingForBoss ? "보스전 종료까지 대기 중 · 힐러가 부활시킬 수 있습니다." : $"{Mathf.CeilToInt(player.ReturnRemaining)}초 뒤 마을로 귀환합니다.";
                GUI.Label(new Rect(Screen.width / 2f - 220, Screen.height / 2f - 10, 450, 60), status);
                return;
            }
            if (player.Skills.IsHolding)
                GUI.Box(new Rect(Screen.width / 2f - 180, Screen.height - 110, 360, 36), $"{SkillCatalog.Name(player.Skills.HoldingSkill)} · 키를 놓으면 발동  {player.Skills.Charge:P0}");
            if (player.MenuOpen) { DrawMenu(); return; }
            if (!string.IsNullOrEmpty(player.NearbyPrompt))
                GUI.Box(new Rect(Screen.width / 2f - 170, Screen.height - 65, 340, 36), player.NearbyPrompt);
            if (!string.IsNullOrEmpty(DialogueState.Line))
                GUI.Box(new Rect(Screen.width / 2f - 270, Screen.height - 160, 540, 80), DialogueState.Speaker + "\n" + DialogueState.Line);
        }

        private void DrawMenu()
        {
            float scale = Mathf.Min(1, Screen.width / 760f, Screen.height / 580f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 720 * scale) / 2, (Screen.height - 540 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            GUILayout.BeginArea(new Rect(0, 0, 720, 540), GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("캐릭터 메뉴", GUILayout.Height(30));
            if (GUILayout.Button("장비 · 인벤토리", GUILayout.Width(150)))
                ItemSession.Instance?.GetComponent<InventoryWindow>()?.Open(player.gameObject);
            if (GUILayout.Button("닫기 (ESC)", GUILayout.Width(120))) player.SetMenuOpen(false);
            GUILayout.EndHorizontal();
            tab = GUILayout.Toolbar(tab, new[] { "스킬", "직업" });
            GUILayout.Space(8);
            scroll = GUILayout.BeginScrollView(scroll);
            if (tab == 0) DrawSkills();
            else DrawClasses();
            GUILayout.EndScrollView();
            GUILayout.Label(message, GUILayout.Height(30));
            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        private void DrawSkills()
        {
            SkillLoadout loadout = player.Loadout;
            GUILayout.Label("일반 스킬 2개 · 보스 스킬 1개 | 특수 스킬은 직업 고정");
            for (int slot = 0; slot < 3; slot++)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(Keys[slot] + "  " + SkillCatalog.Name(loadout.Slots[slot]), GUILayout.Height(28));
                GUI.enabled = loadout.Slots[slot] != SkillId.None;
                if (GUILayout.Button("해제", GUILayout.Width(70))) { loadout.Equip(slot, SkillId.None); message = "스킬을 해제했습니다."; }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8);
            foreach (SkillId id in SkillCatalog.ForClass(loadout.Class))
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(SkillCatalog.Name(id) + " · " + Cooldown(player.Skills.Cooldown(id)));
                GUILayout.Label(SkillCatalog.Description(id));
                GUILayout.BeginHorizontal();
                for (int slot = 0; slot < 2; slot++)
                    if (GUILayout.Button(Keys[slot] + " 장착")) message = loadout.Equip(slot, id) ? "장착했습니다." : "다른 슬롯에 이미 장착돼 있습니다. 먼저 해제하세요.";
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label(loadout.BossLearned ? "보스 스킬 습득 완료" : $"보스 스킬 북: {loadout.BossSkillBooks}개");
            GUI.enabled = !loadout.BossLearned && loadout.BossSkillBooks > 0;
            if (GUILayout.Button("스킬 북 사용")) loadout.UseBossSkillBook();
            GUI.enabled = loadout.BossLearned;
            if (GUILayout.Button("Shift 장착")) loadout.Equip(2, SkillId.BossBlast);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            string special = loadout.Class switch { CharacterClass.Warrior => "패링: 전방 탄막 반사 (원래 피해의 50%)", CharacterClass.Healer => "부활: 근처 아군 HP 70%로 부활 · 3초 무적", _ => "일석이조: 5초간 공속 증가 · 평타 2연사" };
            GUILayout.Label("좌클릭 " + special);
        }

        private void DrawClasses()
        {
            bool inTown = SceneManager.GetActiveScene().name == "Town";
            GUILayout.Label("마을에서 직업을 선택할 수 있습니다.");
            GUI.enabled = inTown;
            foreach (CharacterClass type in System.Enum.GetValues(typeof(CharacterClass)))
                if (GUILayout.Button(SkillCatalog.ClassName(type) + (player.Loadout.Class == type ? " (선택됨)" : ""), GUILayout.Height(45)))
                { player.Loadout.SelectClass(type); message = SkillCatalog.ClassName(type) + " 선택 완료"; }
            GUI.enabled = true;
#if UNITY_EDITOR
            GUILayout.Space(15);
            GUILayout.Label("에디터 연습 도구");
            if (GUILayout.Button("연습용 보스 스킬 북 생성")) BossSkillBook.Spawn((Vector2)player.transform.position + Vector2.right);
            if (GUILayout.Button("연습용 부상 아군 생성")) CreateAlly(false);
            if (GUILayout.Button("연습용 사망 아군 생성")) CreateAlly(true);
#endif
        }

#if UNITY_EDITOR
        private void CreateAlly(bool dead)
        {
            GameObject ally = new GameObject(dead ? "Practice Fallen Ally" : "Practice Ally");
            ally.transform.position = player.transform.position + Vector3.right * 1.5f;
            ally.AddComponent<Core.PlaceholderVisual>().Configure(dead ? Color.gray : Color.green, new Vector2(.5f, .7f));
            Combat.Health health = ally.AddComponent<Combat.Health>();
            health.Configure(Combat.Faction.Player, 100, false);
            ally.AddComponent<Combat.CombatStatus>().ApplyVulnerability();
            ally.AddComponent<CircleCollider2D>();
            health.TryDamage(dead ? 100 : 30, Combat.Faction.Enemy);
        }
#endif
        private static string Cooldown(float value) => value <= 0 ? "준비" : $"{value:F1}s";
    }
}
