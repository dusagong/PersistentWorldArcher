using System;
using System.Collections.Generic;
using PersistentWorld.Player;
using UnityEditor;
using UnityEngine;

namespace PersistentWorld.Editor
{
    public static class DashAnimationValidation
    {
        private static readonly HashSet<string> Frames = new HashSet<string>();
        private static ArcherController player;
        private static SpriteRenderer renderer;
        private static float startedAt;
        private static Vector2 startPosition;

        [MenuItem("Tools/Persistent World/Check Dash Animation (Play Mode)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            player = UnityEngine.Object.FindFirstObjectByType<ArcherController>();
            if (player == null) throw new InvalidOperationException("No archer in the scene.");
            renderer = player.GetComponent<SpriteRenderer>();
            Frames.Clear();
            startPosition = player.transform.position;
            startedAt = Time.time;
            if (!player.TryBackstep()) throw new InvalidOperationException("Wait until backstep is ready.");
            if (player.TryBackstep()) throw new InvalidOperationException("Backstep ignored its cooldown.");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || player == null)
            {
                EditorApplication.update -= Tick;
                return;
            }
            if (renderer.sprite != null && renderer.sprite.name.StartsWith("ArcherDash_")) Frames.Add(renderer.sprite.name);
            if (Time.time - startedAt < .75f) return;
            EditorApplication.update -= Tick;
            float distance = Vector2.Distance(startPosition, player.transform.position);
            if (Frames.Count < 10 || renderer.sprite == null || !renderer.sprite.name.StartsWith("ArcherRun_") || distance < .5f)
                Debug.LogError($"DASH_ANIMATION_VALIDATION_FAILED frames={Frames.Count}, distance={distance}, final={renderer.sprite?.name}");
            else
                Debug.Log($"DASH_ANIMATION_VALIDATION_OK frames={Frames.Count}/12, distance={distance:F2}, final={renderer.sprite.name}");
        }
    }
}
