using System;
using System.Collections.Generic;
using Warband;

// Plain console tests for src/GuildData.cs (no test framework needed).
internal static class GuildTests
{
    private static int _failed, _passed;

    private static void Check(bool ok, string name)
    {
        if (ok) { _passed++; return; }
        _failed++;
        Console.WriteLine("FAIL: " + name);
    }

    // ---- helpers

    private static GuildEntry E(string id, GuildRole role, int ver, string editor, int seq, bool removed)
    {
        GuildEntry e = new GuildEntry();
        e.UserId = id; e.Name = id; e.Role = role; e.Ver = ver; e.Editor = editor; e.Seq = seq; e.Removed = removed;
        return e;
    }

    private static GuildData G(string id, params GuildEntry[] entries)
    {
        GuildData g = new GuildData();
        g.Id = id; g.Name = "Crew"; g.NameVer = 1;
        foreach (GuildEntry e in entries) g.Entries[e.UserId] = e;
        return g;
    }

    private static bool SameEntry(GuildEntry a, GuildEntry b)
    {
        return a.UserId == b.UserId && a.Role == b.Role && a.Removed == b.Removed && a.Ver == b.Ver && a.Editor == b.Editor && a.Seq == b.Seq;
    }

    private static bool Same(GuildData a, GuildData b)
    {
        if (a.Id != b.Id || a.Name != b.Name || a.NameVer != b.NameVer || a.Entries.Count != b.Entries.Count) return false;
        foreach (KeyValuePair<string, GuildEntry> kv in a.Entries)
        {
            GuildEntry o = b.Find(kv.Key);
            if (o == null || !SameEntry(kv.Value, o)) return false;
        }
        return true;
    }

    // ---- task 1: model, merge, effective leader

