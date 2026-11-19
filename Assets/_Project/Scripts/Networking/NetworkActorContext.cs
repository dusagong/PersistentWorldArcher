using UnityEngine;
using PersistentWorld.Player;

namespace PersistentWorld.Networking
{
    public sealed class NetworkActorContext : MonoBehaviour
    {
        public ulong ClientId;
        public GameArea Area;
        public CharacterClass StartingClass;
        public bool BattleActive => Area.IsBoss() && NetworkGameWorld.Instance != null && NetworkGameWorld.Instance.BattleActiveIn(Area);
    }
}
