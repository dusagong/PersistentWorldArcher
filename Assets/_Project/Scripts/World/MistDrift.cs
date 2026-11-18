using UnityEngine;

namespace PersistentWorld.World
{
    // 안개 한 뭉치가 제자리 근처를 천천히 떠다니며 옅어졌다 짙어졌다 한다. 보이기만 하는 장식이라 판정이 없다.
    // 맵 장식은 온라인에서도 각 클라이언트가 직접 그리므로 서버 그림 수에 들어가지 않는다.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MistDrift : MonoBehaviour
    {
        [SerializeField, Tooltip("좌우로 떠다니는 거리")] private float drift = .8f;
        [SerializeField, Tooltip("한 번 왕복하는 시간(초)")] private float period = 18f;
        [SerializeField, Range(0f, 1f)] private float minAlpha = .08f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = .2f;

        private SpriteRenderer visual;
        private Vector3 home;
        private float phase;

        private void Awake()
        {
            visual = GetComponent<SpriteRenderer>();
            home = transform.localPosition;
            // 위치로 박자를 정해 뭉치마다 다르게, 그러나 매번 같게 움직인다
            phase = Mathf.Repeat(home.x * .37f + home.y * .61f, Mathf.PI * 2f);
        }

        private void Update()
        {
            float t = Time.time * Mathf.PI * 2f / Mathf.Max(1f, period) + phase;
            transform.localPosition = home + new Vector3(Mathf.Sin(t) * drift, Mathf.Sin(t * .7f) * drift * .25f, 0f);
            Color color = visual.color;
            color.a = Mathf.Lerp(minAlpha, maxAlpha, .5f + .5f * Mathf.Sin(t * 1.3f + 1f));
            visual.color = color;
        }
    }
}
