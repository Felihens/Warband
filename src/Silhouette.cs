using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Warband
{
    // Shows the parts of an ally that something is in front of (a wall, a tree, a hill) as a flat
    // coloured silhouette. Parts you can see directly stay as they are.
    //
    // Three passes over the ally's meshes, using spare stencil bits:
    //   1. mark the pixels where the ally is the nearest thing (seen directly);
    //   2. colour the ally's pixels that are behind something, skipping the marked ones
    //      (so an arm behind the ally's own body doesn't light up) and colouring each pixel once;
    //   3. clear the bits again.
    internal static class Silhouette
    {
        private class Cached
        {
            public Renderer[] Renderers;
            public int[] SubMeshes;
            public float At;
        }

        private const int SeenBit = 128, DrawnBit = 64, Bits = SeenBit | DrawnBit;
        private const string ZTestName = "unity_GUIZTestMode";

        private static Camera _cam;
        private static CommandBuffer _cb;
        private static Material _mark, _paint, _clear;   // stencil method (UI/Default shader)
        private static Material _simple;                 // fallback without stencil
        private static bool _ready, _failed;
        private static readonly Dictionary<Player, Cached> Cache = new Dictionary<Player, Cached>();
        private static readonly List<Player> Allies = new List<Player>();
        private static float _nextPrune;

        internal static void LateUpdate()
        {
            Camera cam = GameCamera.instance != null ? Utils.GetMainCamera() : null;
            if (cam != _cam)
            {
                if (_cam != null && _cb != null) _cam.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, _cb);
                _cam = cam;
                if (_cam != null)
                {
                    if (_cb == null) { _cb = new CommandBuffer(); _cb.name = "Warband silhouettes"; }
                    _cam.AddCommandBuffer(CameraEvent.AfterForwardAlpha, _cb);
                    Plugin.Debug("[silhouette] attached to camera " + _cam.name + " (" + _cam.actualRenderingPath + ")");
                }
            }
            if (_cb == null) return;
            _cb.Clear();
            if (_cam == null || !Plugin.SilhouetteEnabled.Value || !Party.InParty) return;
            Player me = Player.m_localPlayer;
            if (me == null || !Materials()) return;

            Allies.Clear();
            float max = Plugin.SilhouetteMaxDistance.Value;
            foreach (Player p in Player.GetAllPlayers())
            {
                if (p == null || p == me || !Party.IsAlly(p.GetZDOID().UserID)) continue;
                if (Vector3.Distance(me.transform.position, p.transform.position) > max) continue;
                Allies.Add(p);
            }
            if (Allies.Count == 0) return;

            Color c = Plugin.SilhouetteColor.Value;
            c.a = Plugin.SilhouetteOpacity.Value;

            if (_paint != null)
            {
                _paint.SetColor("_Color", c);
                float oldZ = Shader.GetGlobalFloat(ZTestName);
                foreach (Player p in Allies)
                {
                    Cached k = Renderers(p);
                    _cb.SetGlobalFloat(ZTestName, (float)CompareFunction.LessEqual);
                    Draw(k, _mark);
                    _cb.SetGlobalFloat(ZTestName, (float)CompareFunction.Greater);
                    Draw(k, _paint);
                    _cb.SetGlobalFloat(ZTestName, (float)CompareFunction.Always);
                    Draw(k, _clear);
                }
                _cb.SetGlobalFloat(ZTestName, oldZ);
            }
            else
            {
                _simple.SetColor("_Color", c);
                foreach (Player p in Allies) Draw(Renderers(p), _simple);
            }

            float now = Time.time;
            if (now > _nextPrune)
            {
                _nextPrune = now + 5f;
                List<Player> gone = new List<Player>();
                foreach (Player p in Cache.Keys) if (p == null) gone.Add(p);
                foreach (Player p in gone) Cache.Remove(p);
            }
        }

        private static void Draw(Cached k, Material mat)
        {
            for (int i = 0; i < k.Renderers.Length; i++)
            {
                Renderer r = k.Renderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                for (int s = 0; s < k.SubMeshes[i]; s++) _cb.DrawRenderer(r, mat, s);
            }
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

        private static Material Stencil(Shader sh, int reference, int readMask, int writeMask, CompareFunction comp, StencilOp op, int colorMask, CompareFunction ztest)
        {
            Material m = new Material(sh);
            m.hideFlags = HideFlags.HideAndDontSave;
            m.SetFloat("_Stencil", reference);
            m.SetFloat("_StencilReadMask", readMask);
            m.SetFloat("_StencilWriteMask", writeMask);
            m.SetFloat("_StencilComp", (float)comp);
            m.SetFloat("_StencilOp", (float)op);
            m.SetFloat("_ColorMask", colorMask);
            m.SetFloat(ZTestName, (float)ztest);
            return m;
        }

        private static bool Materials()
        {
            if (_ready) return true;
            if (_failed) return false;
            Shader ui = Shader.Find("UI/Default");
            if (ui != null && ui.isSupported)
            {
                // 1. seen directly: set SeenBit
                _mark = Stencil(ui, SeenBit, Bits, SeenBit, CompareFunction.Always, StencilOp.Replace, 0, CompareFunction.LessEqual);
                // 2. hidden, not seen, not yet coloured: colour it and set DrawnBit
                //    (passes when DrawnBit > stencil & Bits, i.e. both bits clear)
                _paint = Stencil(ui, DrawnBit, Bits, DrawnBit, CompareFunction.Greater, StencilOp.Replace, 15, CompareFunction.Greater);
                // 3. clear both bits
                _clear = Stencil(ui, 0, Bits, Bits, CompareFunction.Always, StencilOp.Replace, 0, CompareFunction.Always);
                _ready = true;
                Plugin.Debug("[silhouette] using the stencil method (UI/Default)");
                return true;
            }
            Shader sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null)
            {
                _failed = true;
                Plugin.Log.LogWarning("Silhouettes are off: no usable shader was found");
                return false;
            }
            _simple = new Material(sh);
            _simple.hideFlags = HideFlags.HideAndDontSave;
            _simple.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _simple.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _simple.SetInt("_Cull", (int)CullMode.Back);
            _simple.SetInt("_ZWrite", 0);
            _simple.SetInt("_ZTest", (int)CompareFunction.Greater);
            _ready = true;
            Plugin.Debug("[silhouette] UI/Default missing, using the simple method (Internal-Colored)");
            return true;
        }
    }
}