    private static void Task1()
    {
        GuildData c = GuildData.Create("A", "Ann", "Crew");
        GuildEntry a = c.Find("A");
        Check(a != null && a.Role == GuildRole.Leader && a.Ver == 1 && a.Seq == 1 && a.Editor == "A", "Create_FounderIsLeader entry");
        Check(c.ActiveCount == 1 && c.LeaderId() == "A" && c.NameVer == 1 && c.Id.Length > 0 && c.Name == "Crew", "Create_FounderIsLeader guild");

        // Merge: higher Ver wins, in both directions
        GuildData x = G("g", E("B", GuildRole.Officer, 2, "A", 2, false));
        GuildData y = G("g", E("B", GuildRole.Member, 1, "A", 2, false));
        bool changedY = y.Merge(x);
        bool changedX = x.Merge(y);
        Check(changedY && !changedX && y.Find("B").Ver == 2 && y.Find("B").Role == GuildRole.Officer && x.Find("B").Ver == 2, "Merge_HigherVerWins");

        // Merge: equal Ver, Editor breaks the tie the same way in both directions
        GuildData t1 = G("g", E("B", GuildRole.Officer, 3, "Z", 2, false));
        GuildData t2 = G("g", E("B", GuildRole.Member, 3, "Y", 2, false));
        GuildData t1c = t1.Clone(), t2c = t2.Clone();
        t1.Merge(t2); t2c.Merge(t1c);
        Check(t1.Find("B").Editor == "Z" && t2c.Find("B").Editor == "Z" && Same(t1, t2c), "Merge_TieBrokenByEditor");

        // Merge: three divergent copies converge in every order, and merging again changes nothing
        GuildData p = G("g", E("A", GuildRole.Leader, 1, "A", 1, false), E("B", GuildRole.Member, 2, "A", 2, false), E("P", GuildRole.Member, 1, "A", 3, false));
        GuildData q = G("g", E("A", GuildRole.Leader, 1, "A", 1, false), E("B", GuildRole.Officer, 3, "A", 2, false), E("Q", GuildRole.Member, 1, "A", 4, false));
        GuildData r = G("g", E("A", GuildRole.Leader, 1, "A", 1, false), E("B", GuildRole.Officer, 3, "R", 2, true), E("R", GuildRole.Member, 1, "R", 5, false));
        GuildData[] copies = new GuildData[] { p, q, r };
        int[][] orders = new int[][] { new int[] {0,1,2}, new int[] {0,2,1}, new int[] {1,0,2}, new int[] {1,2,0}, new int[] {2,0,1}, new int[] {2,1,0} };
        GuildData first = null;
        bool allSame = true, idempotent = true;
        foreach (int[] order in orders)
        {
            GuildData acc = copies[order[0]].Clone();
            acc.Merge(copies[order[1]]);
            acc.Merge(copies[order[2]]);
            if (first == null) first = acc; else if (!Same(first, acc)) allSame = false;
            GuildData again = acc.Clone();
            if (again.Merge(acc) || !Same(again, acc)) idempotent = false;
        }
        Check(allSame, "Merge_OrderIndependent");
        Check(idempotent, "Merge_Idempotent");

        // Merge: the guild name follows NameVer
        GuildData n1 = G("g", E("A", GuildRole.Leader, 1, "A", 1, false)); n1.Name = "Old"; n1.NameVer = 1;
        GuildData n2 = n1.Clone(); n2.Name = "New"; n2.NameVer = 2;
        n1.Merge(n2);
        Check(n1.Name == "New" && n1.NameVer == 2, "Merge_NameByNameVer newer wins");
        GuildData n3 = n1.Clone(); n3.Name = "Other";
        Check(!n1.Merge(n3) && n1.Name == "New", "Merge_NameByNameVer equal keeps");

        // Merge: another guild is never merged
        GuildData other = G("h", E("Z", GuildRole.Member, 9, "Z", 9, false));
        GuildData mine = G("g", E("A", GuildRole.Leader, 1, "A", 1, false));
        Check(!mine.Merge(other) && mine.Find("Z") == null, "Merge_DifferentGuildId_NoChange");

        // Effective leader
        GuildData l1 = G("g", E("A", GuildRole.Leader, 1, "A", 1, false), E("B", GuildRole.Officer, 1, "A", 2, false));
        Check(l1.LeaderId() == "A", "LeaderId_OneLeader");
        GuildData l2 = G("g", E("A", GuildRole.Member, 1, "A", 1, false), E("B", GuildRole.Member, 1, "A", 2, false), E("C", GuildRole.Officer, 1, "A", 3, false), E("D", GuildRole.Officer, 1, "A", 4, false));
        Check(l2.LeaderId() == "C", "LeaderId_None_OldestOfficer");
        GuildData l3 = G("g", E("A", GuildRole.Member, 1, "A", 5, false), E("B", GuildRole.Member, 1, "A", 2, false));
        Check(l3.LeaderId() == "B", "LeaderId_None_NoOfficer_OldestMember");
        GuildData l4 = G("g", E("B", GuildRole.Member, 1, "A", 2, false), E("A", GuildRole.Member, 1, "A", 2, false));
        Check(l4.LeaderId() == "A", "LeaderId_SeqTie_LowestUserId");
        GuildData l5 = G("g", E("A", GuildRole.Leader, 1, "A", 1, false), E("B", GuildRole.Leader, 1, "B", 2, false), E("C", GuildRole.Officer, 1, "A", 3, false));
        Check(l5.LeaderId() == "C", "LeaderId_TwoLeaders_SameAsNone");
        GuildData l6 = G("g", E("A", GuildRole.Leader, 2, "A", 1, true), E("B", GuildRole.Member, 1, "A", 2, false));
        Check(l6.LeaderId() == "B", "LeaderId_RemovedLeaderIgnored");
        Check(G("g", E("A", GuildRole.Leader, 2, "A", 1, true)).LeaderId() == null, "LeaderId_NoActive_Null");

        // Apply
        GuildData mineA = G("g", E("A", GuildRole.Leader, 1, "A", 1, false), E("B", GuildRole.Member, 1, "A", 2, false));
        GuildData stranger = G("g", E("A", GuildRole.Leader, 1, "A", 1, false), E("B", GuildRole.Member, 1, "A", 2, false), E("X", GuildRole.Member, 1, "X", 3, false));
        GuildData before = mineA.Clone();
        Check(mineA.Apply(stranger, "X", "B") == ApplyResult.Ignored && Same(mineA, before), "Apply_StrangerIgnored");
        GuildData otherGuild = G("h", E("A", GuildRole.Leader, 1, "A", 1, false), E("X", GuildRole.Member, 1, "X", 3, false));
        Check(mineA.Apply(otherGuild, "A", "B") == ApplyResult.Ignored && Same(mineA, before), "Apply_OtherGuildIgnored");
        GuildData withRemoved = G("g", E("A", GuildRole.Leader, 1, "A", 1, false), E("B", GuildRole.Member, 1, "A", 2, false), E("C", GuildRole.Member, 2, "A", 3, true));
        GuildData mineC = G("g", E("A", GuildRole.Leader, 1, "A", 1, false), E("B", GuildRole.Member, 1, "A", 2, false), E("C", GuildRole.Member, 2, "A", 3, true));
        GuildData beforeC = mineC.Clone();
        GuildData fromC = G("g", E("A", GuildRole.Leader, 1, "A", 1, false), E("C", GuildRole.Member, 1, "A", 3, false), E("Q", GuildRole.Member, 1, "C", 9, false));
        Check(mineC.Apply(fromC, "C", "B") == ApplyResult.Ignored && Same(mineC, beforeC), "Apply_RemovedSenderIgnored");
        GuildData incoming = mineA.Clone(); incoming.Entries["B"].Role = GuildRole.Officer; incoming.Entries["B"].Ver = 2; incoming.Entries["B"].Editor = "A";
        Check(mineA.Apply(incoming, "A", "B") == ApplyResult.Merged && mineA.Find("B").Role == GuildRole.Officer, "Apply_Merged");
        Check(mineA.Apply(incoming, "A", "B") == ApplyResult.Unchanged, "Apply_Unchanged");
        GuildData kicked = mineA.Clone(); kicked.Entries["B"].Removed = true; kicked.Entries["B"].Ver = 3; kicked.Entries["B"].Editor = "A";
        Check(mineA.Apply(kicked, "A", "B") == ApplyResult.SelfRemoved, "Apply_SelfRemoved");
    }

