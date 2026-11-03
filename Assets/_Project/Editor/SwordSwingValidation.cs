using System;
using System.Collections;
using PersistentWorld.Combat;
using PersistentWorld.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PersistentWorld.Editor
{
    [InitializeOnLoad]
    public static class SwordSwingValidation
    {
        private const string Key = "PersistentWorld.SwordSwingValidation";
        private static IEnumerator steps;
        private static float resumeAt;
        private static double deadline;
        private static int checks;
        static SwordSwingValidation()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.playModeStateChanged += Entered;
        }

        public static void RunBatch()
        {
            PrototypeValidation.ValidateAll();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Town.unity");
            SessionState.SetBool(Key, true);
            EditorApplication.playModeStateChanged += Entered;
            EditorApplication.EnterPlaymode();
        }

        private static void Entered(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= Entered;
            steps = Scenarios(); resumeAt = 0; checks = 0;
            deadline = EditorApplication.timeSinceStartup + 20;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Sword animation validation timed out.");
                if (Time.time < resumeAt) return;
                if (steps.MoveNext()) resumeAt = Time.time + (steps.Current is float seconds ? seconds : 0);
                else Finish(true);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(false); }
        }

        private static void Finish(bool passed)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false);
            if (passed) Debug.Log("SWORD_SWING_VALIDATION_OK checks=" + checks);
            else Debug.LogError("SWORD_SWING_VALIDATION_FAILED checks=" + checks);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void Require(bool result, string message)
        {
            if (!result) throw new InvalidOperationException(message);
            checks++;
        }

        private static Health Target(Vector2 position)
        {
            GameObject obj = new GameObject("Sword swing validation target");
            obj.transform.position = position;
            Health health = obj.AddComponent<Health>(); health.Configure(Faction.Enemy, 100, false);
            obj.AddComponent<CircleCollider2D>().radius = .1f;
            return health;
        }

        private static IEnumerator Scenarios()
        {
            yield return .1f;
            ArcherController player = UnityEngine.Object.FindFirstObjectByType<ArcherController>();
            player.AcceptLocalInput = false;
            player.Loadout.SelectClass(CharacterClass.Warrior);
            player.SetAim(Vector2.right);
            Health front = Target(new Vector2(1, 0));
            front.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one * .1f;
            Health back = Target(new Vector2(-1, 0));
            Physics2D.SyncTransforms();
            Require(player.TryBasicAttack() && !player.TryBasicAttack(), "Basic attack cooldown changed.");
            Require(front.Current == 88 - player.EquipmentBonuses.Attack && back.Current == 100, "120-degree damage or multi-collider damage changed.");
            SwordSwingEffect effect = UnityEngine.Object.FindFirstObjectByType<SwordSwingEffect>();
            Require(effect != null && Mathf.Approximately(effect.SwingAngle, 60), "Sword does not begin at the edge of the arc.");
            Sprite icon = Resources.Load<Sprite>("Items/Equipment/06_warrior");
            Sprite weapon = effect.GetComponent<SpriteRenderer>().sprite;
            Require(weapon.texture == icon.texture && weapon != icon && icon.pivot == icon.rect.size / 2, "Original image/icon was modified or not reused.");
            Require(weapon.pivot == new Vector2(16, 64), "Sword is not rotating around its handle.");
            player.SetAim(Vector2.left);
            float minimum = 60;
            float previous = 60;
            bool correct = true;
            while (effect != null)
            {
                minimum = Mathf.Min(minimum, effect.SwingAngle);
                correct &= effect.SwingAngle <= previous + .01f && effect.SwingAngle >= -60 && effect.SwingAngle <= 60;
                correct &= Quaternion.Angle(effect.transform.rotation, Quaternion.Euler(0, 0, effect.SwingAngle - 45)) < .1f;
                previous = effect.SwingAngle;
                yield return null;
            }
            Require(correct && minimum < -30, "Sword did not sweep continuously inside the locked 120-degree aim arc.");
            Require(front.Current == 88 - player.EquipmentBonuses.Attack, "Animation caused extra damage.");
            Require(!player.TryBasicAttack(), "Animation completion incorrectly reset attack cooldown.");
            yield return .35f;
            Require(player.TryBasicAttack(), "Next sword swing failed.");
            player.SetMenuOpen(true);
            yield return null;
            Require(UnityEngine.Object.FindFirstObjectByType<SwordSwingEffect>() == null, "Opening menu did not cancel sword visual.");
            UnityEngine.Object.Destroy(front.gameObject); UnityEngine.Object.Destroy(back.gameObject);
        }
    }
}
