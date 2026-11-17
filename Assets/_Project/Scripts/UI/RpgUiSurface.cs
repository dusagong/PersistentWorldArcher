using System.Collections.Generic;
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

        // 석재·황동 스킨: 창은 돌 테두리와 황동 모서리, 칸은 안으로 파인 홈, 버튼은 입체감 있는 돌판.
        // 픽셀 단위 사각형만 쌓아 그리므로 흐려지지 않는다. 크기·배치는 소유 요소를 그대로 따른다.
        private static readonly int Outline = 0x17130F, StoneDark = 0x4F4740, Stone = 0x6E665C, StoneLight = 0x8C8476,
            BrassDark = 0x7A5520, Brass = 0xB8893C, BrassLight = 0xECC77A;

        private void Draw(MeshGenerationContext context)
        {
            float w = Mathf.Floor(contentRect.width), h = Mathf.Floor(contentRect.height);
            if (w < 8 || h < 8 || parent == null) return;
            bool selected = parent.ClassListContains("selected") || parent.ClassListContains("equipped-row");
            bool enabled = parent.enabledInHierarchy;
            bool hot = hovered && enabled;
            var q = new List<(Rect rect, Color top, Color bottom)>();
            void Box(float x, float y, float bw, float bh, int top, int bottom = -1) =>
                q.Add((new Rect(x, y, bw, bh), Ink(top), Ink(bottom < 0 ? top : bottom)));
            void Ring(float inset, float t, int color)
            {
                Box(inset, inset, w - inset * 2, t, color);
                Box(inset, h - inset - t, w - inset * 2, t, color);
                Box(inset, inset + t, t, h - (inset + t) * 2, color);
                Box(w - inset - t, inset + t, t, h - (inset + t) * 2, color);
            }

            switch (finish)
            {
                case Finish.Window:
                {
                    const float band = 6, cap = 14;
                    Box(0, 0, w, h, 0x2A2C31, 0x1E1F23);
                    Ring(0, 2, Outline);
                    Ring(2, band, Stone);
                    Box(2, 2, w - 4, 1, StoneLight);              // 돌 윗면 빛
                    Box(2, 3, 1, h - 5, StoneLight);
                    Box(3, h - 2 - band, w - 5, 1, StoneDark);    // 돌 아랫면 그늘
                    Box(w - 2 - band, 3, 1, h - 3 - band, StoneDark);
                    for (float x = 18; x < w - 18; x += 23)       // 돌 이음매
                    {
                        Box(x, 2, 1, band, StoneDark);
                        Box(x + 11, h - 2 - band, 1, band, StoneDark);
                    }
                    Ring(2 + band, 1, Outline);
                    Box(3 + band, 3 + band, w - 6 - band * 2, 1, 0x34363C);
                    foreach (var (cx, cy) in new[] { (0f, 0f), (w - cap, 0f), (0f, h - cap), (w - cap, h - cap) })
                        Cap(Box, cx, cy, cap);
                    break;
                }
                case Finish.Section:
                case Finish.Row:
                    Box(0, 0, w, h, finish == Finish.Row ? 0x2E2F34 : 0x26282D, finish == Finish.Row ? 0x26272B : 0x1F2024);
                    Ring(0, 1, Outline);
                    Ring(1, 1, selected ? Brass : 0x4A443D);
                    Box(2, 2, w - 4, 1, selected ? BrassLight : 0x3B3C42);
                    break;
                case Finish.Slot:
                    Box(0, 0, w, h, hot ? 0x202127 : 0x15161A, hot ? 0x26282D : 0x1C1D21);
                    Ring(0, 1, 0x0E0F12);
                    Box(1, 1, w - 2, 2, 0x0B0C0E);                // 안으로 파인 홈: 위·왼쪽 그늘
                    Box(1, 3, 2, h - 4, 0x0B0C0E);
                    Box(2, h - 2, w - 3, 1, 0x3A3C42);            // 아래·오른쪽 빛
                    Box(w - 2, 3, 1, h - 5, 0x3A3C42);
                    if (selected || hot)
                    {
                        Ring(0, 1, selected ? Brass : StoneLight);
                        if (selected)
                            foreach (var (cx, cy, dx, dy) in new[] { (1f, 1f, 1, 1), (w - 2, 1f, -1, 1), (1f, h - 2, 1, -1), (w - 2, h - 2, -1, -1) })
                            {
                                Box(Mathf.Min(cx, cx + dx * 4), cy, 5, 1, BrassLight);
                                Box(cx, Mathf.Min(cy, cy + dy * 4), 1, 5, BrassLight);
                            }
                    }
                    break;
                case Finish.Button:
                    // 버튼 면은 USS 배경색이 칠하고(글자가 그 위에 먼저 그려진다), 여기서는 테두리와 입체감만 더한다
                    Ring(0, 1, Outline);
                    Box(1, 1, w - 2, 1, !enabled ? 0x4A4640 : selected ? BrassLight : hot ? 0xA39A8A : StoneLight);
                    Box(1, 2, 1, h - 4, !enabled ? 0x4A4640 : selected ? BrassLight : hot ? 0xA39A8A : StoneLight);
                    Box(1, h - 3, w - 2, 2, !enabled ? 0x25231F : selected ? BrassDark : 0x2E2A26);
                    Box(w - 2, 2, 1, h - 5, !enabled ? 0x25231F : selected ? BrassDark : 0x2E2A26);
                    if (selected) Ring(0, 1, Brass);
                    break;
            }
            Paint(context, q);
        }

        // 황동 모서리 장식
        private static void Cap(System.Action<float, float, float, float, int, int> box, float x, float y, float size)
        {
            box(x, y, size, size, Outline, -1);
            box(x + 1, y + 1, size - 2, size - 2, Brass, -1);
            box(x + 1, y + 1, size - 2, 1, BrassLight, -1);
            box(x + 1, y + 2, 1, size - 3, BrassLight, -1);
            box(x + 2, y + size - 2, size - 3, 1, BrassDark, -1);
            box(x + size - 2, y + 2, 1, size - 4, BrassDark, -1);
            box(x + size / 2 - 1, y + size / 2 - 1, 2, 2, Outline, -1);
        }

        private static void Paint(MeshGenerationContext context, List<(Rect rect, Color top, Color bottom)> boxes)
        {
            boxes.RemoveAll(b => b.rect.width <= 0 || b.rect.height <= 0);
            if (boxes.Count == 0) return;
            var mesh = context.Allocate(boxes.Count * 4, boxes.Count * 6);
            ushort i = 0;
            foreach (var (r, top, bottom) in boxes)
            {
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, r.yMin, Vertex.nearZ), tint = top });
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, r.yMin, Vertex.nearZ), tint = top });
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, r.yMax, Vertex.nearZ), tint = bottom });
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, r.yMax, Vertex.nearZ), tint = bottom });
                mesh.SetNextIndex(i); mesh.SetNextIndex((ushort)(i + 1)); mesh.SetNextIndex((ushort)(i + 2));
                mesh.SetNextIndex((ushort)(i + 2)); mesh.SetNextIndex((ushort)(i + 3)); mesh.SetNextIndex(i);
                i += 4;
            }
        }
    }
}
