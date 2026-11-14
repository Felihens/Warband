using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Warband
{
    // The party HUD is drawn with Unity's old GUI, which always paints on top of every game window. So that it
    // sits *under* other windows (the inventory, a chest, Storage Master and other mods' windows), we look a few
    // times a second at what game UI lies where the HUD is; while a window covers it, the HUD isn't drawn.
    // Only while the cursor is free (a window is open): in normal play the HUD stays put over the game's HUD.
    internal static class Covered
    {
        internal static Rect Rect;   // where the HUD was last drawn, in GUI coordinates (y down)
        private static bool _covered;
        private static float _next;
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        private static PointerEventData _ped;
        private static EventSystem _es;

        internal static bool IsCovered()
        {
            if (Time.unscaledTime < _next) return _covered;
            _next = Time.unscaledTime + 0.2f;
            _covered = Check();
            return _covered;
        }

        private static bool Check()
        {
            if (!Cursor.visible || Rect.width <= 0f || EventSystem.current == null) return false;
            Transform hud = Hud.instance != null ? Hud.instance.transform : null;
            if (_ped == null || _es != EventSystem.current) { _es = EventSystem.current; _ped = new PointerEventData(_es); }
            float[] xs = { 0.1f, 0.5f, 0.9f }, ys = { 0.1f, 0.5f, 0.9f };
            foreach (float fx in xs)
                foreach (float fy in ys)
                {
                    Vector2 gui = new Vector2(Rect.x + Rect.width * fx, Rect.y + Rect.height * fy);
                    _ped.position = new Vector2(gui.x, Screen.height - gui.y);
                    Hits.Clear();
                    EventSystem.current.RaycastAll(_ped, Hits);
                    foreach (RaycastResult r in Hits)
                    {
                        if (r.gameObject == null) continue;
                        if (hud != null && r.gameObject.transform.IsChildOf(hud)) continue;   // the game's own HUD bars
                        return true;
                    }
                }
            return false;
        }
    }
}
