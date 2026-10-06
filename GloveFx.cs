using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSLZ.Marrow.Interaction;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace QuickGloves
{
    // the pretty stuff for one hand
    public class GloveFx
    {
        private const int ArcPoints  = 24;
        private const int MaxOutline = 16;

        private readonly string _tag;

        private LineRenderer?  _arc;
        private GameObject?    _cross, _beam;
        private Material?      _crossMat, _beamMat;
        private static Texture2D? _crossTex;
        private TrailRenderer? _trail;
        private Material?      _outlineMat;

        private sealed class Piece
        {
            public GameObject   Go  = null!;
            public MeshFilter   Mf  = null!;
            public MeshRenderer Mr  = null!;
            public Transform?   Src;
        }
        private readonly List<Piece> _pieces = new List<Piece>();
        private int _used;
        private int _outlineId;

        public GloveFx(bool isLeft) { _tag = isLeft ? "L" : "R"; }

        public static readonly Color Orange = new Color(1f, 0.62f, 0.12f);

        public static Color Accent()
        {
            try { return QuickModsTheme.ResolveAccent(Orange, false, () => null); }
            catch { return Orange; }
        }

        // glowy material, no lighting drama
        public static Material Mat(int queue = 3000)
        {
            Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
            var m = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            try { m.SetInt("unity_GUIZTestMode", (int)CompareFunction.LessEqual); } catch { }
            m.renderQueue = queue;
            return m;
        }

        // the bendy aim line
        public void Arc(bool show, Vector3 a, Vector3 b, Color c, float alpha, bool taut)
        {
            if (!show || !GloveSettings.ShowArc)
            {
                if (_arc != null) _arc.enabled = false;
                return;
            }
            BuildArc();
            if (_arc == null) return;
            _arc.enabled = true;

            float dist = Vector3.Distance(a, b);
            Vector3 mid = (a + b) * 0.5f;
            if (taut) mid += UnityEngine.Random.insideUnitSphere * 0.012f;   // it's struggling, give it the shakes
            else      mid += Vector3.up * Mathf.Min(dist * 0.16f, 0.9f);

            for (int i = 0; i < ArcPoints; i++)
            {
                float t = i / (float)(ArcPoints - 1);
                float u = 1f - t;
                _arc.SetPosition(i, u * u * a + 2f * u * t * mid + t * t * b);
            }

            _arc.startColor = new Color(c.r, c.g, c.b, alpha);
            _arc.endColor   = new Color(c.r, c.g, c.b, alpha * 0.55f);
        }

        private void BuildArc()
        {
            if (_arc != null) return;
            var go = new GameObject("[QuickGloves] Arc " + _tag);
            Object.DontDestroyOnLoad(go);
            _arc = go.AddComponent<LineRenderer>();
            _arc.positionCount     = ArcPoints;
            _arc.useWorldSpace     = true;
            _arc.numCapVertices    = 3;
            _arc.startWidth        = 0.007f;
            _arc.endWidth          = 0.003f;
            _arc.shadowCastingMode = ShadowCastingMode.Off;
            _arc.receiveShadows    = false;
            _arc.material          = Mat();
            _arc.enabled           = false;
        }

        // make the prop glow like loot
        public void Outline(Rigidbody? rb)
        {
            int id = 0;
            try { if (rb != null) id = rb.GetInstanceID(); } catch { }
            if (id == _outlineId) return;
            _outlineId = id;

            HidePieces();
            if (rb == null || !GloveSettings.ShowOutline) return;

            try
            {
                var ent = rb.GetComponentInParent<MarrowEntity>();
                GameObject root = ent != null ? ent.gameObject : rb.gameObject;
                foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (_used >= MaxOutline) break;
                    if (mf == null) continue;
                    Mesh mesh = mf.sharedMesh;
                    if (mesh == null) continue;
                    var src = mf.GetComponent<MeshRenderer>();
                    if (src == null || !src.enabled) continue;

                    Piece p = GetPiece(_used++);
                    p.Src = mf.transform;
                    p.Mf.sharedMesh = mesh;
                    int subs = Mathf.Max(1, mesh.subMeshCount);
                    var mats = new Il2CppReferenceArray<Material>(subs);
                    for (int i = 0; i < subs; i++) mats[i] = _outlineMat!;
                    p.Mr.sharedMaterials = mats;
                    p.Go.SetActive(true);
                }
            }
            catch { HidePieces(); }
        }

        public void OutlineTick(Color c, float alpha)
        {
            if (_used == 0) return;
            if (!GloveSettings.ShowOutline) { HidePieces(); _outlineId = 0; return; }
            try
            {
                if (_outlineMat != null) _outlineMat.color = new Color(c.r, c.g, c.b, alpha);
                for (int i = 0; i < _used; i++)
                {
                    Piece p = _pieces[i];
                    if (p.Go == null) continue;
                    if (p.Src == null) { p.Go.SetActive(false); continue; }
                    p.Go.transform.SetPositionAndRotation(p.Src.position, p.Src.rotation);
                    p.Go.transform.localScale = p.Src.lossyScale * 1.035f;
                }
            }
            catch { }
        }

        private Piece GetPiece(int i)
        {
            if (_outlineMat == null) _outlineMat = Mat(3001);
            if (i < _pieces.Count && _pieces[i].Go != null) return _pieces[i];

            var go = new GameObject("[QuickGloves] Outline " + _tag);
            Object.DontDestroyOnLoad(go);
            var p = new Piece { Go = go, Mf = go.AddComponent<MeshFilter>(), Mr = go.AddComponent<MeshRenderer>() };
            p.Mr.shadowCastingMode = ShadowCastingMode.Off;
            p.Mr.receiveShadows    = false;
            if (i < _pieces.Count) _pieces[i] = p; else _pieces.Add(p);
            return p;
        }

        private void HidePieces()
        {
            for (int i = 0; i < _pieces.Count; i++)
            {
                try { if (_pieces[i].Go != null) _pieces[i].Go.SetActive(false); } catch { }
                _pieces[i].Src = null;
            }
            _used = 0;
        }

        // zoomy trail
        public void TrailStart(Vector3 pos, Color c)
        {
            if (!GloveSettings.ShowTrail) return;
            BuildTrail();
            if (_trail == null) return;
            _trail.transform.position = pos;
            _trail.Clear();
            _trail.startColor = new Color(c.r, c.g, c.b, 0.55f);
            _trail.endColor   = new Color(c.r, c.g, c.b, 0f);
            _trail.emitting   = true;
        }

        public void TrailMove(Vector3 pos)
        {
            if (_trail != null) _trail.transform.position = pos;
        }

        // stop the trail, let it fade out
        public void TrailStop()
        {
            if (_trail != null) _trail.emitting = false;
        }

        private void BuildTrail()
        {
            if (_trail != null) return;
            var go = new GameObject("[QuickGloves] Trail " + _tag);
            Object.DontDestroyOnLoad(go);
            _trail = go.AddComponent<TrailRenderer>();
            _trail.time              = 0.4f;
            _trail.startWidth        = 0.035f;
            _trail.endWidth          = 0f;
            _trail.minVertexDistance = 0.03f;
            _trail.numCapVertices    = 2;
            _trail.shadowCastingMode = ShadowCastingMode.Off;
            _trail.receiveShadows    = false;
            _trail.material          = Mat();
            _trail.emitting          = false;
        }

        // crosshair, for pointing at stuff
        public void Crosshair(bool show, Vector3 pos, Vector3 eye, Color c, bool locked)
        {
            if (!show || !GloveSettings.ShowCrosshair)
            {
                if (_cross != null) _cross.SetActive(false);
                return;
            }
            BuildCross();
            if (_cross == null) return;

            Vector3 toEye = eye - pos;
            float dist = toEye.magnitude;
            if (dist < 0.01f) { _cross.SetActive(false); return; }
            _cross.SetActive(true);

            // stays the same size, near or far
            Transform t = _cross.transform;
            t.position   = pos;
            t.rotation   = Quaternion.LookRotation(-toEye / dist, Vector3.up)
                         * Quaternion.Euler(0f, 0f, locked ? 45f : 0f);
            t.localScale = Vector3.one * (dist * (locked ? 0.045f : 0.035f));
            if (_crossMat != null) _crossMat.color = new Color(c.r, c.g, c.b, locked ? 1f : 0.6f);
        }

        private void BuildCross()
        {
            if (_cross != null) return;
            _cross = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _cross.name = "[QuickGloves] Crosshair " + _tag;
            Object.DontDestroyOnLoad(_cross);
            var col = _cross.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // draws over everything, no hiding
            Shader sh = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default");
            _crossMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            int always = (int)CompareFunction.Always;
            try { _crossMat.SetInt("unity_GUIZTestMode", always); _crossMat.SetInt("_ZTest", always); } catch { }
            _crossMat.renderQueue = 5000;
            _crossMat.mainTexture = CrossTex();

            var mr = _cross.GetComponent<MeshRenderer>();
            mr.sharedMaterial    = _crossMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows    = false;
            _cross.SetActive(false);
        }

        // a ring, four ticks and a dot
        private static Texture2D CrossTex()
        {
            if (_crossTex != null) return _crossTex;

            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags  = HideFlags.HideAndDontSave;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float x = (px + 0.5f) / size * 2f - 1f;
                    float y = (py + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(x * x + y * y);
                    float lo = Mathf.Min(Mathf.Abs(x), Mathf.Abs(y));
                    float hi = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));

                    bool dot  = r < 0.07f;
                    bool ring = r > 0.56f && r < 0.64f && lo > 0.16f;   // leave gaps for the ticks
                    bool tick = lo < 0.035f && hi > 0.48f && hi < 0.95f;
                    tex.SetPixel(px, py, new Color(1f, 1f, 1f, dot || ring || tick ? 1f : 0f));
                }
            }
            tex.Apply();
            _crossTex = tex;
            return tex;
        }

        // the see how wide it is tube
        public void Beam(bool show, Vector3 from, Vector3 dir, float length, float radius, Color c)
        {
            if (!show || !GloveSettings.ShowBeam || length < 0.05f)
            {
                if (_beam != null) _beam.SetActive(false);
                return;
            }
            BuildBeam();
            if (_beam == null) return;
            _beam.SetActive(true);

            // unity cylinders are weird: 2 tall, 1 wide
            Transform t = _beam.transform;
            t.position   = from + dir * (length * 0.5f);
            t.rotation   = Quaternion.FromToRotation(Vector3.up, dir);
            t.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            if (_beamMat != null) _beamMat.color = new Color(c.r, c.g, c.b, 0.12f);
        }

        private void BuildBeam()
        {
            if (_beam != null) return;
            _beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _beam.name = "[QuickGloves] Beam " + _tag;
            Object.DontDestroyOnLoad(_beam);
            var col = _beam.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            _beamMat = Mat();
            var mr = _beam.GetComponent<MeshRenderer>();
            mr.sharedMaterial    = _beamMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows    = false;
            _beam.SetActive(false);
        }

        public void Hide()
        {
            try
            {
                if (_arc != null) _arc.enabled = false;
                if (_cross != null) _cross.SetActive(false);
                if (_beam != null) _beam.SetActive(false);
                TrailStop();
                HidePieces();
                _outlineId = 0;
            }
            catch (Exception) { }
        }
    }
}

// imagine reading comments
