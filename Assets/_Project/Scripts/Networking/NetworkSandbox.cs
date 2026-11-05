using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace PersistentWorld.Networking
{
    // Separate infrastructure scene: existing Town/Field combat remains offline.
    public sealed class NetworkSandbox : MonoBehaviour
    {
        [SerializeField] private NetworkManager manager;
        private readonly HashSet<ulong> admitted = new HashSet<ulong>();
        private ServerReadiness readiness;
        private string endpoint = "ws://127.0.0.1:7777";
        private string password = "";
        private string status = "서버 주소를 입력하고 접속하세요.";
        private string serverPassword;
        private int limit;
        private bool connecting;
        public NetworkManager Manager => manager;
        public bool Ready => manager != null && manager.IsServer && manager.IsListening;
        public string Status => status;
        [Serializable] private sealed class Ticket { public int protocol = 1; public string code; }

        public static string Argument(string name, string fallback = null)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return fallback;
        }
        public static bool HasArgument(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
        private static int Setting(string name, int fallback, int minimum, int maximum)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value)) return fallback;
            if (!int.TryParse(value, out int number) || number < minimum || number > maximum)
                throw new ArgumentException("Invalid server setting: " + name);
            return number;
        }

        private void Awake()
        {
            manager.OnClientConnectedCallback += Connected;
            manager.OnClientDisconnectCallback += Disconnected;
            manager.ConnectionApprovalCallback = Approve;
        }

        private void Start()
        {
            // NGO registers its message table during startup; starting in Awake is too early.
            bool server = HasArgument("-pwa-server");
#if UNITY_SERVER && !UNITY_EDITOR
            server = true;
#endif
            if (server) StartServer();
            else if (HasArgument("-pwa-connect")) Connect(Argument("-pwa-connect"), Argument("-pwa-code", ""));
        }

        public bool StartServer()
        {
            if (manager.IsListening) return false;
            try
            {
                Application.targetFrameRate = 30;
                limit = Setting("PWA_MAX_PLAYERS", 4, 1, 32);
                int port = Setting("PWA_GAME_PORT", 7777, 1024, 65535);
                serverPassword = Environment.GetEnvironmentVariable("PWA_JOIN_CODE") ?? "";
                if (serverPassword.Length > 64) throw new ArgumentException("PWA_JOIN_CODE must be at most 64 characters.");
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT_ID")) && serverPassword.Length < 12)
                    throw new ArgumentException("Set a private PWA_JOIN_CODE (12+ characters) before deploying to Railway.");
                UnityTransport transport = manager.GetComponent<UnityTransport>();
                transport.UseWebSockets = true;
                // Railway terminates TLS; Caddy forwards plain WebSocket inside the container.
                transport.UseEncryption = false;
                transport.SetConnectionData("127.0.0.1", (ushort)port, "0.0.0.0");
                admitted.Clear();
                if (!manager.StartServer()) throw new InvalidOperationException("Network server could not start.");
                readiness = new ServerReadiness(Setting("PWA_HEALTH_PORT", 8081, 1024, 65535));
                status = "Server running / WebSocket / max " + limit;
                Debug.Log("PWA_SERVER_READY port=" + port + " capacity=" + limit);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("PWA_SERVER_START_FAILED " + exception.Message);
                manager.Shutdown();
                if (Application.isBatchMode) Application.Quit(1);
                return false;
            }
        }

        public bool Connect(string address, string code)
        {
            if (connecting || manager.IsListening || manager.ShutdownInProgress) return false;
            try
            {
                if (!Uri.TryCreate(address, UriKind.Absolute, out Uri uri) ||
                    (uri.Scheme != "ws" && uri.Scheme != "wss") || string.IsNullOrEmpty(uri.Host) ||
                    uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0)
                    throw new ArgumentException("ws://127.0.0.1:7777 또는 wss://서버주소 형식으로 입력하세요.");
                if (uri.Scheme == "ws" && !uri.IsLoopback)
                    throw new ArgumentException("외부 서버에는 wss:// 주소를 사용하세요.");
                if ((code ?? "").Length > 64) throw new ArgumentException("참가 코드는 최대 64자입니다.");
                connecting = true;
                status = "서버 주소 확인 중…";
                ResolveAndConnect(uri, code ?? "");
                return true;
            }
            catch (Exception exception) { status = exception.Message; return false; }
        }

        private async void ResolveAndConnect(Uri uri, string code)
        {
            try
            {
                // NGO 2.4 enables native hostname resolution only on Unity 6000.1+.
                // Dial the resolved IP, while keeping the hostname for TLS/SNI and HTTP Host.
                if (!IPAddress.TryParse(uri.DnsSafeHost, out IPAddress address))
                {
                    IPAddress[] addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost);
                    address = Array.Find(addresses, value => value.AddressFamily == AddressFamily.InterNetwork)
                        ?? Array.Find(addresses, value => value.AddressFamily == AddressFamily.InterNetworkV6);
                    if (address == null) throw new InvalidOperationException("서버 IP 주소를 찾을 수 없습니다.");
                }
                if (this == null || manager == null) return;
                UnityTransport transport = manager.GetComponent<UnityTransport>();
                transport.UseWebSockets = true;
                transport.UseEncryption = uri.Scheme == "wss";
                if (transport.UseEncryption) transport.SetClientSecrets(uri.DnsSafeHost);
                transport.SetConnectionData(address.ToString(), (ushort)uri.Port);
                manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Ticket { code = code }));
                status = "접속 중…";
                if (!manager.StartClient()) throw new InvalidOperationException("접속을 시작할 수 없습니다.");
            }
            catch (Exception exception) { status = exception.Message; Debug.LogWarning("PWA_CLIENT_START_FAILED " + exception.Message); }
            finally { connecting = false; }
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = "참가 코드 또는 클라이언트 버전이 올바르지 않습니다.";
            if (request.Payload == null || request.Payload.Length > 512) return;
            Ticket ticket;
            try { ticket = JsonUtility.FromJson<Ticket>(Encoding.UTF8.GetString(request.Payload)); }
            catch (ArgumentException) { return; }
            if (ticket == null || ticket.protocol != 1 || ticket.code != serverPassword) return;
            if (admitted.Count >= limit) { response.Reason = "서버 정원이 찼습니다."; return; }
            admitted.Add(request.ClientNetworkId);
            response.Approved = response.CreatePlayerObject = true;
            response.Reason = "";
        }

        private void Connected(ulong id)
        {
            status = "접속 완료 · WASD 이동";
            Debug.Log("PWA_CLIENT_CONNECTED id=" + id);
        }
        private void Disconnected(ulong id)
        {
            admitted.Remove(id);
            status = string.IsNullOrEmpty(manager.DisconnectReason) ? "연결이 종료되었습니다. 다시 접속할 수 있습니다." : manager.DisconnectReason;
            Debug.Log("PWA_CLIENT_DISCONNECTED id=" + id);
        }
        private void Update() => readiness?.Heartbeat(Ready, manager.ConnectedClientsIds.Count);
        private void OnDestroy()
        {
            readiness?.Dispose();
            if (manager == null) return;
            manager.OnClientConnectedCallback -= Connected;
            manager.OnClientDisconnectCallback -= Disconnected;
        }
        private void OnGUI()
        {
            if (Application.isBatchMode || manager.IsServer) return;
            GUILayout.BeginArea(new Rect(16, 16, 460, 215), GUI.skin.box);
            GUILayout.Label("멀티플레이 접속 테스트 · 전투 기능은 아직 연결 전");
            GUILayout.Label(status);
            if (!manager.IsListening)
            {
                endpoint = GUILayout.TextField(endpoint, 200);
                GUILayout.Label("참가 코드"); password = GUILayout.PasswordField(password, '*', 64);
                GUI.enabled = !connecting && !manager.ShutdownInProgress;
                if (GUILayout.Button("서버 접속")) Connect(endpoint.Trim(), password);
                GUI.enabled = true;
            }
            else if (GUILayout.Button("접속 종료")) manager.Shutdown();
            GUILayout.EndArea();
        }
    }
}
