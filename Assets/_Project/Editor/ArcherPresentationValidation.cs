using System;
using System.Collections;
using System.Collections.Generic;
using PersistentWorld.Combat;
using PersistentWorld.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PersistentWorld.Editor
{
    [InitializeOnLoad]
    public static class ArcherPresentationValidation
    {
        private const string Key = "PersistentWorld.ArcherPresentationValidation";
        private static IEnumerator steps;
        private static float resumeAt;
        private static double deadline;
        private static int checks;

        static ArcherPresentationValidation()
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
            steps = Scenarios(); checks = 0; resumeAt = 0;
            deadline = EditorApplication.timeSinceStartup + 30;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Archer presentation validation timed out.");
                if (Time.time < resumeAt) return;
                if (steps.MoveNext()) resumeAt = Time.time + (steps.Current is float seconds ? seconds : 0);
                else Finish(true);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(false); }
        }

        private static void Finish(bool passed)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false);
            if (passed) Debug.Log("ARCHER_PRESENTATION_VALIDATION_OK checks=" + checks);
            else Debug.LogError("ARCHER_PRESENTATION_VALIDATION_FAILED checks=" + checks);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void Require(bool result, string message)
        {
            if (!result) throw new InvalidOperationException(message);
            checks++;
        }

        private static IEnumerator Scenarios()
        {
            yield return .1f;
            ArcherController player = UnityEngine.Object.FindFirstObjectByType<ArcherController>();
            player.AcceptLocalInput = false;
            player.SetAim(Vector2.right);
            PlayerSkills skills = player.Skills;
            int indicators = UnityEngine.Object.FindObjectsByType<SkillIndicator>(FindObjectsSortMode.None).Length;
            Require(skills.BeginSkill(0, Vector2.right), "Repulse unavailable.");
            Require(UnityEngine.Object.FindObjectsByType<SkillIndicator>(FindObjectsSortMode.None).Length == indicators, "Repulse still creates a range indicator.");
            Require(UnityEngine.Object.FindFirstObjectByType<SkillOneEffect>() != null, "Repulse arrow art was removed.");
            yield return .65f;
            Require(player.Loadout.Equip(0, SkillId.Snipe), "Snipe equip failed.");
            Require(skills.BeginSkill(0, Vector2.right) && skills.MovementLocked, "Snipe did not start charging.");
            player.SetAim(Vector2.up);
            yield return .15f;
            LineRenderer line = UnityEngine.Object.FindFirstObjectByType<SkillIndicator>().GetComponent<LineRenderer>();
            Require(Vector2.Dot((line.GetPosition(1) - line.GetPosition(0)).normalized, Vector2.up) > .99f, "Snipe preview does not follow charging aim.");
            GameObject target = new GameObject("Latest aim validation target");
            target.transform.position = new Vector2(-4, .35f);
            Health health = target.AddComponent<Health>(); health.Configure(Faction.Enemy, 100, false);
            target.AddComponent<CircleCollider2D>().radius = .2f;
            Physics2D.SyncTransforms();
            player.SetAim(Vector2.left);
            Require(skills.ReleaseSkill(0), "Snipe release failed.");
            player.SetAim(Vector2.right);
            yield return .62f;
            PowerShotEffect flight = null;
            foreach (PowerShotEffect effect in UnityEngine.Object.FindObjectsByType<PowerShotEffect>(FindObjectsSortMode.None))
                if (effect.IsFlight) flight = effect;
            Require(flight != null && Vector2.Dot(flight.transform.right, Vector2.left) > .99f, "Arrow ignored release-frame aim or changed after release.");
            yield return .4f;
            Require(health.Current < 100, "Arrow missed the target in its updated direction.");
            UnityEngine.Object.Destroy(target);
            Require(skills.TrySpecial(), "Archer special did not start.");
            float buffStarted = Time.time;
            BasicAttackEffect buff = null;
            foreach (BasicAttackEffect effect in UnityEngine.Object.FindObjectsByType<BasicAttackEffect>(FindObjectsSortMode.None))
                if (effect.IsBuff && effect.Owner == player.transform) buff = effect;
            Require(buff != null, "No continuous image for archer special.");
            HashSet<string> frames = new HashSet<string>();
            string last = null;
            bool looped = false;
            while (Time.time - buffStarted < 1)
            {
                string frame = buff.GetComponent<SpriteRenderer>().sprite.name;
                if (frame != last && frames.Contains(frame)) looped = true;
                frames.Add(frame); last = frame;
                yield return null;
            }
            Require(frames.Count == 4 && looped && buff != null, "Special image does not loop all four frames.");
            Require(UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length == 0, "Image loop incorrectly fires arrows without input.");
            player.SetAim(Vector2.up);
            player.transform.position += Vector3.up * .5f;
            yield return .1f;
            Require(Quaternion.Angle(buff.transform.rotation, Quaternion.Euler(0, 0, 45)) < .1f && Vector2.Distance(buff.transform.position, (Vector2)player.transform.position + Vector2.up * .65f) < .01f, "Special image does not follow player and aim.");
            player.SetMenuOpen(true);
            yield return .1f;
            Require(buff != null && skills.SpecialActive, "Opening menu removed an active special image.");
            player.SetMenuOpen(false);
            Require(player.TryBasicAttack(), "Cannot attack while special is active.");
            HashSet<int> shots = new HashSet<int>();
            float sampledUntil = Time.time + .4f;
            while (Time.time < sampledUntil)
            {
                foreach (Projectile projectile in UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)) shots.Add(projectile.GetInstanceID());
                yield return null;
            }
            Require(shots.Count == 2 && UnityEngine.Object.FindObjectsByType<BasicAttackEffect>(FindObjectsSortMode.None).Length == 1, "Special attack duplicates the shared bow or fires the wrong number of arrows.");
            while (Time.time - buffStarted < 4.9f) yield return null;
            Require(buff != null && skills.SpecialActive, "Special image stopped before the buff ended.");
            yield return .2f;
            Require(!skills.SpecialActive && buff == null, "Special image outlived the buff.");
        }
    }
}
