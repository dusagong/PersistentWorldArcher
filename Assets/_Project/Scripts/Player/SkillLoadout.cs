using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using PersistentWorld.Networking;

namespace PersistentWorld.Player
{
    public enum CharacterClass { Archer, Warrior, Healer }
    public enum SkillId { None, Repulse, ArrowRain, Snipe, Whirlwind, ComboSlash, Excalibur, HealingZone, Cleanse, Judgment, BossBlast }

    public static class SkillCatalog
    {
        public static SkillId[] ForClass(CharacterClass type) => type switch
        {
            CharacterClass.Warrior => new[] { SkillId.Whirlwind, SkillId.ComboSlash, SkillId.Excalibur },
            CharacterClass.Healer => new[] { SkillId.HealingZone, SkillId.Cleanse, SkillId.Judgment },
            _ => new[] { SkillId.Repulse, SkillId.ArrowRain, SkillId.Snipe }
        };

        public static string Name(SkillId id) => id switch
        {
            SkillId.Repulse => "오지마", SkillId.ArrowRain => "화살 비", SkillId.Snipe => "저격",
            SkillId.Whirlwind => "원형베기", SkillId.ComboSlash => "연속베기", SkillId.Excalibur => "엑스칼리버",
            SkillId.HealingZone => "힐링 존", SkillId.Cleanse => "디버프 해제", SkillId.Judgment => "심판",
            SkillId.BossBlast => "보스 스킬: 전장 심판", _ => "비어 있음"
        };

        public static string ClassName(CharacterClass type) => type switch
        { CharacterClass.Warrior => "전사", CharacterClass.Healer => "힐러", _ => "궁수" };

        public static string Description(SkillId id) => id switch
        {
            SkillId.Repulse => "전방 90도 적 피해·넉백. 패턴 중인 보스는 밀리지 않음.",
            SkillId.ArrowRain => "사거리 8. 3초간 화살 비. 일반 몬스터 이동속도 50% 감소.",
            SkillId.Snipe => "누르고 차징, 놓으면 저격. 최대 차징 시 자동 발사.",
            SkillId.Whirlwind => "0.5초 준비 후 주변을 크게 벰.",
            SkillId.ComboSlash => "4초 안에 최대 3회 사용. 사용할수록 범위 증가.",
            SkillId.Excalibur => "차징할수록 범위·피해 증가. 차징 중 조준 가능, 이동 불가.",
            SkillId.HealingZone => "5초간 초당 아군 최대 HP 20% 회복. 보스에게 피해. 쿨타임 15초.",
            SkillId.Cleanse => "자신과 주변 아군의 디버프 해제. 쿨타임 10초.",
            SkillId.Judgment => "누르는 동안 최대 5초 레이저. 시전 중 이동 불가. 종료 후 쿨타임 10초.",
            SkillId.BossBlast => "화면에 보이는 모든 적에게 피해. 스킬 북으로 습득.",
            _ => ""
        };

        public static float Cooldown(SkillId id) => id switch
        {
            SkillId.Repulse => 4, SkillId.ArrowRain => 8, SkillId.Snipe => 10,
            SkillId.Whirlwind => 5, SkillId.ComboSlash => 6, SkillId.Excalibur => 10,
            SkillId.HealingZone => 15, SkillId.Cleanse => 10, SkillId.Judgment => 10,
            SkillId.BossBlast => 20, _ => 0
        };
    }

    [DisallowMultipleComponent]
    public sealed class SkillLoadout : MonoBehaviour
    {
        [Serializable] internal sealed class ClassSlots { public SkillId[] values; }
        [Serializable] internal sealed class OnlineState
        {
            public CharacterClass selected;
            public ClassSlots[] classes;
            public bool bossLearned;
            public int books;
        }

