using System.Collections.Generic;
using UnityEngine;

namespace PersistentWorld.Networking
{
    // Scoped to the online IMGUI screens. Never modify Unity's shared default skin.
    internal sealed class RpgGuiTheme
    {
        internal static readonly Color Gold = new Color32(196, 150, 70, 255);
        internal static readonly Color Blue = new Color32(113, 169, 199, 255);
        internal static readonly Color Green = new Color32(133, 173, 137, 255);
        internal static readonly Color Red = new Color32(202, 112, 110, 255);
        internal static readonly Color Ink = new Color32(36, 38, 43, 255);
        internal static readonly Color Text = new Color32(238, 230, 212, 255);
        private static readonly Color Outline = new Color32(23, 19, 15, 255);
        private static readonly Color StoneDark = new Color32(79, 71, 64, 255);
        private static readonly Color Stone = new Color32(110, 102, 92, 255);
        private static readonly Color StoneLight = new Color32(140, 132, 118, 255);
        private static readonly Color BrassDark = new Color32(122, 85, 32, 255);
        private static readonly Color BrassLight = new Color32(236, 199, 122, 255);
        internal GUISkin Skin { get; private set; }
        internal GUIStyle Heading, Center, Muted, SmallButton, Primary, Card, CloseButton, Notice, SmallLeft;
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private Font font;

        internal void Ensure()
        {
            if (Skin != null) return;
            Skin = Object.Instantiate(GUI.skin);
            Skin.hideFlags = HideFlags.HideAndDontSave;
            font = Resources.Load<Font>("UI/Fonts/Galmuri11");
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
            SetButton(Skin.button, new Color32(76, 71, 65, 255), StoneLight);
            Skin.box.fontSize = 15;
            Skin.box.wordWrap = true;
            Skin.box.richText = false;
            Skin.box.padding = new RectOffset(12, 12, 10, 10);
            Skin.box.border = new RectOffset(4, 4, 4, 4);
            Skin.box.normal.background = Texture(new Color32(30, 31, 35, 255), Stone);
            Skin.box.normal.textColor = Text;
            foreach (GUIStyle field in new[] { Skin.textField, Skin.textArea })
            {
                field.fontSize = 16; field.fixedHeight = 32;
                field.padding = new RectOffset(10, 10, 5, 5);
                field.margin = new RectOffset(0, 0, 2, 8);
                field.border = new RectOffset(4, 4, 4, 4);
                field.normal.background = Texture(new Color32(21, 22, 26, 255), StoneDark);
                field.focused.background = Texture(new Color32(26, 27, 31, 255), Gold);
                field.normal.textColor = field.focused.textColor = Text;
            }
            Skin.horizontalSlider.fixedHeight = 10;
            Skin.horizontalSlider.margin = new RectOffset(0, 0, 4, 8);
            Skin.horizontalSlider.border = new RectOffset(4, 4, 4, 4);
            Skin.horizontalSlider.normal.background = null;
            Skin.horizontalSliderThumb.fixedWidth = 15;
            Skin.horizontalSliderThumb.fixedHeight = 16;
            Skin.horizontalSliderThumb.normal.background = Texture(Gold, BrassLight);
            Skin.horizontalSliderThumb.hover.background = Skin.horizontalSliderThumb.active.background = Texture(new Color32(226, 190, 111, 255), Text);
            Skin.horizontalSliderThumb.border = new RectOffset(4, 4, 4, 4);
            Skin.verticalScrollbar.fixedWidth = 12;
            Skin.verticalScrollbar.normal.background = Texture(new Color32(18, 19, 22, 255), StoneDark);
            Skin.verticalScrollbarThumb.normal.background = Texture(Stone, StoneLight);
            Skin.verticalScrollbarThumb.hover.background = Skin.verticalScrollbarThumb.active.background = Skin.verticalScrollbarThumb.normal.background;
            Skin.verticalScrollbarThumb.border = new RectOffset(4, 4, 4, 4);
            Skin.verticalScrollbarUpButton.fixedHeight = Skin.verticalScrollbarDownButton.fixedHeight = 0;
            Heading = new GUIStyle(Skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter, normal = { textColor = BrassLight } };
            Font bold = Resources.Load<Font>("UI/Fonts/Galmuri11-Bold");
            if (bold != null) Heading.font = bold; else Heading.fontStyle = FontStyle.Bold;
            Center = new GUIStyle(Skin.label) { alignment = TextAnchor.MiddleCenter };
            Muted = new GUIStyle(Center) { fontSize = 14, normal = { textColor = new Color32(190, 180, 160, 255) } };
            SmallButton = new GUIStyle(Skin.button) { fontSize = 14, fixedHeight = 30 };
            Primary = new GUIStyle(Skin.button);
            SetButton(Primary, new Color32(176, 128, 56, 255), BrassLight);
            Primary.normal.textColor = Primary.hover.textColor = Primary.active.textColor = Primary.focused.textColor = new Color32(30, 22, 11, 255);
            Card = new GUIStyle(Skin.box) { alignment = TextAnchor.UpperLeft };
            CloseButton = new GUIStyle(Skin.button) { fixedHeight = 0, fixedWidth = 0, padding = new RectOffset(), margin = new RectOffset() };
            Notice = new GUIStyle(Skin.label) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(), margin = new RectOffset(), wordWrap = true };
            SmallLeft = new GUIStyle(Skin.label) { fontSize = 14, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(), margin = new RectOffset(), wordWrap = false, clipping = TextClipping.Clip };
        }

