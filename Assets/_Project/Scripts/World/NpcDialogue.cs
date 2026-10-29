using PersistentWorld.Core;
using UnityEngine;

namespace PersistentWorld.World
{
    public sealed class NpcDialogue : MonoBehaviour, IInteractable
    {
        [SerializeField] private string npcName = "Village Guide";
        [SerializeField] private string[] lines =
        {
            "The field gate is to the east.",
            "Use Q/E for equipped skills, Space to dodge and ESC to change skills."
        };

        private int lineIndex;

        public string Prompt => "Talk to " + npcName;

        public void Configure(string newName, params string[] newLines)
        {
            npcName = newName;
            lines = newLines;
        }

        public void Interact(GameObject actor)
        {
            if (lines == null || lines.Length == 0)
            {
                return;
            }

            DialogueState.Show(npcName, lines[lineIndex]);
            lineIndex = (lineIndex + 1) % lines.Length;
        }
    }
}

