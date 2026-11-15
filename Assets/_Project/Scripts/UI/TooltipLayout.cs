using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace PersistentWorld.UI
{
    internal static class TooltipLayout
    {
        private static readonly ConditionalWeakTable<Label, Placement> placements = new ConditionalWeakTable<Label, Placement>();

        internal static void Show(Label label, float maximumWidth, Vector2 anchor, Rect bounds, float horizontalAnchor = 0)
        {
            label.style.width = StyleKeyword.Auto;
            label.style.height = StyleKeyword.Auto;
            label.style.maxWidth = Mathf.Max(1, maximumWidth);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.overflow = Overflow.Hidden;
            label.style.display = DisplayStyle.Flex;
            Placement placement = placements.GetValue(label, value => new Placement(value));
            placement.Anchor = anchor;
            placement.Bounds = bounds;
            placement.HorizontalAnchor = horizontalAnchor;
            placement.Reposition();
        }

        private sealed class Placement
        {
            private readonly Label label;
            internal Vector2 Anchor;
            internal Rect Bounds;
            internal float HorizontalAnchor;

            internal Placement(Label value)
            {
                label = value;
                label.RegisterCallback<GeometryChangedEvent>(_ => Reposition());
            }

            internal void Reposition()
            {
                if (label.parent == null) return;
                Vector2 size = label.layout.size;
                if (float.IsNaN(size.x) || float.IsNaN(size.y)) return;
                float x = Mathf.Clamp(Anchor.x - size.x * HorizontalAnchor, Bounds.xMin + 4,
                    Mathf.Max(Bounds.xMin + 4, Bounds.xMax - size.x - 4));
                float y = Mathf.Clamp(Anchor.y - size.y, Bounds.yMin + 4,
                    Mathf.Max(Bounds.yMin + 4, Bounds.yMax - size.y - 4));
                Vector2 local = label.parent.WorldToLocal(new Vector2(x, y));
                if (!Mathf.Approximately(label.layout.x, local.x)) label.style.left = local.x;
                if (!Mathf.Approximately(label.layout.y, local.y)) label.style.top = local.y;
            }
        }
    }
}
