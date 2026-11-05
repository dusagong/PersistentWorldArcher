using System;
using Unity.Netcode;
using UnityEngine;

namespace PersistentWorld.Networking
{
    public sealed class NetworkSandboxPlayer : NetworkBehaviour
    {
        public NetworkVariable<Vector2> Position = new NetworkVariable<Vector2>();
        private Vector2 input;
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
            bool blocked = NetworkPartyHud.Instance != null && NetworkPartyHud.Instance.BlocksInput;
            Vector2 aim = Vector2.right, target = Position.Value + aim;
            if (Camera.main != null) { target = Camera.main.ScreenToWorldPoint(Input.mousePosition); aim = target - Position.Value; }
            if (!blocked)
            {
                if (Input.GetMouseButtonDown(0)) pending |= 2;
                if (Input.GetKeyDown(KeyCode.Space)) pending |= 4;
                if (Input.GetKeyDown(KeyCode.Q)) pending |= 8;
                if (Input.GetKeyDown(KeyCode.E)) pending |= 16;
                if (Input.GetKeyDown(KeyCode.LeftShift)) pending |= 32;
                if (Input.GetKeyDown(KeyCode.F)) pending |= 512;
            }
            if (Input.GetKeyUp(KeyCode.Q)) pending |= 64;
            if (Input.GetKeyUp(KeyCode.E)) pending |= 128;
            if (Input.GetKeyUp(KeyCode.LeftShift)) pending |= 256;
            if (Time.unscaledTime < nextSendAt) return;
            Vector2 value = blocked ? Vector2.zero : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (!blocked && Input.GetMouseButton(1)) pending |= 1;
            if (Application.isBatchMode && NetworkSandbox.HasArgument("-pwa-probe") && !NetworkSandbox.HasArgument("-pwa-party-probe"))
                value = NetworkSandbox.Argument("-pwa-test-move", "right") == "up" ? Vector2.up : Vector2.right;
            SubmitInputRpc(value, aim, target, pending); pending = 0;
            nextSendAt = Time.unscaledTime + .05f;
        }
        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void SubmitInputRpc(Vector2 value, Vector2 aim, Vector2 target, int buttons)
        {
            if (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsInfinity(value.x) || float.IsInfinity(value.y)) return;
            input = Vector2.ClampMagnitude(value, 1);
            receivedAt = Time.unscaledTime;
            NetworkGameWorld.Instance?.Input(OwnerClientId, input, aim, target, buttons & 1023);
        }
        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void CommandRpc(byte command, ulong target, int first, int second)
        {
            if (command <= (byte)PartyCommand.Menu) NetworkGameWorld.Instance?.Command(OwnerClientId, (PartyCommand)command, target, first, second);
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
