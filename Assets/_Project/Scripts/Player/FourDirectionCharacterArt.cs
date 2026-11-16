using System;
using System.Collections.Generic;
using UnityEngine;

namespace PersistentWorld.Player
{
    public enum CharacterMotion { Idle, Walk, Dodge, BasicAttack, SkillOne, SkillTwo, SkillThree, Special, Boss }

    // Presentation only. These clips never move actors or schedule damage.
    public static class FourDirectionCharacterArt
    {
        private sealed class Atlas
        {
            public Sprite[] Frames;
            public int Columns;
        }
        [Serializable] private sealed class FrameLayouts { public SheetLayout[] sheets; }
        [Serializable] private sealed class FrameBounds { public int x, y, width, height; }
        [Serializable] private sealed class SheetLayout
        {
            public string name;
            public int width, height, columns, rows;
            public FrameBounds[] frames;
        }
        private static readonly Dictionary<(CharacterClass, string), Atlas> Atlases = new Dictionary<(CharacterClass, string), Atlas>();
        private static readonly Dictionary<CharacterClass, float> Heights = new Dictionary<CharacterClass, float>();
        private static Dictionary<string, SheetLayout> layouts;

        private static SheetLayout Layout(string sheet)
        {
            if (layouts == null)
            {
                layouts = new Dictionary<string, SheetLayout>();
                TextAsset data = Resources.Load<TextAsset>("Characters/FourDirection/FrameLayout");
                FrameLayouts file = data != null ? JsonUtility.FromJson<FrameLayouts>(data.text) : null;
                if (file?.sheets != null)
                    foreach (SheetLayout entry in file.sheets)
                        if (entry != null && !string.IsNullOrEmpty(entry.name)) layouts[entry.name] = entry;
            }
            return layouts.TryGetValue(sheet, out SheetLayout layout) ? layout : null;
        }

        public static int Face(Vector2 direction, int previous)
        {
            if (direction.sqrMagnitude < .001f) return previous;
            // Keep the previous axis near diagonals instead of flickering between two views.
            const float margin = .12f;
            float x = Mathf.Abs(direction.x), y = Mathf.Abs(direction.y);
            if ((previous == 1 || previous == 3) && y <= x + margin) return direction.x < 0 ? 1 : 3;
            if ((previous == 0 || previous == 2) && x <= y + margin) return direction.y > 0 ? 2 : 0;
            return x > y ? direction.x < 0 ? 1 : 3 : direction.y > 0 ? 2 : 0;
        }

        public static CharacterMotion ForSkill(SkillId skill)
        {
            switch (skill)
            {
                case SkillId.Repulse: case SkillId.Whirlwind: case SkillId.HealingZone: return CharacterMotion.SkillOne;
                case SkillId.ArrowRain: case SkillId.ComboSlash: case SkillId.Cleanse: return CharacterMotion.SkillTwo;
                case SkillId.Snipe: case SkillId.Excalibur: case SkillId.Judgment: return CharacterMotion.SkillThree;
                case SkillId.BossBlast: return CharacterMotion.Boss;
                default: return CharacterMotion.Idle;
            }
        }

        public static float Duration(CharacterClass character, CharacterMotion motion)
        {
            if (motion == CharacterMotion.BasicAttack)
                return character == CharacterClass.Warrior ? .32f : character == CharacterClass.Archer ? .30f : .48f;
            if (motion == CharacterMotion.Dodge) return .16f;
            if (motion == CharacterMotion.Special) return character == CharacterClass.Warrior ? 1.2f : .48f;
            if (motion == CharacterMotion.SkillOne && character == CharacterClass.Warrior) return .64f;
            return .48f;
        }

