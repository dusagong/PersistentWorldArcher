using System.Collections.Generic;
using UnityEngine;

namespace PersistentWorld.Networking
{
    // Scoped to the online IMGUI screens. Never modify Unity's shared default skin.
    internal sealed class RpgGuiTheme
    {
        internal static readonly Color Gold = new Color32(194, 153, 77, 255);
        internal static readonly Color Blue = new Color32(113, 169, 199, 255);
        internal static readonly Color Green = new Color32(133, 173, 137, 255);
        internal static readonly Color Red = new Color32(202, 112, 110, 255);
        internal static readonly Color Ink = new Color32(36, 34, 32, 255);
        internal static readonly Color Text = new Color32(242, 232, 208, 255);
        internal GUISkin Skin { get; private set; }
        internal GUIStyle Heading, Center, Muted, SmallButton, Primary, Card, CloseButton, Notice, SmallLeft;
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private Font font;

        internal void Ensure()
        {
            if (Skin != null) return;
            Skin = Object.Instantiate(GUI.skin);
            Skin.hideFlags = HideFlags.HideAndDontSave;
            font = Resources.Load<Font>("UI/Fonts/NanumGothic-Regular");
            if (font != null) Skin.font = font;
            Skin.label.fontSize = 16;
            Skin.label.wordWrap = true;
            Skin.label.richText = false;
            Skin.label.normal.textColor = Text;
            Skin.label.padding = new RectOffset();
            Skin.label.margin = new RectOffset(0, 0, 2, 6);
            Skin.label.alignment = TextAnchor.UpperLeft;
            Skin.button.fontSize = 18;
            Skin.button.alignment = TextAnchor.MiddleCenter;
            Skin.button.richText = false;
            Skin.button.wordWrap = true;
            Skin.button.fixedHeight = 38;
            Skin.button.margin = new RectOffset(0, 0, 3, 5);
            Skin.button.padding = new RectOffset(10, 10, 4, 4);
            Skin.button.border = new RectOffset(4, 4, 4, 4);
            SetButton(Skin.button, new Color32(70, 62, 52, 255), new Color32(145, 125, 94, 255));
            Skin.box.fontSize = 15;
            Skin.box.wordWrap = true;
            Skin.box.richText = false;
            Skin.box.padding = new RectOffset(12, 12, 10, 10);
            Skin.box.border = new RectOffset(4, 4, 4, 4);
            Skin.box.normal.background = Texture(Ink, new Color32(93, 83, 65, 255));
            Skin.box.normal.textColor = Text;
            foreach (GUIStyle field in new[] { Skin.textField, Skin.textArea })
            {
                field.fontSize = 16; field.fixedHeight = 32;
                field.padding = new RectOffset(10, 10, 5, 5);
                field.margin = new RectOffset(0, 0, 2, 8);
                field.border = new RectOffset(4, 4, 4, 4);
                field.normal.background = Texture(new Color32(23, 24, 25, 255), new Color32(109, 98, 77, 255));
                field.focused.background = Texture(new Color32(29, 30, 31, 255), Gold);
                field.normal.textColor = field.focused.textColor = Text;
            }
            Skin.horizontalSlider.fixedHeight = 10;
            Skin.horizontalSlider.margin = new RectOffset(0, 0, 4, 8);
            Skin.horizontalSlider.border = new RectOffset(4, 4, 4, 4);
            Skin.horizontalSlider.normal.background = null;
            Skin.horizontalSliderThumb.fixedWidth = 15;
            Skin.horizontalSliderThumb.fixedHeight = 16;
            Skin.horizontalSliderThumb.normal.background = Texture(Gold, new Color32(244, 215, 150, 255));
            Skin.horizontalSliderThumb.hover.background = Skin.horizontalSliderThumb.active.background = Texture(new Color32(226, 190, 111, 255), Text);
            Skin.horizontalSliderThumb.border = new RectOffset(4, 4, 4, 4);
            Skin.verticalScrollbar.fixedWidth = 12;
            Skin.verticalScrollbar.normal.background = Texture(new Color32(22, 22, 22, 255), new Color32(65, 60, 49, 255));
            Skin.verticalScrollbarThumb.normal.background = Texture(new Color32(111, 98, 72, 255), Gold);
            Skin.verticalScrollbarThumb.hover.background = Skin.verticalScrollbarThumb.active.background = Skin.verticalScrollbarThumb.normal.background;
            Skin.verticalScrollbarThumb.border = new RectOffset(4, 4, 4, 4);
            Skin.verticalScrollbarUpButton.fixedHeight = Skin.verticalScrollbarDownButton.fixedHeight = 0;
            Heading = new GUIStyle(Skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            Center = new GUIStyle(Skin.label) { alignment = TextAnchor.MiddleCenter };
            Muted = new GUIStyle(Center) { fontSize = 14, normal = { textColor = new Color32(204, 193, 171, 255) } };
            SmallButton = new GUIStyle(Skin.button) { fontSize = 14, fixedHeight = 30 };
            Primary = new GUIStyle(Skin.button);
            SetButton(Primary, new Color32(118, 88, 34, 255), Gold);
            Card = new GUIStyle(Skin.box) { alignment = TextAnchor.UpperLeft };
            CloseButton = new GUIStyle(Skin.button) { fixedHeight = 0, fixedWidth = 0, padding = new RectOffset(), margin = new RectOffset() };
            Notice = new GUIStyle(Skin.label) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(), margin = new RectOffset(), wordWrap = true };
            SmallLeft = new GUIStyle(Skin.label) { fontSize = 14, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(), margin = new RectOffset(), wordWrap = false, clipping = TextClipping.Clip };
        }

