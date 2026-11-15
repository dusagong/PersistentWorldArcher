using UnityEngine;
using UnityEngine.UIElements;

namespace PersistentWorld.UI
{
    // Decorative child only: it never picks, captures, or consumes input.
    internal sealed class RpgUiSurface : VisualElement
    {
        internal enum Finish { Window, Section, Slot, Button, Row }
        private readonly Finish finish;
        private bool hovered;

        internal static void AddTo(VisualElement target, Finish finish)
        {
            target.Insert(0, new RpgUiSurface(target, finish));
        }

        internal static void Repaint(VisualElement target)
        {
            if (target.childCount > 0 && target[0] is RpgUiSurface surface) surface.MarkDirtyRepaint();
        }

        private RpgUiSurface(VisualElement owner, Finish value)
        {
            finish = value;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0;
            owner.RegisterCallback<PointerEnterEvent>(_ => { hovered = true; MarkDirtyRepaint(); });
            owner.RegisterCallback<PointerLeaveEvent>(_ => { hovered = false; MarkDirtyRepaint(); });
            generateVisualContent += Draw;
        }

        private static Color Ink(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        private void Draw(MeshGenerationContext context)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w < 8 || h < 8 || parent == null) return;
            bool selected = parent.ClassListContains("selected") || parent.ClassListContains("equipped-row");
            bool enabled = parent.enabledInHierarchy;
            Color top = Ink(0x1C2D40), bottom = Ink(0x101B29);
            if (finish == Finish.Window) { top = Ink(0x182637); bottom = Ink(0x101A27); }
            if (finish == Finish.Slot) { top = Ink(0x0B1520); bottom = Ink(0x22384B); }
            if (finish == Finish.Button) { top = Ink(enabled ? 0x354959 : 0x27313C); bottom = Ink(enabled ? 0x202E3D : 0x1D2631); }
            if (finish == Finish.Row) { top = Ink(0x233244); bottom = Ink(0x152232); }
            if (hovered && enabled && (finish == Finish.Button || finish == Finish.Slot))
            { top = Color.Lerp(top, Ink(0x6587A0), .18f); bottom = Color.Lerp(bottom, Ink(0x45647C), .16f); }
            // Button text is painted by Button itself, before its decorative children.
            // Keep its USS background and draw only the rim above that text.
            if (finish != Finish.Button) Quad(context, w, h, top, bottom);
            var p = context.painter2D;
            bool gold = finish == Finish.Window || selected;
            Color rim = Ink(gold ? 0xB78F49 : 0x627D92);
            Color light = Ink(gold ? 0xEDD49A : 0xA4B9C8);
            if (!enabled) { rim = Ink(0x414D59); light = Ink(0x667581); }
            float cut = finish == Finish.Window ? 7 : 3;
            Frame(p, w, h, 1, cut, Ink(0x080F17), 2);
            Frame(p, w, h, 2, cut, rim, 1);
            Frame(p, w, h, 4, Mathf.Max(1, cut - 2), Ink(gold ? 0x625038 : 0x2C4356), 1);
            Line(p, new Vector2(6, 3), new Vector2(w - 6, 3), light);
            Line(p, new Vector2(6, h - 3), new Vector2(w - 6, h - 3), Ink(gold ? 0x82663B : 0x3D596C));
            if (finish == Finish.Slot || finish == Finish.Window)
            {
                float length = finish == Finish.Window ? 9 : 4;
                Corner(p, 4, 4, 1, 1, length, light);
                Corner(p, w - 4, 4, -1, 1, length, light);
                Corner(p, 4, h - 4, 1, -1, length, rim);
                Corner(p, w - 4, h - 4, -1, -1, length, rim);
            }
        }

        private static void Quad(MeshGenerationContext context, float w, float h, Color top, Color bottom)
        {
            var mesh = context.Allocate(4, 6);
            mesh.SetNextVertex(new Vertex { position = new Vector3(0, 0, Vertex.nearZ), tint = top });
            mesh.SetNextVertex(new Vertex { position = new Vector3(w, 0, Vertex.nearZ), tint = top });
            mesh.SetNextVertex(new Vertex { position = new Vector3(w, h, Vertex.nearZ), tint = bottom });
            mesh.SetNextVertex(new Vertex { position = new Vector3(0, h, Vertex.nearZ), tint = bottom });
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(2); mesh.SetNextIndex(3); mesh.SetNextIndex(0);
        }

        private static void Frame(Painter2D p, float w, float h, float inset, float cut, Color color, float width)
        {
            p.strokeColor = color; p.lineWidth = width;
            p.BeginPath();
            p.MoveTo(new Vector2(inset + cut, inset));
            p.LineTo(new Vector2(w - inset - cut, inset));
            p.LineTo(new Vector2(w - inset, inset + cut));
            p.LineTo(new Vector2(w - inset, h - inset - cut));
            p.LineTo(new Vector2(w - inset - cut, h - inset));
            p.LineTo(new Vector2(inset + cut, h - inset));
            p.LineTo(new Vector2(inset, h - inset - cut));
            p.LineTo(new Vector2(inset, inset + cut));
            p.ClosePath(); p.Stroke();
        }

        private static void Line(Painter2D p, Vector2 a, Vector2 b, Color color)
        {
            p.strokeColor = color; p.lineWidth = 1;
            p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
        }

        private static void Corner(Painter2D p, float x, float y, float dx, float dy, float length, Color color)
        {
            p.strokeColor = color; p.lineWidth = 1;
            p.BeginPath(); p.MoveTo(new Vector2(x, y + dy * length));
            p.LineTo(new Vector2(x, y)); p.LineTo(new Vector2(x + dx * length, y)); p.Stroke();
        }
    }
}
