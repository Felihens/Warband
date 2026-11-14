using System;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Warband
{
    // Two tabs above the chat: "All" (everything, as before) and "Party" (only party messages). While the Party
    // tab is open, what you type goes to the party without /p. Commands that start with / still work as usual.
    // Typing /p alone opens the Party tab, /all (or /a) goes back to All; "/all text" goes back and sends it to everyone.
    [HarmonyPatch]
    internal static class ChatTabs
    {
        internal static bool PartyTab;
        private static GameObject _bar;
        private static Image _allBg, _partyBg;
        private static readonly Color On = new Color(0.95f, 0.75f, 0.3f, 0.85f), Off = new Color(0f, 0f, 0f, 0.55f);

        internal static bool IsPartyLine(string line)
        {
            return line != null && (line.Contains(">[Party]</color>") || line.StartsWith("<color=#ffcc66>"));
        }

        internal static void Set(bool party) { Set(party, false); }

        internal static void Set(bool party, bool say)
        {
            bool changed = PartyTab != party;
            PartyTab = party;
            if (say && changed && Chat.instance != null)
                Chat.instance.AddString(party ? "<color=#ffcc66>Party chat: what you type goes to your party. /all to go back.</color>"
                                              : "<color=#ffcc66>All chat: what you type goes to everyone.</color>");
            Chat chat = Chat.instance;
            if (chat == null) return;
            Traverse.Create(chat).Method("UpdateChat").GetValue();
            Traverse.Create(chat).Field("m_hideTimer").SetValue(0f);
            Paint();
        }

        private static void Paint()
        {
            if (_allBg != null) _allBg.color = PartyTab ? Off : On;
            if (_partyBg != null) _partyBg.color = PartyTab ? On : Off;
        }

        // the chat text: in the Party tab, only the party's lines
        [HarmonyPatch(typeof(Terminal), "UpdateChat"), HarmonyPrefix]
        private static bool Filter(Terminal __instance)
        {
            if (!PartyTab || !(__instance is Chat) || __instance.m_output == null) return true;
            System.Collections.Generic.List<string> buf = Traverse.Create(__instance).Field("m_chatBuffer").GetValue<System.Collections.Generic.List<string>>();
            if (buf == null) return true;
            StringBuilder sb = new StringBuilder();
            int shown = 0;
            for (int i = buf.Count - 1; i >= 0 && shown < 40; i--)
                if (IsPartyLine(buf[i])) { sb.Insert(0, buf[i] + "\n"); shown++; }
            if (shown == 0) sb.Append("<color=#ffcc66>Party tab: what you type goes to your party. /all to go back.</color>\n");
            __instance.m_output.text = sb.ToString();
            return false;
        }

        // the two tab buttons, just above the chat window. They sit beside the window, not inside it (the window
        // clips what is outside its box), follow its top-left corner and show and hide with it.
        private static readonly Vector3[] Corners = new Vector3[4];
        private static bool _failed, _helped;

        internal static void Update()
        {
            Chat chat = Chat.instance;
            if (chat == null || chat.m_chatWindow == null || _failed) return;
            if (!_helped && Player.m_localPlayer != null)
            {
                // once per session, in the chat: how to switch between party and normal chat
                _helped = true;
                chat.AddString("<color=#ffcc66>Shieldmates chat: /p switches to party chat, /all (or /a) back to normal chat. "
                    + "\"/p message\" sends one message to the party, \"/all message\" goes back and sends it to everyone. Or click the All / Party tabs.</color>");
            }
            try
            {
                if (_bar == null) Build(chat);
                bool show = chat.m_chatWindow.gameObject.activeInHierarchy;
                if (_bar.activeSelf != show) _bar.SetActive(show);
                if (!show) return;
                chat.m_chatWindow.GetWorldCorners(Corners);   // 1 = top left
                _bar.transform.position = Corners[1];
                _bar.transform.SetAsLastSibling();
            }
            catch (Exception e) { _failed = true; Plugin.Log.LogWarning("Chat tabs failed: " + e); }
        }

        private static void Build(Chat chat)
        {
            _bar = new GameObject("Warband_ChatTabs", typeof(RectTransform));
            RectTransform r = (RectTransform)_bar.transform;
            Transform parent = chat.m_chatWindow.parent != null ? chat.m_chatWindow.parent : chat.m_chatWindow;
            r.SetParent(parent, false);
            r.localScale = chat.m_chatWindow.localScale;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0f, 0f);
            r.sizeDelta = new Vector2(170f, 26f);
            _allBg = Tab(r, chat, "All", 0f, false);
            _partyBg = Tab(r, chat, "Party", 84f, true);
            Paint();
            Plugin.Log.LogInfo("[chat] tabs placed above " + chat.m_chatWindow.name + " (under " + parent.name + ")");
        }

        private static Image Tab(RectTransform parent, Chat chat, string label, float x, bool party)
        {
            GameObject b = new GameObject("Tab_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform r = (RectTransform)b.transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0f, 0f);
            r.pivot = new Vector2(0f, 0f);
            r.anchoredPosition = new Vector2(x, 0f);
            r.sizeDelta = new Vector2(80f, 26f);
            b.GetComponent<Button>().onClick.AddListener(() => Set(party));
            GameObject t = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform tr = (RectTransform)t.transform;
            tr.SetParent(r, false);
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            TextMeshProUGUI tx = t.GetComponent<TextMeshProUGUI>();
            if (chat.m_output != null) tx.font = chat.m_output.font;
            tx.fontSize = 16; tx.alignment = TextAlignmentOptions.Center; tx.color = Color.white;
            tx.text = label; tx.raycastTarget = false;
            return b.GetComponent<Image>();
        }
    }
}
