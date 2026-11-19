using System.Collections.Generic;
using PersistentWorld.World;
using UnityEngine;

namespace PersistentWorld.Networking
{
    // 온라인 클라이언트의 늑대 인형.
    // 늑대 두뇌는 서버에서만 돈다. 서버는 동작 이름·시작 시각·방향 같은 정보(WolfFrame)만 보내고,
    // 클라이언트는 혼자 하기와 같은 그림·애니메이터로 직접 그린다. 그래서 1초 10번 전송이어도 동작이 끊기지 않는다.
    // 위치는 서버가 보내는 위치 표시(그림 없는 SpriteFrame)를 다른 그림들과 같은 보간으로 따라간다(잡힌 사람과 어긋나지 않게).
    public sealed class WolfPuppet : MonoBehaviour
    {
        private static WolfPuppet instance;
        private WolfSpriteAnimator art;
        private SpriteRenderer visual;
        private readonly List<(double time, WolfFrame frame)> history = new List<(double, WolfFrame)>(12);
        private int visualId;
        private float flashUntil;

        // 화면 정보를 받을 때마다: 늑대 방을 보고 있고 늑대가 있으면 인형을 만들고 상태를 쌓는다
        public static void Sync(GameFrame frame, bool viewingWolfRoom, Transform wolfMap)
        {
            if (!viewingWolfRoom || frame.wolf == null || !frame.wolf.alive) { Remove(); return; }
            if (instance == null)
            {
                var obj = new GameObject("Wolf Puppet");
                obj.transform.localScale = Vector3.one * Mathf.Max(.1f, frame.wolf.scale);
                instance = obj.AddComponent<WolfPuppet>();
                instance.visual = obj.AddComponent<SpriteRenderer>();
                instance.art = obj.AddComponent<WolfSpriteAnimator>();
                instance.art.Puppet = true;
                if (wolfMap != null) ArenaRedShift.Attach(wolfMap);
            }
            instance.visualId = frame.wolf.visual;
            instance.history.Add((frame.time, frame.wolf));
            if (instance.history.Count > 12) instance.history.RemoveAt(0);
        }

        // 그림 보간이 끝난 뒤 매 프레임: 보간된 서버 시각에 맞는 상태로 그린다
        public static void Animate(NetworkGameView view)
        {
            if (instance == null || view == null || instance.history.Count == 0) return;
            double renderTime = view.RenderTime;
            WolfFrame f = instance.history[0].frame;
            foreach (var (time, frame) in instance.history) if (time <= renderTime) f = frame;
            Transform body = instance.transform;
            body.position = view.Position(instance.visualId, body.position);
            WolfSpriteAnimator art = instance.art;
            art.SetLift(f.lift);
            art.Shape(f.shapeX, f.shapeY, f.shapeSpeed);
            art.SortBias = f.sortBias;
            art.Pose(f.clip, (float)(renderTime - f.clipStart), f.right);
            WolfRed.Set(f.red);
            instance.visual.color = Time.unscaledTime < instance.flashUntil ? new Color(1f, .6f, .6f) : Color.white;
        }

        // 지금 그려지는 늑대 발밑 위치 (늑대 인형이 없으면 null)
        public static Vector2? Feet() => instance != null && instance.art != null ? new Vector2(instance.transform.position.x, instance.art.FeetY) : (Vector2?)null;

        // 늑대가 맞았을 때 잠깐 붉게
        public static bool Flash(int visual)
        {
            if (instance == null || visual != instance.visualId) return false;
            instance.flashUntil = Time.unscaledTime + .12f;
            return true;
        }

        // 돌진 잔상: 지금 그림을 그 자리에 복사해 금방 사라지게
        public static void Afterimage(float alpha, Color tint)
        {
            if (instance == null || instance.art.CurrentSprite == null) return;
            var ghost = new GameObject("Wolf Afterimage");
            ghost.transform.SetPositionAndRotation(instance.transform.position, instance.transform.rotation);
            ghost.transform.localScale = instance.transform.lossyScale;
            var sr = ghost.AddComponent<SpriteRenderer>();
            sr.sprite = instance.art.CurrentSprite; sr.flipX = instance.art.FlipX;
            sr.sortingLayerID = instance.visual.sortingLayerID; sr.sortingOrder = instance.visual.sortingOrder - 1;
            sr.color = new Color(tint.r, tint.g, tint.b, alpha);
            WolfRed.Apply(sr);
            ghost.AddComponent<FadeOut>().life = .22f;
        }

        public static void Remove()
        {
            if (instance == null) return;
            Destroy(instance.gameObject);
            instance = null;
            WolfRed.Set(0f);
        }

        private sealed class FadeOut : MonoBehaviour
        {
            public float life = .2f;
            private float age;
            private SpriteRenderer sr;
            private Color start;
            private void Start() { sr = GetComponent<SpriteRenderer>(); start = sr.color; }
            private void Update()
            {
                age += Time.deltaTime;
                float k = 1f - age / life;
                if (k <= 0f) { Destroy(gameObject); return; }
                sr.color = new Color(start.r, start.g, start.b, start.a * k * k);
            }
        }
    }
}
