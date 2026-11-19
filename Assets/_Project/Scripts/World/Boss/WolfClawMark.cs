using UnityEngine;

namespace PersistentWorld.World
{
    // 그림자 참격의 X자 발톱 자국 (보이기만 하는 연출, 판정은 늑대가 한다).
    // Resources/Boss/Wolf/WolfClawMark.png 6프레임: 바닥 기운 → 빗금 → X자 → 번쩍 → 파편 → 잔광.
    public sealed class WolfClawMark : MonoBehaviour
    {
        // 받은 X자 참격 그림 (Resources/Boss/Wolf/WolfFxCross.png, 8프레임 368×145)
        private const int CellW = 368, CellH = 145, Count = 8;
        private const float Reach = 244f; // 가장 큰 프레임의 X자 폭(px)
        private const float Size = 2.4f;  // 그 폭이 이 크기(유닛)로 보인다
        private static readonly float[] FrameSeconds = { .05f, .05f, .05f, .05f, .06f, .07f, .08f, .09f };
        // X자가 가장 크게 번쩍이는 4번째 프레임이 시작되는 시각 = 피해가 들어가는 순간
        public static float BurstTime => FrameSeconds[0] + FrameSeconds[1] + FrameSeconds[2];

        private static Sprite[] frames;
        private SpriteRenderer visual;
        private float startedAt;

        public static void Spawn(Vector2 at, float angle, float scale = 1f)
        {
            if (Frames() == null) return;
            var obj = new GameObject("Wolf Claw Mark");
            obj.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, angle));
            obj.transform.localScale = Vector3.one * scale;
            var mark = obj.AddComponent<WolfClawMark>();
            mark.visual = obj.AddComponent<SpriteRenderer>();
            mark.visual.sortingOrder = -60; // 바닥 위, 캐릭터 아래
            WolfRed.Apply(mark.visual);
            mark.startedAt = Time.time;
            mark.Update();
        }

        private static Sprite[] Frames()
        {
            if (frames != null) return frames;
            Texture2D sheet = Resources.Load<Texture2D>("Boss/Wolf/WolfFxCross");
            if (sheet == null) return null;
            frames = new Sprite[Count];
            for (int i = 0; i < Count; i++)
                frames[i] = Sprite.Create(sheet, new Rect(i * CellW, 0, CellW, CellH), new Vector2(.505f, .476f), Reach / Size, 0, SpriteMeshType.FullRect);
            return frames;
        }

        private void Update()
        {
            float t = Time.time - startedAt;
            for (int i = 0; i < FrameSeconds.Length; i++)
            {
                if (t < FrameSeconds[i]) { visual.sprite = frames[i]; return; }
                t -= FrameSeconds[i];
            }
            Destroy(gameObject);
        }
    }
}
