using System.Collections;
using PersistentWorld.Combat;
using PersistentWorld.Player;
using UnityEngine;

namespace PersistentWorld.World
{
    // 씬에 놓인 몬스터가 죽으면 일정 시간 뒤 같은 자리에 다시 나타나게 한다. 이 컴포넌트를 씬에 하나 두면 된다.
    // 몬스터가 깨어나기 전 모습을 비활성 보관함에 복제해 두고 그 사본으로 되살려, 씬에서 바꾼 크기·색·드롭 표가 그대로 유지된다.
    [DefaultExecutionOrder(-1000)]
    public sealed class MonsterRespawner : MonoBehaviour
    {
        [SerializeField, Tooltip("죽은 뒤 다시 나타나기까지 걸리는 시간(초)")] private float respawnSeconds = 20f;
        [SerializeField, Tooltip("플레이어가 이 거리 안에 있으면 눈앞에서 나타나지 않도록 잠시 기다린다")] private float playerClearance = 2.5f;
        [SerializeField, Tooltip("플레이어 때문에 더 기다릴 수 있는 최대 시간(초). respawnSeconds와 합쳐 30초를 넘지 않게 둘 것")] private float maxExtraWait = 8f;

        private Transform storage;

        private void Awake()
        {
            storage = new GameObject("Monster Templates").transform;
            storage.SetParent(transform, false);
            storage.gameObject.SetActive(false);
            foreach (Health monster in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (monster.Faction != Faction.Enemy) continue;
                GameObject template = Instantiate(monster.gameObject, storage);
                template.name = monster.name;
                Track(monster, template, monster.transform.position, monster.transform.rotation);
            }
        }

        private void Track(Health monster, GameObject template, Vector3 home, Quaternion rotation)
        {
            monster.Died += dead => StartCoroutine(Respawn(dead, template, home, rotation));
        }

        private IEnumerator Respawn(Health dead, GameObject template, Vector3 home, Quaternion rotation)
        {
            yield return new WaitForSeconds(respawnSeconds);
            float giveUpAt = Time.time + maxExtraWait;
            while (Time.time < giveUpAt && PlayerNear(home)) yield return new WaitForSeconds(.5f);

            if (dead != null) Destroy(dead.gameObject);
            GameObject monster = Instantiate(template, home, rotation);
            monster.name = template.name;
            GroundBurst.Spawn(home, .7f, null, 0, 8);
            Track(monster.GetComponent<Health>(), template, home, rotation);
        }

        private bool PlayerNear(Vector2 point)
        {
            foreach (ArcherController player in FindObjectsByType<ArcherController>(FindObjectsSortMode.None))
                if (Vector2.Distance(player.transform.position, point) < playerClearance) return true;
            return false;
        }
    }
}