        internal OnlineState ExportOnline()
        {
            var state = new OnlineState { selected = Class, classes = new ClassSlots[3], bossLearned = personalBossLearned, books = personalBooks };
            for (int i = 0; i < state.classes.Length; i++)
                state.classes[i] = new ClassSlots { values = personalSlots[i] };
            return state;
        }

        internal void ImportOnline(OnlineState state)
        {
            personalBossLearned = state.bossLearned;
            personalBooks = Mathf.Max(0, state.books);
            for (int i = 0; i < personalSlots.Length; i++)
            {
                var values = state.classes != null && i < state.classes.Length ? state.classes[i]?.values : null;
                if (values == null || values.Length != 3) continue;
                SelectClass((CharacterClass)i);
                for (int slot = 0; slot < 3; slot++) Equip(slot, SkillId.None);
                for (int slot = 0; slot < 3; slot++) Equip(slot, values[slot]);
            }
            SelectClass(state.selected);
        }

        // Session state survives scene transitions; cooldowns live on PlayerSkills.
        private static CharacterClass selectedClass;
        private static readonly SkillId[][] Saved = new SkillId[3][];
        private static bool bossLearned;
        private static int bossSkillBooks;
        private readonly SkillId[][] personalSlots = new SkillId[3][];
        private bool personalBossLearned;
        private int personalBooks;
        private bool Shared => GetComponent<NetworkActorContext>() == null;
        private SkillId[][] Store => Shared ? Saved : personalSlots;
        public bool CanChangeSkills => !Application.isPlaying ||
            (GetComponent<NetworkActorContext>() is NetworkActorContext context ? context.Area == GameArea.Town : SceneManager.GetActiveScene().name == "Town");
        public CharacterClass Class { get; private set; }
        public SkillId[] Slots { get; private set; }
        public bool BossLearned => Shared ? bossLearned : personalBossLearned;
        public int BossSkillBooks => Shared ? bossSkillBooks : personalBooks;
        public event Action Changed;

        private void Awake() => SelectClass(Shared ? selectedClass : GetComponent<NetworkActorContext>().StartingClass);

        public void SelectClass(CharacterClass type)
        {
            if (!Enum.IsDefined(typeof(CharacterClass), type)) return;
            if (Slots != null && !CanChangeSkills) return;
            Class = type;
            if (Shared) selectedClass = type;
            if (Store[(int)type] == null)
            {
                SkillId[] list = SkillCatalog.ForClass(type);
                Store[(int)type] = new[] { list[0], list[1], SkillId.None };
            }
            Slots = (SkillId[])Store[(int)type].Clone();
            Changed?.Invoke();
        }

        public bool Equip(int slot, SkillId skill)
        {
            if (!CanChangeSkills) return false;
            if (slot < 0 || slot > 2) return false;
            if (Slots[slot] == skill) return true;
            if (skill != SkillId.None)
            {
                if (slot == 2 ? skill != SkillId.BossBlast || !BossLearned : Array.IndexOf(SkillCatalog.ForClass(Class), skill) < 0) return false;
                for (int i = 0; i < Slots.Length; i++) if (i != slot && Slots[i] == skill) return false;
            }
            Slots[slot] = skill;
            Store[(int)Class] = (SkillId[])Slots.Clone();
            Changed?.Invoke();
            return true;
        }

        private bool LearnBossSkill()
        {
            if (BossLearned) return false;
            if (Shared) bossLearned = true; else personalBossLearned = true;
            Changed?.Invoke();
            return true;
        }

        public void AddBossSkillBook() { if (Shared) bossSkillBooks++; else personalBooks++; Changed?.Invoke(); }
        public bool UseBossSkillBook()
        {
            if (BossSkillBooks <= 0 || BossLearned) return false;
            if (Shared) bossSkillBooks--; else personalBooks--;
            return LearnBossSkill();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            selectedClass = CharacterClass.Archer;
            Array.Clear(Saved, 0, Saved.Length);
            bossLearned = false;
            bossSkillBooks = 0;
        }
    }
}
