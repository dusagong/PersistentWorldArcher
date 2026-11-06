using UnityEngine;

namespace PersistentWorld.World
{
    // 룬처럼 빛나는 그림의 밝기를 천천히 오르내리게 한다. 오브젝트마다 시작 박자가 달라 함께 깜빡이지 않는다.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GlowPulse : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float minAlpha = .35f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;
        [SerializeField, Tooltip("한 번 밝아졌다 어두워지는 시간(초)")] private float period = 2.6f;

        private SpriteRenderer visual;
        private float phase;

        private void Awake()
        {
            visual = GetComponent<SpriteRenderer>();
            phase = Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            float wave = .5f + .5f * Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(.1f, period) + phase);
            Color color = visual.color;
            color.a = Mathf.Lerp(minAlpha, maxAlpha, wave);
            visual.color = color;
        }
    }
}
