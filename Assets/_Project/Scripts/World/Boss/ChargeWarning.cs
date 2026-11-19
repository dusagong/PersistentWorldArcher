using System.Collections.Generic;
using PersistentWorld.Networking;
using UnityEngine;

namespace PersistentWorld.World
{
    // 돌진 경고: 빨간 경로 위로 빛나는 화살표(>)가 보스가 달려갈 방향으로 흘러가며, 발동이 가까울수록 빠르고 밝아진다.
    // 보이기만 하는 연출이라 온라인에서는 서버가 신호만 보내고 각 클라이언트가 그린다.
    // 늑대는 초록 화살표(Boss/Wolf/WolfChevron)를 쓰고, 경로가 대상을 따라 움직이는 동안 Retarget으로 옮긴다.
    public sealed class ChargeWarning : MonoBehaviour
    {
        private const float Spacing = 1.1f;
        private const int Order = -499; // 빨간 경로(-500) 바로 위
        private static Sprite chevron, wolfChevron;

        private readonly List<SpriteRenderer> arrows = new List<SpriteRenderer>();
        private Vector2 from, direction;
        private float length, duration, startedAt, size;
        private Sprite sprite;
        // 온라인: 서버는 그림 없는 대리(ghost)만 두고 신호를 보낸다. 경로가 움직이면(Retarget) 같은 번호로 옮기라는 신호를 보낸다.
        private bool ghost, green;
        private int id;
        private float nextSync;
        private static int nextId;
        private static readonly Dictionary<int, ChargeWarning> live = new Dictionary<int, ChargeWarning>();

        public static ChargeWarning Spawn(Vector2 from, Vector2 to, float width, float duration, bool green = false)
        {
            if (NetworkGameWorld.IsServerWorld)
            {
                var proxy = new GameObject("Charge Warning (server)").AddComponent<ChargeWarning>();
                proxy.transform.position = from;
                proxy.ghost = true; proxy.green = green; proxy.id = ++nextId;
                proxy.duration = duration; proxy.startedAt = Time.time;
                NetworkGameWorld.RecordEffect(new EffectFrame { kind = EffectFrame.ChargeWarn, position = from, direction = to, radius = width, duration = duration, crater = green, chunks = proxy.id });
                return proxy;
            }
            Sprite sprite = green ? WolfChevron : Chevron;
            if (sprite == null || duration <= 0f) return null;
            Vector2 path = to - from;
            float length = path.magnitude;
            if (length < .5f) return null;

            var warning = new GameObject("Charge Warning").AddComponent<ChargeWarning>();
            warning.from = from;
            warning.direction = path / length;
            warning.length = length;
            warning.duration = duration;
            warning.startedAt = Time.time;
            warning.sprite = sprite;
            warning.size = Mathf.Clamp(width * .55f, .5f, 1.2f) / (sprite.rect.height / sprite.pixelsPerUnit);
            warning.EnsureArrows();
            warning.Update();
            return warning;
        }

        // 경로를 옮긴다 (대상을 따라다니는 경고용). 길어지면 화살표를 더 만든다
        public void Retarget(Vector2 newFrom, Vector2 newTo)
        {
            if (ghost)
            {
                // 너무 잦지 않게 0.05초마다 (클라이언트는 이 사이를 그대로 둔다)
                if (Time.time < nextSync) return;
                nextSync = Time.time + .05f;
                NetworkGameWorld.RecordEffect(new EffectFrame { kind = EffectFrame.ChargeWarn, op = 1, position = newFrom, direction = newTo, crater = green, chunks = id });
                return;
            }
            Vector2 path = newTo - newFrom;
            if (path.magnitude < .5f) return;
            from = newFrom;
            length = path.magnitude;
            direction = path / length;
            EnsureArrows();
        }

        private void EnsureArrows()
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            int count = Mathf.CeilToInt(length / Spacing) + 1;
            while (arrows.Count < count)
            {
                var obj = new GameObject("Chevron");
                obj.transform.SetParent(transform, false);
                obj.transform.localScale = Vector3.one * size;
                var r = obj.AddComponent<SpriteRenderer>();
                r.sprite = sprite;
                r.sortingOrder = Order;
                r.color = new Color(1f, 1f, 1f, 0f);
                if (sprite == wolfChevron) WolfRed.Apply(r); // 늑대 초록 화살표는 2페이즈에 빨강으로
                arrows.Add(r);
            }
            foreach (SpriteRenderer r in arrows) r.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        // 클라이언트: 서버 신호대로 만들거나(op 0) 같은 번호의 화살표를 옮긴다(op 1). crater: 늑대 초록 화살표
        public static void Replay(EffectFrame e)
        {
            if (e.op == 1)
            {
                if (live.TryGetValue(e.chunks, out ChargeWarning moving) && moving != null) moving.Retarget(e.position, e.direction);
                return;
            }
            ChargeWarning warning = Spawn(e.position, e.direction, e.radius, e.duration, e.crater);
            if (warning != null && e.chunks != 0) { warning.id = e.chunks; live[e.chunks] = warning; }
        }

        private void OnDestroy() { if (!ghost && id != 0 && live.TryGetValue(id, out ChargeWarning w) && w == this) live.Remove(id); }

        // 보스가 쓰러지거나 패턴이 끊겼을 때 남은 화살표를 지운다 (같은 구역만)
        public static void ClearNear(Vector2 position)
        {
            foreach (ChargeWarning w in FindObjectsByType<ChargeWarning>(FindObjectsSortMode.None))
                if (NetworkGameWorld.SameArea(w.transform.position, position) || NetworkGameWorld.SameArea(w.from, position)) Destroy(w.gameObject);
        }

        private void Update()
        {
            float age = Time.time - startedAt;
            if (age >= duration) { Destroy(gameObject); return; }
            if (ghost) return;
            float t = age / duration;
            // 시간이 갈수록 빨리 흐르고, 끝 무렵엔 깜빡인다
            float travel = age * Mathf.Lerp(3f, 9f, t);
            float shift = travel % Spacing;
            float pulse = t > .65f ? .65f + .35f * Mathf.Abs(Mathf.Sin(age * 28f)) : 1f;
            float fadeIn = Mathf.Clamp01(age / .12f);
            for (int i = 0; i < arrows.Count; i++)
            {
                float d = i * Spacing + shift - Spacing;
                SpriteRenderer r = arrows[i];
                bool on = d >= 0f && d <= length;
                r.enabled = on;
                if (!on) continue;
                r.transform.position = from + direction * d;
                // 경로 양 끝에서는 부드럽게 나타나고 사라진다
                float edge = Mathf.Clamp01(d / .8f) * Mathf.Clamp01((length - d) / .8f);
                r.color = new Color(1f, 1f, 1f, .9f * fadeIn * pulse * edge);
            }
        }

        private static Sprite WolfChevron
        {
            get
            {
                if (wolfChevron != null) return wolfChevron;
                Texture2D texture = Resources.Load<Texture2D>("Boss/Wolf/WolfChevron");
                if (texture == null) return Chevron;
                wolfChevron = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 32f, 0, SpriteMeshType.FullRect);
                wolfChevron.name = "WolfChevron";
                return wolfChevron;
            }
        }

        private static Sprite Chevron
        {
            get
            {
                if (chevron != null) return chevron;
                Texture2D texture = Resources.Load<Texture2D>("Boss/ChargeChevron");
                if (texture == null) return null;
                chevron = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 32f, 0, SpriteMeshType.FullRect);
                chevron.name = "ChargeChevron";
                return chevron;
            }
        }
    }
}
