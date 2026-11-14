using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Warband
{
    // One button per ally on the large map. Click a name and the map zooms out, glides to that ally and
    // zooms back in, with a ring and the ally's name in the middle.
    // Positions come from the party (the same ones the HUD uses), so it works even when the ally hides
    // from the map.
    [HarmonyPatch]
    internal static class MapFind
    {
        private static readonly FieldInfo Offset = AccessTools.Field(typeof(Minimap), "m_mapOffset");
        private static readonly FieldInfo Inertia = AccessTools.Field(typeof(Minimap), "m_zoomInertia");
        private static readonly FieldInfo Drag = AccessTools.Field(typeof(Minimap), "m_dragView");

        private const float WorldWidth = 2048f * 12f;   // what the map shows at zoom 1, roughly

        private static long _target;        // ally we fly to (peer), 0 = none
        private static string _targetName = "";
        private static float _start = -1f;  // when the flight started
        private static Vector3 _fromOffset;
        private static float _fromZoom, _outZoom, _inZoom;
        private static float _arrived = -100f;
        private static Rect _area;          // the whole column of buttons
        private static Font _font;
        private static GUIStyle _label;
        private static Texture2D _ring;

        private static float OutTime { get { return 0.45f * Plugin.MapFindSpeed.Value; } }
        private static float MoveTime { get { return 0.8f * Plugin.MapFindSpeed.Value; } }
        private static float InTime { get { return 0.55f * Plugin.MapFindSpeed.Value; } }
        private static bool Flying { get { return _start >= 0f; } }

        private static bool LargeMap()
        {
            return Minimap.instance != null && Minimap.instance.m_mode == Minimap.MapMode.Large && Player.m_localPlayer != null;
        }

        // every party member but you; the bool says whether we know where they are
        private static List<KeyValuePair<Member, bool>> Allies()
        {
            List<KeyValuePair<Member, bool>> list = new List<KeyValuePair<Member, bool>>();
            if (!Party.InParty) return list;
            foreach (Member m in Party.Members)
            {
                if (m.Peer == Party.Me) continue;
                Vector3 pos;
                list.Add(new KeyValuePair<Member, bool>(m, Ui.AllyPosition(m, out pos)));
            }
            return list;
        }

        // ------------------------------------------------------------ button

        internal static void OnGUI()
        {
            if (!Plugin.MapFindButton.Value || !LargeMap() || !Party.InParty) { if (!LargeMap()) _start = -1f; return; }
            if (_font == null)
                foreach (Font f in Resources.FindObjectsOfTypeAll<Font>())
                    if (f.name == "AveriaSerifLibre-Bold") { _font = f; break; }

            List<KeyValuePair<Member, bool>> allies = Allies();
            if (allies.Count == 0) { _area = new Rect(); DrawMarker(); return; }

            Vector2 at = Plugin.MapFindPosition.Value;
            const float w = 160f, h = 30f, gap = 4f;
            float total = allies.Count * h + (allies.Count - 1) * gap;
            float x = at.x < 0f ? Screen.width + at.x - w : at.x;
            float y = at.y < 0f ? Screen.height + at.y - total : at.y;
            // keep clear of the game's own "shared map" toggle (it shows once you have pins from a map table)
            Rect hint;
            if (SharedHint(out hint) && hint.Overlaps(new Rect(x, y, w, total))) y = hint.yMax + 6f;
            _area = new Rect(x, y, w, total);

            GUISkin old = GUI.skin;
            GUI.skin = Skin.Get(_font);
            for (int i = 0; i < allies.Count; i++)
            {
                Member m = allies[i].Key;
                bool known = allies[i].Value;
                Rect r = new Rect(x, y + i * (h + gap), w, h);
                bool current = (Flying || Time.unscaledTime - _arrived < 3f) && m.Peer == _target;
                GUI.enabled = known;
                GUI.color = current ? new Color(1f, 0.92f, 0.6f) : Color.white;
                string text = known ? Short(m.Name) : Short(m.Name) + " (?)";
                if (GUI.Button(r, text)) Go(m);
            }
            GUI.color = Color.white;
            GUI.enabled = true;
            GUI.skin = old;

            DrawMarker();
        }

        private static bool _hintLogged;

        // the toggle's rectangle on screen, in GUI coordinates (y from the top)
        private static bool SharedHint(out Rect r)
        {
            r = new Rect();
            Minimap map = Minimap.instance;
            if (map == null || map.m_sharedMapHint == null || !map.m_sharedMapHint.activeInHierarchy) return false;
            RectTransform t = map.m_sharedMapHint.transform as RectTransform;
            if (t == null) return false;
            Vector3[] c = new Vector3[4];
            t.GetWorldCorners(c);
            r = new Rect(c[0].x, Screen.height - c[1].y, c[2].x - c[0].x, c[1].y - c[0].y);
            if (!_hintLogged) { _hintLogged = true; Plugin.Debug("[map] shared map toggle at " + r); }
            return true;
        }

        private static string Short(string name)
        {
            return name.Length > 12 ? name.Substring(0, 11) + "…" : name;
        }

        private static void Go(Member m)
        {
            Vector3 pos;
            if (!Ui.AllyPosition(m, out pos) || Offset == null) return;

            Minimap map = Minimap.instance;
            Vector3 me = Player.m_localPlayer.transform.position;
            _fromOffset = (Vector3)Offset.GetValue(map);
            _fromZoom = map.LargeZoom;
            float dist = Vector3.Distance(me + _fromOffset, pos);
            // far enough out to see where you are looking and the ally at once
            _outZoom = Mathf.Clamp(Mathf.Max(_fromZoom, dist * 2.2f / WorldWidth), map.m_minZoom, map.m_maxZoom);
            _inZoom = Mathf.Clamp(Plugin.MapFindZoom.Value, map.m_minZoom, map.m_maxZoom);
            _target = m.Peer;
            _targetName = m.Name;
            _start = Time.unscaledTime;
            Plugin.Debug("[map] finding " + m.Name + " at " + pos.ToString("0") + " (" + dist.ToString("0") + " m from the view), zoom " + _fromZoom.ToString("0.000") + " > " + _outZoom.ToString("0.000") + " > " + _inZoom.ToString("0.000"));
        }

        // ------------------------------------------------------------ flight, before the map draws itself

        [HarmonyPatch(typeof(Minimap), "UpdateMap")]
        [HarmonyPrefix]
        private static void Fly(Minimap __instance, Player player)
        {
            if (!Flying || player == null || Offset == null) return;
            if (__instance.m_mode != Minimap.MapMode.Large) { _start = -1f; return; }
            if (Input.GetMouseButtonDown(0) && !Over() || Input.GetMouseButtonDown(1) || Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f) { _start = -1f; return; }   // you took over

            Member m = Party.Get(_target);
            Vector3 pos;
            if (m == null || !Ui.AllyPosition(m, out pos)) { _start = -1f; return; }
            Vector3 toAlly = pos - player.transform.position;
            toAlly.y = 0f;

            float t = Time.unscaledTime - _start;
            float zoom;
            Vector3 offset;
            if (t < OutTime)
            {
                zoom = Mathf.Lerp(_fromZoom, _outZoom, Ease(t / OutTime));
                offset = _fromOffset;
            }
            else if (t < OutTime + MoveTime)
            {
                zoom = _outZoom;
                offset = Vector3.Lerp(_fromOffset, toAlly, Ease((t - OutTime) / MoveTime));
            }
            else
            {
                float k = Mathf.Clamp01((t - OutTime - MoveTime) / InTime);
                zoom = Mathf.Lerp(_outZoom, _inZoom, Ease(k));
                offset = toAlly;
                if (k >= 1f) { _start = -1f; _arrived = Time.unscaledTime; }
            }
            Offset.SetValue(__instance, offset);
            __instance.LargeZoom = zoom;
            if (Inertia != null) Inertia.SetValue(__instance, 0f);
            if (Drag != null) Drag.SetValue(__instance, false);
        }

        private static float Ease(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // ------------------------------------------------------------ ring and name in the middle

        private static void DrawMarker()
        {
            float since = Time.unscaledTime - _arrived;
            bool landing = Flying && Time.unscaledTime - _start > OutTime + MoveTime * 0.6f;
            if (!landing && since > 3f) return;
            Minimap map = Minimap.instance;
            if (map.m_mapImageLarge == null) return;
            Vector3[] c = new Vector3[4];
            map.m_mapImageLarge.rectTransform.GetWorldCorners(c);
            Vector2 mid = new Vector2((c[0].x + c[2].x) / 2f, Screen.height - (c[0].y + c[2].y) / 2f);

            float a = landing ? 1f : Mathf.Clamp01(3f - since);
            float pulse = 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
            float r = 26f * pulse;
            Color col = Plugin.SilhouetteColor.Value;
            GUI.color = new Color(col.r, col.g, col.b, a);
            GUI.DrawTexture(new Rect(mid.x - r, mid.y - r, r * 2f, r * 2f), Ring());
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label);
                _label.fontSize = 16;
                _label.fontStyle = FontStyle.Bold;
                _label.alignment = TextAnchor.MiddleCenter;
                if (_font != null) _label.font = _font;
            }
            Rect lr = new Rect(mid.x - 120f, mid.y + r + 2f, 240f, 24f);
            GUI.color = new Color(0f, 0f, 0f, a * 0.8f);
            GUI.Label(new Rect(lr.x + 1f, lr.y + 1f, lr.width, lr.height), _targetName, _label);
            GUI.color = new Color(1f, 0.92f, 0.7f, a);
            GUI.Label(lr, _targetName, _label);
            GUI.color = Color.white;
        }

        private static Texture2D Ring()
        {
            if (_ring != null) return _ring;
            const int n = 64;
            _ring = new Texture2D(n, n, TextureFormat.RGBA32, false);
            _ring.hideFlags = HideFlags.HideAndDontSave;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    float v = Mathf.Clamp01(1f - Mathf.Abs(d - 27f) / 3f) + Mathf.Clamp01(1f - Mathf.Abs(d - 16f) / 2f) * 0.7f;
                    _ring.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(v)));
                }
            _ring.Apply();
            return _ring;
        }

        // ------------------------------------------------------------ clicks on the button don't reach the map

        private static bool Over()
        {
            if (!Plugin.MapFindButton.Value || !LargeMap() || !Party.InParty) return false;
            Vector3 mp = Input.mousePosition;
            return _area.Contains(new Vector2(mp.x, Screen.height - mp.y));
        }

        [HarmonyPatch(typeof(Minimap), "OnMapLeftDown")]
        [HarmonyPrefix]
        private static bool LeftDown() { return !Over(); }

        [HarmonyPatch(typeof(Minimap), "OnMapLeftUp")]
        [HarmonyPrefix]
        private static bool LeftUp() { return !Over(); }

        [HarmonyPatch(typeof(Minimap), "OnMapLeftClick")]
        [HarmonyPrefix]
        private static bool LeftClick() { return !Over(); }

        [HarmonyPatch(typeof(Minimap), "OnMapDblClick")]
        [HarmonyPrefix]
        private static bool DblClick() { return !Over(); }
    }
}
