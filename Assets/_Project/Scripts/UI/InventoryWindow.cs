using System.Collections;
using System.Collections.Generic;
using PersistentWorld.Combat;
using PersistentWorld.Items;
using PersistentWorld.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace PersistentWorld.UI
{
    [DefaultExecutionOrder(-100)]
    public sealed class InventoryWindow : MonoBehaviour
    {
        private static InventoryWindow active;
        public static bool IsOpen => active != null && active.visible;
        private static readonly EquipmentSlot[] Slots = { EquipmentSlot.Head, EquipmentSlot.Chest,
            EquipmentSlot.Legs, EquipmentSlot.Weapon, EquipmentSlot.Hands, EquipmentSlot.Feet };
        private static readonly string[] Categories = { "장비", "소모품", "재료", "퀘스트" };
        private readonly Dictionary<EquipmentSlot, Button> equipmentButtons = new Dictionary<EquipmentSlot, Button>();
        private readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        private ItemSession session;
        private PanelSettings settings;
        private UIDocument document;
        private Font font;
        private VisualElement screen, menu, panel, grid;
        private Label capacity, title, description, message, setStatus;
        private Image preview;
        private Button action;
        private readonly List<Button> tabs = new List<Button>();
        private GameObject actor;
        private ItemCategory category;
        private string selectedId;
        private EquipmentSlot selectedSlot;
        private bool visible, dirty;
        private PlayerInventoryProfile observedProfile;

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
            var tree = Resources.Load<VisualTreeAsset>("UI/InventoryScreen");
            var template = Resources.Load<PanelSettings>("UI/BlacksmithPanel");
            if (tree == null || template == null) { Debug.LogError("Inventory UI assets are missing."); return; }
            settings = Instantiate(template);
            settings.sortingOrder = 101;
            var ui = new GameObject("Inventory UI");
            ui.transform.SetParent(transform, false);
            ui.SetActive(false);
            document = ui.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.visualTreeAsset = tree;
            ui.SetActive(true);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 16);
            document.rootVisualElement.AddToClassList("inventory-document");
            document.rootVisualElement.style.unityFont = font;
            screen = document.rootVisualElement.Q("inventory-screen");
            menu = screen.Q("pause-menu");
            panel = screen.Q("inventory-panel");
            grid = screen.Q("bag-grid");
            capacity = screen.Q<Label>("bag-capacity");
            title = screen.Q<Label>("selection-title");
            description = screen.Q<Label>("selection-description");
            message = screen.Q<Label>("inventory-message");
            setStatus = screen.Q<Label>("set-status");
            preview = screen.Q<Image>("character-preview");
            preview.scaleMode = ScaleMode.ScaleToFit;
            action = screen.Q<Button>("equipment-action");
            action.clicked += ApplySelection;
            screen.Q<Button>("open-inventory").clicked += () => Open(actor);
            screen.Q<Button>("resume-game").clicked += Close;
            screen.Q<Button>("close-inventory").clicked += Close;
            foreach (EquipmentSlot slot in Slots)
            {
                var button = new Button(() => SelectEquipped(slot));
                button.name = "equipment-" + slot.ToString().ToLowerInvariant();
                button.AddToClassList("equipment-slot");
                button.Add(new UpgradeItemIcon());
                button.Add(new Label(SlotName(slot)) { pickingMode = PickingMode.Ignore });
                screen.Q(SlotsIndex(slot) < 3 ? "equipment-left" : "equipment-right").Add(button);
                equipmentButtons.Add(slot, button);
            }
            for (int i = 0; i < Categories.Length; i++)
            {
                var value = (ItemCategory)i;
                var button = new Button(() => SelectCategory(value)) { text = Categories[i] };
                button.AddToClassList("category-tab");
                screen.Q("category-tabs").Add(button);
                tabs.Add(button);
            }
            session.ProfileChanged += BindProfile;
            BindProfile();
        }

        private void BindProfile()
        {
            if (observedProfile != null) observedProfile.Changed -= MarkDirty;
            observedProfile = session.Profile;
            observedProfile.Changed += MarkDirty;
            dirty = true;
        }

        private static int SlotsIndex(EquipmentSlot slot) => System.Array.IndexOf(Slots, slot);
        private void MarkDirty() => dirty = true;

        private void Update()
        {
            if (screen == null) return;
            if (visible)
            {
                var health = actor != null ? actor.GetComponent<Health>() : null;
                if (health == null || health.IsDead || Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
                if (dirty) Refresh();
                return;
            }
        }

        public void Open(GameObject player)
        {
            if (screen == null || player == null) return;
            ArcherController controller = player.GetComponent<ArcherController>();
            if (controller == null || controller.Health.IsDead) return;
            controller.SetMenuOpen(false);
            controller.Skills.Cancel();
            GetComponent<BlacksmithWindow>()?.Close();
            actor = player;
            visible = true;
            selectedId = null;
            selectedSlot = EquipmentSlot.None;
            category = ItemCategory.Equipment;
            screen.RemoveFromClassList("hidden");
            menu.AddToClassList("hidden");
            panel.RemoveFromClassList("hidden");
            screen.Q<Label>("class-label").text = ClassName(session.Profile.Class) + " · 장착 장비";
            preview.sprite = Resources.Load<Sprite>("Characters/ArcherInventoryIdle");
            message.text = "아이템을 선택하면 아래에서 정보를 확인할 수 있습니다.";
            Refresh();
        }

        public void Close()
        {
            if (!visible) return;
            visible = false;
            ItemWindows.ConsumeClose();
            screen?.AddToClassList("hidden");
            actor = null;
        }

        private void SelectCategory(ItemCategory value)
        {
            category = value;
            selectedId = null;
            selectedSlot = EquipmentSlot.None;
            Refresh();
        }

        private void SelectEquipped(EquipmentSlot slot)
        {
            selectedSlot = slot;
            selectedId = session.Profile.GetEquipped(slot);
            Refresh();
        }

        private Sprite Icon(ItemDefinition item)
        {
            if (!icons.TryGetValue(item.Id, out Sprite sprite))
            { sprite = Resources.Load<Sprite>(item.IconPath); icons.Add(item.Id, sprite); }
            return sprite;
        }

        private void Refresh()
        {
            dirty = false;
            var profile = session.Profile;
            var inventory = profile.Inventory;
            if (selectedSlot != EquipmentSlot.None) selectedId = profile.GetEquipped(selectedSlot);
            else if (selectedId != null && inventory.GetCount(selectedId) == 0) selectedId = null;
            capacity.text = $"전체 가방 {inventory.UsedSlots} / {inventory.Capacity}칸 · 장착 장비 제외";
            for (int i = 0; i < tabs.Count; i++) tabs[i].EnableInClassList("selected", i == (int)category);
            foreach (var pair in equipmentButtons)
            {
                ItemCatalog.TryGet(profile.GetEquipped(pair.Key), out ItemDefinition equipped);
                pair.Value.Q<UpgradeItemIcon>().SetItem(equipped == null ? null : Icon(equipped), equipped == null ? 0 : profile.GetUpgradeLevel(equipped.Id));
                pair.Value.Q<Label>().text = SlotName(pair.Key) + (equipped != null && profile.GetUpgradeLevel(equipped.Id) > 0 ? " +" + profile.GetUpgradeLevel(equipped.Id) : "");
                pair.Value.tooltip = equipped == null ? SlotName(pair.Key) + " · 비어 있음" : profile.DisplayName(equipped);
                pair.Value.EnableInClassList("selected", selectedSlot == pair.Key);
            }
            int pieces = profile.GetBonuses().SetPieces;
            setStatus.text = $"세트 {pieces}/6\n" +
                $"3부위: 일반 몬스터 피해 +20% {(pieces >= 3 ? "[적용]" : "")}\n" +
                $"5부위: 보스 피격 피해 -20% {(pieces >= 5 ? "[적용]" : "")}\n" +
                $"6부위: 보스에게 주는 피해 +30% {(pieces >= 6 ? "[적용]" : "")}";
            grid.Clear();
            int cells = 0;
            foreach (ItemDefinition item in ItemCatalog.All)
            {
                if (item.Category != category) continue;
                int remaining = inventory.GetCount(item.Id);
                int limit = item.IsEquipment ? 1 : inventory.StackLimit;
                while (remaining > 0)
                {
                    int quantity = System.Math.Min(remaining, limit);
                    var cell = new Button(() => { selectedId = item.Id; selectedSlot = EquipmentSlot.None; Refresh(); });
                    cell.AddToClassList("bag-cell");
                    cell.tooltip = profile.DisplayName(item);
                    var icon = new UpgradeItemIcon();
                    icon.SetItem(Icon(item), profile.GetUpgradeLevel(item.Id));
                    cell.Add(icon);
                    if (profile.GetUpgradeLevel(item.Id) > 0)
                    {
                        var upgrade = new Label("+" + profile.GetUpgradeLevel(item.Id)) { pickingMode = PickingMode.Ignore };
                        upgrade.AddToClassList("upgrade-level");
                        cell.Add(upgrade);
                    }
                    if (!item.IsEquipment)
                    {
                        var count = new Label(quantity.ToString()) { pickingMode = PickingMode.Ignore };
                        count.AddToClassList("stack-count");
                        cell.Add(count);
                    }
                    cell.EnableInClassList("selected", selectedSlot == EquipmentSlot.None && selectedId == item.Id);
                    grid.Add(cell);
                    remaining -= quantity;
                    cells++;
                }
            }
            while (cells++ < inventory.Capacity)
            {
                var empty = new Button(() => { selectedId = null; selectedSlot = EquipmentSlot.None; Refresh(); });
                empty.AddToClassList("bag-cell");
                grid.Add(empty);
            }
            RefreshSelection();
            if (!string.IsNullOrEmpty(session.SaveError)) message.text = "저장할 수 없습니다. 저장 위치를 확인해주세요.";
        }

        private void RefreshSelection()
        {
            action.AddToClassList("hidden");
            if (!ItemCatalog.TryGet(selectedId, out ItemDefinition item))
            {
                title.text = selectedSlot == EquipmentSlot.None ? "선택한 아이템 없음" : SlotName(selectedSlot) + " · 비어 있음";
                description.text = "왼쪽 장착 장비 또는 오른쪽 가방의 아이템을 선택하세요.";
                return;
            }
            bool equipped = selectedSlot != EquipmentSlot.None;
            title.text = session.Profile.DisplayName(item) + (equipped ? " · 장착 중" : "");
            if (!item.IsEquipment)
            {
                description.text = Categories[(int)item.Category] + $" · 총 보유 {session.Profile.Inventory.GetCount(item.Id)}개";
                return;
            }
            string stats = "";
            if (item.Attack != 0) stats += $"공격력 +{item.Attack + session.Profile.GetUpgradeLevel(item.Id)}  ";
            if (item.Defense != 0) stats += $"방어력 +{item.Defense}  ";
            if (item.MoveSpeed != 0) stats += $"이동속도 +{item.MoveSpeed:0.0}";
            bool allowed = item.CanEquip(session.Profile.Class);
            description.text = SlotName(item.Slot) + " · " + (item.RequiredClass.HasValue ? ClassName(item.RequiredClass.Value) + " 전용" : "전 직업 공용") +
                "\n" + stats + (allowed ? "" : "\n현재 직업으로 장착할 수 없습니다.");
            action.text = equipped ? "해제" : "장착";
            action.RemoveFromClassList("hidden");
            bool sameEquipped = !equipped && session.Profile.GetEquipped(item.Slot) == item.Id;
            action.SetEnabled(allowed && !sameEquipped);
            if (sameEquipped) description.text += "\n같은 장비를 이미 장착하고 있습니다.";
        }

        private void ApplySelection()
        {
            if (selectedId == null) return;
            bool removing = selectedSlot != EquipmentSlot.None;
            string reason;
            bool success = removing ? session.Profile.TryUnequip(selectedSlot, out reason) : session.Profile.TryEquip(selectedId, out reason);
            if (success && !removing && ItemCatalog.TryGet(selectedId, out ItemDefinition item)) selectedSlot = item.Slot;
            message.text = success ? (removing ? "장비를 해제했습니다." : "장비를 장착했습니다.") : reason;
            Refresh();
        }

        private static string ClassName(PlayerClass value) => value == PlayerClass.Warrior ? "전사" : value == PlayerClass.Archer ? "궁수" : "힐러";
        private static string SlotName(EquipmentSlot value)
        {
            switch (value)
            {
                case EquipmentSlot.Weapon: return "무기";
                case EquipmentSlot.Head: return "모자";
                case EquipmentSlot.Chest: return "상의";
                case EquipmentSlot.Legs: return "하의";
                case EquipmentSlot.Hands: return "장갑";
                case EquipmentSlot.Feet: return "신발";
                default: return "";
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            Close();
            if (observedProfile != null) observedProfile.Changed -= MarkDirty;
            if (session != null) session.ProfileChanged -= BindProfile;
            observedProfile = null;
            if (active == this) active = null;
            if (document != null) Destroy(document.gameObject);
            if (settings != null) Destroy(settings);
            if (font != null) Destroy(font);
            document = null;
            settings = null;
            font = null;
            screen = null;
            menu = null;
            panel = null;
            grid = null;
            equipmentButtons.Clear();
            tabs.Clear();
            icons.Clear();
            selectedId = null;
            selectedSlot = EquipmentSlot.None;
            dirty = false;
        }
    }
}
