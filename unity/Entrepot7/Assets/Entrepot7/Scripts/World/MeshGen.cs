using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace E7
{
    /// <summary>Formes de base et fusion de maillages (pour le décor).</summary>
    public static class MeshGen
    {
        static Mesh cube, cyl, quad;

        public static Mesh Cube => cube ? cube : (cube = Prim(PrimitiveType.Cube));
        public static Mesh Cylinder => cyl ? cyl : (cyl = Prim(PrimitiveType.Cylinder));
        public static Mesh Quad => quad ? quad : (quad = Prim(PrimitiveType.Quad));

        static Mesh Prim(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t);
            var m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return m;
        }

        /// <summary>Cône tronqué ouvert (abat-jour, cône de lumière), axe Y, de 0 à h.</summary>
        public static Mesh Cone(float rTop, float rBottom, float h, int seg = 20, bool inside = false)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f, c = Mathf.Cos(a), s = Mathf.Sin(a);
                v.Add(new Vector3(c * rBottom, 0, s * rBottom)); uv.Add(new Vector2(i / (float)seg, 0));
                v.Add(new Vector3(c * rTop, h, s * rTop)); uv.Add(new Vector2(i / (float)seg, 1));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                if (!inside) { tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(c); tri.Add(b); tri.Add(d); }
                else { tri.Add(a); tri.Add(c); tri.Add(b); tri.Add(c); tri.Add(d); tri.Add(b); }
            }
            var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>Accumule des formes par matériau et par « morceau » de carte, puis fusionne.</summary>
        public class Batch
        {
            readonly Dictionary<string, List<CombineInstance>> parts = new Dictionary<string, List<CombineInstance>>();

            public void Add(string key, Mesh mesh, Matrix4x4 m)
            {
                if (!parts.TryGetValue(key, out var l)) parts[key] = l = new List<CombineInstance>();
                l.Add(new CombineInstance { mesh = mesh, transform = m });
            }

            public void Box(string key, Vector3 center, Vector3 size, float yRotDeg = 0f) => Add(key, Cube, Matrix4x4.TRS(center, Quaternion.Euler(0, yRotDeg, 0), size));
            public void Cyl(string key, Vector3 center, float r, float h) => Add(key, Cylinder, Matrix4x4.TRS(center, Quaternion.identity, new Vector3(r * 2f, h * 0.5f, r * 2f)));

            public IEnumerable<KeyValuePair<string, Mesh>> Build()
            {
                foreach (var kv in parts)
                {
                    var mesh = new Mesh { name = kv.Key, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                    mesh.RecalculateBounds();
                    try { mesh.RecalculateTangents(); } catch (System.Exception) { }
                    yield return new KeyValuePair<string, Mesh>(kv.Key, mesh);
                }
            }
        }
    }

    /// <summary>Type de surface (pour le son et l'effet d'impact des balles).</summary>
    public class Surface : MonoBehaviour { public string kind = "concrete"; }
}
