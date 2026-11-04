using UnityEngine;

namespace PersistentWorld.World
{
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float smoothTime = 0.12f;

        private static float shakeMagnitude, shakeStartedAt, shakeDuration;

        private Vector3 velocity;
        private Vector3 basePosition;
        private bool hasBase;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        // 화면을 잠깐 흔든다. 흔들림은 따라가기 위치에 더해지기만 해서 따라가기 계산에는 섞이지 않는다.
        public static void Shake(float magnitude, float duration)
        {
            shakeMagnitude = magnitude;
            shakeStartedAt = Time.time;
            shakeDuration = Mathf.Max(.01f, duration);
        }

        private void LateUpdate()
        {
            if (!hasBase)
            {
                basePosition = transform.position;
                hasBase = true;
            }

            if (target != null)
            {
                Vector3 desired = new Vector3(target.position.x, target.position.y, basePosition.z);
                basePosition = Vector3.SmoothDamp(basePosition, desired, ref velocity, smoothTime);
            }

            float t = (Time.time - shakeStartedAt) / shakeDuration;
            Vector2 shake = t < 1f ? Random.insideUnitCircle * shakeMagnitude * (1f - t) : Vector2.zero;
            transform.position = basePosition + (Vector3)shake;
        }
    }
}
