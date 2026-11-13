using System;
using Unity.Netcode;
using UnityEngine;

namespace PersistentWorld.Networking
{
    public sealed class NetworkSandboxPlayer : NetworkBehaviour
    {
        public NetworkVariable<Vector2> Position = new NetworkVariable<Vector2>();
        private Vector2 input, pendingMovement;
        private float receivedAt, nextSendAt;
        private int pending;
        public static NetworkSandboxPlayer LocalPlayer;
        private const float Speed = 4;
        public override void OnNetworkSpawn()
        {
            if (IsOwner) LocalPlayer = this;
            if (NetworkGameWorld.Instance != null)
            {
                GetComponent<SpriteRenderer>().enabled = false;
                if (IsServer) NetworkGameWorld.Instance.Spawn(this);
                return;
            }
            if (IsServer) Position.Value = new Vector2((int)(OwnerClientId % 4) * 2 - 3, 0);
            transform.position = Position.Value;
            SpriteRenderer visual = GetComponent<SpriteRenderer>();
            if (visual != null) visual.color = Color.HSVToRGB((OwnerClientId * .23f) % 1, .65f, .95f);
            Debug.Log("PWA_PLAYER_SPAWN owner=" + OwnerClientId + " local=" + IsOwner);
        }
        public override void OnNetworkDespawn()
        {
            if (LocalPlayer == this) LocalPlayer = null;
            if (IsServer) NetworkGameWorld.Instance?.Remove(OwnerClientId);
        }
        private void Update()
        {
            if (!IsSpawned) return;
            if (!IsServer) transform.position = Vector2.Lerp(transform.position, Position.Value, 1 - Mathf.Exp(-18 * Time.deltaTime));
            if (!IsOwner) return;
            // The party harness sends its own scripted RPCs and server actions.
            // Sampling the desktop cursor here would overwrite the aim being tested.
            if (NetworkSandbox.HasArgument("-pwa-party-probe")) return;
            NetworkPartyHud hud = NetworkPartyHud.Instance;
            bool blocked = PersistentWorld.UI.ItemWindows.BlocksGameplay || (hud != null && hud.BlocksKeyboard);
            bool pointerBlocked = blocked || (hud != null && hud.BlocksPointer);
            Vector2 aim = Vector2.right, target = Position.Value + aim;
            if (Camera.main != null) { target = Camera.main.ScreenToWorldPoint(Input.mousePosition); aim = target - Position.Value; }
            if (!blocked)
            {
                if (!pointerBlocked && Input.GetMouseButtonDown(0)) pending |= 2;
                if (!pointerBlocked && Input.GetMouseButtonDown(1)) pending |= 1;
                if (Input.GetKeyDown(KeyCode.Space)) pending |= 4;
                if (Input.GetKeyDown(KeyCode.Q)) pending |= 8;
                if (Input.GetKeyDown(KeyCode.E)) pending |= 16;
                if (Input.GetKeyDown(KeyCode.LeftShift)) pending |= 32;
                if (Input.GetKeyDown(KeyCode.F)) pending |= 512;
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) pending |= 1024;
                if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) pending |= 2048;
            }
            if (Input.GetKeyUp(KeyCode.Q)) pending |= 64;
            if (Input.GetKeyUp(KeyCode.E)) pending |= 128;
            if (Input.GetKeyUp(KeyCode.LeftShift)) pending |= 256;
            Vector2 value = blocked ? Vector2.zero : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (blocked) { pendingMovement = Vector2.zero; pending &= 64 | 128 | 256; }
            else if (value != Vector2.zero) pendingMovement = value;
            if (Time.unscaledTime < nextSendAt) return;
            if (value == Vector2.zero) value = pendingMovement;
            pendingMovement = Vector2.zero;
            if (!pointerBlocked && Input.GetMouseButton(1)) pending |= 1;
            if (Application.isBatchMode && NetworkSandbox.HasArgument("-pwa-probe") && !NetworkSandbox.HasArgument("-pwa-party-probe"))
                value = NetworkSandbox.Argument("-pwa-test-move", "right") == "up" ? Vector2.up : Vector2.right;
            if (Application.isBatchMode && NetworkGameWorld.Instance != null &&
                NetworkManager.TryGetComponent(out NetworkSandboxProbe probe) &&
                probe.SoakInput(out var soakMove, out var soakAim, out var soakTarget, out int soakButtons))
            { value = soakMove; aim = soakAim; target = soakTarget; pending = soakButtons; }
            SubmitInputRpc(value, aim, target, pending); pending = 0;
            nextSendAt = Time.unscaledTime + .05f;
        }
        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void SubmitInputRpc(Vector2 value, Vector2 aim, Vector2 target, int buttons)
        {
            if (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsInfinity(value.x) || float.IsInfinity(value.y)) return;
            input = Vector2.ClampMagnitude(value, 1);
            receivedAt = Time.unscaledTime;
            NetworkGameWorld.Instance?.Input(OwnerClientId, input, aim, target, buttons & 4095);
        }
        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void CommandRpc(byte command, ulong target, int first, int second)
        {
            if (command <= (byte)PartyCommand.ResetPractice) NetworkGameWorld.Instance?.Command(OwnerClientId, (PartyCommand)command, target, first, second);
        }
        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void ItemRpc(byte command, string item, int slot)
        {
            if (command <= (byte)ItemCommand.UsePotion && item != null && item.Length <= 80)
                NetworkGameWorld.Instance?.ItemAction(OwnerClientId, (ItemCommand)command, item, slot);
        }
        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            if (NetworkGameWorld.Instance is NetworkGameWorld world)
            {
                if (world.Actors.TryGetValue(OwnerClientId, out var actor))
                {
                    if (Time.unscaledTime - receivedAt >= .3f) actor.SetExternalMovement(Vector2.zero, actor.AimDirection);
                    Position.Value = actor.transform.position;
                }
                return;
            }
            Vector2 position = Position.Value;
            if (Time.unscaledTime - receivedAt < .3f) position += input * (Speed * Time.fixedDeltaTime);
            position.x = Mathf.Clamp(position.x, -8, 8); position.y = Mathf.Clamp(position.y, -4, 4);
            Position.Value = position;
            transform.position = position;
        }
        private void OnGUI()
        {
            if (NetworkGameWorld.Instance != null || !IsSpawned || Application.isBatchMode || Camera.main == null) return;
            Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * .65f);
            GUI.Label(new Rect(screen.x - 65, Screen.height - screen.y, 150, 25), "Player " + OwnerClientId + (IsOwner ? " (나)" : ""));
        }
    }
}
