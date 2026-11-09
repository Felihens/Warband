using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Warband
{
    // Send an item to a shieldmate: with the inventory open, hover an item and press the send key (P),
    // pick a party member, and they get a prompt to accept or decline. They must be close (SendRange).
    //
    // The item never vanishes or doubles:
    //  1. Offer: only the item's description travels; the item stays in your bag.
    //  2. They accept (and their game checks there is room; a full bag declines on its own).
    //  3. Your game checks the item is still there, unchanged, and they are still close; only then
    //     it leaves your bag and the whole item (quality, durability, crafter, other mods' data) is sent.
    //  4. Their game puts it in the bag, or at their feet if the bag filled up meanwhile, and says so.
    //     If they leave the game before saying they got it, the item comes back to you.
    internal static class Gift
    {
        internal const string Rpc = "Warband_Gift";
        private const int Offer = 0, Accept = 1, Decline = 2, Deliver = 3, Got = 4;
        private const int WhyDeclined = 0, WhyFull = 1, WhyBusy = 2, WhyFar = 3, WhyTimeout = 4, WhyGone = 5;
        private const float OfferTimeout = 30f, DeliverTimeout = 20f;

        private class Outgoing
        {
            public int Id;
            public long To;
            public string ToName = "";
            public ItemDrop.ItemData Item;
            public string Prefab = "";
            public int Stack, Quality, Variant;
            public float Durability;
            public float At;
            public bool Sent;              // the item left your bag and is on its way
            public ZPackage Backup;        // the item as sent, to give it back if it never arrives
        }

        private class Incoming
        {
            public int Id;
            public long From;
            public string FromName = "";
            public string Prefab = "";
            public string Label = "";
            public int Stack, Quality;
            public float At;
        }

        private static readonly List<Outgoing> Out = new List<Outgoing>();
        private static Incoming _offer;            // waiting for your answer
        private static ItemDrop.ItemData _picking; // the item you are choosing a shieldmate for
        private static Rect _pickRect;
        private static int _nextId = UnityEngine.Random.Range(1, 1000000);

        internal static bool HasOffer { get { return _offer != null; } }

        // ---------------------------------------------------------------- keys, per frame

        internal static void Update()
        {
            float now = Time.time;
            for (int i = Out.Count - 1; i >= 0; i--)
            {
                Outgoing o = Out[i];
                if (!o.Sent && now - o.At > OfferTimeout + 5f)
                {
                    Out.RemoveAt(i);
                    Plugin.Message(o.ToName + " didn't answer: the " + Name(o.Prefab) + " stays with you");
                }
                else if (o.Sent && now - o.At > DeliverTimeout && Party.FindOnline(o.To) == null)
                {
                    Out.RemoveAt(i);
                    GiveBack(o, o.ToName + " left before getting it");
                }
                else if (o.Sent && now - o.At > DeliverTimeout * 6f) Out.RemoveAt(i);   // they're online: it arrived
            }
            if (_offer != null && now - _offer.At > OfferTimeout)
            {
                Answer(_offer, Decline, WhyTimeout);
                Plugin.Message("You let the offer from " + _offer.FromName + " pass");
                _offer = null;
            }

            if (Player.m_localPlayer == null || Plugin.Typing()) { _picking = null; return; }
            if (!InventoryGui.IsVisible()) _picking = null;
            else if (Plugin.SendKey.Value.IsDown())
            {
                ItemDrop.ItemData item = Hovered();
                if (item == null) Plugin.Message("Hover an item in your bag to send it");
                else StartPicking(item);
            }
            if (_picking != null && Input.GetKeyDown(KeyCode.Escape)) _picking = null;

            if (_offer != null && !Party.HasPendingInvite && !Ui.PanelOpen)
            {
                if (Plugin.AcceptKey.Value.IsDown()) TakeOffer();
                else if (Plugin.DeclineKey.Value.IsDown())
                {
                    Answer(_offer, Decline, WhyDeclined);
                    Plugin.Message("You declined the " + _offer.Label);
                    _offer = null;
                }
            }
        }

        private static ItemDrop.ItemData Hovered()
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || gui.m_playerGrid == null) return null;
            Vector3 m = Input.mousePosition;
            return gui.m_playerGrid.GetItem(new Vector2i((int)m.x, (int)m.y));
        }

        private static void StartPicking(ItemDrop.ItemData item)
        {
            if (!Party.InParty) { Plugin.Message("You are not in a party"); return; }
            Player me = Player.m_localPlayer;
            if (me.IsItemEquiped(item)) { Plugin.Message("Unequip the " + Label(item) + " first"); return; }
            _picking = item;
            Vector3 m = Input.mousePosition;
            _pickRect = new Rect(Mathf.Clamp(m.x + 24f, 10f, Screen.width - 290f), Mathf.Clamp(Screen.height - m.y - 20f, 10f, Screen.height - 300f), 280f, 0f);
        }

        // ---------------------------------------------------------------- sending

        private static void SendOffer(ItemDrop.ItemData item, Member to)
        {
            Player me = Player.m_localPlayer;
            if (me == null || item == null || !me.GetInventory().ContainsItem(item)) return;
            if (item.m_dropPrefab == null) { Plugin.Message("This item can't be sent"); return; }
            float d = Ui.Distance(to);
            if (d < 0f || d > Plugin.SendRange.Value) { Plugin.Message(to.Name + " is too far (they must be within " + Plugin.SendRange.Value.ToString("0") + " m)"); return; }
            foreach (Outgoing x in Out)
                if (x.Item == item) { Plugin.Message("You already offered that to " + x.ToName); return; }

            Outgoing o = new Outgoing
            {
                Id = _nextId++, To = to.Peer, ToName = to.Name, Item = item, Prefab = item.m_dropPrefab.name,
                Stack = item.m_stack, Quality = item.m_quality, Variant = item.m_variant, Durability = item.m_durability, At = Time.time,
            };
            Out.Add(o);
            ZPackage pkg = Head(Offer, o.Id);
            pkg.Write(Party.MyName());
            pkg.Write(o.Prefab);
            pkg.Write(Label(item));
            pkg.Write(o.Stack);
            pkg.Write(o.Quality);
            Send(o.To, pkg);
            Plugin.Message("You offer " + Label(item) + " to " + to.Name);
            Plugin.Debug("[gift] offer #" + o.Id + " " + o.Stack + "x " + o.Prefab + " q" + o.Quality + " to " + to.Name);
        }

        private static void OnAccept(long from, int id)
        {
            Outgoing o = Find(id, from);
            if (o == null || o.Sent) return;
            Player me = Player.m_localPlayer;
            Inventory inv = me != null ? me.GetInventory() : null;
            ItemDrop.ItemData it = o.Item;
            string fail = null;
            if (inv == null || !inv.ContainsItem(it)) fail = "it's no longer in your bag";
            else if (it.m_stack != o.Stack || it.m_quality != o.Quality || it.m_variant != o.Variant) fail = "it changed since you offered it";
            else if (me.IsItemEquiped(it)) fail = "you are using it";
            else
            {
                Member m = Party.Get(from);
                float d = m != null ? Ui.Distance(m) : -1f;
                if (d < 0f || d > Plugin.SendRange.Value + 5f) fail = o.ToName + " walked too far away";
            }
            if (fail != null)
            {
                Out.Remove(o);
                Answer(from, id, Decline, WhyDeclined);
                Plugin.Message("Not sent: " + fail);
                return;
            }

            ZPackage data = Pack(it);
            inv.RemoveItem(it);
            o.Sent = true;
            o.At = Time.time;
            o.Backup = new ZPackage(data.GetArray());
            ZPackage pkg = Head(Deliver, id);
            pkg.Write(data);
            Send(from, pkg);
            Plugin.Message(o.ToName + " took the " + Name(o.Prefab));
            Plugin.Debug("[gift] #" + id + " sent to " + o.ToName);
        }

        private static void OnGot(long from, int id)
        {
            Outgoing o = Find(id, from);
            if (o != null) Out.Remove(o);
            Plugin.Debug("[gift] #" + id + " arrived");
        }

        private static void OnDeclined(long from, int id, int why)
        {
            Outgoing o = Find(id, from);
            if (o == null) return;
            Out.Remove(o);
            if (o.Sent) { GiveBack(o, o.ToName + " couldn't take it"); return; }
            string n = Name(o.Prefab);
            switch (why)
            {
                case WhyFull: Plugin.Message(o.ToName + "'s bag is full: the " + n + " stays with you"); break;
                case WhyBusy: Plugin.Message(o.ToName + " is answering another offer, try again in a moment"); break;
                case WhyFar: Plugin.Message(o.ToName + " is too far away"); break;
                case WhyTimeout: Plugin.Message(o.ToName + " didn't answer: the " + n + " stays with you"); break;
                default: Plugin.Message(o.ToName + " declined the " + n); break;
            }
        }

        private static void GiveBack(Outgoing o, string why)
        {
            if (o.Backup == null) return;
            ItemDrop.ItemData it = Unpack(new ZPackage(o.Backup.GetArray()));
            Player me = Player.m_localPlayer;
            if (it == null || me == null) return;
            if (!me.GetInventory().AddItem(it)) ItemDrop.DropItem(it, it.m_stack, me.transform.position + me.transform.forward + Vector3.up, Quaternion.identity);
            Plugin.Message(why + ": the " + Name(o.Prefab) + " is back with you");
            Plugin.Debug("[gift] #" + o.Id + " given back");
        }

        // ---------------------------------------------------------------- receiving

        private static void OnOffer(long from, ZPackage pkg, int id)
        {
            Incoming x = new Incoming { Id = id, From = from, At = Time.time };
            x.FromName = pkg.ReadString();
            x.Prefab = pkg.ReadString();
            x.Label = pkg.ReadString();
            x.Stack = pkg.ReadInt();
            x.Quality = pkg.ReadInt();
            if (!Party.IsAlly(from)) { Answer(x, Decline, WhyDeclined); return; }
            if (_offer != null) { Answer(x, Decline, WhyBusy); return; }
            Member m = Party.Get(from);
            float d = m != null ? Ui.Distance(m) : -1f;
            if (d < 0f || d > Plugin.SendRange.Value + 5f) { Answer(x, Decline, WhyFar); return; }
            if (!Room(x.Prefab, x.Stack, x.Quality))
            {
                Answer(x, Decline, WhyFull);
                Plugin.Message(x.FromName + " tried to send you " + x.Label + ", but your bag is full");
                return;
            }
            _offer = x;
            Plugin.Debug("[gift] offer #" + id + " from " + x.FromName + ": " + x.Label);
        }

        private static void TakeOffer()
        {
            Incoming x = _offer;
            _offer = null;
            if (!Room(x.Prefab, x.Stack, x.Quality))
            {
                Answer(x, Decline, WhyFull);
                Plugin.Message("Your bag is full: the offer from " + x.FromName + " was declined");
                return;
            }
            Answer(x, Accept, 0);
        }

        private static void OnDeliver(long from, ZPackage pkg, int id)
        {
            ItemDrop.ItemData it = Unpack(pkg.ReadPackage());
            Player me = Player.m_localPlayer;
            if (it == null || me == null) { Answer(from, id, Decline, WhyDeclined); return; }
            Answer(from, id, Got, 0);
            string who = Party.Get(from) != null ? Party.Get(from).Name : "A shieldmate";
            string label = Label(it);
            if (me.GetInventory().AddItem(it)) Plugin.Message(who + " gave you " + label);
            else
            {
                ItemDrop.DropItem(it, it.m_stack, me.transform.position + me.transform.forward + Vector3.up, Quaternion.identity);
                Plugin.Message(who + " gave you " + label + " (your bag was full: it's at your feet)");
            }
            Plugin.Debug("[gift] #" + id + " received: " + label);
        }

        private static bool Room(string prefab, int stack, int quality)
        {
            Player me = Player.m_localPlayer;
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            if (me == null || go == null) return false;
            ItemDrop.ItemData probe = go.GetComponent<ItemDrop>().m_itemData.Clone();
            probe.m_dropPrefab = go;
            probe.m_quality = quality;
            probe.m_stack = stack;
            return me.GetInventory().CanAddItem(probe, stack);
        }

        // ---------------------------------------------------------------- the item on the wire

        private static ZPackage Pack(ItemDrop.ItemData it)
        {
            ZPackage p = new ZPackage();
            p.Write(1);   // format
            p.Write(it.m_dropPrefab.name);
            p.Write(it.m_stack);
            p.Write(it.m_durability);
            p.Write(it.m_quality);
            p.Write(it.m_variant);
            p.Write(it.m_crafterID);
            p.Write(it.m_crafterName ?? "");
            p.Write(it.m_worldLevel);
            Dictionary<string, string> cd = it.m_customData ?? new Dictionary<string, string>();
            p.Write(cd.Count);
            foreach (KeyValuePair<string, string> kv in cd) { p.Write(kv.Key); p.Write(kv.Value ?? ""); }
            return p;
        }

        private static ItemDrop.ItemData Unpack(ZPackage p)
        {
            p.SetPos(0);
            if (p.ReadInt() != 1) return null;
            string prefab = p.ReadString();
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            if (go == null || go.GetComponent<ItemDrop>() == null) { Plugin.Log.LogWarning("Gift: unknown item " + prefab); return null; }
            ItemDrop.ItemData it = go.GetComponent<ItemDrop>().m_itemData.Clone();
            it.m_dropPrefab = go;
            it.m_stack = p.ReadInt();
            it.m_durability = p.ReadSingle();
            it.m_quality = p.ReadInt();
            it.m_variant = p.ReadInt();
            it.m_crafterID = p.ReadLong();
            it.m_crafterName = p.ReadString();
            it.m_worldLevel = p.ReadInt();
            int n = p.ReadInt();
            it.m_customData = new Dictionary<string, string>();
            for (int i = 0; i < n; i++) { string k = p.ReadString(); it.m_customData[k] = p.ReadString(); }
            it.m_pickedUp = true;
            it.m_equipped = false;
            return it;
        }

        // ---------------------------------------------------------------- messages

        private static ZPackage Head(int kind, int id)
        {
            ZPackage p = new ZPackage();
            p.Write(kind);
            p.Write(id);
            return p;
        }

        private static void Send(long peer, ZPackage pkg)
        {
            if (ZRoutedRpc.instance != null && peer != 0) ZRoutedRpc.instance.InvokeRoutedRPC(peer, Rpc, new object[] { pkg });
        }

        private static void Answer(Incoming x, int kind, int why) { Answer(x.From, x.Id, kind, why); }

        private static void Answer(long peer, int id, int kind, int why)
        {
            ZPackage p = Head(kind, id);
            p.Write(why);
            Send(peer, p);
        }

        internal static void OnMessage(long from, ZPackage pkg)
        {
            try
            {
                int kind = pkg.ReadInt(), id = pkg.ReadInt();
                switch (kind)
                {
                    case Offer: OnOffer(from, pkg, id); break;
                    case Accept: pkg.ReadInt(); OnAccept(from, id); break;
                    case Decline: OnDeclined(from, id, pkg.ReadInt()); break;
                    case Deliver: OnDeliver(from, pkg, id); break;
                    case Got: OnGot(from, id); break;
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Gift message failed: " + e.Message); }
        }

        private static Outgoing Find(int id, long peer)
        {
            foreach (Outgoing o in Out) if (o.Id == id && o.To == peer) return o;
            return null;
        }

        private static string Name(string prefab)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop d = go != null ? go.GetComponent<ItemDrop>() : null;
            return d != null ? Localization.instance.Localize(d.m_itemData.m_shared.m_name) : prefab;
        }

        private static string Label(ItemDrop.ItemData it)
        {
            string n = Localization.instance.Localize(it.m_shared.m_name);
            if (it.m_quality > 1) n += " (level " + it.m_quality + ")";
            return it.m_stack > 1 ? it.m_stack + "x " + n : n;
        }

        // ---------------------------------------------------------------- drawing

        internal static void OnGUI(GUIStyle prompt, Font font, Action<Rect, Color> box, Action<Rect, string, GUIStyle, Color> text)
        {
            if (_picking != null && InventoryGui.IsVisible()) DrawPicker(font);
            if (_offer != null && !Party.HasPendingInvite && !Ui.PanelOpen)
            {
                string t = _offer.FromName + " wants to give you " + _offer.Label + "\n[" + Plugin.AcceptKey.Value + "] accept      [" + Plugin.DeclineKey.Value + "] decline";
                Rect r = new Rect((Screen.width - 500) / 2f, Screen.height * 0.26f, 500, 56);
                box(r, new Color(0f, 0f, 0f, 0.55f));
                text(r, t, prompt, new Color(0.75f, 1f, 0.7f));
            }
        }

        private static void DrawPicker(Font font)
        {
            List<Member> allies = new List<Member>();
            foreach (Member m in Party.Members) if (m.Peer != Party.Me) allies.Add(m);
            GUISkin old = GUI.skin;
            GUI.skin = Skin.Get(font);
            _pickRect.height = 0f;
            _pickRect = GUILayout.Window(0x5A4B3D, _pickRect, id =>
            {
                ItemDrop.ItemData it = _picking;
                if (it == null) return;
                GUILayout.Label("Send " + Label(it) + " to:");
                if (allies.Count == 0) GUILayout.Label("No one else is in your party.");
                foreach (Member m in allies)
                {
                    float d = Ui.Distance(m);
                    bool near = d >= 0f && d <= Plugin.SendRange.Value;
                    string dist = d < 0f ? "far away" : d.ToString("0") + " m";
                    GUI.enabled = near;
                    if (GUILayout.Button(m.Name + "   (" + dist + ")"))
                    {
                        _picking = null;
                        SendOffer(it, m);
                    }
                    GUI.enabled = true;
                }
                GUILayout.Label("Within " + Plugin.SendRange.Value.ToString("0") + " m.   Esc closes.");
                if (GUILayout.Button("Cancel")) _picking = null;
                GUI.DragWindow();
            }, "", GUI.skin.window);
            GUI.skin = old;
        }
    }
}
