using System.Linq;
using PersistentWorld.Combat;
using PersistentWorld.Networking;
using PersistentWorld.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace PersistentWorld.Editor
{
    // Deterministic layouts layered onto the team's source scenes; rebuilding keeps the same routes.
    internal static class CooperativeMapLayouts
    {
        public static void Configure(NetworkGameWorld world, GameArea area)
        {
            Transform map = world.maps[(int)area];
            Vector2 origin = NetworkGameWorld.Origin(area);
            if (area == GameArea.BossArena)
            {
                foreach (WorldObstacle obstacle in map.GetComponentsInChildren<WorldObstacle>(true))
                {
                    if (obstacle.GetComponentInParent<Health>() != null) continue;
                    Vector2 local = (Vector2)obstacle.transform.position - origin;
                    if (Mathf.Abs(local.x) >= 11.5f || Mathf.Abs(local.y) >= 6.8f) continue;
                    local.x = local.x < 0 ? -12.7f : 12.7f;
                    MoveProp(map, obstacle.transform, origin + local);
                }
            }
            if (area == GameArea.Field2)
            {
                // 두 번째 필드는 Scenes/Field2.unity를 손대지 않고 그대로 쓴다. 첫 번째 필드에 이곳으로 오는 포탈만 놓는다.
                Portal(world, GameArea.Field, world.mapSpawns[(int)GameArea.Field] + Vector2.right * 4, "Field2", "두 번째 필드 · 안개 숲으로 이동");
                return;
            }
            if (area != GameArea.Tutorial) return;
            Sprite dummySprite = map.GetComponentsInChildren<ServerWorldEntity>(true)
                .Select(e => e.GetComponent<SpriteRenderer>()?.sprite).FirstOrDefault(s => s != null);
            foreach (ScenePortal portal in map.GetComponentsInChildren<ScenePortal>(true))
                Object.DestroyImmediate(portal.gameObject);
            foreach (Transform child in map)
            {
                Vector3 position = child.position;
                if (child.GetComponentInChildren<Tilemap>() == null)
                { position.x = origin.x - (position.x - origin.x); child.position = position; }
            }
            foreach (Tilemap ground in map.GetComponentsInChildren<Tilemap>(true))
                ground.color = new Color(.95f, .92f, .83f);
            foreach (WorldObstacle obstacle in map.GetComponentsInChildren<WorldObstacle>(true))
            {
                if (obstacle.GetComponentInParent<Health>() != null) continue;
                Vector2 local = (Vector2)obstacle.transform.position - origin;
                bool trainingLane = Mathf.Abs(local.x) < 23 && Mathf.Abs(local.y) < 6;
                if (trainingLane) MoveProp(map, obstacle.transform, origin + new Vector2(local.x, local.y < 0 ? -8.5f : 8.5f));
            }
            world.mapCameraSizes[(int)area] = 5.5f;
            world.mapSpawns[(int)area] = new Vector2(0, -2);
            AddPath(map, origin);
            {
                foreach (ServerWorldEntity enemy in map.GetComponentsInChildren<ServerWorldEntity>(true)) Object.DestroyImmediate(enemy.gameObject);
                GameObject dummy = new GameObject("Training Target"); dummy.transform.SetParent(map, false);
                dummy.transform.position = origin + Vector2.up;
                SpriteRenderer visual = dummy.AddComponent<SpriteRenderer>(); visual.sprite = dummySprite; visual.sortingOrder = 1000;
                // Match the normal monster size rather than the original source-sheet resolution.
                if (dummySprite != null) dummy.transform.localScale = Vector3.one * (1.1f / Mathf.Max(.1f, dummySprite.bounds.size.y));
                dummy.AddComponent<CircleCollider2D>().radius = .35f / dummy.transform.localScale.x;
                dummy.AddComponent<Health>().Configure(Faction.Enemy, 500, false);
                dummy.AddComponent<CombatStatus>().Immovable = true;
                dummy.AddComponent<TrainingDummy>();
                ServerWorldEntity gate = dummy.AddComponent<ServerWorldEntity>(); gate.area = area;
                gate.serverBehaviours = dummy.GetComponents<MonoBehaviour>().Where(b => b != gate).ToArray();
                foreach (MonoBehaviour behaviour in gate.serverBehaviours) behaviour.enabled = false;
                dummy.SetActive(false); // Template for each player's own practice target.
                Portal(world, area, new Vector2(-4, -2), "Town", "연습 종료 · 마을로 귀환");
            }
        }
        private static void MoveProp(Transform map, Transform prop, Vector2 destination)
        {
            Vector3 delta = (Vector3)destination - prop.position;
            foreach (SpriteRenderer shadow in map.GetComponentsInChildren<SpriteRenderer>(true))
                if (!shadow.transform.IsChildOf(prop) && shadow.name.Contains("Shadow") &&
                    Vector2.Distance(shadow.transform.position, prop.position) < .5f) shadow.transform.position += delta;
            prop.position += delta;
        }
        private static void AddPath(Transform map, Vector2 origin)
        {
            TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/_Project/EnvironmentTiles/Town Ground 21300024.asset");
            if (tile == null) return;
            GameObject grid = new GameObject("Stone Route"); grid.transform.SetParent(map, false); grid.transform.position = origin;
            grid.AddComponent<Grid>();
            GameObject layer = new GameObject("Walking Path"); layer.transform.SetParent(grid.transform, false);
            Tilemap path = layer.AddComponent<Tilemap>(); layer.AddComponent<TilemapRenderer>().sortingOrder = -90;
            Vector2[] waypoints = { new Vector2(-22, -2), new Vector2(22, -2) };
            for (int segment = 1; segment < waypoints.Length; segment++)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(waypoints[segment - 1], waypoints[segment]) * 2);
                for (int step = 0; step <= steps; step++)
                {
                    Vector2 point = Vector2.Lerp(waypoints[segment - 1], waypoints[segment], step / (float)steps);
                    for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
                        path.SetTile(new Vector3Int(Mathf.RoundToInt(point.x) + x, Mathf.RoundToInt(point.y) + y, 0), tile);
                }
            }
        }
        private static void Portal(NetworkGameWorld world, GameArea area, Vector2 local, string destination, string prompt)
        {
            ScenePortal source = world.maps[(int)GameArea.Town].GetComponentInChildren<ScenePortal>(true);
            if (source == null) return;
            ScenePortal portal = Object.Instantiate(source, world.maps[(int)area], true);
            portal.name = destination + " Route Portal";
            portal.transform.position = NetworkGameWorld.Origin(area) + local;
            portal.Configure(destination, prompt);
        }
    }
}
