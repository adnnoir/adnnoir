using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace E7
{
    /// <summary>
    /// Charge ce qui a été exporté depuis la version web :
    /// modèles (.bytes), matériaux (.json) et textures (.png) dans Resources/E7.
    /// </summary>
    public static class E7Assets
    {
        static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Dictionary<string, object>> matSets = new Dictionary<string, Dictionary<string, object>>();
        static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
        static readonly Dictionary<string, ModelData> modelCache = new Dictionary<string, ModelData>();
        static readonly Dictionary<string, object> jsonCache = new Dictionary<string, object>();

        // ---------------- Shaders ----------------
        static Shader lit, litFade, unlit;
        public static Shader Lit => lit ? lit : (lit = Find("E7/Lit", "Standard"));
        public static Shader LitFade => litFade ? litFade : (litFade = Find("E7/LitFade", "Standard"));
        public static Shader Unlit => unlit ? unlit : (unlit = Find("E7/Unlit", "Unlit/Color"));

        static Shader Find(string name, string fallback)
        {
            var s = Shader.Find(name);
            if (!s) { Debug.LogWarning("[E7] Shader introuvable : " + name + " → " + fallback); s = Shader.Find(fallback); }
            return s;
        }

        // ---------------- Textures ----------------
        public static Texture2D Tex(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (texCache.TryGetValue(name, out var t)) return t;
            t = Resources.Load<Texture2D>("E7/Textures/" + name);
            if (t) { t.wrapMode = TextureWrapMode.Repeat; t.anisoLevel = 4; }
            texCache[name] = t;
            return t;
        }

        public static object Json(string path)
        {
            if (jsonCache.TryGetValue(path, out var o)) return o;
            var ta = Resources.Load<TextAsset>("E7/" + path);
            o = ta ? MiniJson.Parse(ta.text) : null;
            if (o == null) Debug.LogWarning("[E7] JSON manquant : " + path);
            jsonCache[path] = o;
            return o;
        }

        // ---------------- Matériaux ----------------
        /// <summary>set = "gun", "human", "world", "worldgun".</summary>
        public static Material Mat(string set, string key)
        {
            string ck = set + "/" + key;
            if (matCache.TryGetValue(ck, out var m)) return m;
            if (!matSets.TryGetValue(set, out var descs))
            {
                descs = Json("Models/" + set + "_materials") as Dictionary<string, object> ?? new Dictionary<string, object>();
                matSets[set] = descs;
            }
            descs.TryGetValue(key, out var d);
            m = d != null ? FromDesc(d as Dictionary<string, object>) : Simple(new Color(0.3f, 0.3f, 0.3f));
            m.name = ck;
            matCache[ck] = m;
            return m;
        }

        public static Material Simple(Color c, float metallic = 0f, float roughness = 0.7f)
        {
            var m = new Material(Lit);
            m.SetColor("_Color", c);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Roughness", roughness);
            m.SetFloat("_Glossiness", 1f - roughness);
            return m;
        }

        public static Material UnlitMat(Color c, Texture tex = null, bool additive = false, bool transparent = false, bool doubleSided = false)
        {
            var m = new Material(Unlit);
            m.SetColor("_Color", c);
            if (tex) m.SetTexture("_MainTex", tex);
            // additif : la transparence de la texture module l'éclat (comme en web : SrcAlpha, One)
            SetBlend(m, (additive || transparent) ? BlendMode.SrcAlpha : BlendMode.One, additive ? BlendMode.One : (transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero), !(additive || transparent));
            m.SetFloat("_Cull", doubleSided ? 0 : 2);
            if (additive || transparent) m.renderQueue = 3000;
            return m;
        }

        static void SetBlend(Material m, BlendMode src, BlendMode dst, bool zwrite)
        {
            m.SetFloat("_SrcBlend", (float)src);
            m.SetFloat("_DstBlend", (float)dst);
            m.SetFloat("_ZWrite", zwrite ? 1 : 0);
        }

        static void ApplyTex(Material m, string prop, object texDesc)
        {
            var d = texDesc as Dictionary<string, object>;
            if (d == null) return;
            var t = Tex(MiniJson.Text(d, "name"));
            if (!t) return;
            m.SetTexture(prop, t);
            var rep = MiniJson.Arr(d, "repeat");
            if (rep != null && rep.Count == 2) m.SetTextureScale(prop, new Vector2(MiniJson.F(rep[0]), MiniJson.F(rep[1])));
        }

        public static Material FromDesc(Dictionary<string, object> d)
        {
            string kind = MiniJson.Text(d, "kind", "std");
            bool transparent = MiniJson.Bool(d, "transparent");
            bool additive = MiniJson.Bool(d, "additive");
            bool dbl = MiniJson.Bool(d, "doubleSide");
            float opacity = MiniJson.Num(d, "opacity", 1f);
            var lin = MiniJson.Arr(d, "lin");
            Color hex = U.Hex(MiniJson.Text(d, "color", "#ffffff"));
            Material m;
            if (kind == "basic")
            {
                // couleurs « HDR » (point rouge, tritium…) : on garde l'intensité > 1
                Color c = hex;
                if (lin != null && lin.Count == 3)
                {
                    float r = MiniJson.F(lin[0]), g = MiniJson.F(lin[1]), b = MiniJson.F(lin[2]);
                    if (r > 1f || g > 1f || b > 1f) c = new Color(r, g, b, 1f);
                }
                c.a = opacity;
                var tex = Tex(MiniJson.Text(MiniJson.Obj(d, "map"), "name"));
                m = UnlitMat(c, tex, additive, transparent, dbl);
                return m;
            }
            m = new Material(transparent ? LitFade : Lit);
            Color col = hex; col.a = opacity;
            m.SetColor("_Color", col);
            m.SetFloat("_Metallic", MiniJson.Num(d, "metalness", 0f));
            float rough = MiniJson.Num(d, "roughness", 0.8f);
            // le vernis (clearcoat) rend l'arme un peu plus brillante
            rough = Mathf.Clamp01(rough - MiniJson.Num(d, "clearcoat", 0f) * 0.12f);
            m.SetFloat("_Roughness", rough);
            m.SetFloat("_Glossiness", 1f - rough);
            ApplyTex(m, "_MainTex", MiniJson.Obj(d, "map"));
            ApplyTex(m, "_BumpMap", MiniJson.Obj(d, "normalMap"));
            m.SetFloat("_BumpScale", MiniJson.Num(d, "normalScale", 1f));
            ApplyTex(m, "_RoughMap", MiniJson.Obj(d, "roughnessMap"));
            ApplyTex(m, "_MetalMap", MiniJson.Obj(d, "metalnessMap"));
            var em = MiniJson.Arr(d, "emissive");
            if (em != null && em.Count == 3)
            {
                float k = MiniJson.Num(d, "emissiveIntensity", 1f);
                m.SetColor("_EmissionColor", new Color(MiniJson.F(em[0]) * k, MiniJson.F(em[1]) * k, MiniJson.F(em[2]) * k, 1f));
                m.EnableKeyword("_EMISSION");
                ApplyTex(m, "_EmissionMap", MiniJson.Obj(d, "emissiveMap"));
            }
            m.SetFloat("_Cull", dbl ? 0 : 2);
            if (MiniJson.Bool(d, "backSide")) m.SetFloat("_Cull", 1);
            if (transparent) m.renderQueue = 3000;
            return m;
        }

        // ---------------- Modèles ----------------
        public static ModelData Model(string name)
        {
            if (modelCache.TryGetValue(name, out var md)) return md;
            var ta = Resources.Load<TextAsset>("E7/Models/" + name);
            md = ta ? ModelData.Read(ta.bytes) : null;
            if (md == null) Debug.LogWarning("[E7] Modèle manquant : " + name);
            modelCache[name] = md;
            return md;
        }
    }

    public class ModelNode { public string name; public int parent; public Vector3 pos; public Quaternion rot; }
    public class ModelPiece { public int node; public string tag, mat; public Vector3[] v, n; public Vector2[] uv; public int[] idx; }

    /// <summary>Modèle au format « E7M2 » écrit par l'exportateur de la version web.</summary>
    public class ModelData
    {
        public readonly List<ModelNode> nodes = new List<ModelNode>();
        public readonly List<ModelPiece> pieces = new List<ModelPiece>();
        readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

        public int NodeIndex(string name) { for (int i = 0; i < nodes.Count; i++) if (nodes[i].name == name) return i; return -1; }

        public static ModelData Read(byte[] bytes)
        {
            var md = new ModelData();
            using (var br = new BinaryReader(new MemoryStream(bytes)))
            {
                if (br.ReadUInt32() != 0x324d3745u) { Debug.LogWarning("[E7] format de modèle inconnu"); return null; }
                int nc = br.ReadUInt16();
                for (int i = 0; i < nc; i++)
                {
                    var n = new ModelNode { name = S(br), parent = br.ReadInt16() };
                    n.pos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    n.rot = new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    md.nodes.Add(n);
                }
                uint pc = br.ReadUInt32();
                for (int p = 0; p < pc; p++)
                {
                    var pc2 = new ModelPiece { node = br.ReadUInt16(), tag = S(br), mat = S(br) };
                    int vc = (int)br.ReadUInt32(), ic = (int)br.ReadUInt32();
                    pc2.v = new Vector3[vc]; pc2.n = new Vector3[vc]; pc2.uv = new Vector2[vc]; pc2.idx = new int[ic];
                    for (int i = 0; i < vc; i++) pc2.v[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    for (int i = 0; i < vc; i++) pc2.n[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    for (int i = 0; i < vc; i++) pc2.uv[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
                    for (int i = 0; i < ic; i++) pc2.idx[i] = (int)br.ReadUInt32();
                    md.pieces.Add(pc2);
                }
            }
            return md;
        }

        static string S(BinaryReader br) { int n = br.ReadUInt16(); return System.Text.Encoding.UTF8.GetString(br.ReadBytes(n)); }

        /// <summary>
        /// Crée la hiérarchie du modèle. <paramref name="keep"/> décide quelles pièces garder (selon leur étiquette : "base", "optic:reddot"…),
        /// <paramref name="material"/> donne le matériau de chaque clé.
        /// </summary>
        public GameObject Build(string name, Func<string, bool> keep, Func<string, Material> material, int layer, bool shadows = true)
        {
            var go = new GameObject(name);
            var tr = new Transform[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (i == 0) { tr[i] = go.transform; continue; }
                var ng = new GameObject(n.name);
                ng.transform.SetParent(n.parent >= 0 ? tr[n.parent] : go.transform, false);
                ng.transform.localPosition = n.pos;
                ng.transform.localRotation = n.rot;
                tr[i] = ng.transform;
            }
            for (int i = 0; i < nodes.Count; i++)
            {
                var mats = new List<string>();
                var mesh = MeshFor(i, keep, mats);
                if (mesh == null) continue;
                var holder = i == 0 ? U.Child(go.transform, "mesh") : U.Child(tr[i], "mesh");
                holder.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = holder.AddComponent<MeshRenderer>();
                var arr = new Material[mats.Count];
                for (int k = 0; k < mats.Count; k++) arr[k] = material(mats[k]);
                mr.sharedMaterials = arr;
                mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            U.SetLayer(go, layer);
            go.layer = layer;
            return go;
        }

        Mesh MeshFor(int node, Func<string, bool> keep, List<string> matsOut)
        {
            var sel = new List<ModelPiece>();
            var keyParts = new List<string>();
            for (int p = 0; p < pieces.Count; p++)
                if (pieces[p].node == node && keep(pieces[p].tag)) { sel.Add(pieces[p]); keyParts.Add(p.ToString()); }
            if (sel.Count == 0) return null;
            // regroupe par matériau (un sous-maillage par matériau)
            var order = new List<string>();
            foreach (var p in sel) if (!order.Contains(p.mat)) order.Add(p.mat);
            matsOut.AddRange(order);
            string ck = node + ":" + string.Join(",", keyParts);
            if (meshCache.TryGetValue(ck, out var cached)) return cached;
            var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>();
            var subs = new List<List<int>>();
            foreach (var mk in order)
            {
                var tri = new List<int>();
                foreach (var p in sel)
                {
                    if (p.mat != mk) continue;
                    int b = V.Count;
                    V.AddRange(p.v); N.AddRange(p.n); UV.AddRange(p.uv);
                    for (int i = 0; i < p.idx.Length; i++) tri.Add(p.idx[i] + b);
                }
                subs.Add(tri);
            }
            var mesh = new Mesh { name = "E7_" + node };
            if (V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, UV);
            mesh.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s);
            mesh.RecalculateBounds();
            try { mesh.RecalculateTangents(); } catch (Exception) { }
            meshCache[ck] = mesh;
            return mesh;
        }
    }
}
