using PersistentWorld.Combat;
using UnityEngine;

namespace PersistentWorld.World
{
    public static class BossEncounter
    {
        private static bool externallyCompleted;
        public static bool Active
        {
            get
            {
                if (externallyCompleted) return false;
                foreach (BossController boss in Object.FindObjectsByType<BossController>(FindObjectsSortMode.None))
                    if (!boss.GetComponent<Health>().IsDead) return true;
                return false;
            }
        }
        // Boss encounter owner calls Complete on party wipe/timeout; boss death is automatic.
        public static void Complete() => externallyCompleted = true;
        public static void Begin() => externallyCompleted = false;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => externallyCompleted = false;
    }
}