        public static Sprite Frame(CharacterClass character, CharacterMotion motion, int direction, float elapsed, bool holding = false)
        {
            string sheet = motion <= CharacterMotion.Dodge ? "Locomotion" : motion >= CharacterMotion.Special ? "Special" : "Actions";
            Atlas atlas = Load(character, sheet);
            if (atlas == null) return null;
            int row = Mathf.Clamp(direction, 0, 3), column;
            if (motion == CharacterMotion.Idle) column = 0;
            else if (motion == CharacterMotion.Walk) column = 1 + Mathf.FloorToInt(Mathf.Max(0, elapsed) * 10) % 4;
            else
            {
                float duration = Duration(character, motion);
                int phase = holding ? 1 : Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0, elapsed) / duration * 4), 0, 3);
                if (motion == CharacterMotion.Dodge) column = 5 + phase;
                else if (motion == CharacterMotion.BasicAttack) column = phase;
                else if (motion == CharacterMotion.SkillOne) column = 4 + phase;
                else if (motion == CharacterMotion.SkillTwo) { row += 4; column = phase; }
                else if (motion == CharacterMotion.SkillThree) { row += 4; column = 4 + phase; }
                else column = (motion == CharacterMotion.Boss ? 4 : 0) + phase;
            }
            return atlas.Frames[row * atlas.Columns + column];
        }

        private static Atlas Load(CharacterClass character, string name)
        {
            var key = (character, name);
            if (Atlases.TryGetValue(key, out Atlas cached)) return cached;
            Texture2D texture = Resources.Load<Texture2D>("Characters/FourDirection/" + character + name);
            int columns = name == "Locomotion" ? 9 : 8, rows = name == "Actions" ? 8 : 4;
            SheetLayout layout = Layout(character + name);
            if (texture == null || !texture.isReadable || layout == null || layout.width != texture.width ||
                layout.height != texture.height || layout.columns != columns || layout.rows != rows ||
                layout.frames == null || layout.frames.Length != columns * rows)
            {
                // A missing optional sheet keeps the original character art available.
                return null;
            }
            if (!Heights.TryGetValue(character, out float originalHeight))
            {
                Sprite[] original = ArcherSpriteAnimator.RunFrames(character);
                originalHeight = original != null && original.Length > 0 ? original[0].bounds.size.y : 1.2f;
                Heights[character] = originalHeight;
            }
            Color32[] pixels = texture.GetPixels32();
            int referenceHeight = 0;
            int[] bottoms = new int[columns * rows];
            Rect[] cells = new Rect[columns * rows];
            for (int i = 0; i < bottoms.Length; i++)
            {
                // Authored bounds follow each complete character, not an assumed uniform grid.
                FrameBounds bounds = layout.frames[i];
                if (bounds == null) return null;
                Rect rect = new Rect(bounds.x, bounds.y, bounds.width, bounds.height);
                if (rect.width < 1 || rect.height < 1 || rect.xMin < 0 || rect.yMin < 0 ||
                    rect.xMax > texture.width || rect.yMax > texture.height) return null;
                int x = Mathf.RoundToInt(rect.x), y = Mathf.RoundToInt(rect.y);
                int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
                cells[i] = rect;
                int bottom = height, top = -1;
                for (int py = 0; py < height; py++)
                    for (int px = 0; px < width; px++)
                        if (pixels[(y + py) * texture.width + x + px].a >= 96)
                        { bottom = Mathf.Min(bottom, py); top = Mathf.Max(top, py); }
                bottoms[i] = top >= bottom ? bottom : 0;
                // Use the first (ready) pose in each direction, not an arm raised overhead.
                if (top >= bottom && i < columns * 4 && i % columns == 0)
                    referenceHeight = Mathf.Max(referenceHeight, top - bottom + 1);
            }
            // One scale for a whole atlas, never rescale individual animation frames.
            float ppu = Mathf.Max(1, referenceHeight) / Mathf.Max(.1f, originalHeight);
            var atlas = new Atlas { Columns = columns, Frames = new Sprite[columns * rows] };
            for (int i = 0; i < atlas.Frames.Length; i++)
            {
                Rect rect = cells[i];
                atlas.Frames[i] = Sprite.Create(texture, rect, new Vector2(.5f, bottoms[i] / rect.height), ppu, 0, SpriteMeshType.FullRect);
                atlas.Frames[i].name = character + "FourDirection" + name + "_" + i;
            }
            Atlases[key] = atlas;
            return atlas;
        }
    }
}
