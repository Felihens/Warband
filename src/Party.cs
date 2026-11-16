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

    // The guild itself (who belongs to it, and in which role) is kept by Guild/GuildData and survives
    // between sessions. This class is the live part: who of the guild is online right now (Members,
    // which includes you), plus the messages that go player to player through the server (the server
    // itself needs no mod). Every member sends its own health, stamina, eitr, effects and weapon to the
    // other online members; copies of the guild are exchanged whenever two members meet or it changes.
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
            public string GuildName = "";
            public float At;
        }

        private class Sent
        {
            public string Name = "";
            public float At;
        }

        // Meeting another member: our copy is sent until they answer with theirs.
        private class Hand
        {
            public int Tries;
            public float NextAt;
            public bool Done;
        }

        private const string RpcInvite = "Warband_Invite";
        private const string RpcReply = "Warband_Reply";
        private const string RpcJoin = "Warband_Join";
        private const string RpcGuild = "Warband_Guild";
        private const string RpcVitals = "Warband_Vitals";

        private const int ReplyDeclined = 0, ReplyBusy = 1, ReplyFull = 2, ReplyStale = 3;
        private const float InviteTimeout = 30f;
        private const float VitalsInterval = 0.5f;
        private const float GoneAfter = 10f;
        private const float HandRetry = 5f;
        private const int HandTries = 6;

        internal static readonly List<Member> Members = new List<Member>();   // online guild members, includes you
        internal static readonly List<GuildEntry> OfflineMembers = new List<GuildEntry>();   // guild members not online, excludes you
        internal static readonly List<Online> OnlinePlayers = new List<Online>();  // excludes you

        private static Invite _pending;
        private static long _joiningFrom;
        private static float _joiningAt;
        private static readonly Dictionary<long, Sent> SentInvites = new Dictionary<long, Sent>();
        private static readonly Dictionary<long, Hand> Hands = new Dictionary<long, Hand>();
        private static ZRoutedRpc _registeredOn;
        private static float _nextVitals, _nextCheck;

        internal static long Me { get { return ZNet.GetUID(); } }
        internal static bool InParty { get { return Members.Count >= 2; } }

        internal static string PendingFrom { get { return _pending != null ? _pending.FromName : null; } }
        internal static string PendingGuild { get { return _pending != null ? _pending.GuildName : null; } }
        internal static bool HasPendingInvite { get { return _pending != null; } }

        // The leader of your guild when they are online, else 0.
        internal static long Leader
        {
            get
            {
                if (Guild.Current == null) return 0;
                Member m = GetByUser(Guild.Current.LeaderId());
                return m != null ? m.Peer : 0;
            }
        }

        internal static bool IsLeader { get { return Guild.Exists && Guild.Current.RoleOf(Guild.MyId) == GuildRole.Leader; } }

        internal static GuildRole MyRole { get { return Guild.Current != null ? Guild.Current.RoleOf(Guild.MyId) : GuildRole.Member; } }

        // You may invite when you have no guild yet (that founds one) or are its leader or an officer.
        internal static bool CanInvite { get { return !Guild.Exists || Guild.Current.CanInvite(Guild.MyId); } }

        internal static bool GuildFull { get { return Guild.Exists && Guild.Current.ActiveCount >= Plugin.MaxMembers.Value; } }

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

        internal static Member GetByUser(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return null;
            foreach (Member m in Members) if (m.UserId == userId) return m;
            return null;
        }

        // An ally: in your guild, online, and not you.
        internal static bool IsAlly(long peer)
        {
            return InParty && peer != 0 && peer != Me && Get(peer) != null;
        }

        internal static Online FindOnline(long peer)
        {
            foreach (Online o in OnlinePlayers) if (o.Peer == peer) return o;
            return null;
        }

        internal static Online FindOnlineByUser(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return null;
            foreach (Online o in OnlinePlayers) if (o.UserId == userId) return o;
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
                Handshakes(now);
                SyncMembers(now);
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
            rpc.Register<ZPackage>(RpcGuild, new Action<long, ZPackage>(OnGuild));
            rpc.Register<ZPackage>(RpcVitals, new Action<long, ZPackage>(OnVitals));
            rpc.Register<ZPackage>(PartyChat.Rpc, new Action<long, ZPackage>(PartyChat.OnMessage));
            rpc.Register<ZPackage>(Gift.Rpc, new Action<long, ZPackage>(Gift.OnMessage));
            _registeredOn = rpc;
            Plugin.Debug("[net] messages registered, my id " + Me);
        }

        // Forgets everything live. The guild itself stays (only a draft nobody joined is dropped).
        private static void Reset(string why)
        {
            if (Members.Count > 0) Plugin.Debug("[party] cleared (" + why + ")");
            Members.Clear();
            OfflineMembers.Clear();
            _pending = null;
            _joiningFrom = 0;
            SentInvites.Clear();
            Hands.Clear();
            OnlinePlayers.Clear();
            if (Guild.Current != null && Guild.Current.Entries.Count < 2) Guild.Current = null;
        }

        private static void RefreshOnline()
        {
            OnlinePlayers.Clear();
            long me = Me;
            string myName = MyName();
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                long peer = info.m_characterID.UserID;
                string id = info.m_userInfo.m_id.ToString() ?? "";
                if (peer == me || (peer == 0 && info.m_name == myName))
                {
                    if (id.Length > 0 && id != Guild.MyId)
                    {
                        Guild.MyId = id;
                        Plugin.Debug("[guild] my platform id is " + id);
                    }
                    continue;
                }
                Online o = new Online();
                o.Peer = peer;
                o.Name = info.m_name ?? "";
                o.UserId = id;
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
                Plugin.Message("The guild invite from " + _pending.FromName + " expired");
                _pending = null;
            }
            if (_joiningFrom != 0 && now - _joiningAt > 15f)
            {
                Plugin.Message("The guild didn't answer");
                _joiningFrom = 0;
            }
            List<long> expired = null;
            foreach (KeyValuePair<long, Sent> kv in SentInvites)
                if (now - kv.Value.At > InviteTimeout)
                {
                    if (expired == null) expired = new List<long>();
                    expired.Add(kv.Key);
                }
            if (expired != null)
                foreach (long peer in expired)
                {
                    Plugin.Message(SentInvites[peer].Name + " didn't answer your invite (do they have Warband?)");
                    SentInvites.Remove(peer);
                }
            // A guild of just you that nobody joined is no guild.
            if (Guild.Current != null && Guild.Current.Entries.Count == 1 && SentInvites.Count == 0) Guild.Current = null;
        }

        // ---------------------------------------------------------------- meeting other members

        // Whenever a member of the guild (even a removed one, who has to learn it) is online, our copy of
        // the guild is sent to them until they answer with theirs.
        private static void Handshakes(float now)
        {
            List<long> stale = new List<long>();
            foreach (long peer in Hands.Keys) if (FindOnline(peer) == null && Get(peer) == null) stale.Add(peer);
            foreach (long peer in stale) Hands.Remove(peer);

            GuildData g = Guild.Current;
            if (g == null || Guild.MyId.Length == 0 || g.Find(Guild.MyId) == null) return;   // also while leaving
            foreach (Online o in OnlinePlayers)
            {
                if (o.Peer == 0 || g.Find(o.UserId) == null) continue;   // peer 0: still loading, or dead and waiting to respawn
                Hand h;
                if (!Hands.TryGetValue(o.Peer, out h)) { h = new Hand(); Hands[o.Peer] = h; }
                if (h.Done || h.Tries >= HandTries || now < h.NextAt) continue;
                h.Tries++;
                h.NextAt = now + HandRetry;
                SendGuild(o.Peer, false);
            }
        }

        private static bool Active(GuildData g, string userId)
        {
            GuildEntry e = g.Find(userId);
            return e != null && !e.Removed;
        }

        // Keeps Members (you plus the online members who answered) and OfflineMembers in line with the guild.
        private static void SyncMembers(float now)
        {
            GuildData g = Guild.Current;
            string me = Guild.MyId;
            if (g == null || me.Length == 0 || !Active(g, me))
            {
                Members.Clear();
                OfflineMembers.Clear();
                return;
            }

            Member self = Self();
            if (self == null)
            {
                self = new Member();
                self.Peer = Me;
                Members.Insert(0, self);
            }
            self.Name = MyName();
            self.UserId = me;

            for (int i = Members.Count - 1; i >= 0; i--)
            {
                Member m = Members[i];
                if (m.Peer == Me) continue;
                bool active = Active(g, m.UserId);
                Online current = FindOnlineByUser(m.UserId);
                bool moved = current != null && current.Peer != 0 && current.Peer != m.Peer;   // reconnected under a new id (a dead ally shows peer 0 until respawn)
                bool here = FindOnline(m.Peer) != null || now - m.VitalsAt < 6f;
                if (here) m.MissingSince = -1f;
                else if (m.MissingSince < 0f) m.MissingSince = now;
                bool drop = !active || moved || (!here && now - m.MissingSince > GoneAfter);
                if (!drop) continue;
                Members.RemoveAt(i);
                if (active && !moved) Plugin.Message(m.Name + " went offline");
            }

            foreach (GuildEntry e in g.Active())
            {
                if (e.UserId == me || GetByUser(e.UserId) != null) continue;
                Online o = FindOnlineByUser(e.UserId);
                Hand h;
                if (o == null || !Hands.TryGetValue(o.Peer, out h) || !h.Done) continue;
                Member m = new Member();
                m.Peer = o.Peer;
                m.Name = o.Name;
                m.UserId = e.UserId;
                Members.Add(m);
                Plugin.Message(o.Name + " is online");
            }

            OfflineMembers.Clear();
            foreach (GuildEntry e in g.Active())
                if (e.UserId != me && GetByUser(e.UserId) == null) OfflineMembers.Add(e);
        }

        // ---------------------------------------------------------------- actions

        internal static void InviteTo(long peer, string name)
        {
            if (peer == 0 || peer == Me) { Plugin.Message(name + " can't be invited right now"); return; }
            Online target = FindOnline(peer);
            if (Guild.MyId.Length == 0) { Plugin.Message("Your guild id isn't known yet, try again in a moment"); return; }
            if (target == null || target.UserId.Length == 0) { Plugin.Message(name + " can't be identified yet"); return; }
            GuildData g = Guild.Exists ? Guild.Current : null;
            if (g != null)
            {
                if (!g.CanInvite(Guild.MyId)) { Plugin.Message("Only the leader and officers can invite"); return; }
                if (Active(g, target.UserId)) { Plugin.Message(name + " is already in your guild"); return; }
                if (g.ActiveCount >= Plugin.MaxMembers.Value) { Plugin.Message("Your guild is full"); return; }
            }
            else
            {
                g = GuildData.Create(Guild.MyId, MyName(), MyName() + "'s guild");
                Guild.Current = g;
            }
            ZPackage pkg = new ZPackage();
            pkg.Write(g.Id);
            pkg.Write(g.Name);
            pkg.Write(MyName());
            Send(peer, RpcInvite, pkg);
            Sent s = new Sent();
            s.Name = name;
            s.At = Time.time;
            SentInvites[peer] = s;
            Plugin.Message("Guild invite sent to " + name);
        }

        internal static void Accept()
        {
            if (_pending == null) return;
            if (Guild.Exists) { Plugin.Message("Leave your guild first"); return; }
            if (Guild.MyId.Length == 0) { Plugin.Message("Your guild id isn't known yet, try again in a moment"); return; }
            if (_joiningFrom != 0) { Plugin.Message("You are already joining a guild"); return; }
            Guild.Current = null;   // a draft of your own, if any
            SentInvites.Clear();
            _joiningFrom = _pending.From;
            _joiningAt = Time.time;
            ZPackage pkg = new ZPackage();
            pkg.Write(MyName());
            Send(_pending.From, RpcJoin, pkg);
            Plugin.Message("Joining " + _pending.GuildName + "...");
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
            GuildData g = Guild.Current;
            if (g == null) return;
            if (Guild.Exists)
            {
                // The copy stays (with you removed) until a member has taken the news, even across sessions.
                g.Leave(Guild.MyId);
                Guild.Save();   // deletes the file when nobody is left
                SendGuildToAll();
            }
            else Guild.Clear();   // a draft
            Members.Clear();
            OfflineMembers.Clear();
            Hands.Clear();
            Plugin.Message("You left the guild");
        }

        internal static void Kick(string userId)
        {
            GuildData g = Guild.Current;
            GuildEntry e = g != null ? g.Find(userId) : null;
            if (e == null || !g.Remove(Guild.MyId, userId)) return;
            Edited();
            Plugin.Message(e.Name + " was removed from the guild");
        }

        internal static void SetOfficer(string userId, bool officer)
        {
            GuildData g = Guild.Current;
            GuildEntry e = g != null ? g.Find(userId) : null;
            if (e == null || !g.SetOfficer(Guild.MyId, userId, officer)) return;
            Edited();
            Plugin.Message(e.Name + (officer ? " is an officer now" : " is no longer an officer"));
        }

        internal static void MakeLeader(string userId)
        {
            GuildData g = Guild.Current;
            GuildEntry e = g != null ? g.Find(userId) : null;
            if (e == null || !g.TransferLeader(Guild.MyId, userId)) return;
            Edited();
            Plugin.Message(e.Name + " leads the guild now");
        }

        internal static void Rename(string newName)
        {
            GuildData g = Guild.Current;
            if (g == null || !g.Rename(Guild.MyId, newName)) return;
            Edited();
            Plugin.Message("Your guild is now called " + g.Name);
        }

        // After an edit of ours: write it down and tell everybody who is online (including whoever was removed).
        private static void Edited()
        {
            Guild.Save();
            SendGuildToAll();
            SyncMembers(Time.time);
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

        // isReply = true means "no answer needed" (an answer, or news after an edit).
        private static void SendGuild(long peer, bool isReply)
        {
            GuildData g = Guild.Current;
            if (g == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(g.Id);
            pkg.Write(g.Name);
            pkg.Write(g.NameVer);
            pkg.Write(isReply);
            List<GuildEntry> entries = g.Snapshot();
            pkg.Write(entries.Count);
            foreach (GuildEntry e in entries)
            {
                pkg.Write(e.UserId);
                pkg.Write(e.Name);
                pkg.Write((int)e.Role);
                pkg.Write(e.Removed);
                pkg.Write(e.Ver);
                pkg.Write(e.Editor);
                pkg.Write(e.Seq);
            }
            Send(peer, RpcGuild, pkg);
        }

        private static void SendGuildToAll()
        {
            GuildData g = Guild.Current;
            if (g == null) return;
            foreach (Online o in OnlinePlayers)
                if (g.Find(o.UserId) != null) SendGuild(o.Peer, true);
        }

        private static void OnGuild(long sender, ZPackage pkg)
        {
            GuildData incoming;
            bool isReply;
            try
            {
                string id = pkg.ReadString();
                string name = pkg.ReadString();
                int nameVer = pkg.ReadInt();
                isReply = pkg.ReadBool();
                int n = pkg.ReadInt();
                if (n < 0 || n > 64) return;
                List<GuildEntry> entries = new List<GuildEntry>();
                for (int i = 0; i < n; i++)
                {
                    GuildEntry e = new GuildEntry();
                    e.UserId = pkg.ReadString();
                    e.Name = pkg.ReadString();
                    e.Role = (GuildRole)pkg.ReadInt();
                    e.Removed = pkg.ReadBool();
                    e.Ver = pkg.ReadInt();
                    e.Editor = pkg.ReadString();
                    e.Seq = pkg.ReadInt();
                    entries.Add(e);
                }
                incoming = GuildData.FromSnapshot(id, name, nameVer, entries);
            }
            catch (Exception ex)
            {
                Plugin.Debug("[net] unreadable guild message from " + sender + ": " + ex.Message);
                return;
            }
            if (incoming == null) return;
            Online from = FindOnline(sender);
            if (from == null || from.UserId.Length == 0 || Guild.MyId.Length == 0) return;

            GuildData mine = Guild.Current;
            if (mine != null && mine.Leaving(Guild.MyId))
            {
                // We left and are waiting to hand the news on: the first member who answers has it.
                if (mine.Apply(incoming, from.UserId, Guild.MyId) != ApplyResult.Ignored) { Guild.Clear(); Hands.Clear(); }
                return;
            }
            if (mine == null || !Guild.Exists)
            {
                // Only the player who invited us may hand us a guild, and only right after we said yes.
                if (sender != _joiningFrom || Time.time - _joiningAt > 30f) return;
                if (!Active(incoming, Guild.MyId) || !Active(incoming, from.UserId)) return;
                Guild.Current = incoming;
                Guild.Save();
                _joiningFrom = 0;
                Plugin.Message("You joined " + incoming.Name);
                SendGuild(sender, true);
                return;
            }

            GuildData before = mine.Clone();
            ApplyResult res = mine.Apply(incoming, from.UserId, Guild.MyId);
            if (res == ApplyResult.Ignored) return;
            if (res == ApplyResult.SelfRemoved)
            {
                Plugin.Message("You were removed from " + mine.Name);
                Guild.Clear();
                Members.Clear();
                OfflineMembers.Clear();
                Hands.Clear();
                return;
            }
            if (res == ApplyResult.Merged)
            {
                Guild.Save();
                Announce(before, mine);
                if (Guild.Current == null) return;
            }
            Hand h;
            if (!Hands.TryGetValue(sender, out h)) { h = new Hand(); Hands[sender] = h; }
            h.Done = true;
            if (!isReply) SendGuild(sender, true);
        }

        // Tells the player what changed in a copy that arrived.
        private static void Announce(GuildData before, GuildData after)
        {
            string me = Guild.MyId;
            foreach (GuildEntry e in after.Active())
                if (e.UserId != me && !Active(before, e.UserId)) Plugin.Message(e.Name + " joined the guild");
            foreach (GuildEntry e in before.Active())
                if (e.UserId != me && !Active(after, e.UserId)) Plugin.Message(e.Name + " left the guild");
            string was = before.LeaderId(), now = after.LeaderId();
            if (now != null && now != was)
            {
                GuildEntry lead = after.Find(now);
                Plugin.Message(now == me ? "You lead the guild now" : lead.Name + " leads the guild now");
            }
            if (before.RoleOf(me) != GuildRole.Officer && after.RoleOf(me) == GuildRole.Officer) Plugin.Message("You are an officer now");
        }

        private static void OnInvite(long sender, ZPackage pkg)
        {
            string guildId = pkg.ReadString();
            string guildName = pkg.ReadString();
            string name = pkg.ReadString();
            Plugin.Debug("[net] invite from " + name + " (" + sender + ") to guild " + guildId);
            if (Guild.Exists)
            {
                Reply(sender, ReplyBusy);
                return;
            }
            _pending = new Invite();
            _pending.From = sender;
            _pending.FromName = name;
            _pending.GuildName = guildName;
            _pending.At = Time.time;

            Online o = FindOnline(sender);
            if (Plugin.AutoAcceptFriends.Value && o != null && Friends.IsFriendId(o.UserId))
            {
                Accept();
                if (_pending == null) return;   // else Accept refused: show the prompt instead
            }
            Plugin.Center(name + " invited you to the guild " + guildName + "\n[" + Plugin.AcceptKey.Value + "] accept    [" + Plugin.DeclineKey.Value + "] decline");
        }

        private static void OnReply(long sender, ZPackage pkg)
        {
            int code = pkg.ReadInt();
            string name = pkg.ReadString();
            SentInvites.Remove(sender);
            if (code == ReplyDeclined) Plugin.Message(name + " declined your guild invite");
            else if (code == ReplyBusy) Plugin.Message(name + " is already in a guild");
            else if (code == ReplyFull) { Plugin.Message("That guild is full"); _joiningFrom = 0; }
            else if (code == ReplyStale) { Plugin.Message("That invite is no longer valid"); _joiningFrom = 0; }
        }

        // Someone we invited said yes.
        private static void OnJoin(long sender, ZPackage pkg)
        {
            string name = pkg.ReadString();
            Plugin.Debug("[net] join request from " + name + " (" + sender + ")");
            GuildData g = Guild.Current;
            Online o = FindOnline(sender);
            if (g == null || o == null || o.UserId.Length == 0 || !SentInvites.ContainsKey(sender) || !g.CanInvite(Guild.MyId))
            {
                Reply(sender, ReplyStale);
                return;
            }
            if (!Active(g, o.UserId))
            {
                if (!g.Invite(Guild.MyId, o.UserId, name, Plugin.MaxMembers.Value)) { Reply(sender, ReplyFull); return; }
                Plugin.Message(name + " joined the guild");
            }
            SentInvites.Remove(sender);
            Guild.Save();
            SendGuild(sender, false);   // the new member needs the whole guild first
            foreach (Online other in OnlinePlayers)
                if (other.Peer != sender && g.Find(other.UserId) != null) SendGuild(other.Peer, true);
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
    }
}
