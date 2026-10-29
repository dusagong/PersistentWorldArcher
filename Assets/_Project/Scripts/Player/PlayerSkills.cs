using System.Collections;
using System.Collections.Generic;
using PersistentWorld.Combat;
using PersistentWorld.Core;
using PersistentWorld.Items;
using PersistentWorld.UI;
using PersistentWorld.World;
using UnityEngine;

namespace PersistentWorld.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerSkills : MonoBehaviour
    {
        [SerializeField] private float maximumCharge = 1.8f;
        [SerializeField] private float skillRange = 6f;
        [SerializeField] private float reviveRange = 3f;
        public static bool SpecialSkillsEnabled { get; set; } = true;
        private static readonly Dictionary<string, float> Ready = new Dictionary<string, float>();
        private ArcherController controller;
        private Health health;
        private SkillLoadout loadout;
        private SkillIndicator preview;
        private SkillId held;
        private int heldSlot = -1;
        private float heldAt;
        private Vector2 target;
        private Vector2 heldAim;
        private float attackReadyAt;
        private float specialEndsAt;
        private int combo;
        private float comboEndsAt;
        private bool busy;
        private bool releasing;
        private CharacterClass previousClass;
        public bool MovementLocked => held == SkillId.Snipe || held == SkillId.Excalibur || held == SkillId.Judgment || releasing;
        public bool IsHolding => held != SkillId.None;
        public SkillId HoldingSkill => held;
        public float Charge => IsHolding ? Mathf.Clamp01((Time.time - heldAt) / maximumCharge) : 0;
        public bool SpecialActive => Time.time < specialEndsAt;
        public float SpecialRemaining => Ready.TryGetValue(loadout.Class + ":Special", out float ready) ? Mathf.Max(0, ready - Time.time) : 0;
        public int ComboStage => combo;

        private void Awake()
        {
            controller = GetComponent<ArcherController>();
            health = GetComponent<Health>();
            loadout = GetComponent<SkillLoadout>();
            previousClass = loadout.Class;
            loadout.Changed += OnLoadoutChanged;
        }

        private void OnDestroy() { if (loadout != null) loadout.Changed -= OnLoadoutChanged; }
        private void OnDisable() => Cancel();
        private string Key(SkillId id) => loadout.Class + ":" + id;
        public float Cooldown(SkillId id) => Ready.TryGetValue(Key(id), out float ready) ? Mathf.Max(0, ready - Time.time) : 0;
        private void SetCooldown(SkillId id, float seconds) => Ready[Key(id)] = Time.time + seconds;
        private bool CanAct => isActiveAndEnabled && controller.isActiveAndEnabled && health != null && !health.IsDead && !controller.IsDashing && !controller.MenuOpen && !ItemWindows.BlocksGameplay;
        private int Damage(float value) => Mathf.RoundToInt((value + controller.EquipmentBonuses.Attack) * (GetComponent<CombatStatus>()?.OutgoingMultiplier ?? 1));
        private void OnLoadoutChanged()
        {
            if (combo > 0) Ready[previousClass + ":" + SkillId.ComboSlash] = Time.time + 6;
            Cancel(); combo = 0; specialEndsAt = 0;
            previousClass = loadout.Class;
        }

        private void Update()
        {
            if (health.IsDead) { Cancel(); return; }
            if (combo > 0 && Time.time >= comboEndsAt) { combo = 0; SetCooldown(SkillId.ComboSlash, 6); }
            if (held == SkillId.Snipe || held == SkillId.Excalibur)
            {
                float range = Mathf.Lerp(held == SkillId.Snipe ? 8 : 2, held == SkillId.Snipe ? 15 : 5, Charge);
                if (held == SkillId.Excalibur) preview.Sector(transform.position, heldAim, range, 100);
                else preview.Beam((Vector2)transform.position + Vector2.up * .35f, (Vector2)transform.position + Vector2.up * .35f + heldAim * range, Mathf.Lerp(.06f, .2f, Charge));
                if (Time.time - heldAt >= maximumCharge) ReleaseSkill(heldSlot);
            }
            else if (held == SkillId.ArrowRain && preview != null) preview.Circle(target, 1.3f, .45f);
            else if (held == SkillId.Judgment && Time.time - heldAt >= 5) ReleaseSkill(heldSlot);
        }

        public void UpdateTarget(Vector2 mouseWorld)
        {
            target = (Vector2)transform.position + Vector2.ClampMagnitude(mouseWorld - (Vector2)transform.position, skillRange);
        }

        public bool BeginSkill(int slot, Vector2 mouseWorld)
        {
            if (!CanAct || busy || IsHolding || releasing || slot < 0 || slot > 2) return false;
            SkillId id = loadout.Slots[slot];
            if (id == SkillId.None || Cooldown(id) > 0) return false;
            if (!controller.HasWeapon && id != SkillId.HealingZone && id != SkillId.Cleanse) return false;
            Vector2 direction = controller.AimDirection;
            UpdateTarget(mouseWorld);
            switch (id)
            {
                case SkillId.Repulse:
                    SetCooldown(id, 4);
                    SkillOneEffect.Spawn((Vector2)transform.position + Vector2.up * .35f + direction * .3f, direction);
                    SectorDamage(transform.position, direction, 3, 90, Damage(20), 2);
                    break;
                case SkillId.ArrowRain:
                case SkillId.Snipe:
                case SkillId.Excalibur:
                case SkillId.Judgment:
                    held = id; heldSlot = slot; heldAt = Time.time; heldAim = direction;
                    preview = SkillIndicator.Create(id == SkillId.ArrowRain ? Color.yellow : Color.cyan);
                    if (id == SkillId.Judgment) { SetCooldown(id, 10); StartCoroutine(Judgment()); }
                    break;
                case SkillId.Whirlwind:
                    SetCooldown(id, 5); busy = true; StartCoroutine(Whirlwind()); break;
                case SkillId.ComboSlash:
                    if (combo == 0) comboEndsAt = Time.time + 4;
                    combo++;
                    SectorDamage(transform.position, direction, 1.6f + combo * .55f, 220, Damage(12 + combo * 4));
                    if (combo >= 3) { combo = 0; SetCooldown(id, 6); }
                    break;
                case SkillId.HealingZone:
                    SetCooldown(id, 8); HealingZone.Spawn(target, health, 5); break;
                case SkillId.Cleanse:
                    SetCooldown(id, 8);
                    foreach (Health ally in UnityEngine.Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
                        if (ally.Faction == Faction.Player && !ally.IsDead && Vector2.Distance(transform.position, ally.transform.position) <= 3) ally.GetComponent<CombatStatus>()?.Cleanse();
                    SkillIndicator.Create(Color.green, .4f).Circle(transform.position, 3, 3);
                    break;
                case SkillId.BossBlast:
                    if (!loadout.BossLearned) return false;
                    SetCooldown(id, 20);
                    Camera camera = Camera.main;
                    if (camera != null)
                        foreach (Health enemy in UnityEngine.Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
                        {
                            Vector3 point = camera.WorldToViewportPoint(enemy.transform.position);
                            if (enemy.Faction == Faction.Enemy && !enemy.IsDead && point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1)
                            {
                                enemy.TryDamage(Damage(60), Faction.Player, gameObject);
                                SkillIndicator.Create(Color.magenta, .35f).Circle(enemy.transform.position, .8f, .8f);
                            }
                        }
                    break;
                default: return false;
            }
            return true;
        }

        public bool ReleaseSkill(int slot)
        {
            if (!IsHolding || heldSlot != slot) return false;
            SkillId id = held;
            float charge = Charge;
            Vector2 direction = heldAim;
            ClearHold();
            if (!CanAct) return false;
            if (id == SkillId.ArrowRain)
            {
                SetCooldown(id, 8); ArrowRainEffect.Spawn(target, Damage(30), gameObject);
            }
            else if (id == SkillId.Snipe)
            {
                SetCooldown(id, 10); releasing = true;
                PowerShotEffect.PlayCast(transform, direction);
                StartCoroutine(ReleaseSnipe(direction, charge));
            }
            else if (id == SkillId.Excalibur)
            {
                SetCooldown(id, 10);
                SectorDamage(transform.position, direction, Mathf.Lerp(2, 5, charge), 100, Damage(Mathf.Lerp(25, 80, charge)));
            }
            return true;
        }

        private IEnumerator ReleaseSnipe(Vector2 direction, float charge)
        {
            yield return new WaitForSeconds(PowerShotEffect.ReleaseDelay);
            releasing = false;
            if (health.IsDead || !controller.HasWeapon || ItemWindows.BlocksGameplay) yield break;
            float range = Mathf.Lerp(8, 15, charge);
            Projectile arrow = Projectile.Spawn((Vector2)transform.position + Vector2.up * .35f + direction * .5f, direction, Damage(Mathf.Lerp(25, 80, charge)), Faction.Player, gameObject, Color.magenta, 12, range / 12);
            arrow.HitRadius = Mathf.Lerp(.09f, .32f, charge);
            PowerShotEffect.AttachFlight(arrow);
            arrow.transform.localScale = Vector3.one * Mathf.Lerp(.7f, 1.3f, charge);
        }

        private IEnumerator Whirlwind()
        {
            SkillIndicator windup = SkillIndicator.Create(Color.yellow, .5f);
            windup.Circle(transform.position, 2.5f, 2.5f);
            yield return new WaitForSeconds(.5f);
            busy = false;
            if (!health.IsDead) SectorDamage(transform.position, Vector2.right, 2.5f, 360, Damage(30));
        }

        private IEnumerator Judgment()
        {
            float nextTick = Time.time;
            while (held == SkillId.Judgment && !health.IsDead)
            {
                Vector2 origin = (Vector2)transform.position + Vector2.up * .35f;
                Vector2 direction = controller.AimDirection;
                float length = 8;
                foreach (RaycastHit2D hit in Physics2D.RaycastAll(origin, direction, length))
                    if (hit.collider.GetComponentInParent<WorldObstacle>() != null) length = Mathf.Min(length, hit.distance);
                preview.Beam(origin, origin + direction * length, .4f);
                if (Time.time >= nextTick)
                {
                    HashSet<Health> seen = new HashSet<Health>();
                    foreach (Collider2D collider in Physics2D.OverlapBoxAll(origin + direction * length / 2, new Vector2(length, .4f), Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg))
                    {
                        Health enemy = collider.GetComponentInParent<Health>();
                        if (enemy != null && enemy.Faction == Faction.Enemy && seen.Add(enemy)) enemy.TryDamage(Damage(4), Faction.Player, gameObject);
                    }
                    nextTick = Time.time + .1f;
                }
                yield return null;
            }
        }

        public bool TryBasicAttack()
        {
            if (!CanAct || !controller.HasWeapon || busy || IsHolding || releasing || Time.time < attackReadyAt) return false;
            Vector2 direction = controller.AimDirection;
            if (loadout.Class == CharacterClass.Warrior)
            {
                attackReadyAt = Time.time + .55f;
                SectorDamage(transform.position, direction, 1.8f, 120, Damage(12));
            }
            else if (loadout.Class == CharacterClass.Healer)
            {
                attackReadyAt = Time.time + .7f;
                Projectile.Spawn((Vector2)transform.position + Vector2.up * .35f + direction * .5f, direction, Damage(10), Faction.Player, gameObject, Color.cyan, 8);
            }
            else
            {
                attackReadyAt = Time.time + (SpecialActive ? .27f : .45f);
                BasicAttackEffect.PlayCast(transform, direction);
                StartCoroutine(BasicArrows(direction, SpecialActive));
            }
            return true;
        }

        private IEnumerator BasicArrows(Vector2 direction, bool doubled)
        {
            yield return new WaitForSeconds(BasicAttackEffect.ReleaseDelay);
            if (health.IsDead || !controller.HasWeapon || ItemWindows.BlocksGameplay) yield break;
            SpawnBasicArrow(direction);
            if (!doubled) yield break;
            yield return new WaitForSeconds(.08f);
            if (!health.IsDead && controller.HasWeapon && !ItemWindows.BlocksGameplay) SpawnBasicArrow(direction);
        }

        private void SpawnBasicArrow(Vector2 direction)
        {
            Projectile arrow = Projectile.Spawn((Vector2)transform.position + Vector2.up * .35f + direction * .5f, direction, Damage(10), Faction.Player, gameObject, Color.white);
            BasicAttackEffect.AttachArrow(arrow);
        }

        public bool TrySpecial()
        {
            if (!SpecialSkillsEnabled || !CanAct || busy || IsHolding || releasing || SpecialRemaining > 0) return false;
            if (loadout.Class == CharacterClass.Warrior && !controller.HasWeapon) return false;
            if (loadout.Class == CharacterClass.Healer)
            {
                Health nearest = null;
                float distance = reviveRange;
                foreach (Health ally in UnityEngine.Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
                {
                    float candidate = Vector2.Distance(transform.position, ally.transform.position);
                    if (ally != health && ally.Faction == Faction.Player && ally.IsDead && candidate <= distance) { nearest = ally; distance = candidate; }
                }
                if (nearest == null || !nearest.Revive(.7f, 3)) return false;
                Ready[loadout.Class + ":Special"] = Time.time + 15;
                SkillIndicator.Create(Color.green, .6f).Circle(nearest.transform.position, 1, 1);
            }
            else
            {
                Ready[loadout.Class + ":Special"] = Time.time + (loadout.Class == CharacterClass.Warrior ? 4 : 10);
                specialEndsAt = Time.time + (loadout.Class == CharacterClass.Warrior ? .7f : 5);
                if (loadout.Class == CharacterClass.Warrior) SkillIndicator.Create(Color.cyan, .7f).Sector(transform.position, controller.AimDirection, 1.7f, 140);
            }
            return true;
        }

        public bool TryParry(Projectile projectile)
        {
            if (loadout.Class != CharacterClass.Warrior || !SpecialActive || health.IsDead || projectile.SourceFaction != Faction.Enemy) return false;
            Vector2 delta = projectile.transform.position - transform.position;
            if (Vector2.Angle(controller.AimDirection, delta) > 70) return false;
            projectile.Reflect(gameObject);
            return true;
        }

        private void SectorDamage(Vector2 center, Vector2 direction, float radius, float angle, int damage, float knockback = 0)
        {
            SkillIndicator.Create(loadout.Class == CharacterClass.Warrior ? Color.yellow : Color.magenta, .22f).Sector(center, direction, radius, angle);
            HashSet<Health> seen = new HashSet<Health>();
            foreach (Collider2D collider in Physics2D.OverlapCircleAll(center, radius))
            {
                Health enemy = collider.GetComponentInParent<Health>();
                if (enemy == null || enemy.Faction != Faction.Enemy || enemy.IsDead || seen.Contains(enemy)) continue;
                Vector2 delta = collider.ClosestPoint(center) - center;
                if (angle < 360 && delta.sqrMagnitude > .01f && Vector2.Angle(direction, delta) > angle / 2) continue;
                seen.Add(enemy);
                if (!enemy.TryDamage(damage, Faction.Player, gameObject) || knockback <= 0) continue;
                CombatStatus status = enemy.GetComponent<CombatStatus>() ?? enemy.gameObject.AddComponent<CombatStatus>();
                status.Knockback(((Vector2)enemy.transform.position - center).normalized, knockback);
            }
        }

        private void ClearHold()
        {
            held = SkillId.None; heldSlot = -1;
            if (preview != null) Destroy(preview.gameObject);
            preview = null;
        }

        public void Cancel()
        {
            ClearHold(); StopAllCoroutines(); busy = releasing = false;
            foreach (BasicAttackEffect effect in UnityEngine.Object.FindObjectsByType<BasicAttackEffect>(FindObjectsSortMode.None))
                if (effect.Owner == transform) Destroy(effect.gameObject);
            foreach (PowerShotEffect effect in UnityEngine.Object.FindObjectsByType<PowerShotEffect>(FindObjectsSortMode.None))
                if (!effect.IsFlight && effect.Caster == transform) Destroy(effect.gameObject);
        }

        public void OnDeath()
        {
            if (combo > 0) SetCooldown(SkillId.ComboSlash, 6);
            Cancel(); specialEndsAt = 0; combo = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { Ready.Clear(); SpecialSkillsEnabled = true; }
    }
}
