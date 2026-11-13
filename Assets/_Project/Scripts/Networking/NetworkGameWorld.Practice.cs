using System.Collections.Generic;
using System.Linq;
using PersistentWorld.Combat;
using PersistentWorld.Player;
using PersistentWorld.World;
using UnityEngine;

namespace PersistentWorld.Networking
{
    public sealed partial class NetworkGameWorld
    {
        private readonly Dictionary<ulong, TrainingCompanion[]> companions = new Dictionary<ulong, TrainingCompanion[]>();
        private readonly Dictionary<ulong, GameObject> practiceDummies = new Dictionary<ulong, GameObject>();
        private readonly Dictionary<ulong, int> practiceStations = new Dictionary<ulong, int>();
        private Vector2 PracticeSpawn(ulong id)
        {
            if (!practiceStations.TryGetValue(id, out int station))
            {
                // Client IDs grow after reconnects; modulo IDs can assign two people the same lane.
                station = 0;
                while (practiceStations.ContainsValue(station)) station++;
                practiceStations.Add(id, station);
            }
            return mapSpawns[(int)GameArea.Tutorial] + Vector2.right * (station * 12 - 18);
        }
        internal void RecordPractice(ulong id, int flag)
        {
            if (!manager.IsServer || !Parties.Members.TryGetValue(id, out PartyMember member) || member.Area != GameArea.Tutorial) return;
            tutorialProgress.TryGetValue(id, out int progress); tutorialProgress[id] = progress | flag;
        }
        private void ResetPractice(ulong id)
        {
            ClearPractice(id, false);
            ArcherController actor = Actors[id];
            foreach (HealingZone zone in FindObjectsByType<HealingZone>(FindObjectsSortMode.None))
                if (zone.Caster == actor.Health) Destroy(zone.gameObject);
            actor.SetExternalMovement(Vector2.zero, Vector2.up);
            actor.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            actor.transform.position = Origin(GameArea.Tutorial) + PracticeSpawn(id);
            actor.Skills.ResetPractice(); actor.Mana.Restore(100); actor.Health.Heal(actor.Health.Maximum);
            tutorialProgress[id] = 0;
            Sprite[] frames = ArcherSpriteAnimator.RunFrames(CharacterClass.Healer);
            var targets = new TrainingCompanion[3];
            for (int i = 0; i < targets.Length; i++)
            {
                var root = new GameObject("Practice Ally " + i);
                root.transform.position = actor.transform.position + new Vector3((i - 1) * 2.2f, 1.1f, 0);
                root.AddComponent<SpriteRenderer>();
                var companion = root.AddComponent<TrainingCompanion>();
                companion.Configure(id, (TrainingPurpose)i, frames != null ? frames[0] : null);
                targets[i] = companion;
            }
            companions[id] = targets;
            TrainingDummy template = maps[(int)GameArea.Tutorial].GetComponentInChildren<TrainingDummy>(true);
            var dummy = new GameObject("Personal Practice Target"); dummy.transform.position = actor.transform.position + Vector3.up * 3;
            SpriteRenderer renderer = dummy.AddComponent<SpriteRenderer>(); renderer.sprite = template.GetComponent<SpriteRenderer>().sprite;
            dummy.transform.localScale = template.transform.localScale; renderer.sortingOrder = 1000 - Mathf.RoundToInt(dummy.transform.position.y * 100);
            dummy.AddComponent<CircleCollider2D>().radius = .35f / dummy.transform.localScale.x;
            Health health = dummy.AddComponent<Health>(); health.Configure(Faction.Enemy, 500, false);
            dummy.AddComponent<CombatStatus>(); dummy.AddComponent<TrainingDummy>();
            practiceDummies[id] = dummy;
            Physics2D.SyncTransforms();
        }
        private void ClearPractice(ulong id, bool releaseStation = true)
        {
            if (releaseStation) practiceStations.Remove(id);
            if (companions.Remove(id, out TrainingCompanion[] targets))
                foreach (TrainingCompanion companion in targets) if (companion != null)
                { companion.enabled = false; Destroy(companion.gameObject); }
            if (practiceDummies.Remove(id, out GameObject dummy) && dummy != null) Destroy(dummy);
        }
        private PracticeFrame[] PracticeTargets(ulong id) => companions.TryGetValue(id, out TrainingCompanion[] targets)
            ? targets.Where(t => t != null).Select(t => new PracticeFrame
            {
                kind = (int)t.Purpose, complete = t.Completed, hp = t.GetComponent<Health>().Current,
                maximum = t.GetComponent<Health>().Maximum, visual = t.GetComponent<SpriteRenderer>().GetInstanceID(), position = t.transform.position
            }).ToArray() : System.Array.Empty<PracticeFrame>();
    }
}
