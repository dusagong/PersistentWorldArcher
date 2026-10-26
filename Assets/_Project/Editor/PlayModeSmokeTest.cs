using System;
using PersistentWorld.Core;
using PersistentWorld.Player;
using PersistentWorld.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersistentWorld.Editor
{
    [InitializeOnLoad]
    public static class PlayModeSmokeTest
    {
        private const string RunningKey = "PersistentWorld.PlayModeSmokeTest.Running";
        private const string StageKey = "PersistentWorld.PlayModeSmokeTest.Stage";
        private const string StartedAtKey = "PersistentWorld.PlayModeSmokeTest.StartedAt";
        private const string TownScene = "Assets/_Project/Scenes/Town.unity";

        static PlayModeSmokeTest()
        {
            if (SessionState.GetBool(RunningKey, false))
            {
                AttachCallbacks();
            }
        }

        public static void Run()
        {
            SessionState.SetBool(RunningKey, true);
            SessionState.SetString(StageKey, "EnteringPlayMode");
            AttachCallbacks();
            EditorSceneManager.OpenScene(TownScene);
            EditorApplication.EnterPlaymode();
        }

        private static void AttachCallbacks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(RunningKey, false))
            {
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetString(StageKey, "Town");
                SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
            }
            else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetString(StageKey, string.Empty) == "Finishing")
            {
                CompleteSuccessfully();
            }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(RunningKey, false) || !EditorApplication.isPlaying)
            {
                return;
            }

            float startedAt = SessionState.GetFloat(StartedAtKey, 0f);
            if (EditorApplication.timeSinceStartup - startedAt < 0.75d)
            {
                return;
            }

            try
            {
                string stage = SessionState.GetString(StageKey, string.Empty);
                if (stage == "Town")
                {
                    RequireRuntimeComponent<ArcherController>(1);
                    RequireRuntimeComponent<ArcherSpriteAnimator>(1);
                    RequireRuntimeComponent<NpcDialogue>(1);
                    RequirePlayerSprite();
                    RequireRuntimeVisuals();
                    LoadNext("Field", "Field");
                }
                else if (stage == "Field")
                {
                    RequireRuntimeComponent<EnemyController>(4);
                    RequireRuntimeComponent<ScenePortal>(2);
                    LoadNext("BossArena", "BossArena");
                }
                else if (stage == "BossArena")
                {
                    RequireRuntimeComponent<BossController>(1);
                    RequireRuntimeComponent<ArcherController>(1);
                    SessionState.SetString(StageKey, "Finishing");
                    EditorApplication.ExitPlaymode();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetBool(RunningKey, false);
                EditorApplication.Exit(1);
            }
        }

        private static void LoadNext(string sceneName, string nextStage)
        {
            SceneManager.LoadScene(sceneName);
            SessionState.SetString(StageKey, nextStage);
            SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
        }

        private static void RequireRuntimeComponent<T>(int expected) where T : UnityEngine.Object
        {
            int actual = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;
            if (actual != expected)
            {
                throw new InvalidOperationException($"Play mode expected {expected} {typeof(T).Name}, found {actual}.");
            }
        }

        private static void RequireRuntimeVisuals()
        {
            PlaceholderVisual[] visuals = UnityEngine.Object.FindObjectsByType<PlaceholderVisual>(FindObjectsSortMode.None);
            if (visuals.Length == 0)
            {
                throw new InvalidOperationException("Play mode did not load placeholder visuals.");
            }

            foreach (PlaceholderVisual visual in visuals)
            {
                SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
                if (renderer == null || renderer.sprite == null)
                {
                    throw new InvalidOperationException("A runtime placeholder visual has no sprite.");
                }
            }
        }

        private static void RequirePlayerSprite()
        {
            ArcherSpriteAnimator animator = UnityEngine.Object.FindFirstObjectByType<ArcherSpriteAnimator>();
            SpriteRenderer renderer = animator == null ? null : animator.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null)
            {
                throw new InvalidOperationException("The player animation did not load a sprite frame.");
            }
        }

        private static void CompleteSuccessfully()
        {
            SessionState.SetBool(RunningKey, false);
            SessionState.EraseString(StageKey);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Debug.Log("PERSISTENT_WORLD_PLAYMODE_OK");
            EditorApplication.Exit(0);
        }
    }
}

