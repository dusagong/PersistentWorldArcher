using System;
using System.Collections;
using PersistentWorld.Combat;
using PersistentWorld.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PersistentWorld.Editor
{
    [InitializeOnLoad]
    public static class SwordSwingValidation
    {
        private const string Key = "PersistentWorld.SwordSwingValidation";
        private static IEnumerator steps;
        private static float resumeAt;
        private static double deadline;
        private static int checks;
        static SwordSwingValidation()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.playModeStateChanged += Entered;
        }

        public static void RunBatch()
        {
            PrototypeValidation.ValidateAll();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Town.unity");
            SessionState.SetBool(Key, true);
            EditorApplication.playModeStateChanged += Entered;
            EditorApplication.EnterPlaymode();
        }

        private static void Entered(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= Entered;
            steps = Scenarios(); resumeAt = 0; checks = 0;
            deadline = EditorApplication.timeSinceStartup + 105;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Sword animation validation timed out.");
                if (Time.time < resumeAt) return;
                if (steps.MoveNext()) resumeAt = Time.time + (steps.Current is float seconds ? seconds : 0);
                else Finish(true);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(false); }
        }

        private static void Finish(bool passed)
        {
            Time.timeScale = 1;
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false);
            if (passed) Debug.Log("SWORD_SWING_VALIDATION_OK checks=" + checks);
            else Debug.LogError("SWORD_SWING_VALIDATION_FAILED checks=" + checks);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void Require(bool result, string message)
        {
            if (!result) throw new InvalidOperationException(message);
            checks++;
        }

        private static Health Target(Vector2 position)
        {
            GameObject obj = new GameObject("Sword swing validation target");
            obj.transform.position = position;
            Health health = obj.AddComponent<Health>(); health.Configure(Faction.Enemy, 100, false);
            obj.AddComponent<CircleCollider2D>().radius = .1f;
            return health;
        }

        private static IEnumerator Scenarios()
        {
            yield return .1f;
            ArcherController player = UnityEngine.Object.FindFirstObjectByType<ArcherController>();
            player.AcceptLocalInput = false;
            player.Loadout.SelectClass(CharacterClass.Warrior);
            player.SetAim(Vector2.right);
            Health front = Target(new Vector2(1, 0));
            front.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one * .1f;
            Health back = Target(new Vector2(-1, 0));
            Health extended = Target(new Vector2(2.5f, 0));
            Health outside = Target(new Vector2(3, 0));
            Physics2D.SyncTransforms();
            float firstAttackAt = Time.time;
            Require(player.TryBasicAttack() && !player.TryBasicAttack(), "Basic attack cooldown changed.");
            Require(front.Current == 88 - player.EquipmentBonuses.Attack && back.Current == 100, "120-degree damage or multi-collider damage changed.");
            Require(extended.Current == front.Current && outside.Current == 100, "Warrior basic range is not 2.7 units.");
            Require(UnityEngine.Object.FindObjectsByType<SkillIndicator>(FindObjectsSortMode.None).Length == 0, "Basic attack still displays range.");
            SwordSwingEffect effect = UnityEngine.Object.FindFirstObjectByType<SwordSwingEffect>();
            Require(effect != null && Mathf.Approximately(effect.SwingAngle, 60), "Sword does not begin at the edge of the arc.");
            Sprite icon = Resources.Load<Sprite>("Items/Equipment/06_warrior");
            Sprite weapon = effect.GetComponent<SpriteRenderer>().sprite;
            Require(weapon.texture == icon.texture && weapon != icon && icon.pivot == icon.rect.size / 2, "Original image/icon was modified or not reused.");
            Require(weapon.pivot == new Vector2(16, 64), "Sword is not rotating around its handle.");
            Require(effect.transform.localScale == Vector3.one * 1.5f && Mathf.Approximately(SwordSwingEffect.Duration, .16f), "Sword motion did not scale with range and attack speed.");
            player.SetAim(Vector2.left);
            float minimum = 60;
            float previous = 60;
            bool correct = true;
            while (effect != null)
            {
                minimum = Mathf.Min(minimum, effect.SwingAngle);
                correct &= effect.SwingAngle <= previous + .01f && effect.SwingAngle >= -60 && effect.SwingAngle <= 60;
                correct &= Quaternion.Angle(effect.transform.rotation, Quaternion.Euler(0, 0, effect.SwingAngle - 45)) < .1f;
                previous = effect.SwingAngle;
                yield return null;
            }
            Require(correct && minimum < -30, "Sword did not sweep continuously inside the locked 120-degree aim arc.");
            Require(front.Current == 88 - player.EquipmentBonuses.Attack, "Animation caused extra damage.");
            Require(!player.TryBasicAttack(), "Animation completion incorrectly reset attack cooldown.");
            yield return Mathf.Max(0, firstAttackAt + .30f - Time.time);
            Require(!player.TryBasicAttack(), "Warrior attacks before the new 0.367-second interval.");
            yield return Mathf.Max(0, firstAttackAt + .40f - Time.time);
            Require(player.TryBasicAttack(), "Next sword swing failed.");
            player.SetMenuOpen(true);
            yield return null;
            Require(UnityEngine.Object.FindFirstObjectByType<SwordSwingEffect>() == null, "Opening menu did not cancel sword visual.");
            UnityEngine.Object.Destroy(front.gameObject); UnityEngine.Object.Destroy(back.gameObject);
            UnityEngine.Object.Destroy(extended.gameObject); UnityEngine.Object.Destroy(outside.gameObject);
            player.SetMenuOpen(false);
            Time.timeScale = 2;
            yield return .05f;
            PlayerSkills skills = player.Skills;
            Require(skills.BeginSkill(0, Vector2.zero), "Whirlwind failed.");
            Require(NoRange(), "Whirlwind windup displays range.");
            yield return .6f;
            ClassSheetEffect ring = Find(ClassArtEffect.WarriorWhirlwind);
            Require(ring != null && NoRange(), "Whirlwind displays range or has no animation.");
            Require(Vector2.Distance(ring.transform.position, player.GetComponent<SpriteRenderer>().bounds.center) < .01f, "Whirlwind does not center on the character body.");
            player.transform.position += Vector3.right;
            yield return .05f;
            Require(Vector2.Distance(ring.transform.position, player.GetComponent<SpriteRenderer>().bounds.center) < .01f, "Whirlwind does not follow the moving character center.");
            player.transform.position = Vector3.zero;

            player.SetAim(Vector2.right);
            Require(skills.TrySpecial(), "Parry failed.");
            ClassSheetEffect parry = Find(ClassArtEffect.WarriorParry);
            Require(parry != null && parry.transform.localScale == Vector3.one * 1.5f && Vector2.Dot(parry.transform.right, Vector2.left) > .99f, "Parry source orientation or scale is wrong.");
            player.SetAim(Vector2.up);
            yield return .05f;
            Require(Vector2.Dot(parry.transform.right, Vector2.down) > .99f, "Parry art did not follow aim with source correction.");
            Projectile ahead = Projectile.Spawn(new Vector2(0, .3f), Vector2.down, 20, Faction.Enemy, null, Color.red, 1);
            Projectile behind = Projectile.Spawn(new Vector2(0, -.3f), Vector2.up, 20, Faction.Enemy, null, Color.red, 1);
            Require(!skills.TryParry(behind) && skills.TryParry(ahead), "Parry hit test does not match the forward direction.");
            UnityEngine.Object.Destroy(ahead.gameObject); UnityEngine.Object.Destroy(behind.gameObject);
            foreach (Vector2 aim in new[] { Vector2.left, Vector2.down, Vector2.right })
            {
                player.SetAim(aim);
                yield return null;
                Require(parry != null && Vector2.Dot(parry.transform.right, -aim) > .99f, "Parry art source correction failed in direction " + aim);
            }

            player.Loadout.Equip(0, SkillId.Excalibur);
            player.SetAim(Vector2.right);
            Health newAim = Target(new Vector2(0, 4));
            Health oldAim = Target(new Vector2(4, 0));
            Physics2D.SyncTransforms();
            Require(skills.BeginSkill(0, Vector2.right), "Excalibur charge failed.");
            player.SetAim(Vector2.up);
            yield return .2f;
            ClassSheetEffect charge = Find(ClassArtEffect.WarriorCharge);
            Require(NoRange() && charge != null && Vector2.Dot(charge.transform.up, Vector2.down) > .99f, "Excalibur charge range or live aim is wrong.");
            yield return 1.75f;
            Require(!skills.IsHolding && newAim.Current < 100 && oldAim.Current == 100 && NoRange(), "Excalibur auto release used stale aim or displayed range.");
            yield return 10.1f;
            player.SetAim(Vector2.right);
            Require(skills.BeginSkill(0, Vector2.right), "Excalibur second charge failed.");
            player.SetAim(Vector2.left);
            Require(skills.ReleaseSkill(0) && Vector2.Dot(Find(ClassArtEffect.WarriorRelease).transform.up, Vector2.right) > .99f, "Excalibur manual release used stale aim.");
            UnityEngine.Object.Destroy(newAim.gameObject); UnityEngine.Object.Destroy(oldAim.gameObject);

            // Recover mana normally before checking the healer sequence.
            yield return 35f;
            player.Loadout.SelectClass(CharacterClass.Healer);
            Require(skills.BeginSkill(0, Vector2.zero) && skills.Cooldown(SkillId.HealingZone) > 14.9f, "Healing cooldown is not 15 seconds.");
            Require(skills.BeginSkill(1, Vector2.zero) && skills.Cooldown(SkillId.Cleanse) > 9.9f, "Cleanse cooldown is not 10 seconds.");
            player.Loadout.Equip(0, SkillId.Judgment);
            Require(skills.BeginSkill(0, Vector2.right), "Judgment failed.");
            yield return 1f;
            Require(skills.Cooldown(SkillId.Judgment) == 0 && skills.IsHolding, "Judgment cooldown started during channel.");
            Require(skills.ReleaseSkill(0) && skills.Cooldown(SkillId.Judgment) > 9.9f, "Judgment early release does not start 10 seconds.");
            yield return 10.1f;
            Require(skills.BeginSkill(0, Vector2.right), "Judgment reuse failed.");
            yield return 5.1f;
            Require(!skills.IsHolding && skills.Cooldown(SkillId.Judgment) > 9.7f, "Judgment maximum channel does not start cooldown on completion.");
            yield return 20f;
            Require(skills.BeginSkill(0, Vector2.right), "Judgment before class switch failed.");
            player.Loadout.SelectClass(CharacterClass.Warrior);
            Require(!skills.IsHolding, "Class switch did not interrupt Judgment.");
            player.Loadout.SelectClass(CharacterClass.Healer);
            Require(skills.Cooldown(SkillId.Judgment) > 9.9f && !skills.BeginSkill(0, Vector2.right), "Class switch bypassed the healer cooldown.");
            yield return 10.1f;
            Require(skills.BeginSkill(0, Vector2.right), "Judgment before menu interruption failed.");
            player.SetMenuOpen(true);
            yield return null;
            Require(!skills.IsHolding && Find(ClassArtEffect.HealerJudgment) == null && skills.Cooldown(SkillId.Judgment) > 9.8f, "Menu interruption did not stop Judgment and start cooldown.");
            player.SetMenuOpen(false);
            yield return 20f;
            Health deadAlly = Target(new Vector2(1, 0));
            deadAlly.Configure(Faction.Player, 100, false); deadAlly.KeepOnDeath();
            deadAlly.TryDamage(1000, Faction.Enemy);
            Require(skills.TrySpecial() && !deadAlly.IsDead && skills.SpecialRemaining > 179.9f && skills.SpecialCooldownDuration > 179.9f && !skills.TrySpecial(), "Revive cooldown is not 180 seconds.");
            UnityEngine.Object.Destroy(deadAlly.gameObject);
            yield return 3f;
            Require(skills.BeginSkill(0, Vector2.right), "Judgment before death failed.");
            player.Health.TryDamage(1000, Faction.Enemy);
            Require(!skills.IsHolding && skills.Cooldown(SkillId.Judgment) > 9.9f, "Death did not interrupt Judgment and start cooldown.");
        }

        private static bool NoRange() => UnityEngine.Object.FindObjectsByType<SkillIndicator>(FindObjectsSortMode.None).Length == 0;
        private static ClassSheetEffect Find(ClassArtEffect id)
        {
            foreach (ClassSheetEffect effect in UnityEngine.Object.FindObjectsByType<ClassSheetEffect>(FindObjectsSortMode.None))
                if (effect.Id == id) return effect;
            return null;
        }
    }
}