        private void SetButton(GUIStyle style, Color fill, Color border)
        {
            style.normal.background = Texture(fill, border);
            style.hover.background = Texture(Color.Lerp(fill, Gold, .22f), Gold);
            style.active.background = Texture(Color.Lerp(fill, Color.black, .25f), Gold);
            style.focused.background = style.hover.background;
            style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = Text;
        }

        private Texture2D Texture(Color fill, Color border)
        {
            const int size = 24;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - x - 1, size - y - 1));
                pixels[y * size + x] = edge == 0 ? new Color32(12, 12, 12, 255) : edge == 1 ? border :
                    edge == 2 ? Color.Lerp(fill, y > size / 2 ? Color.white : Color.black, .18f) : Color.Lerp(fill, Color.white, (float)y / size * .04f);
                if ((x < 2 || x >= size - 2) && (y < 2 || y >= size - 2)) pixels[y * size + x] = Color.clear;
            }
            texture.SetPixels(pixels); texture.Apply(false, true); textures.Add(texture); return texture;
        }

        internal static float ScaleFor(float width, float height) => Mathf.Max(.85f, Mathf.Min(width / 1280f, height / 720f));
        internal static Rect Bounds(float width, float height, float preferredWidth, float preferredHeight)
        {
            float scale = ScaleFor(width, height);
            float w = Mathf.Min(preferredWidth, Mathf.Max(1, (width - 32) / scale));
            float h = Mathf.Min(preferredHeight, Mathf.Max(1, (height - 32) / scale));
            return new Rect((width / scale - w) / 2, (height / scale - h) / 2, w, h);
        }

        internal static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }
        internal static void Border(Rect r, Color color, float thickness = 1)
        {
            Fill(new Rect(r.x, r.y, r.width, thickness), color); Fill(new Rect(r.x, r.yMax - thickness, r.width, thickness), color);
            Fill(new Rect(r.x, r.y, thickness, r.height), color); Fill(new Rect(r.xMax - thickness, r.y, thickness, r.height), color);
        }
        internal static void Panel(Rect r, Color accent)
        {
            Fill(new Rect(r.x + 3, r.y + 4, r.width, r.height), new Color(0, 0, 0, .6f));
            Fill(r, Ink); Border(r, new Color32(12, 12, 12, 255), 3);
            Border(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), accent);
            Border(new Rect(r.x + 6, r.y + 6, r.width - 12, r.height - 12), Color.Lerp(accent, Ink, .55f));
            foreach (Vector2 corner in new[] { new Vector2(r.x + 3, r.y + 3), new Vector2(r.xMax - 9, r.y + 3), new Vector2(r.x + 3, r.yMax - 9), new Vector2(r.xMax - 9, r.yMax - 9) })
                Fill(new Rect(corner.x, corner.y, 6, 6), accent);
        }
        internal static void Divider(Rect r, Color accent)
        { Fill(new Rect(r.x, r.y + 2, r.width, 1), Color.Lerp(accent, Ink, .25f)); Fill(new Rect(r.x, r.y, 3, 5), accent); Fill(new Rect(r.xMax - 3, r.y, 3, 5), accent); }

        internal void Dispose()
        {
            if (Skin != null) Object.Destroy(Skin);
            foreach (Texture2D texture in textures) if (texture != null) Object.Destroy(texture);
            textures.Clear(); Skin = null; font = null;
        }
    }
}
