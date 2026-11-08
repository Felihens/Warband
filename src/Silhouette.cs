using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Warband
{
    // Shows the parts of an ally that something is in front of (a wall, a tree, a hill).
    // Parts you can see directly stay as they are.
    //
    // Fill and Outline work with a few stencil bits (a per-pixel scratch pad), never touching the depth:
    //   Ally  = pixels the ally covers (drawn pushed to the front so every pixel counts),
    //   Seen  = pixels where the ally's own surface is the closest thing (drawn at its real depth,
    //           a few cm closer, with the normal "closer or equal" test).
    //   Fill paints Ally-but-not-Seen: what is behind the ally's own body is never painted,
    //   because at those pixels you see the ally (its back, its torso), so they are Seen.
    //   Outline draws the ally shifted a few pixels in 8 directions the same way (Near / NearSeen)
    //   and paints the pixels outside the ally where the shifted copy is entirely hidden,
    //   which leaves a ring on the wall around the hidden part.
    // AlwaysOnTop paints all of Ally.
    // The buffer is filled right before the camera renders, with the camera exactly where it will be,
    // so zooming or moving the camera can't make it flicker.
    internal static class Silhouette
    {
        private class Cached
        {
            public Renderer[] Renderers;
            public int[] SubMeshes;
            public float At;
        }

        // Low stencil bits; the deferred renderer uses the high ones. Cleared before and after.
        private const int AllyBit = 1, SeenBit = 2, NearBit = 4, NearSeenBit = 8, AllBits = 15;

        private static Camera _cam;
        private static CameraEvent _event;
        private static CommandBuffer _cb;
        private static Material _clear, _markAlly, _markSeen, _markNear, _markNearSeen, _paintFill, _paintRing, _paintAll;
        private static Mesh _quad;
        private static bool _made, _hooked;
        private static readonly Dictionary<Player, Cached> Cache = new Dictionary<Player, Cached>();
        private static readonly List<Player> Targets = new List<Player>();
        private static float _nextPrune, _nextLog;
        private static string _lastUsed = "";

        internal static void LateUpdate()
        {
            Camera cam = GameCamera.instance != null ? Utils.GetMainCamera() : null;
            CameraEvent ev = CameraEvent.AfterHaloAndLensFlares;
            if (cam != _cam || ev != _event)
            {
                if (_cam != null && _cb != null) _cam.RemoveCommandBuffer(_event, _cb);
                _cam = cam;
                _event = ev;
                if (_cam != null)
                {
                    if (_cb == null) { _cb = new CommandBuffer(); _cb.name = "Warband silhouettes"; }
                    _cam.AddCommandBuffer(_event, _cb);
                    Plugin.Debug("[silhouette] attached to camera " + _cam.name + " after " + _event + " (" + _cam.actualRenderingPath +
                        ", reversed depth " + SystemInfo.usesReversedZBuffer + ", hdr " + _cam.allowHDR + ")");
                }
            }
            if (!_hooked) { Camera.onPreRender += PreRender; _hooked = true; }
        }

        private static void PreRender(Camera cam)
        {
            if (cam != _cam || _cb == null) return;
            _cb.Clear();
            bool self = Plugin.SilhouetteOnSelf.Value;
            if (_cam == null || !Plugin.SilhouetteEnabled.Value || (!Party.InParty && !self)) return;
            Player me = Player.m_localPlayer;
            if (me == null) return;
            Make();

            Targets.Clear();
            float max = Plugin.SilhouetteMaxDistance.Value;
            int loaded = 0;
            foreach (Player p in Player.GetAllPlayers())
            {
                if (p == null) continue;
                if (p == me) { if (self) Targets.Add(p); continue; }
                loaded++;
                if (!Party.IsAlly(p.GetZDOID().UserID)) continue;
                if (Vector3.Distance(me.transform.position, p.transform.position) > max) continue;
                Targets.Add(p);
            }

            Color c = Plugin.SilhouetteColor.Value;
            c.a = Plugin.SilhouetteOpacity.Value;
            SilhouetteStyle style = Plugin.SilhouetteMode.Value;
            if (_quad == null) return;

            int drawn = 0;
            if (Targets.Count > 0)
            {
                drawn = Stencil(c, style);
            }

            float now = Time.time;
            string used = style.ToString();
            if (now > _nextLog || used != _lastUsed)
            {
                _nextLog = now + 10f;
                _lastUsed = used;
                Plugin.Debug("[silhouette] " + used + " after " + _event + ", other players loaded " + loaded + ", drawing " + Targets.Count +
                    " (" + drawn + " meshes)" + (self ? ", on yourself too" : ""));
            }
            if (now > _nextPrune)
            {
                _nextPrune = now + 5f;
                List<Player> gone = new List<Player>();
                foreach (Player p in Cache.Keys) if (p == null) gone.Add(p);
                foreach (Player p in gone) Cache.Remove(p);
            }
        }

        private static int Stencil(Color c, SilhouetteStyle style)
        {
            Matrix4x4 view = _cam.worldToCameraMatrix, proj = _cam.projectionMatrix;
            Matrix4x4 front = AtNear(proj);
            float px = Plugin.SilhouetteOutlineWidth.Value;
            float w = Mathf.Max(1, _cam.pixelWidth), h = Mathf.Max(1, _cam.pixelHeight);
            float margin = Plugin.SilhouetteMargin.Value / 100f;
            Vector3 camPos = _cam.transform.position, fwd = _cam.transform.forward;

            SetMatrices(Matrix4x4.identity, Matrix4x4.identity);
            _cb.DrawMesh(_quad, Matrix4x4.identity, _clear);

            int drawn = 0;
            foreach (Player p in Targets)
            {
                Cached k = Renderers(p);
                float d = Mathf.Max(Vector3.Dot(p.transform.position + Vector3.up - camPos, fwd), margin + 0.3f);
                Matrix4x4 seen = Closer(proj, margin, d);
                SetMatrices(view, front);
                drawn += Draw(k, _markAlly);
                if (style == SilhouetteStyle.AlwaysOnTop) continue;
                SetMatrices(view, seen);
                Draw(k, _markSeen);
                if (style != SilhouetteStyle.Outline) continue;
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    float ox = Mathf.Cos(a) * px * 2f / w, oy = Mathf.Sin(a) * px * 2f / h;
                    SetMatrices(view, Shift(front, ox, oy));
                    Draw(k, _markNear);
                    SetMatrices(view, Shift(seen, ox, oy));
                    Draw(k, _markNearSeen);
                }
            }

            Material paint = style == SilhouetteStyle.Outline ? _paintRing : style == SilhouetteStyle.AlwaysOnTop ? _paintAll : _paintFill;
            paint.SetColor("_Color", c);
            SetMatrices(Matrix4x4.identity, Matrix4x4.identity);
            _cb.DrawMesh(_quad, Matrix4x4.identity, paint);
            _cb.DrawMesh(_quad, Matrix4x4.identity, _clear);
            SetMatrices(view, proj);
            return drawn;
        }

        private static void SetMatrices(Matrix4x4 view, Matrix4x4 proj)
        {
            _cb.SetViewProjectionMatrices(view, proj);
        }

        // Everything lands just in front of the near plane, so the "closer or equal" test always passes.
        private static Matrix4x4 AtNear(Matrix4x4 p)
        {
            p.m20 = -0.999f * p.m30; p.m21 = -0.999f * p.m31; p.m22 = -0.999f * p.m32; p.m23 = -0.999f * p.m33;
            return p;
        }

        // Depth as if the object were `m` metres closer (for something about `d` metres away); x/y unchanged.
        private static Matrix4x4 Closer(Matrix4x4 p, float m, float d)
        {
            p.m23 += p.m23 * m / (d - m);
            return p;
        }

        // Moves the picture by (ox, oy) in screen units (-1..1).
        private static Matrix4x4 Shift(Matrix4x4 p, float ox, float oy)
        {
            p.m00 += ox * p.m30; p.m01 += ox * p.m31; p.m02 += ox * p.m32; p.m03 += ox * p.m33;
            p.m10 += oy * p.m30; p.m11 += oy * p.m31; p.m12 += oy * p.m32; p.m13 += oy * p.m33;
            return p;
        }

        private static int Draw(Cached k, Material mat)
        {
            int n = 0;
            for (int i = 0; i < k.Renderers.Length; i++)
            {
                Renderer r = k.Renderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                for (int s = 0; s < k.SubMeshes[i]; s++) { _cb.DrawRenderer(r, mat, s); n++; }
            }
            return n;
        }

        private static Cached Renderers(Player p)
        {
            Cached cached;
            if (Cache.TryGetValue(p, out cached) && Time.time - cached.At < 0.5f) return cached;
            List<Renderer> rs = new List<Renderer>();
            List<int> subs = new List<int>();
            foreach (Renderer r in p.GetComponentsInChildren<Renderer>(false))
            {
                Mesh mesh = null;
                SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
                if (smr != null) mesh = smr.sharedMesh;
                else if (r is MeshRenderer)
                {
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf != null) mesh = mf.sharedMesh;
                }
                if (mesh == null) continue;
                rs.Add(r);
                subs.Add(Mathf.Max(1, mesh.subMeshCount));
            }
            cached = new Cached();
            cached.Renderers = rs.ToArray();
            cached.SubMeshes = subs.ToArray();
            cached.At = Time.time;
            Cache[p] = cached;
            return cached;
        }

        private static Material StencilMat(Shader sh, int reference, int readMask, int writeMask, CompareFunction comp, StencilOp op, int colorMask)
        {
            Material m = new Material(sh);
            m.hideFlags = HideFlags.HideAndDontSave;
            m.SetFloat("_Stencil", reference);
            m.SetFloat("_StencilReadMask", readMask);
            m.SetFloat("_StencilWriteMask", writeMask);
            m.SetFloat("_StencilComp", (float)comp);
            m.SetFloat("_StencilOp", (float)op);
            m.SetFloat("_ColorMask", colorMask);
            return m;
        }

        private static void Make()
        {
            if (_made) return;
            _made = true;
            Shader ui = Shader.Find("UI/Default");
            if (ui != null && ui.isSupported)
            {
                _clear = StencilMat(ui, 0, 0, AllBits, CompareFunction.Always, StencilOp.Replace, 0);
                _markAlly = StencilMat(ui, AllyBit, 0, AllyBit, CompareFunction.Always, StencilOp.Replace, 0);
                _markSeen = StencilMat(ui, SeenBit, 0, SeenBit, CompareFunction.Always, StencilOp.Replace, 0);
                _markNear = StencilMat(ui, NearBit, 0, NearBit, CompareFunction.Always, StencilOp.Replace, 0);
                _markNearSeen = StencilMat(ui, NearSeenBit, 0, NearSeenBit, CompareFunction.Always, StencilOp.Replace, 0);
                // covered by the ally, not seen directly
                _paintFill = StencilMat(ui, AllyBit, AllyBit | SeenBit, 0, CompareFunction.Equal, StencilOp.Keep, 15);
                _paintAll = StencilMat(ui, AllyBit, AllyBit, 0, CompareFunction.Equal, StencilOp.Keep, 15);
                // covered by a shifted copy that is entirely hidden, and outside the ally
                _paintRing = StencilMat(ui, NearBit, NearBit | NearSeenBit | AllyBit, 0, CompareFunction.Equal, StencilOp.Keep, 15);
                // a full-screen quad in screen coordinates, just in front of the near plane
                _quad = new Mesh();
                _quad.name = "Warband screen quad";
                _quad.vertices = new Vector3[]
                {
                    new Vector3(-1f, -1f, -0.999f), new Vector3(1f, -1f, -0.999f), new Vector3(1f, 1f, -0.999f), new Vector3(-1f, 1f, -0.999f),
                };
                _quad.uv = new Vector2[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
                _quad.colors = new Color[] { Color.white, Color.white, Color.white, Color.white };
                _quad.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
                _quad.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            }
            Plugin.Debug("[silhouette] " + (_quad != null ? "ready" : "missing"));
            if (_quad == null) Plugin.Log.LogWarning("Silhouettes are off: no usable shader was found");
        }
    }
}
