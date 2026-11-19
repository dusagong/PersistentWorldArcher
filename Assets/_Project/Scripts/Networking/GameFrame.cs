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
        public Vector2 aim;
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
        public EffectFrame[] effects;
        public string prompt;
        public int tutorial;
        public PracticeFrame[] practice;
        // 보스를 잡은 직후 보스방에 있는 참가자에게만 실린다. sequence가 0이면 없음.
        public BossResultFrame bossResult;
        // 늑대 보스방에 있는 사람에게만: 늑대 인형을 그릴 정보 (null이면 늑대 없음)
        public WolfFrame wolf;
    }
    // 늑대는 서버 그림을 찍어 보내지 않고, 각 클라이언트가 같은 그림·동작으로 직접 그린다(인형).
    // 서버는 지금 동작 이름과 그 동작을 시작한 서버 시각, 보는 쪽, 공중 높이, 몸 비율, 붉은 정도만 보낸다.
    // 위치는 visual(같은 번호의 위치 표시 그림)로 다른 그림들과 같은 보간을 탄다.
    [Serializable] public sealed class WolfFrame
    {
        public int visual;
        public string clip;
        public double clipStart;
        public bool right, alive;
        public float lift, shapeX, shapeY, shapeSpeed, red, scale;
        public int sortBias;
    }
    // 보스 딜량표: 누가 얼마나 때렸고 얼마나 맞았는지
    [Serializable] public sealed class BossResultFrame
    {
        public int sequence;
        public string boss;
        public float seconds;
        public BossResultRow[] rows;
    }
    [Serializable] public sealed class BossResultRow
    {
        public ulong id;
        public string name;
        public int character, damage, taken, deaths;
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
    // 보이기만 하는 연출(흙먼지·돌조각·즉사기)은 서버가 그리지 않고 이 신호만 보낸다. 각 클라이언트가 직접 그린다.
    [Serializable] public sealed class EffectFrame
    {
        public const int Burst = 1, DoomBegin = 2, DoomDetonate = 3, DoomCancel = 4, ChargeWarn = 5, WolfFx = 6;
        public int sequence, kind, chunks, puffs, debris;
        public Vector2 position, direction, size;
        public float radius, power, scale, linger, duration;
        public bool crater;
        public Color[] colors;
        public double time;
        // 늑대 이펙트(WolfFx): 어떤 이펙트인지(op)와 숫자 인자들
        public int op;
        public float[] args;
    }
}
