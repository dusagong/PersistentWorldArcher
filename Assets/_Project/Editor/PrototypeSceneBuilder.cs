using System.IO;
using PersistentWorld.Combat;
using PersistentWorld.Core;
using PersistentWorld.Player;
using PersistentWorld.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersistentWorld.Editor
{
    public static class PrototypeSceneBuilder
    {
        private const string SceneFolder = "Assets/_Project/Scenes";

        [MenuItem("Tools/Persistent World/Rebuild Prototype Scenes")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(SceneFolder);
            BuildTown();
            BuildField();
            BuildBossArena();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneFolder + "/Town.unity", true),
                new EditorBuildSettingsScene(SceneFolder + "/Field.unity", true),
                new EditorBuildSettingsScene(SceneFolder + "/BossArena.unity", true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("PERSISTENT_WORLD_PROTOTYPE_READY");
        }

        private static void BuildTown()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateEnvironment(new Color(0.18f, 0.31f, 0.22f));
            CreatePlayer(Vector2.zero);
            CreateNpc(new Vector2(2f, 1f));
            CreatePortal(new Vector2(7.5f, 0f), "Field", "Enter the field");
            CreateWalls(10f, 6f);
            EditorSceneManager.SaveScene(scene, SceneFolder + "/Town.unity");
        }

        private static void BuildField()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateEnvironment(new Color(0.17f, 0.25f, 0.16f));
            CreatePlayer(new Vector2(-6f, 0f));
            CreatePortal(new Vector2(-8f, -4f), "Town", "Return to town");
            CreatePortal(new Vector2(8f, 4f), "BossArena", "Enter the boss arena");
            CreateEnemy(new Vector2(-1f, 2f), false);
            CreateEnemy(new Vector2(2f, 0f), false);
            CreateEnemy(new Vector2(4f, -2f), false);
            CreateEnemy(new Vector2(0f, -3f), true);
            CreateWalls(10f, 6f);
            EditorSceneManager.SaveScene(scene, SceneFolder + "/Field.unity");
        }

        private static void BuildBossArena()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateEnvironment(new Color(0.25f, 0.12f, 0.14f));
            CreatePlayer(new Vector2(-6f, 0f));
            CreateBoss(new Vector2(2f, 0f));
            CreatePortal(new Vector2(-8f, -4f), "Field", "Leave the boss arena");
            CreateWalls(10f, 6f);
            EditorSceneManager.SaveScene(scene, SceneFolder + "/BossArena.unity");
        }

        private static void CreateEnvironment(Color background)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.625f;
            camera.backgroundColor = background;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CameraFollow>();

            new GameObject("Prototype HUD").AddComponent<PrototypeHud>();
        }

        private static GameObject CreatePlayer(Vector2 position)
        {
            GameObject player = new GameObject("Player Archer");
            player.tag = "Player";
            player.transform.position = position;

            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D collider = player.AddComponent<CircleCollider2D>();
            collider.radius = 0.28f;

            Health health = player.AddComponent<Health>();
            health.Configure(Faction.Player, 100, false);
            player.AddComponent<ArcherController>();

            PlaceholderVisual visual = player.AddComponent<PlaceholderVisual>();
            visual.Configure(new Color(0.2f, 0.75f, 1f), new Vector2(0.8f, 1.2f));

            Camera.main.GetComponent<CameraFollow>().SetTarget(player.transform);
            return player;
        }

        private static void CreateNpc(Vector2 position)
        {
            GameObject npc = CreateMarker("Village Guide", position, new Color(1f, 0.75f, 0.25f), new Vector2(0.8f, 1.1f));
            CircleCollider2D collider = npc.AddComponent<CircleCollider2D>();
            collider.radius = 0.45f;
            collider.isTrigger = true;
            NpcDialogue dialogue = npc.AddComponent<NpcDialogue>();
            dialogue.Configure("Village Guide", "Welcome. The field gate is to the east.", "Q fires twice, E steps back, and right click empowers arrows.");
        }

        private static void CreatePortal(Vector2 position, string destination, string prompt)
        {
            GameObject portal = CreateMarker(destination + " Gate", position, new Color(0.65f, 0.3f, 1f), new Vector2(0.9f, 1.6f));
            BoxCollider2D collider = portal.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.9f, 1.6f);
            collider.isTrigger = true;
            ScenePortal scenePortal = portal.AddComponent<ScenePortal>();
            scenePortal.Configure(destination, prompt);
        }

        private static void CreateEnemy(Vector2 position, bool rare)
        {
            GameObject enemy = CreateMarker(rare ? "Rare Enemy" : "Enemy", position, rare ? new Color(1f, 0.25f, 0.8f) : new Color(1f, 0.4f, 0.2f), Vector2.one * 0.8f);
            Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            CircleCollider2D collider = enemy.AddComponent<CircleCollider2D>();
            collider.radius = 0.35f;
            Health health = enemy.AddComponent<Health>();
            health.Configure(Faction.Enemy, rare ? 60 : 20);
            enemy.AddComponent<EnemyController>();
        }

        private static void CreateBoss(Vector2 position)
        {
            GameObject boss = CreateMarker("Prototype Boss", position, new Color(0.8f, 0.12f, 0.12f), new Vector2(1.8f, 1.8f));
            CircleCollider2D collider = boss.AddComponent<CircleCollider2D>();
            collider.radius = 0.85f;
            Health health = boss.AddComponent<Health>();
            health.Configure(Faction.Enemy, 800);
            boss.AddComponent<BossController>();
        }

        private static void CreateWalls(float halfWidth, float halfHeight)
        {
            CreateWall("North Wall", new Vector2(0f, halfHeight), new Vector2(halfWidth * 2f, 0.5f));
            CreateWall("South Wall", new Vector2(0f, -halfHeight), new Vector2(halfWidth * 2f, 0.5f));
            CreateWall("East Wall", new Vector2(halfWidth, 0f), new Vector2(0.5f, halfHeight * 2f));
            CreateWall("West Wall", new Vector2(-halfWidth, 0f), new Vector2(0.5f, halfHeight * 2f));
        }

        private static void CreateWall(string name, Vector2 position, Vector2 size)
        {
            GameObject wall = CreateMarker(name, position, new Color(0.08f, 0.08f, 0.1f), size);
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            wall.AddComponent<WorldObstacle>();
        }

        private static GameObject CreateMarker(string name, Vector2 position, Color color, Vector2 size)
        {
            GameObject marker = new GameObject(name);
            marker.transform.position = position;
            PlaceholderVisual visual = marker.AddComponent<PlaceholderVisual>();
            visual.Configure(color, size);
            return marker;
        }
    }
}

