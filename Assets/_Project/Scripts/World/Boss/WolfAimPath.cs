using PersistentWorld.Networking;
using PersistentWorld.Player;
using UnityEngine;

namespace PersistentWorld.World
{
    // 덮치기 조준: 늑대 발밑에서 노린 사람 쪽으로 뻗은 돌진 경로(바닥 장판 + 초록 화살표)가 그 사람을 따라 돈다.
    // 각 PC가 "화면에 보이는" 늑대와 사람 위치로 직접 그린다. 그래서 온라인에서도 늑대·사람·경로가 어긋나지 않는다.
    // 서버는 시작할 때 "늑대가 몇 번 사람을 몇 초 노린다(폭·길이)"만 한 번 보낸다. 고정되는 순간의 확정 경로는 따로 깐다.
    public sealed class WolfAimPath : MonoBehaviour
    {
        private System.Func<Vector2?> source, target;
        private float born, life, width, length;
        private BossTelegraph path;
        private ChargeWarning arrows;
        private Vector2 direction = Vector2.left;

        // wolf: 늑대 (발밑에서 뻗는다), victim: 노린 사람, length: 경로 길이(늑대에서 벽까지)
        public static void Show(WolfBossController wolf, ArcherController victim, float seconds, float width, float length)
        {
            if (wolf == null || victim == null || seconds <= 0f) return;
            if (NetworkGameWorld.IsServerWorld)
            {
                NetworkActorContext context = victim.GetComponent<NetworkActorContext>();
                if (context != null) WolfFxNet.Send(WolfFxNet.AimPath, wolf.transform.position, Vector2.zero, seconds, width, length, context.ClientId);
                return;
            }
            WolfSpriteAnimator art = wolf.GetComponent<WolfSpriteAnimator>();
            Transform body = victim.transform;
            Create(() => wolf != null && art != null ? new Vector2(wolf.transform.position.x, art.FeetY) : (Vector2?)null,
                () => body != null ? (Vector2)body.position : (Vector2?)null, seconds, width, length);
        }

        // 온라인 클라이언트: 늑대 인형의 발밑에서 그 번호 사람(화면에 그려지는 위치)으로
        public static void Replay(float seconds, float width, float length, ulong playerId) =>
            Create(WolfPuppet.Feet, () => NetworkGameWorld.Instance != null ? (Vector2?)NetworkGameWorld.Instance.PlayerRenderedPosition(playerId) : null,
                seconds, width, length);

        private static void Create(System.Func<Vector2?> from, System.Func<Vector2?> to, float seconds, float width, float length)
        {
            Vector2? a = from(), b = to();
            if (a == null || b == null) return;
            var aim = new GameObject("Wolf Aim Path").AddComponent<WolfAimPath>();
            aim.source = from; aim.target = to; aim.born = Time.time; aim.life = seconds; aim.width = width; aim.length = Mathf.Max(1f, length);
            aim.direction = (b.Value - a.Value).sqrMagnitude > .01f ? (b.Value - a.Value).normalized : Vector2.left;
            Vector2 end = a.Value + aim.direction * aim.length;
            Color warning = WolfRed.Tint(new Color(.55f, 1f, .2f), new Color(1f, .28f, .18f));
            aim.path = BossTelegraph.Rect(a.Value, end, width, seconds, warning).Steady(.4f);
            aim.arrows = ChargeWarning.Spawn(a.Value, end, width, seconds, true);
        }

        private void Update()
        {
            Vector2? a = source?.Invoke(), b = target?.Invoke();
            if (Time.time - born >= life || a == null)
            {
                // 조준이 끝나면 따라다니던 경로는 치운다 (고정된 확정 경로가 이어서 깔린다)
                if (path != null) path.Cancel();
                if (arrows != null) Destroy(arrows.gameObject);
                Destroy(gameObject);
                return;
            }
            if (b != null && (b.Value - a.Value).sqrMagnitude > .01f) direction = (b.Value - a.Value).normalized;
            Vector2 end = a.Value + direction * length;
            if (path != null) path.Reshape(a.Value, end, width);
            if (arrows != null) arrows.Retarget(a.Value, end);
        }
    }
}
