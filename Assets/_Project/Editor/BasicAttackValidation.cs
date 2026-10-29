using System;
using System.Collections.Generic;
using PersistentWorld.Combat;
using PersistentWorld.Core;
using PersistentWorld.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PersistentWorld.Editor
{
    [InitializeOnLoad]
    public static class BasicAttackValidation
    {
        private const string Key = "PersistentWorld.BasicAttackValidation.Batch";
        private static readonly HashSet<string> Frames = new HashSet<string>();
        private static readonly HashSet<int> Shots = new HashSet<int>();
        private static ArcherController player;
        private static Health target;
        private static Vector2 aim;
        private static float startedAt;
        private static bool valid;

        static BasicAttackValidation()
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
            try
            {
                player = UnityEngine.Object.FindFirstObjectByType<ArcherController>();
                aim = player.AimDirection;
                GameObject obj = new GameObject("Basic attack validation target");
                obj.transform.position = (Vector2)player.transform.position + Vector2.up * .35f + aim * 4f;
                target = obj.AddComponent<Health>();
                target.Configure(Faction.Enemy, 100, false);
                obj.AddComponent<CircleCollider2D>().radius = .15f;
                Physics2D.SyncTransforms();
                Frames.Clear();
                Shots.Clear();
                startedAt = Time.time;
                valid = player.TryBasicAttack() && !player.TryBasicAttack();
                Sample();
                EditorApplication.update += Tick;
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        private static void Sample()
        {
            foreach (BasicAttackEffect effect in UnityEngine.Object.FindObjectsByType<BasicAttackEffect>(FindObjectsSortMode.None))
                Frames.Add(effect.GetComponent<SpriteRenderer>().sprite.name);
            foreach (Projectile shot in UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                if (shot.name != "Basic Attack Arrow") continue;
                Shots.Add(shot.GetInstanceID());
                SpriteRenderer visual = shot.transform.GetChild(0).GetComponent<SpriteRenderer>();
                valid &= visual.sprite.name == "BasicArrow" && !shot.GetComponent<SpriteRenderer>().enabled;
                valid &= Time.time - startedAt >= BasicAttackEffect.ReleaseDelay - .02f;
                valid &= Vector2.Dot(shot.transform.right, aim) > .99f;
                valid &= Vector2.Dot(visual.transform.TransformDirection(new Vector3(1, 1, 0)).normalized, aim) > .99f;
            }
        }

        private static void Tick()
        {
            Sample();
            if (Time.time - startedAt < .85f) return;
            EditorApplication.update -= Tick;
            bool cleanup = UnityEngine.Object.FindObjectsByType<BasicAttackEffect>(FindObjectsSortMode.None).Length == 0 && UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length == 0;
            bool passed = valid && Frames.Count == 4 && Shots.Count == 1 && target.Current == 90 - player.EquipmentBonuses.Attack && cleanup;
            string result = $"frames={Frames.Count}/4, arrows={Shots.Count}, timingDirectionCooldown={valid}, enemyHP={target.Current}, cleanup={cleanup}";
            if (passed) Debug.Log("BASIC_ATTACK_VALIDATION_OK " + result);
            else Debug.LogError("BASIC_ATTACK_VALIDATION_FAILED " + result);
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
