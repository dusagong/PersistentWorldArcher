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
    public static class SkillThreeValidation
    {
        private const string BatchKey = "PersistentWorld.SkillThreeValidation.Batch";
        private static readonly HashSet<string> CastFrames = new HashSet<string>();
        private static readonly HashSet<string> FlightFrames = new HashSet<string>();
        private static readonly HashSet<int> Shots = new HashSet<int>();
        private static ArcherController player;
        private static Health target;
        private static Vector2 aim;
        private static Vector2 launchPosition;
        private static float startedAt;
        private static bool timingAndDirection;
        private static float travel;

        static SkillThreeValidation()
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

        [MenuItem("Tools/Persistent World/Check Skill 3 (Play Mode)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            player = UnityEngine.Object.FindFirstObjectByType<ArcherController>();
            if (player == null) throw new InvalidOperationException("No archer.");
            aim = player.AimDirection;
            Vector2 origin = (Vector2)player.transform.position + Vector2.up * .35f;
            launchPosition = origin + aim * .5f;
            GameObject obj = new GameObject("Power shot validation target");
            obj.transform.position = origin + aim * 8f;
            target = obj.AddComponent<Health>();
            target.Configure(Faction.Enemy, 100, false);
            obj.AddComponent<CircleCollider2D>().radius = .15f;
            Physics2D.SyncTransforms();
            CastFrames.Clear();
            FlightFrames.Clear();
            Shots.Clear();
            travel = 0;
            timingAndDirection = true;
            startedAt = Time.time;
            if (!player.TryPowerShot() || player.TryPowerShot()) throw new InvalidOperationException("Cast or cooldown failed.");
            Sample();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Sample()
        {
            foreach (PowerShotEffect effect in UnityEngine.Object.FindObjectsByType<PowerShotEffect>(FindObjectsSortMode.None))
            {
                string frame = effect.GetComponent<SpriteRenderer>().sprite.name;
                if (effect.IsFlight)
                {
                    FlightFrames.Add(frame);
                    Shots.Add(effect.GetInstanceID());
                    timingAndDirection &= Time.time - startedAt >= PowerShotEffect.ReleaseDelay - .02f && Vector2.Dot(effect.transform.right, aim) > .99f;
                    travel = Mathf.Max(travel, Vector2.Distance(effect.transform.position, launchPosition));
                }
                else CastFrames.Add(frame);
            }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || player == null)
            {
                EditorApplication.update -= Tick;
                return;
            }
            Sample();
            if (Time.time - startedAt < 1.8f) return;
            EditorApplication.update -= Tick;
            bool cleanup = UnityEngine.Object.FindObjectsByType<PowerShotEffect>(FindObjectsSortMode.None).Length == 0;
            bool passed = CastFrames.Count == 9 && FlightFrames.Count == 9 && Shots.Count == 1 && timingAndDirection && travel > 6f && target.Current == 50 && cleanup && player.PowerShotRemaining > 0;
            string result = $"cast={CastFrames.Count}/9, flight={FlightFrames.Count}/9, shots={Shots.Count}, timingAndDirection={timingAndDirection}, travel={travel:F2}, enemyHP={target.Current}, cleanup={cleanup}";
            if (passed) Debug.Log("SKILL_THREE_VALIDATION_OK " + result);
            else Debug.LogError("SKILL_THREE_VALIDATION_FAILED " + result);
            UnityEngine.Object.Destroy(target.gameObject);
            if (SessionState.GetBool(BatchKey, false))
            {
                SessionState.SetBool(BatchKey, false);
                EditorApplication.Exit(passed ? 0 : 1);
            }
        }
    }
}
