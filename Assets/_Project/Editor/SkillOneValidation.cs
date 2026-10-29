using System;
using System.Collections.Generic;
using PersistentWorld.Combat;
using PersistentWorld.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PersistentWorld.Editor
{
    [InitializeOnLoad]
    public static class SkillOneValidation
    {
        private static readonly HashSet<string> Frames = new HashSet<string>();
        private static readonly HashSet<int> Projectiles = new HashSet<int>();
        private static ArcherController player;
        private static SkillOneEffect effect;
        private static float startedAt;
        private const string BatchKey = "PersistentWorld.SkillOneValidation.Batch";

        static SkillOneValidation()
        {
            if (SessionState.GetBool(BatchKey, false))
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void RunBatch()
        {
            PrototypeValidation.ValidateAll();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Town.unity");
            SessionState.SetBool(BatchKey, true);
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            try { Run(); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/Persistent World/Check Skill 1 (Play Mode)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            player = UnityEngine.Object.FindFirstObjectByType<ArcherController>();
            if (player == null) throw new InvalidOperationException("No archer in scene.");
            Frames.Clear();
            Projectiles.Clear();
            startedAt = Time.time;
            Vector2 aim = player.AimDirection;
            if (!player.TryDoubleShot()) throw new InvalidOperationException("Wait until Q is ready.");
            if (player.TryDoubleShot()) throw new InvalidOperationException("Q ignored its cooldown.");
            effect = UnityEngine.Object.FindFirstObjectByType<SkillOneEffect>();
            if (effect == null || Vector2.Dot(effect.transform.right, -aim) < .99f)
                throw new InvalidOperationException("Skill 1 did not spawn facing its aim direction.");
            Sample();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Sample()
        {
            if (effect != null) Frames.Add(effect.GetComponent<SpriteRenderer>().sprite.name);
            foreach (Projectile projectile in UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                Projectiles.Add(projectile.GetInstanceID());
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || player == null)
            {
                EditorApplication.update -= Tick;
                return;
            }
            Sample();
            if (Time.time - startedAt < .65f) return;
            EditorApplication.update -= Tick;
            bool passed = Frames.Count == SkillOneEffect.FrameCount && Projectiles.Count == 2 && effect == null && player.DoubleShotRemaining > 0;
            if (passed) Debug.Log($"SKILL_ONE_VALIDATION_OK frames={Frames.Count}/6, projectiles=2, cleanup=OK, cooldown=OK");
            else Debug.LogError($"SKILL_ONE_VALIDATION_FAILED frames={Frames.Count}, projectiles={Projectiles.Count}, effectAlive={effect != null}");
            if (SessionState.GetBool(BatchKey, false))
            {
                SessionState.SetBool(BatchKey, false);
                EditorApplication.Exit(passed ? 0 : 1);
            }
        }
    }
}
