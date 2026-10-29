using UnityEngine;

namespace PersistentWorld.UI
{
    public static class ItemWindows
    {
        private static int closedFrame = -1;
        public static bool BlocksGameplay => BlacksmithWindow.IsOpen || InventoryWindow.IsOpen || closedFrame == Time.frameCount;
        public static void ConsumeClose() => closedFrame = Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => closedFrame = -1;
    }
}
