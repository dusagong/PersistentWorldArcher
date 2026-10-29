using PersistentWorld.Player;
using PersistentWorld.UI;
using UnityEngine;

namespace PersistentWorld.World
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        private ArcherController player;

        private void OnGUI()
        {
            if (ItemWindows.BlocksGameplay) return;
            if (player == null)
            {
                player = FindFirstObjectByType<ArcherController>();
            }

            GUI.Box(new Rect(16f, 16f, 330f, 138f), "Archer Prototype");
            if (player != null)
            {
                GUI.Label(new Rect(30f, 45f, 300f, 22f), $"HP {player.Health.Current} / {player.Health.Maximum}");
                GUI.Label(new Rect(30f, 67f, 300f, 22f), $"Q Double Shot: {FormatCooldown(player.DoubleShotRemaining)}");
                GUI.Label(new Rect(30f, 89f, 300f, 22f), $"E Backstep: {FormatCooldown(player.BackstepRemaining)}");
                GUI.Label(new Rect(30f, 111f, 300f, 22f), player.IsFocused ? "Focus: ACTIVE" : $"RMB Focus: {FormatCooldown(player.FocusRemaining)}");

                if (!string.IsNullOrEmpty(player.NearbyPrompt))
                {
                    GUI.Box(new Rect(Screen.width * 0.5f - 140f, Screen.height - 70f, 280f, 36f), player.NearbyPrompt);
                }
            }

            if (!string.IsNullOrEmpty(DialogueState.Line))
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 260f, Screen.height - 150f, 520f, 68f), string.Empty);
                GUI.Label(new Rect(Screen.width * 0.5f - 242f, Screen.height - 140f, 484f, 22f), DialogueState.Speaker);
                GUI.Label(new Rect(Screen.width * 0.5f - 242f, Screen.height - 116f, 484f, 28f), DialogueState.Line);
            }

            GUI.Label(new Rect(Screen.width - 355f, 18f, 340f, 100f), "WASD Move | LMB Arrow | Q Double Shot\nE Backstep | RMB Focus | F Interact");
        }

        private static string FormatCooldown(float remaining)
        {
            return remaining <= 0f ? "READY" : remaining.ToString("0.0") + "s";
        }
    }
}

