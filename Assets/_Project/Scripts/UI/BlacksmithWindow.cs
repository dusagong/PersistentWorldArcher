using PersistentWorld.Combat;
using PersistentWorld.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace PersistentWorld.UI
{
    public sealed class BlacksmithWindow : MonoBehaviour
    {
        private static BlacksmithWindow active;
        public static bool IsOpen => active != null && active.visible;
        private ItemSession session;
        private UIDocument document;
        private PanelSettings settings;
        private Font font;
        private VisualElement screen;
        private ScrollView recipes;
        private Label materials;
        private Label message;
        private GameObject actor;
        private Transform smith;
        private bool visible;
        private bool dirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active = null;

        private void Start()
        {
            session = GetComponent<ItemSession>();
            active = this;
            VisualTreeAsset tree = Resources.Load<VisualTreeAsset>("UI/BlacksmithScreen");
            if (tree == null) { Debug.LogError("Blacksmith UI asset is missing."); return; }
            PanelSettings template = Resources.Load<PanelSettings>("UI/BlacksmithPanel");
            if (template == null) { Debug.LogError("Blacksmith panel settings are missing."); return; }
            settings = Instantiate(template);
            GameObject ui = new GameObject("Blacksmith UI");
            ui.transform.SetParent(transform, false);
            ui.SetActive(false);
            document = ui.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.visualTreeAsset = tree;
            ui.SetActive(true);
            // The Windows target supplies this CJK font; no new art/font package is required.
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 16);
            VisualElement root = document.rootVisualElement;
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.right = 0;
            root.style.top = 0;
            root.style.bottom = 0;
            root.style.unityFont = font;
            screen = document.rootVisualElement.Q<VisualElement>("blacksmith-screen");
            recipes = screen.Q<ScrollView>("recipe-list");
            materials = screen.Q<Label>("materials");
            message = screen.Q<Label>("message");
            screen.Q<Button>("close-button").clicked += Close;
            session.Profile.Changed += MarkDirty;
            screen.style.display = DisplayStyle.None;
        }

        public void Open(GameObject newActor, Transform blacksmith)
        {
            if (screen == null) return;
            GetComponent<InventoryWindow>()?.Close();
            actor = newActor;
            smith = blacksmith;
            visible = true;
            screen.style.display = DisplayStyle.Flex;
            message.text = "장비를 제작한 뒤 장착할 수 있습니다.";
            Refresh();
        }

        public void Close()
        {
            if (visible) ItemWindows.ConsumeClose();
            visible = false;
            if (screen != null) screen.style.display = DisplayStyle.None;
            actor = null;
            smith = null;
        }

        private void MarkDirty() => dirty = true;

        private void Update()
        {
            if (!visible) return;
            Health health = actor != null ? actor.GetComponent<Health>() : null;
            if (actor == null || smith == null || health == null || health.IsDead ||
                Vector2.Distance(actor.transform.position, smith.position) > 2.5f || Input.GetKeyDown(KeyCode.Escape))
            { Close(); return; }
            if (dirty) Refresh();
        }

        private void Refresh()
        {
            dirty = false;
            PlayerInventoryProfile profile = session.Profile;
            Inventory inventory = profile.Inventory;
            materials.text = $"인벤토리 {inventory.UsedSlots}/{inventory.Capacity}칸  |  " +
                $"재료 1: {inventory.GetCount(ItemCatalog.MaterialOne)}  ·  재료 2: {inventory.GetCount(ItemCatalog.MaterialTwo)}";
            recipes.Clear();
            foreach (EquipmentRecipe recipe in EquipmentCrafting.All)
            {
                ItemCatalog.TryGet(recipe.ResultId, out ItemDefinition item);
                if (!item.CanEquip(profile.Class)) continue;
                var row = new VisualElement();
                row.AddToClassList("recipe-row");
                var icon = new Image { sprite = Resources.Load<Sprite>(item.IconPath), scaleMode = ScaleMode.ScaleToFit };
                icon.AddToClassList("item-icon");
                row.Add(icon);
                var description = new VisualElement();
                description.AddToClassList("recipe-description");
                bool equipped = profile.GetEquipped(item.Slot) == item.Id;
                var nameLabel = new Label(item.Name + (equipped ? "  [장착 중]" : ""));
                nameLabel.AddToClassList("item-name");
                description.Add(nameLabel);
                description.Add(new Label($"공격 +{item.Attack} · 방어 +{item.Defense} · 이동 +{item.MoveSpeed:0.0}"));
                description.Add(new Label($"재료 1 × {recipe.Materials[0].Quantity} + 재료 2 × {recipe.Materials[1].Quantity}" +
                    $"  |  보유 {inventory.GetCount(item.Id)}"));
                row.Add(description);
                var craft = new Button(() => Craft(item.Id)) { text = "제작" };
                craft.SetEnabled(EquipmentCrafting.CanCraft(inventory, item.Id, out _));
                row.Add(craft);
                var equip = new Button(() => ToggleEquipment(item)) { text = equipped ? "해제" : "장착" };
                equip.SetEnabled(equipped || inventory.HasItem(item.Id, 1));
                row.Add(equip);
                recipes.Add(row);
            }
            if (!string.IsNullOrEmpty(session.SaveError)) message.text = "현재 진행 상태를 저장할 수 없습니다. 저장 위치를 확인해주세요.";
        }

        private void Craft(string id)
        {
            bool success = EquipmentCrafting.TryCraft(session.Profile.Inventory, id, out string reason);
            message.text = success ? "장비를 제작했습니다." : reason;
            Refresh();
        }

        private void ToggleEquipment(ItemDefinition item)
        {
            bool equipped = session.Profile.GetEquipped(item.Slot) == item.Id;
            string reason;
            bool success = equipped ? session.Profile.TryUnequip(item.Slot, out reason) : session.Profile.TryEquip(item.Id, out reason);
            message.text = success ? (equipped ? "장비를 해제했습니다." : "장비를 장착했습니다.") : reason;
            Refresh();
        }

        private void OnDestroy()
        {
            if (session != null && session.Profile != null) session.Profile.Changed -= MarkDirty;
            if (active == this) active = null;
            if (settings != null) Destroy(settings);
            if (font != null) Destroy(font);
        }
    }
}