        private void SetButton(GUIStyle style, Color fill, Color border)
        {
            style.normal.background = Texture(fill, border);
            style.hover.background = Texture(Color.Lerp(fill, Color.white, .14f), Gold);
            style.active.background = Texture(Color.Lerp(fill, Color.black, .22f), Gold);
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
                // 텍스처의 y는 아래에서 위로 올라간다: 큰 y가 화면 위쪽이다
                int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - x - 1, size - y - 1));
                bool upperLeft = y >= size - 3 || x <= 2;
                Color c = Color.Lerp(fill, Color.white, (float)y / size * .06f);
                if (edge == 0) c = Outline;
                else if (edge == 1) c = upperLeft ? border : Color.Lerp(border, Color.black, .45f);
                else if (edge == 2) c = upperLeft ? Color.Lerp(fill, Color.white, .2f) : Color.Lerp(fill, Color.black, .35f);
                else if (y == 3) c = Color.Lerp(fill, Color.black, .2f);
                pixels[y * size + x] = c;
                if ((x < 1 || x >= size - 1) && (y < 1 || y >= size - 1)) pixels[y * size + x] = Color.clear;
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
            bool small = r.height < 60;
            float band = small ? 3 : 6, cap = small ? 9 : 14;
            Fill(new Rect(r.x + 3, r.y + 4, r.width, r.height), new Color(0, 0, 0, .55f));
            Fill(r, Ink);
            Fill(new Rect(r.x, r.y, r.width, r.height * .5f), new Color(1, 1, 1, .025f));
            Border(r, Outline, 2);
            Border(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), Stone, band);
            Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, 1), StoneLight);
            Fill(new Rect(r.x + 2, r.y + 3, 1, r.height - 5), StoneLight);
            Fill(new Rect(r.x + 3, r.yMax - 3 - band + 1, r.width - 5, 1), StoneDark);
            Fill(new Rect(r.xMax - 3 - band + 1, r.y + 3, 1, r.height - 3 - band), StoneDark);
            for (float x = r.x + 18; x < r.xMax - 18; x += 23)
            { Fill(new Rect(x, r.y + 2, 1, band), StoneDark); Fill(new Rect(x + 11, r.yMax - 2 - band, 1, band), StoneDark); }
            Border(new Rect(r.x + 2 + band, r.y + 2 + band, r.width - 4 - band * 2, r.height - 4 - band * 2), Outline);
            Fill(new Rect(r.x + 3 + band, r.y + 3 + band, r.width - 6 - band * 2, 1), Color.Lerp(accent, Ink, .55f));
            foreach (Vector2 c in new[] { new Vector2(r.x, r.y), new Vector2(r.xMax - cap, r.y), new Vector2(r.x, r.yMax - cap), new Vector2(r.xMax - cap, r.yMax - cap) })
            {
                Fill(new Rect(c.x, c.y, cap, cap), Outline);
                Fill(new Rect(c.x + 1, c.y + 1, cap - 2, cap - 2), Gold);
                Fill(new Rect(c.x + 1, c.y + 1, cap - 2, 1), BrassLight);
                Fill(new Rect(c.x + 1, c.y + 2, 1, cap - 3), BrassLight);
                Fill(new Rect(c.x + 2, c.y + cap - 2, cap - 3, 1), BrassDark);
                Fill(new Rect(c.x + cap - 2, c.y + 2, 1, cap - 4), BrassDark);
                Fill(new Rect(c.x + cap / 2 - 1, c.y + cap / 2 - 1, 2, 2), Outline);
            }
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
