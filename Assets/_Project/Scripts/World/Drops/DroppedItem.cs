using PersistentWorld.Combat;
using PersistentWorld.Items;
using PersistentWorld.Player;
using UnityEngine;

namespace PersistentWorld.World
{
    // 바닥에 떨어진 아이템. 죽은 자리에서 튀어나와 떨어지고, 플레이어가 닿으면 자동으로 인벤토리에 들어간다.
    // 가방이 가득 차면 바닥에 남는다. Lifetime이 지나면 깜빡이다 사라진다.
    public sealed class DroppedItem : MonoBehaviour
    {
        private const float PickupRadius = .65f;
        private const float PopTime = .4f;
        private const float PopHeight = .7f;
        private const float IconSize = .5f;
        private const float Lifetime = 60f;
        private const float BlinkBeforeDespawn = 5f;
        private const float FullBagMessageCooldown = 2f;

        private static float lastFullBagMessageAt = -10f;
        private static Sprite square;
        private static Sprite circle;

        private string itemId;
        private string displayName;
        private int quantity;
        private Color labelColor;
        private Transform iconRoot;
        private SpriteRenderer icon;
        private SpriteRenderer shadow;
        private MeshRenderer countRenderer;
        private Vector3 startPosition;
        private Vector3 landPosition;
        private float spawnedAt;
        private float bobOffset;

        public string ItemId => itemId;
        public int Quantity => quantity;

        public static DroppedItem Spawn(Vector2 position, string itemId, int quantity, Vector2 origin)
        {
            ItemCatalog.TryGet(itemId, out ItemDefinition definition);
            GameObject obj = new GameObject("Dropped " + itemId);
            obj.transform.position = origin;
            DroppedItem item = obj.AddComponent<DroppedItem>();
            item.itemId = itemId;
            item.quantity = quantity;
            item.displayName = definition != null ? definition.Name : itemId;
            item.labelColor = LabelColor(itemId, definition);
            item.startPosition = origin;
            item.landPosition = position;
            item.spawnedAt = Time.time;
            item.bobOffset = Random.value * 6f;

            GameObject shadowObject = new GameObject("Shadow");
            shadowObject.transform.SetParent(obj.transform, false);
            shadowObject.transform.localScale = new Vector3(.42f, .16f, 1f);
            item.shadow = shadowObject.AddComponent<SpriteRenderer>();
            item.shadow.sprite = Circle;
            item.shadow.color = new Color(0f, 0f, 0f, .35f);

            GameObject iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(obj.transform, false);
            item.iconRoot = iconObject.transform;
            item.icon = iconObject.AddComponent<SpriteRenderer>();
            Sprite sprite = definition != null ? Resources.Load<Sprite>(definition.IconPath) : null;
            if (sprite != null)
            {
                item.icon.sprite = sprite;
                Vector2 size = sprite.bounds.size;
                iconObject.transform.localScale = Vector3.one * (IconSize / Mathf.Max(.01f, Mathf.Max(size.x, size.y)));
            }
            else
            {
                item.icon.sprite = Square;
                item.icon.color = item.labelColor;
                iconObject.transform.localScale = Vector3.one * IconSize * .7f;
            }

            if (quantity > 1)
            {
                GameObject countObject = new GameObject("Count");
                countObject.transform.SetParent(obj.transform, false);
                countObject.transform.localPosition = new Vector3(.22f, -.05f, 0f);
                TextMesh text = countObject.AddComponent<TextMesh>();
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.font = font;
                text.text = "x" + quantity;
                text.fontSize = 48;
                text.characterSize = .045f;
                text.fontStyle = FontStyle.Bold;
                text.anchor = TextAnchor.MiddleLeft;
                text.color = Color.white;
                item.countRenderer = countObject.GetComponent<MeshRenderer>();
                item.countRenderer.sharedMaterial = font.material;
            }

            item.Update();
            return item;
        }

        private void Update()
        {
            float age = Time.time - spawnedAt;
            float remaining = Lifetime - age;
            if (remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // 죽은 자리에서 포물선으로 튀어나와 떨어진 뒤, 제자리에서 살짝 위아래로 떠 있다
            float height;
            if (age < PopTime)
            {
                float k = age / PopTime;
                transform.position = Vector3.Lerp(startPosition, landPosition, k);
                height = 4f * PopHeight * k * (1f - k);
            }
            else
            {
                transform.position = landPosition;
                height = .1f + .06f * Mathf.Sin((Time.time + bobOffset) * 3f);
            }
            iconRoot.localPosition = new Vector3(0f, height, 0f);

            int order = 1000 - Mathf.RoundToInt(transform.position.y * 100f);
            shadow.sortingOrder = order - 1;
            icon.sortingOrder = order;
            if (countRenderer != null) countRenderer.sortingOrder = order + 1;

            bool visible = remaining > BlinkBeforeDespawn || Mathf.FloorToInt(Time.time * 8f) % 2 == 0;
            icon.enabled = visible;
            if (countRenderer != null) countRenderer.enabled = visible;

            if (age >= PopTime) TryPickup();
        }

        private void TryPickup()
        {
            foreach (ArcherController player in FindObjectsByType<ArcherController>(FindObjectsSortMode.None))
            {
                if (!player.CompareTag("Player")) continue;
                if (player.GetComponent<Health>() is Health h && h.IsDead) continue;
                if (Vector2.Distance(player.transform.position, transform.position) > PickupRadius) continue;

                ItemSession session = ItemSession.Instance;
                if (session == null || session.Profile == null) return;
                Vector3 labelPosition = transform.position + Vector3.up * .5f;
                if (session.Profile.Inventory.AddItem(itemId, quantity))
                {
                    DamageNumber.Spawn(labelPosition, $"+{displayName} x{quantity}", labelColor);
                    Destroy(gameObject);
                }
                else if (Time.time - lastFullBagMessageAt >= FullBagMessageCooldown)
                {
                    lastFullBagMessageAt = Time.time;
                    DamageNumber.Spawn(labelPosition, "가방이 가득 찼습니다", new Color(.8f, .8f, .8f));
                }
                return;
            }
        }

        private static Color LabelColor(string id, ItemDefinition definition)
        {
            if (id == MonsterDropTable.HealthPotion) return new Color(1f, .45f, .45f);
            if (id == MonsterDropTable.ManaPotion) return new Color(.45f, .7f, 1f);
            if (definition != null && definition.Category == ItemCategory.Material) return new Color(1f, .85f, .35f);
            return Color.white;
        }

        private static Sprite Square
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

        private static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int size = 32;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                float r = size * .5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r)) / r;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 3f)));
                }
                texture.Apply();
                circle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
                return circle;
            }
        }
    }
}
