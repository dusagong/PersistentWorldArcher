using System.Collections;
using System.Collections.Generic;
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

        private void OnEnable() => StartCoroutine(InitializeWhenReady());

        private IEnumerator InitializeWhenReady()
        {
            session = GetComponent<ItemSession>();
            while (session != null && session.Profile == null) yield return null;
            if (session != null) Initialize();
        }

        private void Initialize()
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
            message.text = "방어구는 제작하고, 시작 무기는 최대 +3까지 강화할 수 있습니다.";
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
            foreach (ItemDefinition item in ItemCatalog.All)
            {
                if (!item.CanEquip(profile.Class)) continue;
                bool weapon = item.Slot == EquipmentSlot.Weapon;
                EquipmentRecipe recipe = weapon ? null : EquipmentCrafting.Find(item.Id);
                if (!weapon && recipe == null) continue;
                var row = new VisualElement { name = "recipe-" + item.Id };
                row.AddToClassList("recipe-row");
                var icon = new UpgradeItemIcon();
                icon.SetItem(Resources.Load<Sprite>(item.IconPath), profile.GetUpgradeLevel(item.Id));
                icon.AddToClassList("item-icon");
                row.Add(icon);
                var description = new VisualElement();
                description.AddToClassList("recipe-description");
                bool equipped = profile.GetEquipped(item.Slot) == item.Id;
                bool owned = profile.OwnsEquipment(item.Id);
                var nameLabel = new Label(profile.DisplayName(item) + (equipped ? "  [장착 중]" : owned ? "  [보유]" : "  [미보유]"));
                nameLabel.AddToClassList("item-name");
                description.Add(nameLabel);
                description.Add(new Label($"공격 +{item.Attack + profile.GetUpgradeLevel(item.Id)} · 방어 +{item.Defense} · 이동 +{item.MoveSpeed:0.0}"));
                IReadOnlyList<ItemAmount> costs = null;
                string progress = null;
                if (weapon)
                {
                    int level = profile.WeaponUpgradeLevel;
                    if (level >= WeaponUpgrading.MaximumLevel) progress = "최대 강화 +3 · 공격력 추가 +3";
                    else
                    {
                        costs = WeaponUpgrading.MaterialsFor(level + 1);
                        progress = $"강화 +{level} → +{level + 1}";
                    }
                }
                else costs = recipe.Materials;
                var requirements = new VisualElement();
                requirements.AddToClassList("requirements");
                if (progress != null)
                {
                    var label = new Label(progress);
                    label.AddToClassList("recipe-progress");
                    requirements.Add(label);
                }
                if (costs != null)
                    foreach (ItemAmount cost in costs)
                    {
                        if (cost.Quantity < 1) continue;
                        ItemCatalog.TryGet(cost.ItemId, out ItemDefinition material);
                        int have = inventory.GetCount(cost.ItemId);
                        var label = new Label($"{material.Name} {have}/{cost.Quantity}");
                        label.AddToClassList("material-requirement");
                        label.AddToClassList(have >= cost.Quantity ? "material-enough" : "material-missing");
                        requirements.Add(label);
                    }
                description.Add(requirements);
                row.Add(description);
                var craft = new Button(() => { if (weapon) UpgradeWeapon(); else Craft(item.Id); })
                { name = "make-" + item.Id, text = weapon ? "강화" : owned ? "보유" : "제작" };
                bool canMake = weapon ? profile.CanUpgradeWeapon(out string reason) : EquipmentCrafting.CanCraft(inventory, item.Id, out reason);
                craft.SetEnabled(canMake);
                craft.tooltip = canMake ? (weapon ? "100% 성공 · 추가 공격력 +1" : "방어구 제작") : reason;
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

        private void UpgradeWeapon()
        {
            bool success = session.Profile.TryUpgradeWeapon(out string reason);
            message.text = success ? $"무기를 +{session.Profile.WeaponUpgradeLevel}로 강화했습니다." : reason;
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

        private void OnDisable()
        {
            StopAllCoroutines();
            Close();
            if (session != null && session.Profile != null) session.Profile.Changed -= MarkDirty;
            if (active == this) active = null;
            if (document != null) Destroy(document.gameObject);
            if (settings != null) Destroy(settings);
            if (font != null) Destroy(font);
            document = null;
            settings = null;
            font = null;
            screen = null;
            recipes = null;
            materials = null;
            message = null;
            dirty = false;
        }
    }
}
