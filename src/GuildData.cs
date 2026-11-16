using System;
using System.Collections.Generic;

namespace Warband
{
    // The guild's data and rules. No Unity types here, so tests/GuildTests.cs can compile it on its own.

    internal enum GuildRole { Member = 0, Officer = 1, Leader = 2 }

    // What Apply did with a copy of the guild that arrived from another player.
    internal enum ApplyResult { Ignored, Unchanged, Merged, SelfRemoved }

    // One member of the guild. Ver goes up by one on every edit; Removed entries ("tombstones") stay
    // so that a removal reaches copies that were offline when it happened.
    internal class GuildEntry
    {
        public string UserId = "";
        public string Name = "";
        public GuildRole Role;
        public bool Removed;
        public int Ver;
        public string Editor = "";
        public int Seq;

        public GuildEntry Clone()
        {
            return (GuildEntry)MemberwiseClone();
        }
    }

    internal class GuildData
    {
        public string Id = "";
        public string Name = "";
        public int NameVer;
        public readonly Dictionary<string, GuildEntry> Entries = new Dictionary<string, GuildEntry>(StringComparer.Ordinal);

        internal static GuildData Create(string founderId, string founderName, string guildName)
        {
            GuildData g = new GuildData();
            g.Id = Guid.NewGuid().ToString("N");
            g.Name = CleanName(guildName);
            g.NameVer = 1;
            GuildEntry e = new GuildEntry();
            e.UserId = founderId;
            e.Name = CleanName(founderName);
            e.Role = GuildRole.Leader;
            e.Ver = 1;
            e.Editor = founderId;
            e.Seq = 1;
            g.Entries[founderId] = e;
            return g;
        }

        internal GuildData Clone()
        {
            GuildData g = new GuildData();
            g.Id = Id;
            g.Name = Name;
            g.NameVer = NameVer;
            foreach (KeyValuePair<string, GuildEntry> kv in Entries) g.Entries[kv.Key] = kv.Value.Clone();
            return g;
        }

        internal GuildEntry Find(string userId)
        {
            GuildEntry e;
            return userId != null && Entries.TryGetValue(userId, out e) ? e : null;
        }

        // Members who have not been removed, oldest first (Seq, then UserId).
        internal List<GuildEntry> Active()
        {
            List<GuildEntry> list = new List<GuildEntry>();
            foreach (GuildEntry e in Entries.Values) if (!e.Removed) list.Add(e);
            list.Sort(delegate (GuildEntry a, GuildEntry b)
            {
                if (a.Seq != b.Seq) return a.Seq.CompareTo(b.Seq);
                return string.CompareOrdinal(a.UserId, b.UserId);
            });
            return list;
        }

