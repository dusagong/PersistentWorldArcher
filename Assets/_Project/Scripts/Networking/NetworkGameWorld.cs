using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using PersistentWorld.Combat;
using PersistentWorld.Core;
using PersistentWorld.Items;
using PersistentWorld.Player;
using PersistentWorld.World;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PersistentWorld.Networking
{
    [DefaultExecutionOrder(-200)]
    public sealed class NetworkGameWorld : MonoBehaviour
    {
        public static NetworkGameWorld Instance { get; private set; }
        public NetworkManager manager;
        public GameObject actorPrefab, bossPrefab;
        public Transform[] maps;
        public NetworkGameAssets assets;
        public Camera gameCamera;
        public Color[] mapBackgrounds;
        public float[] mapCameraSizes;
        public Vector2[] mapSpawns;
        public readonly PartyState Parties = new PartyState();
        public readonly Dictionary<ulong, ArcherController> Actors = new Dictionary<ulong, ArcherController>();
        public GameFrame Frame { get; private set; }
        public PlayerFrame Local => Frame?.players?.FirstOrDefault(p => p.id == manager.LocalClientId);
        public bool BossBattleActive => Parties.BossParty != 0 && CurrentBoss != null && !CurrentBoss.GetComponent<Health>().IsDead;
        public BossController CurrentBoss { get; private set; }
        public int ValidationStage { get; set; }
        private readonly HashSet<SpriteRenderer> scenery = new HashSet<SpriteRenderer>();
        private readonly Dictionary<Texture2D, int> textureIds = new Dictionary<Texture2D, int>();
        private readonly Dictionary<ulong, string> messages = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, float> messageTimes = new Dictionary<ulong, float>();
        private ServerWorldEntity boss;
        private NetworkGameView view;
        private NetworkPartyHud hud;
        private float nextFrame;
        private int previousBossParty;
        private int viewedArea = -1;
        public static Vector2 Origin(GameArea area) => Vector2.right * ((int)area * 1000);
        public static GameArea AreaAt(Vector2 position) => (GameArea)Mathf.Clamp(Mathf.RoundToInt(position.x / 1000), 0, 2);
        public static bool SameArea(Vector2 a, Vector2 b) => Instance == null || AreaAt(a) == AreaAt(b);

        private void Awake()
        {
            Debug.Log("PWA_WORLD_INITIALIZING");
            Instance = this;
            Application.runInBackground = true;
            for (int i = 0; i < assets.textures.Length; i++) if (assets.textures[i] != null) textureIds[assets.textures[i]] = i;
            bool server = NetworkSandbox.HasArgument("-pwa-server");
#if UNITY_SERVER && !UNITY_EDITOR
            server = true;
#endif
            gameCamera.enabled = !server;
            foreach (Transform map in maps)
            {
                foreach (SpriteRenderer sprite in map.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sprite.GetComponentInParent<ServerWorldEntity>() == null) scenery.Add(sprite);
                foreach (Collider2D collider in map.GetComponentsInChildren<Collider2D>(true)) collider.enabled = server && collider.enabled;
                foreach (Rigidbody2D body in map.GetComponentsInChildren<Rigidbody2D>(true)) if (!server) body.simulated = false;
                foreach (ServerWorldEntity entity in map.GetComponentsInChildren<ServerWorldEntity>(true))
                {
                    entity.SetSimulation(false);
                    if (!server) foreach (Renderer renderer in entity.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                    if (entity.GetComponent<BossController>() != null) { boss = entity; CurrentBoss = entity.GetComponent<BossController>(); }
                }
            }
            manager.OnServerStarted += ServerStarted;
            manager.OnClientConnectedCallback += ClientConnected;
            if (!server)
            {
                for (int i = 1; i < maps.Length; i++) maps[i].gameObject.SetActive(false);
                view = new NetworkGameView(assets, new GameObject("Server Render Proxies").transform);
                hud = gameObject.AddComponent<NetworkPartyHud>();
            }
            Debug.Log("PWA_WORLD_INITIALIZED server=" + server);
        }
        private void ServerStarted()
        {
            foreach (ServerWorldEntity entity in FindObjectsByType<ServerWorldEntity>(FindObjectsSortMode.None))
                if (entity.area != GameArea.BossArena) entity.SetSimulation(true);
        }
        private void ClientConnected(ulong id)
        {
            if (!manager.IsServer && id == manager.LocalClientId)
                manager.CustomMessagingManager.RegisterNamedMessageHandler("GameFrame", Receive);
        }
        private void Start()
        {
            if (view != null)
                foreach (ServerWorldEntity entity in FindObjectsByType<ServerWorldEntity>(FindObjectsSortMode.None))
                    foreach (Renderer renderer in entity.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }
        public void Spawn(NetworkSandboxPlayer pawn)
        {
            if (!manager.IsServer) return;
            ArcherController actor = Instantiate(actorPrefab).GetComponent<ArcherController>();
            NetworkActorContext context = actor.GetComponent<NetworkActorContext>();
            context.ClientId = pawn.OwnerClientId; context.Area = GameArea.Town;
            actor.AcceptLocalInput = false; actor.name = "Player " + pawn.OwnerClientId;
            actor.GetComponent<PlayerEquipment>().Bind(PlayerInventoryProfile.CreateNew());
            actor.transform.position = TownSpawn(pawn.OwnerClientId);
            ActorShadow.Attach(actor.transform, .62f, 0);
            Actors[pawn.OwnerClientId] = actor;
            Parties.Add(pawn.OwnerClientId, manager.GetComponent<NetworkSandbox>().PlayerName(pawn.OwnerClientId));
            pawn.Position.Value = actor.transform.position;
        }
        public void Remove(ulong id)
        {
            Parties.Remove(id);
            if (Actors.TryGetValue(id, out ArcherController actor)) Destroy(actor.gameObject);
            Actors.Remove(id); messages.Remove(id); messageTimes.Remove(id);
        }
        public Transform NearestLivingPlayer(Vector2 position)
        {
            return Actors.Values.Where(p => p != null && !p.Health.IsDead && SameArea(position, p.transform.position))
                .OrderBy(p => Vector2.SqrMagnitude((Vector2)p.transform.position - position)).FirstOrDefault()?.transform;
        }
        public void Input(ulong id, Vector2 movement, Vector2 aim, Vector2 target, int buttons)
        {
            if (!Actors.TryGetValue(id, out ArcherController actor)) return;
            if (!Finite(movement) || !Finite(aim) || !Finite(target)) return;
            actor.SetExternalMovement(movement, aim);
            actor.Skills.UpdateTarget(target);
            if (actor.Health.IsDead) return;
            if ((buttons & 4) != 0) actor.TryBackstep();
            if ((buttons & 8) != 0) actor.Skills.BeginSkill(0, target);
            if ((buttons & 16) != 0) actor.Skills.BeginSkill(1, target);
            if ((buttons & 32) != 0) actor.Skills.BeginSkill(2, target);
            if ((buttons & 64) != 0) actor.Skills.ReleaseSkill(0);
            if ((buttons & 128) != 0) actor.Skills.ReleaseSkill(1);
            if ((buttons & 256) != 0) actor.Skills.ReleaseSkill(2);
            if ((buttons & 2) != 0) actor.Skills.TrySpecial();
            if ((buttons & 1) != 0) actor.TryBasicAttack();
            if ((buttons & 512) != 0 && !actor.Skills.IsHolding) Interact(id);
        }
        public static bool Finite(Vector2 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
        public void Command(ulong id, PartyCommand command, ulong target, int first, int second)
        {
            if (!Actors.TryGetValue(id, out ArcherController actor)) return;
            string reason = "";
            switch (command)
            {
                case PartyCommand.Create: Parties.Create(id, out reason); break;
                case PartyCommand.Invite: Parties.Invite(id, target, Time.unscaledTime, out reason); break;
                case PartyCommand.Accept: Parties.Respond(id, true, Time.unscaledTime, out reason); break;
                case PartyCommand.Decline: Parties.Respond(id, false, Time.unscaledTime, out reason); break;
                case PartyCommand.Leave:
                    bool inBoss = Parties.Members[id].Area == GameArea.BossArena;
                    if (Parties.Leave(id, out reason) && inBoss) ReturnToTown(id);
                    break;
                case PartyCommand.Interact: Interact(id); break;
                case PartyCommand.Equip:
                    if (actor.Health.IsDead) { reason = "사망 중에는 스킬을 바꿀 수 없습니다."; break; }
                    reason = actor.Loadout.Equip(first, (SkillId)second) ? "스킬 장착을 변경했습니다." : "스킬 교체는 마을에서만 가능합니다. 중복·다른 직업 스킬도 장착할 수 없습니다.";
                    break;
                case PartyCommand.ChangeClass:
                    if (!actor.Loadout.CanChangeSkills || actor.Health.IsDead || !Enum.IsDefined(typeof(CharacterClass), first)) { reason = "마을에서만 직업을 바꿀 수 있습니다."; break; }
                    actor.Loadout.SelectClass((CharacterClass)first);
                    actor.GetComponent<PlayerEquipment>().Bind(PlayerInventoryProfile.CreateNew(ItemSession.ToItemClass((CharacterClass)first)));
                    reason = "직업을 변경했습니다."; break;
                case PartyCommand.Menu: actor.SetMenuOpen(first != 0); break;
            }
            if (!string.IsNullOrEmpty(reason)) Message(id, reason);
        }
        public void Message(ulong id, string text) { messages[id] = text; messageTimes[id] = Time.unscaledTime + 4; }
        public string LastMessage(ulong id) => messages.TryGetValue(id, out string text) ? text : "";
        private void Interact(ulong id)
        {
            ArcherController actor = Actors[id];
            ScenePortal portal = FindObjectsByType<ScenePortal>(FindObjectsSortMode.None)
                .Where(p => Vector2.Distance(p.transform.position, actor.transform.position) <= 1.3f).OrderBy(p => Vector2.Distance(p.transform.position, actor.transform.position)).FirstOrDefault();
            if (portal != null) { UsePortal(id, portal); return; }
            NpcDialogue npc = FindObjectsByType<NpcDialogue>(FindObjectsSortMode.None)
                .FirstOrDefault(p => Vector2.Distance(p.transform.position, actor.transform.position) <= 1.3f);
            if (npc != null) { npc.Interact(actor.gameObject); Message(id, DialogueState.Speaker + ": " + DialogueState.Line); DialogueState.Clear(); }
        }
        public void UsePortal(ulong id, ScenePortal portal)
        {
            if (!manager.IsServer || !Actors.TryGetValue(id, out ArcherController actor) || actor.Health.IsDead ||
                Vector2.Distance(actor.transform.position, portal.transform.position) > 1.3f) return;
            if (portal.Destination == "BossArena")
            {
                SyncMembers();
                if (!Parties.EnterBoss(id, out ulong[] entering, out string reason)) { Message(id, reason); return; }
                for (int i = 0; i < entering.Length; i++) Teleport(entering[i], GameArea.BossArena, mapSpawns[(int)GameArea.BossArena] + Vector2.right * (.6f * i));
                boss.SetSimulation(true); previousBossParty = Parties.BossParty;
                foreach (ulong member in entering) Message(member, reason);
            }
            else if (Parties.Members[id].Area == GameArea.BossArena)
            {
                foreach (PartyMember member in Parties.Group(Parties.Members[id].Party))
                {
                    if (member.Area != GameArea.BossArena) continue;
                    if (portal.Destination == "Field" && !Actors[member.Id].Health.IsDead)
                        Teleport(member.Id, GameArea.Field, mapSpawns[(int)GameArea.Field]);
                    else ReturnToTown(member.Id);
                }
            }
            else if (portal.Destination == "Field") Teleport(id, GameArea.Field, mapSpawns[(int)GameArea.Field]);
            else if (portal.Destination == "Town") ReturnToTown(id);
        }
        public void ReturnToTown(ulong id)
        {
            if (!Actors.TryGetValue(id, out ArcherController actor)) return;
            Teleport(id, GameArea.Town, TownSpawn(id));
            if (actor.Health.IsDead) actor.Health.Revive(1, 3); else actor.Health.Heal(actor.Health.Maximum);
            actor.Mana.RestoreAtTown();
        }
        private Vector2 TownSpawn(ulong id) => mapSpawns[(int)GameArea.Town] + new Vector2((int)(id % 4) - 1.5f, 0);
        public void Teleport(ulong id, GameArea area, Vector2 local)
        {
            if (!Actors.TryGetValue(id, out ArcherController actor)) return;
            actor.Skills.Cancel(); actor.SetMenuOpen(false); actor.SetExternalMovement(Vector2.zero, Vector2.right);
            actor.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            actor.transform.position = Origin(area) + local; actor.GetComponent<NetworkActorContext>().Area = area;
            Parties.Move(id, area); Physics2D.SyncTransforms();
        }
        private void SyncMembers()
        {
            foreach (var pair in Actors) if (Parties.Members.TryGetValue(pair.Key, out PartyMember member)) member.Alive = !pair.Value.Health.IsDead;
        }
        private void Update()
        {
            if (!manager.IsListening) { Frame = null; viewedArea = -1; view?.Clear(); return; }
            if (!manager.IsServer) return;
            SyncMembers(); Parties.Expire(Time.unscaledTime);
            if (Parties.BossParty != 0)
            {
                PartyMember[] fighting = Parties.Group(Parties.BossParty).Where(p => p.Area == GameArea.BossArena).ToArray();
                if (fighting.Length > 0 && fighting.All(p => !p.Alive)) foreach (PartyMember member in fighting) ReturnToTown(member.Id);
            }
            if (previousBossParty != 0 && Parties.BossParty == 0) ResetBoss();
            previousBossParty = Parties.BossParty;
            if (Time.unscaledTime < nextFrame) return;
            nextFrame = Time.unscaledTime + .1f;
            var renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            var lineRenderers = FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
            var frames = new GameFrame[maps.Length];
            foreach (ulong recipient in Actors.Keys)
            {
                if (!manager.ConnectedClients.ContainsKey(recipient)) continue;
                int area = (int)Parties.Members[recipient].Area;
                GameFrame frame = frames[area] ??= Capture((GameArea)area, renderers, lineRenderers);
                frame.message = messageTimes.TryGetValue(recipient, out float until) && until > Time.unscaledTime ? messages[recipient] : "";
                frame.inviteRemaining = 0;
                frame.inviter = ulong.MaxValue;
                frame.inviterName = null;
                if (Parties.Invitations.TryGetValue(recipient, out PartyInvitation invitation))
                {
                    frame.inviter = invitation.From;
                    frame.inviterName = Parties.Members[invitation.From].Name;
                    frame.inviteRemaining = (float)(invitation.Expires - Time.unscaledTime);
                }
                byte[] data = Compress(JsonUtility.ToJson(frame));
                if (data.Length > 100000) { Debug.LogWarning("Game frame exceeded bandwidth limit."); continue; }
                using FastBufferWriter writer = new FastBufferWriter(data.Length + 4, Allocator.Temp);
                writer.WriteValueSafe(data.Length); writer.WriteBytesSafe(data);
                manager.CustomMessagingManager.SendNamedMessage("GameFrame", recipient, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }
        private void ResetBoss()
        {
            if (boss != null) { boss.SetSimulation(false); Destroy(boss.gameObject); }
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                if (root != gameObject && root.transform.parent == null && root.GetComponent<NetworkObject>() == null &&
                    root.GetComponent<NetworkActorContext>() == null && AreaAt(root.transform.position) == GameArea.BossArena &&
                    !maps.Any(m => m.gameObject == root)) Destroy(root);
            boss = Instantiate(bossPrefab, maps[(int)GameArea.BossArena]).GetComponent<ServerWorldEntity>();
            boss.SetSimulation(false); CurrentBoss = boss.GetComponent<BossController>();
        }
        private GameFrame Capture(GameArea area, SpriteRenderer[] renderers, LineRenderer[] lineRenderers)
        {
            List<SpriteFrame> visuals = new List<SpriteFrame>();
            foreach (SpriteRenderer renderer in renderers)
            {
                if (!renderer.enabled || renderer.sprite == null || scenery.Contains(renderer) || AreaAt(renderer.transform.position) != area || renderer.GetComponent<NetworkSandboxPlayer>() != null) continue;
                Sprite sprite = renderer.sprite;
                int texture = -1;
                if (textureIds.TryGetValue(sprite.texture, out int index)) texture = index;
                else if (renderer.GetComponent<ActorShadow>() != null) texture = -2;
                else if (sprite.name == GroundBurst.CircleSpriteName) texture = -3;
                visuals.Add(new SpriteFrame { id = renderer.GetInstanceID(), texture = texture, rect = sprite.rect, layer = renderer.sortingLayerID,
                    pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height), ppu = sprite.pixelsPerUnit, order = renderer.sortingOrder,
                    position = renderer.transform.position, scale = renderer.transform.lossyScale, angle = renderer.transform.eulerAngles.z,
                    color = renderer.color, flipX = renderer.flipX, flipY = renderer.flipY });
                if (visuals.Count >= 300) break;
            }
            List<LineFrame> lines = new List<LineFrame>();
            foreach (LineRenderer line in lineRenderers)
            {
                if (!line.enabled || line.positionCount == 0 || line.positionCount > 128) continue;
                Vector3[] points = new Vector3[line.positionCount]; line.GetPositions(points);
                if (!line.useWorldSpace) for (int i = 0; i < points.Length; i++) points[i] = line.transform.TransformPoint(points[i]);
                if (AreaAt(points[0]) != area) continue;
                lines.Add(new LineFrame { id = line.GetInstanceID(), points = points, width = line.startWidth, color = line.startColor, layer = line.sortingLayerID, order = line.sortingOrder, loop = line.loop });
                if (lines.Count >= 100) break;
            }
            PlayerFrame[] players = Actors.Select(pair =>
            {
                ArcherController actor = pair.Value; PartyMember member = Parties.Members[pair.Key];
                return new PlayerFrame { id = pair.Key, name = member.Name, party = member.Party, area = (int)member.Area,
                    hp = actor.Health.Current, maximum = actor.Health.Maximum, mp = actor.Mana.Current,
                    character = (int)actor.Loadout.Class, slots = actor.Loadout.Slots.Select(s => (int)s).ToArray(),
                    cooldowns = actor.Loadout.Slots.Select(s => actor.Skills.Cooldown(s)).ToArray(), charge = actor.Skills.Charge,
                    held = (int)actor.Skills.HoldingSkill, special = actor.Skills.SpecialRemaining,
                    position = actor.transform.position, leader = Parties.Leader(member.Party) == pair.Key };
            }).ToArray();
            GameFrame frame = new GameFrame { players = players, sprites = visuals.ToArray(), lines = lines.ToArray(), bossParty = Parties.BossParty, validationStage = ValidationStage,
                bossHp = CurrentBoss != null ? CurrentBoss.GetComponent<Health>().Current : 0, bossMax = CurrentBoss != null ? CurrentBoss.GetComponent<Health>().Maximum : 0,
                bossPhaseTwo = CurrentBoss != null && CurrentBoss.IsPhaseTwo };
            if (area == GameArea.BossArena)
                frame.bossNotice = FindObjectsByType<TextMesh>(FindObjectsSortMode.None).FirstOrDefault(t => t.text.Contains("즉사기") && SameArea(t.transform.position, Origin(area)))?.text;
            return frame;
        }
        private void Receive(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || manager.IsServer) return;
            reader.ReadValueSafe(out int size);
            if (size < 0 || size > 100000 || reader.Length - reader.Position < size) return;
            byte[] data = new byte[size]; reader.ReadBytesSafe(ref data, size);
            try
            {
                Frame = JsonUtility.FromJson<GameFrame>(Decompress(data));
                PlayerFrame local = Local;
                if (local == null) return;
                if (viewedArea != local.area)
                {
                    viewedArea = local.area;
                    view.Clear();
                    gameCamera.transform.position = CameraTarget(local);
                    gameCamera.backgroundColor = mapBackgrounds[viewedArea];
                    gameCamera.orthographicSize = mapCameraSizes[viewedArea];
                    for (int i = 0; i < maps.Length; i++) maps[i].gameObject.SetActive(i == viewedArea);
                }
                view.Apply(Frame);
            }
            catch (Exception error) { Debug.LogWarning("Game frame rejected: " + error.Message); }
        }
        private static Vector3 CameraTarget(PlayerFrame local) => new Vector3(local.position.x, local.position.y + .4f, -10);
        private void LateUpdate()
        {
            if (view == null || Local is not PlayerFrame local) return;
            float blend = 1 - Mathf.Exp(-20 * Time.unscaledDeltaTime);
            view.Animate(blend);
            Vector3 target = CameraTarget(local);
            Transform cameraBody = gameCamera.transform;
            cameraBody.position = Vector3.Lerp(cameraBody.position, target, (cameraBody.position - target).sqrMagnitude > 25 ? 1 : blend);
        }
        public static byte[] Compress(string json)
        {
            using MemoryStream output = new MemoryStream();
            using (GZipStream gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Fastest, true))
            { byte[] data = Encoding.UTF8.GetBytes(json); gzip.Write(data, 0, data.Length); }
            return output.ToArray();
        }
        public static string Decompress(byte[] data)
        {
            using MemoryStream input = new MemoryStream(data); using GZipStream gzip = new GZipStream(input, CompressionMode.Decompress);
            using StreamReader reader = new StreamReader(gzip, Encoding.UTF8); return reader.ReadToEnd();
        }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (manager != null) { manager.OnServerStarted -= ServerStarted; manager.OnClientConnectedCallback -= ClientConnected; }
            view?.Clear();
        }
    }
}
