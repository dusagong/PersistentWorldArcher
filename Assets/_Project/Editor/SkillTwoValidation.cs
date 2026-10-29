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
    public static class SkillTwoValidation
    {
        private const string BatchKey = "PersistentWorld.SkillTwoValidation.Batch";
        private static readonly HashSet<string> Frames = new HashSet<string>();
        private static ArcherController player;
        private static ArrowRainEffect effect;
        private static Health enemy;
        private static Health ally;
        private static Health outside;
        private static float startedAt;

        static SkillTwoValidation()
        {
            if (SessionState.GetBool(BatchKey, false)) EditorApplication.playModeStateChanged += OnPlayModeChanged;
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

        [MenuItem("Tools/Persistent World/Check Skill 2 (Play Mode)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            player = UnityEngine.Object.FindFirstObjectByType<ArcherController>();
            if (player == null) throw new InvalidOperationException("No archer in scene.");
            Vector2 origin = player.transform.position;
            Vector2 center = origin + Vector2.up * 6f;
            enemy = MakeTarget(center, Faction.Enemy);
            enemy.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one * .2f;
            ally = MakeTarget(center + Vector2.right * .3f, Faction.Player);
            outside = MakeTarget(center + Vector2.up * 2f, Faction.Enemy);
            Physics2D.SyncTransforms();
            Frames.Clear();
            startedAt = Time.time;
            if (!player.TryArrowRain(origin + Vector2.up * 100f)) throw new InvalidOperationException("R was not ready.");
            if (player.TryArrowRain(center)) throw new InvalidOperationException("R ignored cooldown.");
            effect = UnityEngine.Object.FindFirstObjectByType<ArrowRainEffect>();
            if (effect == null || Vector2.Distance(effect.transform.position, center) > .01f || enemy.Current != 100)
                throw new InvalidOperationException("Range clamp, spawn or delayed impact failed.");
            Frames.Add(effect.GetComponent<SpriteRenderer>().sprite.name);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static Health MakeTarget(Vector2 position, Faction faction)
        {
            GameObject obj = new GameObject("Skill 2 validation target");
            obj.transform.position = position;
            Health health = obj.AddComponent<Health>();
            health.Configure(faction, 100, false);
            obj.AddComponent<CircleCollider2D>().radius = .1f;
            return health;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || player == null)
            {
                EditorApplication.update -= Tick;
                return;
            }
            if (effect != null) Frames.Add(effect.GetComponent<SpriteRenderer>().sprite.name);
            if (Time.time - startedAt < .9f) return;
            EditorApplication.update -= Tick;
            bool passed = Frames.Count == 9 && effect == null && enemy.Current == 70 && ally.Current == 100 && outside.Current == 100 && player.ArrowRainRemaining > 0;
            string result = $"frames={Frames.Count}/9, enemyHP={enemy.Current}, allyHP={ally.Current}, outsideHP={outside.Current}, cleanup={effect == null}";
            if (passed) Debug.Log("SKILL_TWO_VALIDATION_OK " + result);
            else Debug.LogError("SKILL_TWO_VALIDATION_FAILED " + result);
            UnityEngine.Object.Destroy(enemy.gameObject);
            UnityEngine.Object.Destroy(ally.gameObject);
            UnityEngine.Object.Destroy(outside.gameObject);
            if (SessionState.GetBool(BatchKey, false))
            {
                SessionState.SetBool(BatchKey, false);
                EditorApplication.Exit(passed ? 0 : 1);
            }
        }
    }
}
