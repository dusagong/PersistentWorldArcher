using UnityEngine;
using UnityEngine.UIElements;

namespace PersistentWorld.UI
{
    internal enum SkillGlyph
    {
        Empty, Dodge, Repulse, ArrowRain, Snipe, Whirlwind, ComboSlash, Excalibur,
        HealingZone, Cleanse, Judgment, Boss, DoubleShot, Parry, Revive, Locked
    }

    internal sealed class MouseButtonIcon : VisualElement
    {
        private readonly bool right;
        private bool emphasized;
        public MouseButtonIcon(bool rightButton)
        {
            right = rightButton;
            AddToClassList("combat-mouse-icon");
            AddToClassList(right ? "combat-mouse-right" : "combat-mouse-left");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void SetEmphasis(bool value)
        {
            if (emphasized == value) return;
            emphasized = value;
            EnableInClassList("combat-mouse-emphasized", value);
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w < 2 || h < 2) return;
            var p = context.painter2D;
            p.fillColor = new Color(.06f, .08f, .1f, .9f);
            p.strokeColor = new Color(.7f, .74f, .76f);
            p.lineWidth = 1;
            p.BeginPath(); p.MoveTo(new Vector2(w / 2, 1));
            p.BezierCurveTo(new Vector2(w - 1, 1), new Vector2(w - 1, 3), new Vector2(w - 1, h / 2));
            p.LineTo(new Vector2(w - 1, h - 4));
            p.BezierCurveTo(new Vector2(w - 1, h), new Vector2(1, h), new Vector2(1, h - 4));
            p.LineTo(new Vector2(1, h / 2));
            p.BezierCurveTo(new Vector2(1, 3), new Vector2(1, 1), new Vector2(w / 2, 1));
            p.ClosePath(); p.Fill(); p.Stroke();
            p.fillColor = emphasized ? new Color(.65f, .92f, .83f) : new Color(.65f, .69f, .7f);
            float x = right ? w / 2 + .5f : 2;
            p.BeginPath(); p.MoveTo(new Vector2(x, 3)); p.LineTo(new Vector2(x + w / 2 - 2.5f, 3));
            p.LineTo(new Vector2(x + w / 2 - 2.5f, h / 2)); p.LineTo(new Vector2(x, h / 2));
            p.ClosePath(); p.Fill();
        }
    }

    internal sealed class SkillCooldownIcon : VisualElement
    {
        private readonly Image art = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
        private readonly VisualElement lockMark = new VisualElement { pickingMode = PickingMode.Ignore };
        private Texture2D atlas;
        private SkillGlyph glyph = (SkillGlyph)(-1);
        private bool active, available, cooling;
        public float ReadyFraction { get; private set; }

        public SkillCooldownIcon()
        {
            AddToClassList("combat-skill-icon");
            pickingMode = PickingMode.Ignore;
            art.AddToClassList("combat-skill-art");
            Add(art);
            lockMark.AddToClassList("combat-lock");
            lockMark.generateVisualContent += DrawLock;
            Add(lockMark);
            generateVisualContent += Draw;
        }

        public void Set(SkillGlyph value, float fraction, bool usingSkill, bool canUse, bool onCooldown)
        {
            if (atlas == null)
            {
                atlas = Resources.Load<Texture2D>("UI/SkillIconAtlas");
                if (atlas != null)
                {
                    atlas.filterMode = FilterMode.Point;
                    art.image = atlas;
                }
            }
            int cell = (int)value;
            lockMark.style.display = value == SkillGlyph.Locked ? DisplayStyle.Flex : DisplayStyle.None;
            if (glyph != value)
            {
                art.uv = new Rect(cell % 4 * .25f, 1 - (cell / 4 + 1) * .25f, .25f, .25f);
                art.MarkDirtyRepaint();
            }
            float brightness = canUse ? onCooldown ? .55f : 1 : .4f;
            art.tintColor = new Color(brightness, brightness, brightness, 1);
            fraction = Mathf.Clamp01(fraction);
            if (glyph == value && Mathf.Approximately(ReadyFraction, fraction) && active == usingSkill &&
                available == canUse && cooling == onCooldown) return;
            glyph = value; ReadyFraction = fraction; active = usingSkill; available = canUse; cooling = onCooldown;
            MarkDirtyRepaint();
        }

        private void DrawLock(MeshGenerationContext context)
        {
            var p = context.painter2D;
            p.strokeColor = new Color(.9f, .83f, .6f);
            p.lineWidth = 2;
            p.BeginPath(); p.Arc(new Vector2(10, 8), 5, Angle.Degrees(180), Angle.Degrees(360)); p.Stroke();
            p.fillColor = new Color(.85f, .76f, .51f);
            Rectangle(p, 3, 8, 14, 11);
            p.fillColor = new Color(.08f, .1f, .12f);
            Rectangle(p, 9, 11, 2, 5);
        }

        private void Draw(MeshGenerationContext context)
        {
            if (contentRect.width < 1 || contentRect.height < 1) return;
            var p = context.painter2D;
            float w = contentRect.width, h = contentRect.height;
            p.fillColor = new Color(.055f, .075f, .09f, .8f);
            Rectangle(p, 0, 0, w, h);
            p.lineWidth = 1;
            p.strokeColor = new Color(.38f, .4f, .4f, .75f);
            Perimeter(p, w, h, 1);
            if (ReadyFraction <= 0) return;
            p.strokeColor = available || cooling ? active ? new Color(.58f, .85f, .79f) : new Color(.9f, .86f, .72f)
                : new Color(.38f, .4f, .4f, .75f);
            p.lineWidth = 1.5f;
            Perimeter(p, w, h, ReadyFraction);
        }

        private static void Rectangle(Painter2D p, float x, float y, float w, float h)
        {
            p.BeginPath(); p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(x + w, y));
            p.LineTo(new Vector2(x + w, y + h)); p.LineTo(new Vector2(x, y + h)); p.ClosePath(); p.Fill();
        }

        private static void Perimeter(Painter2D p, float w, float h, float fraction)
        {
            const float inset = 1;
            Vector2[] points = { new Vector2(w / 2, inset), new Vector2(w - inset, inset),
                new Vector2(w - inset, h - inset), new Vector2(inset, h - inset),
                new Vector2(inset, inset), new Vector2(w / 2, inset) };
            float distance = (2 * (w - 2 * inset) + 2 * (h - 2 * inset)) * fraction;
            p.BeginPath(); p.MoveTo(points[0]);
            for (int i = 1; i < points.Length && distance > 0; i++)
            {
                float length = Vector2.Distance(points[i - 1], points[i]);
                p.LineTo(Vector2.Lerp(points[i - 1], points[i], Mathf.Min(1, distance / length)));
                distance -= length;
            }
            p.Stroke();
        }
    }
}
