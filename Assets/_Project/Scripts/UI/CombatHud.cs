using PersistentWorld.Items;
using PersistentWorld.Networking;
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
        private NetworkGameWorld onlineWorld;
        private NetworkPartyHud onlineMenu;
        private PanelSettings settings;
        private UIDocument document;
        private Font font;
        private VisualElement screen, panel, vitals, skillRow, hpFill, mpFill;
        private Label hpText, mpText, skillTooltip;
        private readonly SkillView[] slots = new SkillView[5];
        private VisualElement potionRow;
        private readonly Image[] potionIcons = new Image[2];
        private readonly Label[] potionTimers = new Label[2], potionCounts = new Label[2];
        private readonly string[] shownPotions = new string[2];
        private IManaSource manaSource;

        public void BindManaSource(IManaSource source) => manaSource = source;
        internal void BindOnline(NetworkGameWorld world, NetworkPartyHud menu)
        {
            onlineWorld = world;
            onlineMenu = menu;
            menu.BindCombatHud(this);
            if (skillRow != null) AddPotionRow();
        }

        public float TopScreenY => panel != null && screen.resolvedStyle.display != DisplayStyle.None &&
            document.rootVisualElement.worldBound.height > 0
            ? Mathf.Min(panel.worldBound.y, potionRow?.worldBound.y ?? panel.worldBound.y) /
                document.rootVisualElement.worldBound.height * Screen.height : Screen.height;

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
            vitals = root.Q("combat-vitals");
            skillRow = root.Q("combat-skills");
            hpFill = root.Q("hp-fill"); mpFill = root.Q("mp-fill");
            hpText = root.Q<Label>("hp-text"); mpText = root.Q<Label>("mp-text");
            skillTooltip = root.Q<Label>("combat-tooltip");
            string[] keys = { "Space", "Q", "E", "좌클릭", "Shift" };
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new SkillView(keys[i], i);
                skillRow.Add(slots[i].Root);
            }
            root.Query<VisualElement>().ForEach(element =>
            { element.pickingMode = PickingMode.Ignore; element.focusable = false; });
            root.pickingMode = PickingMode.Ignore;
            root.focusable = false;
            screen.style.display = DisplayStyle.None;
            if (onlineWorld != null) AddPotionRow();
        }

        private void AddPotionRow()
        {
            potionRow = new VisualElement { pickingMode = PickingMode.Ignore };
            potionRow.AddToClassList("combat-potions");
            skillRow.Add(potionRow);
            for (int i = 0; i < potionIcons.Length; i++)
            {
                var cell = new VisualElement { pickingMode = PickingMode.Ignore };
                cell.AddToClassList("combat-potion");
                var key = new Label((i + 1).ToString()) { pickingMode = PickingMode.Ignore };
                key.AddToClassList("combat-key");
                potionIcons[i] = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                potionIcons[i].AddToClassList("combat-skill-art");
                potionTimers[i] = new Label { pickingMode = PickingMode.Ignore };
                potionTimers[i].AddToClassList("combat-cooldown");
                potionCounts[i] = new Label { pickingMode = PickingMode.Ignore };
                potionCounts[i].AddToClassList("combat-potion-count");
                cell.Add(potionIcons[i]); cell.Add(key); cell.Add(potionTimers[i]); cell.Add(potionCounts[i]);
                potionRow.Add(cell);
                shownPotions[i] = null;
            }
        }

        private void LateUpdate()
        {
            if (screen == null) return;
            int width = settings.targetTexture != null ? settings.targetTexture.width : Screen.width;
            int height = settings.targetTexture != null ? settings.targetTexture.height : Screen.height;
            PlayerFrame local = onlineWorld != null ? onlineWorld.Local : null;
            if (onlineWorld == null && player == null) player = FindFirstObjectByType<ArcherController>();
            bool visible = !ItemWindows.BlocksGameplay && (onlineWorld != null
                ? local != null && !onlineMenu.MenuOpen
                : player != null && !player.MenuOpen);
            screen.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) { skillTooltip.style.display = DisplayStyle.None; return; }
            float preferredScale = Mathf.Max(.5f, Mathf.Min(width / 1280f, height / 720f));
            settings.scale = Mathf.Max(.1f, Mathf.Min(preferredScale, width / 688f, (height - 24) / 154f));
            float viewportWidth = width / settings.scale;
            int columns = viewportWidth >= 960 ? 5 : 3;
            float rowHeight = columns == 5 ? 74 : 154;
            panel.style.height = rowHeight;
            vitals.style.left = (viewportWidth - 240) / 2;
            skillRow.style.width = columns == 5 ? 328 : 192;
            skillRow.style.height = rowHeight;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Root.style.left = i % columns * 68;
                slots[i].Root.style.top = i / columns * 80;
            }
            if (local != null) DrawOnline(local);
            else DrawOffline();
            Vector3 mouse = Input.mousePosition;
            UpdateSkillTooltip(new Vector2(mouse.x * screen.worldBound.width / Mathf.Max(1, Screen.width),
                (Screen.height - mouse.y) * screen.worldBound.height / Mathf.Max(1, Screen.height)));
        }

        private void DrawOffline()
        {
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
            slots[0].Details = $"공격을 피하며 이동합니다.\n쿨타임 {player.BackstepCooldown:0.#}초 · MP {PrototypeManaBalance.DodgeCost}";
            SetSkill(1, loadout.Slots[0], false);
            SetSkill(2, loadout.Slots[1], false);
            DrawSpecial(loadout.Class, skills.SpecialRemaining, skills.SpecialCooldownDuration,
                skills.SpecialActive, alive && PlayerSkills.SpecialSkillsEnabled &&
                (skills.SpecialActive || player.Mana.CanAfford(PrototypeManaBalance.SpecialCost(loadout.Class))) &&
                (loadout.Class != CharacterClass.Warrior || player.HasWeapon), skills.SpecialActiveRemaining, skills.SpecialActiveDuration);
            SetSkill(4, loadout.Slots[2], !loadout.BossLearned);
        }

        private void DrawOnline(PlayerFrame local)
        {
            DrawPotions();
            hpText.text = $"{local.hp}/{local.maximum}";
            SetFill(hpFill, local.hp, local.maximum);
            mpText.text = $"{local.mp:0.#}/{PrototypeManaBalance.Maximum:0.#}";
            SetFill(mpFill, local.mp, PrototypeManaBalance.Maximum);
            bool alive = local.hp > 0;
            CharacterClass type = (CharacterClass)local.character;
            slots[0].Set(SkillGlyph.Dodge, "회피", local.dodge, local.dodgeDuration, local.dodging,
                alive && (local.dodging || local.mp >= PrototypeManaBalance.DodgeCost), false, false);
            slots[0].Details = $"공격을 피하며 이동합니다.\n쿨타임 {local.dodgeDuration:0.#}초 · MP {PrototypeManaBalance.DodgeCost}";
            for (int i = 0; i < 3; i++)
            {
                SkillId id = (SkillId)At(local.slots, i);
                bool active = At(local.activeSkills, i);
                bool available = alive && (local.hasWeapon || id == SkillId.HealingZone || id == SkillId.Cleanse) &&
                    (active || local.mp >= PrototypeManaBalance.Cost(id));
                DrawSkill(i == 2 ? 4 : i + 1, id, i == 2 && !local.bossLearned,
                    At(local.cooldowns, i), At(local.cooldownDurations, i), active, available);
            }
            DrawSpecial(type, local.special, local.specialDuration, local.specialActive,
                alive && local.specialEnabled && (local.specialActive || local.mp >= PrototypeManaBalance.SpecialCost(type)) &&
                (type != CharacterClass.Warrior || local.hasWeapon), local.specialEffect, local.specialEffectDuration);
        }

        private void DrawPotions()
        {
            if (potionRow == null) return;
            GameFrame frame = onlineWorld.Frame;
            for (int i = 0; i < potionIcons.Length; i++)
            {
                string id = At(frame.potionSlots, i);
                if (shownPotions[i] != id)
                {
                    shownPotions[i] = id;
                    potionIcons[i].sprite = ItemCatalog.TryGet(id, out var item) ? Resources.Load<Sprite>(item.IconPath) : null;
                }
                float remaining = At(frame.potionCooldowns, i);
                int count = string.IsNullOrEmpty(id) ? 0 : onlineWorld.ClientInventory?.Inventory.GetCount(id) ?? 0;
                potionIcons[i].style.opacity = remaining > 0 || count == 0 ? .45f : 1f;
                potionTimers[i].text = remaining > 0 ? Mathf.CeilToInt(remaining).ToString() : "";
                potionCounts[i].text = string.IsNullOrEmpty(id) ? "" : count.ToString();
            }
        }

        private static T At<T>(T[] values, int index) => values != null && index < values.Length ? values[index] : default;

        private void DrawSpecial(CharacterClass type, float remaining, float duration, bool active, bool available,
            float effectRemaining, float effectDuration)
        {
            SkillGlyph glyph = type == CharacterClass.Warrior ? SkillGlyph.Parry :
                type == CharacterClass.Healer ? SkillGlyph.Revive : SkillGlyph.DoubleShot;
            string name = type == CharacterClass.Warrior ? "패링" : type == CharacterClass.Healer ? "부활" : "일석이조";
            slots[3].Set(glyph, name, remaining, duration, active, available, false, false);
            slots[3].SetMouseSequence(type == CharacterClass.Archer, active);
            slots[3].SetEffect(effectRemaining, effectDuration);
            string description = type == CharacterClass.Warrior ? "전방 탄환을 1.2초간 반사합니다." :
                type == CharacterClass.Healer ? "가까운 사망 아군을 부활시킵니다." : "5초간 기본 화살을 두 발씩 발사합니다.";
            slots[3].Details = description + $"\n쿨타임 {(duration > 0 ? duration : type == CharacterClass.Healer ? 180 : type == CharacterClass.Warrior ? 4 : 10):0.#}초 · MP {PrototypeManaBalance.SpecialCost(type)}";
        }

        private void UpdateSkillTooltip(Vector2 position)
        {
            skillTooltip.style.display = DisplayStyle.None;
            if (screen.style.display == DisplayStyle.None || screen.resolvedStyle.display == DisplayStyle.None) return;
            foreach (SkillView slot in slots)
            {
                if (!slot.Root.worldBound.Contains(position)) continue;
                skillTooltip.text = slot.Hint;
                float height = 116;
                skillTooltip.style.height = height;
                skillTooltip.style.left = Mathf.Clamp(slot.Root.worldBound.center.x - 110, 8, Mathf.Max(8, screen.worldBound.width - 228));
                float top = Mathf.Min(panel.worldBound.yMin, potionRow?.worldBound.yMin ?? panel.worldBound.yMin);
                if (player != null && !player.Health.IsDead)
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
            if (onlineWorld == null || potionRow == null) return;
            for (int i = 0; potionIcons != null && i < potionIcons.Length; i++)
            {
                VisualElement element = potionIcons[i].parent;
                if (!element.worldBound.Contains(position)) continue;
                string id = At(onlineWorld.Frame.potionSlots, i);
                skillTooltip.text = string.IsNullOrEmpty(id) ? "인벤토리(I)에서 포션을 장착하세요." :
                    (id == PersistentWorld.World.MonsterDropTable.HealthPotion ? "체력 포션 · HP 30 회복" : "마나 포션 · MP 30 회복") + "\n재사용 10초 · MP 소모 없음";
                skillTooltip.style.height = 72;
                skillTooltip.style.left = Mathf.Clamp(element.worldBound.x - 160, 8, Mathf.Max(8, screen.worldBound.width - 228));
                skillTooltip.style.top = Mathf.Max(8, element.worldBound.y - 80);
                skillTooltip.style.display = DisplayStyle.Flex; return;
            }
        }

        private void SetSkill(int index, SkillId id, bool locked)
        {
            bool available = !player.Health.IsDead && (player.HasWeapon || id == SkillId.HealingZone || id == SkillId.Cleanse);
            available &= player.Skills.IsUsingSkill(id) || player.Mana.CanAfford(PrototypeManaBalance.Cost(id));
            DrawSkill(index, id, locked, player.Skills.Cooldown(id), player.Skills.CooldownDuration(id),
                player.Skills.IsUsingSkill(id), available);
        }

        private void DrawSkill(int index, SkillId id, bool locked, float remaining, float duration, bool active, bool available)
        {
            string title = locked ? "보스 스킬" : id == SkillId.None ? "미장착" : id == SkillId.BossBlast ? "전장 심판" : SkillCatalog.Name(id);
            slots[index].Set(Glyph(id), title, remaining, duration, active, available, locked, id == SkillId.None);
            slots[index].Details = id == SkillId.None ? "마을에서 K키로 스킬을 장착하세요." :
                SkillCatalog.Description(id) + $"\n쿨타임 {(duration > 0 ? duration : SkillCatalog.Cooldown(id)):0.#}초 · MP {PrototypeManaBalance.Cost(id)}";
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
            vitals = null; skillRow = null;
            skillTooltip = null;
            player = null; manaSource = null;
        }

        private sealed class SkillView
        {
            public readonly VisualElement Root = new VisualElement();
            private readonly SkillCooldownIcon icon = new SkillCooldownIcon();
            private readonly Label countdown = new Label();
            private string title, usage = "";
            public string Details = "";
            public string Hint => title + usage + "\n" + Details;
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
