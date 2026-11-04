using System.Collections.Generic;
using PersistentWorld.Player;
using UnityEngine;

namespace PersistentWorld.Combat
{
    public enum ClassArtEffect { ArcherSpecial, WarriorWhirlwind, WarriorCombo, WarriorCharge, WarriorRelease, WarriorParry, HealerBolt, HealerHealingZone, HealerCleanse, HealerJudgment, HealerRevive }

    [DefaultExecutionOrder(200)]
    public sealed class ClassSheetEffect : MonoBehaviour
    {
        private readonly struct Sheet
        {
            public readonly int Columns, Rows;
            public readonly float Ppu, SourceAngle;
            public readonly Vector2 Pivot;
            public readonly bool Ground;
            public Sheet(int columns, int rows, float ppu, float sourceAngle = 0, Vector2? pivot = null, bool ground = false)
            { Columns = columns; Rows = rows; Ppu = ppu; SourceAngle = sourceAngle; Pivot = pivot ?? new Vector2(.5f, .5f); Ground = ground; }
        }
        private static readonly Dictionary<ClassArtEffect, Sprite[]> Cache = new Dictionary<ClassArtEffect, Sprite[]>();
        private const float FrameDuration = .08f;
        private Transform follow;
        private bool hasFollow, loop, liveAim;
        private Vector2 followOffset;
        private float startedAt, endsAt;
        private SpriteRenderer visual;
        public ClassArtEffect Id { get; private set; }
        public int FrameIndex { get; private set; }
        public Transform Owner => follow;
        public bool IsSpecial => Id == ClassArtEffect.ArcherSpecial || Id == ClassArtEffect.WarriorParry;

        private static Sheet Definition(ClassArtEffect id) => id switch
        {
            ClassArtEffect.ArcherSpecial => new Sheet(3, 3, 140),
            ClassArtEffect.WarriorWhirlwind => new Sheet(4, 4, 48, ground: true),
            ClassArtEffect.WarriorCombo => new Sheet(4, 2, 100),
            ClassArtEffect.WarriorCharge => new Sheet(4, 3, 150, -90),
            ClassArtEffect.WarriorRelease => new Sheet(2, 2, 70, -90),
            ClassArtEffect.WarriorParry => new Sheet(2, 2, 160, 180),
            ClassArtEffect.HealerBolt => new Sheet(3, 1, 300, 90, new Vector2(.5f, .8f)),
            ClassArtEffect.HealerHealingZone => new Sheet(4, 2, 64, ground: true),
            ClassArtEffect.HealerCleanse => new Sheet(4, 4, 70, ground: true),
            ClassArtEffect.HealerJudgment => new Sheet(4, 3, 128, 0, new Vector2(.08f, .5f)),
            _ => new Sheet(4, 4, 100, ground: true)
        };

        public static Sprite[] Frames(ClassArtEffect id)
        {
            if (Cache.TryGetValue(id, out Sprite[] frames)) return frames;
            Texture2D texture = Resources.Load<Texture2D>("Effects/" + id);
            Sheet spec = Definition(id);
            if (texture == null || texture.width % spec.Columns != 0 || texture.height % spec.Rows != 0) return null;
            int width = texture.width / spec.Columns, height = texture.height / spec.Rows;
            frames = new Sprite[spec.Columns * spec.Rows];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = Sprite.Create(texture, new Rect(i % spec.Columns * width, (spec.Rows - 1 - i / spec.Columns) * height, width, height), spec.Pivot, spec.Ppu, 0, SpriteMeshType.FullRect);
                frames[i].name = id + "_" + i.ToString("00");
            }
            Cache[id] = frames;
            return frames;
        }

