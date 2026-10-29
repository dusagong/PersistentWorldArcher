using System;
using UnityEngine;

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
            SkillId.ArrowRain => "누르는 동안 범위 조준, 놓으면 화살 비.",
            SkillId.Snipe => "누르고 차징, 놓으면 저격. 최대 차징 시 자동 발사.",
            SkillId.Whirlwind => "0.5초 준비 후 주변을 크게 벰.",
            SkillId.ComboSlash => "4초 안에 최대 3회 사용. 사용할수록 범위 증가.",
            SkillId.Excalibur => "차징할수록 범위·피해 증가. 차징 중 이동 불가.",
            SkillId.HealingZone => "5초간 초당 아군 최대 HP 20% 회복. 보스에게 피해.",
            SkillId.Cleanse => "자신과 주변 아군의 디버프 해제.",
            SkillId.Judgment => "누르는 동안 최대 5초 레이저. 시전 중 이동 불가.",
            SkillId.BossBlast => "화면에 보이는 모든 적에게 피해. 스킬 북으로 습득.",
            _ => ""
        };
    }

    [DisallowMultipleComponent]
    public sealed class SkillLoadout : MonoBehaviour
    {
        // Session state survives scene transitions; cooldowns live on PlayerSkills.
        private static CharacterClass selectedClass;
        private static readonly SkillId[][] Saved = new SkillId[3][];
        private static bool bossLearned;
        private static int bossSkillBooks;
        public CharacterClass Class { get; private set; }
        public SkillId[] Slots { get; private set; }
        public bool BossLearned => bossLearned;
        public int BossSkillBooks => bossSkillBooks;
        public event Action Changed;

        private void Awake() => SelectClass(selectedClass);

        public void SelectClass(CharacterClass type)
        {
            if (!Enum.IsDefined(typeof(CharacterClass), type)) return;
            Class = selectedClass = type;
            if (Saved[(int)type] == null)
            {
                SkillId[] list = SkillCatalog.ForClass(type);
                Saved[(int)type] = new[] { list[0], list[1], SkillId.None };
            }
            Slots = (SkillId[])Saved[(int)type].Clone();
            Changed?.Invoke();
        }

        public bool Equip(int slot, SkillId skill)
        {
            if (slot < 0 || slot > 2) return false;
            if (Slots[slot] == skill) return true;
            if (skill != SkillId.None)
            {
                if (slot == 2 ? skill != SkillId.BossBlast || !bossLearned : Array.IndexOf(SkillCatalog.ForClass(Class), skill) < 0) return false;
                for (int i = 0; i < Slots.Length; i++) if (i != slot && Slots[i] == skill) return false;
            }
            Slots[slot] = skill;
            Saved[(int)Class] = (SkillId[])Slots.Clone();
            Changed?.Invoke();
            return true;
        }

        private bool LearnBossSkill()
        {
            if (bossLearned) return false;
            bossLearned = true;
            Changed?.Invoke();
            return true;
        }

        public void AddBossSkillBook() { bossSkillBooks++; Changed?.Invoke(); }
        public bool UseBossSkillBook()
        {
            if (bossSkillBooks <= 0 || bossLearned) return false;
            bossSkillBooks--;
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
