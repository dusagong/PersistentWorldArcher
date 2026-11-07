using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersistentWorld.Combat;
using PersistentWorld.Items;
using PersistentWorld.Networking;
using PersistentWorld.Player;
using PersistentWorld.World;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersistentWorld.Editor
{
    public static class NetworkGameBuild
    {
        public const string Folder = "Assets/_Project/Networking";
        public const string ScenePath = Folder + "/NetworkGame.unity";
        [MenuItem("Tools/Persistent World/Multiplayer/Prepare Cooperative World")]
        public static void Prepare()
        {
            const string materials = "Assets/Cainos/Pixel Art Top Down - Basic/Material/";
            foreach (string name in new[] { "MT Tileset", "MT Shadow" })
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materials + name + ".mat");
                if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                    throw new InvalidOperationException("Import the Cainos tileset and shadow materials with their shaders before building the cooperative world.");
            }
            NetworkSandboxBuild.Prepare();
            Scene destination = SceneManager.GetActiveScene();
            UnityEngine.Object.DestroyImmediate(GameObject.Find("Test Field"));
            NetworkSandbox sandbox = UnityEngine.Object.FindFirstObjectByType<NetworkSandbox>();
            NetworkGameWorld world = sandbox.gameObject.AddComponent<NetworkGameWorld>();
            sandbox.gameObject.AddComponent<NetworkCoopValidation>();
            world.manager = sandbox.Manager; world.gameCamera = Camera.main; world.gameCamera.orthographicSize = 6;
            world.maps = new Transform[3];
            world.mapBackgrounds = new Color[3];
            world.mapCameraSizes = new float[3];
            world.mapSpawns = new Vector2[3];
            foreach (GameArea area in Enum.GetValues(typeof(GameArea)))
            {
                Scene source = EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + area + ".unity", OpenSceneMode.Additive);
                GameObject map = new GameObject(area + " World"); SceneManager.MoveGameObjectToScene(map, destination);
                world.maps[(int)area] = map.transform;
                foreach (GameObject root in source.GetRootGameObjects())
                {
                    if (root.TryGetComponent(out Camera sourceCamera))
                    {
                        world.mapBackgrounds[(int)area] = sourceCamera.backgroundColor;
                        world.mapCameraSizes[(int)area] = sourceCamera.orthographicSize;
                        UnityEngine.Object.DestroyImmediate(root);
                        continue;
                    }
                    if (root.GetComponent<PrototypeHud>() != null)
                    { UnityEngine.Object.DestroyImmediate(root); continue; }
                    if (root.GetComponent<ArcherController>() != null)
                    {
                        world.mapSpawns[(int)area] = root.transform.position;
                        if (area == GameArea.Town)
                        {
                            root.AddComponent<NetworkActorContext>();
                            if (root.GetComponent<PlayerEquipment>() == null) root.AddComponent<PlayerEquipment>();
                            world.actorPrefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/CooperativeActor.prefab");
                        }
                        UnityEngine.Object.DestroyImmediate(root); continue;
                    }
                    SceneManager.MoveGameObjectToScene(root, destination);
                    root.transform.position += (Vector3)NetworkGameWorld.Origin(area);
                    BossController boss = root.GetComponent<BossController>();
                    if (boss != null)
                    {
                        SerializedObject serialized = new SerializedObject(boss);
                        serialized.FindProperty("arenaCenter").vector2Value += NetworkGameWorld.Origin(area);
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (root.GetComponentsInChildren<Health>(true).Length > 0)
                    {
                        ServerWorldEntity gate = root.AddComponent<ServerWorldEntity>(); gate.area = area;
                        gate.serverBehaviours = root.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b != gate && b.enabled).ToArray();
                        foreach (MonoBehaviour behaviour in gate.serverBehaviours) behaviour.enabled = false;
                        if (boss != null) world.bossPrefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/CooperativeBoss.prefab");
                    }
                    root.transform.SetParent(map.transform, true);
                }
                EditorSceneManager.CloseScene(source, true);
            }
            string registryPath = Folder + "/NetworkGameAssets.asset";
            NetworkGameAssets registry = AssetDatabase.LoadAssetAtPath<NetworkGameAssets>(registryPath);
            if (registry == null) { registry = ScriptableObject.CreateInstance<NetworkGameAssets>(); AssetDatabase.CreateAsset(registry, registryPath); }
            registry.textures = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<Texture2D>).Where(t => t != null).ToArray();
            EditorUtility.SetDirty(registry); world.assets = registry;
            if (world.actorPrefab == null || world.bossPrefab == null) throw new InvalidOperationException("Missing cooperative actor or boss template.");
            SceneManager.SetActiveScene(destination); EditorSceneManager.SaveScene(destination, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("PWA_COOPERATIVE_WORLD_READY textures=" + registry.textures.Length);
        }
        [MenuItem("Tools/Persistent World/Multiplayer/Build Cooperative Windows Client")]
        public static void BuildWindows() { Prepare(); Build(BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player, "Builds/CooperativeWindows/PwaCoop.exe"); }
        [MenuItem("Tools/Persistent World/Multiplayer/Build Cooperative Linux Server")]
        public static void BuildLinux()
        {
            Prepare();
            bool optimizations = PlayerSettings.dedicatedServerOptimizations;
            try
            {
                // Renderer metadata is used for visual snapshots, although the server never draws.
                PlayerSettings.dedicatedServerOptimizations = false;
                Build(BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server, "Builds/Railway/Server/PwaServer");
            }
            finally { PlayerSettings.dedicatedServerOptimizations = optimizations; }
        }
        private static void Build(BuildTarget target, StandaloneBuildSubtarget subtarget, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            { scenes = new[] { ScenePath }, target = target, subtarget = (int)subtarget, locationPathName = path, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Cooperative build failed: " + report.summary.result);
            Debug.Log("PWA_COOPERATIVE_BUILD_OK " + target);
        }
    }
}
