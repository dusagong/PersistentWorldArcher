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
    public sealed partial class NetworkGameWorld : MonoBehaviour
    {
        public static NetworkGameWorld Instance { get; private set; }
        public NetworkManager manager;
        public GameObject actorPrefab, bossPrefab, wolfPrefab;
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
        // 늑대 보스방(두 번째 필드의 보스)
        public WolfBossController CurrentWolf { get; private set; }
        public bool BattleActiveIn(GameArea area) => area == GameArea.WolfArena
            ? Parties.RoomParty(GameArea.WolfArena) != 0 && CurrentWolf != null && !CurrentWolf.GetComponent<Health>().IsDead
            : BossBattleActive;
        public int ValidationStage { get; set; }
        private readonly HashSet<SpriteRenderer> scenery = new HashSet<SpriteRenderer>();
        private readonly Dictionary<ulong, string> messages = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, float> messageTimes = new Dictionary<ulong, float>();
        private ServerWorldEntity boss, wolf;
        // 늑대 이펙트 신호: 그림 보간과 같은 시각(0.1초 늦게)에 재생하려고 잠깐 모아 둔다
        private readonly List<EffectFrame> pendingWolfFx = new List<EffectFrame>();
        private GameArea bossResultArea;
        private int previousWolfParty;
        private NetworkGameView view;
        public Vector3 RenderedPosition(int visual, Vector3 fallback) => view != null ? view.Position(visual, fallback) : fallback;
        private NetworkPartyHud hud;
        private float nextFrame;
        private int hitSequence;
        private readonly List<HitFrame> recentHits = new List<HitFrame>();
        private int lastHit;
        // 보이기만 하는 연출 신호: 서버는 기록만 하고, 클라이언트가 받아서 직접 그린다
        private int effectSequence, lastEffect;
        private readonly List<EffectFrame> recentEffects = new List<EffectFrame>();
        private PersistentWorld.World.DoomEffect clientDoom;
        // 보스를 잡으면 만든 딜량표. 참가자가 보스방에 있는 동안 그 사람의 프레임에 싣는다.
        private BossResultFrame bossResult;
        private static int bossResultSequence;

        public static bool IsServerWorld => Instance != null && Instance.manager != null && Instance.manager.IsServer;

        // 온라인 서버면 연출 신호를 기록하고 true. 그 외(혼자 하기·클라이언트)는 false라 호출한 쪽이 직접 그린다.
        public static bool RecordEffect(EffectFrame effect)
        {
            if (!IsServerWorld) return false;
            effect.sequence = ++Instance.effectSequence;
            effect.time = Time.unscaledTimeAsDouble;
            Instance.recentEffects.Add(effect);
            return true;
        }

        public Sprite[] BossDebris => bossPrefab != null && bossPrefab.GetComponent<BossController>() is BossController boss ? boss.QuakeDebris : null;

        private void PlayEffect(EffectFrame effect)
        {
            switch (effect.kind)
            {
                case EffectFrame.Burst:
                    PersistentWorld.World.GroundBurst.Replay(effect, effect.debris == 1 ? BossDebris : null);
                    break;
                case EffectFrame.DoomBegin:
                    if (clientDoom != null) Destroy(clientDoom.gameObject);
                    clientDoom = PersistentWorld.World.DoomEffect.Begin(null, effect.position, effect.direction, effect.size, Time.time + effect.duration);
                    break;
                case EffectFrame.DoomDetonate:
                    if (clientDoom != null) clientDoom.Detonate();
                    clientDoom = null;
                    break;
                case EffectFrame.DoomCancel:
                    if (clientDoom != null) Destroy(clientDoom.gameObject);
                    clientDoom = null;
                    break;
                case EffectFrame.WolfFx:
                    WolfFxNet.Replay(effect);
                    break;
                case EffectFrame.ChargeWarn:
                    // 신호가 기록된 뒤 이 화면 정보가 만들어지기까지 지난 시간(서버 기준)만큼 경고를 줄여 보스 출발과 맞춘다
                    if (Frame != null) effect.duration -= Mathf.Max(0f, (float)(Frame.time - effect.time));
                    PersistentWorld.World.ChargeWarning.Replay(effect);
                    break;
            }
        }
        private readonly Dictionary<ulong, int> tutorialProgress = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, int> attackSequences = new Dictionary<ulong, int>();
        private int previousBossParty;
        private int viewedArea = -1;
        public static Vector2 Origin(GameArea area) => Vector2.right * ((int)area * 1000);
        public static GameArea AreaAt(Vector2 position) => (GameArea)Mathf.Clamp(Mathf.RoundToInt(position.x / 1000), 0, (int)GameArea.WolfArena);
        public static bool SameArea(Vector2 a, Vector2 b) => Instance == null || AreaAt(a) == AreaAt(b);

        private void Awake()
        {
            Debug.Log("PWA_WORLD_INITIALIZING");
            Instance = this;
            Health.DamageApplied += RecordHit;
            Application.runInBackground = true;
            bool server = NetworkSandbox.HasArgument("-pwa-server");
#if UNITY_SERVER && !UNITY_EDITOR
            server = true;
#endif
            gameCamera.enabled = !server;
            PrepareProgress(server);
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
                    if (entity.GetComponent<WolfBossController>() != null) { wolf = entity; CurrentWolf = entity.GetComponent<WolfBossController>(); }
                    else if (entity.GetComponent<BossController>() != null) { boss = entity; CurrentBoss = entity.GetComponent<BossController>(); }
                }
            }
            manager.OnServerStarted += ServerStarted;
            manager.OnClientConnectedCallback += ClientConnected;
            if (!server)
            {
                Application.targetFrameRate = 60;
                for (int i = 1; i < maps.Length; i++) maps[i].gameObject.SetActive(false);
                view = new NetworkGameView(assets, new GameObject("Server Render Proxies").transform);
                hud = gameObject.AddComponent<NetworkPartyHud>();
                if (!Application.isBatchMode || NetworkSandbox.HasArgument("-pwa-capture"))
                    gameObject.AddComponent<PersistentWorld.UI.CombatHud>().BindOnline(this, hud);
            }
            Debug.Log("PWA_WORLD_INITIALIZED server=" + server);
        }
        private void ServerStarted()
        {
            CurrentBoss.GetComponent<Health>().Died += AwardBoss;
            if (CurrentWolf != null) CurrentWolf.GetComponent<Health>().Died += AwardWolf;
            foreach (ServerWorldEntity entity in FindObjectsByType<ServerWorldEntity>(FindObjectsSortMode.None))
            {
                if (entity.area == GameArea.Field || entity.area == GameArea.Field2) PrepareFieldEnemy(entity);
                if (!entity.area.IsBoss()) entity.SetSimulation(true);
            }
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
            BindCharacter(pawn.OwnerClientId, actor);
            actor.transform.position = TownSpawn(pawn.OwnerClientId);
            ActorShadow.Attach(actor.transform, .62f, 0);
            Actors[pawn.OwnerClientId] = actor;
            Parties.Add(pawn.OwnerClientId, manager.GetComponent<NetworkSandbox>().PlayerName(pawn.OwnerClientId));
            if (Application.isBatchMode && NetworkSandbox.HasArgument("-pwa-field-soak-validation"))
            {
                GameArea validationArea = NetworkSandbox.Argument("-pwa-validation-area") == "Field2" ? GameArea.Field2 : GameArea.Field;
                Teleport(pawn.OwnerClientId, validationArea, mapSpawns[(int)validationArea]);
            }
            pawn.Position.Value = actor.transform.position;
        }
        public void Remove(ulong id)
        {
            ClearPractice(id);
            Parties.Remove(id);
            if (Actors.TryGetValue(id, out ArcherController actor)) Destroy(actor.gameObject);
            Actors.Remove(id); messages.Remove(id); messageTimes.Remove(id);
            tutorialProgress.Remove(id);
            attackSequences.Remove(id);
            ReleaseCharacter(id);
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
            if (movement.sqrMagnitude > .01f) RecordPractice(id, 1);
            if ((buttons & 1024) != 0) ItemAction(id, ItemCommand.UsePotion, "", 0);
            if ((buttons & 2048) != 0) ItemAction(id, ItemCommand.UsePotion, "", 1);
            if ((buttons & 4) != 0 && actor.TryBackstep()) RecordPractice(id, 4);
            if ((buttons & 8) != 0 && actor.Skills.BeginSkill(0, target)) RecordPractice(id, 8);
            if ((buttons & 16) != 0 && actor.Skills.BeginSkill(1, target)) RecordPractice(id, 8);
            if ((buttons & 32) != 0 && actor.Skills.BeginSkill(2, target)) RecordPractice(id, 8);
            if ((buttons & 64) != 0) actor.Skills.ReleaseSkill(0);
            if ((buttons & 128) != 0) actor.Skills.ReleaseSkill(1);
            if ((buttons & 256) != 0) actor.Skills.ReleaseSkill(2);
            if ((buttons & 2) != 0 && actor.Skills.TrySpecial()) RecordPractice(id, 16);
            if ((buttons & 1) != 0 && actor.TryBasicAttack())
            { attackSequences.TryGetValue(id, out int sequence); attackSequences[id] = sequence + 1; RecordPractice(id, 2); }
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
                    bool inBoss = Parties.Members[id].Area.IsBoss();
                    if (Parties.Leave(id, out reason) && inBoss) ReturnToTown(id);
                    break;
                case PartyCommand.Interact: Interact(id); break;
                case PartyCommand.Equip:
                    if (actor.Health.IsDead) { reason = "사망 중에는 스킬을 바꿀 수 없습니다."; break; }
                    reason = actor.Loadout.Equip(first, (SkillId)second) ? "스킬 장착을 변경했습니다." : "스킬 교체는 마을에서만 가능합니다. 중복·다른 직업 스킬도 장착할 수 없습니다.";
                    break;
                case PartyCommand.ChangeClass:
                    if (!actor.Loadout.CanChangeSkills || actor.Health.IsDead || !Enum.IsDefined(typeof(CharacterClass), first)) { reason = "마을에서만 직업을 바꿀 수 있습니다."; break; }
                    ChangeOnlineClass(id, actor, (CharacterClass)first);
                    reason = "직업을 변경했습니다."; break;
                case PartyCommand.LearnBossSkill:
                    if (!actor.Health.IsDead && actor.Loadout.CanChangeSkills && actor.Loadout.UseBossSkillBook())
                        reason = "보스 스킬을 배웠습니다.";
                    break;
                case PartyCommand.Menu: actor.SetMenuOpen(first != 0); break;
                case PartyCommand.Tutorial:
                    if (Parties.Members[id].Area.IsBoss() || actor.Health.IsDead)
                    { reason = "보스전 또는 사망 중에는 연습장에 입장할 수 없습니다."; break; }
                    if (Parties.Members[id].Area == GameArea.Tutorial) ReturnToTown(id);
                    else
                    {
                        Teleport(id, GameArea.Tutorial, PracticeSpawn(id));
                        ResetPractice(id);
                    }
                    break;
                case PartyCommand.ResetPractice:
                    if (Parties.Members[id].Area == GameArea.Tutorial && !actor.Health.IsDead) ResetPractice(id);
                    break;
                case PartyCommand.Logout:
                    characters[id].Forget = true;
                    manager.DisconnectClient(id, "로그아웃했습니다.");
                    return;
            }
            if (!string.IsNullOrEmpty(reason)) Message(id, reason);
        }
        public void Message(ulong id, string text) { messages[id] = text; messageTimes[id] = Time.unscaledTime + 4; }
        public string LastMessage(ulong id) => messages.TryGetValue(id, out string text) ? text : "";
        private void Interact(ulong id)
        {
            ArcherController actor = Actors[id];
            if (actor.Health.IsDead) return;
            if (NearSmith(id, actor, 1.3f))
            {
                smithRequests.TryGetValue(id, out int request);
                smithRequests[id] = request + 1;
                return;
            }
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
            if (portal.Destination == "BossArena" || portal.Destination == "WolfArena")
            {
                GameArea room = portal.Destination == "WolfArena" ? GameArea.WolfArena : GameArea.BossArena;
                ServerWorldEntity entity = room == GameArea.WolfArena ? wolf : boss;
                if (entity == null) { Message(id, "보스가 아직 준비되지 않았습니다."); return; }
                SyncMembers();
                if (!Parties.EnterBoss(id, room, out ulong[] entering, out string reason)) { Message(id, reason); return; }
                for (int i = 0; i < entering.Length; i++) Teleport(entering[i], room, mapSpawns[(int)room] + Vector2.right * (.6f * i));
                entity.SetSimulation(true);
                if (room == GameArea.WolfArena) previousWolfParty = Parties.RoomParty(room); else previousBossParty = Parties.BossParty;
                foreach (ulong member in entering) Message(member, reason);
            }
            else if (Parties.Members[id].Area.IsBoss())
            {
                // 보스방에서 나가는 문: 그 방의 파티원이 함께 나간다 (살아 있으면 문이 가리키는 필드로, 아니면 마을로)
                GameArea room = Parties.Members[id].Area;
                GameArea? field = portal.Destination == "Field" ? GameArea.Field : portal.Destination == "Field2" ? GameArea.Field2 : (GameArea?)null;
                foreach (PartyMember member in Parties.Group(Parties.Members[id].Party))
                {
                    if (member.Area != room) continue;
                    if (field is GameArea target && !Actors[member.Id].Health.IsDead)
                        Teleport(member.Id, target, mapSpawns[(int)target]);
                    else ReturnToTown(member.Id);
                }
            }
            else if (portal.Destination == "Field") Teleport(id, GameArea.Field, mapSpawns[(int)GameArea.Field]);
            else if (portal.Destination == "Field2") Teleport(id, GameArea.Field2, mapSpawns[(int)GameArea.Field2]);
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
            if (area != GameArea.Tutorial) ClearPractice(id);
            actor.Skills.Cancel(); actor.SetMenuOpen(false); actor.SetExternalMovement(Vector2.zero, Vector2.right);
            actor.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            actor.transform.position = Origin(area) + local; actor.GetComponent<NetworkActorContext>().Area = area;
            actor.Health.Invulnerable = area == GameArea.Tutorial;
            Parties.Move(id, area); Physics2D.SyncTransforms();
        }
        private void SyncMembers()
        {
            foreach (var pair in Actors) if (Parties.Members.TryGetValue(pair.Key, out PartyMember member)) member.Alive = !pair.Value.Health.IsDead;
        }
        private void Update()
        {
            if (!manager.IsListening)
            {
                Frame = null;
                viewedArea = -1;
                view?.Clear();
                WolfPuppet.Remove();
                pendingWolfFx.Clear();
                ApplyInventory(null);
                clientSmithRequest = 0;
                return;
            }
            if (!manager.IsServer) return;
            if (Time.unscaledTime >= nextSave)
            {
                nextSave = Time.unscaledTime + 1;
                SaveProgress();
            }
            if (Application.isBatchMode && NetworkSandbox.HasArgument("-pwa-field-soak-validation"))
                foreach (var pair in Actors)
                    if (Parties.Members[pair.Key].Area == GameArea.Town && !pair.Value.Health.IsDead)
                    {
                        GameArea area = NetworkSandbox.Argument("-pwa-validation-area") == "Field2" ? GameArea.Field2 : GameArea.Field;
                        Teleport(pair.Key, area, mapSpawns[(int)area]);
                    }
            SyncMembers(); Parties.Expire(Time.unscaledTime);
            // 보스방마다: 들어간 파티가 전멸하면 마을로
            foreach (GameArea room in GameAreas.BossRooms)
            {
                int party = Parties.RoomParty(room);
                if (party == 0) continue;
                PartyMember[] fighting = Parties.Group(party).Where(p => p.Area == room).ToArray();
                if (fighting.Length > 0 && fighting.All(p => !p.Alive)) foreach (PartyMember member in fighting) ReturnToTown(member.Id);
            }
            if (previousBossParty != 0 && Parties.BossParty == 0) ResetBoss();
            previousBossParty = Parties.BossParty;
            int wolfParty = Parties.RoomParty(GameArea.WolfArena);
            if (previousWolfParty != 0 && wolfParty == 0) ResetWolf();
            previousWolfParty = wolfParty;
            if (Time.unscaledTime < nextFrame) return;
            nextFrame = Time.unscaledTime + .1f;
            recentHits.RemoveAll(hit => Time.unscaledTimeAsDouble - hit.time > 1);
            // 늑대 이펙트는 아주 잦아서 짧게만 들고 있는다 (화면 정보는 0.1초마다 빠짐없이 가므로 충분하다)
            recentEffects.RemoveAll(effect => Time.unscaledTimeAsDouble - effect.time > (effect.kind == EffectFrame.WolfFx ? .5 : 1));
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
                Character character = characters[recipient];
                frame.inventory = character.Encode(Actors[recipient].Loadout.Class);
                frame.potionSlots = character.PotionSlots;
                frame.potionCooldowns = new[] { character.PotionRemaining(0), character.PotionRemaining(1) };
                frame.prompt = NearbyPrompt(Actors[recipient]);
                frame.tutorial = tutorialProgress.TryGetValue(recipient, out int progress) ? progress : 0;
                frame.practice = PracticeTargets(recipient);
                frame.bossResult = bossResult != null && area == (int)bossResultArea && bossResult.rows.Any(r => r.id == recipient) ? bossResult : null;
                smithRequests.TryGetValue(recipient, out frame.smithRequest);
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
            if (bossResultArea == GameArea.BossArena) bossResult = null;
            if (boss != null) { boss.SetSimulation(false); Destroy(boss.gameObject); }
            ClearRoom(GameArea.BossArena);
            boss = Instantiate(bossPrefab, maps[(int)GameArea.BossArena]).GetComponent<ServerWorldEntity>();
            boss.SetSimulation(false); CurrentBoss = boss.GetComponent<BossController>();
            CurrentBoss.GetComponent<Health>().Died += AwardBoss;
        }
        // 늑대방을 비우고 새 늑대를 세운다 (파티가 나가거나 전멸했을 때)
        private void ResetWolf()
        {
            if (bossResultArea == GameArea.WolfArena) bossResult = null;
            if (wolf != null) { wolf.SetSimulation(false); Destroy(wolf.gameObject); }
            WolfWipeBanner.Hide();
            WolfSliceZone.ClearAll();
            WolfRed.Set(0f);
            ClearRoom(GameArea.WolfArena);
            if (wolfPrefab == null) { wolf = null; CurrentWolf = null; return; }
            wolf = Instantiate(wolfPrefab, maps[(int)GameArea.WolfArena]).GetComponent<ServerWorldEntity>();
            wolf.SetSimulation(false); CurrentWolf = wolf.GetComponent<WolfBossController>();
            CurrentWolf.GetComponent<Health>().Died += AwardWolf;
        }
        // 보스방에 남은 연출 조각(경고 장판·투사체 등)을 지운다
        private void ClearRoom(GameArea room)
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                if (root != gameObject && root.transform.parent == null && root.GetComponent<NetworkObject>() == null &&
                    root.GetComponent<NetworkActorContext>() == null && AreaAt(root.transform.position) == room &&
                    !maps.Any(m => m.gameObject == root)) Destroy(root);
        }
        private GameFrame Capture(GameArea area, SpriteRenderer[] renderers, LineRenderer[] lineRenderers)
        {
            List<SpriteFrame> visuals = new List<SpriteFrame>();
            foreach (SpriteRenderer renderer in renderers)
            {
                if (!renderer.enabled || renderer.sprite == null || renderer.name == "Hit Flash" || scenery.Contains(renderer) || AreaAt(renderer.transform.position) != area || renderer.GetComponent<NetworkSandboxPlayer>() != null) continue;
                Sprite sprite = renderer.sprite;
                int texture = -1;
                if (renderer.GetComponent<WolfBossController>() != null) texture = -8; // 늑대 몸: 위치만 보낸다 (그림은 클라이언트 인형이)
                else if (assets.TryGetId(sprite.texture, out int textureId)) texture = textureId;
                else if (renderer.GetComponent<ActorShadow>() != null) texture = -2;
                else if (sprite.name == GroundBurst.CircleSpriteName) texture = -3;
                else if (sprite.name == "Jump Warning Circle") texture = -6;
                else if (sprite.name == "Boss Telegraph Circle") texture = -4;
                else if (sprite.name == "Boss Telegraph Frame") texture = -5;
                else if (sprite.name == DroppedItem.CircleSpriteName) texture = -7;
                Transform follow = renderer.GetComponent<ActorShadow>()?.Target;
                SwordSwingEffect swing = renderer.GetComponent<SwordSwingEffect>();
                if (swing != null) follow = swing.Owner;
                if (follow == null && renderer.transform.parent != null && renderer.GetComponentInParent<Health>() != null)
                    follow = renderer.GetComponentInParent<Health>().transform;
                SpriteRenderer parent = follow != null ? follow.GetComponent<SpriteRenderer>() : null;
                visuals.Add(new SpriteFrame { id = renderer.GetInstanceID(), texture = texture, rect = sprite.rect, layer = renderer.sortingLayerID,
                    drawMode = (int)renderer.drawMode, size = renderer.size,
                    swordSwing = swing != null, aim = swing != null ? swing.Aim : Vector2.zero,
                    follow = parent != null && parent != renderer ? parent.GetInstanceID() : 0,
                    followOffset = renderer.GetComponent<ActorShadow>() is ActorShadow shadow ? shadow.Offset : follow != null ? renderer.transform.position - follow.position : Vector3.zero,
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
                    dodge = actor.BackstepRemaining, dodgeDuration = actor.BackstepCooldown, dodging = actor.IsDashing,
                    specialDuration = actor.Skills.SpecialCooldownDuration, specialActive = actor.Skills.SpecialActive,
                    specialEffect = actor.Skills.SpecialActiveRemaining, specialEffectDuration = actor.Skills.SpecialActiveDuration,
                    specialEnabled = PlayerSkills.SpecialSkillsEnabled, hasWeapon = actor.HasWeapon,
                    cooldownDurations = actor.Loadout.Slots.Select(actor.Skills.CooldownDuration).ToArray(),
                    activeSkills = actor.Loadout.Slots.Select(actor.Skills.IsUsingSkill).ToArray(),
                    position = actor.transform.position, aim = actor.AimDirection, leader = Parties.Leader(member.Party) == pair.Key,
                    visual = actor.GetComponent<SpriteRenderer>().GetInstanceID(),
                    attacks = attackSequences.TryGetValue(pair.Key, out int attacks) ? attacks : 0,
                    bossLearned = actor.Loadout.BossLearned, books = actor.Loadout.BossSkillBooks };
            }).ToArray();
            bool wolfRoom = area == GameArea.WolfArena;
            Health bossHealth = wolfRoom ? (CurrentWolf != null ? CurrentWolf.GetComponent<Health>() : null) : (CurrentBoss != null ? CurrentBoss.GetComponent<Health>() : null);
            int bossHp = bossHealth != null ? bossHealth.Current : 0, bossMax = bossHealth != null ? bossHealth.Maximum : 0;
            if (wolfRoom && CurrentWolf != null) CurrentWolf.BarValues(out bossHp, out bossMax); // 늑대는 페이즈마다 한 줄
            GameFrame frame = new GameFrame { time = Time.unscaledTimeAsDouble,
                hits = recentHits.Where(hit => AreaAt(hit.position) == area).ToArray(),
                effects = recentEffects.Where(effect => AreaAt(effect.position) == area).ToArray(),
                players = players, sprites = visuals.ToArray(), lines = lines.ToArray(),
                bossParty = wolfRoom ? Parties.RoomParty(GameArea.WolfArena) : Parties.BossParty, validationStage = ValidationStage,
                bossHp = bossHp, bossMax = bossMax,
                bossPhaseTwo = wolfRoom ? CurrentWolf != null && CurrentWolf.IsPhaseTwo : CurrentBoss != null && CurrentBoss.IsPhaseTwo };
            if (area == GameArea.BossArena)
                frame.bossNotice = FindObjectsByType<TextMesh>(FindObjectsSortMode.None).FirstOrDefault(t => t.text.Contains("즉사기") && SameArea(t.transform.position, Origin(area)))?.text;
            if (wolfRoom)
            {
                frame.bossNotice = WolfWipeBanner.Current;
                frame.wolf = WolfSnapshot();
            }
            return frame;
        }
        // 클라이언트: 그 번호 사람이 화면에 그려지는 위치 (같은 구역에 없으면 null)
        public Vector3? PlayerRenderedPosition(ulong id)
        {
            PlayerFrame player = Frame?.players?.FirstOrDefault(p => p.id == id);
            if (player == null || Local == null || player.area != Local.area || player.hp <= 0) return null;
            return player.renderedPosition;
        }

        // 클라이언트 늑대 인형에게 보낼 지금 상태
        private WolfFrame WolfSnapshot()
        {
            if (CurrentWolf == null || !CurrentWolf.isActiveAndEnabled) return null;
            WolfSpriteAnimator art = CurrentWolf.GetComponent<WolfSpriteAnimator>();
            SpriteRenderer body = CurrentWolf.GetComponent<SpriteRenderer>();
            if (art == null || body == null || !art.Ready) return null;
            Vector2 shape = art.ShapeTarget;
            return new WolfFrame
            {
                visual = body.GetInstanceID(), clip = art.CurrentClip, clipStart = Time.unscaledTimeAsDouble - art.ClipElapsed,
                right = art.LookingRight, alive = !CurrentWolf.GetComponent<Health>().IsDead, lift = art.Lift,
                shapeX = shape.x, shapeY = shape.y, shapeSpeed = art.ShapeSpeed, red = WolfRed.Amount, scale = art.BaseScale, sortBias = art.SortBias
            };
        }
        private void Receive(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || manager.IsServer) return;
            reader.ReadValueSafe(out int size);
            if (size < 0 || size > 100000 || reader.Length - reader.Position < size) return;
            byte[] data = new byte[size]; reader.ReadBytesSafe(ref data, size);
            try
            {
                GameFrame previous = Frame;
                Frame = JsonUtility.FromJson<GameFrame>(Decompress(data));
                foreach (PlayerFrame player in Frame.players)
                {
                    PlayerFrame old = previous?.players?.FirstOrDefault(p => p.id == player.id && p.area == player.area && p.hp > 0);
                    player.renderedPosition = old != null ? old.renderedPosition : (Vector3)player.position;
                }
                PlayerFrame local = Local;
                if (local == null) return;
                GameAudio.SetArea((GameArea)local.area);
                ApplyInventory(Frame.inventory);
                if (Frame.smithRequest != clientSmithRequest)
                {
                    clientSmithRequest = Frame.smithRequest;
                    if (clientSmithRequest != 0 && CanUseSmith) GetComponent<PersistentWorld.UI.BlacksmithWindow>()?.OpenOnline();
                }
                if (viewedArea != local.area)
                {
                    viewedArea = local.area;
                    view.Clear();
                    WolfPuppet.Remove();
                    pendingWolfFx.Clear();
                    gameCamera.transform.position = CameraTarget(local);
                    cameraBase = gameCamera.transform.position; cameraBaseReady = true;
                    gameCamera.backgroundColor = mapBackgrounds[viewedArea];
                    gameCamera.orthographicSize = mapCameraSizes[viewedArea];
                    for (int i = 0; i < maps.Length; i++) maps[i].gameObject.SetActive(i == viewedArea);
                    // 보스방에 들어가면 보스 얼굴과 이름을 잠깐 보여 준다 (서버 보스도 그동안 공격하지 않는다)
                    if (viewedArea == (int)GameArea.BossArena && bossPrefab != null && bossPrefab.GetComponent<BossController>() is BossController bossInfo)
                        PersistentWorld.World.BossIntro.Show(bossInfo.DisplayName, bossInfo.DisplayTitle, bossInfo.Portrait);
                    if (viewedArea == (int)GameArea.WolfArena && wolfPrefab != null && wolfPrefab.GetComponent<WolfBossController>() is WolfBossController wolfInfo)
                        PersistentWorld.World.BossIntro.Show(wolfInfo.DisplayName, wolfInfo.DisplayTitle, wolfInfo.Portrait);
                }
                view.Apply(Frame);
                WolfPuppet.Sync(Frame, viewedArea == (int)GameArea.WolfArena, maps[(int)GameArea.WolfArena]);
                PlayerFrame oldLocal = previous?.players?.FirstOrDefault(p => p.id == local.id);
                if (oldLocal != null && oldLocal.area == local.area)
                {
                    if (local.attacks > oldLocal.attacks) GameAudio.Attack((CharacterClass)local.character);
                    if (oldLocal.hp > 0 && local.hp == 0) GameAudio.Death();
                    if (local.special > oldLocal.special + .5f || local.dodge > oldLocal.dodge + .5f ||
                        local.cooldowns.Where((value, i) => value > oldLocal.cooldowns[i] + .5f).Any()) GameAudio.Action(true);
                    if (Frame.potionCooldowns != null && previous.potionCooldowns != null &&
                        Frame.potionCooldowns.Where((value, i) => value > previous.potionCooldowns[i] + .5f).Any()) GameAudio.Action(isPotion: true);
                }
                if (Frame.hits != null) foreach (HitFrame hit in Frame.hits)
                {
                    if (hit.sequence <= lastHit) continue;
                    lastHit = hit.sequence;
                    DamageNumber.Spawn(hit.position, hit.amount.ToString(), hit.player ? new Color(1, .5f, .4f) : Color.white);
                    if (!WolfPuppet.Flash(hit.visual)) view.Flash(hit.visual);
                    if (!hit.player) view.React(hit.visual, NearestPlayerPosition(hit.position));
                    GameAudio.Hit(hit.player);
                }
                if (Frame.effects != null) foreach (EffectFrame effect in Frame.effects)
                {
                    if (effect.sequence <= lastEffect) continue;
                    lastEffect = effect.sequence;
                    if (effect.kind == EffectFrame.WolfFx || (effect.kind == EffectFrame.ChargeWarn && effect.crater)) pendingWolfFx.Add(effect); // 늑대 몸과 같은 시각에 재생
                    else PlayEffect(effect);
                }
            }
            catch (Exception error) { Debug.LogWarning("Game frame rejected: " + error.Message); }
        }
        // 피격 연출의 밀리는 방향을 정할 때 쓴다 (때린 사람은 전송하지 않으므로 가장 가까운 플레이어로 본다)
        private Vector2 NearestPlayerPosition(Vector3 from)
        {
            Vector2 best = from;
            float bestDistance = float.MaxValue;
            if (Frame?.players != null)
                foreach (PlayerFrame player in Frame.players)
                {
                    float d = (player.position - (Vector2)from).sqrMagnitude;
                    if (d < bestDistance) { bestDistance = d; best = player.position; }
                }
            return best;
        }
        private Vector3 cameraBase;
        private bool cameraBaseReady;
        private static Vector3 CameraTarget(PlayerFrame local) => new Vector3(local.position.x, local.position.y + .4f, -10);
        private void LateUpdate()
        {
            if (view == null || Local is not PlayerFrame local) return;
            float blend = 1 - Mathf.Exp(-20 * Time.unscaledDeltaTime);
            view.Animate(blend);
            WolfPuppet.Animate(view);
            // 늑대 이펙트: 그림이 보간되어 그려지는 서버 시각(0.1초 전)에 이르면 재생한다 (너무 밀리면 바로)
            if (pendingWolfFx.Count > 0)
            {
                double renderTime = view.RenderTime;
                for (int i = 0; i < pendingWolfFx.Count; i++)
                {
                    EffectFrame effect = pendingWolfFx[i];
                    if (effect.time > renderTime && effect.time - renderTime < .5) continue;
                    pendingWolfFx.RemoveAt(i--);
                    if (effect.kind == EffectFrame.ChargeWarn) PersistentWorld.World.ChargeWarning.Replay(effect);
                    else WolfFxNet.Replay(effect);
                }
            }
            foreach (PlayerFrame player in Frame.players)
                player.renderedPosition = view.Position(player.visual, player.position);
            Vector3 target = new Vector3(local.renderedPosition.x, local.renderedPosition.y + .4f, -10);
            Transform cameraBody = gameCamera.transform;
            // 따라가기는 흔들림을 뺀 위치로 계산하고, 흔들림(늑대 연출 신호 등)은 마지막에 더한다
            if (!cameraBaseReady) { cameraBase = cameraBody.position; cameraBaseReady = true; }
            cameraBase = Vector3.Lerp(cameraBase, target, (cameraBase - target).sqrMagnitude > 25 ? 1 : blend);
            cameraBody.position = cameraBase + (Vector3)CameraFollow.ShakeOffset;
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
            Health.DamageApplied -= RecordHit;
            if (manager != null) { manager.OnServerStarted -= ServerStarted; manager.OnClientConnectedCallback -= ClientConnected; }
            view?.Clear();
        }

        private void RecordHit(Health target, int amount)
        {
            if (!manager.IsServer) return;
            if (target.GetComponent<TrainingCompanion>() is TrainingCompanion companion && companion.Initializing) return;
            SpriteRenderer visual = target.GetComponent<SpriteRenderer>();
            recentHits.Add(new HitFrame { sequence = ++hitSequence, visual = visual != null ? visual.GetInstanceID() : 0,
                amount = amount, player = target.Faction == Faction.Player,
                position = target.transform.position + Vector3.up * .9f, time = Time.unscaledTimeAsDouble });
        }

        private string NearbyPrompt(ArcherController actor)
        {
            if (actor.Health.IsDead) return "";
            if (Parties.Members.TryGetValue(actor.GetComponent<NetworkActorContext>().ClientId, out PartyMember member) &&
                member.Area == GameArea.Town && Vector2.Distance(actor.transform.position, onlineBlacksmith.transform.position) <= 1.3f)
                return "F · 대장장이 제작 / 강화";
            ScenePortal portal = FindObjectsByType<ScenePortal>(FindObjectsSortMode.None)
                .Where(p => Vector2.Distance(p.transform.position, actor.transform.position) <= 1.3f)
                .OrderBy(p => Vector2.Distance(p.transform.position, actor.transform.position)).FirstOrDefault();
            if (portal != null) return "F · " + portal.Prompt;
            NpcDialogue npc = FindObjectsByType<NpcDialogue>(FindObjectsSortMode.None)
                .FirstOrDefault(p => Vector2.Distance(p.transform.position, actor.transform.position) <= 1.3f);
            return npc != null ? "F · 대화하기" : "";
        }
    }
}
