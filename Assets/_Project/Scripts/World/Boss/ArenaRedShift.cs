using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace PersistentWorld.World
{
    // 늑대 2페이즈에 아레나 바닥의 연두빛 장식(빛나는 룬·빛 웅덩이·연두 균열)과 바닥 타일 색을 늑대와 같은 비율로 붉게 물들인다.
    // 늑대가 시작할 때 같은 장면에 하나 붙인다. 원래 색을 기억해 두었다가 WolfRed.Amount만큼 빨강 쪽으로 옮긴다.
    public sealed class ArenaRedShift : MonoBehaviour
    {
        private readonly List<(SpriteRenderer renderer, Color color)> sprites = new List<(SpriteRenderer, Color)>();
        private readonly List<(Tilemap map, Color color)> tiles = new List<(Tilemap, Color)>();
        private float shown = -1f;

        public static void Attach(Scene scene)
        {
            if (FindFirstObjectByType<ArenaRedShift>() != null) return;
            var obj = new GameObject("Arena Red Shift");
            SceneManager.MoveGameObjectToScene(obj, scene);
            obj.AddComponent<ArenaRedShift>().Collect(scene.GetRootGameObjects());
        }

        // 온라인 클라이언트: 늑대 보스방 지도(한 장면 안의 한 구역)만 물들인다
        public static void Attach(Transform map)
        {
            if (map == null || FindFirstObjectByType<ArenaRedShift>() != null) return;
            new GameObject("Arena Red Shift").AddComponent<ArenaRedShift>().Collect(new[] { map.gameObject });
        }

        private void Collect(GameObject[] roots)
        {
            foreach (GameObject root in roots)
            {
                foreach (SpriteRenderer r in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (r.sprite == null || r.GetComponentInParent<WolfBossController>() != null) continue;
                    // 분위기 연출(빛 웅덩이·떠다니는 입자)은 ArenaAtmosphere가 직접 붉게 바꾼다
                    if (r.GetComponentInParent<ArenaAtmosphere>() != null || r.name == "Light Pool" || r.name == "Mote") continue;
                    string texture = r.sprite.texture != null ? r.sprite.texture.name : "";
                    bool drawnGreen = texture.Contains("GlowCrack");               // 그림 자체가 연두인 균열
                    bool tintedGreen = WolfRed.IsGreen(r.color);                   // 연두로 물들인 룬·빛 웅덩이
                    if (!drawnGreen && !tintedGreen) continue;
                    if (drawnGreen) WolfRed.Apply(r);                              // 그림 속 연두를 셰이더가 빨강으로
                    sprites.Add((r, r.color));
                }
                foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true))
                    tiles.Add((map, map.color));
            }
        }

        private void Update()
        {
            float amount = WolfRed.Amount;
            if (Mathf.Approximately(amount, shown)) return;
            shown = amount;
            foreach (var (renderer, color) in sprites)
                if (renderer != null) renderer.color = Color.Lerp(color, WolfRed.Redden(color), amount);
            // 바닥은 이끼빛 초록 기운을 빼고 살짝 붉은 흙빛으로
            foreach (var (map, color) in tiles)
                if (map != null) map.color = Color.Lerp(color, new Color(color.g * 1.05f, color.r * .9f, color.b, color.a), amount * .8f);
        }

        private void OnDestroy()
        {
            foreach (var (renderer, color) in sprites) if (renderer != null) renderer.color = color;
            foreach (var (map, color) in tiles) if (map != null) map.color = color;
        }
    }
}
