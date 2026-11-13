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
    // Shared connection bootstrap for the sandbox and cooperative game scenes.
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
        private int connectionGeneration;
        private bool demo, selectClass, loggingOut;
        private float logoutAt;
        private string loginMode = "resume";
        private int selectedClass;
        public bool IsDemo => demo;
        public string PlayerCode => demo ? "" : pendingCharacterKey ?? "";
        public NetworkManager Manager => manager;
        public bool Ready => manager != null && manager.IsServer && manager.IsListening;
        public string Status => status;
        private readonly Dictionary<ulong, string> names = new Dictionary<ulong, string>();
        private string displayName = "";
        private string recoveryKey = "";
        private string pendingCharacterKey;
        private string pendingCharacterPreference;
        private Uri lastServer;
        private string lastCode, lastCharacterKey;
        private bool connectedLocally;
        private int retryIndex;
        private float retryAt = -1;
        private static readonly float[] RetryDelays = { 2, 5, 10, 20 };
        private bool CanReconnect => NetworkGameWorld.Instance != null &&
            (!Application.isBatchMode || HasArgument("-pwa-reconnect-test")) &&
            !HasArgument("-pwa-probe");
        public string PlayerName(ulong id) => names.TryGetValue(id, out string value) ? value : "Player " + id;
        // The town assets change the shared render registry's texture indices.
        public const int Protocol = 6;
        private const int CooperativePacketQueueSize = 1024;
        [Serializable] private sealed class Ticket { public int protocol, selected; public string code, name, characterKey, mode; }

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
            if (!Application.isBatchMode) GameAudio.StartMusic();
            recoveryKey = Argument("-pwa-character-key", "");
            loginMode = Argument("-pwa-account-mode", "resume"); demo = loginMode == "demo";
            int.TryParse(Argument("-pwa-class", "0"), out selectedClass);
            displayName = Argument("-pwa-name", PlayerPrefs.GetString("pwa-display-name", ""));
            if (NetworkGameWorld.Instance != null && !HasArgument("-pwa-server") && !Application.isBatchMode)
                endpoint = "wss://pwa-game-server-production.up.railway.app";
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
                Application.targetFrameRate = 60;
                limit = Setting("PWA_MAX_PLAYERS", 4, 1, 32);
                int port = Setting("PWA_GAME_PORT", 7777, 1024, 65535);
                serverPassword = Environment.GetEnvironmentVariable("PWA_JOIN_CODE") ?? "";
                if (serverPassword.Length > 64) throw new ArgumentException("PWA_JOIN_CODE must be at most 64 characters.");
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT_ID")) && serverPassword.Length < 12)
                    throw new ArgumentException("Set a private PWA_JOIN_CODE (12+ characters) before deploying to Railway.");
                UnityTransport transport = manager.GetComponent<UnityTransport>();
                if (NetworkGameWorld.Instance != null) transport.MaxPacketQueueSize = CooperativePacketQueueSize;
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
                retryAt = -1;
                retryIndex = 0;
                loggingOut = false;
                lastServer = uri;
                lastCode = code ?? "";
                lastCharacterKey = NetworkGameWorld.Instance == null ? null : CharacterKey(uri);
                StartConnection();
                return true;
            }
            catch (Exception exception) { status = exception.Message; return false; }
        }

        private void StartConnection()
        {
            connecting = true;
            status = retryIndex == 0 ? "서버 주소 확인 중…" : "같은 캐릭터로 다시 접속 중…";
            ResolveAndConnect(lastServer, lastCode, lastCharacterKey, ++connectionGeneration);
        }

        private async void ResolveAndConnect(Uri uri, string code, string key, int generation)
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
                if (this == null || manager == null || generation != connectionGeneration || loggingOut) return;
                UnityTransport transport = manager.GetComponent<UnityTransport>();
                if (NetworkGameWorld.Instance != null) transport.MaxPacketQueueSize = CooperativePacketQueueSize;
                transport.UseWebSockets = true;
                transport.UseEncryption = uri.Scheme == "wss";
                if (transport.UseEncryption)
                {
                    // A cold edge or TLS negotiation can outlast the local socket default.
                    transport.ConnectTimeoutMS = 5000;
                    transport.SetClientSecrets(uri.DnsSafeHost);
                }
                transport.SetConnectionData(address.ToString(), (ushort)uri.Port);
                pendingCharacterKey = key;
                pendingCharacterPreference = CharacterPreference(uri);
                manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Ticket { protocol = Protocol, code = code, name = displayName, characterKey = key, mode = loginMode, selected = selectedClass }));
                status = "접속 중…";
                if (!manager.StartClient()) throw new InvalidOperationException("접속을 시작할 수 없습니다.");
            }
            catch (Exception exception)
            {
                status = exception.Message;
                Debug.LogWarning("PWA_CLIENT_START_FAILED " + exception.Message);
                if (retryIndex > 0) QueueReconnect();
            }
            finally { if (generation == connectionGeneration) connecting = false; }
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
            if (ticket == null || ticket.protocol != Protocol || ticket.code != serverPassword) return;
            if (admitted.Count >= limit) { response.Reason = "서버 정원이 찼습니다."; return; }
            if (NetworkGameWorld.Instance is NetworkGameWorld world && !world.ReserveCharacter(request.ClientNetworkId, ticket.characterKey, out string reason, ticket.mode ?? "resume", ticket.selected))
            { response.Reason = reason; return; }
            admitted.Add(request.ClientNetworkId);
            string requestedName = (ticket.name ?? "").Trim();
            requestedName = new string(Array.FindAll(requestedName.ToCharArray(), value => !char.IsControl(value)));
            names[request.ClientNetworkId] = requestedName.Length == 0 ? "Player " + request.ClientNetworkId : requestedName.Substring(0, Math.Min(16, requestedName.Length));
            if (NetworkGameWorld.Instance != null)
                names[request.ClientNetworkId] = NetworkGameWorld.Instance.CharacterName(request.ClientNetworkId,
                    requestedName.Length == 0 ? "" : names[request.ClientNetworkId]);
            response.Approved = response.CreatePlayerObject = true;
            response.Reason = "";
        }

        private void Connected(ulong id)
        {
            if (!manager.IsServer && id == manager.LocalClientId)
            {
                connectedLocally = true;
                if (loginMode == "create") loginMode = "resume";
                GameAudio.StartMusic();
                retryIndex = 0;
                retryAt = -1;
            }
            if (!manager.IsServer && id == manager.LocalClientId && pendingCharacterKey != null &&
                !demo && !Application.isBatchMode && !HasArgument("-pwa-probe"))
            {
                PlayerPrefs.SetString(pendingCharacterPreference, pendingCharacterKey);
                PlayerPrefs.SetString("pwa-display-name", displayName);
                PlayerPrefs.Save();
                recoveryKey = "";
            }
            status = "접속 완료 · WASD 이동";
            Debug.Log("PWA_CLIENT_CONNECTED id=" + id);
        }
        private void Disconnected(ulong id)
        {
            bool local = !manager.IsServer && id == manager.LocalClientId;
            string reason = manager.DisconnectReason;
            if (local)
            {
                bool shouldRetry = !loggingOut && CanReconnect && (connectedLocally || retryIndex > 0) &&
                    (string.IsNullOrEmpty(reason) || reason == "이미 이 캐릭터가 접속 중입니다.");
                connectedLocally = false;
                status = string.IsNullOrEmpty(reason) ? "연결이 종료되었습니다. 다시 접속할 수 있습니다." : reason;
                if (shouldRetry) QueueReconnect();
                if (loggingOut) FinishLogout();
            }
            if (manager.IsServer && NetworkGameWorld.Instance is NetworkGameWorld world && !world.Actors.ContainsKey(id))
                world.ReleaseCharacter(id);
            admitted.Remove(id);
            names.Remove(id);
            Debug.Log("PWA_CLIENT_DISCONNECTED id=" + id + " reason=" + (string.IsNullOrEmpty(reason) ? "transport" : reason));
        }
        private void QueueReconnect()
        {
            if (retryIndex >= RetryDelays.Length)
            {
                retryAt = -1;
                retryIndex = 0;
                status = "자동 재접속에 실패했습니다. 서버 접속을 다시 눌러주세요.";
                return;
            }
            retryAt = Time.realtimeSinceStartup + RetryDelays[retryIndex++];
            status = $"연결이 끊겨 재접속을 기다리는 중입니다 ({retryIndex}/{RetryDelays.Length}).";
            Debug.Log("PWA_RECONNECT_QUEUED attempt=" + retryIndex);
        }

        private void Update()
        {
            readiness?.Heartbeat(Ready, manager.ConnectedClientsIds.Count);
            if (loggingOut && Time.realtimeSinceStartup > logoutAt) { manager.Shutdown(); FinishLogout(); }
            if (retryAt < 0 || Time.realtimeSinceStartup < retryAt || connecting || manager.IsListening || manager.ShutdownInProgress) return;
            retryAt = -1;
            StartConnection();
        }
        private void OnDestroy()
        {
            connectionGeneration++;
            readiness?.Dispose();
            if (manager == null) return;
            manager.OnClientConnectedCallback -= Connected;
            manager.OnClientDisconnectCallback -= Disconnected;
        }
        public void Logout()
        {
            loggingOut = true; logoutAt = Time.realtimeSinceStartup + 2;
            connectionGeneration++; connecting = connectedLocally = false; retryAt = -1; retryIndex = 0;
            if (manager.IsConnectedClient) NetworkSandboxPlayer.LocalPlayer?.CommandRpc((byte)PartyCommand.Logout, 0, 0, 0);
            else { manager.Shutdown(); FinishLogout(); }
        }
        private void FinishLogout()
        {
            loggingOut = false; demo = false; selectClass = false; loginMode = "resume";
            pendingCharacterKey = lastCharacterKey = null; recoveryKey = "";
            status = "로그아웃했습니다. 데모 계정의 진행 내용은 삭제됩니다.";
        }
        private void OnGUI()
        {
            if (NetworkGameWorld.Instance != null && manager.IsConnectedClient) return;
            if (Application.isBatchMode || manager.IsServer) return;
            float scale = Mathf.Max(.5f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
            Matrix4x4 previous = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            GUILayout.BeginArea(new Rect((width - 680) / 2, (height - 600) / 2, 680, 600), GUI.skin.box);
            GUILayout.Space(16);
            GUILayout.Label("PERSISTENT WORLD · 4인 협동", new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter });
            GUILayout.Label(status);
            GUILayout.Space(12);
            if (!manager.IsListening)
            {
                if (selectClass)
                {
                    GUILayout.Label("처음 플레이할 직업을 선택하세요. 마을에서 변경할 수 있습니다.");
                    GUILayout.BeginHorizontal();
                    foreach (PersistentWorld.Player.CharacterClass type in Enum.GetValues(typeof(PersistentWorld.Player.CharacterClass)))
                    {
                        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(208));
                        GUILayout.Label(PersistentWorld.Player.SkillCatalog.ClassName(type), new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 });
                        Texture2D portrait = Resources.Load<Texture2D>("Characters/" + type + "InventoryIdle");
                        Rect picture = GUILayoutUtility.GetRect(190, 210);
                        if (portrait != null) GUI.DrawTexture(picture, portrait, ScaleMode.ScaleToFit, true);
                        GUI.enabled = !connecting && !manager.ShutdownInProgress;
                        if (GUI.Button(picture, GUIContent.none, GUIStyle.none) || GUILayout.Button("선택 · 플레이 시작", GUILayout.Height(38)))
                        { selectedClass = (int)type; selectClass = false; Connect(endpoint.Trim(), password); }
                        GUI.enabled = true; GUILayout.EndVertical();
                    }
                    GUILayout.EndHorizontal();
                    if (GUILayout.Button("뒤로")) selectClass = false;
                }
                else
                {
                    GUILayout.Label("서버 주소"); endpoint = GUILayout.TextField(endpoint, 200);
                    GUILayout.Label("닉네임"); displayName = GUILayout.TextField(displayName, 16);
                    GUILayout.Label("팀 참가 코드"); password = GUILayout.PasswordField(password, '*', 64);
                    GUILayout.Label("플레이어 코드 · 다른 PC에서도 같은 코드를 입력하면 진행 내용을 복구합니다.");
                    recoveryKey = GUILayout.PasswordField(recoveryKey, '*', 32);
                    GUI.enabled = !connecting && !manager.ShutdownInProgress && retryAt < 0;
                    if (GUILayout.Button("플레이어 코드로 로그인", GUILayout.Height(36)))
                    {
                        demo = false; loginMode = "restore";
                        if (recoveryKey.Trim().Length == 0 && Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out Uri savedServer))
                            recoveryKey = PlayerPrefs.GetString(CharacterPreference(savedServer), "");
                        if (recoveryKey.Trim().Length == 0) status = "저장된 플레이어 코드가 없습니다. 새 계정을 만들어주세요.";
                        else Connect(endpoint.Trim(), password);
                    }
                    if (GUILayout.Button("새 플레이어 만들기", GUILayout.Height(36)))
                    { demo = false; loginMode = "create"; recoveryKey = Guid.NewGuid().ToString("N"); selectClass = true; }
                    if (GUILayout.Button("데모 계정으로 플레이 · 로그아웃 시 삭제", GUILayout.Height(36)))
                    { demo = true; loginMode = "demo"; recoveryKey = Guid.NewGuid().ToString("N"); selectClass = true; }
                    GUI.enabled = true;
                    if (retryAt >= 0 && GUILayout.Button("자동 재접속 취소")) Logout();
                    GUILayout.Label("플레이어 코드는 비밀번호 역할을 합니다. 게임 메뉴에서 복사해 보관하세요.");
                }
            }
            else if (GUILayout.Button("접속 취소")) Logout();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("게임 종료", GUILayout.Height(34))) Application.Quit();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        private string CharacterKey(Uri server)
        {
            if (recoveryKey.Length != 0) return recoveryKey.Trim();
            if (Application.isBatchMode || HasArgument("-pwa-probe")) return Guid.NewGuid().ToString("N");
            string preference = CharacterPreference(server);
            string key = PlayerPrefs.GetString(preference, "");
            if (Guid.TryParseExact(key, "N", out Guid existing) && existing != Guid.Empty) return key;
            key = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(preference, key);
            PlayerPrefs.Save();
            return key;
        }

        private static string CharacterPreference(Uri server) =>
            "pwa-character-" + NetworkGameWorld.HashKey(server.GetLeftPart(UriPartial.Authority));
    }
}
