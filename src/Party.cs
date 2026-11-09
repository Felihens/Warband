using System;
using System.Collections.Generic;
using UnityEngine;

namespace Warband
{
    internal class Buff
    {
        public int Hash;
        public float Remaining;   // seconds left when received; < 0 = no timer
    }

    internal class Member
    {
        public long Peer;
        public string Name = "";
        public string UserId = "";
        public float Hp, MaxHp = 1f, Stam, MaxStam = 1f, Eitr, MaxEitr;
        public string Weapon = "";
        public Vector3 Pos;
        public bool HasPos;
        public readonly List<Buff> Buffs = new List<Buff>();
        public float VitalsAt = -100f;
        public float MissingSince = -1f;
        public float DeadSince = -1f;   // when they died, -1 while alive
        public string ChatText = "";
        public float ChatAt = -100f;   // when their last party chat message arrived
    }

    // The party lives only in the games of its members: messages go player to player through the
    // server (the server itself needs no mod). The leader keeps the member list and sends it to
    // everyone; every member sends its own health, stamina, eitr, effects and weapon to the others.
    internal static class Party
    {
        internal class Online
        {
            public long Peer;
            public string Name = "";
            public string UserId = "";
        }

        private class Invite
        {
            public long From;
            public string FromName = "";
            public long Leader;
            public float At;
        }

        private class Sent
        {
            public string Name = "";
            public float At;
        }

        private const string RpcInvite = "Warband_Invite";
        private const string RpcReply = "Warband_Reply";
        private const string RpcJoin = "Warband_Join";
        private const string RpcRoster = "Warband_Roster";
        private const string RpcLeave = "Warband_Leave";
        private const string RpcVitals = "Warband_Vitals";

        private const int ReplyDeclined = 0, ReplyBusy = 1, ReplyFull = 2, ReplyStale = 3;
        private const float InviteTimeout = 30f;
        private const float VitalsInterval = 0.5f;
        private const float GoneAfter = 10f;

        internal static readonly List<Member> Members = new List<Member>();   // includes you
        internal static readonly List<Online> OnlinePlayers = new List<Online>();  // excludes you
        internal static long Leader;

        private static Invite _pending;
        private static long _joiningLeader;
        private static float _joiningAt;
        private static readonly Dictionary<long, Sent> SentInvites = new Dictionary<long, Sent>();
        private static ZRoutedRpc _registeredOn;
        private static float _nextVitals, _nextCheck;

        internal static long Me { get { return ZNet.GetUID(); } }
        internal static bool InParty { get { return Members.Count >= 2; } }
        internal static bool IsLeader { get { return InParty && Leader == Me; } }

        internal static string PendingFrom { get { return _pending != null ? _pending.FromName : null; } }

        internal static string MyName()
        {
            Player p = Player.m_localPlayer;
            if (p != null) return p.GetPlayerName();
            if (Game.instance != null && Game.instance.GetPlayerProfile() != null) return Game.instance.GetPlayerProfile().GetName();
            return "?";
        }

        internal static Member Get(long peer)
        {
            foreach (Member m in Members) if (m.Peer == peer) return m;
            return null;
        }

        // An ally: in your party and not you.
        internal static bool IsAlly(long peer)
        {
            return InParty && peer != 0 && peer != Me && Get(peer) != null;
        }

        internal static Online FindOnline(long peer)
        {
            foreach (Online o in OnlinePlayers) if (o.Peer == peer) return o;
            return null;
        }

        internal static Online FindOnlineByFriend(Friend f)
        {
            foreach (Online o in OnlinePlayers)
                if ((f.Id.Length > 0 && o.UserId == f.Id) || string.Equals(o.Name, f.Name, StringComparison.OrdinalIgnoreCase)) return o;
            return null;
        }

        // The loaded character of a player, or null when they are too far away to be loaded.
        internal static Player FindPlayer(long peer)
        {
            foreach (Player p in Player.GetAllPlayers())
                if (p != null && p.GetZDOID().UserID == peer) return p;
            return null;
        }

        internal static Member Self()
        {
            return Get(Me);
        }

        // ---------------------------------------------------------------- per frame

        internal static void Update()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || ZNet.instance == null)
            {
                if (Members.Count > 0 || _pending != null) Reset("left the world");
                _registeredOn = null;
                return;
            }
            if (rpc != _registeredOn) Register(rpc);

