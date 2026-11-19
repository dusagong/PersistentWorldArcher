using PersistentWorld.Networking;
using UnityEngine;

namespace PersistentWorld.World
{
    // 늑대 이펙트를 온라인으로 보내고 받는 곳.
    // 온라인 서버에서는 이펙트 함수가 그림을 만들지 않고 "어떤 이펙트를 어디서, 어떤 값으로"만 기록한다(Send가 true).
    // 클라이언트는 그 신호를 받아 같은 이펙트 함수를 그대로 다시 부른다(Replay). 혼자 하기에서는 Send가 false라 그냥 그린다.
    public static class WolfFxNet
    {
        public const int Bolt = 1, Claw = 2, BigClaw = 3, Crescent = 4, Arc = 5, GroundRing = 6, Erupt = 7, GroundSpark = 8,
            Burst = 9, Ring = 10, Strike = 11, Beam = 12, Sparks = 13, Flash = 14, Shake = 15, SlashMark = 16,
            Scrape = 17, Crack = 18, SpeedLine = 19, Slice = 20, SliceFire = 21, SliceClear = 22, Afterimage = 23, Dust = 24, AimPath = 25;

        private static readonly float[] None = new float[0];

        // 온라인 서버면 기록하고 true (호출한 쪽은 그리지 않고 끝낸다)
        public static bool Send(int op, Vector2 position, Vector2 direction, params float[] args) =>
            NetworkGameWorld.RecordEffect(new EffectFrame { kind = EffectFrame.WolfFx, op = op, position = position, direction = direction, args = args ?? None });

        public static void Replay(EffectFrame e)
        {
            float[] a = e.args ?? None;
            float A(int i, float fallback = 0f) => i < a.Length ? a[i] : fallback;
            Vector2 p = e.position, d = e.direction;
            switch (e.op)
            {
                case Bolt: WolfLightning.Bolt(p, d, A(0, .12f), A(1, .2f), A(2, .3f)); break;
                case Claw: WolfLightning.Claw(p, d, A(0, 2.2f), A(1, .45f), A(2, .22f)); break;
                case BigClaw: WolfLightning.BigClaw(p, d, A(0)); break;
                case Crescent: WolfLightning.Crescent(p, d, A(0)); break;
                case Arc: WolfLightning.Arc(p, d, A(0)); break;
                case GroundRing: WolfLightning.GroundRing(p, A(0), A(1)); break;
                case Erupt: WolfLightning.Erupt(p, A(0), A(1) > .5f); break;
                case GroundSpark: WolfLightning.GroundSpark(p, d, A(0)); break;
                case Burst: WolfLightning.Burst(p, A(0), Mathf.RoundToInt(A(1, 6)), A(2, .22f)); break;
                case Ring: WolfLightning.Ring(p, A(0), Mathf.RoundToInt(A(1, 10)), A(2, .25f)); break;
                case Strike: WolfLightning.Strike(p, A(0, 6f), A(1, 1.6f)); break;
                case Beam: WolfLightning.Beam(p, d, A(0, .12f), A(1)); break;
                case Sparks: WolfLightning.Sparks(p, A(0), Mathf.RoundToInt(A(1, 2))); break;
                case Flash: ScreenFlash.Show(A(0)); break;
                case Shake: CameraFollow.Shake(A(0), A(1)); break;
                case SlashMark: WolfSlashMark.Spawn(p, d, A(0), Time.time + A(1)); break;
                case Scrape: WolfImpactFx.Scrape(p, d); break;
                case Crack: WolfImpactFx.Crack(p, A(0, 1f)); break;
                case SpeedLine: WolfImpactFx.SpeedLine(p, d, new Color(A(0, 1), A(1, 1), A(2, 1), A(3, 1))); break;
                case Slice:
                {
                    int count = Mathf.Max(0, (a.Length - 11) / 2);
                    var targets = new Vector2[count];
                    for (int i = 0; i < count; i++) targets[i] = new Vector2(A(11 + i * 2), A(12 + i * 2));
                    WolfSliceZone.Spawn(p, A(0), A(1), A(2), A(3), A(4), new Color(A(5), A(6), A(7), A(8)), A(9) > .5f, targets);
                    break;
                }
                case SliceFire: WolfSliceZone.FireAll(); break;
                case SliceClear: WolfSliceZone.ClearAll(); break;
                case Afterimage: WolfPuppet.Afterimage(A(0), new Color(A(1), A(2), A(3))); break;
                case Dust: WolfBossController.Dust(p, A(0, 1f)); break;
                case AimPath: WolfAimPath.Replay(A(0), A(1), A(2), (ulong)Mathf.RoundToInt(A(3))); break;
            }
        }
    }
}
