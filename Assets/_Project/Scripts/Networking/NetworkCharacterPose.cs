using PersistentWorld.Player;
using UnityEngine;

namespace PersistentWorld.Networking
{
    // A client-side presentation of server-confirmed actions; never predicts damage or movement.
    internal sealed class NetworkCharacterPose
    {
        private PlayerFrame previous, current;
        private CharacterClass character;
        private CharacterMotion action;
        private float actionAt, dodgeAt;
        private Vector2 movement, aim, actionAim;
        private int facing, comboEffect;

        internal void Observe(PlayerFrame player, Vector2 visualAim, int latestComboEffect, float now)
        {
            CharacterClass next = (CharacterClass)player.character;
            if (previous != null && next != character) { previous = null; action = CharacterMotion.Idle; facing = 0; }
            character = next;
            current = player; aim = visualAim;
            movement = previous != null ? player.position - previous.position : Vector2.zero;
            if (movement.sqrMagnitude > 25) { movement = Vector2.zero; action = CharacterMotion.Idle; }
            if (previous != null)
            {
                if (player.attacks != previous.attacks) Play(CharacterMotion.BasicAttack, now);
                if (player.special > previous.special + .1f) Play(CharacterMotion.Special, now);
                if (player.cooldowns != null && previous.cooldowns != null && player.slots != null)
                    for (int i = 0; i < Mathf.Min(player.cooldowns.Length, previous.cooldowns.Length, player.slots.Length); i++)
                        if (player.cooldowns[i] > previous.cooldowns[i] + .1f && player.slots[i] != (int)SkillId.None)
                            Play(FourDirectionCharacterArt.ForSkill((SkillId)player.slots[i]), now);
                if (player.dodging && !previous.dodging) dodgeAt = now;
                // Combo hits 1/2 do not start cooldowns; the new server effect confirms each hit.
                if (latestComboEffect != 0 && latestComboEffect != comboEffect && character == CharacterClass.Warrior)
                    Play(CharacterMotion.SkillTwo, now);
            }
            else dodgeAt = now;
            comboEffect = latestComboEffect;
            previous = player;
        }

        private void Play(CharacterMotion motion, float now)
        { action = motion; actionAt = now; actionAim = aim; }

        internal Sprite Sprite(float now)
        {
            if (current == null || current.hp <= 0) return null;
            bool moving = movement.sqrMagnitude > .0001f;
            CharacterMotion motion = moving ? CharacterMotion.Walk : CharacterMotion.Idle;
            float elapsed = now;
            bool holding = false;
            if (moving) facing = FourDirectionCharacterArt.Face(movement.normalized, facing);
            if (current.held != (int)SkillId.None)
            {
                motion = moving && current.held == (int)SkillId.ArrowRain ? CharacterMotion.Walk
                    : FourDirectionCharacterArt.ForSkill((SkillId)current.held);
                holding = motion != CharacterMotion.Walk; facing = FourDirectionCharacterArt.Face(aim, facing);
            }
            else if (action != CharacterMotion.Idle && now - actionAt < FourDirectionCharacterArt.Duration(character, action))
            {
                motion = action; elapsed = now - actionAt;
                facing = FourDirectionCharacterArt.Face(actionAim, facing);
            }
            else if (current.specialActive && character == CharacterClass.Warrior && !moving)
            { motion = CharacterMotion.Special; holding = true; facing = FourDirectionCharacterArt.Face(aim, facing); }
            if (current.dodging)
            {
                motion = CharacterMotion.Dodge; elapsed = now - dodgeAt; holding = false;
                facing = FourDirectionCharacterArt.Face(moving ? movement.normalized : aim, facing);
            }
            return FourDirectionCharacterArt.Frame(character, motion, facing, elapsed, holding);
        }
    }
}
