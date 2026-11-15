using PersistentWorld.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace PersistentWorld.UI
{
    public sealed partial class InventoryWindow
    {
        private VisualElement dragGhost, dragSource, dragDropTarget;
        private UpgradeItemIcon dragIcon;
        private string draggedItem;
        private EquipmentSlot draggedSlot;
        private PlayerInventoryProfile dragProfile;
        private Vector2 dragStart;
        private int dragPointer = -1;
        private bool draggingEquipment;

        private void OnApplicationFocus(bool focused)
        { if (!focused) CancelEquipmentDrag(); }

        private void InitializeEquipmentDrag()
        {
            VisualElement root = document.rootVisualElement;
            dragGhost = new VisualElement { name = "equipment-drag-preview", pickingMode = PickingMode.Ignore, usageHints = UsageHints.DynamicTransform };
            dragGhost.AddToClassList("equipment-drag-ghost");
            dragIcon = new UpgradeItemIcon(); dragGhost.Add(dragIcon); screen.Add(dragGhost);
            root.RegisterCallback<PointerMoveEvent>(MoveEquipmentDrag, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(EndEquipmentDrag, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCancelEvent>(_ => CancelEquipmentDrag());
            root.RegisterCallback<PointerCaptureOutEvent>(evt =>
            { if (evt.target == root && draggingEquipment) CancelEquipmentDrag(); });
            root.RegisterCallback<DetachFromPanelEvent>(_ => CancelEquipmentDrag());
        }

        private void BindEquipmentDrag(Button button, System.Func<string> itemId, EquipmentSlot sourceSlot)
        {
            button.RegisterCallback<PointerMoveEvent>(MoveEquipmentDrag, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerUpEvent>(EndEquipmentDrag, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerCancelEvent>(_ =>
            { if (dragSource == button) CancelEquipmentDrag(); }, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerCaptureOutEvent>(_ =>
            { if (dragSource == button && !draggingEquipment) CancelEquipmentDrag(); }, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (!visible || evt.button != 0 || draggingEquipment || Profile == null) return;
                string id = itemId();
                if (!ItemCatalog.TryGet(id, out ItemDefinition item) || !item.IsEquipment) return;
                CancelEquipmentDrag();
                draggedItem = id; draggedSlot = sourceSlot; dragProfile = Profile;
                dragSource = button; dragStart = evt.position; dragPointer = evt.pointerId;
            }, TrickleDown.TrickleDown);
        }

        private void MoveEquipmentDrag(PointerMoveEvent evt)
        {
            if (dragPointer != evt.pointerId || draggedItem == null) return;
            if (!visible || Profile != dragProfile || (evt.pressedButtons & 1) == 0)
            { CancelEquipmentDrag(); return; }
            if (!draggingEquipment)
            {
                if (((Vector2)evt.position - dragStart).sqrMagnitude < 36) return;
                draggingEquipment = true;
                document.rootVisualElement.CapturePointer(dragPointer);
                dragSource.AddToClassList("equipment-drag-source");
                ItemCatalog.TryGet(draggedItem, out ItemDefinition item);
                dragIcon.SetItem(Icon(item), Profile.GetUpgradeLevel(item.Id));
                dragGhost.style.display = DisplayStyle.Flex;
                hoverHint.style.display = DisplayStyle.None;
                dragGhost.BringToFront();
            }
            Vector2 position = screen.WorldToLocal(evt.position);
            dragGhost.style.translate = new Translate(position.x - 40, position.y - 40, 0);
            if (dragDropTarget != null) { dragDropTarget.RemoveFromClassList("equipment-drop-valid"); dragDropTarget.RemoveFromClassList("equipment-drop-invalid"); }
            FindEquipmentDrop(evt.position, out EquipmentSlot slot, out bool bag, out dragDropTarget);
            if (dragDropTarget != null)
                dragDropTarget.AddToClassList(CanDropEquipment(draggedItem, draggedSlot, slot, bag, out _) ? "equipment-drop-valid" : "equipment-drop-invalid");
            evt.StopImmediatePropagation();
        }

        private void EndEquipmentDrag(PointerUpEvent evt)
        {
            if (dragPointer != evt.pointerId || evt.button != 0) return;
            if (!draggingEquipment) { CancelEquipmentDrag(); return; }
            string id = draggedItem; EquipmentSlot sourceSlot = draggedSlot;
            FindEquipmentDrop(evt.position, out EquipmentSlot slot, out bool bag, out _);
            bool sameProfile = Profile == dragProfile;
            CancelEquipmentDrag();
            evt.StopImmediatePropagation();
            if (sameProfile) DropEquipment(id, sourceSlot, slot, bag);
        }

        private void FindEquipmentDrop(Vector2 position, out EquipmentSlot slot, out bool bag, out VisualElement target)
        {
            slot = EquipmentSlot.None; bag = false; target = null;
            VisualElement picked = document.rootVisualElement.panel?.Pick(position);
            if (picked == null) return;
            foreach (var pair in equipmentButtons)
                if (picked == pair.Value || pair.Value.Contains(picked))
                { slot = pair.Key; target = pair.Value; return; }
            if (bagScroll != null && (picked == grid || grid.Contains(picked) || picked == bagScroll.contentViewport) &&
                bagScroll.contentViewport.worldBound.Contains(position))
            { bag = true; target = bagScroll; }
        }

        private bool CanDropEquipment(string id, EquipmentSlot from, EquipmentSlot to, bool toBag, out string reason)
        {
            reason = "장비를 맞는 장착 칸 또는 가방 칸으로 옮겨주세요.";
            if (Profile == null || !ItemCatalog.TryGet(id, out ItemDefinition item) || !item.IsEquipment) return false;
            if (from != EquipmentSlot.None)
            {
                if (!toBag) return false;
                if (Profile.GetEquipped(from) != id) { reason = "장비 상태가 바뀌었습니다. 다시 선택해주세요."; return false; }
                return Profile.Inventory.CanExchange(System.Array.Empty<ItemAmount>(), new[] { new ItemAmount(id, 1) }, out reason);
            }
            if (to == EquipmentSlot.None || toBag) return false;
            if (to != item.Slot) { reason = "장비 부위에 맞는 칸에 놓아주세요."; return false; }
            if (!item.CanEquip(Profile.Class)) { reason = "현재 직업이 장착할 수 없는 장비입니다."; return false; }
            if (!Profile.Inventory.HasItem(id, 1)) { reason = "보유하지 않은 장비입니다."; return false; }
            reason = string.Empty; return true;
        }

        private bool DropEquipment(string id, EquipmentSlot from, EquipmentSlot to, bool toBag)
        {
            if (!CanDropEquipment(id, from, to, toBag, out string reason))
            { message.text = reason; return false; }
            selectedId = id; selectedSlot = from;
            ApplySelection();
            return true;
        }

        private void CancelEquipmentDrag()
        {
            int pointer = dragPointer;
            dragPointer = -1; draggingEquipment = false; draggedItem = null; dragProfile = null;
            dragSource?.RemoveFromClassList("equipment-drag-source"); dragSource = null;
            if (dragDropTarget != null) { dragDropTarget.RemoveFromClassList("equipment-drop-valid"); dragDropTarget.RemoveFromClassList("equipment-drop-invalid"); }
            dragDropTarget = null;
            if (dragGhost != null) dragGhost.style.display = DisplayStyle.None;
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (pointer >= 0 && root != null && root.HasPointerCapture(pointer)) root.ReleasePointer(pointer);
        }
    }
}
