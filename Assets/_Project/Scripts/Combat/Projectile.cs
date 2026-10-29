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
        private DamageContext damageContext;
        private GameObject owner;
        private bool initialized;

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
            var equipment = owner != null ? owner.GetComponentInParent<PlayerEquipment>() : null;
            if (sourceFaction == Faction.Player && equipment?.Profile?.GetEquipped(EquipmentSlot.Weapon) != null)
                WeaponUpgradeVisuals.ApplyProjectile(projectileObject, equipment.Profile.WeaponUpgradeLevel);
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
            RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, 0.09f, direction, distance);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || hit.collider.gameObject == owner)
                {
                    continue;
                }

                Health health = hit.collider.GetComponentInParent<Health>();
                if (health != null && health.TryDamage(damage, damageContext))
                {
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

