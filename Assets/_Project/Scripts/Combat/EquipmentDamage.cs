using PersistentWorld.Items;
using UnityEngine;

namespace PersistentWorld.Combat
{
    public enum MonsterKind { None, Normal, Rare, Boss }

    public readonly struct DamageContext
    {
        public readonly Faction Faction;
        public readonly MonsterKind Monster;
        public readonly EquipmentBonuses Equipment;
        public DamageContext(Faction faction, MonsterKind monster = MonsterKind.None, EquipmentBonuses equipment = default)
        { Faction = faction; Monster = monster; Equipment = equipment; }

        public static DamageContext From(Faction faction, GameObject source)
        {
            PlayerEquipment equipment = source != null ? source.GetComponentInParent<PlayerEquipment>() : null;
            return new DamageContext(faction, MonsterIdentity.Resolve(source),
                equipment != null ? equipment.Bonuses : default);
        }
    }

    public static class EquipmentDamage
    {
        // Prototype calculation order; PDF set percentages stay fixed.
        public static int Resolve(int baseDamage, DamageContext source, MonsterKind targetKind,
            EquipmentBonuses defender)
        {
            if (baseDamage <= 0) return 0;
            float damage = baseDamage;
            if (targetKind == MonsterKind.Normal && source.Equipment.SetPieces >= 3) damage *= 1.2f;
            if (targetKind == MonsterKind.Boss && source.Equipment.SetPieces >= 6) damage *= 1.3f;
            damage = Mathf.Max(1f, damage - defender.Defense);
            if (source.Monster == MonsterKind.Boss && defender.SetPieces >= 5) damage *= 0.8f;
            return Mathf.Max(1, Mathf.RoundToInt(damage));
        }
    }
}
