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
        private readonly Dictionary<int, List<(double time, SpriteFrame frame)>> history = new Dictionary<int, List<(double, SpriteFrame)>>();
        private readonly Dictionary<int, float> flashes = new Dictionary<int, float>();
        private readonly Dictionary<int, float> born = new Dictionary<int, float>();
        private double serverTime, receivedAt;
        private double clockOffset;
        private bool clockReady;
        public NetworkGameView(NetworkGameAssets registry, Transform parent)
        {
            assets = registry; root = parent;
            Texture2D pixel = new Texture2D(1, 1); pixel.SetPixel(0, 0, Color.white); pixel.Apply();
            square = Sprite.Create(pixel, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            material = new Material(Shader.Find("Sprites/Default"));
        }
        public void Apply(GameFrame frame)
        {
            serverTime = frame.time; receivedAt = Time.unscaledTimeAsDouble;
            double offset = serverTime - receivedAt;
            clockOffset = clockReady ? clockOffset + (offset - clockOffset) * .04 : offset;
            clockReady = true;
            visible.Clear();
            foreach (SpriteFrame data in frame.sprites)
            {
                visible.Add(data.id);
                if (!actors.TryGetValue(data.id, out SpriteRenderer visual))
                {
                    visual = new GameObject("Server Visual " + data.id).AddComponent<SpriteRenderer>();
                    visual.transform.SetParent(root); actors[data.id] = visual;
                    born[data.id] = Time.unscaledTime;
                    visual.transform.SetPositionAndRotation(data.position, Quaternion.Euler(0, 0, data.angle));
                }
                var key = (data.texture, data.rect, data.pivot, data.ppu);
                if (!sprites.TryGetValue(key, out Sprite sprite))
                {
                    sprite = data.texture switch
                    {
                        -2 => PersistentWorld.World.ActorShadow.Blob,
                        -3 => PersistentWorld.World.GroundBurst.Circle,
                        -4 => PersistentWorld.World.BossTelegraph.CircleSprite,
                        -5 => PersistentWorld.World.BossTelegraph.FramedSquareSprite,
                        -6 => PersistentWorld.World.SlimeJumpEnemyController.CircleSprite,
                        _ => square
                    };
                    if (data.texture >= 0 && data.texture < assets.textures.Length && data.rect.width > 0 && data.rect.height > 0)
                        sprite = Sprite.Create(assets.textures[data.texture], data.rect, data.pivot, Mathf.Max(1, data.ppu), 0, SpriteMeshType.FullRect);
                    sprites[key] = sprite;
                }
                visual.sprite = sprite; visual.color = data.color; visual.flipX = data.flipX; visual.flipY = data.flipY;
                visual.drawMode = (SpriteDrawMode)data.drawMode; visual.size = data.size;
                visual.sortingLayerID = data.layer;
                visual.sortingOrder = data.order;
                targets[data.id] = data;
                if (!history.TryGetValue(data.id, out var samples)) history[data.id] = samples = new List<(double, SpriteFrame)>(8);
                if (samples.Count > 0 && (samples[samples.Count - 1].frame.position - data.position).sqrMagnitude > 25) samples.Clear();
                samples.Add((frame.time, data));
                if (samples.Count > 8) samples.RemoveAt(0);
                visual.transform.localScale = data.scale;
                visual.enabled = true;
            }
            foreach (var pair in actors) if (!visible.Contains(pair.Key))
                pair.Value.enabled = targets[pair.Key].swordSwing && Time.unscaledTime - born[pair.Key] < PersistentWorld.Combat.SwordSwingEffect.Duration;
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
            foreach (int id in retired) { Object.Destroy(actors[id].gameObject); actors.Remove(id); targets.Remove(id); history.Remove(id); flashes.Remove(id); born.Remove(id); }
            retired.Clear();
            foreach (var pair in lines) if (!pair.Value.enabled) retired.Add(pair.Key);
            foreach (int id in retired) { Object.Destroy(lines[id].gameObject); lines.Remove(id); }
        }
        public void Animate(float blend)
        {
            double renderTime = System.Math.Min(serverTime, Time.unscaledTimeAsDouble + clockOffset - .10);
            foreach (var pair in actors)
            {
                SpriteFrame target = targets[pair.Key];
                Transform body = pair.Value.transform;
                Sample(pair.Key, renderTime, out Vector3 position, out float angle);
                if (target.swordSwing)
                {
                    float phase = Mathf.Clamp01((Time.unscaledTime - born[pair.Key]) / PersistentWorld.Combat.SwordSwingEffect.Duration);
                    angle = Mathf.Atan2(target.aim.y, target.aim.x) * Mathf.Rad2Deg - 45 +
                        Mathf.Lerp(60, -60, Mathf.SmoothStep(0, 1, phase));
                }
                body.SetPositionAndRotation(position, Quaternion.Euler(0, 0, angle));
                pair.Value.color = flashes.TryGetValue(pair.Key, out float until) && Time.unscaledTime < until
                    ? Color.Lerp(target.color, Color.red, .5f) : target.color;
            }
            // Shadows use the exact rendered actor position, so interpolation cannot make them lag behind.
            foreach (var pair in actors)
            {
                SpriteFrame target = targets[pair.Key];
                if (target.follow != 0 && actors.TryGetValue(target.follow, out SpriteRenderer owner))
                    pair.Value.transform.position = owner.transform.position + target.followOffset;
            }
        }
        private void Sample(int id, double time, out Vector3 position, out float angle)
        {
            var samples = history[id];
            var first = samples[0];
            position = first.frame.position; angle = first.frame.angle;
            for (int i = 1; i < samples.Count; i++)
            {
                var next = samples[i];
                if (time <= next.time)
                {
                    float t = Mathf.Clamp01((float)((time - first.time) / System.Math.Max(.001, next.time - first.time)));
                    position = Vector3.Lerp(first.frame.position, next.frame.position, t);
                    angle = Mathf.LerpAngle(first.frame.angle, next.frame.angle, t); return;
                }
                first = next; position = next.frame.position; angle = next.frame.angle;
            }
        }
        public Vector3 Position(int id, Vector3 fallback) => actors.TryGetValue(id, out SpriteRenderer visual) ? visual.transform.position : fallback;
        public void Flash(int id) { if (actors.ContainsKey(id)) flashes[id] = Time.unscaledTime + .16f; }
        internal static float PositionBlend(Vector3 current, Vector3 target, float blend) =>
            (current - target).sqrMagnitude > 25 ? 1 : blend;
        public void Clear()
        {
            foreach (var pair in actors) if (pair.Value != null) Object.Destroy(pair.Value.gameObject);
            foreach (var pair in lines) if (pair.Value != null) Object.Destroy(pair.Value.gameObject);
            actors.Clear(); lines.Clear(); targets.Clear(); history.Clear(); flashes.Clear(); born.Clear();
            clockReady = false;
        }
    }
}
