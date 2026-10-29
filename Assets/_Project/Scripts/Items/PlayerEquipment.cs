using PersistentWorld.Combat;
using UnityEngine;

namespace PersistentWorld.Items
{
    [RequireComponent(typeof(Health))]
    public sealed class PlayerEquipment : MonoBehaviour
    {
        public PlayerInventoryProfile Profile { get; private set; }
        public EquipmentBonuses Bonuses => Profile != null ? Profile.GetBonuses() : default;
        public void Bind(PlayerInventoryProfile profile) => Profile = profile;
    }
}
