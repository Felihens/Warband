using UnityEngine;
using UnityEngine.UI;

namespace Warband
{
    // The party panel's look: the inventory window's background and frame, the game's button,
    // its font and colours, instead of Unity's grey default.
    internal static class Skin
    {
        internal static readonly Color TextColor = new Color(0.95f, 0.9f, 0.8f);
        internal static readonly Color Orange = new Color(1f, 0.63f, 0.24f);

        private static GUISkin _skin;
        private static bool _fromGame;
        private static GUIStyle _title;

        internal static GUISkin Get(Font font)
        {
            if (_skin != null && (_fromGame || InventoryGui.instance == null)) return _skin;
            Texture2D button = null;
            Vector4 border = new Vector4(8, 8, 8, 8);
            Button src = InventoryGui.instance != null ? InventoryGui.instance.m_takeAllButton : null;
            Image img = src != null ? src.GetComponent<Image>() : null;
            if (img != null && img.sprite != null)
            {
                button = ToTexture(img.sprite);
                border = img.sprite.border;
                _fromGame = button != null;
                Plugin.Debug("[panel] button look from " + img.sprite.name + " (" + (button != null) + ")");
            }

            GUISkin s = Object.Instantiate(GUI.skin);
            s.hideFlags = HideFlags.HideAndDontSave;
            if (font != null) s.font = font;

            Texture2D dark = Solid(new Color(0f, 0f, 0f, 0.35f));
            Texture2D darker = Solid(new Color(0f, 0f, 0f, 0.6f));

            s.window = new GUIStyle();
            s.window.padding = new RectOffset(18, 18, 44, 16);
            s.window.font = font;

            s.label.normal.textColor = TextColor;
            s.label.fontSize = 14;
            if (font != null) s.label.font = font;

            s.box = new GUIStyle();
            s.box.normal.background = dark;
            s.box.padding = new RectOffset(8, 8, 4, 4);
            s.box.margin = new RectOffset(0, 0, 2, 2);

            GUIStyle b = new GUIStyle(GUI.skin.button);
            if (button != null)
            {
                b.normal.background = b.hover.background = b.active.background = button;
                b.onNormal.background = b.onHover.background = b.onActive.background = button;
                b.focused.background = b.onFocused.background = button;
                b.border = new RectOffset((int)border.x, (int)border.z, (int)border.w, (int)border.y);
            }
            b.normal.textColor = TextColor;
            b.hover.textColor = Color.white;
            b.active.textColor = Orange;
            b.onNormal.textColor = b.onHover.textColor = b.onActive.textColor = Orange;
            b.fontSize = 14;
            b.padding = new RectOffset(8, 8, 5, 5);
            if (font != null) b.font = font;
            s.button = b;

            s.textField = new GUIStyle(GUI.skin.textField);
            s.textField.normal.background = s.textField.hover.background = s.textField.focused.background = darker;
            s.textField.onNormal.background = s.textField.onFocused.background = darker;
            s.textField.normal.textColor = s.textField.focused.textColor = s.textField.hover.textColor = Color.white;
            s.textField.border = new RectOffset(0, 0, 0, 0);
            s.textField.padding = new RectOffset(6, 6, 4, 4);
            s.textField.fontSize = 14;
            if (font != null) s.textField.font = font;

            s.verticalScrollbar = new GUIStyle(GUI.skin.verticalScrollbar);
            s.verticalScrollbar.normal.background = dark;
            s.verticalScrollbar.border = new RectOffset(0, 0, 0, 0);
            s.verticalScrollbar.fixedWidth = 10;
            s.verticalScrollbarThumb = new GUIStyle(GUI.skin.verticalScrollbarThumb);
            s.verticalScrollbarThumb.normal.background = button != null ? button : Solid(new Color(0.6f, 0.45f, 0.3f));
            s.verticalScrollbarThumb.border = new RectOffset(4, 4, 4, 4);
            s.verticalScrollbarThumb.fixedWidth = 10;
            s.scrollView = new GUIStyle();

            _title = new GUIStyle(s.label);
            _title.fontSize = 22;
            _title.fontStyle = FontStyle.Bold;
            _title.alignment = TextAnchor.MiddleCenter;
            _title.normal.textColor = Orange;

            _skin = s;
            return _skin;
        }

        internal static GUIStyle Title { get { return _title; } }

        private static Texture2D Solid(Color c)
        {
            Texture2D t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        // A standalone copy of a sprite (game sprites live in shared atlases that can't be read).
        private static Texture2D ToTexture(Sprite sp)
        {
            try
            {
                Texture2D tex = sp.texture;
                Rect r = sp.textureRect;
                int w = Mathf.Max(1, (int)r.width), h = Mathf.Max(1, (int)r.height);
                RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(tex, rt, new Vector2(r.width / tex.width, r.height / tex.height), new Vector2(r.x / tex.width, r.y / tex.height));
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                t.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                t.hideFlags = HideFlags.HideAndDontSave;
                return t;
            }
            catch (System.Exception e)
            {
                Plugin.Debug("[panel] could not copy " + sp.name + ": " + e.Message);
                return null;
            }
        }
    }
}