        public static ClassSheetEffect Play(ClassArtEffect id, Vector2 position, float lifetime = 0, bool repeat = false, Transform owner = null, Vector2? aim = null, bool followAim = false, float scale = 1)
        {
            Sprite[] frames = Frames(id);
            if (frames == null) return null;
            GameObject obj = new GameObject(id + " Animation");
            obj.transform.position = position;
            obj.transform.localScale = Vector3.one * scale;
            if (aim.HasValue) obj.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.Value.y, aim.Value.x) * Mathf.Rad2Deg - Definition(id).SourceAngle);
            ClassSheetEffect effect = obj.AddComponent<ClassSheetEffect>();
            effect.Id = id; effect.follow = owner; effect.hasFollow = owner != null; effect.liveAim = followAim;
            effect.followOffset = owner != null ? position - (Vector2)owner.position : Vector2.zero;
            effect.startedAt = Time.time; effect.loop = repeat;
            effect.endsAt = Time.time + (lifetime > 0 ? lifetime : repeat ? float.PositiveInfinity : frames.Length * FrameDuration);
            effect.visual = obj.AddComponent<SpriteRenderer>();
            effect.visual.sprite = frames[0];
            effect.Update();
            return effect;
        }

        public static bool AttachProjectile(Projectile projectile, ClassArtEffect id)
        {
            ClassSheetEffect effect = Play(id, projectile.transform.position, repeat: true, owner: projectile.transform, aim: projectile.transform.right);
            if (effect == null) return false;
            projectile.GetComponent<SpriteRenderer>().enabled = false;
            projectile.transform.localScale = Vector3.one;
            effect.transform.SetParent(projectile.transform, true);
            if (projectile.UpgradeLevel > 0) effect.visual.color = WeaponUpgradeVisuals.ColorFor(projectile.UpgradeLevel);
            return true;
        }

        public void SetBeam(Vector2 origin, Vector2 direction, float length)
        {
            transform.position = origin;
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            transform.localScale = new Vector3(length / (visual.sprite.rect.width / visual.sprite.pixelsPerUnit), 1, 1);
        }

        public void SetAimPose(Vector2 position, Vector2 direction)
        {
            transform.position = position;
            if (follow != null) followOffset = position - (Vector2)follow.position;
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - Definition(Id).SourceAngle);
        }

        private void Update()
        {
            if (Time.time >= endsAt || (hasFollow && (follow == null || !follow.gameObject.activeInHierarchy))) { Destroy(gameObject); return; }
            if (hasFollow)
            {
                Health health = follow.GetComponent<Health>();
                if (health != null && health.IsDead) { Destroy(gameObject); return; }
                transform.position = (Vector2)follow.position + followOffset;
                if (IsSpecial)
                {
                    PlayerSkills skills = follow.GetComponent<PlayerSkills>();
                    CharacterClass expected = Id == ClassArtEffect.ArcherSpecial ? CharacterClass.Archer : CharacterClass.Warrior;
                    if (skills == null || !skills.isActiveAndEnabled || !skills.SpecialActive || follow.GetComponent<SkillLoadout>().Class != expected) { Destroy(gameObject); return; }
                }
                if (liveAim && follow.GetComponent<ArcherController>() is ArcherController controller)
                    transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(controller.AimDirection.y, controller.AimDirection.x) * Mathf.Rad2Deg - Definition(Id).SourceAngle);
            }
            Sprite[] frames = Frames(Id);
            FrameIndex = Mathf.FloorToInt((Time.time - startedAt) / FrameDuration);
            if (!loop && FrameIndex >= frames.Length) { Destroy(gameObject); return; }
            FrameIndex %= frames.Length;
            visual.sprite = frames[FrameIndex];
            visual.sortingOrder = (Definition(Id).Ground ? 999 : 1100) - Mathf.RoundToInt(transform.position.y * 100);
            UpdateArcherBackdrop();
        }

        private void LateUpdate() => UpdateArcherBackdrop();

        private void UpdateArcherBackdrop()
        {
            if (Id == ClassArtEffect.WarriorWhirlwind && follow != null)
            {
                SpriteRenderer body = follow.GetComponent<SpriteRenderer>();
                if (body != null) transform.position = body.bounds.center;
            }
            if (Id != ClassArtEffect.ArcherSpecial || follow == null || visual == null) return;
            SpriteRenderer actor = follow.GetComponent<SpriteRenderer>();
            if (actor == null) return;
            // Follow the visible body, rather than the feet, after animation and depth sorting update.
            transform.position = actor.bounds.center;
            transform.rotation = Quaternion.identity;
            visual.sortingLayerID = actor.sortingLayerID;
            visual.sortingOrder = actor.sortingOrder - 1;
        }
    }
}
