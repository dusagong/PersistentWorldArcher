using PersistentWorld.Items;
using PersistentWorld.Player;
using PersistentWorld.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace PersistentWorld.UI
{
    [DisallowMultipleComponent]
    public sealed class CombatHud : MonoBehaviour
    {
        private ArcherController player;
        private PanelSettings settings;
        private UIDocument document;
        private Font font;
        private VisualElement screen, panel, hpFill, mpFill;
        private Label hpText, mpText, skillTooltip;
        private readonly SkillView[] slots = new SkillView[5];
        private IManaSource manaSource;

        public void BindManaSource(IManaSource source) => manaSource = source;

        public float TopScreenY => panel != null && screen.resolvedStyle.display != DisplayStyle.None &&
            document.rootVisualElement.worldBound.height > 0
            ? panel.worldBound.y / document.rootVisualElement.worldBound.height * Screen.height : Screen.height;

        public void GetGuideRects(Vector2 viewport, out Rect charge, out Rect interaction, out Rect dialogue)
        {
            float bottom = panel != null && screen.worldBound.height > 0
                ? panel.worldBound.yMin / screen.worldBound.height * viewport.y - 12 : viewport.y - 156;
            charge = GuideRect(viewport.x, bottom, 360, 36);
            if (player != null && player.Skills.IsHolding) bottom -= 48;
            interaction = GuideRect(viewport.x, bottom, 340, 36);
            if (player != null && !string.IsNullOrEmpty(player.NearbyPrompt)) bottom -= 48;
            dialogue = GuideRect(viewport.x, bottom, 540, 80);
        }

        private static Rect GuideRect(float viewportWidth, float bottom, float width, float height)
        {
            width = Mathf.Min(width, Mathf.Max(1, viewportWidth - 16));
            return new Rect((viewportWidth - width) / 2, bottom - height, width, height);
        }

        private void OnEnable()
        {
            var tree = Resources.Load<VisualTreeAsset>("UI/CombatHud");
            var template = Resources.Load<PanelSettings>("UI/BlacksmithPanel");
            if (tree == null || template == null) { Debug.LogError("Combat HUD assets are missing."); return; }
            settings = Instantiate(template);
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.sortingOrder = 90;
            var ui = new GameObject("Combat HUD UI");
            ui.transform.SetParent(transform, false);
            ui.SetActive(false);
            document = ui.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.visualTreeAsset = tree;
            ui.SetActive(true);
            var root = document.rootVisualElement;
            root.AddToClassList("combat-document");
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 16);
            root.style.unityFont = font;
            screen = root.Q("combat-screen");
            panel = root.Q("combat-panel");
            hpFill = root.Q("hp-fill"); mpFill = root.Q("mp-fill");
            hpText = root.Q<Label>("hp-text"); mpText = root.Q<Label>("mp-text");
            skillTooltip = root.Q<Label>("combat-tooltip");
            var row = root.Q("combat-skills");
            string[] keys = { "Space", "Q", "E", "좌클릭", "Shift" };
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new SkillView(keys[i], i);
                row.Add(slots[i].Root);
            }
            root.Query<VisualElement>().ForEach(element =>
            { element.pickingMode = PickingMode.Ignore; element.focusable = false; });
            root.pickingMode = PickingMode.Ignore;
            root.focusable = false;
            screen.style.display = DisplayStyle.None;
        }

        private void LateUpdate()
        {
            if (screen == null) return;
            int width = settings.targetTexture != null ? settings.targetTexture.width : Screen.width;
            int height = settings.targetTexture != null ? settings.targetTexture.height : Screen.height;
            if (player == null) player = FindFirstObjectByType<ArcherController>();
            bool visible = player != null && !player.MenuOpen && !ItemWindows.BlocksGameplay;
            screen.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) { skillTooltip.style.display = DisplayStyle.None; return; }
            float preferredScale = Mathf.Clamp(Mathf.Min(width / 1280f, height / 720f), 1, 1.5f);
            settings.scale = Mathf.Max(.1f, Mathf.Min(preferredScale, (width - 32) / 328f, (height - 24) / 144f));
            hpText.text = $"{player.Health.Current}/{player.Health.Maximum}";
            SetFill(hpFill, player.Health.Current, player.Health.Maximum);
            if (manaSource is Object owner && owner == null) manaSource = null;
            IManaSource source = manaSource ?? player.Mana;
            bool hasMana = source != null && source.Maximum > 0 &&
                !float.IsNaN(source.Maximum) && !float.IsInfinity(source.Maximum) &&
                !float.IsNaN(source.Current) && !float.IsInfinity(source.Current);
            mpText.text = hasMana ? $"{source.Current:0.#}/{source.Maximum:0.#}" : "100/100";
            SetFill(mpFill, hasMana ? source.Current : 100, hasMana ? source.Maximum : 100);
            var skills = player.Skills;
            var loadout = player.Loadout;
            bool alive = !player.Health.IsDead;
            slots[0].Set(SkillGlyph.Dodge, "회피", player.BackstepRemaining, player.BackstepCooldown,
                player.IsDashing, alive && (player.IsDashing || player.Mana.CanAfford(PrototypeManaBalance.DodgeCost)), false, false);
            SetSkill(1, loadout.Slots[0], false);
            SetSkill(2, loadout.Slots[1], false);
            SkillGlyph special = loadout.Class == CharacterClass.Warrior ? SkillGlyph.Parry :
                loadout.Class == CharacterClass.Healer ? SkillGlyph.Revive : SkillGlyph.DoubleShot;
            string specialName = loadout.Class == CharacterClass.Warrior ? "패링" :
                loadout.Class == CharacterClass.Healer ? "부활" : "일석이조";
            slots[3].Set(special, specialName, skills.SpecialRemaining, skills.SpecialCooldownDuration,
                skills.SpecialActive, alive && PlayerSkills.SpecialSkillsEnabled &&
                (skills.SpecialActive || player.Mana.CanAfford(PrototypeManaBalance.SpecialCost(loadout.Class))) &&
                (loadout.Class != CharacterClass.Warrior || player.HasWeapon), false, false);
            slots[3].SetMouseSequence(loadout.Class == CharacterClass.Archer, skills.SpecialActive);
            slots[3].SetEffect(skills.SpecialActiveRemaining, skills.SpecialActiveDuration);
            SetSkill(4, loadout.Slots[2], !loadout.BossLearned);
            Vector3 mouse = Input.mousePosition;
            UpdateSkillTooltip(new Vector2(mouse.x * screen.worldBound.width / Mathf.Max(1, Screen.width),
                (Screen.height - mouse.y) * screen.worldBound.height / Mathf.Max(1, Screen.height)));
        }

        private void UpdateSkillTooltip(Vector2 position)
        {
            skillTooltip.style.display = DisplayStyle.None;
            if (player == null || screen.style.display == DisplayStyle.None || screen.resolvedStyle.display == DisplayStyle.None) return;
            foreach (SkillView slot in slots)
            {
                if (!slot.Root.worldBound.Contains(position)) continue;
                skillTooltip.text = slot.Hint;
                float height = slot.Hint.Contains("\n") ? 48 : 32;
                skillTooltip.style.height = height;
                skillTooltip.style.left = Mathf.Clamp(slot.Root.worldBound.center.x - 110, 8, Mathf.Max(8, screen.worldBound.width - 228));
                float top = panel.worldBound.yMin;
                if (!player.Health.IsDead)
                {
                    Vector2 viewport = settings.targetTexture != null ?
                        new Vector2(settings.targetTexture.width, settings.targetTexture.height) : new Vector2(Screen.width, Screen.height);
                    GetGuideRects(viewport, out Rect charge, out Rect interaction, out Rect dialogue);
                    float ratio = screen.worldBound.height / viewport.y;
                    if (player.Skills.IsHolding) top = Mathf.Min(top, charge.yMin * ratio);
                    if (!string.IsNullOrEmpty(player.NearbyPrompt)) top = Mathf.Min(top, interaction.yMin * ratio);
                    if (!string.IsNullOrEmpty(DialogueState.Line)) top = Mathf.Min(top, dialogue.yMin * ratio);
                }
                skillTooltip.style.top = Mathf.Max(8, top - height - 8);
                skillTooltip.style.display = DisplayStyle.Flex;
                return;
            }
        }

        private void SetSkill(int index, SkillId id, bool locked)
        {
            bool available = !player.Health.IsDead && (player.HasWeapon || id == SkillId.HealingZone || id == SkillId.Cleanse);
            available &= player.Skills.IsUsingSkill(id) || player.Mana.CanAfford(PrototypeManaBalance.Cost(id));
            string title = locked ? "보스 스킬" : id == SkillId.None ? "미장착" : id == SkillId.BossBlast ? "전장 심판" : SkillCatalog.Name(id);
            slots[index].Set(Glyph(id), title, player.Skills.Cooldown(id),
                player.Skills.CooldownDuration(id), player.Skills.IsUsingSkill(id), available, locked, id == SkillId.None);
        }

        private static SkillGlyph Glyph(SkillId id) => id switch
        {
            SkillId.Repulse => SkillGlyph.Repulse, SkillId.ArrowRain => SkillGlyph.ArrowRain,
            SkillId.Snipe => SkillGlyph.Snipe, SkillId.Whirlwind => SkillGlyph.Whirlwind,
            SkillId.ComboSlash => SkillGlyph.ComboSlash, SkillId.Excalibur => SkillGlyph.Excalibur,
            SkillId.HealingZone => SkillGlyph.HealingZone, SkillId.Cleanse => SkillGlyph.Cleanse,
            SkillId.Judgment => SkillGlyph.Judgment, SkillId.BossBlast => SkillGlyph.Boss,
            _ => SkillGlyph.Empty
        };

        private static void SetFill(VisualElement fill, float current, float maximum) =>
            fill.style.width = Length.Percent(maximum > 0 ? Mathf.Clamp01(current / maximum) * 100 : 0);

        private void OnDisable()
        {
            if (document != null) Destroy(document.gameObject);
            if (settings != null) Destroy(settings);
            if (font != null) Destroy(font);
            document = null; settings = null; font = null; screen = null; panel = null;
            skillTooltip = null;
            player = null; manaSource = null;
        }

        private sealed class SkillView
        {
            public readonly VisualElement Root = new VisualElement();
            private readonly SkillCooldownIcon icon = new SkillCooldownIcon();
            private readonly Label countdown = new Label();
            private string title, usage = "";
            public string Hint => title + usage;
            private readonly VisualElement mouseShortcut;
            private readonly MouseButtonIcon leftMouse, rightMouse;
            private readonly Label sequence;
            private readonly VisualElement effect, effectFill;
            public SkillView(string key, int index)
            {
                Root.name = "combat-slot-" + index;
                Root.AddToClassList("combat-slot");
                countdown.AddToClassList("combat-cooldown");
                Root.Add(icon); Root.Add(countdown);
                if (index == 3)
                {
                    mouseShortcut = new VisualElement();
                    mouseShortcut.AddToClassList("combat-mouse-shortcut");
                    leftMouse = new MouseButtonIcon(false);
                    rightMouse = new MouseButtonIcon(true);
                    sequence = new Label("›");
                    sequence.AddToClassList("combat-mouse-sequence");
                    mouseShortcut.Add(leftMouse); mouseShortcut.Add(sequence); mouseShortcut.Add(rightMouse);
                    Root.Add(mouseShortcut);
                    effect = new VisualElement();
                    effect.AddToClassList("combat-effect");
                    var effectLabel = new Label("효과"); effectLabel.AddToClassList("combat-effect-label");
                    var track = new VisualElement(); track.AddToClassList("combat-effect-track");
                    effectFill = new VisualElement(); effectFill.AddToClassList("combat-effect-fill");
                    track.Add(effectFill); effect.Add(effectLabel); effect.Add(track); Root.Add(effect);
                }
                else
                {
                    var shortcut = new Label(key);
                    shortcut.AddToClassList("combat-key");
                    Root.Add(shortcut);
                }
            }

            public void SetMouseSequence(bool archer, bool active)
            {
                if (mouseShortcut == null) return;
                sequence.style.display = rightMouse.style.display = archer ? DisplayStyle.Flex : DisplayStyle.None;
                leftMouse.SetEmphasis(!archer || !active);
                rightMouse.SetEmphasis(active);
                usage = archer ? "\n좌클릭 버프 → 우클릭 2연사" : "";
            }

            public void SetEffect(float remaining, float duration)
            {
                if (effect == null) return;
                effect.style.display = remaining > 0 && duration > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                SetFill(effectFill, remaining, duration);
            }

            public void Set(SkillGlyph glyph, string name, float remaining, float duration,
                bool active, bool available, bool locked, bool empty)
            {
                title = locked ? name + " · 미습득" : name;
                bool cooling = !locked && !empty && remaining > 0;
                countdown.text = cooling ? Mathf.CeilToInt(remaining).ToString() : "";
                float fraction = remaining <= 0 ? 1 : duration > 0 ? Mathf.Clamp01(1 - remaining / duration) : 0;
                icon.Set(locked ? SkillGlyph.Locked : glyph, locked || empty ? 0 : fraction,
                    active && !locked && !empty, available && !locked && !empty, cooling);
                Root.EnableInClassList("combat-cooling", cooling);
                Root.EnableInClassList("combat-active", active && !locked && !empty);
                Root.EnableInClassList("combat-locked", locked);
                Root.EnableInClassList("combat-empty", !locked && empty);
                Root.EnableInClassList("combat-unavailable", !available || locked || empty);
            }
        }
    }
}
