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
            ValidatePlayerArt();
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
            RequireCount<ArcherSpriteAnimator>(sceneName, 1);
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

        private static void ValidatePlayerArt()
        {
            foreach (string name in new[] { "BasicAttackBow", "BasicAttackArrow" })
            {
                Texture2D basic = Resources.Load<Texture2D>("Effects/" + name);
                if (basic == null || basic.width != 1254 || basic.height != 1254)
                    throw new InvalidOperationException(name + " requires the original 1254x1254 image.");
            }
            Texture2D texture = Resources.Load<Texture2D>("Characters/ArcherRun");
            if (texture == null)
            {
                throw new InvalidOperationException("Missing Resources/Characters/ArcherRun texture.");
            }

            if (!texture.isReadable || texture.width % 4 != 0 || texture.height % 2 != 0)
            {
                throw new InvalidOperationException("ArcherRun must be readable and divisible into a 4x2 frame grid.");
            }
            Texture2D dash = Resources.Load<Texture2D>("Characters/ArcherDash");
            if (dash == null || dash.width != 2560 || dash.height != 1080)
                throw new InvalidOperationException("ArcherDash must retain its 2560x1080 4x3 frame grid.");
            Texture2D skill = Resources.Load<Texture2D>("Effects/SkillOneVolley");
            if (skill == null || skill.width != 1026 || skill.height != 684)
                throw new InvalidOperationException("SkillOneVolley must retain its 1026x684 3x2 grid.");
            Texture2D rain = Resources.Load<Texture2D>("Effects/SkillTwoArrowRain");
            if (rain == null || rain.width != 1026 || rain.height != 1026)
                throw new InvalidOperationException("SkillTwoArrowRain must retain its 1026x1026 3x3 grid.");
            Texture2D cast = Resources.Load<Texture2D>("Effects/SkillThreeCast");
            Texture2D flight = Resources.Load<Texture2D>("Effects/SkillThreeFlight");
            if (cast == null || cast.width != 1254 || cast.height != 1254 || flight == null || flight.width != 1026 || flight.height != 1026)
                throw new InvalidOperationException("Skill 3 requires its separate original 3x3 cast and flight sheets.");
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

