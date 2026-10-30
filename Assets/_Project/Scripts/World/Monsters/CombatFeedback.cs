using PersistentWorld.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersistentWorld.World
{
    // 피격 피드백: 흰색 번쩍임, 데미지 숫자, 머리 위 HP 바, 사망 시 터지는 효과.
    // 씬에 배치된 모든 적(Enemy 진영)과 플레이어에는 실행 시 자동으로 붙는다.
    [RequireComponent(typeof(Health))]
    public sealed class CombatFeedback : MonoBehaviour
    {
        [SerializeField] private bool showFlash = true;
        [SerializeField] private bool showHealthBar = true;
        [SerializeField] private bool showDeathBurst = true;
        [SerializeField] private Color damageNumberColor = Color.white;
        [SerializeField] private float flashTime = .08f;
        [SerializeField] private float healthBarWidth = .9f;
        [SerializeField] private float healthBarHeight = .1f;
        [SerializeField] private float healthBarGap = .2f;

        private static Sprite square;
        private Health health;
        private SpriteRenderer body;
        private SpriteRenderer flash;
        private Transform barRoot;
        private Transform barFill;
        private SpriteRenderer barBack;
        private SpriteRenderer barFront;
        private int lastHealth;
        private float flashEndsAt;

        public void Configure(bool flashOnHit, bool healthBar, bool deathBurst, Color numberColor)
        {
            showFlash = flashOnHit;
            showHealthBar = healthBar;
            showDeathBurst = deathBurst;
            damageNumberColor = numberColor;
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            body = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            lastHealth = health.Current;
            health.Changed += OnHealthChanged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Changed -= OnHealthChanged;
            health.Died -= OnDied;
        }

        private void Start()
        {
            if (showFlash && body != null) CreateFlash();
            if (showHealthBar) CreateHealthBar();
        }

        private void OnDestroy()
        {
            if (barRoot != null) Destroy(barRoot.gameObject);
        }

        private void OnHealthChanged(int current, int maximum)
        {
            int delta = lastHealth - current;
            lastHealth = current;
            if (delta == 0) return;

            Vector3 top = TopPosition();
            if (delta > 0)
            {
                DamageNumber.Spawn(top, delta.ToString(), damageNumberColor);
                flashEndsAt = Time.time + flashTime;
            }
            else
            {
                DamageNumber.Spawn(top, "+" + (-delta), new Color(.4f, 1f, .45f));
            }
        }

        private void OnDied(Health _)
        {
            if (barRoot != null) barRoot.gameObject.SetActive(false);
            if (flash != null) flash.enabled = false;
            if (showDeathBurst && body != null)
                DeathBurst.Spawn(transform.position, body.color, body.bounds.size, body.sortingOrder + 2);
        }

        private void LateUpdate()
        {
            if (flash != null)
            {
                flash.sprite = body.sprite;
                flash.flipX = body.flipX;
                flash.sortingOrder = body.sortingOrder + 1;
                flash.enabled = Time.time < flashEndsAt && !health.IsDead;
            }

            if (barRoot != null)
            {
                bool visible = !health.IsDead && health.Current < health.Maximum;
                barRoot.gameObject.SetActive(visible);
                if (!visible) return;
                barRoot.position = TopPosition() + Vector3.up * healthBarGap;
                float ratio = Mathf.Clamp01((float)health.Current / health.Maximum);
                barFill.localScale = new Vector3(healthBarWidth * ratio, healthBarHeight, 1f);
                barFill.localPosition = new Vector3(-healthBarWidth * (1f - ratio) * .5f, 0f, 0f);
                int order = body != null ? body.sortingOrder + 5 : 3000;
                barBack.sortingOrder = order;
                barFront.sortingOrder = order + 1;
            }
        }

        private Vector3 TopPosition()
        {
            float half = body != null ? body.bounds.extents.y : .4f;
            return transform.position + Vector3.up * half;
        }

        private void CreateFlash()
        {
            GameObject flashObject = new GameObject("Hit Flash");
            flashObject.transform.SetParent(transform, false);
            flash = flashObject.AddComponent<SpriteRenderer>();
            flash.sprite = body.sprite;
            flash.color = new Color(1f, 1f, 1f, .9f);
            flash.enabled = false;
        }

        private void CreateHealthBar()
        {
            barRoot = new GameObject(name + " HP Bar").transform;
            barBack = CreateBarPart("Back", new Color(0f, 0f, 0f, .7f), new Vector3(healthBarWidth + .04f, healthBarHeight + .04f, 1f));
            barFront = CreateBarPart("Fill", new Color(.9f, .2f, .2f), new Vector3(healthBarWidth, healthBarHeight, 1f));
            barFill = barFront.transform;
            barRoot.gameObject.SetActive(false);
        }

        private SpriteRenderer CreateBarPart(string partName, Color color, Vector3 scale)
        {
            GameObject part = new GameObject(partName);
            part.transform.SetParent(barRoot, false);
            part.transform.localScale = scale;
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = Square;
            renderer.color = color;
            return renderer;
        }

        internal static Sprite Square
        {
            get
            {
                if (square != null) return square;
                Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                square = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(.5f, .5f), 1f);
                return square;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachToScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToScene();

        private static void AttachToScene()
        {
            foreach (Health target in FindObjectsByType<Health>(FindObjectsSortMode.None)) Attach(target);
        }

        public static void Attach(Health target)
        {
            if (target == null || target.GetComponent<CombatFeedback>() != null) return;
            if (target.Faction == Faction.Enemy)
                target.gameObject.AddComponent<CombatFeedback>();
            else if (target.CompareTag("Player"))
                target.gameObject.AddComponent<CombatFeedback>().Configure(false, false, false, new Color(1f, .35f, .35f));
        }
    }
}
