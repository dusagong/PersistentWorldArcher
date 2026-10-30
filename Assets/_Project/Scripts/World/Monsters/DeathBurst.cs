using UnityEngine;

namespace PersistentWorld.World
{
    // 몬스터가 죽을 때 커지며 사라지는 잔상과 사방으로 튀는 조각.
    public sealed class DeathBurst : MonoBehaviour
    {
        private const float Lifetime = .35f;
        private const int ShardCount = 8;

        private SpriteRenderer ghost;
        private Vector3 baseScale;
        private Color color;
        private SpriteRenderer[] shards;
        private Vector3[] velocities;
        private float age;

        public static void Spawn(Vector3 position, Color color, Vector3 size, int sortingOrder)
        {
            GameObject root = new GameObject("Death Burst");
            root.transform.position = position;
            DeathBurst burst = root.AddComponent<DeathBurst>();
            burst.color = color;
            burst.baseScale = new Vector3(size.x, size.y, 1f);

            GameObject ghostObject = new GameObject("Ghost");
            ghostObject.transform.SetParent(root.transform, false);
            ghostObject.transform.localScale = burst.baseScale;
            burst.ghost = ghostObject.AddComponent<SpriteRenderer>();
            burst.ghost.sprite = CombatFeedback.Square;
            burst.ghost.color = Color.white;
            burst.ghost.sortingOrder = sortingOrder;

            burst.shards = new SpriteRenderer[ShardCount];
            burst.velocities = new Vector3[ShardCount];
            float shardSize = Mathf.Max(size.x, size.y) * .22f;
            for (int i = 0; i < ShardCount; i++)
            {
                GameObject shard = new GameObject("Shard");
                shard.transform.SetParent(root.transform, false);
                shard.transform.localScale = Vector3.one * shardSize;
                shard.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
                SpriteRenderer renderer = shard.AddComponent<SpriteRenderer>();
                renderer.sprite = CombatFeedback.Square;
                renderer.color = color;
                renderer.sortingOrder = sortingOrder + 1;
                burst.shards[i] = renderer;
                float angle = (360f / ShardCount * i + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                burst.velocities[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(2.5f, 4f);
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Lifetime);
            ghost.transform.localScale = baseScale * Mathf.Lerp(1f, 1.6f, t);
            ghost.color = new Color(1f, 1f, 1f, 1f - t);
            for (int i = 0; i < shards.Length; i++)
            {
                shards[i].transform.localPosition += velocities[i] * Time.deltaTime;
                velocities[i] *= .9f;
                shards[i].color = new Color(color.r, color.g, color.b, 1f - t);
            }

            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
