using UnityEngine;

namespace PersistentWorld.World
{
    // 분수처럼 씬에 놓인 장식의 프레임 그림을 계속 돌린다.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class LoopingSprite : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float secondsPerFrame = .16f;

        private SpriteRenderer visual;

        private void Awake() => visual = GetComponent<SpriteRenderer>();

        private void Update()
        {
            if (frames == null || frames.Length == 0) return;
            visual.sprite = frames[Mathf.FloorToInt(Time.time / Mathf.Max(.01f, secondsPerFrame)) % frames.Length];
        }
    }
}
