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
        private const float Speed = 4;
        public override void OnNetworkSpawn()
        {
            if (IsServer) Position.Value = new Vector2((int)(OwnerClientId % 4) * 2 - 3, 0);
            transform.position = Position.Value;
            SpriteRenderer visual = GetComponent<SpriteRenderer>();
            if (visual != null) visual.color = Color.HSVToRGB((OwnerClientId * .23f) % 1, .65f, .95f);
            Debug.Log("PWA_PLAYER_SPAWN owner=" + OwnerClientId + " local=" + IsOwner);
        }
        private void Update()
        {
            if (!IsSpawned) return;
            if (!IsServer) transform.position = Vector2.Lerp(transform.position, Position.Value, 1 - Mathf.Exp(-18 * Time.deltaTime));
            if (!IsOwner || Time.unscaledTime < nextSendAt) return;
            Vector2 value = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (Application.isBatchMode && NetworkSandbox.HasArgument("-pwa-probe"))
                value = NetworkSandbox.Argument("-pwa-test-move", "right") == "up" ? Vector2.up : Vector2.right;
            SubmitInputRpc(value);
            nextSendAt = Time.unscaledTime + .05f;
        }
        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void SubmitInputRpc(Vector2 value)
        {
            if (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsInfinity(value.x) || float.IsInfinity(value.y)) return;
            input = Vector2.ClampMagnitude(value, 1);
            receivedAt = Time.unscaledTime;
        }
        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            Vector2 position = Position.Value;
            if (Time.unscaledTime - receivedAt < .3f) position += input * (Speed * Time.fixedDeltaTime);
            position.x = Mathf.Clamp(position.x, -8, 8); position.y = Mathf.Clamp(position.y, -4, 4);
            Position.Value = position;
            transform.position = position;
        }
        private void OnGUI()
        {
            if (!IsSpawned || Application.isBatchMode || Camera.main == null) return;
            Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * .65f);
            GUI.Label(new Rect(screen.x - 65, Screen.height - screen.y, 150, 25), "Player " + OwnerClientId + (IsOwner ? " (나)" : ""));
        }
    }
}
