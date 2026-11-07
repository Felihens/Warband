using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Warband
{
    // Puts a coloured circle with the initial of each ally on the Nav Compass strip (if that mod is installed).
    // It adds its own markers to the strip; Nav Compass's pins and saved data are not touched.
    internal static class Compass
    {
        private class Marker
        {
            public GameObject Go;
            public RectTransform Root;
            public Image Icon;
            public TextMeshProUGUI Text;
            public float Size;
        }

        private static bool _looked;
        private static FieldInfo _instanceField, _stripField;
        private static MethodInfo _getFont;
        private static readonly Dictionary<long, Marker> Markers = new Dictionary<long, Marker>();

        private static void Look()
        {
            if (_looked) return;
            _looked = true;
            Type t = AccessTools.TypeByName("NavCompass.NavCompassPlugin");
            if (t == null) { Plugin.Debug("[compass] Nav Compass not installed"); return; }
            _instanceField = AccessTools.Field(t, "_instance");
            _stripField = AccessTools.Field(t, "_stripRect");
            _getFont = AccessTools.Method(t, "GetFont");
            Plugin.Debug("[compass] Nav Compass found (" + (_instanceField != null) + "/" + (_stripField != null) + "/" + (_getFont != null) + ")");
        }

        private static RectTransform Strip(out object compass)
        {
            compass = null;
            Look();
            if (_instanceField == null || _stripField == null) return null;
            compass = _instanceField.GetValue(null);
            if (compass == null) return null;
            return _stripField.GetValue(compass) as RectTransform;
        }

        internal static void LateUpdate()
        {
            object compass;
            RectTransform strip = Plugin.CompassEnabled.Value && Party.InParty ? Strip(out compass) : null;
            compass = null;
            if (strip == null || Player.m_localPlayer == null || GameCamera.instance == null) { HideAll(); return; }

            Vector3 fwd = GameCamera.instance.transform.forward;
            float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            Vector3 me = Player.m_localPlayer.transform.position;
            float max = Plugin.CompassMaxDistance.Value;
            long self = Party.Me;
            List<long> seen = new List<long>();

            foreach (Member m in Party.Members)
            {
                if (m.Peer == self) continue;
                Vector3 pos;
                if (!Ui.AllyPosition(m, out pos)) continue;
                float dist = Vector3.Distance(me, pos);
                if (max > 0f && dist > max) continue;
                Marker mk;
                if (!Markers.TryGetValue(m.Peer, out mk) || mk.Go == null || mk.Root.parent != strip)
                {
                    if (mk != null && mk.Go != null) UnityEngine.Object.Destroy(mk.Go);
                    mk = Create(strip, m.Name);
                    Markers[m.Peer] = mk;
                }
                seen.Add(m.Peer);

                float bearing = Mathf.Atan2(pos.x - me.x, pos.z - me.z) * Mathf.Rad2Deg;
                float delta = Mathf.DeltaAngle(yaw, bearing);
                bool visible = Mathf.Abs(delta) <= 90f;
                if (mk.Go.activeSelf != visible) mk.Go.SetActive(visible);
                if (!visible) continue;
                mk.Root.anchoredPosition = new Vector2(delta / 90f * 240f, 0f);
                mk.Icon.color = Looks.ColorOf(m);
                string letter = Looks.Initial(m.Name);
                if (mk.Text.text != letter) mk.Text.text = letter;
                float size = Plugin.CompassIconSize.Value;
                if (mk.Size != size) Resize(mk, size);
            }

            List<long> stale = null;
            foreach (KeyValuePair<long, Marker> kv in Markers)
                if (!seen.Contains(kv.Key))
                {
                    if (stale == null) stale = new List<long>();
                    stale.Add(kv.Key);
                }
            if (stale != null)
                foreach (long peer in stale)
                {
                    Marker mk = Markers[peer];
                    if (mk.Go != null) UnityEngine.Object.Destroy(mk.Go);
                    Markers.Remove(peer);
                }
        }

        private static readonly MethodInfo GetSprite = AccessTools.Method(typeof(Minimap), "GetSprite");
        private static Sprite PlayerSprite()
        {
            if (Minimap.instance == null || GetSprite == null) return null;
            try { return GetSprite.Invoke(Minimap.instance, new object[] { Minimap.PinType.Player }) as Sprite; }
            catch (Exception) { return null; }
        }

        private static void HideAll()
        {
            if (Markers.Count == 0) return;
            foreach (Marker mk in Markers.Values)
                if (mk.Go != null) UnityEngine.Object.Destroy(mk.Go);
            Markers.Clear();
        }

        // A circle in the ally's colour with their initial, centred on the compass strip.
        private static Marker Create(RectTransform strip, string name)
        {
            Marker mk = new Marker();
            mk.Go = new GameObject("WarbandAlly_" + name, typeof(RectTransform));
            mk.Go.transform.SetParent(strip, false);
            mk.Root = mk.Go.GetComponent<RectTransform>();
            mk.Root.anchorMin = mk.Root.anchorMax = mk.Root.pivot = new Vector2(0.5f, 0.5f);

            GameObject icon = new GameObject("Icon", typeof(RectTransform));
            icon.transform.SetParent(mk.Go.transform, false);
            mk.Icon = icon.AddComponent<Image>();
            mk.Icon.sprite = Looks.CircleSprite;
            mk.Icon.preserveAspect = true;
            mk.Icon.raycastTarget = false;
            RectTransform ir = icon.GetComponent<RectTransform>();
            ir.anchorMin = Vector2.zero;
            ir.anchorMax = Vector2.one;
            ir.offsetMin = ir.offsetMax = Vector2.zero;

            GameObject text = new GameObject("Initial", typeof(RectTransform));
            text.transform.SetParent(mk.Go.transform, false);
            mk.Text = text.AddComponent<TextMeshProUGUI>();
            object compass;
            Strip(out compass);
            TMP_FontAsset font = null;
            try { if (_getFont != null && compass != null) font = _getFont.Invoke(compass, null) as TMP_FontAsset; }
            catch (Exception e) { Plugin.Debug("[compass] font: " + e.Message); }
            if (font != null) mk.Text.font = font;
            mk.Text.fontStyle = FontStyles.Bold;
            mk.Text.alignment = TextAlignmentOptions.Center;
            mk.Text.textWrappingMode = TextWrappingModes.NoWrap;
            mk.Text.raycastTarget = false;
            mk.Text.color = new Color(0.1f, 0.07f, 0.04f, 1f);
            RectTransform tr = text.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            Resize(mk, Plugin.CompassIconSize.Value);
            Plugin.Debug("[compass] marker made for " + name);
            return mk;
        }

        private static void Resize(Marker mk, float size)
        {
            mk.Size = size;
            mk.Root.sizeDelta = new Vector2(size, size);
            mk.Text.fontSize = size * 0.62f;
        }
    }
}
