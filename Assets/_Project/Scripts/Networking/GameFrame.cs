using System;
using UnityEngine;

namespace PersistentWorld.Networking
{
    public enum PartyCommand : byte { Create, Invite, Accept, Decline, Leave, Interact, Equip, ChangeClass, Menu, LearnBossSkill, Tutorial, Logout, ResetPractice }
    [Serializable] public sealed class PlayerFrame
    {
        public ulong id; public string name; public int party, area, hp, maximum, character;
        public float mp, charge, special; public int held; public int[] slots; public float[] cooldowns;
        public float dodge, dodgeDuration, specialDuration, specialEffect, specialEffectDuration;
        public bool dodging, specialActive, specialEnabled, hasWeapon;
        public float[] cooldownDurations;
        public bool[] activeSkills;
        public Vector2 position; public bool leader;
        public int visual;
        public int attacks;
        [NonSerialized] public Vector3 renderedPosition;
        public bool bossLearned;
        public int books;
    }
    [Serializable] public sealed class SpriteFrame
    {
        public int id, texture, layer, order, follow; public Rect rect; public Vector2 pivot; public float ppu, angle;
        public Vector3 followOffset;
        public int drawMode;
        public bool swordSwing;
        public Vector2 aim;
        public Vector2 size;
        public Vector3 position, scale; public Color color; public bool flipX, flipY;
    }
    [Serializable] public sealed class LineFrame
    {
        public int id, layer, order; public Vector3[] points; public float width; public Color color; public bool loop;
    }
    [Serializable] public sealed class GameFrame
    {
        public PlayerFrame[] players; public SpriteFrame[] sprites; public LineFrame[] lines;
        public ulong inviter = ulong.MaxValue; public string inviterName, message; public float inviteRemaining;
        public int bossParty; public int bossHp, bossMax;
        public bool bossPhaseTwo;
        public string bossNotice;
        public int validationStage;
        public string inventory;
        public string[] potionSlots;
        public float[] potionCooldowns;
        public int smithRequest;
        public double time;
        public HitFrame[] hits;
        public string prompt;
        public int tutorial;
        public PracticeFrame[] practice;
    }
    [Serializable] public sealed class PracticeFrame
    {
        public int kind, hp, maximum, visual;
        public bool complete;
        public Vector3 position;
    }
    [Serializable] public sealed class HitFrame
    {
        public int sequence, visual, amount;
        public Vector3 position;
        public bool player;
        public double time;
    }
}
