using PersistentWorld.Core;
using PersistentWorld.Items;
using UnityEngine;

namespace PersistentWorld.Combat
{
    public sealed class Projectile : MonoBehaviour
    {
        private Vector2 direction;
        private float speed;
        private float remainingLife;
        private int damage;
        private Faction sourceFaction;
        private DamageContext damageContext;
        private GameObject owner;
        private bool initialized;
        public float HitRadius { get; set; } = .09f;
        public Faction SourceFaction => sourceFaction;
        public int Damage => damage;
        public bool InflictsVulnerability { get; set; }
        public int UpgradeLevel { get; private set; }

        public void Reflect(GameObject reflector)
        {
            direction = -direction;
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * .5f));
            sourceFaction = Faction.Player;
            owner = reflector;
            damageContext = DamageContext.From(sourceFaction, reflector);
            InflictsVulnerability = false;
            transform.right = direction;
            remainingLife = Mathf.Max(remainingLife, 1);
            GetComponent<SpriteRenderer>().color = Color.cyan;
        }

        public static Projectile Spawn(
            Vector2 position,
            Vector2 direction,
            int damage,
            Faction sourceFaction,
            GameObject owner,
            Color color,
            float speed = 11f,
            float lifetime = 1.5f)
        {
            GameObject projectileObject = new GameObject(sourceFaction + " Projectile");
            projectileObject.transform.position = position;
            PlaceholderVisual visual = projectileObject.AddComponent<PlaceholderVisual>();
            visual.Configure(color, new Vector2(0.32f, 0.12f));
            Projectile projectile = projectileObject.AddComponent<Projectile>();
            projectile.Initialize(direction, damage, sourceFaction, owner, speed, lifetime);
            PlayerEquipment equipment = owner != null ? owner.GetComponentInParent<PlayerEquipment>() : null;
            if (sourceFaction == Faction.Player && equipment?.Profile?.GetEquipped(EquipmentSlot.Weapon) != null)
            {
                projectile.UpgradeLevel = equipment.Profile.WeaponUpgradeLevel;
                WeaponUpgradeVisuals.ApplyProjectile(projectileObject, projectile.UpgradeLevel);
            }
            return projectile;
        }

        public void Initialize(
            Vector2 newDirection,
            int newDamage,
            Faction newSourceFaction,
            GameObject newOwner,
            float newSpeed,
            float lifetime)
        {
            direction = newDirection.sqrMagnitude > 0f ? newDirection.normalized : Vector2.right;
            damage = Mathf.Max(1, newDamage);
            sourceFaction = newSourceFaction;
            damageContext = DamageContext.From(newSourceFaction, newOwner);
            owner = newOwner;
            speed = Mathf.Max(0.1f, newSpeed);
            remainingLife = Mathf.Max(0.1f, lifetime);
            transform.right = direction;
            initialized = true;
        }

        private void FixedUpdate()
        {
            if (!initialized)
            {
                return;
            }

            float distance = speed * Time.fixedDeltaTime;
            RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, HitRadius, direction, distance);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || hit.collider.gameObject == owner)
                {
                    continue;
                }

                Health health = hit.collider.GetComponentInParent<Health>();
                if (health != null && health.gameObject == owner) continue;
                if (health != null && health.GetComponent<PersistentWorld.Player.PlayerSkills>() is PersistentWorld.Player.PlayerSkills skills && skills.TryParry(this)) return;
                if (health != null && health.TryDamage(damage, damageContext))
                {
                    if (InflictsVulnerability && !health.IsDead)
                        (health.GetComponent<CombatStatus>() ?? health.gameObject.AddComponent<CombatStatus>()).ApplyVulnerability();
                    Destroy(gameObject);
                    return;
                }

                if (hit.collider.GetComponentInParent<WorldObstacle>() != null)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            transform.position += (Vector3)(direction * distance);
            remainingLife -= Time.fixedDeltaTime;
            if (remainingLife <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}

