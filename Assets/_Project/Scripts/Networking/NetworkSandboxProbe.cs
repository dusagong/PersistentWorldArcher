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
        private int hitSequence, enemyHits, playerHits, ownHits, attacks;
        private float accountReadyAt = -1;
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
            if (Application.isBatchMode && NetworkSandbox.HasArgument("-pwa-account-probe"))
            {
                NetworkGameWorld world = NetworkGameWorld.Instance;
                int.TryParse(NetworkSandbox.Argument("-pwa-expect-class", "0"), out int expected);
                if (world?.Local != null)
                {
                    if (world.Local.character != expected) { Fail("restored account class"); return; }
                    if (accountReadyAt < 0) accountReadyAt = Time.realtimeSinceStartup;
                    if (Time.realtimeSinceStartup - accountReadyAt > 2) sandbox.Logout();
                }
                else if (accountReadyAt >= 0 && !sandbox.Manager.IsListening)
                { Debug.Log("PWA_ACCOUNT_OK class=" + expected); Application.Quit(0); }
                if (Time.realtimeSinceStartup - startedAt > 20) Fail("account login/logout timeout");
                return;
            }
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
            else if (player.area == (int)GameArea.Field || player.area == (int)GameArea.Field2)
            {
                float phase = Time.realtimeSinceStartup * .9f;
                Vector2 center = NetworkGameWorld.Origin((GameArea)player.area) +
                    (player.area == (int)GameArea.Field2 ? new Vector2(-1, 1) : Vector2.zero);
                Vector2 orbit = center + new Vector2(Mathf.Cos(phase), Mathf.Sin(phase)) * 1.3f;
                movement = Vector2.ClampMagnitude(orbit - player.position, 1);
                aim = (center - player.position).normalized;
                if (aim.sqrMagnitude < .01f) aim = Vector2.right;
                target = center;
                buttons = 1;
                int cycle = (int)(Time.realtimeSinceStartup * 10) % 40;
                if (cycle == 0) buttons |= 2 | 8;
                if (cycle == 1) buttons |= 64;
                if (cycle == 10) buttons |= 4;
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
                if (NetworkGameWorld.Instance.Local?.area == (int)GameArea.Field || NetworkGameWorld.Instance.Local?.area == (int)GameArea.Field2) fieldFrames++;
                PlayerFrame local = NetworkGameWorld.Instance.Local;
                if (local != null) attacks = Mathf.Max(attacks, local.attacks);
                if (frame.hits != null) foreach (HitFrame hit in frame.hits)
                {
                    if (hit.sequence <= hitSequence) continue;
                    hitSequence = hit.sequence;
                    if (hit.player) { playerHits++; if (local != null && hit.visual == local.visual) ownHits++; }
                    else enemyHits++;
                }
            }
            if (now - lastFrameAt > 2) { Fail("soak game frames stalled for two seconds"); return; }
            if (now >= nextSampleAt)
            {
                nextSampleAt = now + 1;
                ulong rtt = sandbox.Manager.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
                if (rtt > 0) rttSamples.Add(rtt);
            }
            if (now - soakStartedAt < soakDuration) return;
            if (fieldFrames < soakDuration * 3) { Fail("soak client did not spend enough time in actual field combat"); return; }
            if (attacks < 10 || enemyHits == 0 || playerHits == 0)
            { Fail("soak did not observe actual attacks, monster damage and player damage"); return; }
            rttSamples.Sort();
            string p95 = rttSamples.Count == 0 ? "unavailable" : rttSamples[Mathf.CeilToInt(rttSamples.Count * .95f) - 1].ToString();
            Debug.Log($"PWA_SOAK_OK seconds={soakDuration} frames={frames} fieldFrames={fieldFrames} attacks={attacks} enemyHits={enemyHits} playerHits={playerHits} ownHits={ownHits} maxFrameGapMs={maxFrameGap * 1000:F0} rttP95Ms={p95}");
            Application.Quit(0);
        }
        private static void Fail(string reason) { Debug.LogError("PWA_NETWORK_PROBE_FAILED " + reason); Application.Quit(1); }
    }
}
