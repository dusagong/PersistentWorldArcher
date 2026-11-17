using System.Collections.Generic;
using PersistentWorld.Combat;
using PersistentWorld.Networking;
using UnityEngine;

namespace PersistentWorld.World
{
    // 즉사기 연출. 판정은 BossController가 하고 이것은 보이기만 한다.
    // - 남은 5초: 보스 발밑에 붉은 마법진이 돌고 경기장이 어두워진다. 끝으로 갈수록 빠르게 맥동한다.
    // - 남은 3초: 보스에서 경기장 끝까지 땅이 갈라지며 붉은 빛 균열이 뻗는다.
    // - 발동: 큰 충격파 고리가 경기장 전체로 퍼지고 번쩍이며 땅이 터진다. 균열과 마법진은 타올랐다 사라진다.
    // 그림은 Resources/Boss의 Doom*.png 파일과 GroundBurst의 부드러운 원이라 온라인 클라이언트에서도 같은 모양으로 보인다.
    public sealed class DoomEffect : MonoBehaviour
    {
        public const float LeadTime = 5f;
        private const float CrackStart = 3f, CrackGrow = .9f, BlastTime = .55f, Lifetime = 1.1f;
        private const int CrackCount = 10;
        private const float CrackUnits = 12f;       // DoomCrack 한 줄 길이 (384px / 32PPU)
        private const float CrackThickness = 1.5f;
        private const float WaveRadiusUnits = 3.7f; // DoomWave 고리 반지름 (118px / 32PPU)
        private const int GroundOrder = -7600;      // 바닥 장식(-7900)보다 위, 캐릭터보다 아래

        private sealed class Crack
        {
            public SpriteRenderer View;
            public float Delay, Length;
        }

        private static Sprite[] crackSprites;
        private static Sprite runeSprite, waveSprite;

        private readonly List<Crack> cracks = new List<Crack>();
        private Health owner;
        private bool watchOwner, remote;
        private Vector2 center;
        private float startedAt, endsAt, detonatedAt = -1f, waveScale;
        private SpriteRenderer rune, shade, wave, flash;