    // ---- task 2: edit operations and permissions

    // A leader, B and D officers, C member, built through the real operations.
    private static GuildData Crew()
    {
        GuildData g = GuildData.Create("A", "Ann", "Crew");
        g.Invite("A", "B", "Bob", 10);
        g.Invite("A", "C", "Cat", 10);
        g.Invite("A", "D", "Dan", 10);
        g.SetOfficer("A", "B", true);
        g.SetOfficer("A", "D", true);
        return g;
    }

    private static void Task2()
    {
        // Permission table
        GuildData p = Crew();
        Check(p.CanInvite("A") && p.CanInvite("B") && !p.CanInvite("C") && !p.CanInvite("nobody"), "Perm_CanInvite");
        Check(p.CanRemove("A", "D") && p.CanRemove("A", "C") && !p.CanRemove("A", "A"), "Perm_LeaderRemoves");
        Check(p.CanRemove("B", "C") && !p.CanRemove("B", "D") && !p.CanRemove("B", "A") && !p.CanRemove("C", "B"), "Perm_OfficerRemovesOnlyMembers");
        Check(p.CanSetOfficer("A", "C") && !p.CanSetOfficer("B", "C") && !p.CanSetOfficer("A", "A"), "Perm_SetOfficer");
        Check(p.CanTransfer("A", "C") && !p.CanTransfer("B", "C") && !p.CanTransfer("A", "A"), "Perm_Transfer");
        Check(p.CanRename("A") && !p.CanRename("B") && !p.CanRename("C"), "Perm_Rename");
        Check(p.RoleOf("A") == GuildRole.Leader && p.RoleOf("B") == GuildRole.Officer && p.RoleOf("C") == GuildRole.Member && p.RoleOf("zzz") == GuildRole.Member, "RoleOf");

        // Invite
        GuildData g = GuildData.Create("A", "Ann", "Crew");
        Check(g.Invite("A", "B", "Bob", 10), "Invite_AddsMember returns true");
        GuildEntry b = g.Find("B");
        Check(b != null && b.Role == GuildRole.Member && b.Ver == 1 && b.Seq == 2 && b.Editor == "A" && b.Name == "Bob", "Invite_AddsMemberWithSeq");
        Check(!g.Invite("B", "X", "x", 10) && g.Find("X") == null, "Invite_ByMember_False");
        Check(!g.Invite("A", "B", "Bob", 10), "Invite_ActiveAlready_False");
        GuildData small = GuildData.Create("A", "Ann", "Crew");
        Check(small.Invite("A", "B", "Bob", 2) && !small.Invite("A", "C", "Cat", 2) && small.Find("C") == null, "Invite_CapEnforced");
        GuildData names = GuildData.Create("A", "Ann", "Crew");
        names.Invite("A", "B", "A\tB", 10);
        Check(names.Find("B").Name == "A B", "CleanName_InEntries");

        // Reinvite beats the tombstone
        GuildData r = GuildData.Create("A", "Ann", "Crew");
        r.Invite("A", "B", "Bob", 10);
        r.Remove("A", "B");
        GuildData tomb = r.Clone();
        Check(tomb.Find("B").Removed && tomb.Find("B").Ver == 2, "Remove_LeavesTombstone");
        Check(r.Invite("A", "B", "Bob", 10) && r.Find("B").Ver == 3 && !r.Find("B").Removed && r.Find("B").Role == GuildRole.Member, "Reinvite_BumpsVer");
        Check(tomb.Merge(r) && !tomb.Find("B").Removed && !r.Merge(tomb), "Reinvite_BeatsTombstone");

        // Two officers invite different people while apart
        GuildData g0 = Crew();
        GuildData byB = g0.Clone(), byD = g0.Clone();
        byB.Invite("B", "X", "x", 10);
        byD.Invite("D", "Y", "y", 10);
        GuildData m1 = byB.Clone(); m1.Merge(byD);
        GuildData m2 = byD.Clone(); m2.Merge(byB);
        Check(m1.Find("X") != null && !m1.Find("X").Removed && m1.Find("Y") != null && !m1.Find("Y").Removed, "Concurrent_Invites_BothSurvive");
        Check(Same(m1, m2), "Concurrent_Invites_SameEitherOrder");

        // Kicking someone who is offline
        GuildData k = GuildData.Create("A", "Ann", "Crew");
        k.Invite("A", "B", "Bob", 10);
        GuildData stale = k.Clone();
        Check(k.Remove("A", "B") && k.Find("B").Removed, "Kick_Removes");
        Check(stale.Apply(k, "A", "B") == ApplyResult.SelfRemoved && stale.Find("B").Removed, "Kick_Offline_PropagatesTombstone");

        // Officers and leadership
        GuildData o = GuildData.Create("A", "Ann", "Crew");
        o.Invite("A", "B", "Bob", 10);
        o.Invite("A", "C", "Cat", 10);
        Check(o.SetOfficer("A", "B", true) && o.Find("B").Role == GuildRole.Officer && o.Find("B").Ver == 2, "SetOfficer_Promotes");
        Check(!o.SetOfficer("A", "B", true), "SetOfficer_NoChange_False");
        Check(!o.SetOfficer("B", "C", true) && !o.TransferLeader("B", "C"), "Officer_CannotPromoteOrTransfer");
        Check(o.TransferLeader("A", "B") && o.LeaderId() == "B" && o.Find("A").Role == GuildRole.Officer && o.Find("A").Ver == 2 && o.Find("B").Role == GuildRole.Leader && o.Find("B").Ver == 3 && o.ActiveCount == 3, "TransferLeader");
        Check(o.SetOfficer("B", "A", false) && o.Find("A").Role == GuildRole.Member, "SetOfficer_Demotes");

        // Leaving
        GuildData l1 = Crew();            // A leader, B officer (seq 2), C member, D officer (seq 4)
        Check(l1.Leave("A") && l1.Find("A").Removed && l1.LeaderId() == "B" && l1.Find("B").Role == GuildRole.Leader, "Leave_Leader_PromotesOldestOfficer");
        GuildData l2 = GuildData.Create("A", "Ann", "Crew");
        l2.Invite("A", "B", "Bob", 10); l2.Invite("A", "C", "Cat", 10);
        Check(l2.Leave("A") && l2.LeaderId() == "B" && l2.Find("B").Role == GuildRole.Leader, "Leave_Leader_NoOfficer_PromotesOldestMember");
        GuildData l3 = GuildData.Create("A", "Ann", "Crew");
        Check(l3.Leave("A") && l3.ActiveCount == 0, "Leave_LastMember_ActiveCountZero");
        GuildData l4 = Crew();
        Check(l4.Leave("C") && l4.Find("C").Removed && l4.Find("C").Ver == 2 && l4.Find("C").Editor == "C" && l4.LeaderId() == "A", "Leave_Member_JustRemoved");
        Check(!l4.Leave("C") && !l4.Leave("nobody"), "Leave_NotActive_False");

        // Renaming and name cleaning
        GuildData n = Crew();
        Check(n.Rename("A", "New name") && n.Name == "New name" && n.NameVer == 2, "Rename_Leader");
        Check(!n.Rename("B", "Nope") && n.Name == "New name", "Rename_Officer_False");
        Check(!n.Rename("A", " \t\n ") && n.NameVer == 2, "Rename_BlankAfterClean_False");
        Check(n.Rename("A", "Tab\tName") && n.Name == "Tab Name", "Rename_CleansTabs");
        Check(n.Rename("A", new string('x', 80)) && n.Name.Length == 32, "Rename_Truncates");
        Check(GuildData.CleanName(null) == "" && GuildData.CleanName(" a\r\nb ") == "a  b", "CleanName_Basics");
    }

