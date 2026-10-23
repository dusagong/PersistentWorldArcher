using System;
using System.IO;
using PersistentWorld.Combat;
using PersistentWorld.Player;
using PersistentWorld.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PersistentWorld.Editor
{
    public static class PrototypeValidation
    {
        private const string SceneFolder = "Assets/_Project/Scenes";

        public static void ValidateAll()
        {
            ValidateBuildSettings();
            ValidateScene("Town", expectedEnemies: 0, expectedBosses: 0, expectedNpcs: 1, expectedPortals: 1);
            ValidateScene("Field", expectedEnemies: 4, expectedBosses: 0, expectedNpcs: 0, expectedPortals: 2);
            ValidateScene("BossArena", expectedEnemies: 0, expectedBosses: 1, expectedNpcs: 0, expectedPortals: 1);
            ValidateDamageRules();
            Debug.Log("PERSISTENT_WORLD_VALIDATION_OK");
        }

        private static void ValidateBuildSettings()
        {
            if (EditorBuildSettings.scenes.Length != 3)
            {
                throw new InvalidOperationException("Expected exactly three scenes in Build Settings.");
            }

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled || !File.Exists(scene.path))
                {
                    throw new InvalidOperationException("Missing or disabled build scene: " + scene.path);
                }
            }
        }

        private static void ValidateScene(
            string sceneName,
            int expectedEnemies,
            int expectedBosses,
            int expectedNpcs,
            int expectedPortals)
        {
            EditorSceneManager.OpenScene(SceneFolder + "/" + sceneName + ".unity");
            RequireCount<ArcherController>(sceneName, 1);
            RequireCount<EnemyController>(sceneName, expectedEnemies);
            RequireCount<BossController>(sceneName, expectedBosses);
            RequireCount<NpcDialogue>(sceneName, expectedNpcs);
            RequireCount<ScenePortal>(sceneName, expectedPortals);
            RequireCount<PrototypeHud>(sceneName, 1);

            Camera mainCamera = Camera.main;
            if (mainCamera == null || !mainCamera.orthographic)
            {
                throw new InvalidOperationException(sceneName + " requires one orthographic Main Camera.");
            }
        }

        private static void RequireCount<T>(string sceneName, int expected) where T : UnityEngine.Object
        {
            int actual = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;
            if (actual != expected)
            {
                throw new InvalidOperationException($"{sceneName}: expected {expected} {typeof(T).Name}, found {actual}.");
            }
        }

        private static void ValidateDamageRules()
        {
            GameObject target = new GameObject("Damage Validation Target");
            try
            {
                Health health = target.AddComponent<Health>();
                health.Configure(Faction.Enemy, 20, false);
                if (!health.TryDamage(10, Faction.Player) || health.Current != 10)
                {
                    throw new InvalidOperationException("Player damage did not reduce enemy health.");
                }

                if (health.TryDamage(10, Faction.Enemy) || health.Current != 10)
                {
                    throw new InvalidOperationException("Friendly damage must be ignored.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}