        // owner: 보스 체력. 보스가 죽으면 연출을 거둔다. 온라인 클라이언트는 보스가 없으므로 null을 넘긴다.
        public static DoomEffect Begin(Health owner, Vector2 center, Vector2 arenaCenter, Vector2 arenaHalfSize, float endsAt)
        {
            if (NetworkGameWorld.IsServerWorld)
            {
                // 온라인 서버는 그리지 않고 시작·발동·취소 신호만 보낸다. 실제 그림은 각 클라이언트가 그린다.
                var proxy = new GameObject("Doom Effect (online)").AddComponent<DoomEffect>();
                proxy.remote = true; proxy.owner = owner; proxy.center = center; proxy.endsAt = endsAt;
                NetworkGameWorld.RecordEffect(new EffectFrame
                {
                    kind = EffectFrame.DoomBegin, position = center, direction = arenaCenter, size = arenaHalfSize,
                    duration = Mathf.Max(0f, endsAt - Time.time)
                });
                return proxy;
            }
            if (!LoadSprites()) return null;
            var effect = new GameObject("Doom Effect").AddComponent<DoomEffect>();
            effect.owner = owner;
            effect.watchOwner = owner != null;
            effect.center = center;
            effect.startedAt = Time.time;
            effect.endsAt = endsAt;

            effect.shade = effect.Create("Doom Shade", GroundBurst.Circle, arenaCenter, GroundOrder - 50);
            effect.shade.transform.localScale = new Vector3(arenaHalfSize.x, arenaHalfSize.y, 1f) * 3.4f;
            effect.shade.color = new Color(.12f, 0f, .02f, 0f);

            effect.rune = effect.Create("Doom Rune", runeSprite, center, GroundOrder + 1);
            effect.rune.color = new Color(1f, 1f, 1f, 0f);

            Vector2 bounds = arenaHalfSize + Vector2.one;
            float offset = Random.Range(0f, 360f);
            // 보스가 벽 가까이 있으면 균열이 벽 밖으로 새지 않게 경기장 안쪽으로 부채꼴로 뻗는다
            Vector2 inward = arenaCenter - center;
            bool nearWall = inward.magnitude > 3f;
            float inwardAngle = Mathf.Atan2(inward.y, inward.x) * Mathf.Rad2Deg;
            const float fan = 220f;
            for (int i = 0; i < CrackCount; i++)
            {
                float angle = nearWall
                    ? inwardAngle - fan * .5f + (i + .5f) * fan / CrackCount + Random.Range(-8f, 8f)
                    : offset + i * 360f / CrackCount + Random.Range(-12f, 12f);
                Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                SpriteRenderer view = effect.Create("Doom Crack", crackSprites[i % crackSprites.Length], center, GroundOrder + 2);
                view.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                view.transform.localScale = new Vector3(0f, CrackThickness, 1f);
                view.color = new Color(1f, 1f, 1f, 0f);
                effect.cracks.Add(new Crack
                {
                    View = view,
                    Delay = (i * 7 % CrackCount) * .1f, // 이웃끼리 겹치지 않는 순서로 하나씩 뻗는다
                    Length = Mathf.Max(2f, DistanceToEdge(center, direction, arenaCenter, bounds))
                });
            }

            float far = 0f;
            foreach (Vector2 corner in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 1) })
                far = Mathf.Max(far, Vector2.Distance(center, arenaCenter + Vector2.Scale(corner, bounds)));
            effect.waveScale = far / WaveRadiusUnits;
            return effect;
        }

        public void Detonate()
        {
            if (detonatedAt >= 0f) return;
            if (remote)
            {
                NetworkGameWorld.RecordEffect(new EffectFrame { kind = EffectFrame.DoomDetonate, position = center });
                Destroy(gameObject);
                return;
            }
            detonatedAt = Time.time;
            wave = Create("Doom Wave", waveSprite, center, 5000);
            wave.transform.localScale = Vector3.zero;
            flash = Create("Doom Flash", GroundBurst.Circle, center, 6000);
            flash.transform.localScale = Vector3.one * waveScale * WaveRadiusUnits * 2.4f;
            flash.color = new Color(1f, .82f, .65f, .85f);
            GroundBurst.Spawn(center, 3f, null, 40, 16);
        }

        private void Update()
        {
            if (remote)
            {
                // 서버 쪽 대리 객체: 발동 전에 보스가 죽으면 클라이언트에게 연출을 거두라고 알린다
                if (owner == null || owner.IsDead)
                {
                    NetworkGameWorld.RecordEffect(new EffectFrame { kind = EffectFrame.DoomCancel, position = center });
                    Destroy(gameObject);
                }
                return;
            }
            if (detonatedAt < 0f)
            {
                if (watchOwner && (owner == null || owner.IsDead)) { Destroy(gameObject); return; }
                // 클라이언트에서 발동 신호를 놓쳤을 때를 대비해 끝난 지 2초가 지나면 스스로 사라진다
                if (!watchOwner && Time.time > endsAt + 2f) { Destroy(gameObject); return; }
                Charge(Time.time);
            }
            else Blast(Time.time - detonatedAt);
        }

        private void Charge(float now)
        {
            float remaining = endsAt - now;
            float tension = Mathf.Clamp01(1f - remaining / LeadTime);
            float beat = Mathf.Sin(now * Mathf.Lerp(5f, 24f, tension)) * .5f + .5f; // 끝으로 갈수록 빠르게 맥동
            float fadeIn = Mathf.Clamp01((now - startedAt) / 1f);

            rune.color = new Color(1f, 1f, 1f, fadeIn * Mathf.Lerp(.7f, 1f, beat));
            rune.transform.rotation = Quaternion.Euler(0f, 0f, now * 25f);
            rune.transform.localScale = Vector3.one * (1f + .05f * beat + .25f * tension);
            shade.color = new Color(.12f, 0f, .02f, .42f * Mathf.Clamp01((now - startedAt) / 1.5f));

            float sinceCracks = CrackStart - remaining;
            foreach (Crack crack in cracks)
            {
                float t = sinceCracks - crack.Delay;
                if (t <= 0f) continue;
                float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / CrackGrow), 3f);
                crack.View.transform.localScale = new Vector3(crack.Length / CrackUnits * grow, CrackThickness, 1f);
                crack.View.color = new Color(1f, 1f, 1f, Mathf.Lerp(.55f, 1f, beat));
            }
        }

        private void Blast(float age)
        {
            float t = Mathf.Clamp01(age / BlastTime);
            wave.transform.localScale = Vector3.one * waveScale * (1f - Mathf.Pow(1f - t, 3f));
            wave.color = new Color(1f, 1f, 1f, t < .5f ? 1f : 1f - (t - .5f) * 2f);
            flash.color = new Color(1f, .82f, .65f, .85f * Mathf.Clamp01(1f - age / .35f));
            rune.transform.localScale = Vector3.one * (1.3f + age);
            rune.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - age / .4f));
            shade.color = new Color(.12f, 0f, .02f, .42f * Mathf.Clamp01(1f - age / .8f));
            foreach (Crack crack in cracks)
                crack.View.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - age / .7f));
            if (age >= Lifetime) Destroy(gameObject);
        }

        private SpriteRenderer Create(string name, Sprite sprite, Vector2 at, int order)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            obj.transform.position = at;
            var view = obj.AddComponent<SpriteRenderer>();
            view.sprite = sprite;
            view.sortingOrder = order;
            return view;
        }

        // 경기장 사각형 안의 점에서 direction 쪽으로 테두리까지 거리
        private static float DistanceToEdge(Vector2 from, Vector2 direction, Vector2 arenaCenter, Vector2 half)
        {
            float tx = Mathf.Abs(direction.x) < .0001f ? float.MaxValue
                : ((direction.x > 0f ? arenaCenter.x + half.x : arenaCenter.x - half.x) - from.x) / direction.x;
            float ty = Mathf.Abs(direction.y) < .0001f ? float.MaxValue
                : ((direction.y > 0f ? arenaCenter.y + half.y : arenaCenter.y - half.y) - from.y) / direction.y;
            return Mathf.Max(0f, Mathf.Min(tx, ty));
        }

        private static bool LoadSprites()
        {
            if (runeSprite != null) return true;
            Texture2D crack = Resources.Load<Texture2D>("Boss/DoomCrack");
            Texture2D rune = Resources.Load<Texture2D>("Boss/DoomRune");
            Texture2D wave = Resources.Load<Texture2D>("Boss/DoomWave");
            if (crack == null || rune == null || wave == null) return false;
            const int rows = 3;
            int rowHeight = crack.height / rows;
            crackSprites = new Sprite[rows];
            for (int i = 0; i < rows; i++)
            {
                // 균열은 보스 쪽(왼쪽 끝)이 기준점이라 가로 크기를 키우면 바깥으로 뻗는다
                crackSprites[i] = Sprite.Create(crack, new Rect(0, i * rowHeight, crack.width, rowHeight), new Vector2(0f, .5f), 32f, 0, SpriteMeshType.FullRect);
                crackSprites[i].name = "DoomCrack_" + i;
            }
            runeSprite = Sprite.Create(rune, new Rect(0, 0, rune.width, rune.height), new Vector2(.5f, .5f), 32f, 0, SpriteMeshType.FullRect);
            runeSprite.name = "DoomRune";
            waveSprite = Sprite.Create(wave, new Rect(0, 0, wave.width, wave.height), new Vector2(.5f, .5f), 32f, 0, SpriteMeshType.FullRect);
            waveSprite.name = "DoomWave";
            return true;
        }
    }
}
