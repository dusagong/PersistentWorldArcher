using PersistentWorld.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersistentWorld.World
{
    // 피격 피드백: 흰색 번쩍임, 데미지 숫자, 머리 위 HP 바, 사망 시 터지는 효과.
    // 씬에 배치된 모든 적(Enemy 진영)과 플레이어에는 실행 시 자동으로 붙는다.
    // Health.Awake(현재 HP 초기화)보다 늦게 준비되도록 실행 순서를 뒤로 둔다
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Health))]
    public sealed class CombatFeedback : MonoBehaviour
    {
        [SerializeField] private bool showFlash = true;
        [SerializeField] private bool showHealthBar = true;
        [SerializeField] private bool showDeathBurst = true;
        [SerializeField] private Color damageNumberColor = Color.white;
        [SerializeField] private float flashTime = .16f;
        [SerializeField] private float healthBarWidth = .9f;
        [SerializeField] private float healthBarHeight = .1f;
        [SerializeField] private float healthBarGap = .2f;

        private static Sprite square;
        private static Material silhouetteMaterial;
        private Health health;
        private SpriteRenderer body;
        private SpriteRenderer flash;
        private Transform barRoot;
        private Transform barFill;
        private SpriteRenderer barBack;
        private SpriteRenderer barFront;
        private int lastHealth = -1;
        private float flashEndsAt;
        private bool alwaysShowHealthBar;
        private bool showHealthNumbers;
        private TextMesh healthLabel;

        public void Configure(bool flashOnHit, bool healthBar, bool deathBurst, Color numberColor)
        {
            showFlash = flashOnHit;
            showHealthBar = healthBar;
            showDeathBurst = deathBurst;
            damageNumberColor = numberColor;
        }

        // 부숴야 하는 목표물처럼 맞기 전에도 HP 바를 계속 보여 준다. numbers면 바 위에 "현재/최대"를 적는다.
        public void ShowHealthAlways(float width, float height, bool numbers)
        {
            showHealthBar = true;
            alwaysShowHealthBar = true;
            healthBarWidth = width;
            healthBarHeight = height;
            showHealthNumbers = numbers;
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            body = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            // Health가 아직 초기화 전(0)이면 기억하지 않고 Start나 첫 변화 때 정한다
            lastHealth = health.Current > 0 ? health.Current : -1;
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
            if (lastHealth < 0 && health.Current > 0) lastHealth = health.Current;
            if (showFlash && body != null) CreateFlash();
            if (showHealthBar) CreateHealthBar();
        }

        private void OnDestroy()
        {
            if (barRoot != null) Destroy(barRoot.gameObject);
        }

        private void OnHealthChanged(int current, int maximum)
        {
            if (lastHealth < 0) lastHealth = maximum; // 첫 변화 전 HP는 항상 최대치에서 시작
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
                DeathBurst.Spawn(body, health.DestroysOnDeath);
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
                bool visible = !health.IsDead && (alwaysShowHealthBar || health.Current < health.Maximum);
                barRoot.gameObject.SetActive(visible);
                if (!visible) return;
                barRoot.position = TopPosition() + Vector3.up * healthBarGap;
                float ratio = Mathf.Clamp01((float)health.Current / health.Maximum);
                barFill.localScale = new Vector3(healthBarWidth * ratio, healthBarHeight, 1f);
                barFill.localPosition = new Vector3(-healthBarWidth * (1f - ratio) * .5f, 0f, 0f);
                int order = body != null ? body.sortingOrder + 5 : 3000;
                barBack.sortingOrder = order;
                barFront.sortingOrder = order + 1;
                if (healthLabel != null)
                {
                    healthLabel.text = health.Current + " / " + health.Maximum;
                    healthLabel.GetComponent<MeshRenderer>().sortingOrder = order + 2;
                }
            }
        }

        // 그림 윗변. 피벗이 발밑인 그림(기둥 등)도 머리 위에 오도록 실제 경계를 쓴다.
        private Vector3 TopPosition()
        {
            if (body == null || body.sprite == null) return transform.position + Vector3.up * .4f;
            return new Vector3(transform.position.x, body.bounds.max.y, transform.position.z);
        }

        private void CreateFlash()
        {
            GameObject flashObject = new GameObject("Hit Flash");
            flashObject.transform.SetParent(transform, false);
            flash = flashObject.AddComponent<SpriteRenderer>();
            flash.sprite = body.sprite;
            flash.color = new Color(1f, 0f, 0f, .5f);
            // 그림 스프라이트도 흰 실루엣으로 번쩍이게 텍스처 알파만 쓰는 셰이더를 쓴다 (없으면 기본 셰이더)
            if (SilhouetteMaterial != null) flash.sharedMaterial = SilhouetteMaterial;
            flash.enabled = false;
        }

        private void CreateHealthBar()
        {
            barRoot = new GameObject(name + " HP Bar").transform;
            barBack = CreateBarPart("Back", new Color(0f, 0f, 0f, .7f), new Vector3(healthBarWidth + .04f, healthBarHeight + .04f, 1f));
            barFront = CreateBarPart("Fill", new Color(.9f, .2f, .2f), new Vector3(healthBarWidth, healthBarHeight, 1f));
            barFill = barFront.transform;
            if (showHealthNumbers)
            {
                var labelObject = new GameObject("HP Label");
                labelObject.transform.SetParent(barRoot, false);
                labelObject.transform.localPosition = Vector3.up * (healthBarHeight * .5f + .04f);
                healthLabel = labelObject.AddComponent<TextMesh>();
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                healthLabel.font = font;
                healthLabel.fontSize = 48;
                healthLabel.characterSize = .05f;
                healthLabel.fontStyle = FontStyle.Bold;
                healthLabel.anchor = TextAnchor.LowerCenter;
                healthLabel.alignment = TextAlignment.Center;
                healthLabel.color = Color.white;
                labelObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
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

        internal static Material SilhouetteMaterial
        {
            get
            {
                if (silhouetteMaterial == null && Shader.Find("GUI/Text Shader") is Shader shader)
                    silhouetteMaterial = new Material(shader);
                return silhouetteMaterial;
            }
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
