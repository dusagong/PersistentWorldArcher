using System;
using PersistentWorld.Networking;
using UnityEngine;

namespace PersistentWorld.Editor
{
    public static class PartyValidation
    {
        private static int checks;
        private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); checks++; }
        public static void Run()
        {
            checks = 0;
            PartyState state = new PartyState();
            for (ulong i = 1; i <= 7; i++) state.Add(i, "Player " + i);
            Check(!state.Invite(1, 1, 0, out _), "self invitation");
            Check(!state.Invite(1, 99, 0, out _), "disconnected target");
            Check(state.Invite(1, 2, 0, out _) && state.Members[1].Party != 0, "inviter creates a solo party");
            Check(state.Members[2].Party == 0 && state.Invitations.ContainsKey(2), "invitation does not auto join");
            Check(!state.Invite(3, 2, 0, out _), "one pending invitation per target");
            Check(state.Respond(2, false, 1, out _) && state.Members[2].Party == 0, "decline preserves membership");
            Check(state.Invite(1, 2, 2, out _) && state.Respond(2, true, 3, out _), "accept invitation");
            Check(state.Members[1].Party == state.Members[2].Party && state.Group(state.Members[1].Party).Length == 2, "two-person party");
            Check(!state.Invite(3, 2, 3, out _), "already grouped target rejected");
            Check(state.Invite(1, 4, 4, out _) && state.Respond(4, true, 5, out _), "third member");
            Check(state.Invite(1, 5, 6, out _) && state.Invite(1, 6, 6, out _), "simultaneous invitations for last slot");
            Check(state.Respond(5, true, 7, out _) && !state.Respond(6, true, 7, out _), "last slot is atomic");
            Check(state.Group(state.Members[1].Party).Length == 4 && state.Members[6].Party == 0, "max capacity four");
            Check(!state.Invite(1, 6, 8, out _), "full party cannot invite");
            Check(state.Leave(1, out _) && state.Leader(state.Members[2].Party) == 2, "leader transfers on leave");
            Check(state.Invite(2, 6, 10, out _), "member may invite"); state.Expire(41);
            Check(!state.Respond(6, true, 41, out _), "expired invitation rejected");
            Check(state.Invite(2, 6, 42, out _), "reinvite after expiry"); state.Remove(6);
            Check(!state.Invitations.ContainsKey(6), "disconnect cancels incoming invitation");
            state = new PartyState();
            for (ulong i = 1; i <= 4; i++) state.Add(i, "Player " + i);
            state.Move(1, GameArea.Field);
            Check(!state.EnterBoss(1, out _, out _) && state.Members[1].Party == 0, "boss requires an existing party");
            state.Move(1, GameArea.Town); state.Invite(1, 2, 0, out _); state.Respond(2, true, 1, out _);
            state.Move(1, GameArea.Field); state.Move(2, GameArea.Field); state.Move(3, GameArea.Field); state.Create(3, out _);
            Check(!state.EnterBoss(2, out _, out _), "only party leader enters");
            state.Members[2].Alive = false;
            Check(!state.EnterBoss(1, out _, out _), "dead member blocks entry"); state.Members[2].Alive = true;
            state.Move(2, GameArea.Town);
            Check(!state.EnterBoss(1, out _, out _), "members in different maps block entry"); state.Move(2, GameArea.Field);
            Check(state.EnterBoss(1, out ulong[] entering, out _) && entering.Length == 2, "party enters together");
            Check(state.Members[1].Area == GameArea.BossArena && state.Members[2].Area == GameArea.BossArena, "all admitted members move atomically");
            Check(!state.EnterBoss(3, out _, out _), "second party cannot enter occupied room");
            Check(!state.Invite(1, 4, 2, out _), "boss room invitation denied");
            Check(state.Leave(1, out _) && state.Members[1].Area == GameArea.Town && state.BossParty != 0, "leaver returns to town without releasing active room");
            state.Remove(2); Check(state.BossParty == 0, "last disconnected member releases room");
            Check(state.EnterBoss(3, out entering, out _) && entering.Length == 1, "explicit solo party can enter");
            state.Move(3, GameArea.Town); Check(state.BossParty == 0, "room released after exit");
            state.Members[4].Area = GameArea.BossArena;
            Check(!state.Create(4, out _), "cannot form party in boss room");
            string json = JsonUtility.ToJson(new GameFrame { message = "파티 검증", players = Array.Empty<PlayerFrame>(), sprites = Array.Empty<SpriteFrame>(), lines = Array.Empty<LineFrame>() });
            Check(NetworkGameWorld.Decompress(NetworkGameWorld.Compress(json)) == json, "snapshot compression round trip");
            Debug.Log("PARTY_MODEL_VALIDATION_OK checks=" + checks);
        }
    }
}
