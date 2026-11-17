using System.Collections.Generic;
using UnityEngine;

namespace PersistentWorld.World
{
    // 맞은 몬스터가 맞은 반대쪽으로 움찔 밀렸다 돌아오고, 순간 납작해졌다 복원되며, 아주 잠깐 그림이 멈춘다.
    // 카메라가 그리는 순간에만 위치·크기·그림을 바꾸고 그린 뒤 되돌린다. 실제 위치·충돌·AI·애니메이션에는 영향이 없다.
    // 온라인 클라이언트는 같은 곡선(Offset·Squash)을 자기 화면의 그림에 직접 적용한다.
    public static class HitReaction
    {
        public const float Duration = .16f;
        public const float FreezeTime = .06f;

        private sealed class Entry
        {
            public Transform Target;
            public SpriteRenderer Body, Extra;
            public Sprite Frozen;
            public Vector2 Direction;
            public float Distance, StartedAt;
            public bool Applied;
            public Vector3 SavedPosition, SavedScale;
            public Sprite SavedBody, SavedExtra;
        }

        private static readonly List<Entry> active = new List<Entry>();
        private static bool hooked;

        // 빠르게 밀렸다가 부드럽게 돌아온다
        public static Vector2 Offset(float age, Vector2 direction, float distance)
        {
            if (age < 0f || age >= Duration) return Vector2.zero;
            float t = age / Duration;
            float push = t < .2f ? t / .2f : 1f - Mathf.SmoothStep(0f, 1f, (t - .2f) / .8f);
            return direction * (distance * push);
        }

        // 맞은 순간 가장 납작하고 점점 원래 모양으로
        public static Vector3 Squash(float age)
        {
            if (age < 0f || age >= Duration) return Vector3.one;
            float k = 1f - age / Duration;
            k *= k;
            return new Vector3(1f + .2f * k, 1f - .15f * k, 1f);
        }

        // extra: 몸과 같은 그림을 쓰는 겹침 그림(피격 번쩍임 등). 멈칫하는 동안 같이 멈춘다.
        public static void Play(Transform target, SpriteRenderer body, SpriteRenderer extra, Vector2 direction, float distance)
        {
            if (target == null || body == null) return;
            if (!hooked)
            {
                Camera.onPreCull += Apply;
                Camera.onPostRender += Restore;
                hooked = true;
            }
            Entry entry = active.Find(e => e.Target == target);
            if (entry == null) { entry = new Entry { Target = target }; active.Add(entry); }
            entry.Body = body;
            entry.Extra = extra;
            entry.Frozen = body.sprite;
            entry.Direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.right;
            entry.Distance = distance;
            entry.StartedAt = Time.time;
        }

        private static void Apply(Camera _)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Entry e = active[i];
                if (e.Applied) continue;
                float age = Time.time - e.StartedAt;
                if (e.Target == null || e.Body == null || age >= Duration) { active.RemoveAt(i); continue; }
                e.SavedPosition = e.Target.position;
                e.SavedScale = e.Target.localScale;
                e.SavedBody = e.Body.sprite;
                e.SavedExtra = e.Extra != null ? e.Extra.sprite : null;
                e.Target.position = e.SavedPosition + (Vector3)Offset(age, e.Direction, e.Distance);
                e.Target.localScale = Vector3.Scale(e.SavedScale, Squash(age));
                if (age < FreezeTime && e.Frozen != null)
                {
                    e.Body.sprite = e.Frozen;
                    if (e.Extra != null) e.Extra.sprite = e.Frozen;
                }
                e.Applied = true;
            }
        }

        private static void Restore(Camera _)
        {
            foreach (Entry e in active)
            {
                if (!e.Applied) continue;
                e.Applied = false;
                if (e.Target == null) continue;
                e.Target.position = e.SavedPosition;
                e.Target.localScale = e.SavedScale;
                if (e.Body != null) e.Body.sprite = e.SavedBody;
                if (e.Extra != null) e.Extra.sprite = e.SavedExtra;
            }
        }
    }
}
