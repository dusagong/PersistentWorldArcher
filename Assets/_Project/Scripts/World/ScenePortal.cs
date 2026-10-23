using PersistentWorld.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersistentWorld.World
{
    public sealed class ScenePortal : MonoBehaviour, IInteractable
    {
        [SerializeField] private string destinationScene;
        [SerializeField] private string prompt = "Travel";

        public string Prompt => prompt;

        public void Configure(string destination, string newPrompt)
        {
            destinationScene = destination;
            prompt = newPrompt;
        }

        public void Interact(GameObject actor)
        {
            if (!string.IsNullOrWhiteSpace(destinationScene))
            {
                DialogueState.Clear();
                SceneManager.LoadScene(destinationScene);
            }
        }
    }
}

