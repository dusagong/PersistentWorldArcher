using PersistentWorld.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace PersistentWorld.UI
{
    public sealed class UpgradeItemIcon : VisualElement
    {
        private readonly UpgradeHalo halo = new UpgradeHalo();
        private readonly Image image = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
        public int UpgradeLevel { get; private set; }

        public UpgradeItemIcon()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("upgrade-item-icon");
            image.AddToClassList("upgrade-icon-image");
            Add(halo);
            Add(image);
        }

        public void SetItem(Sprite sprite, int level)
        {
            image.sprite = sprite;
            UpgradeLevel = sprite != null ? Mathf.Clamp(level, 0, 3) : 0;
            halo.SetLevel(UpgradeLevel);
        }

        private sealed class UpgradeHalo : VisualElement
        {
            private readonly IVisualElementScheduledItem pulse;
            private int level;

            public UpgradeHalo()
            {
                pickingMode = PickingMode.Ignore;
                AddToClassList("upgrade-halo");
                AddToClassList("upgrade-halo-off");
                generateVisualContent += DrawHalo;
                pulse = schedule.Execute(MarkDirtyRepaint).Every(80);
                pulse.Pause();
                RegisterCallback<AttachToPanelEvent>(_ => UpdatePulse());
                RegisterCallback<DetachFromPanelEvent>(_ => pulse.Pause());
            }

            public void SetLevel(int value)
            {
                level = value;
                EnableInClassList("upgrade-halo-off", level == 0);
                UpdatePulse();
                MarkDirtyRepaint();
            }

            private void UpdatePulse()
            {
                if (level > 0 && panel != null) pulse.Resume();
                else pulse.Pause();
            }

            private void DrawHalo(MeshGenerationContext context)
            {
                if (level == 0 || contentRect.width < 1 || contentRect.height < 1) return;
                var painter = context.painter2D;
                Vector2 center = contentRect.center;
                float radius = Mathf.Min(contentRect.width, contentRect.height) * 0.49f;
                float brightness = 0.9f + 0.1f * Mathf.Sin(Time.unscaledTime * 2f);
                Color color = WeaponUpgradeVisuals.ColorFor(level);
                // Layered translucent circles form soft light; the original sprite stays untouched above it.
                for (int ring = 0; ring < 16; ring++)
                {
                    float progress = ring / 15f;
                    float size = radius * Mathf.Lerp(1f, 0.25f, progress);
                    color.a = (0.012f + (0.075f + level * 0.012f) * progress) * brightness;
                    painter.fillColor = color;
                    painter.BeginPath();
                    painter.Arc(center, size, Angle.Degrees(0), Angle.Degrees(360));
                    painter.ClosePath();
                    painter.Fill();
                }
            }
        }
    }
}
