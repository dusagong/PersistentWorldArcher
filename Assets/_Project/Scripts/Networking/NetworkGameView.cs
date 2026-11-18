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
        private readonly Dictionary<int, NetworkCharacterPose> characterPoses = new Dictionary<int, NetworkCharacterPose>();
        // 몬스터 피격 연출(움찔·납작·멈칫): 서버는 그리지 않고 클라이언트가 피격 소식으로 직접 재생한다
        private readonly Dictionary<int, (float at, Vector2 away, float distance)> reactions = new Dictionary<int, (float, Vector2, float)>();
        private readonly List<int> finished = new List<int>();
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
                        -7 => PersistentWorld.World.DroppedItem.Circle,
                        _ => square
                    };
                    if (data.texture >= 0 && data.texture < assets.textures.Length && data.rect.width > 0 && data.rect.height > 0)
                        sprite = Sprite.Create(assets.textures[data.texture], data.rect, data.pivot, Mathf.Max(1, data.ppu), 0, SpriteMeshType.FullRect);
                    sprites[key] = sprite;
                }
                bool frozen = reactions.TryGetValue(data.id, out var hit) && Time.unscaledTime - hit.at < PersistentWorld.World.HitReaction.FreezeTime;
                if (!frozen) visual.sprite = sprite;
                visual.color = data.color; visual.flipX = data.flipX; visual.flipY = data.flipY;
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
            if (frame.players != null)
                foreach (PlayerFrame player in frame.players)
                {
                    if (player.hp <= 0) { characterPoses.Remove(player.visual); continue; }
                    if (!visible.Contains(player.visual) || !actors.TryGetValue(player.visual, out SpriteRenderer body)) continue;
                    if (!characterPoses.TryGetValue(player.visual, out NetworkCharacterPose pose))
                        characterPoses[player.visual] = pose = new NetworkCharacterPose();
                    Vector2 aim = CharacterAim(player, frame, out int comboEffect);
                    pose.Observe(player, aim, comboEffect, Time.unscaledTime);
                    Sprite directional = pose.Sprite(Time.unscaledTime);
                    if (directional != null) { body.sprite = directional; body.flipX = false; }
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
            foreach (int id in retired) { Object.Destroy(actors[id].gameObject); actors.Remove(id); targets.Remove(id); history.Remove(id); flashes.Remove(id); born.Remove(id); characterPoses.Remove(id); reactions.Remove(id); }
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
                if (reactions.TryGetValue(pair.Key, out var hit))
                {
                    float age = Time.unscaledTime - hit.at;
                    position += (Vector3)PersistentWorld.World.HitReaction.Offset(age, hit.away, hit.distance);
                    body.localScale = Vector3.Scale(target.scale, PersistentWorld.World.HitReaction.Squash(age));
                    if (age >= PersistentWorld.World.HitReaction.Duration) finished.Add(pair.Key);
                }
                body.SetPositionAndRotation(position, Quaternion.Euler(0, 0, angle));
                pair.Value.color = flashes.TryGetValue(pair.Key, out float until) && Time.unscaledTime < until
                    ? Color.Lerp(target.color, Color.red, .5f) : target.color;
                if (characterPoses.TryGetValue(pair.Key, out NetworkCharacterPose pose))
                {
                    Sprite directional = pose.Sprite(Time.unscaledTime);
                    if (directional != null) { pair.Value.sprite = directional; pair.Value.flipX = false; }
                }
            }
            foreach (int id in finished) reactions.Remove(id);
            finished.Clear();
            // Shadows use the exact rendered actor position, so interpolation cannot make them lag behind.
            foreach (var pair in actors)
            {
                SpriteFrame target = targets[pair.Key];
                if (target.follow != 0 && actors.TryGetValue(target.follow, out SpriteRenderer owner))
                    pair.Value.transform.position = owner.transform.position + target.followOffset;
            }
        }
        private Vector2 CharacterAim(PlayerFrame player, GameFrame frame, out int comboEffect)
        {
            comboEffect = 0;
            Vector2 result = player.aim;
            float best = float.PositiveInfinity, latestCombo = float.NegativeInfinity;
            // Old deployed servers have no PlayerFrame.aim yet. Their existing effect rotations
            // still supply remote attack directions without changing the wire protocol/version.
            foreach (SpriteFrame effect in frame.sprites)
            {
                if (effect.id == player.visual) continue;
                if (effect.swordSwing && effect.follow == player.visual && effect.aim.sqrMagnitude > .001f)
                { if (result.sqrMagnitude < .001f) result = effect.aim; continue; }
                if (effect.texture < 0 || effect.texture >= assets.textures.Length) continue;
                Texture2D texture = assets.textures[effect.texture];
                if (texture == null) continue;
                string name = texture.name;
                float sourceAngle;
                switch (name)
                {
                    case "BasicAttackBow": case "BasicAttackArrow": sourceAngle = 45; break;
                    case "SkillThreeCast": sourceAngle = -135; break;
                    case "WarriorCharge": case "WarriorRelease": sourceAngle = -90; break;
                    case "WarriorParry": sourceAngle = 180; break;
                    case "HealerBolt": sourceAngle = 90; break;
                    case "WarriorCombo": case "HealerJudgment": sourceAngle = 0; break;
                    default: continue;
                }
                float distance = ((Vector2)effect.position - player.position).sqrMagnitude;
                if (distance > 9) continue;
                bool nearerPlayer = false;
                foreach (PlayerFrame other in frame.players)
                    if (other.visual != player.visual && ((Vector2)effect.position - other.position).sqrMagnitude + .001f < distance)
                    { nearerPlayer = true; break; }
                if (nearerPlayer) continue;
                if (name == "WarriorCombo" && born.TryGetValue(effect.id, out float began) && began > latestCombo)
                { latestCombo = began; comboEffect = effect.id; }
                if (player.aim.sqrMagnitude < .001f && distance < best)
                {
                    best = distance; float angle = (effect.angle + sourceAngle) * Mathf.Deg2Rad;
                    result = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
            }
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (player.aim.sqrMagnitude < .001f && manager != null && manager.LocalClientId == player.id && Camera.main != null)
                result = ((Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition) - player.position).normalized;
            return result;
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
        // attacker: 때린 쪽으로 볼 위치(가장 가까운 플레이어). 큰 그림(보스)은 덜 밀린다.
        public void React(int id, Vector2 attacker)
        {
            if (!actors.TryGetValue(id, out SpriteRenderer visual) || visual.sprite == null) return;
            Vector2 center = visual.bounds.center;
            Vector2 away = center - attacker;
            away = away.sqrMagnitude > .0001f ? away.normalized : Random.insideUnitCircle.normalized;
            bool heavy = visual.bounds.size.y > 2.2f;
            reactions[id] = (Time.unscaledTime, away, heavy ? .05f : .13f);
            PersistentWorld.World.HitSpark.Spawn(center - away * visual.bounds.extents.x * .5f, heavy ? 1.4f : .8f, visual.sortingLayerID, visual.sortingOrder + 3);
        }
        internal static float PositionBlend(Vector3 current, Vector3 target, float blend) =>
            (current - target).sqrMagnitude > 25 ? 1 : blend;
        public void Clear()
        {
            foreach (var pair in actors) if (pair.Value != null) Object.Destroy(pair.Value.gameObject);
            foreach (var pair in lines) if (pair.Value != null) Object.Destroy(pair.Value.gameObject);
            actors.Clear(); lines.Clear(); targets.Clear(); history.Clear(); flashes.Clear(); born.Clear();
            characterPoses.Clear(); reactions.Clear();
            clockReady = false;
        }
    }
}
