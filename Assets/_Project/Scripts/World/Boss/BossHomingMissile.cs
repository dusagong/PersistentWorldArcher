using PersistentWorld.Combat;
using PersistentWorld.Core;
using UnityEngine;

namespace PersistentWorld.World
{
    // 보스 유도탄: 대상 플레이어를 따라가다가 플레이어와 부딪히거나 수명이 끝나면 폭발한다.
    // 속도는 플레이어 이동 속도와 같고 회전이 느려서, 옆으로 비켜서면 피할 수 있다.
    public sealed class BossHomingMissile : MonoBehaviour
    {
        private const float ContactDistance = .45f;
        private const float WarnBeforeExplode = .6f;
        private const float TurnRate = 140f;

        private Transform target;
        private GameObject owner;
        private float speed;
        private float radius;
        private float explodeAt;
        private int damage;
        private Vector2 direction;
        private Color warnColor;
        private BossTelegraph warning;
        private bool exploded;
        private Vector3 baseScale;

        public static BossHomingMissile Spawn(Vector2 position, Transform target, GameObject owner, int damage,
            float speed, float lifetime, float radius, Color color, Color warnColor)
        {
            GameObject obj = new GameObject("Boss Homing Missile");
            obj.transform.position = position;
            obj.AddComponent<PlaceholderVisual>().Configure(color, new Vector2(.42f, .42f));
            obj.GetComponent<SpriteRenderer>().sortingOrder = 1500;
            BossHomingMissile missile = obj.AddComponent<BossHomingMissile>();
            missile.target = target;
            missile.owner = owner;
            missile.damage = damage;
            missile.speed = speed;
            missile.radius = radius;
            missile.explodeAt = Time.time + lifetime;
            missile.warnColor = warnColor;
            missile.baseScale = obj.transform.localScale;
            Vector2 aim = target != null ? (Vector2)target.position - position : Vector2.right;
            missile.direction = aim.sqrMagnitude > .01f ? aim.normalized : Vector2.right;
            return missile;
        }

        private void Update()
        {
            if (exploded) return;
            if (target != null && target.GetComponent<Health>() is Health h && !h.IsDead)
            {
                Vector2 desired = ((Vector2)target.position - (Vector2)transform.position).normalized;
                if (desired.sqrMagnitude > .01f)
                    direction = Vector3.RotateTowards(direction, desired, TurnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
            }

            transform.position += (Vector3)(direction * speed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 45f);
            transform.localScale = baseScale * (1f + .15f * Mathf.Sin(Time.time * 18f));

            foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
            {
                Health ph = player.GetComponent<Health>();
                if (ph != null && !ph.IsDead && Vector2.Distance(player.transform.position, transform.position) <= ContactDistance)
                {
                    Explode();
                    return;
                }
            }

            if (warning == null && Time.time >= explodeAt - WarnBeforeExplode)
                warning = BossTelegraph.Circle(transform.position, radius, WarnBeforeExplode, warnColor);
            if (warning != null) warning.transform.position = transform.position;
            if (Time.time >= explodeAt) Explode();
        }

        private void Explode()
        {
            exploded = true;
            if (warning == null) warning = BossTelegraph.Circle(transform.position, radius, .05f, warnColor);
            warning.transform.position = transform.position;
            warning.Fire();
            foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
            {
                Health h = player.GetComponent<Health>();
                if (h == null || h.IsDead || Vector2.Distance(player.transform.position, transform.position) > radius + .3f) continue;
                if (owner != null) h.TryDamage(damage, Faction.Enemy, owner);
                else h.TryDamage(damage, Faction.Enemy);
            }
            Destroy(gameObject);
        }
    }
}
