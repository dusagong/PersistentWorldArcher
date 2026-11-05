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
        public string Destination => destinationScene;

        public void Configure(string destination, string newPrompt)
        {
            destinationScene = destination;
            prompt = newPrompt;
        }

        public void Interact(GameObject actor)
        {
            if (actor.GetComponent<PersistentWorld.Networking.NetworkActorContext>() is PersistentWorld.Networking.NetworkActorContext context)
            { PersistentWorld.Networking.NetworkGameWorld.Instance?.UsePortal(context.ClientId, this); return; }
            if (destinationScene == "BossArena")
            { DialogueState.Show("보스 입장", "협동 모드에서 파티에 가입하거나 1인 파티를 먼저 만들어주세요."); return; }
            if (!string.IsNullOrWhiteSpace(destinationScene))
            {
                DialogueState.Clear();
                SceneManager.LoadScene(destinationScene);
            }
        }
    }
}

