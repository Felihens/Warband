using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace Warband
{
    internal class Friend
    {
        public string Id = "";   // platform user id (e.g. Steam_7656...), empty when added by name only
        public string Name = "";
    }

    // Friends list, kept on this computer only (BepInEx/config/Warband/friends.txt).
    internal static class Friends
    {
        internal static readonly List<Friend> List = new List<Friend>();

        private static string FilePath
        {
            get { return Path.Combine(Path.Combine(Paths.ConfigPath, "Warband"), "friends.txt"); }
        }

        internal static void Load()
        {
            List.Clear();
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    if (line.Trim().Length == 0 || line.StartsWith("#")) continue;
                    string[] parts = line.Split('\t');
                    Friend f = new Friend();
                    if (parts.Length >= 2) { f.Id = parts[0].Trim(); f.Name = parts[1].Trim(); }
                    else f.Name = parts[0].Trim();
                    if (f.Name.Length > 0 || f.Id.Length > 0) List.Add(f);
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Could not read the friends list: " + e.Message); }
        }

        internal static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                List<string> lines = new List<string>();
                lines.Add("# Warband friends: one per line, platform id <TAB> name. The id can be empty.");
                foreach (Friend f in List) lines.Add(f.Id + "\t" + f.Name);
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (Exception e) { Plugin.Log.LogWarning("Could not save the friends list: " + e.Message); }
        }

        internal static Friend Find(string id, string name)
        {
            foreach (Friend f in List)
                if (id.Length > 0 && f.Id == id) return f;
            foreach (Friend f in List)
                if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return f;
            return null;
        }

        internal static bool IsFriend(string id, string name)
        {
            return Find(id, name) != null;
        }

        internal static void Add(string id, string name)
        {
            Friend f = Find(id, name);
            if (f != null)
            {
                if (f.Id.Length == 0 && id.Length > 0) { f.Id = id; Save(); }
                return;
            }
            f = new Friend();
            f.Id = id;
            f.Name = name;
            List.Add(f);
            Save();
            Plugin.Message(name + " added to your friends");
        }

        internal static void Remove(Friend f)
        {
            List.Remove(f);
            Save();
            Plugin.Message(f.Name + " removed from your friends");
        }

        // Fills in ids and current names of friends who are online.
        internal static void Refresh(List<Party.Online> online)
        {
            bool changed = false;
            foreach (Party.Online o in online)
            {
                Friend f = Find(o.UserId, o.Name);
                if (f == null) continue;
                if (f.Id.Length == 0 && o.UserId.Length > 0) { f.Id = o.UserId; changed = true; }
                if (f.Name != o.Name && o.Name.Length > 0) { f.Name = o.Name; changed = true; }
            }
            if (changed) Save();
        }
    }
}
