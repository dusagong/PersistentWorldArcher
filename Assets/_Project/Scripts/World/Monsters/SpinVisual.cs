using UnityEngine;

namespace PersistentWorld.World
{
    // 던진 물건처럼 제자리에서 계속 돈다. 이동 방향과는 무관하다.
    public sealed class SpinVisual : MonoBehaviour
    {
        public float DegreesPerSecond { get; set; } = 540f;

        private void Update() => transform.Rotate(0f, 0f, -DegreesPerSecond * Time.deltaTime);
    }
}
