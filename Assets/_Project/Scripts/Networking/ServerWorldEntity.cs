using UnityEngine;
namespace PersistentWorld.Networking
{
    public sealed class ServerWorldEntity : MonoBehaviour
    {
        public GameArea area;
        public MonoBehaviour[] serverBehaviours;
        public void SetSimulation(bool active)
        {
            foreach (MonoBehaviour behaviour in serverBehaviours) if (behaviour != null) behaviour.enabled = active;
        }
    }
}
