using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Warband
{
    // Party chat: type "/p message" (or "/party message") in the normal chat. The message goes only
    // to the party members, straight from player to player like the rest of Warband, and shows in
    // the chat with a [Party] tag and the sender's colour, and for a few seconds under their HUD block.
    internal static class PartyChat
    {
        internal const string Rpc = "Warband_Chat";
        private const int MaxLength = 200;
        private static readonly FieldInfo HideTimer = AccessTools.Field(typeof(Chat), "m_hideTimer");

        // Returns the message if the text is a party chat command, else null.
        internal static string Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string t = text.TrimStart();
            string lower = t.ToLowerInvariant();
            if (lower == "/p" || lower == "/party") return "";
            if (lower.StartsWith("/p ")) return t.Substring(3).Trim();
            if (lower.StartsWith("/party ")) return t.Substring(7).Trim();
            return null;
        }

        internal static void Send(string text)
        {
            if (text.Length == 0) { Line("<color=#ffcc66>Party chat: type /p and your message.</color>"); return; }
            if (!Party.InParty) { Line("<color=#ffcc66>You are not in a party.</color>"); return; }
            if (text.Length > MaxLength) text = text.Substring(0, MaxLength);
            ZPackage pkg = new ZPackage();
            pkg.Write(Party.MyName());
            pkg.Write(text);
            byte[] data = pkg.GetArray();
            long me = Party.Me;
            int sent = 0;
            foreach (Member m in Party.Members)
                if (m.Peer != me && ZRoutedRpc.instance != null) { ZRoutedRpc.instance.InvokeRoutedRPC(m.Peer, Rpc, new object[] { new ZPackage(data) }); sent++; }
            Show(Party.Self(), Party.MyName(), text);
            Plugin.Debug("[chat] sent to " + sent);
        }

        internal static void OnMessage(long sender, ZPackage pkg)
        {
            string name = pkg.ReadString();
            string text = pkg.ReadString();
            Member m = Party.Get(sender);
            if (m == null || !Party.InParty) return;   // only from people in our party
            if (text.Length > MaxLength) text = text.Substring(0, MaxLength);
            Show(m, m.Name.Length > 0 ? m.Name : name, text);
        }

        private static void Show(Member m, string name, string text)
        {
            if (m != null) { m.ChatText = text; m.ChatAt = Time.time; }
            Color c = m != null ? Looks.ColorOf(m) : Color.white;
            Line("<color=#" + ColorUtility.ToHtmlStringRGB(Plugin.ChatColor.Value) + ">[Party]</color> <color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" +
                Clean(name) + "</color>: <color=#" + ColorUtility.ToHtmlStringRGB(Plugin.ChatColor.Value) + "><noparse>" + text.Replace("</noparse>", "") + "</noparse></color>");
        }

        private static string Clean(string s)
        {
            return s.Replace("<", "").Replace(">", "");
        }

        private static void Line(string rich)
        {
            Chat chat = Chat.instance;
            if (chat == null) return;
            chat.AddString(rich);
            if (HideTimer != null) HideTimer.SetValue(chat, 0f);   // pop the chat window open, like a normal message
        }
    }

    [HarmonyPatch(typeof(Chat), "InputText")]
    internal static class ChatInputPatch
    {
        private static bool Prefix(Chat __instance)
        {
            if (__instance.m_input == null) return true;
            string raw = __instance.m_input.text ?? "";
            string low = raw.Trim().ToLowerInvariant();
            if (low == "/p" || low == "/party") { ChatTabs.Set(true, true); return false; }   // alone: open the Party tab
            if (low == "/all" || low == "/a") { ChatTabs.Set(false, true); return false; }
            if (low.StartsWith("/all ") || low.StartsWith("/a "))   // back to All and send this to everyone
            {
                ChatTabs.Set(false, true);
                __instance.m_input.text = raw.TrimStart().Substring(low.StartsWith("/all ") ? 5 : 3).Trim();
                return __instance.m_input.text.Length > 0;
            }
            string msg = PartyChat.Parse(raw);
            if (msg == null && ChatTabs.PartyTab && raw.Trim().Length > 0 && !raw.TrimStart().StartsWith("/")) msg = raw.Trim();
            if (msg == null) return true;
            try { PartyChat.Send(msg); }
            catch (Exception e) { Plugin.Log.LogWarning("Party chat failed: " + e.Message); }
            return false;   // never reaches the server
        }
    }
}
