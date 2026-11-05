using System.Collections.Generic;
using UnityEngine;

namespace PersistentWorld.Networking
{
    // Runs only with explicit batch-mode test flags, never in normal play.
    public sealed class NetworkSandboxProbe : MonoBehaviour
    {
        private NetworkSandbox sandbox;
        private float startedAt, pairedAt = -1;
        private readonly Dictionary<ulong, Vector2> initial = new Dictionary<ulong, Vector2>();
        private void Start() { sandbox = GetComponent<NetworkSandbox>(); startedAt = Time.realtimeSinceStartup; }
        private void Update()
        {
            if (!Application.isBatchMode || !NetworkSandbox.HasArgument("-pwa-probe")) return;
            if (NetworkSandbox.HasArgument("-pwa-party-probe")) return;
            float elapsed = Time.realtimeSinceStartup - startedAt;
            if (NetworkSandbox.HasArgument("-pwa-expect-reject"))
            {
                if (elapsed > 1 && !sandbox.Manager.IsListening && !string.IsNullOrEmpty(sandbox.Manager.DisconnectReason))
                { Debug.Log("PWA_REJECTION_PROBE_OK"); Application.Quit(0); }
                else if (elapsed > 20) Fail("approval rejection not received");
                return;
            }
            NetworkSandboxPlayer[] players = FindObjectsByType<NetworkSandboxPlayer>(FindObjectsSortMode.None);
            if (players.Length >= 2 && sandbox.Manager.IsConnectedClient)
            {
                if (pairedAt < 0)
                {
                    pairedAt = Time.realtimeSinceStartup;
                    foreach (NetworkSandboxPlayer player in players) initial[player.OwnerClientId] = player.Position.Value;
                }
                if (Time.realtimeSinceStartup - pairedAt > 1.5f)
                {
                    bool owner = false, remote = false;
                    foreach (NetworkSandboxPlayer player in players)
                        if (initial.TryGetValue(player.OwnerClientId, out Vector2 position) && Vector2.Distance(position, player.Position.Value) > .5f)
                        { if (player.IsOwner) owner = true; else remote = true; }
                    if (owner && remote) { Debug.Log("PWA_TWO_CLIENTS_PROBE_OK players=" + players.Length); Application.Quit(0); }
                }
            }
            if (elapsed > 25) Fail("two replicated moving players were not observed");
        }
        private static void Fail(string reason) { Debug.LogError("PWA_NETWORK_PROBE_FAILED " + reason); Application.Quit(1); }
    }
}
