using PersistentWorld.Core;
using PersistentWorld.Items;
using PersistentWorld.UI;
using UnityEngine;

namespace PersistentWorld.World
{
    public sealed class BlacksmithNpc : MonoBehaviour, IInteractable
    {
        public string Prompt => "Blacksmith - Craft equipment";
        public void Interact(GameObject actor)
        {
            if (actor.GetComponent<PlayerEquipment>() == null || ItemSession.Instance == null) return;
            DialogueState.Clear();
            ItemSession.Instance.GetComponent<BlacksmithWindow>().Open(actor, transform);
        }
    }
}