    // ---- task 3: text format and file

    private static string TempDir()
    {
        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "warband-test-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Task3()
    {
        GuildData g = Crew();
        g.Remove("A", "C");
        GuildData back = GuildData.FromLines(g.ToLines());
        Check(back != null && Same(g, back), "Lines_RoundTrip");
        Check(back != null && back.Find("A").Name == "Ann" && back.Find("C").Removed, "Lines_RoundTrip names and tombstone");

        GuildData tabs = GuildData.Create("A", "Ann\tX", "Cr\tew");
        GuildData tabsBack = GuildData.FromLines(tabs.ToLines());
        Check(tabs.Name == "Cr ew" && tabsBack != null && tabsBack.Name == "Cr ew" && tabsBack.Find("A").Name == "Ann X", "Create_CleansNames");

        string[] good = g.ToLines();
        string[][] bad = new string[][] {
            new string[] { "complete garbage" },
            new string[0],
            new string[] { "m\tB\tBob\t0\t0\t1\tA\t2" },
            new string[] { "guild\tg\tCrew\t1", "guild\th\tCrew\t1", "m\tA\tAnn\t2\t0\t1\tA\t1" },
            new string[] { "guild\tg\tCrew\tx", "m\tA\tAnn\t2\t0\t1\tA\t1" },
            new string[] { "guild\tg\tCrew\t1", "m\tA\tAnn\t5\t0\t1\tA\t1" },
            new string[] { "guild\tg\tCrew\t1", "m\tA\tAnn\t2\t2\t1\tA\t1" },
            new string[] { "guild\tg\tCrew\t1", "m\t\tAnn\t2\t0\t1\tA\t1" },
            new string[] { "guild\tg\tCrew\t1", "m\tA\tAnn\t2\t0\t1\tA\t1", "m\tA\tAnn\t2\t0\t1\tA\t1" },
            new string[] { "guild\tg\tCrew\t1", "m\tA\tAnn\t2\t0\tzz\tA\t1" },
            new string[] { "guild\tg\tCrew\t1", "m\tA\tAnn\t2\t0\t1\tA" },
            new string[] { "guild\t\tCrew\t1", "m\tA\tAnn\t2\t0\t1\tA\t1" },
            new string[] { "guild\tg\tCrew\t1" },
        };
        bool allNull = true;
        foreach (string[] lines in bad) if (GuildData.FromLines(lines) != null) allNull = false;
        Check(allNull && GuildData.FromLines(null) == null, "FromLines_Invalid_Null");
        Check(good.Length > 0, "ToLines_NotEmpty");

        List<string> padded = new List<string>();
        padded.Add(""); padded.Add("# a comment"); padded.AddRange(good); padded.Add("   ");
        GuildData withBlank = GuildData.FromLines(padded.ToArray());
        Check(withBlank != null && Same(g, withBlank), "FromLines_IgnoresBlankAndComment");

        // Files
        string dir = TempDir();
        string path = System.IO.Path.Combine(System.IO.Path.Combine(dir, "sub"), "guild.txt");
        Check(GuildFile.Load(path) == null, "File_Missing_Null");
        GuildFile.Save(path, g);
        GuildData loaded = GuildFile.Load(path);
        Check(loaded != null && Same(g, loaded) && !System.IO.File.Exists(path + ".tmp"), "File_SaveLoad");
        GuildFile.Save(path, back);
        Check(Same(g, GuildFile.Load(path)), "File_SaveOverwrites");

        System.IO.File.WriteAllText(path, "this is not a guild file\n");
        Check(GuildFile.Load(path) == null && System.IO.File.Exists(path + ".bak") && System.IO.File.ReadAllText(path + ".bak").StartsWith("this is not"), "File_Corrupt_NullAndBak");
        GuildFile.Delete(path);
        Check(!System.IO.File.Exists(path), "File_Delete");
        GuildFile.Delete(path);
        Check(true, "File_Delete_Missing_NoThrow");
        try { System.IO.Directory.Delete(dir, true); } catch (Exception) { }

        // Snapshot for the network
        GuildData snap = GuildData.FromSnapshot(g.Id, "Crew\tX", 3, g.Snapshot());
        Check(snap != null && snap.Name == "Crew X" && snap.NameVer == 3 && snap.Entries.Count == g.Entries.Count, "FromSnapshot_Cleans");
        List<GuildEntry> entries = g.Snapshot();
        entries[0].Ver = 99;
        Check(g.Find(entries[0].UserId).Ver != 99, "Snapshot_Clones");
        List<GuildEntry> dirty = new List<GuildEntry>();
        dirty.Add(E("B", GuildRole.Member, 1, "A", 2, false));
        dirty[0].Name = "Bo\tb";
        Check(GuildData.FromSnapshot("g", "Crew", 1, dirty).Find("B").Name == "Bo b", "FromSnapshot_CleansEntryNames");
        Check(GuildData.FromSnapshot("", "Crew", 1, dirty) == null, "FromSnapshot_EmptyId_Null");
        List<GuildEntry> noId = new List<GuildEntry>();
        noId.Add(E("", GuildRole.Member, 1, "A", 2, false));
        Check(GuildData.FromSnapshot("g", "Crew", 1, noId) == null, "FromSnapshot_EmptyUserId_Null");
    }

    // ---- final review fixes

    private static void Final()
    {
        // A removal beats an edit with the same Ver, whichever editor id sorts higher.
        GuildData gone = G("g", E("A", GuildRole.Leader, 1, "A", 1, false), E("M", GuildRole.Member, 3, "A", 2, true));
        GuildData promoted = G("g", E("A", GuildRole.Leader, 1, "A", 1, false), E("M", GuildRole.Officer, 3, "Z", 2, false));
        GuildData gone2 = gone.Clone(), promoted2 = promoted.Clone();
        gone.Merge(promoted);
        promoted2.Merge(gone2);
        Check(gone.Find("M").Removed && promoted2.Find("M").Removed && Same(gone, promoted2), "Merge_RemovalWinsTie");

        // Someone who left but whose removal nobody has received yet is "leaving".
        GuildData l = Crew();
        Check(!l.Leaving("C"), "Leaving_FalseWhileMember");
        l.Leave("C");
        Check(l.Leaving("C") && !l.Leaving("A") && !l.Leaving("nobody") && !l.Leaving(""), "Leaving_TrueAfterLeave");
        Check(!GuildData.Create("A", "Ann", "Crew").Leaving("A"), "Leaving_FalseForDraft");
    }

    public static int Main()
    {
        Task1();
        Task2();
        Task3();
        Final();
        Console.WriteLine(_passed + " passed, " + _failed + " failed");
        return _failed == 0 ? 0 : 1;
    }
}
