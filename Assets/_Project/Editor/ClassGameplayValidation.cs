using System;
using System.Collections;
using System.Collections.Generic;
using PersistentWorld.Combat;
using PersistentWorld.Player;
using PersistentWorld.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersistentWorld.Editor
{
    [InitializeOnLoad]
    public static class ClassGameplayValidation
    {
        private const string BatchKey = "PersistentWorld.ClassGameplayValidation";
        private static IEnumerator steps;
        private static float resumeAt;
        private static double deadline;
        private static ArcherController player;
        private static readonly List<GameObject> Targets = new List<GameObject>();
        private static int assertions;

        static ClassGameplayValidation()
        {
            if (SessionState.GetBool(BatchKey, false)) EditorApplication.playModeStateChanged += Entered;
        }

        [MenuItem("Tools/Persistent World/Validate Class Skills and Death (Batch)")]
        public static void RunBatch()
        {
            PrototypeValidation.ValidateAll();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Town.unity");
            SessionState.SetBool(BatchKey, true);
            EditorApplication.playModeStateChanged += Entered;
            EditorApplication.EnterPlaymode();
        }

        private static void Entered(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= Entered;
            steps = Scenarios(); resumeAt = 0; assertions = 0;
            deadline = EditorApplication.timeSinceStartup + 100;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timeout");
                if (Time.time < resumeAt) return;
                if (!steps.MoveNext())
                {
                    EditorApplication.update -= Tick;
                    SessionState.SetBool(BatchKey, false);
                    Time.timeScale = 1;
                    Debug.Log("CLASS_GAMEPLAY_VALIDATION_OK assertions=" + assertions);
                    EditorApplication.Exit(0);
                }
                else resumeAt = Time.time + (steps.Current is float seconds ? seconds : 0);
            }
            catch (Exception exception)
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool(BatchKey, false);
                Time.timeScale = 1;
                Debug.LogException(exception);
                Debug.LogError("CLASS_GAMEPLAY_VALIDATION_FAILED assertions=" + assertions);
                EditorApplication.Exit(1);
            }
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            assertions++;
            Debug.Log("CLASS_CHECK_OK " + name);
        }

        private static Health Target(Vector2 position, Faction faction = Faction.Enemy, bool boss = false)
        {
            GameObject obj = new GameObject("Validation Target");
            Targets.Add(obj); obj.transform.position = position;
            Health health = obj.AddComponent<Health>(); health.Configure(faction, 100, false);
            obj.AddComponent<CircleCollider2D>().radius = .15f;
            if (boss) { BossController controller = obj.AddComponent<BossController>(); controller.enabled = false; }
            Physics2D.SyncTransforms();
            return health;
        }

        private static void AimRight() => player.SetAim(Vector2.right);
        private static void ClearTargets()
        {
            foreach (GameObject obj in Targets) if (obj != null) UnityEngine.Object.Destroy(obj);
            Targets.Clear();
            foreach (Projectile projectile in UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(projectile.gameObject);
        }

        private static IEnumerator Scenarios()
        {
            yield return .1f;
            Time.timeScale = 2;
            player = UnityEngine.Object.FindFirstObjectByType<ArcherController>();
            player.AcceptLocalInput = false;
            SkillLoadout loadout = player.Loadout;
            PlayerSkills skills = player.Skills;
            Check(loadout.Slots.Length == 3 && loadout.Slots[0] == SkillId.Repulse, "default slots");
            Check(!loadout.Equip(1, SkillId.Repulse) && !loadout.Equip(0, SkillId.Whirlwind), "duplicate and foreign class rejected");
            Check(!loadout.Equip(2, SkillId.BossBlast) && !loadout.Equip(2, SkillId.Snipe) && !loadout.UseBossSkillBook(), "boss slot and missing book restrictions");
            Check(loadout.Equip(0, SkillId.None) && !skills.BeginSkill(0, Vector2.zero), "unequipped cannot cast");
            loadout.Equip(0, SkillId.Repulse);
            AimRight();
            Health front = Target(new Vector2(1.5f, 0));
            front.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one * .1f;
            Health back = Target(new Vector2(-1, 0));
            Health boss = Target(new Vector2(2, 1), boss: true);
            boss.GetComponent<BossController>().PatternLocked = true;
            Check(skills.BeginSkill(0, Vector2.right * 2) && !skills.BeginSkill(0, Vector2.right), "repulse cooldown");
            Check(front.Current == 80 && back.Current == 100 && boss.Current == 80, "90 degree cone damage once per enemy");
            loadout.Equip(0, SkillId.None); loadout.Equip(0, SkillId.Repulse);
            Check(skills.Cooldown(SkillId.Repulse) > 0 && !skills.BeginSkill(0, Vector2.right), "equip preserves cooldown");
            yield return .3f;
            Check(front.transform.position.x > 3 && Vector2.Distance(boss.transform.position, new Vector2(2, 1)) < .01f, "knockback and boss pattern immunity");
            boss.GetComponent<BossController>().PatternLocked = false;
            Check(boss.GetComponent<CombatStatus>().Knockback(Vector2.right, 1), "idle boss can be knocked back");
            ClearTargets(); yield return .05f;

            Health rainTarget = Target(new Vector2(1, 0));
            Check(skills.BeginSkill(1, new Vector2(1, 0)) && skills.IsHolding, "rain starts targeting");
            yield return .3f;
            Check(rainTarget.Current == 100 && skills.Cooldown(SkillId.ArrowRain) == 0, "rain waits for release");
            Check(skills.ReleaseSkill(1), "rain release");
            yield return .4f;
            Check(rainTarget.Current == 70 && !skills.IsHolding, "rain impact");
            yield return .4f;
            ClearTargets(); yield return .05f;
            Check(loadout.Equip(0, SkillId.Snipe), "equip third skill");
            AimRight();
            Health snipeTarget = Target(new Vector2(6, .35f));
            Check(skills.BeginSkill(0, Vector2.right * 6) && skills.MovementLocked && !player.TryBackstep(), "snipe charge locks movement and dash");
            yield return 1.95f;
            Check(!skills.IsHolding && skills.Cooldown(SkillId.Snipe) > 0, "maximum charge auto releases");
            yield return 1.2f;
            Check(snipeTarget.Current == 20 && !skills.MovementLocked, "charged snipe damage and release cleanup");
            ClearTargets(); yield return .05f;
            Check(skills.TrySpecial() && !skills.TrySpecial(), "archer special cooldown");
            AimRight(); Check(player.TryBasicAttack() && !player.TryBasicAttack(), "basic attack cooldown");
            HashSet<int> arrows = new HashSet<int>(); float sampledUntil = Time.time + .4f;
            while (Time.time < sampledUntil)
            {
                foreach (Projectile projectile in UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)) if (projectile.name == "Basic Attack Arrow") arrows.Add(projectile.GetInstanceID());
                yield return null;
            }
            Check(arrows.Count == 2, "archer special two consecutive arrows");
            ClearTargets(); yield return .05f;

            loadout.SelectClass(CharacterClass.Warrior); AimRight();
            Health swordFront = Target(new Vector2(1, 0)); Health swordBack = Target(new Vector2(-1, 0));
            Check(player.TryBasicAttack() && swordFront.Current == 88 && swordBack.Current == 100, "warrior 120 degree basic attack");
            Check(skills.BeginSkill(0, Vector2.zero), "whirlwind start");
            yield return .25f;
            Check(swordBack.Current == 100, "whirlwind preparation delay");
            yield return .4f;
            Check(swordBack.Current == 70 && swordFront.Current == 58, "whirlwind circular damage");
            AimRight();
            Check(skills.BeginSkill(1, Vector2.right) && skills.ComboStage == 1, "combo first");
            Health far = Target(new Vector2(3, 0));
            Check(skills.BeginSkill(1, Vector2.right) && far.Current == 100, "combo second range");
            Check(skills.BeginSkill(1, Vector2.right) && far.Current == 76 && skills.Cooldown(SkillId.ComboSlash) > 0, "combo third wider and cooldown");
            ClearTargets(); yield return .05f;
            loadout.Equip(0, SkillId.Excalibur); AimRight();
            Health excaliburTarget = Target(new Vector2(4, 0));
            Check(skills.BeginSkill(0, Vector2.right) && skills.MovementLocked, "excalibur charge");
            yield return 1.95f;
            Check(excaliburTarget.Current == 20 && !skills.MovementLocked, "excalibur full charge range and damage");
            ClearTargets(); yield return .05f;
            yield return Mathf.Max(0, skills.SpecialRemaining) + .05f;
            AimRight(); Check(skills.TrySpecial(), "parry start");
            Projectile hostile = Projectile.Spawn(new Vector2(.35f, 0), Vector2.left, 20, Faction.Enemy, null, Color.red, 1);
            Check(skills.TryParry(hostile) && hostile.SourceFaction == Faction.Player && hostile.Damage == 10, "parry returns half damage");
            ClearTargets(); yield return .05f;

            loadout.SelectClass(CharacterClass.Healer);
            Health ally = Target(new Vector2(1, 0), Faction.Player); ally.TryDamage(70, Faction.Enemy);
            CombatStatus debuff = ally.gameObject.AddComponent<CombatStatus>(); debuff.ApplyVulnerability();
            player.GetComponent<CombatStatus>().ApplyVulnerability();
            Check(skills.BeginSkill(1, Vector2.zero) && !debuff.Vulnerable && !player.GetComponent<CombatStatus>().Vulnerable, "cleanse self and ally");
            boss = Target(new Vector2(1, 1), boss: true);
            Health ordinaryEnemy = Target(new Vector2(1, -1));
            Check(skills.BeginSkill(0, Vector2.zero), "healing zone start");
            yield return 1.1f;
            Check(ally.Current == 50 && boss.Current == 90 && ordinaryEnemy.Current == 100, "healing zone 20 percent per second and boss only damage");
            yield return 4.1f;
            Check(ally.Current == 100 && boss.Current == 50 && UnityEngine.Object.FindObjectsByType<HealingZone>(FindObjectsSortMode.None).Length == 0, "healing zone five ticks cleanup");
            ClearTargets(); yield return .05f;
            loadout.Equip(0, SkillId.Judgment); AimRight();
            Health beamTarget = Target(new Vector2(2, .35f));
            Check(skills.BeginSkill(0, Vector2.right * 2) && skills.MovementLocked, "judgment movement lock");
            // Hold aim stable only for the first synchronous beam tick.
            Check(beamTarget.Current < 100, "judgment beam damages");
            yield return 5.2f;
            Check(!skills.IsHolding && !skills.MovementLocked, "judgment five second cap");
            ClearTargets(); yield return .05f;

            loadout.AddBossSkillBook();
            Check(loadout.UseBossSkillBook() && loadout.BossSkillBooks == 0 && !loadout.UseBossSkillBook(), "book consumed once to learn boss skill");
            Check(loadout.Equip(2, SkillId.BossBlast), "learned boss skill equip");
            player.SetMenuOpen(true);
            Check(!skills.BeginSkill(2, Vector2.zero), "menu blocks equipped skill");
            player.SetMenuOpen(false);
            Health visible = Target((Vector2)Camera.main.transform.position + Vector2.right * 2);
            Health offscreen = Target(new Vector2(100, 100));
            Check(skills.BeginSkill(2, Vector2.zero) && visible.Current == 40 && offscreen.Current == 100, "boss skill screen target restriction");
            ClearTargets(); yield return .05f;

            loadout.SelectClass(CharacterClass.Archer); loadout.Equip(0, SkillId.Snipe);
            AimRight(); Check(skills.BeginSkill(0, Vector2.right), "charge before menu");
            player.SetMenuOpen(true);
            Check(!skills.IsHolding && !skills.MovementLocked && !skills.TryBasicAttack(), "menu cancels cast and blocks combat");
            player.SetMenuOpen(false);
            Check(skills.BeginSkill(0, Vector2.right), "charge before death");
            boss = Target(new Vector2(0, 4), boss: true);
            player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.right * 4;
            player.Health.TryDamage(1000, Faction.Enemy);
            Check(player.Health.IsDead && player.WaitingForBoss && !player.GetComponent<Rigidbody2D>().simulated && player.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero, "death stops physics and waits for boss");
            Check(!skills.TryBasicAttack() && !skills.TrySpecial() && !player.TryBackstep() && !player.Health.Heal(10), "dead player cannot act or receive normal healing");
            yield return .1f;
            Check(!skills.IsHolding && !skills.MovementLocked && UnityEngine.Object.FindObjectsByType<PowerShotEffect>(FindObjectsSortMode.None).Length == 0, "death cancels charging");
            yield return 10.2f;
            Check(player != null && player.Health.IsDead && player.WaitingForBoss, "boss corpse stays beyond ten seconds");
            GameObject healerObj = new GameObject("Validation Healer"); Targets.Add(healerObj);
            healerObj.transform.position = player.transform.position + Vector3.right;
            healerObj.AddComponent<SpriteRenderer>();
            healerObj.AddComponent<Health>().Configure(Faction.Player, 100, false);
            healerObj.AddComponent<Rigidbody2D>().gravityScale = 0;
            ArcherController healer = healerObj.AddComponent<ArcherController>();
            healer.AcceptLocalInput = false;
            healer.Loadout.SelectClass(CharacterClass.Healer);
            Check(healer.Skills.TrySpecial() && player.Health.Current == 70, "healer revives ally at seventy percent");
            yield return .1f;
            Check(player.GetComponent<Rigidbody2D>().simulated && player.GetComponent<Collider2D>().enabled && !player.Health.TryDamage(10, Faction.Enemy), "revival restores body and grants immunity");
            yield return 3.1f;
            Check(player.Health.TryDamage(1, Faction.Enemy), "revival immunity expires after three seconds");
            player.Health.TryDamage(1000, Faction.Enemy);
            BossEncounter.Complete();
            yield return .3f;
            Check(SceneManager.GetActiveScene().name == "Town", "boss encounter completion returns corpse to town");
            Targets.Clear();
            player = UnityEngine.Object.FindFirstObjectByType<ArcherController>();
            Check(player != null && !player.Health.IsDead && player.Loadout.BossLearned, "town respawn and learned skill survive scene change");
            SceneManager.LoadScene("Field"); yield return .3f;
            player = UnityEngine.Object.FindFirstObjectByType<ArcherController>();
            foreach (EnemyController enemy in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) enemy.enabled = false;
            player.Health.TryDamage(1000, Faction.Enemy);
            yield return 9.5f;
            Check(SceneManager.GetActiveScene().name == "Field" && player.Health.IsDead, "field corpse stays before ten seconds");
            yield return .8f;
            Check(SceneManager.GetActiveScene().name == "Town" && !UnityEngine.Object.FindFirstObjectByType<ArcherController>().Health.IsDead, "field returns to town after ten seconds");
        }
    }
}
