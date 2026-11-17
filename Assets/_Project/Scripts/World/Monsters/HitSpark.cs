using UnityEngine;

namespace PersistentWorld.World
{
    // 맞은 자리에 터지는 작은 불꽃 (Resources/Effects/HitSpark.png, 32px 4프레임 가로 시트).
    // 그림 파일을 쓰므로 온라인 클라이언트에서도 같은 모양으로 보인다.
    public sealed class HitSpark : MonoBehaviour
    {
        private const float FrameTime = .045f;
        private static Sprite[] frames;

        private SpriteRenderer view;
        private float startedAt;

        public static void Spawn(Vector2 at, float size, int sortingLayerId, int sortingOrder)
        {
            Sprite[] sheet = Frames;
            if (sheet == null) return;
            var obj = new GameObject("Hit Spark");
            obj.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f)));
            obj.transform.localScale = Vector3.one * size;
            var spark = obj.AddComponent<HitSpark>();
            spark.view = obj.AddComponent<SpriteRenderer>();
            spark.view.sprite = sheet[0];
            spark.view.sortingLayerID = sortingLayerId;
            spark.view.sortingOrder = sortingOrder;
            spark.startedAt = Time.unscaledTime;
        }

        private void Update()
        {
            int frame = Mathf.FloorToInt((Time.unscaledTime - startedAt) / FrameTime);
            if (frame >= frames.Length) { Destroy(gameObject); return; }
            view.sprite = frames[frame];
        }

        private static Sprite[] Frames
        {
            get
            {
                if (frames != null) return frames;
                Texture2D texture = Resources.Load<Texture2D>("Effects/HitSpark");
                if (texture == null) return null;
                int size = texture.height, count = texture.width / size;
                frames = new Sprite[count];
                for (int i = 0; i < count; i++)
                {
                    frames[i] = Sprite.Create(texture, new Rect(i * size, 0, size, size), new Vector2(.5f, .5f), 32f, 0, SpriteMeshType.FullRect);
                    frames[i].name = "HitSpark_" + i;
                }
                return frames;
            }
        }
    }
}