            float now = Time.time;
            if (now >= _nextCheck)
            {
                _nextCheck = now + 1f;
                RefreshOnline();
                Timeouts(now);
                CheckGone(now);
            }
            if (InParty && now >= _nextVitals)
            {
                _nextVitals = now + VitalsInterval;
                SendVitals();
            }
        }

        private static void Register(ZRoutedRpc rpc)
        {
            Reset("new connection");
            rpc.Register<ZPackage>(RpcInvite, new Action<long, ZPackage>(OnInvite));
            rpc.Register<ZPackage>(RpcReply, new Action<long, ZPackage>(OnReply));
            rpc.Register<ZPackage>(RpcJoin, new Action<long, ZPackage>(OnJoin));
            rpc.Register<ZPackage>(RpcRoster, new Action<long, ZPackage>(OnRoster));
            rpc.Register<ZPackage>(RpcLeave, new Action<long, ZPackage>(OnLeave));
            rpc.Register<ZPackage>(RpcVitals, new Action<long, ZPackage>(OnVitals));
            rpc.Register<ZPackage>(PartyChat.Rpc, new Action<long, ZPackage>(PartyChat.OnMessage));
            rpc.Register<ZPackage>(Gift.Rpc, new Action<long, ZPackage>(Gift.OnMessage));
            _registeredOn = rpc;
            Plugin.Debug("[net] messages registered, my id " + Me);
        }

        private static void Reset(string why)
        {
            if (Members.Count > 0) Plugin.Debug("[party] cleared (" + why + ")");
            Members.Clear();
            Leader = 0;
            _pending = null;
            _joiningLeader = 0;
            SentInvites.Clear();
            OnlinePlayers.Clear();
        }

        private static void RefreshOnline()
        {
            OnlinePlayers.Clear();
            long me = Me;
            string myName = MyName();
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                long peer = info.m_characterID.UserID;
                if (peer == me) continue;
                if (peer == 0 && info.m_name == myName) continue;
                Online o = new Online();
                o.Peer = peer;
                o.Name = info.m_name ?? "";
                o.UserId = info.m_userInfo.m_id.ToString() ?? "";
                OnlinePlayers.Add(o);
            }
            Friends.Refresh(OnlinePlayers);
            foreach (Member m in Members)
            {
                Online o = FindOnline(m.Peer);
                if (o != null && o.UserId.Length > 0) m.UserId = o.UserId;
            }
        }

        private static void Timeouts(float now)
        {
            if (_pending != null && now - _pending.At > InviteTimeout)
            {
                Plugin.Message("The party invite from " + _pending.FromName + " expired");
                _pending = null;
            }
            if (_joiningLeader != 0 && now - _joiningAt > 15f)
            {
                Plugin.Message("The party leader didn't answer");
                _joiningLeader = 0;
            }
            List<long> expired = null;
            foreach (KeyValuePair<long, Sent> kv in SentInvites)
                if (now - kv.Value.At > InviteTimeout)
                {
                    if (expired == null) expired = new List<long>();
                    expired.Add(kv.Key);
                }
            if (expired == null) return;
            foreach (long peer in expired)
            {
                Plugin.Message(SentInvites[peer].Name + " didn't answer your invite (do they have Warband?)");
                SentInvites.Remove(peer);
            }
            // A party of just you that nobody joined is no party.
            if (Members.Count == 1 && SentInvites.Count == 0) Members.Clear();
        }

        // Players who disconnected: the leader drops them; if the leader is gone, the next member takes over.
        private static void CheckGone(float now)
        {
            if (!InParty) return;
            long me = Me;
            List<Member> gone = new List<Member>();
            foreach (Member m in Members)
            {
                if (m.Peer == me) continue;
                bool here = FindOnline(m.Peer) != null || now - m.VitalsAt < 6f;
                if (here) { m.MissingSince = -1f; continue; }
                if (m.MissingSince < 0f) m.MissingSince = now;
                else if (now - m.MissingSince > GoneAfter) gone.Add(m);
            }
            if (gone.Count == 0) return;

            Member leader = Get(Leader);
            bool leaderGone = leader != null && gone.Contains(leader);
            if (leaderGone)
            {
                Member next = null;
                foreach (Member m in Members)
                    if (!gone.Contains(m)) { next = m; break; }
                if (next == null || next.Peer != me) return;   // someone else takes over and tells us
                Leader = me;
                Plugin.Message(leader.Name + " is gone, you lead the party now");
            }
            else if (Leader != me) return;

            List<long> notify = new List<long>();
            foreach (Member m in gone)
            {
                Members.Remove(m);
                if (m != leader) Plugin.Message(m.Name + " left the party (disconnected)");
            }
            SendRoster(notify);
        }

        // ---------------------------------------------------------------- actions

        internal static void InviteTo(long peer, string name)
        {
            if (peer == 0 || peer == Me) { Plugin.Message(name + " can't be invited right now"); return; }
            if (Get(peer) != null && InParty) { Plugin.Message(name + " is already in your party"); return; }
            if (Members.Count >= Plugin.MaxMembers.Value) { Plugin.Message("Your party is full"); return; }
            if (Members.Count == 0)
            {
                Member self = new Member();
                self.Peer = Me;
                self.Name = MyName();
                Members.Add(self);
                Leader = Me;
            }
            ZPackage pkg = new ZPackage();
            pkg.Write(Leader);
            pkg.Write(MyName());
            Send(peer, RpcInvite, pkg);
            Sent s = new Sent();
            s.Name = name;
            s.At = Time.time;
            SentInvites[peer] = s;
            Plugin.Message("Party invite sent to " + name);
        }

        internal static void Accept()
        {
            if (_pending == null) return;
            if (InParty) { Plugin.Message("Leave your party first"); return; }
            Members.Clear();
            Leader = 0;
            SentInvites.Clear();
            _joiningLeader = _pending.Leader;
            _joiningAt = Time.time;
            ZPackage pkg = new ZPackage();
            pkg.Write(MyName());
            Send(_pending.Leader, RpcJoin, pkg);
            Plugin.Message("Joining " + _pending.FromName + "'s party...");
            _pending = null;
        }

        internal static void Decline()
        {
            if (_pending == null) return;
            Reply(_pending.From, ReplyDeclined);
            Plugin.Message("You declined the invite from " + _pending.FromName);
            _pending = null;
        }

        internal static void Leave()
        {
            if (!InParty) { Members.Clear(); Leader = 0; return; }
            if (Leader == Me)
            {
                Member self = Self();
                Members.Remove(self);
                Leader = Members[0].Peer;   // the next one in line leads
                SendRoster(null);
            }
            else Send(Leader, RpcLeave, new ZPackage());
            Members.Clear();
            Leader = 0;
            Plugin.Message("You left the party");
        }

        internal static void Kick(long peer)
        {
            if (!IsLeader || peer == Me) return;
            Member m = Get(peer);
            if (m == null) return;
            Members.Remove(m);
            List<long> extra = new List<long>();
            extra.Add(peer);
            SendRoster(extra);
            Plugin.Message(m.Name + " was removed from the party");
        }

        internal static void MakeLeader(long peer)
        {
            if (!IsLeader || peer == Me || Get(peer) == null) return;
            Leader = peer;
            SendRoster(null);
            Plugin.Message(Get(peer).Name + " leads the party now");
        }

        // ---------------------------------------------------------------- messages

        private static void Send(long peer, string method, ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null || peer == 0) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(peer, method, new object[] { pkg });
        }

        private static void Reply(long peer, int code)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(code);
            pkg.Write(MyName());
            Send(peer, RpcReply, pkg);
        }

        // Sends the member list to every member (and to the extra peers, e.g. someone just removed).
        private static void SendRoster(List<long> extra)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(Leader);
            pkg.Write(Members.Count);
            foreach (Member m in Members)
            {
                pkg.Write(m.Peer);
                pkg.Write(m.Name);
            }
            byte[] data = pkg.GetArray();
            long me = Me;
            List<long> to = new List<long>();
            foreach (Member m in Members) if (m.Peer != me) to.Add(m.Peer);
            if (extra != null) foreach (long p in extra) if (!to.Contains(p) && p != me) to.Add(p);
            foreach (long p in to) Send(p, RpcRoster, new ZPackage(data));
            Plugin.Debug("[party] roster sent to " + to.Count + " (leader " + Leader + ", " + Members.Count + " members)");
        }

        private static void OnInvite(long sender, ZPackage pkg)
        {
            long leader = pkg.ReadLong();
            string name = pkg.ReadString();
            Plugin.Debug("[net] invite from " + name + " (" + sender + "), leader " + leader);
            if (InParty)
            {
                if (Get(sender) == null) Reply(sender, ReplyBusy);
                return;
            }
            _pending = new Invite();
            _pending.From = sender;
            _pending.FromName = name;
            _pending.Leader = leader;
            _pending.At = Time.time;

            Online o = FindOnline(sender);
            if (Plugin.AutoAcceptFriends.Value && Friends.IsFriend(o != null ? o.UserId : "", name))
            {
                Accept();
                return;
            }
            Plugin.Center(name + " invited you to their party\n[" + Plugin.AcceptKey.Value + "] accept    [" + Plugin.DeclineKey.Value + "] decline");
        }

        private static void OnReply(long sender, ZPackage pkg)
        {
            int code = pkg.ReadInt();
            string name = pkg.ReadString();
            SentInvites.Remove(sender);
            if (code == ReplyDeclined) Plugin.Message(name + " declined your party invite");
            else if (code == ReplyBusy) Plugin.Message(name + " is already in a party");
            else if (code == ReplyFull) { Plugin.Message("That party is full"); _joiningLeader = 0; }
            else if (code == ReplyStale) { Plugin.Message("That invite is no longer valid"); _joiningLeader = 0; }
            if (Members.Count == 1 && SentInvites.Count == 0) Members.Clear();
        }

        private static void OnJoin(long sender, ZPackage pkg)
        {
            string name = pkg.ReadString();
            Plugin.Debug("[net] join request from " + name + " (" + sender + ")");
            if (Members.Count == 0 || Leader != Me) { Reply(sender, ReplyStale); return; }
            if (Get(sender) == null)
            {
                if (Members.Count >= Plugin.MaxMembers.Value) { Reply(sender, ReplyFull); return; }
                Member m = new Member();
                m.Peer = sender;
                m.Name = name;
                Online o = FindOnline(sender);
                if (o != null) m.UserId = o.UserId;
                Members.Add(m);
                Plugin.Message(name + " joined the party");
            }
            SentInvites.Remove(sender);
            SendRoster(null);
        }

        private static void OnRoster(long sender, ZPackage pkg)
        {
            long leader = pkg.ReadLong();
            int n = pkg.ReadInt();
            List<Member> list = new List<Member>();
            for (int i = 0; i < n; i++)
            {
                long peer = pkg.ReadLong();
                string name = pkg.ReadString();
                Member m = Get(peer);
                if (m == null) { m = new Member(); m.Peer = peer; }
                m.Name = name;
                list.Add(m);
            }

            bool fromLeader = InParty && sender == Leader;
            bool fromJoin = sender == _joiningLeader && Time.time - _joiningAt < 30f;
            bool takeover = InParty && leader == sender && Get(sender) != null;
            if (!fromLeader && !fromJoin && !takeover)
            {
                Plugin.Debug("[net] ignored a member list from " + sender);
                return;
            }

            long me = Me;
            bool wasIn = InParty;
            bool listed = false;
            foreach (Member m in list) if (m.Peer == me) listed = true;
            if (!listed)
            {
                if (wasIn) Plugin.Message("You are no longer in the party");
                Members.Clear();
                Leader = 0;
                _joiningLeader = 0;
                return;
            }

            if (wasIn)
            {
                foreach (Member old in Members)
                {
                    bool still = false;
                    foreach (Member m in list) if (m.Peer == old.Peer) still = true;
                    if (!still && old.Peer != me) Plugin.Message(old.Name + " left the party");
                }
                foreach (Member m in list)
                    if (m.Peer != me && Get(m.Peer) == null && m.Peer != sender) Plugin.Message(m.Name + " joined the party");
            }
            long oldLeader = Leader;
            Members.Clear();
            Members.AddRange(list);
            Leader = leader;
            _joiningLeader = 0;

            Member lead = Get(leader);
            string leadName = lead != null ? lead.Name : "?";
            if (!wasIn && InParty) Plugin.Message("You joined " + leadName + "'s party");
            else if (wasIn && oldLeader != leader && InParty) Plugin.Message(leader == me ? "You lead the party now" : leadName + " leads the party now");
            if (!InParty) { Plugin.Message("The party broke up"); Members.Clear(); Leader = 0; }
            Plugin.Debug("[party] roster: " + Members.Count + " members, leader " + leadName);
        }

        private static void OnLeave(long sender, ZPackage pkg)
        {
            if (Leader != Me) return;
            Member m = Get(sender);
            if (m == null) return;
            Members.Remove(m);
            Plugin.Message(m.Name + " left the party");
            SendRoster(null);
            if (Members.Count < 2) { Members.Clear(); Leader = 0; }
        }

        private static readonly System.Reflection.FieldInfo RightField = HarmonyLib.AccessTools.Field(typeof(Humanoid), "m_rightItem");
        private static readonly System.Reflection.FieldInfo LeftField = HarmonyLib.AccessTools.Field(typeof(Humanoid), "m_leftItem");
        private static ItemDrop.ItemData RightItem(Humanoid h) { return RightField != null ? RightField.GetValue(h) as ItemDrop.ItemData : null; }
        private static ItemDrop.ItemData LeftItem(Humanoid h) { return LeftField != null ? LeftField.GetValue(h) as ItemDrop.ItemData : null; }

        private static void SendVitals()
        {
            Player p = Player.m_localPlayer;
            Member self = Self();
            ZPackage pkg = new ZPackage();
            if (p != null)
            {
                pkg.Write(p.GetHealth());
                pkg.Write(p.GetMaxHealth());
                pkg.Write(p.GetStamina());
                pkg.Write(p.GetMaxStamina());
                pkg.Write(p.GetEitr());
                pkg.Write(p.GetMaxEitr());
                ItemDrop.ItemData item = RightItem(p);
                if (item == null) item = LeftItem(p);
                pkg.Write(item != null && item.m_dropPrefab != null ? item.m_dropPrefab.name : "");
                pkg.Write(true);
                pkg.Write(p.transform.position);
                List<StatusEffect> effects = p.GetSEMan().GetStatusEffects();
                List<StatusEffect> shown = new List<StatusEffect>();
                foreach (StatusEffect se in effects)
                    if (se != null && se.m_icon != null && shown.Count < 16) shown.Add(se);
                pkg.Write(shown.Count);
                foreach (StatusEffect se in shown)
                {
                    pkg.Write(se.NameHash());
                    pkg.Write(se.m_ttl > 0f ? Mathf.Max(0f, se.GetRemaningTime()) : -1f);
                }
            }
            else
            {
                // Dead and waiting to respawn.
                pkg.Write(0f); pkg.Write(self != null ? self.MaxHp : 1f);
                pkg.Write(0f); pkg.Write(1f);
                pkg.Write(0f); pkg.Write(0f);
                pkg.Write("");
                pkg.Write(false);
                pkg.Write(Vector3.zero);
                pkg.Write(0);
            }
            byte[] data = pkg.GetArray();
            long me = Me;
            foreach (Member m in Members)
                if (m.Peer != me) Send(m.Peer, RpcVitals, new ZPackage(data));
        }

        private static void OnVitals(long sender, ZPackage pkg)
        {
            Member m = Get(sender);
            if (m == null || !InParty) return;
            m.Hp = pkg.ReadSingle();
            m.MaxHp = pkg.ReadSingle();
            m.Stam = pkg.ReadSingle();
            m.MaxStam = pkg.ReadSingle();
            m.Eitr = pkg.ReadSingle();
            m.MaxEitr = pkg.ReadSingle();
            m.Weapon = pkg.ReadString();
            m.HasPos = pkg.ReadBool();
            m.Pos = pkg.ReadVector3();
            int n = pkg.ReadInt();
            m.Buffs.Clear();
            for (int i = 0; i < n; i++)
            {
                Buff b = new Buff();
                b.Hash = pkg.ReadInt();
                b.Remaining = pkg.ReadSingle();
                m.Buffs.Add(b);
            }
            if (m.Hp <= 0f) { if (m.DeadSince < 0f) m.DeadSince = Time.time; }
            else m.DeadSince = -1f;
            m.VitalsAt = Time.time;
        }

        internal static bool HasPendingInvite { get { return _pending != null; } }
    }
}
