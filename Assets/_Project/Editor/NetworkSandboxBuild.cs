using System;
using System.IO;
using PersistentWorld.Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersistentWorld.Editor
{
    public static class NetworkSandboxBuild
    {
        public const string Folder = "Assets/_Project/Networking";
        public const string ScenePath = Folder + "/NetworkSandbox.unity";
        [MenuItem("Tools/Persistent World/Multiplayer/Prepare Test Scene")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Folder);
            string texturePath = Folder + "/SandboxSquare.png";
            if (!File.Exists(texturePath))
            {
                Texture2D texture = new Texture2D(2, 2);
                texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); texture.Apply();
                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(texturePath);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 2;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            GameObject actor = new GameObject("Network Test Player");
            actor.AddComponent<SpriteRenderer>().sprite = square;
            actor.transform.localScale = new Vector3(.6f, .8f, 1);
            actor.AddComponent<NetworkObject>(); actor.AddComponent<NetworkSandboxPlayer>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(actor, Folder + "/NetworkSandboxPlayer.prefab");
            UnityEngine.Object.DestroyImmediate(actor);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.orthographic = true; camera.orthographicSize = 5.5f;
            camera.backgroundColor = new Color(.06f, .09f, .12f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(0, 0, -10);
            GameObject ground = new GameObject("Test Field");
            ground.transform.localScale = new Vector3(17, 9, 1);
            SpriteRenderer field = ground.AddComponent<SpriteRenderer>(); field.sprite = square;
            field.color = new Color(.13f, .22f, .23f); field.sortingOrder = -10;
            GameObject root = new GameObject("Network Sandbox");
            UnityTransport transport = root.AddComponent<UnityTransport>(); transport.UseWebSockets = true;
            NetworkManager manager = root.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport, PlayerPrefab = prefab, ConnectionApproval = true,
                EnableSceneManagement = false, TickRate = 20, ClientConnectionBufferTimeout = 10
            };
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            NetworkSandbox sandbox = root.AddComponent<NetworkSandbox>();
            SerializedObject serialized = new SerializedObject(sandbox);
            serialized.FindProperty("manager").objectReferenceValue = manager; serialized.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<NetworkSandboxProbe>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("PWA_NETWORK_SCENE_READY");
        }
        [MenuItem("Tools/Persistent World/Multiplayer/Build Windows Test Client")]
        public static void BuildWindows()
        {
            Prepare();
            Build(BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player, "Builds/NetworkWindows/PwaNetwork.exe");
        }
        [MenuItem("Tools/Persistent World/Multiplayer/Build Railway Linux Server")]
        public static void BuildLinux()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
                throw new InvalidOperationException("Install Linux Dedicated Server Build Support for Unity 6000.0.60f1 in Unity Hub.");
            Prepare();
            Build(BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server, "Builds/Railway/Server/PwaServer");
        }
        private static void Build(BuildTarget target, StandaloneBuildSubtarget subtarget, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = path, target = target,
                subtarget = (int)subtarget, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Network build failed: " + report.summary.result);
            Debug.Log("PWA_NETWORK_BUILD_OK target=" + target + " path=" + path);
        }
    }
}
