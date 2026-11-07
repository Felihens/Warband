using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Warband
{
    // Each ally gets their own colour (the same one every session, picked from their name),
    // shown as a circle with their initial on the compass and in the HUD.
    internal static class Looks
    {
        private static readonly Color[] Palette =
        {
            new Color(1.00f, 0.80f, 0.20f),   // gold
            new Color(0.30f, 0.85f, 1.00f),   // sky
            new Color(0.95f, 0.35f, 0.85f),   // pink
            new Color(0.45f, 0.95f, 0.35f),   // green
            new Color(1.00f, 0.50f, 0.15f),   // orange
            new Color(0.65f, 0.50f, 1.00f),   // violet
            new Color(1.00f, 0.30f, 0.30f),   // red
            new Color(0.25f, 0.95f, 0.75f),   // teal
            new Color(0.95f, 0.95f, 0.95f),   // white
            new Color(0.70f, 0.55f, 0.35f),   // brown
        };

        private static uint Hash(string s)
        {
            uint h = 2166136261;
            foreach (char c in s.ToLowerInvariant()) { h ^= c; h *= 16777619; }
            return h;
        }

        // Colours for everyone in the party: their own pick, or the next free one if two collide.
        internal static Color ColorOf(Member who)
        {
            List<int> used = new List<int>();
            foreach (Member m in Party.Members)
            {
                if (m.Peer == Party.Me && m != who) continue;
                int i = (int)(Hash(m.Name) % (uint)Palette.Length);
                while (used.Contains(i) && used.Count < Palette.Length) i = (i + 1) % Palette.Length;
                used.Add(i);
                if (m == who) return Palette[i];
            }
            return Palette[(int)(Hash(who.Name) % (uint)Palette.Length)];
        }

        internal static string Initial(string name)
        {
            foreach (char c in name)
                if (char.IsLetterOrDigit(c)) return char.ToUpperInvariant(c).ToString();
            return "?";
        }

        // ---------------------------------------------------------------- circle

        private static Texture2D _circle;
        private static Sprite _circleSprite;

        // White disc with a dark rim, tinted per ally.
        internal static Texture2D Circle
        {
            get
            {
                if (_circle != null) return _circle;
                const int N = 64;
                _circle = new Texture2D(N, N, TextureFormat.RGBA32, false);
                _circle.wrapMode = TextureWrapMode.Clamp;
                float c = (N - 1) / 2f, outer = N / 2f - 1f, rim = outer - 5f;
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                        float a = Mathf.Clamp01(outer - d + 0.5f);
                        float shade = d > rim ? Mathf.Lerp(1f, 0.15f, Mathf.Clamp01(d - rim)) : 1f;
                        _circle.SetPixel(x, y, new Color(shade, shade, shade, a));
                    }
                _circle.Apply();
                return _circle;
            }
        }

        internal static Sprite CircleSprite
        {
            get
            {
                if (_circleSprite == null)
                    _circleSprite = Sprite.Create(Circle, new Rect(0, 0, Circle.width, Circle.height), new Vector2(0.5f, 0.5f), 100f);
                return _circleSprite;
            }
        }

        // ---------------------------------------------------------------- window background

        internal class Layer
        {
            public Sprite Sprite;
            public Color Color;
            public Vector2 OffsetMin, OffsetMax;   // inset from the box edges, as on the inventory window
        }

        private static List<Layer> _layers;
        private static float _nextTry;

        // The inventory window's background and frame, the same ones Resource Scanner copies.
        internal static List<Layer> Background()
        {
            if (_layers != null || Time.time < _nextTry) return _layers;
            _nextTry = Time.time + 2f;
            RectTransform win = InventoryGui.instance != null ? InventoryGui.instance.m_player : null;
            if (win == null) return null;
            List<Layer> list = new List<Layer>();
            Image own = win.GetComponent<Image>();
            if (own != null && own.sprite != null) list.Add(Make(own, Vector2.zero, Vector2.zero));
            for (int i = 0; i < win.childCount; i++)
            {
                RectTransform ch = win.GetChild(i) as RectTransform;
                if (ch == null) continue;
                Image im = ch.GetComponent<Image>();
                if (im == null || im.sprite == null) continue;
                bool covers = ch.anchorMin.x <= 0.01f && ch.anchorMin.y <= 0.01f && ch.anchorMax.x >= 0.99f && ch.anchorMax.y >= 0.99f;
                if (covers) list.Add(Make(im, ch.offsetMin, ch.offsetMax));
            }
            _layers = list;
            string info = "";
            foreach (Layer l in list) info += (info.Length > 0 ? " + " : "") + l.Sprite.name;
            Plugin.Debug("[hud] background from the inventory window: " + (info.Length > 0 ? info : "none, plain dark box"));
            return _layers;
        }

        private static Layer Make(Image im, Vector2 min, Vector2 max)
        {
            Layer l = new Layer();
            l.Sprite = im.sprite;
            l.Color = im.color;
            l.OffsetMin = min;
            l.OffsetMax = max;
            return l;
        }

        // Draws a sprite stretched over r, keeping its corners and edges (nine-slice).
        internal static void NineSlice(Rect r, Sprite s, Color tint, float borderScale)
        {
            Texture2D tex = s.texture;
            if (tex == null) return;
            Rect tr = s.textureRect;
            Vector4 b = s.border;   // left, bottom, right, top in pixels
            float tw = tex.width, th = tex.height;
            float L = b.x * borderScale, B = b.y * borderScale, R = b.z * borderScale, T = b.w * borderScale;
            float sx = Mathf.Min(1f, r.width / Mathf.Max(1f, L + R)), sy = Mathf.Min(1f, r.height / Mathf.Max(1f, T + B));
            L *= sx; R *= sx; T *= sy; B *= sy;

            float[] xs = { r.xMin, r.xMin + L, r.xMax - R, r.xMax };
            float[] ys = { r.yMin, r.yMin + T, r.yMax - B, r.yMax };        // screen y grows down
            float[] us = { tr.xMin, tr.xMin + b.x, tr.xMax - b.z, tr.xMax };
            float[] vs = { tr.yMax, tr.yMax - b.w, tr.yMin + b.y, tr.yMin }; // texture v grows up

            Color old = GUI.color;
            GUI.color = tint;
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    Rect dst = new Rect(xs[i], ys[j], xs[i + 1] - xs[i], ys[j + 1] - ys[j]);
                    if (dst.width <= 0f || dst.height <= 0f) continue;
                    float v0 = vs[j + 1], v1 = vs[j];
                    Rect uv = new Rect(us[i] / tw, v0 / th, (us[i + 1] - us[i]) / tw, (v1 - v0) / th);
                    GUI.DrawTextureWithTexCoords(dst, tex, uv);
                }
            GUI.color = old;
        }
    }

    // The order allies are listed in the HUD, kept by name in the config.
    internal static class Order
    {
        private static List<string> Names()
        {
            List<string> list = new List<string>();
            foreach (string s in Plugin.HudOrder.Value.Split('|'))
                if (s.Trim().Length > 0) list.Add(s.Trim());
            return list;
        }

        internal static void Sort(List<Member> members)
        {
            List<string> names = Names();
            if (names.Count == 0) return;
            List<Member> copy = new List<Member>(members);
            members.Sort(delegate (Member a, Member b)
            {
                int ia = Index(names, a.Name), ib = Index(names, b.Name);
                if (ia != ib) return ia.CompareTo(ib);
                return copy.IndexOf(a).CompareTo(copy.IndexOf(b));
            });
        }

        private static int Index(List<string> names, string name)
        {
            for (int i = 0; i < names.Count; i++)
                if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            return 10000;
        }

        // Puts the shown allies in this order, keeping remembered names of absent ones after them.
        internal static void Save(List<Member> shown)
        {
            List<string> names = new List<string>();
            foreach (Member m in shown) names.Add(m.Name);
            foreach (string s in Names())
            {
                bool there = false;
                foreach (string n in names) if (string.Equals(n, s, StringComparison.OrdinalIgnoreCase)) there = true;
                if (!there) names.Add(s);
            }
            Plugin.HudOrder.Value = string.Join("|", names.ToArray());
        }
    }
}
