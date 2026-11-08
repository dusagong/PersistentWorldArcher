using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using PersistentWorld.World;

namespace PersistentWorld.Networking
{
    // Runs only with explicit batch-mode test flags, never in normal play.
    public sealed class NetworkSandboxProbe : MonoBehaviour
    {
        private NetworkSandbox sandbox;
        private float startedAt, pairedAt = -1;
        private readonly Dictionary<ulong, Vector2> initial = new Dictionary<ulong, Vector2>();
        private float soakDuration, soakStartedAt = -1, lastFrameAt, maxFrameGap, nextSampleAt;
        private GameFrame lastFrame;
        private int fieldFrames, frames;
        private readonly List<ulong> rttSamples = new List<ulong>();
        private void Start()
        {
            sandbox = GetComponent<NetworkSandbox>();
            startedAt = Time.realtimeSinceStartup;
            if (Application.isBatchMode && int.TryParse(NetworkSandbox.Argument("-pwa-soak-seconds"), out int seconds))
                soakDuration = Mathf.Clamp(seconds, 30, 1800);
        }
        private void Update()
        {
            if (soakDuration > 0) { ObserveSoak(); return; }
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
        internal bool SoakInput(out Vector2 movement, out Vector2 aim, out Vector2 target, out int buttons)
        {
            movement = Vector2.zero;
            aim = Vector2.right;
            target = Vector2.zero;
            buttons = 0;
            if (soakDuration <= 0) return false;
            PlayerFrame player = NetworkGameWorld.Instance?.Local;
            if (player == null || player.hp <= 0) return true;
            if (player.area == (int)GameArea.Town)
            {
                foreach (ScenePortal portal in FindObjectsByType<ScenePortal>(FindObjectsSortMode.None))
                {
                    if (portal.Destination != "Field") continue;
                    Vector2 offset = (Vector2)portal.transform.position - player.position;
                    movement = offset.magnitude > 1 ? offset.normalized : Vector2.zero;
                    if (movement == Vector2.zero) buttons = 512;
                    break;
                }
            }
            else if (player.area == (int)GameArea.Field)
            {
                float phase = Time.realtimeSinceStartup * .9f;
                movement = new Vector2(Mathf.Cos(phase), Mathf.Sin(phase));
                aim = movement;
                target = player.position + aim * 2;
                buttons = 1;
                int cycle = (int)(Time.realtimeSinceStartup * 10) % 40;
                if (cycle == 0) buttons |= 2 | 8;
                if (cycle == 1) buttons |= 64;
                if (cycle == 20) buttons |= 16;
                if (cycle == 21) buttons |= 128;
            }
            return true;
        }
        private void ObserveSoak()
        {
            float now = Time.realtimeSinceStartup;
            GameFrame frame = NetworkGameWorld.Instance?.Frame;
            if (!sandbox.Manager.IsConnectedClient || frame == null)
            {
                if (soakStartedAt >= 0 || now - startedAt > 20) Fail("soak connection lost or no game frames");
                return;
            }
            if (soakStartedAt < 0) soakStartedAt = lastFrameAt = now;
            if (frame != lastFrame)
            {
                maxFrameGap = Mathf.Max(maxFrameGap, now - lastFrameAt);
                lastFrameAt = now;
                lastFrame = frame;
                frames++;
                if (NetworkGameWorld.Instance.Local?.area == (int)GameArea.Field) fieldFrames++;
            }
            if (now - lastFrameAt > 2) { Fail("soak game frames stalled for two seconds"); return; }
            if (now >= nextSampleAt)
            {
                nextSampleAt = now + 1;
                ulong rtt = sandbox.Manager.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
                if (rtt > 0) rttSamples.Add(rtt);
            }
            if (now - soakStartedAt < soakDuration) return;
            if (fieldFrames < 20) { Fail("soak client did not receive enough Field frames"); return; }
            rttSamples.Sort();
            string p95 = rttSamples.Count == 0 ? "unavailable" : rttSamples[Mathf.CeilToInt(rttSamples.Count * .95f) - 1].ToString();
            Debug.Log($"PWA_SOAK_OK seconds={soakDuration} frames={frames} fieldFrames={fieldFrames} maxFrameGapMs={maxFrameGap * 1000:F0} rttP95Ms={p95}");
            Application.Quit(0);
        }
        private static void Fail(string reason) { Debug.LogError("PWA_NETWORK_PROBE_FAILED " + reason); Application.Quit(1); }
    }
}
