using System;
using System.Collections;
using System.Collections.Generic;
using PersistentWorld.Combat;
using PersistentWorld.Player;
using UnityEngine;

namespace PersistentWorld.Editor
{
    public static class ClassArtValidation
    {
        private static int checks;
        private static void Require(bool result, string message) { if (!result) throw new InvalidOperationException(message); checks++; }
        public static void CheckDefinitions()
        {
            checks = 0;
            int[] counts = { 9, 16, 8, 12, 4, 4, 3, 8, 16, 12, 16 };
            foreach (ClassArtEffect id in Enum.GetValues(typeof(ClassArtEffect)))
                Require(ClassSheetEffect.Frames(id)?.Length == counts[(int)id], "Wrong sheet grid: " + id);
            foreach (CharacterClass type in Enum.GetValues(typeof(CharacterClass)))
            {
                int run = type == CharacterClass.Archer ? 8 : type == CharacterClass.Warrior ? 11 : 12;
                Require(ArcherSpriteAnimator.RunFrames(type)?.Length == run, "Wrong run grid: " + type);
                Require(ArcherSpriteAnimator.DashFrames(type)?.Length == (type == CharacterClass.Healer ? 24 : 12), "Wrong dash grid: " + type);
                Require(Resources.Load<Sprite>("Characters/" + type + "InventoryIdle") != null, "Missing portrait: " + type);
            }
            Require(Resources.Load<Texture2D>("Characters/WarriorRun").width == 7040, "Warrior sheet was resized on import.");
        }

        public static bool Has(ClassArtEffect id)
        {
            foreach (ClassSheetEffect effect in UnityEngine.Object.FindObjectsByType<ClassSheetEffect>(FindObjectsSortMode.None)) if (effect.Id == id) return true;
            return false;
        }

        public static IEnumerator Runtime(ArcherController player)
        {
            foreach (CharacterClass type in Enum.GetValues(typeof(CharacterClass)))
            {
                player.Loadout.SelectClass(type);
                yield return .05f;
                SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
                Require(renderer.sprite.name.StartsWith(type + "Run_") && renderer.color == Color.white && player.GetComponent<ArcherSpriteAnimator>().enabled, "Wrong live character: " + type);
                player.enabled = false;
                player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.right;
                HashSet<string> run = new HashSet<string>();
                float until = Time.time + 1.3f;
                while (Time.time < until) { run.Add(renderer.sprite.name); yield return null; }
                Require(run.Count == ArcherSpriteAnimator.RunFrames(type).Length, "Movement missed frames: " + type);
                player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero; player.enabled = true;
                yield return player.BackstepRemaining + .05f;
                Require(player.TryBackstep(), "Dash unavailable: " + type);
                HashSet<string> dash = new HashSet<string>();
                until = Time.time + .65f;
                while (Time.time < until) { if (renderer.sprite.name.Contains("Dash_")) dash.Add(renderer.sprite.name); yield return null; }
                Require(dash.Count == ArcherSpriteAnimator.DashFrames(type).Length, "Dash missed frames: " + type);
            }
            Dictionary<ClassArtEffect, ClassSheetEffect> spawned = new Dictionary<ClassArtEffect, ClassSheetEffect>();
            Dictionary<ClassArtEffect, HashSet<int>> seen = new Dictionary<ClassArtEffect, HashSet<int>>();
            foreach (ClassArtEffect id in Enum.GetValues(typeof(ClassArtEffect)))
            {
                spawned[id] = ClassSheetEffect.Play(id, new Vector2(100, 100), 1.6f, true);
                seen[id] = new HashSet<int>();
            }
            float end = Time.time + 1.5f;
            while (Time.time < end)
            {
                foreach (var pair in spawned) if (pair.Value != null) seen[pair.Key].Add(pair.Value.FrameIndex);
                yield return null;
            }
            foreach (var pair in seen) Require(pair.Value.Count == ClassSheetEffect.Frames(pair.Key).Length, "Effect missed frames: " + pair.Key);
            yield return .2f;
            foreach (var pair in spawned) Require(pair.Value == null, "Effect outlived duration: " + pair.Key);
            Debug.Log("CLASS_ART_VALIDATION_OK checks=" + checks);
        }
    }
}
