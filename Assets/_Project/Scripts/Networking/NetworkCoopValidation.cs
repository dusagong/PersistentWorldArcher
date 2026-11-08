using System;
using System.Collections;
using System.Linq;
using System.IO;
using PersistentWorld.Combat;
using PersistentWorld.Items;
using PersistentWorld.Player;
using PersistentWorld.World;
using UnityEngine;

namespace PersistentWorld.Networking
{
    // Explicit local test mode only. Production has no teleport/damage QA command endpoint.
    public sealed class NetworkCoopValidation : MonoBehaviour
    {
        private NetworkGameWorld world;
        private bool started, pauseAi;
        private int lastStage;
        private float deadline;
        private int checks;
        private bool captured, capturedInvite, capturedHp, capturedDeath;
        private bool reconnecting;
        private static string TestArmor => EquipmentCrafting.All.First(recipe =>
            ItemCatalog.TryGet(recipe.ResultId, out ItemDefinition item) && item.CanEquip(PlayerClass.Archer)).ResultId;
        private void Start() { world = GetComponent<NetworkGameWorld>(); deadline = Time.realtimeSinceStartup + 90; }
        private void Update()
        {
            if (!Application.isBatchMode && !NetworkSandbox.HasArgument("-pwa-party-probe")) return;
            if (NetworkSandbox.HasArgument("-pwa-party-validation"))
            {
                if (world.manager.IsServer && !started && world.Actors.Count == 4) { started = true; pauseAi = true; StartCoroutine(ServerSteps()); }
                if (pauseAi) foreach (ServerWorldEntity entity in FindObjectsByType<ServerWorldEntity>(FindObjectsSortMode.None)) entity.SetSimulation(false);
                if (Time.realtimeSinceStartup > deadline) Fail("server validation timeout");
            }
            if (!NetworkSandbox.HasArgument("-pwa-party-probe")) return;
            if (Time.realtimeSinceStartup > deadline) { Fail("client validation timeout"); return; }
            if (reconnecting && !world.manager.IsListening && !world.manager.ShutdownInProgress)
            {
                reconnecting = false;
                GetComponent<NetworkSandbox>().Connect(NetworkSandbox.Argument("-pwa-connect"), NetworkSandbox.Argument("-pwa-code"));
                return;
            }
            GameFrame frame = world.Frame;
            if (frame == null || world.Local == null || frame.validationStage == lastStage) return;
            if (frame.players.Length < 4) return;
            lastStage = frame.validationStage;
            string role = NetworkSandbox.Argument("-pwa-name");
            ulong member = frame.players.First(p => p.name == "Member").id;
            ulong outsider = frame.players.First(p => p.name == "Outsider").id;
            ulong healer = frame.players.First(p => p.name == "Healer").id;
            void Send(PartyCommand cmd, ulong target = 0, int first = 0, int second = 0) => NetworkSandboxPlayer.LocalPlayer.CommandRpc((byte)cmd, target, first, second);
            switch (lastStage)
            {
                case 27: if (role == "Member") NetworkSandboxPlayer.LocalPlayer.ItemRpc((byte)ItemCommand.Craft, TestArmor, 0); break;
                case 28: if (role == "Member") NetworkSandboxPlayer.LocalPlayer.ItemRpc((byte)ItemCommand.Equip, TestArmor, 0); break;
                case 30: if (role == "Member") NetworkSandboxPlayer.LocalPlayer.ItemRpc((byte)ItemCommand.Upgrade, "", 0); break;
                case 29:
                    if (role == "Member") { reconnecting = true; world.manager.Shutdown(); }
                    break;
                case 1: if (role == "Leader") Send(PartyCommand.Invite, member); break;
                case 2: if (role == "Member") Send(PartyCommand.Decline); break;
                case 3: if (role == "Leader") Send(PartyCommand.Invite, member); break;
                case 4: if (role == "Member") Send(PartyCommand.Accept); break;
                case 5: if (role == "Leader") Send(PartyCommand.Invite, outsider); break;
                case 6: if (role == "Outsider") Send(PartyCommand.Accept); break;
                case 7: if (role == "Leader") Send(PartyCommand.Invite, healer); break;
                case 8: if (role == "Healer") Send(PartyCommand.Accept); break;
                case 9: if (role == "Outsider") Send(PartyCommand.Leave); break;
                case 10: if (role == "Healer") Send(PartyCommand.Leave); break;
                case 11: if (role == "Outsider") Send(PartyCommand.Create); break;
                case 12:
                    if (role == "Member") Send(PartyCommand.Equip, first: 0, second: (int)SkillId.Snipe);
                    if (role == "Healer") Send(PartyCommand.ChangeClass, first: (int)CharacterClass.Healer);
                    if (role == "Leader") Send(PartyCommand.ChangeClass, first: (int)CharacterClass.Warrior);
                    break;
                case 13: if (role == "Healer") Send(PartyCommand.Interact); break;
                case 14: if (role == "Member") Send(PartyCommand.Equip, first: 0, second: (int)SkillId.Repulse); break;
                case 15: if (role == "Member") Send(PartyCommand.Interact); break;
                case 16: if (role == "Leader") Send(PartyCommand.Interact); break;
                case 17: if (role == "Outsider") Send(PartyCommand.Interact); break;
                case 18: if (role == "Leader") Send(PartyCommand.Invite, healer); break;
                case 19:
                    if (frame.players.First(p => p.name == "Member").hp != 90) Fail("party HP replication");
                    else Debug.Log("PWA_PARTY_HP_REPLICATION_OK");
                    break;
                case 20: if (role == "Member") Send(PartyCommand.Leave); break;
                case 21: if (role == "Leader") Send(PartyCommand.Leave); break;
                case 22: if (role == "Outsider") Send(PartyCommand.Interact); break;
                case 23: if (role == "Outsider") Send(PartyCommand.Leave); break;
                case 26:
                    if (frame.players.First(p => p.name == "Member").hp != 0) Fail("dead party HP replication");
                    if (world.Local.area == (int)GameArea.BossArena)
                        foreach (string texture in new[] { "DeathGhost", "DeathGravestone" })
                            if (!frame.sprites.Any(s => s.texture >= 0 && world.assets.textures[s.texture].name == texture)) Fail("death sprite replication " + texture);
                    Debug.Log("PWA_PARTY_DEATH_REPLICATION_OK");
                    break;
                case 25:
                    if (role == "Member")
                    {
                        PlayerInventoryProfile inventory = world.ClientInventory;
                        if (inventory == null || inventory.WeaponUpgradeLevel != 1 || !inventory.OwnsEquipment(TestArmor))
                        { Fail("restored inventory reaches the client"); return; }
                    }
                    Debug.Log("PWA_PARTY_CLIENT_OK " + role); Application.Quit(0); break;
            }
        }
        private void LateUpdate()
        {
            string path = NetworkSandbox.Argument("-pwa-capture");
            if (string.IsNullOrEmpty(path) || world.Frame == null || world.Local == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string Variant(string suffix) => Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + suffix + ".png");
            if (!captured && world.Local.party != 0 && world.Frame.players.Count(p => p.party == world.Local.party) == 4)
            { CaptureWorld(path); captured = true; }
            if (!capturedInvite && world.Frame.inviteRemaining > 0)
            { CaptureWorld(Variant("-invite")); capturedInvite = true; }
            if (!capturedHp && world.Frame.validationStage == 19)
            { CaptureWorld(Variant("-hp")); capturedHp = true; }
            if (!capturedDeath && world.Frame.validationStage == 26)
            { CaptureWorld(Variant("-death")); capturedDeath = true; }
        }
        private void CaptureWorld(string path)
        {
            // Hidden windows may have no presented backbuffer. Render the real camera offscreen.
            Camera camera = world.gameCamera;
            RenderTexture target = RenderTexture.GetTemporary(1280, 720, 24);
            RenderTexture previous = RenderTexture.active, previousTarget = camera.targetTexture;
            Texture2D pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally { camera.targetTexture = previousTarget; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Destroy(pixels); }
        }
        private ulong Id(string name) => world.Parties.Members.Values.First(p => p.Name == name).Id;
        private IEnumerator Step(int stage, Func<bool> condition)
        {
            world.ValidationStage = stage;
            float until = Time.realtimeSinceStartup + 8;
            while (!condition()) { if (Time.realtimeSinceStartup > until) { Fail("stage " + stage); yield break; } yield return null; }
            checks++; Debug.Log("PWA_PARTY_STAGE_OK " + stage); yield return new WaitForSeconds(.25f);
        }
        private IEnumerator ServerSteps()
        {
            ulong leader = Id("Leader"), member = Id("Member"), outsider = Id("Outsider"), healer = Id("Healer");
            var actors = world.Actors;
            Require(actors[leader].Skills.BeginSkill(0, Vector2.right) && actors[member].Skills.BeginSkill(0, Vector2.right), "same-class cooldown independence");
            yield return Step(1, () => world.Parties.Invitations.ContainsKey(member));
            yield return Step(2, () => !world.Parties.Invitations.ContainsKey(member) && world.Parties.Members[member].Party == 0);
            yield return Step(3, () => world.Parties.Invitations.ContainsKey(member));
            yield return Step(4, () => world.Parties.Members[member].Party == world.Parties.Members[leader].Party);
            yield return Step(5, () => world.Parties.Invitations.ContainsKey(outsider));
            yield return Step(6, () => world.Parties.Members[outsider].Party == world.Parties.Members[leader].Party);
            yield return Step(7, () => world.Parties.Invitations.ContainsKey(healer));
            yield return Step(8, () => world.Parties.Group(world.Parties.Members[leader].Party).Length == 4);
            yield return Step(9, () => world.Parties.Members[outsider].Party == 0);
            yield return Step(10, () => world.Parties.Members[healer].Party == 0);
            yield return Step(11, () => world.Parties.Members[outsider].Party != 0);
            yield return Step(12, () => actors[member].Loadout.Slots[0] == SkillId.Snipe && actors[healer].Loadout.Class == CharacterClass.Healer && actors[leader].Loadout.Class == CharacterClass.Warrior);
            ServerWorldEntity lootEnemy = FindObjectsByType<ServerWorldEntity>(FindObjectsSortMode.None).First(e =>
                e.area == GameArea.Field && e.GetComponent<RangedEnemyController>() != null);
            MonsterDropTable lootTable = lootEnemy.GetComponent<MonsterDropTable>();
            lootTable.Configure(new[] { new DropEntry(ItemCatalog.MaterialTwo, 5, 1) });
            lootTable.enabled = true;
            Vector2 lootPosition = lootEnemy.transform.position;
            Require(lootEnemy.GetComponent<Health>().TryDamage(100000, Faction.Player), "field enemy killed");
            Require(FindObjectsByType<DroppedItem>(FindObjectsSortMode.None).Any(item =>
                item.ItemId == ItemCatalog.MaterialTwo && item.Quantity == 5 && Vector2.Distance(item.transform.position, lootPosition) < 1),
                "field death drops items through its server drop table");
            ScenePortal entrance = FindObjectsByType<ScenePortal>(FindObjectsSortMode.None).First(p => p.Destination == "BossArena");
            foreach (ulong id in actors.Keys.ToArray()) world.Teleport(id, GameArea.Field, (Vector2)entrance.transform.position - NetworkGameWorld.Origin(GameArea.Field) + Vector2.left * .3f);
            yield return Step(13, () => world.Parties.Members[healer].Area == GameArea.Field && world.Parties.Members[healer].Party == 0 && MessageContains(healer, "파티"));
            yield return Step(14, () => actors[member].Loadout.Slots[0] == SkillId.Snipe && MessageContains(member, "마을"));
            yield return Step(15, () => MessageContains(member, "파티장"));
            yield return Step(16, () => world.Parties.Members[leader].Area == GameArea.BossArena && world.Parties.Members[member].Area == GameArea.BossArena);
            Require(world.Parties.Members[outsider].Area == GameArea.Field && world.Parties.Members[healer].Area == GameArea.Field, "non-party players stay outside");
            yield return Step(17, () => MessageContains(outsider, "사용 중"));
            yield return Step(18, () => !world.Parties.Invitations.ContainsKey(healer) && MessageContains(leader, "보스 룸"));
            actors[member].Health.TryDamage(10, Faction.Enemy); world.ValidationStage = 19;
            yield return new WaitForSeconds(.4f); Require(actors[member].Health.Current == 90, "party health source damage");
            actors[member].Health.TryDamage(1000, Faction.Enemy);
            world.ValidationStage = 26;
            yield return new WaitForSeconds(10.4f);
            Require(actors[member].Health.IsDead && world.Parties.Members[member].Area == GameArea.BossArena, "boss corpse waits past normal ten-second return");
            Require(actors[member].GetComponent<PlayerDeathVisual>() != null && !actors[member].GetComponent<SpriteRenderer>().enabled, "death marker replaces living sprite");
            Require(actors[member].Health.Revive(.7f, 3), "revival succeeds during boss fight");
            yield return new WaitForSeconds(.4f);
            Require(actors[member].GetComponent<SpriteRenderer>().enabled && actors[member].GetComponent<Rigidbody2D>().simulated && actors[member].Health.Current == 70, "revival restores sprite physics and seventy percent HP");
            yield return Step(20, () => world.Parties.Members[member].Area == GameArea.Town && world.Parties.Members[member].Party == 0);
            Require(world.Parties.BossParty != 0, "one remaining member holds room");
            yield return Step(21, () => world.Parties.BossParty == 0 && world.Parties.Members[leader].Area == GameArea.Town);
            yield return Step(22, () => world.Parties.Members[outsider].Area == GameArea.BossArena);
            yield return Step(23, () => world.Parties.BossParty == 0 && world.Parties.Members[outsider].Area == GameArea.Town);
            world.ReturnToTown(healer); world.ReturnToTown(member);
            ArcherController warrior = actors[leader]; warrior.SetAim(Vector2.right);
            Projectile shot = Projectile.Spawn((Vector2)warrior.transform.position + Vector2.right * .3f, Vector2.left, 20, Faction.Enemy, null, Color.red, 5, 2);
            Require(warrior.Skills.TrySpecial() && warrior.Skills.TryParry(shot) && shot.Damage == 100, "500 percent reflection");
            GameObject target = new GameObject("Parry target"); target.transform.position = warrior.transform.position + Vector3.right * 1.5f;
            target.AddComponent<CircleCollider2D>().radius = .2f; Health health = target.AddComponent<Health>(); health.Configure(Faction.Enemy, 300, false);
            Physics2D.SyncTransforms(); yield return new WaitForSeconds(.5f);
            Require(health.Current == 200, "reflected projectile deals actual fivefold damage"); Destroy(target);
            yield return new WaitForSeconds(Mathf.Max(0, actors[member].Health.InvulnerableUntil - Time.time) + .1f);
            world.Teleport(member, GameArea.Field, Vector2.left * 5);
            Require(actors[member].Health.TryDamage(1000, Faction.Enemy) && actors[member].Health.IsDead, "field death occurs after revival immunity expires");
            yield return new WaitForSeconds(10.5f);
            Require(world.Parties.Members[member].Area == GameArea.Town && !actors[member].Health.IsDead, "field death returns and revives in town after ten seconds");
            Require(actors[member].GetComponent<SpriteRenderer>().enabled && actors[member].GetComponent<Rigidbody2D>().simulated, "town return restores living presentation and physics");
            ServerWorldEntity monster = FindObjectsByType<ServerWorldEntity>(FindObjectsSortMode.None).First(e => e.area == GameArea.Field && e.GetComponent<SlimeJumpEnemyController>() != null);
            Vector2 before = monster.transform.position;
            foreach (var actor in actors.Values) actor.Health.Invulnerable = true;
            world.Teleport(leader, GameArea.Field, before - NetworkGameWorld.Origin(GameArea.Field) + Vector2.left * 3);
            pauseAi = false;
            foreach (ServerWorldEntity entity in FindObjectsByType<ServerWorldEntity>(FindObjectsSortMode.None)) if (entity.area == GameArea.Field) entity.SetSimulation(true);
            yield return new WaitForSeconds(1.5f);
            Require(Vector2.Distance(before, monster.transform.position) > .05f, "actual field AI follows network player");
            world.ReturnToTown(leader); world.Parties.Create(leader, out _);
            world.Teleport(leader, GameArea.Field, (Vector2)entrance.transform.position - NetworkGameWorld.Origin(GameArea.Field) + Vector2.left * .3f);
            world.UsePortal(leader, entrance);
            float patternDeadline = Time.realtimeSinceStartup + 8;
            while (world.CurrentBoss != null && string.IsNullOrEmpty(world.CurrentBoss.CurrentPattern) && Time.realtimeSinceStartup < patternDeadline) yield return null;
            Require(world.Parties.Members[leader].Area == GameArea.BossArena && world.CurrentBoss != null && !string.IsNullOrEmpty(world.CurrentBoss.CurrentPattern), "actual boss AI runs for admitted party");
            Health bossHealth = world.CurrentBoss.GetComponent<Health>();
            Require(bossHealth.TryDamage(100000, Faction.Player) && bossHealth.IsDead && warrior.Loadout.BossSkillBooks == 1, "boss reward granted to admitted player");
            ScenePortal exit = FindObjectsByType<ScenePortal>(FindObjectsSortMode.None).First(p =>
                p.Destination == "Field" && NetworkGameWorld.AreaAt(p.transform.position) == GameArea.BossArena);
            world.Teleport(leader, GameArea.BossArena, (Vector2)exit.transform.position - NetworkGameWorld.Origin(GameArea.BossArena));
            world.UsePortal(leader, exit);
            Require(world.Parties.BossParty == 0 && world.Parties.Members[leader].Area == GameArea.Field, "boss exit follows the main scene portal to Field");
            yield return null;
            world.Teleport(leader, GameArea.Field, (Vector2)entrance.transform.position - NetworkGameWorld.Origin(GameArea.Field));
            world.UsePortal(leader, entrance);
            actors[leader].Health.Invulnerable = false;
            actors[leader].Health.TryDamage(10000, Faction.Enemy);
            yield return new WaitForSeconds(.5f);
            Require(world.Parties.BossParty == 0 && world.Parties.Members[leader].Area == GameArea.Town && !actors[leader].Health.IsDead, "solo party wipe releases boss room and returns to town");
            Require(FindObjectsByType<ServerWorldEntity>(FindObjectsSortMode.None).Any(e =>
                e.area == GameArea.Field && e.GetComponent<RangedEnemyController>() != null &&
                !e.GetComponent<Health>().IsDead && Vector2.Distance(e.transform.position, lootPosition) < 1.5f),
                "field enemy respawns at its spawn point");
            world.Teleport(member, GameArea.Field, Vector2.left * 5);
            Vector2 drop = actors[member].transform.position;
            PlayerInventoryProfile profile = actors[member].GetComponent<PlayerEquipment>().Profile;
            DroppedItem.Spawn(drop, ItemCatalog.MaterialOne, 10, drop);
            DroppedItem.Spawn(drop, ItemCatalog.MaterialTwo, 5, drop);
            yield return new WaitForSeconds(.85f);
            Require(profile.Inventory.GetCount(ItemCatalog.MaterialOne) == 10 && profile.Inventory.GetCount(ItemCatalog.MaterialTwo) == 5, "field drops collected exactly once into owning inventory");
            world.Teleport(member, GameArea.Town, world.BlacksmithPosition - NetworkGameWorld.Origin(GameArea.Town));
            yield return Step(27, () => profile.Inventory.HasItem(TestArmor, 1));
            ItemCatalog.TryGet(TestArmor, out ItemDefinition crafted);
            yield return Step(28, () => profile.GetEquipped(crafted.Slot) == TestArmor);
            yield return Step(30, () => profile.WeaponUpgradeLevel == 1);
            yield return Step(29, () => world.Parties.Members.Values.Any(p => p.Id != member && p.Name == "Member"));
            ArcherController returned = actors[Id("Member")];
            PlayerInventoryProfile restored = returned.GetComponent<PlayerEquipment>().Profile;
            Require(restored.GetEquipped(crafted.Slot) == TestArmor && restored.WeaponUpgradeLevel == 1 &&
                restored.Inventory.GetCount(ItemCatalog.MaterialOne) == 0 && restored.Inventory.GetCount(ItemCatalog.MaterialTwo) == 0 &&
                returned.Loadout.Slots[0] == SkillId.Snipe, "reconnect restores crafted gear, upgrade, spent materials and skill");
            world.ValidationStage = 25;
            float until = Time.realtimeSinceStartup + 8;
            while (actors.Count != 0 && Time.realtimeSinceStartup < until) yield return null;
            Require(actors.Count == 0 && world.Parties.BossParty == 0, "disconnect cleanup");
            Debug.Log("PWA_PARTY_INTEGRATION_OK checks=" + checks); Application.Quit(0);
        }
        // Read the exact authoritative message used by client snapshots, without a QA command endpoint.
        private bool MessageContains(ulong id, string part) => world.LastMessage(id).Contains(part);
        private void Require(bool condition, string message) { if (!condition) { Fail(message); return; } checks++; }
        private static void Fail(string message) { Debug.LogError("PWA_PARTY_VALIDATION_FAILED " + message); Application.Quit(1); }
    }
}
