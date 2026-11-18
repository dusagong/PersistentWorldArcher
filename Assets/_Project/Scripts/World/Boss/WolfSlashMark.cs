using UnityEngine;

namespace PersistentWorld.World
{
    // 그림자 참격의 큰 X를 이루는 한 획: 바닥에 깊게 파인 세 갈래 발톱 자국 (보이기만 하는 연출, 판정은 늑대가 한다).
    // 그어진 뒤 터질 때까지 어두운 자국(틈에 옅은 연두)으로 남고, 터지기 직전 틈이 깜빡이며 차오르다가,
    // 정해진 순간 틈에서 연두빛이 뿜어져 나오고, 그을린 자국이 잠시 남았다가 사라진다.
    // 그림: Resources/Boss/Wolf/WolfClawGouge.png (512×128, 5프레임: 갓 긁힘·충전·폭발·잦아듦·그을린 자국)
    public sealed class WolfSlashMark : MonoBehaviour
    {
        private const int CellW = 512, CellH = 128, Count = 5;
        private const float Reach = 480f;           // 그림 속 획 길이(px)
        private const float WarnBlink = .18f;       // 터지기 전 깜빡이는 시간
        private const float BurstFrame = .08f, FadeFrame = .14f, ScarTime = 1.4f;
        private static Sprite[] frames;

        private SpriteRenderer visual;
        private float burstAt;

        // a → b 로 바닥을 긁은 한 획. width: 자국 칸의 폭(유닛), burstTime: 터지는 시각(Time.time)
        public static WolfSlashMark Spawn(Vector2 a, Vector2 b, float width, float burstTime)
        {
            if (Frames() == null) return null;
            Vector2 d = b - a;
            var obj = new GameObject("Wolf Claw Gouge");
            obj.transform.SetPositionAndRotation((a + b) * .5f, Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg));
            // 그림 1유닛 = 획 길이 전체. 가로는 실제 길이, 세로는 원하는 폭에 맞춘다
            obj.transform.localScale = new Vector3(d.magnitude, width / (CellH / Reach), 1f);
            var mark = obj.AddComponent<WolfSlashMark>();
            mark.visual = obj.AddComponent<SpriteRenderer>();
            mark.visual.sortingOrder = -60; // 바닥 위, 캐릭터 아래
            WolfRed.Apply(mark.visual);
            mark.burstAt = burstTime;
            mark.Update();
            return mark;
        }

        private static Sprite[] Frames()
        {
            if (frames != null) return frames;
            Texture2D sheet = Resources.Load<Texture2D>("Boss/Wolf/WolfClawGouge");
            if (sheet == null) return null;
            frames = new Sprite[Count];
            for (int i = 0; i < Count; i++)
                frames[i] = Sprite.Create(sheet, new Rect(i * CellW, 0, CellW, CellH), new Vector2(.5f, .5f), Reach, 0, SpriteMeshType.FullRect);
            return frames;
        }

        private void Update()
        {
            float now = Time.time, left = burstAt - now;
            if (left > 0f)
            {
                // 갓 긁힌 자국 → 터지기 직전에는 틈이 빠르게 깜빡이며 차오른다
                visual.sprite = frames[left < WarnBlink && Mathf.Sin(now * 70f) > 0f ? 1 : 0];
                return;
            }
            float t = -left;
            if (t < BurstFrame) { visual.sprite = frames[2]; return; }
            t -= BurstFrame;
            if (t < FadeFrame) { visual.sprite = frames[3]; return; }
            t -= FadeFrame;
            if (t < ScarTime)
            {
                visual.sprite = frames[4];
                visual.color = new Color(1f, 1f, 1f, 1f - t / ScarTime);
                return;
            }
            Destroy(gameObject);
        }
    }
}
