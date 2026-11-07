using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Warband
{
    // Keys, the party HUD, the invite prompt and the party panel (drawn with Unity's immediate GUI).
    internal static class Ui
    {
        internal static bool PanelOpen;
        internal static bool Editing;
        internal static bool WantsCursor { get { return PanelOpen || Editing; } }

        private const float W = 220f;
        private static int _tab;
        private static Vector2 _scroll;
        private static string _friendName = "";
        private static Rect _window = new Rect(0, 0, 460, 500);
        private static bool _windowPlaced;

        private static Vector2 _editPos;
        private static float _editScale;
        private static bool _dragging;

        private static Texture2D _white;
        private static GUIStyle _name, _small, _tiny, _tinyLeft, _prompt, _bold, _initial, _barText;
        private static Font _font;
        private static bool _stylesReady;

        private static readonly Dictionary<string, Sprite> ItemIcons = new Dictionary<string, Sprite>();
        private static readonly Dictionary<int, Sprite> EffectIcons = new Dictionary<int, Sprite>();

        // ---------------------------------------------------------------- keys

        internal static void Update()
        {
            if (Player.m_localPlayer == null || ZNet.instance == null)
            {
                PanelOpen = false;
                if (Editing) StopEditing();
                return;
            }
            bool typing = Plugin.Typing() || (PanelOpen && GUIUtility.keyboardControl != 0);
            if (typing) return;
            bool inventory = InventoryGui.IsVisible();

            if (PanelOpen && Input.GetKeyDown(KeyCode.Escape)) { PanelOpen = false; return; }
            if (Plugin.PanelKey.Value.IsDown() && !inventory)
            {
                PanelOpen = !PanelOpen;
                GUIUtility.keyboardControl = 0;
                return;
            }
            if (Plugin.EditHudKey.Value.IsDown())
            {
                if (Editing) StopEditing(); else StartEditing();
                return;
            }
            if (Plugin.InviteKey.Value.IsDown() && !inventory && !PanelOpen)
            {
                Player target = LookTarget();
                if (target == null) Plugin.Message("Look at a player to invite them");
                else Party.InviteTo(target.GetZDOID().UserID, target.GetPlayerName());
                return;
            }
            if (Party.HasPendingInvite && !PanelOpen)
            {
                if (Plugin.AcceptKey.Value.IsDown()) Party.Accept();
                else if (Plugin.DeclineKey.Value.IsDown()) Party.Decline();
            }
        }

        private static void StartEditing()
        {
            Editing = true;
            _editPos = Plugin.HudPosition.Value;
            _editScale = Plugin.HudScale.Value;
            Plugin.Message("Party HUD: drag it, scroll to resize, " + Plugin.EditHudKey.Value + " to finish");
        }

        private static void StopEditing()
        {
            Editing = false;
            _dragging = false;
            Plugin.HudPosition.Value = _editPos;
            Plugin.HudScale.Value = _editScale;
        }

        // The other player closest to the middle of the screen.
        private static Player LookTarget()
        {
            Camera cam = Utils.GetMainCamera();
            if (cam == null) return null;
            Player best = null;
            float bestScore = 1f;
            foreach (Player p in Player.GetAllPlayers())
            {
                if (p == null || p == Player.m_localPlayer) continue;
                Vector3 d = p.transform.position + Vector3.up * 1.1f - cam.transform.position;
                float dist = d.magnitude;
                if (dist > 40f || dist < 0.1f) continue;
                float angle = Vector3.Angle(cam.transform.forward, d);
                float allowed = Mathf.Atan2(1f, dist) * Mathf.Rad2Deg + 2f;
                float score = angle / allowed;
                if (score < bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        // ---------------------------------------------------------------- drawing

        internal static void OnGUI()
        {
            if (Player.m_localPlayer == null || ZNet.instance == null) return;
            Styles();
            bool hudHidden = Hud.instance == null || Hud.instance.m_rootObject == null || !Hud.instance.m_rootObject.activeSelf;

            if (Plugin.ShowHud.Value && !hudHidden && !Minimap.IsOpen()) DrawHud();
            else if (Editing) DrawHud();

            if (Party.HasPendingInvite && !PanelOpen && !hudHidden) DrawPrompt();

            if (PanelOpen)
            {
                if (!_windowPlaced)
                {
                    _window.x = (Screen.width - _window.width) / 2f;
                    _window.y = (Screen.height - _window.height) / 2f;
                    _windowPlaced = true;
                }
                GUISkin oldSkin = GUI.skin;
                GUI.skin = Skin.Get(_font);
                _window = GUILayout.Window(0x5A4B3C, _window, DrawWindow, "", GUI.skin.window);
                GUI.skin = oldSkin;
            }
        }

        private static void Styles()
        {
            if (_stylesReady) return;
            _white = new Texture2D(1, 1);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
            foreach (Font f in Resources.FindObjectsOfTypeAll<Font>())
                if (f.name == "AveriaSerifLibre-Bold") { _font = f; break; }
            _name = new GUIStyle(GUI.skin.label);
            _name.fontSize = 14;
            _name.fontStyle = FontStyle.Bold;
            _name.normal.textColor = Color.white;
            _name.clipping = TextClipping.Clip;
            _name.wordWrap = false;
            if (_font != null) _name.font = _font;
            _small = new GUIStyle(_name);
            _small.fontSize = 12;
            _small.alignment = TextAnchor.UpperRight;
            _tiny = new GUIStyle(_name);
            _tiny.fontSize = 10;
            _tiny.alignment = TextAnchor.LowerRight;
            _tinyLeft = new GUIStyle(_name);
            _tinyLeft.fontSize = 12;
            _tinyLeft.alignment = TextAnchor.UpperLeft;
            _initial = new GUIStyle(_name);
            _initial.fontSize = 11;
            _initial.alignment = TextAnchor.MiddleCenter;
            _barText = new GUIStyle(_name);
            _barText.fontSize = 11;
            _barText.alignment = TextAnchor.MiddleCenter;
            _prompt = new GUIStyle(_name);
            _prompt.fontSize = 16;
            _prompt.alignment = TextAnchor.MiddleCenter;
            _prompt.wordWrap = true;
            _bold = new GUIStyle(GUI.skin.label);
            _bold.fontStyle = FontStyle.Bold;
            _bold.fontSize = 14;
            _bold.normal.textColor = Skin.Orange;
            if (_font != null) _bold.font = _font;
            _stylesReady = true;
        }

        private static void Box(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        private static void Text(Rect r, string text, GUIStyle style, Color color)
        {
            Text(r, text, style, color, true);
        }

        private static void Text(Rect r, string text, GUIStyle style, Color color, bool shadow)
        {
            Color old = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.9f);
            if (shadow) GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }

        private static void Icon(Rect r, Sprite s, float alpha)
        {
            if (s == null || s.texture == null) return;
            Rect tr = s.textureRect;
            float tw = s.texture.width, th = s.texture.height;
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTextureWithTexCoords(r, s.texture, new Rect(tr.x / tw, tr.y / th, tr.width / tw, tr.height / th));
            GUI.color = old;
        }

        private static void Bar(Rect r, float value, float max, Color c, float alpha)
        {
            Box(r, new Color(0f, 0f, 0f, 0.65f * alpha));
            float f = max > 0f ? Mathf.Clamp01(value / max) : 0f;
            if (f > 0f) Box(new Rect(r.x + 1, r.y + 1, (r.width - 2) * f, r.height - 2), new Color(c.r, c.g, c.b, alpha));
        }

        private static Sprite ItemIcon(string prefab)
        {
            if (string.IsNullOrEmpty(prefab) || ObjectDB.instance == null) return null;
            Sprite s;
            if (ItemIcons.TryGetValue(prefab, out s)) return s;
            GameObject go = ObjectDB.instance.GetItemPrefab(prefab);
            ItemDrop d = go != null ? go.GetComponent<ItemDrop>() : null;
            s = d != null ? d.m_itemData.GetIcon() : null;
            ItemIcons[prefab] = s;
            return s;
        }

        private static Sprite EffectIcon(int hash)
        {
            if (ObjectDB.instance == null) return null;
            Sprite s;
            if (EffectIcons.TryGetValue(hash, out s)) return s;
            StatusEffect se = ObjectDB.instance.GetStatusEffect(hash);
            if (se == null && Player.m_localPlayer != null) se = Player.m_localPlayer.GetSEMan().GetStatusEffect(hash);
            s = se != null ? se.m_icon : null;
            if (s != null || se != null) EffectIcons[hash] = s;
            return s;
        }

        private static string Time_(float seconds)
        {
            if (seconds >= 3600f) return Mathf.FloorToInt(seconds / 3600f) + "h";
            if (seconds >= 60f) return Mathf.FloorToInt(seconds / 60f) + "m";
            return Mathf.CeilToInt(seconds) + "s";
        }

        // ---------------------------------------------------------------- party HUD

        // Dummies shown while editing with nobody in the party, so the HUD can still be placed.
        private static List<Member> _dummies;

        private static List<Member> HudMembers()
        {
            List<Member> list = new List<Member>();
            long me = Party.Me;
            if (Party.InParty)
                foreach (Member m in Party.Members) if (m.Peer != me) list.Add(m);
            if (list.Count == 0 && Editing)
            {
                if (_dummies == null)
                {
                    _dummies = new List<Member>();
                    string[] names = { "Ally", "Another ally", "Third ally" };
                    for (int i = 0; i < names.Length; i++)
                    {
                        Member d = new Member();
                        d.Name = names[i];
                        d.Hp = 70 - i * 22; d.MaxHp = 80; d.Stam = 70 - i * 15; d.MaxStam = 100;
                        d.Eitr = i == 0 ? 30 : 0; d.MaxEitr = i == 0 ? 50 : 0;
                        d.Weapon = i == 0 ? "SwordIron" : (i == 1 ? "Bow" : "AxeBronze");
                        _dummies.Add(d);
                    }
                }
                foreach (Member d in _dummies) d.VitalsAt = Time.time;
                return _dummies;
            }
            Order.Sort(list);
            return list;
        }

        private static float BlockHeight(Member m)
        {
            float h = 20f + 15f;
            if (Plugin.ShowStamina.Value) h += 7f;
            if (Plugin.ShowEitr.Value && m.MaxEitr > 0f) h += 7f;
            if (Plugin.ShowBuffs.Value && Plugin.MaxBuffs.Value > 0 && m.Buffs.Count > 0) h += 22f;
            return Mathf.Max(h, Plugin.ShowWeapon.Value ? 40f : 0f) + 6f;
        }

        private const float HandleH = 36f;   // the "move" bar above the HUD in edit mode
        private const float Pad = 8f;        // room for the window frame around the list
        private static Member _dragMember;

        private static void DrawHud()
        {
            List<Member> list = HudMembers();
            if (list.Count == 0) return;
            Vector2 pos = Editing ? _editPos : Plugin.HudPosition.Value;
            float scale = Editing ? _editScale : Plugin.HudScale.Value;

            if (Editing) HandleDrag(list, pos, scale);

            float total = 0f;
            foreach (Member m in list) total += BlockHeight(m) + 4f;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(pos.x, pos.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

            DrawBackground(new Rect(-Pad, -Pad, W + Pad * 2, total - 4f + Pad * 2));
            if (Editing)
            {
                Box(new Rect(-Pad, -HandleH - Pad - 2, W + Pad * 2, HandleH), new Color(1f, 0.85f, 0.3f, 0.6f));
                Text(new Rect(2, -HandleH - Pad - 1, W + 4, HandleH), "Drag here to move the HUD\nDrag an ally to change the order", _tinyLeft, Color.white);
                Text(new Rect(0, total + Pad, W + 160, 20), "Scroll to resize · " + Plugin.EditHudKey.Value + " to finish", _tinyLeft, new Color(1f, 0.9f, 0.5f));
            }
            float y = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                Member m = list[i];
                if (i > 0) Box(new Rect(4, y - 3, W - 8, 1), new Color(1f, 0.9f, 0.7f, 0.18f));
                if (Editing && m == _dragMember) Box(new Rect(-2, y - 2, W + 4, BlockHeight(m)), new Color(1f, 0.85f, 0.3f, 0.35f));
                DrawMember(m, y);
                y += BlockHeight(m) + 4f;
            }
            GUI.matrix = oldMatrix;
        }

        // The inventory window's background and frame (same look as Resource Scanner), or a dark box.
        private static void DrawBackground(Rect r)
        {
            DrawFrame(r, Plugin.HudBackgroundOpacity.Value, 0.5f);
        }

        // k: size of the frame relative to the inventory window.
        private static void DrawFrame(Rect r, float op, float k)
        {
            if (op <= 0f) return;
            List<Looks.Layer> layers = Looks.Background();
            if (layers == null || layers.Count == 0) { Box(r, new Color(0f, 0f, 0f, 0.6f * op)); return; }
            foreach (Looks.Layer l in layers)
            {
                Rect d = new Rect(r.x + l.OffsetMin.x * k, r.y - l.OffsetMax.y * k,
                    r.width - (l.OffsetMin.x - l.OffsetMax.x) * k, r.height - (l.OffsetMin.y - l.OffsetMax.y) * k);
                Color c = l.Color;
                c.a *= op;
                Looks.NineSlice(d, l.Sprite, c, k);
            }
        }

        // Index of the block under a local y (HUD coordinates), clamped to the list.
        private static int BlockAt(List<Member> list, float localY)
        {
            float y = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                y += BlockHeight(list[i]) + 4f;
                if (localY < y) return i;
            }
            return list.Count - 1;
        }

        private static void HandleDrag(List<Member> list, Vector2 pos, float scale)
        {
            Event e = Event.current;
            float total = 0f;
            foreach (Member m in list) total += BlockHeight(m) + 4f;
            Rect handle = new Rect(pos.x - Pad * scale, pos.y - (HandleH + Pad + 2) * scale, (W + Pad * 2) * scale, HandleH * scale);
            Rect body = new Rect(pos.x, pos.y, W * scale, total * scale);

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (handle.Contains(e.mousePosition)) { _dragging = true; e.Use(); }
                else if (body.Contains(e.mousePosition))
                {
                    _dragMember = list[BlockAt(list, (e.mousePosition.y - pos.y) / scale)];
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && _dragging)
            {
                _editPos += e.delta;
                _editPos.x = Mathf.Clamp(_editPos.x, 0f, Screen.width - 40f);
                _editPos.y = Mathf.Clamp(_editPos.y, (HandleH + Pad) * scale, Screen.height - 40f);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _dragMember != null)
            {
                int from = list.IndexOf(_dragMember);
                int to = BlockAt(list, (e.mousePosition.y - pos.y) / scale);
                if (from >= 0 && to != from)
                {
                    list.RemoveAt(from);
                    list.Insert(to, _dragMember);
                    if (list != _dummies) Order.Save(list);
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && (_dragging || _dragMember != null))
            {
                _dragging = false;
                _dragMember = null;
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && (body.Contains(e.mousePosition) || handle.Contains(e.mousePosition)))
            {
                _editScale = Mathf.Clamp(_editScale - e.delta.y * 0.03f, 0.5f, 2.5f);
                e.Use();
            }
        }

        // A dead ally's block shakes hard for a moment, then keeps trembling until they respawn.
        private static Vector2 Shake(Member m, float now)
        {
            if (m.DeadSince < 0f) return Vector2.zero;
            float t = now - m.DeadSince;
            float amp = t < 1.2f ? 1.2f + 4f * (1f - t / 1.2f) : 1.2f;
            return new Vector2(Mathf.Sin(now * 47f) * amp, Mathf.Cos(now * 31f) * amp * 0.5f);
        }

        private static void DrawMember(Member m, float y0)
        {
            float now = Time.time;
            bool stale = now - m.VitalsAt > 3f;
            bool dead = m.DeadSince >= 0f;
            float a = stale || dead ? 0.6f : 1f;
            Vector2 sh = Shake(m, now);
            float y = y0 + sh.y;
            float x = sh.x;

            if (Plugin.ShowWeapon.Value)
            {
                Box(new Rect(x, y + 3, 36, 36), new Color(0f, 0f, 0f, 0.4f));
                Icon(new Rect(x + 3, y + 6, 30, 30), ItemIcon(m.Weapon), a);
                x += 42f;
            }
            float bw = W - x + sh.x - 2f;

            // Coloured circle with the initial, then the name.
            Color col = Looks.ColorOf(m);
            Color old = GUI.color;
            GUI.color = new Color(col.r, col.g, col.b, a);
            GUI.DrawTexture(new Rect(x, y + 3, 15, 15), Looks.Circle);
            GUI.color = old;
            Text(new Rect(x, y + 2, 15, 16), Looks.Initial(m.Name), _initial, new Color(0.08f, 0.06f, 0.04f, a), false);

            string status = stale ? "  (no signal)" : (dead ? "  (dead)" : "");
            Color nameColor = dead ? new Color(1f, 0.45f, 0.4f, 1f) : new Color(1f, 1f, 1f, a);
            Text(new Rect(x + 19, y + 1, bw - 19, 20), m.Name + status, _name, nameColor);
            if (Plugin.ShowDistance.Value && Player.m_localPlayer != null && m.Peer != 0)
            {
                float dist = Distance(m);
                if (dist >= 0f) Text(new Rect(x, y + 3, bw, 18), Mathf.RoundToInt(dist) + " m", _small, new Color(0.9f, 0.9f, 0.85f, a));
            }

            // Health bar with current / max in the middle.
            float by = y + 21f;
            Bar(new Rect(x, by, bw, 14), m.Hp, m.MaxHp, new Color(0.78f, 0.12f, 0.08f), a);
            Text(new Rect(x, by - 1, bw, 16), Mathf.CeilToInt(Mathf.Max(0f, m.Hp)) + " / " + Mathf.CeilToInt(m.MaxHp), _barText, new Color(1f, 1f, 1f, a));
            by += 15f;
            if (Plugin.ShowStamina.Value)
            {
                Bar(new Rect(x, by, bw, 6), m.Stam, m.MaxStam, new Color(0.95f, 0.75f, 0.15f), a);
                by += 7f;
            }
            if (Plugin.ShowEitr.Value && m.MaxEitr > 0f)
            {
                Bar(new Rect(x, by, bw, 6), m.Eitr, m.MaxEitr, new Color(0.35f, 0.5f, 1f), a);
                by += 7f;
            }
            if (Plugin.ShowBuffs.Value && Plugin.MaxBuffs.Value > 0 && m.Buffs.Count > 0)
            {
                float bx = x;
                int shown = 0;
                float age = now - m.VitalsAt;
                foreach (Buff b in m.Buffs)
                {
                    if (shown >= Plugin.MaxBuffs.Value || bx + 20f > W + sh.x) break;
                    Sprite s = EffectIcon(b.Hash);
                    if (s == null) continue;
                    Rect r = new Rect(bx, by + 2, 20, 20);
                    Icon(r, s, a);
                    if (b.Remaining >= 0f)
                        Text(new Rect(r.x - 6, r.y + 6, r.width + 6, 16), Time_(Mathf.Max(0f, b.Remaining - age)), _tiny, new Color(1f, 1f, 1f, a));
                    bx += 22f;
                    shown++;
                }
            }
        }

        // Distance to an ally, from its character when loaded, else from the position it sent.
        internal static float Distance(Member m)
        {
            Player me = Player.m_localPlayer;
            if (me == null) return -1f;
            Vector3 pos;
            if (!AllyPosition(m, out pos)) return -1f;
            return Vector3.Distance(me.transform.position, pos);
        }

        internal static bool AllyPosition(Member m, out Vector3 pos)
        {
            Player p = Party.FindPlayer(m.Peer);
            if (p != null) { pos = p.transform.position; return true; }
            pos = m.Pos;
            return m.HasPos;
        }

        private static void DrawPrompt()
        {
            string text = Party.PendingFrom + " invited you to their party\n[" + Plugin.AcceptKey.Value + "] accept      [" + Plugin.DeclineKey.Value + "] decline";
            Rect r = new Rect((Screen.width - 460) / 2f, Screen.height * 0.18f, 460, 56);
            Box(r, new Color(0f, 0f, 0f, 0.55f));
            Text(r, text, _prompt, new Color(1f, 0.9f, 0.6f));
        }

        // ---------------------------------------------------------------- party panel

        private static void DrawWindow(int id)
        {
            if (Event.current.type == EventType.Repaint) DrawFrame(new Rect(0, 0, _window.width, _window.height), 1f, 1f);
            GUI.Label(new Rect(0, 8, _window.width, 30), "Warband", Skin.Title);
            if (GUI.Button(new Rect(_window.width - 40, 12, 26, 24), "x")) PanelOpen = false;

            if (Party.HasPendingInvite)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(Party.PendingFrom + " invited you to their party", _bold);
                if (GUILayout.Button("Accept", GUILayout.Width(70))) Party.Accept();
                if (GUILayout.Button("Decline", GUILayout.Width(70))) Party.Decline();
                GUILayout.EndHorizontal();
            }

            if (Party.InParty)
            {
                Member lead = Party.Get(Party.Leader);
                GUILayout.Label("Party: " + Party.Members.Count + " players, led by " + (Party.IsLeader ? "you" : (lead != null ? lead.Name : "?")));
            }
            else GUILayout.Label("You are not in a party. Invite someone from the list, or look at them and press " + Plugin.InviteKey.Value + ".");

            _tab = GUILayout.Toolbar(_tab, new string[] { "Players (" + Party.OnlinePlayers.Count + ")", "Friends (" + Friends.List.Count + ")", "Party" });
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            if (_tab == 0) PlayersTab();
            else if (_tab == 1) FriendsTab();
            else PartyTab();
            GUILayout.EndScrollView();
            GUILayout.Label(Plugin.PanelKey.Value + " or Esc closes this panel", _tinyLabel());
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        private static GUIStyle _tinyLabelStyle;
        private static GUIStyle _tinyLabel()
        {
            if (_tinyLabelStyle == null)
            {
                _tinyLabelStyle = new GUIStyle(GUI.skin.label);
                _tinyLabelStyle.fontSize = 11;
                if (_font != null) _tinyLabelStyle.font = _font;
                _tinyLabelStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            }
            return _tinyLabelStyle;
        }

        // An on/off switch drawn as a game button plus its label.
        private static bool Switch(bool value, string text)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(value ? "On" : "Off", GUILayout.Width(52))) value = !value;
            GUILayout.Label(text);
            GUILayout.EndHorizontal();
            return value;
        }

        private static bool CanInvite(long peer)
        {
            return peer != 0 && !(Party.InParty && Party.Get(peer) != null) && Party.Members.Count < Plugin.MaxMembers.Value;
        }

        private static void PlayersTab()
        {
            if (Party.OnlinePlayers.Count == 0) { GUILayout.Label("Nobody else is on the server."); return; }
            foreach (Party.Online o in Party.OnlinePlayers)
            {
                bool friend = Friends.IsFriend(o.UserId, o.Name);
                bool inParty = Party.IsAlly(o.Peer);
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label((friend ? "* " : "") + o.Name + (inParty ? "   (in your party)" : ""), friend ? _bold : GUI.skin.label);
                GUILayout.FlexibleSpace();
                GUI.enabled = CanInvite(o.Peer);
                if (GUILayout.Button("Invite", GUILayout.Width(70))) Party.InviteTo(o.Peer, o.Name);
                GUI.enabled = !friend;
                if (GUILayout.Button(friend ? "Friend" : "Add friend", GUILayout.Width(90))) Friends.Add(o.UserId, o.Name);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private static void FriendsTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:", GUILayout.Width(45));
            _friendName = GUILayout.TextField(_friendName, 32);
            GUI.enabled = _friendName.Trim().Length > 0;
            if (GUILayout.Button("Add friend", GUILayout.Width(90)))
            {
                string name = _friendName.Trim();
                Party.Online o = null;
                foreach (Party.Online x in Party.OnlinePlayers)
                    if (string.Equals(x.Name, name, System.StringComparison.OrdinalIgnoreCase)) o = x;
                Friends.Add(o != null ? o.UserId : "", o != null ? o.Name : name);
                _friendName = "";
                GUIUtility.keyboardControl = 0;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            bool auto = Switch(Plugin.AutoAcceptFriends.Value, "Accept invites from friends on their own");
            if (auto != Plugin.AutoAcceptFriends.Value) Plugin.AutoAcceptFriends.Value = auto;

            if (Friends.List.Count == 0) { GUILayout.Label("No friends yet. Add them here or from the Players tab."); return; }
            Friend remove = null;
            foreach (Friend f in Friends.List)
            {
                Party.Online o = Party.FindOnlineByFriend(f);
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(f.Name + (o != null ? "   (online)" : "   (offline)"), o != null ? _bold : GUI.skin.label);
                GUILayout.FlexibleSpace();
                GUI.enabled = o != null && CanInvite(o.Peer);
                if (GUILayout.Button("Invite", GUILayout.Width(70)) && o != null) Party.InviteTo(o.Peer, o.Name);
                GUI.enabled = true;
                if (GUILayout.Button("Remove", GUILayout.Width(70))) remove = f;
                GUILayout.EndHorizontal();
            }
            if (remove != null) Friends.Remove(remove);
        }

        private static void PartyTab()
        {
            if (!Party.InParty) GUILayout.Label("No party yet.");
            else
            {
                long me = Party.Me;
                long kick = 0, promote = 0;
                foreach (Member m in Party.Members)
                {
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    string tag = (m.Peer == Party.Leader ? "  (leader)" : "") + (m.Peer == me ? "  (you)" : "");
                    GUILayout.Label(m.Name + tag, m.Peer == Party.Leader ? _bold : GUI.skin.label);
                    GUILayout.FlexibleSpace();
                    if (Party.IsLeader && m.Peer != me)
                    {
                        if (GUILayout.Button("Make leader", GUILayout.Width(95))) promote = m.Peer;
                        if (GUILayout.Button("Remove", GUILayout.Width(70))) kick = m.Peer;
                    }
                    GUILayout.EndHorizontal();
                }
                if (kick != 0) Party.Kick(kick);
                if (promote != 0) Party.MakeLeader(promote);
                if (GUILayout.Button("Leave party")) Party.Leave();
            }

            GUILayout.Space(10);
            GUILayout.Label("Options", _bold);
            bool v;
            v = Switch(Plugin.ShowHud.Value, "Party HUD (health, stamina, eitr, effects, weapon)");
            if (v != Plugin.ShowHud.Value) Plugin.ShowHud.Value = v;
            v = Switch(Plugin.SilhouetteEnabled.Value, "See allies through walls (silhouette)");
            if (v != Plugin.SilhouetteEnabled.Value) Plugin.SilhouetteEnabled.Value = v;
            v = Switch(Plugin.CompassEnabled.Value, "Allies on the compass");
            if (v != Plugin.CompassEnabled.Value) Plugin.CompassEnabled.Value = v;
            if (GUILayout.Button("Move / resize the party HUD (" + Plugin.EditHudKey.Value + ")"))
            {
                PanelOpen = false;
                StartEditing();
            }
        }
    }

    // While the panel or the HUD editor is open: free mouse, no camera turning or attacking.
    [HarmonyPatch]
    internal static class InputBlock
    {
        [HarmonyPatch(typeof(GameCamera), "UpdateMouseCapture")]
        [HarmonyPrefix]
        private static bool Mouse()
        {
            if (!Ui.WantsCursor) return true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return false;
        }

        [HarmonyPatch(typeof(PlayerController), "TakeInput")]
        [HarmonyPostfix]
        private static void NoInput(ref bool __result)
        {
            if (Ui.WantsCursor) __result = false;
        }
    }
}
