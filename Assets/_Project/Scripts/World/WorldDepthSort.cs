using UnityEngine;

namespace PersistentWorld.World
{
    // Sort at the feet so actors can walk in front of and behind scenery.
    public sealed class WorldDepthSort : MonoBehaviour
    {
        private SpriteRenderer[] renderers;

        private void Awake() => renderers = GetComponentsInChildren<SpriteRenderer>();

        private void LateUpdate()
        {
            int order = 1000 - Mathf.RoundToInt(transform.position.y * 100f);
            foreach (SpriteRenderer renderer in renderers)
                renderer.sortingOrder = order;
        }
    }
}
