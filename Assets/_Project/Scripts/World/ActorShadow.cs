using PersistentWorld.Combat;
using PersistentWorld.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersistentWorld.World
{
    // 움직이는 캐릭터의 발밑 그림자. WorldDepthSort가 자식까지 같은 순서로 묶으므로 자식이 아닌 별도 오브젝트로 따라다닌다.
    // 배경 그림자와 같은 바닥 높이에 그려지고, 대상이 사라지면 함께 사라진다.
    public sealed class ActorShadow : MonoBehaviour
    {
        private const int GroundOrder = -8500;
        private static readonly Vector3 LightOffset = new Vector3(.06f, -.02f, 0f); // 배경 그림자처럼 살짝 오른쪽으로
        private static Sprite blob;

        private Transform target;
        private Health health;
        private SlimeJumpEnemyController jumper;
        private SpriteRenderer visual;
        private float footOffset;
        private bool hideOnDeath;

        // footOffset: 대상 위치에서 발까지의 거리 (피벗이 발밑이면 0)
        public static ActorShadow Attach(Transform target, float width, float footOffset, bool hideOnDeath = true)
        {
            var obj = new GameObject(target.name + " Shadow");
            var shadow = obj.AddComponent<ActorShadow>();
            shadow.target = target;
            shadow.health = target.GetComponent<Health>();
            shadow.jumper = target.GetComponent<SlimeJumpEnemyController>();
            shadow.footOffset = footOffset;
            shadow.hideOnDeath = hideOnDeath;
            shadow.visual = obj.AddComponent<SpriteRenderer>();
            shadow.visual.sprite = Blob;
            shadow.visual.color = new Color(0f, 0f, 0f, .32f);
            shadow.visual.sortingOrder = GroundOrder;
            obj.transform.localScale = new Vector3(width, width * .42f, 1f);
            shadow.LateUpdate();
            return shadow;
        }

        private void LateUpdate()
        {
            if (target == null) { Destroy(gameObject); return; }
            bool dead = hideOnDeath && health != null && health.IsDead;
            // 점프 몬스터는 공중에서 자기 그림자를 따로 그린다
            visual.enabled = target.gameObject.activeInHierarchy && !dead && !(jumper != null && jumper.IsAirborne);
            transform.position = target.position + Vector3.down * footOffset + LightOffset;
        }

        // 플레이어는 씬마다 놓여 있어 씬이 열릴 때 붙인다. 몬스터·보스는 각자 애니메이터가 붙인다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachToPlayers();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToPlayers();

        private static void AttachToPlayers()
        {
            ActorShadow[] existing = FindObjectsByType<ActorShadow>(FindObjectsSortMode.None);
            foreach (ArcherSpriteAnimator player in FindObjectsByType<ArcherSpriteAnimator>(FindObjectsSortMode.None))
            {
                bool attached = false;
                foreach (ActorShadow shadow in existing) attached |= shadow.target == player.transform;
                if (!attached) Attach(player.transform, .62f, 0f);
            }
        }

        // 가장자리가 부드러운 원 (지름 1유닛)
        private static Sprite Blob
        {
            get
            {
                if (blob != null) return blob;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                float r = size * .5f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r)) / r;
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 3f)));
                    }
                texture.Apply();
                blob = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
                return blob;
            }
        }
    }
}