        internal int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (GuildEntry e in Entries.Values) if (!e.Removed) n++;
                return n;
            }
        }

        // Worked out from the entries, never stored, so every copy agrees. Exactly one active Leader
        // is the leader; with none or several, the oldest officer (then the oldest member) leads.
        internal string LeaderId()
        {
            List<GuildEntry> active = Active();
            if (active.Count == 0) return null;
            GuildEntry leader = null;
            int leaders = 0;
            foreach (GuildEntry e in active) if (e.Role == GuildRole.Leader) { leaders++; leader = e; }
            if (leaders == 1) return leader.UserId;
            foreach (GuildEntry e in active) if (e.Role == GuildRole.Officer) return e.UserId;
            return active[0].UserId;
        }

        // The entry with the higher Ver wins; on a tie a removal wins (a re-invite always has a higher Ver,
        // so it can't be lost this way), and after that the higher Editor (ordinal).
        private static bool Beats(GuildEntry a, GuildEntry b)
        {
            if (a.Ver != b.Ver) return a.Ver > b.Ver;
            if (a.Removed != b.Removed) return a.Removed;
            return string.CompareOrdinal(a.Editor, b.Editor) > 0;
        }

        // You left (or were removed) but your copy still holds the guild, so the removal can be handed on to
        // the members who haven't heard of it yet.
        internal bool Leaving(string userId)
        {
            GuildEntry e = Find(userId);
            return e != null && e.Removed && Entries.Count >= 2;
        }

        // Folds another copy of the same guild into this one. True when something changed.
        internal bool Merge(GuildData other)
        {
            if (other == null || other.Id != Id) return false;
            bool changed = false;
            foreach (KeyValuePair<string, GuildEntry> kv in other.Entries)
            {
                GuildEntry mine;
                if (!Entries.TryGetValue(kv.Key, out mine) || Beats(kv.Value, mine))
                {
                    Entries[kv.Key] = kv.Value.Clone();
                    changed = true;
                }
            }
            if (other.NameVer > NameVer)
            {
                Name = other.Name;
                NameVer = other.NameVer;
                changed = true;
            }
            return changed;
        }

        // A copy of the guild arrived from senderId. Only members (not removed, in OUR copy before
        // merging) of the same guild are listened to.
        internal ApplyResult Apply(GuildData incoming, string senderId, string myId)
        {
            if (incoming == null || incoming.Id != Id) return ApplyResult.Ignored;
            GuildEntry sender = Find(senderId);
            if (sender == null || sender.Removed) return ApplyResult.Ignored;
            bool changed = Merge(incoming);
            GuildEntry me = Find(myId);
            if (me != null && me.Removed) return ApplyResult.SelfRemoved;
            return changed ? ApplyResult.Merged : ApplyResult.Unchanged;
        }

        // ---------------------------------------------------------------- permissions

        // Tabs and line breaks would break the file format; names are kept short.
        internal static string CleanName(string s)
        {
            if (s == null) return "";
            s = s.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (s.Length > 32) s = s.Substring(0, 32).TrimEnd();
            return s;
        }

        private bool IsActive(string userId)
        {
            GuildEntry e = Find(userId);
            return e != null && !e.Removed;
        }

        internal GuildRole RoleOf(string userId)
        {
            if (!IsActive(userId)) return GuildRole.Member;
            if (LeaderId() == userId) return GuildRole.Leader;
            return Find(userId).Role == GuildRole.Member ? GuildRole.Member : GuildRole.Officer;
        }

        internal bool CanInvite(string actor)
        {
            return RoleOf(actor) != GuildRole.Member;
        }

        internal bool CanRemove(string actor, string target)
        {
            if (!IsActive(actor) || !IsActive(target) || actor == target) return false;
            GuildRole a = RoleOf(actor), t = RoleOf(target);
            if (t == GuildRole.Leader) return false;
            if (a == GuildRole.Leader) return true;
            return a == GuildRole.Officer && t == GuildRole.Member;
        }

        internal bool CanSetOfficer(string actor, string target)
        {
            return RoleOf(actor) == GuildRole.Leader && IsActive(actor) && IsActive(target) && actor != target;
        }

        internal bool CanTransfer(string actor, string target)
        {
            return CanSetOfficer(actor, target);
        }

        internal bool CanRename(string actor)
        {
            return IsActive(actor) && RoleOf(actor) == GuildRole.Leader;
        }

        // ---------------------------------------------------------------- edits
        // Each edit raises Ver by one and records who made it; false means nothing changed.

        private static void Bump(GuildEntry e, string actor)
        {
            e.Ver++;
            e.Editor = actor;
        }

        private int NextSeq()
        {
            int max = 0;
            foreach (GuildEntry e in Entries.Values) if (e.Seq > max) max = e.Seq;
            return max + 1;
        }

        internal bool Invite(string actor, string userId, string name, int maxMembers)
        {
            if (string.IsNullOrEmpty(userId) || !CanInvite(actor) || IsActive(userId) || ActiveCount >= maxMembers) return false;
            string clean = CleanName(name);
            if (clean.Length == 0) clean = "?";
            GuildEntry e = Find(userId);
            if (e == null)
            {
                e = new GuildEntry();
                e.UserId = userId;
                Entries[userId] = e;
            }
            e.Name = clean;
            e.Role = GuildRole.Member;
            e.Removed = false;
            e.Seq = NextSeq();
            Bump(e, actor);
            return true;
        }

        internal bool Remove(string actor, string target)
        {
            if (!CanRemove(actor, target)) return false;
            GuildEntry e = Find(target);
            e.Removed = true;
            Bump(e, actor);
            return true;
        }

        internal bool SetOfficer(string actor, string target, bool officer)
        {
            if (!CanSetOfficer(actor, target) || RoleOf(target) == GuildRole.Leader) return false;
            GuildRole want = officer ? GuildRole.Officer : GuildRole.Member;
            GuildEntry e = Find(target);
            if (e.Role == want) return false;
            e.Role = want;
            Bump(e, actor);
            return true;
        }

        internal bool TransferLeader(string actor, string target)
        {
            if (!CanTransfer(actor, target)) return false;
            GuildEntry from = Find(actor), to = Find(target);
            from.Role = GuildRole.Officer;
            Bump(from, actor);
            to.Role = GuildRole.Leader;
            Bump(to, actor);
            return true;
        }

        internal bool Rename(string actor, string newName)
        {
            if (!CanRename(actor)) return false;
            string clean = CleanName(newName);
            if (clean.Length == 0) return false;
            Name = clean;
            NameVer++;
            return true;
        }

        // Leaving is an edit of your own entry. A leaving leader hands the guild to the oldest officer,
        // or else the oldest member, in the same edit.
        internal bool Leave(string me)
        {
            if (!IsActive(me)) return false;
            if (LeaderId() == me)
            {
                GuildEntry heir = null;
                foreach (GuildEntry e in Active())
                {
                    if (e.UserId == me) continue;
                    if (e.Role == GuildRole.Officer) { heir = e; break; }
                    if (heir == null) heir = e;
                }
                if (heir != null)
                {
                    heir.Role = GuildRole.Leader;
                    Bump(heir, me);
                }
            }
            GuildEntry self = Find(me);
            self.Removed = true;
            Bump(self, me);
            return true;
        }

        // ---------------------------------------------------------------- text format

        // guild<TAB>Id<TAB>Name<TAB>NameVer
        // m<TAB>UserId<TAB>Name<TAB>Role<TAB>Removed(0/1)<TAB>Ver<TAB>Editor<TAB>Seq
        internal string[] ToLines()
        {
            List<string> lines = new List<string>();
            lines.Add("# Warband guild. The mod keeps this file up to date; please don't edit it by hand.");
            lines.Add("guild\t" + Id + "\t" + Name + "\t" + NameVer);
            foreach (GuildEntry e in Active()) lines.Add(EntryLine(e));
            List<GuildEntry> gone = new List<GuildEntry>();
            foreach (GuildEntry e in Entries.Values) if (e.Removed) gone.Add(e);
            gone.Sort(delegate (GuildEntry a, GuildEntry b) { return string.CompareOrdinal(a.UserId, b.UserId); });
            foreach (GuildEntry e in gone) lines.Add(EntryLine(e));
            return lines.ToArray();
        }

        private static string EntryLine(GuildEntry e)
        {
            return "m\t" + e.UserId + "\t" + e.Name + "\t" + (int)e.Role + "\t" + (e.Removed ? "1" : "0") + "\t" + e.Ver + "\t" + e.Editor + "\t" + e.Seq;
        }

        // Null when the text is not a valid guild (anything odd makes the whole file invalid).
        internal static GuildData FromLines(string[] lines)
        {
            if (lines == null) return null;
            GuildData g = null;
            foreach (string raw in lines)
            {
                string line = raw.TrimEnd('\r', '\n');
                if (line.Trim().Length == 0 || line.StartsWith("#")) continue;
                string[] f = line.Split('\t');
                if (f[0] == "guild")
                {
                    int nameVer;
                    if (g != null || f.Length != 4 || f[1].Length == 0 || !int.TryParse(f[3], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out nameVer) || nameVer < 1) return null;
                    g = new GuildData();
                    g.Id = f[1];
                    g.Name = f[2];
                    g.NameVer = nameVer;
                }
                else if (f[0] == "m")
                {
                    int role, ver, seq;
                    if (g == null || f.Length != 8 || f[1].Length == 0 || g.Entries.ContainsKey(f[1])) return null;
                    System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
                    if (!int.TryParse(f[3], System.Globalization.NumberStyles.Integer, inv, out role) || role < 0 || role > 2) return null;
                    if (f[4] != "0" && f[4] != "1") return null;
                    if (!int.TryParse(f[5], System.Globalization.NumberStyles.Integer, inv, out ver) || ver < 1) return null;
                    if (!int.TryParse(f[7], System.Globalization.NumberStyles.Integer, inv, out seq) || seq < 1) return null;
                    GuildEntry e = new GuildEntry();
                    e.UserId = f[1];
                    e.Name = f[2];
                    e.Role = (GuildRole)role;
                    e.Removed = f[4] == "1";
                    e.Ver = ver;
                    e.Editor = f[6];
                    e.Seq = seq;
                    g.Entries[e.UserId] = e;
                }
                else return null;
            }
            return g != null && g.Entries.Count > 0 ? g : null;
        }

        // ---------------------------------------------------------------- network

        internal List<GuildEntry> Snapshot()
        {
            List<GuildEntry> list = new List<GuildEntry>();
            foreach (GuildEntry e in Entries.Values) list.Add(e.Clone());
            return list;
        }

        // Builds a guild from what another player sent. Null when anything in it is out of range.
        internal static GuildData FromSnapshot(string id, string name, int nameVer, IEnumerable<GuildEntry> entries)
        {
            if (string.IsNullOrEmpty(id) || nameVer < 1 || entries == null) return null;
            GuildData g = new GuildData();
            g.Id = id;
            g.Name = CleanName(name);
            g.NameVer = nameVer;
            foreach (GuildEntry src in entries)
            {
                if (src == null || string.IsNullOrEmpty(src.UserId) || src.Ver < 1 || src.Seq < 1 || (int)src.Role < 0 || (int)src.Role > 2) return null;
                GuildEntry e = src.Clone();
                e.Name = CleanName(e.Name);
                e.Editor = e.Editor ?? "";
                g.Entries[e.UserId] = e;
            }
            return g.Entries.Count > 0 ? g : null;
        }
    }

    // The guild's file on disk. Written in two steps so a crash never leaves half a file.
    internal static class GuildFile
    {
        internal static void Save(string path, GuildData g)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            System.IO.File.WriteAllLines(tmp, g.ToLines());
            System.IO.File.Copy(tmp, path, true);
            System.IO.File.Delete(tmp);
        }

        // Null when there is no file or it is unreadable; a corrupt file is kept as path + ".bak".
        internal static GuildData Load(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path)) return null;
                GuildData g = GuildData.FromLines(System.IO.File.ReadAllLines(path));
                if (g == null) System.IO.File.Copy(path, path + ".bak", true);
                return g;
            }
            catch (Exception) { return null; }
        }

        internal static void Delete(string path)
        {
            try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
            catch (Exception) { }
        }
    }
}
