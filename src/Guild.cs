using System;
using System.IO;
using BepInEx;

namespace Warband
{
    // The guild this player belongs to, kept in BepInEx/config/Warband/guild.txt. It stays loaded
    // between worlds; only the live group (Party.Members) comes and goes.
    internal static class Guild
    {
        internal static GuildData Current;   // null when you are in no guild; one entry = a draft nobody joined yet
        internal static string MyId = "";    // platform id of this player, "" until the player list shows it

        private static string FilePath
        {
            get { return Path.Combine(Path.Combine(Paths.ConfigPath, "Warband"), "guild.txt"); }
        }

        // A guild somebody has joined that you are still in (a draft has only its founder and is never written to disk).
        internal static bool Exists
        {
            get
            {
                if (Current == null || Current.Entries.Count < 2) return false;
                if (MyId.Length == 0) return true;
                GuildEntry e = Current.Find(MyId);
                return e != null && !e.Removed;
            }
        }

        // You left, but the copy is kept until a member has taken the news (see Party.Leave).
        internal static bool Leaving { get { return Current != null && MyId.Length > 0 && Current.Leaving(MyId); } }

        internal static void Load()
        {
            Current = GuildFile.Load(FilePath);
            if (Current != null) Plugin.Log.LogInfo("Guild loaded: " + Current.Name + " (" + Current.ActiveCount + " members)");
            else if (File.Exists(FilePath + ".bak")) Plugin.Log.LogWarning("guild.txt could not be read; a copy was kept as guild.txt.bak");
        }

        // Writes the guild, or removes the file when there is no guild left.
        internal static void Save()
        {
            try
            {
                if (Current == null || Current.ActiveCount == 0) { Current = null; GuildFile.Delete(FilePath); }
                else if (Current.Entries.Count >= 2) GuildFile.Save(FilePath, Current);
            }
            catch (Exception e) { Plugin.Log.LogWarning("Could not save the guild: " + e.Message); }
        }

        internal static void Clear()
        {
            Current = null;
            GuildFile.Delete(FilePath);
        }
    }
}
