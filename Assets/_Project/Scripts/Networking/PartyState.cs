using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentWorld.Networking
{
    public enum GameArea { Town, Field, BossArena, Field2, Tutorial }
    public sealed class PartyMember
    {
        public ulong Id;
        public string Name;
        public int Party;
        public GameArea Area;
        public bool Alive = true;
    }
    public sealed class PartyInvitation
    {
        public ulong From, To;
        public int Party;
        public double Expires;
    }
    // Server-owned state. No client can assign party membership or room ownership.
    public sealed class PartyState
    {
        public const int Capacity = 4;
        public readonly Dictionary<ulong, PartyMember> Members = new Dictionary<ulong, PartyMember>();
        public readonly Dictionary<ulong, PartyInvitation> Invitations = new Dictionary<ulong, PartyInvitation>();
        private readonly Dictionary<int, ulong> leaders = new Dictionary<int, ulong>();
        private int nextParty;
        public int BossParty { get; private set; }
        public ulong Leader(int party) => leaders.TryGetValue(party, out ulong id) ? id : ulong.MaxValue;
        public PartyMember[] Group(int party) => Members.Values.Where(p => party != 0 && p.Party == party).OrderBy(p => p.Id).ToArray();
        public void Add(ulong id, string name) => Members[id] = new PartyMember { Id = id, Name = name, Area = GameArea.Town };
        public bool Create(ulong id, out string reason)
        {
            reason = "보스 룸에서는 파티를 편성할 수 없습니다.";
            if (!Members.TryGetValue(id, out PartyMember player) || player.Area == GameArea.BossArena) return false;
            if (player.Party != 0) { reason = "이미 파티에 속해 있습니다."; return false; }
            player.Party = ++nextParty; leaders[player.Party] = id; reason = "1인 파티를 만들었습니다."; return true;
        }
        public bool Invite(ulong from, ulong to, double now, out string reason)
        {
            Expire(now); reason = "초대할 수 없는 대상입니다.";
            if (from == to || !Members.TryGetValue(from, out PartyMember source) || !Members.TryGetValue(to, out PartyMember target)) return false;
            if (source.Area == GameArea.BossArena || target.Area == GameArea.BossArena) { reason = "보스 룸에서는 파티 초대가 불가능합니다."; return false; }
            if (source.Area != target.Area) { reason = "같은 맵의 캐릭터를 선택해주세요."; return false; }
            if (target.Party != 0) { reason = "상대는 이미 파티에 속해 있습니다."; return false; }
            if (source.Party == 0) Create(from, out _);
            if (Group(source.Party).Length >= Capacity) { reason = "파티 정원은 4명입니다."; return false; }
            if (Invitations.ContainsKey(to)) { reason = "상대가 다른 초대에 응답 중입니다."; return false; }
            Invitations[to] = new PartyInvitation { From = from, To = to, Party = source.Party, Expires = now + 30 };
            reason = "파티 초대를 보냈습니다."; return true;
        }
        public bool Respond(ulong to, bool accept, double now, out string reason)
        {
            Expire(now); reason = "만료되거나 취소된 초대입니다.";
            if (!Invitations.TryGetValue(to, out PartyInvitation invitation)) return false;
            Invitations.Remove(to);
            if (!accept) { reason = "파티 초대를 거절했습니다."; return true; }
            if (!Members.TryGetValue(to, out PartyMember target) || !Members.TryGetValue(invitation.From, out PartyMember source) ||
                source.Party != invitation.Party || source.Party == 0 || target.Party != 0 ||
                source.Area == GameArea.BossArena || target.Area == GameArea.BossArena || source.Area != target.Area)
                return false;
            if (Group(invitation.Party).Length >= Capacity) { reason = "이미 파티 정원이 찼습니다."; return false; }
            target.Party = invitation.Party; reason = "파티에 참가했습니다."; return true;
        }
        public bool Leave(ulong id, out string reason)
        {
            reason = "소속된 파티가 없습니다.";
            if (!Members.TryGetValue(id, out PartyMember member) || member.Party == 0) return false;
            int party = member.Party; member.Party = 0;
            if (member.Area == GameArea.BossArena) member.Area = GameArea.Town;
            CancelInvites(id);
            PartyMember[] remaining = Group(party);
            if (remaining.Length == 0) leaders.Remove(party);
            else if (Leader(party) == id) leaders[party] = remaining[0].Id;
            ReleaseEmptyRoom(); reason = "파티에서 탈퇴했습니다."; return true;
        }
        public bool EnterBoss(ulong id, out ulong[] entering, out string reason)
        {
            entering = Array.Empty<ulong>(); reason = "필드의 보스 입구에서 입장해주세요.";
            if (!Members.TryGetValue(id, out PartyMember requester) ||
                (requester.Area != GameArea.Field && requester.Area != GameArea.Field2) || !requester.Alive) return false;
            if (BossParty != 0) { reason = "다른 파티가 보스 룸을 사용 중입니다."; return false; }
            if (requester.Party == 0) { reason = "파티에 가입하거나 1인 파티를 먼저 만들어주세요."; return false; }
            if (Leader(requester.Party) != id) { reason = "파티장만 보스 입장을 요청할 수 있습니다."; return false; }
            PartyMember[] group = Group(requester.Party);
            if (group.Any(p => p.Area != requester.Area || !p.Alive)) { reason = "파티원 전원이 같은 필드에 살아 있어야 합니다."; return false; }
            BossParty = requester.Party;
            entering = group.Select(p => p.Id).ToArray();
            foreach (PartyMember player in group) { player.Area = GameArea.BossArena; CancelInvites(player.Id); }
            reason = "파티가 보스 룸에 입장했습니다."; return true;
        }
        public void Move(ulong id, GameArea area)
        {
            if (Members.TryGetValue(id, out PartyMember player)) player.Area = area;
            CancelInvites(id); ReleaseEmptyRoom();
        }
        public void Remove(ulong id)
        {
            Leave(id, out _); CancelInvites(id); Members.Remove(id); ReleaseEmptyRoom();
        }
        public void Expire(double now)
        {
            foreach (ulong id in Invitations.Where(p => p.Value.Expires <= now).Select(p => p.Key).ToArray()) Invitations.Remove(id);
        }
        private void CancelInvites(ulong id)
        {
            foreach (ulong key in Invitations.Where(p => p.Key == id || p.Value.From == id).Select(p => p.Key).ToArray()) Invitations.Remove(key);
        }
        private void ReleaseEmptyRoom()
        {
            if (BossParty != 0 && !Members.Values.Any(p => p.Party == BossParty && p.Area == GameArea.BossArena)) BossParty = 0;
        }
    }
}
