using System.Collections.Generic;
using UnityEngine;

namespace PersistentWorld.Networking
{
    // Render only: clients never run enemy AI, damage, projectiles or healing logic.
    public sealed class NetworkGameView
    {
        private readonly NetworkGameAssets assets;
        private readonly Transform root;
        private readonly Sprite square;
        private readonly Dictionary<(int, Rect, Vector2, float), Sprite> sprites = new Dictionary<(int, Rect, Vector2, float), Sprite>();
        private readonly Dictionary<int, SpriteRenderer> actors = new Dictionary<int, SpriteRenderer>();
        private readonly Dictionary<int, SpriteFrame> targets = new Dictionary<int, SpriteFrame>();
        private readonly HashSet<int> visible = new HashSet<int>();
        private readonly List<int> retired = new List<int>();
        private readonly Dictionary<int, LineRenderer> lines = new Dictionary<int, LineRenderer>();
        private readonly Material material;
        public NetworkGameView(NetworkGameAssets registry, Transform parent)
        {
            assets = registry; root = parent;
            Texture2D pixel = new Texture2D(1, 1); pixel.SetPixel(0, 0, Color.white); pixel.Apply();
            square = Sprite.Create(pixel, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            material = new Material(Shader.Find("Sprites/Default"));
        }
        public void Apply(GameFrame frame)
        {
            visible.Clear();
            foreach (SpriteFrame data in frame.sprites)
            {
                visible.Add(data.id);
                if (!actors.TryGetValue(data.id, out SpriteRenderer visual))
                {
                    visual = new GameObject("Server Visual " + data.id).AddComponent<SpriteRenderer>();
                    visual.transform.SetParent(root); actors[data.id] = visual;
                    visual.transform.SetPositionAndRotation(data.position, Quaternion.Euler(0, 0, data.angle));
                }
                var key = (data.texture, data.rect, data.pivot, data.ppu);
                if (!sprites.TryGetValue(key, out Sprite sprite))
                {
                    sprite = data.texture switch
                    {
                        -2 => PersistentWorld.World.ActorShadow.Blob,
                        -3 => PersistentWorld.World.GroundBurst.Circle,
                        _ => square
                    };
                    if (data.texture >= 0 && data.texture < assets.textures.Length && data.rect.width > 0 && data.rect.height > 0)
                        sprite = Sprite.Create(assets.textures[data.texture], data.rect, data.pivot, Mathf.Max(1, data.ppu), 0, SpriteMeshType.FullRect);
                    sprites[key] = sprite;
                }
                visual.sprite = sprite; visual.color = data.color; visual.flipX = data.flipX; visual.flipY = data.flipY;
                visual.sortingLayerID = data.layer;
                visual.sortingOrder = data.order;
                targets[data.id] = data;
                visual.transform.localScale = data.scale;
                visual.enabled = true;
            }
            foreach (var pair in actors) if (!visible.Contains(pair.Key)) pair.Value.enabled = false;
            visible.Clear();
            foreach (LineFrame data in frame.lines)
            {
                visible.Add(data.id);
                if (!lines.TryGetValue(data.id, out LineRenderer line))
                {
                    line = new GameObject("Server Telegraph " + data.id).AddComponent<LineRenderer>();
                    line.transform.SetParent(root); line.sharedMaterial = material; line.useWorldSpace = true; lines[data.id] = line;
                }
                line.positionCount = data.points.Length; line.SetPositions(data.points); line.loop = data.loop;
                line.startWidth = line.endWidth = data.width; line.startColor = line.endColor = data.color;
                line.sortingLayerID = data.layer; line.sortingOrder = data.order; line.enabled = true;
            }
            foreach (var pair in lines) if (!visible.Contains(pair.Key)) pair.Value.enabled = false;
            // Remove retired objects instead of retaining an unbounded history of projectiles/effects.
            retired.Clear();
            foreach (var pair in actors) if (!pair.Value.enabled) retired.Add(pair.Key);
            foreach (int id in retired) { Object.Destroy(actors[id].gameObject); actors.Remove(id); targets.Remove(id); }
            retired.Clear();
            foreach (var pair in lines) if (!pair.Value.enabled) retired.Add(pair.Key);
            foreach (int id in retired) { Object.Destroy(lines[id].gameObject); lines.Remove(id); }
        }
        public void Animate(float blend)
        {
            foreach (var pair in actors)
            {
                SpriteFrame target = targets[pair.Key];
                Transform body = pair.Value.transform;
                float step = (body.position - target.position).sqrMagnitude > 25 ? 1 : blend;
                body.SetPositionAndRotation(Vector3.Lerp(body.position, target.position, step),
                    Quaternion.Slerp(body.rotation, Quaternion.Euler(0, 0, target.angle), step));
            }
        }
        public void Clear()
        {
            foreach (var pair in actors) if (pair.Value != null) Object.Destroy(pair.Value.gameObject);
            foreach (var pair in lines) if (pair.Value != null) Object.Destroy(pair.Value.gameObject);
            actors.Clear(); lines.Clear(); targets.Clear();
        }
    }
}
