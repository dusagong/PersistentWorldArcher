using UnityEngine;

namespace PersistentWorld.World
{
    // 투사체처럼 잠깐 쓰는 오브젝트에 프레임 그림을 반복 재생한다.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFrameLoop : MonoBehaviour
    {
        private Sprite[] frames;
        private float frameDuration = .08f;
        private float startedAt;
        private SpriteRenderer visual;

        public void Play(Sprite[] newFrames, float secondsPerFrame = .08f)
        {
            frames = newFrames;
            frameDuration = Mathf.Max(.01f, secondsPerFrame);
            startedAt = Time.time;
            visual = GetComponent<SpriteRenderer>();
            Update();
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0) return;
            visual.sprite = frames[Mathf.FloorToInt((Time.time - startedAt) / frameDuration) % frames.Length];
        }
    }
}
