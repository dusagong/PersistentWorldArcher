using UnityEngine;

namespace PersistentWorld.Core
{
    public interface IInteractable
    {
        string Prompt { get; }

        void Interact(GameObject actor);
    }
}

