using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Warband
{
    public enum SilhouetteStyle { Fill, Outline, AlwaysOnTop }

    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "felih.Warband";
        public const string NAME = "Warband";
        public const string VERSION = "1.3.0";

        internal static ManualLogSource Log;

        // Keys
        internal static ConfigEntry<KeyboardShortcut> PanelKey;
        internal static ConfigEntry<KeyboardShortcut> InviteKey;
        internal static ConfigEntry<KeyboardShortcut> AcceptKey;
        internal static ConfigEntry<KeyboardShortcut> DeclineKey;
        internal static ConfigEntry<KeyboardShortcut> SendKey;
        internal static ConfigEntry<float> SendRange;
        internal static ConfigEntry<KeyboardShortcut> EditHudKey;
        // Party
        internal static ConfigEntry<bool> AutoAcceptFriends;
        internal static ConfigEntry<int> MaxMembers;
        // HUD
        internal static ConfigEntry<bool> ShowHud;
        internal static ConfigEntry<Vector2> HudPosition;
        internal static ConfigEntry<float> HudScale;
        internal static ConfigEntry<bool> ShowStamina;
        internal static ConfigEntry<bool> ShowEitr;
        internal static ConfigEntry<bool> ShowBuffs;
        internal static ConfigEntry<int> MaxBuffs;
        internal static ConfigEntry<bool> ShowWeapon;
        internal static ConfigEntry<bool> ShowDistance;
        internal static ConfigEntry<float> HudBackgroundOpacity;
        internal static ConfigEntry<string> HudOrder;
        // Silhouette
        internal static ConfigEntry<bool> SilhouetteEnabled;
        internal static ConfigEntry<Color> SilhouetteColor;
        internal static ConfigEntry<float> SilhouetteMaxDistance;
        internal static ConfigEntry<SilhouetteStyle> SilhouetteMode;
        internal static ConfigEntry<int> SilhouetteOutlineWidth;
        internal static ConfigEntry<bool> SilhouetteOnSelf;
        internal static ConfigEntry<float> SilhouetteMargin;
        internal static ConfigEntry<float> SilhouetteOpacity;
        // Compass
        internal static ConfigEntry<Color> ChatColor;
        internal static ConfigEntry<bool> ChatInHud;
        internal static ConfigEntry<float> ChatHudSeconds;
        internal static ConfigEntry<bool> CompassEnabled;
                internal static ConfigEntry<float> CompassMaxDistance;
        internal static ConfigEntry<float> CompassIconSize;
        // Debug
        internal static ConfigEntry<bool> MapFindButton;
        internal static ConfigEntry<Vector2> MapFindPosition;
        internal static ConfigEntry<float> MapFindZoom, MapFindSpeed;
        internal static ConfigEntry<bool> DetailedLog;

        private void Awake()
        {
            Log = Logger;
            PanelKey = Config.Bind("1 - Keys", "PanelKey", new KeyboardShortcut(KeyCode.P, KeyCode.LeftShift),
                "Opens the party panel: everyone on the server, your friends and your party.");
            InviteKey = Config.Bind("1 - Keys", "InviteKey", new KeyboardShortcut(KeyCode.J),
                "Invites the player you are looking at to your party.");
            AcceptKey = Config.Bind("1 - Keys", "AcceptKey", new KeyboardShortcut(KeyCode.Y),
                "Accepts a party invite.");
            DeclineKey = Config.Bind("1 - Keys", "DeclineKey", new KeyboardShortcut(KeyCode.N),
                "Declines a party invite.");
            SendKey = Config.Bind("1 - Keys", "SendItemKey", new KeyboardShortcut(KeyCode.P),
                "With your bag open, hover an item and press this to send it to a party member nearby.");
            EditHudKey = Config.Bind("1 - Keys", "EditHudKey", new KeyboardShortcut(KeyCode.F8),
                "Edit mode for the party HUD: drag it with the mouse, scroll to resize, press again to finish.");

            SendRange = Config.Bind("2 - Party", "SendItemRange", 30f,
                new ConfigDescription("How close a party member must be to send them an item (metres).", new AcceptableValueRange<float>(2f, 100f)));
            AutoAcceptFriends = Config.Bind("2 - Party", "AutoAcceptFriends", false,
                "Invites from players on your friends list are accepted on their own.");
            MaxMembers = Config.Bind("2 - Party", "MaxMembers", 10,
                new ConfigDescription("Most players in one party (counting you).", new AcceptableValueRange<int>(2, 10)));

            ShowHud = Config.Bind("3 - HUD", "Enabled", true, "Shows your allies' health, stamina, eitr, buffs and weapon.");
            HudPosition = Config.Bind("3 - HUD", "Position", new Vector2(20f, 330f),
                "Top left corner of the party HUD, in pixels from the top left of the screen. Easier to set with the edit key.");
            HudScale = Config.Bind("3 - HUD", "Scale", 1f,
                new ConfigDescription("Size of the party HUD.", new AcceptableValueRange<float>(0.5f, 2.5f)));
            ShowStamina = Config.Bind("3 - HUD", "ShowStamina", true, "Stamina bar.");
            ShowEitr = Config.Bind("3 - HUD", "ShowEitr", true, "Eitr bar (only for allies who have eitr).");
            ShowBuffs = Config.Bind("3 - HUD", "ShowBuffs", true, "Active effects (rested, wet, mead, set bonus...).");
            MaxBuffs = Config.Bind("3 - HUD", "MaxBuffs", 8,
                new ConfigDescription("Most effect icons per ally.", new AcceptableValueRange<int>(0, 16)));
            ShowWeapon = Config.Bind("3 - HUD", "ShowWeapon", true, "Icon of the weapon in the ally's hand.");
            ShowDistance = Config.Bind("3 - HUD", "ShowDistance", true, "How far each ally is.");
            HudBackgroundOpacity = Config.Bind("3 - HUD", "BackgroundOpacity", 0.85f,
                new ConfigDescription("How opaque the HUD background is (0 = no background, 1 = solid).", new AcceptableValueRange<float>(0f, 1f)));
            HudOrder = Config.Bind("3 - HUD", "Order", "",
                "Order of your allies in the HUD (names separated by |). Easier to set by dragging them in edit mode.");

            SilhouetteEnabled = Config.Bind("4 - Silhouette", "Enabled", true,
                "When an ally is behind a wall, tree or hill, the hidden part of them shows as a coloured silhouette. Parts you can see directly are left as they are.");
            SilhouetteColor = Config.Bind("4 - Silhouette", "Color", new Color(1f, 0.85f, 0.3f, 1f),
                "Colour of the silhouette.");
            SilhouetteOpacity = Config.Bind("4 - Silhouette", "Opacity", 0.5f,
                new ConfigDescription("How opaque the silhouette is.", new AcceptableValueRange<float>(0.05f, 1f)));
            SilhouetteMaxDistance = Config.Bind("4 - Silhouette", "MaxDistance", 80f,
                new ConfigDescription("Allies farther than this (metres) get no silhouette.", new AcceptableValueRange<float>(5f, 300f)));
            SilhouetteMode = Config.Bind("4 - Silhouette", "Style", SilhouetteStyle.Fill,
                "Fill = the hidden part of the ally is filled with the colour. Outline = only the outer edge of the hidden part. AlwaysOnTop = the whole ally, always (for testing).");
            SilhouetteOutlineWidth = Config.Bind("4 - Silhouette", "OutlineWidth", 3,
                new ConfigDescription("Thickness of the outline, in pixels.", new AcceptableValueRange<int>(1, 10)));
            SilhouetteMargin = Config.Bind("4 - Silhouette", "VisibleMargin", 15f,
                new ConfigDescription("Centimetres of tolerance when deciding that a part of the ally is seen directly. Raise it if feet or parts touching the ground get coloured; lower it if parts just behind a wall don't show.", new AcceptableValueRange<float>(0f, 50f)));
            SilhouetteOnSelf = Config.Bind("4 - Silhouette", "TestOnYourself", false,
                "Draws the silhouette on your own character too, so you can try it alone (stand behind a tree in third person).");

            CompassEnabled = Config.Bind("5 - Compass", "Enabled", true,
                "Shows each ally on the Nav Compass bar as a coloured circle with their initial (needs the NavCompass mod).");
            CompassMaxDistance = Config.Bind("5 - Compass", "MaxDistance", 0f,
                new ConfigDescription("Allies farther than this (metres) are not shown on the compass. 0 = always shown.", new AcceptableValueRange<float>(0f, 10000f)));
            CompassIconSize = Config.Bind("5 - Compass", "IconSize", 20f,
                new ConfigDescription("Size of the ally icon on the compass.", new AcceptableValueRange<float>(10f, 40f)));

            ChatColor = Config.Bind("6 - Chat", "Color", new Color(0.55f, 0.85f, 1f, 1f),
                "Colour of party chat messages. Type /p and your message in the chat to talk to your party only.");
            ChatInHud = Config.Bind("6 - Chat", "ShowInHud", true,
                "Also shows each ally's last message under their part of the party HUD for a few seconds.");
            ChatHudSeconds = Config.Bind("6 - Chat", "HudSeconds", 10f,
                new ConfigDescription("How long a message stays in the party HUD.", new AcceptableValueRange<float>(2f, 60f)));

            MapFindButton = Config.Bind("7 - Map", "FindPartyButton", true,
                "A button for each ally on the large map: click a name and the map zooms out, glides to that ally and zooms back in.");
            MapFindPosition = Config.Bind("7 - Map", "ButtonPosition", new Vector2(-30f, 80f),
                "Where the column of ally buttons sits on the screen, in pixels from the top-left corner. Negative numbers count from the right or bottom edge.");
            MapFindZoom = Config.Bind("7 - Map", "ZoomOnAlly", 0.035f,
                new ConfigDescription("How close the map zooms in on the ally (smaller = closer).", new AcceptableValueRange<float>(0.01f, 0.5f)));
            MapFindSpeed = Config.Bind("7 - Map", "FlightTime", 1f,
                new ConfigDescription("How long the trip across the map takes (1 = about 1.8 seconds).", new AcceptableValueRange<float>(0.2f, 4f)));

            DetailedLog = Config.Bind("9 - Debug", "DetailedLog", false, "Writes details to the BepInEx log.");

            Friends.Load();
            new Harmony(GUID).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo(NAME + " " + VERSION + " loaded");
        }

        private void Update()
        {
            Party.Update();
            Ui.Update();
            Gift.Update();
        }

        private void LateUpdate()
        {
            Silhouette.LateUpdate();
            Compass.LateUpdate();
        }

        private void OnGUI()
        {
            Ui.OnGUI();
            MapFind.OnGUI();
        }

        internal static void Debug(string msg)
        {
            if (DetailedLog.Value) Log.LogInfo(msg);
        }

        internal static void Message(string text)
        {
            Player p = Player.m_localPlayer;
            if (p != null) p.Message(MessageHud.MessageType.TopLeft, text);
            Debug("[msg] " + text);
        }

        internal static void Center(string text)
        {
            Player p = Player.m_localPlayer;
            if (p != null) p.Message(MessageHud.MessageType.Center, text);
            Debug("[msg] " + text);
        }

        // True when the player is typing somewhere or a game menu is in the way.
        internal static bool Typing()
        {
            if (Chat.instance != null && Chat.instance.HasFocus()) return true;
            if (Console.IsVisible() || TextInput.IsVisible() || Minimap.InTextInput()) return true;
            if (Menu.IsVisible()) return true;
            return false;
        }
    }
}
