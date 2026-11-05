using System;
using UnityEngine;

namespace PersistentWorld.Networking
{
    public enum PartyCommand : byte { Create, Invite, Accept, Decline, Leave, Interact, Equip, ChangeClass, Menu }
    [Serializable] public sealed class PlayerFrame
    {
        public ulong id; public string name; public int party, area, hp, maximum, character;
        public float mp, charge, special; public int held; public int[] slots; public float[] cooldowns;
        public Vector2 position; public bool leader;
    }
    [Serializable] public sealed class SpriteFrame
    {
        public int id, texture, order; public Rect rect; public Vector2 pivot; public float ppu, angle;
        public Vector3 position, scale; public Color color; public bool flipX, flipY;
    }
    [Serializable] public sealed class LineFrame
    {
        public int id, order; public Vector3[] points; public float width; public Color color; public bool loop;
    }
    [Serializable] public sealed class GameFrame
    {
        public PlayerFrame[] players; public SpriteFrame[] sprites; public LineFrame[] lines;
        public ulong inviter = ulong.MaxValue; public string inviterName, message; public float inviteRemaining;
        public int bossParty; public int bossHp, bossMax;
        public string bossNotice;
        public int validationStage;
    }
}
