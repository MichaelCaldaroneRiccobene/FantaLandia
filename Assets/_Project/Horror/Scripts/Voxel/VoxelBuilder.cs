using System.Collections.Generic;
using UnityEngine;

namespace FantaLandia.Horror.Voxel
{
    /// <summary>
    /// Costruisce oggetti "a voxel": tanti cubetti colorati su una griglia.
    /// Le facce interne tra cubetti vicini non vengono generate. Ogni colore diventa una sotto-mesh
    /// (un materiale per colore). Coordinate in cubetti; 'size' è il lato di un cubetto in metri.
    /// </summary>
    public class VoxelBuilder
    {
        public readonly float size;
        readonly Dictionary<Vector3Int, int> cells = new Dictionary<Vector3Int, int>();
        readonly List<Color> palette = new List<Color>();

        public VoxelBuilder(float voxelSize) { size = voxelSize; }

        public IReadOnlyList<Color> Palette => palette;

        /// <summary>Indice di un colore nella tavolozza (lo aggiunge se nuovo).</summary>
        public int C(Color c)
        {
            for (int i = 0; i < palette.Count; i++)
                if (Approximately(palette[i], c)) return i;
            palette.Add(c);
            return palette.Count - 1;
        }

        public int C(string hex) => C(ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out Color c) ? c : Color.magenta);

        static bool Approximately(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;

        public void Set(int x, int y, int z, int color) => cells[new Vector3Int(x, y, z)] = color;

        public void Clear(int x, int y, int z) => cells.Remove(new Vector3Int(x, y, z));

        /// <summary>Riempie il blocco [x0,x1) x [y0,y1) x [z0,z1).</summary>
        public void Fill(int x0, int y0, int z0, int x1, int y1, int z1, int color)
        {
            for (int x = x0; x < x1; x++)
                for (int y = y0; y < y1; y++)
                    for (int z = z0; z < z1; z++) Set(x, y, z, color);
        }

        /// <summary>Svuota il blocco [x0,x1) x [y0,y1) x [z0,z1).</summary>
        public void Carve(int x0, int y0, int z0, int x1, int y1, int z1)
        {
            for (int x = x0; x < x1; x++)
                for (int y = y0; y < y1; y++)
                    for (int z = z0; z < z1; z++) Clear(x, y, z);
        }

        /// <summary>Colora (senza aggiungere) i cubetti già presenti nel blocco.</summary>
        public void Paint(int x0, int y0, int z0, int x1, int y1, int z1, int color)
        {
            for (int x = x0; x < x1; x++)
                for (int y = y0; y < y1; y++)
                    for (int z = z0; z < z1; z++)
                    {
                        var k = new Vector3Int(x, y, z);
                        if (cells.ContainsKey(k)) cells[k] = color;
                    }
        }

        public bool Has(int x, int y, int z) => cells.ContainsKey(new Vector3Int(x, y, z));

        public Bounds GetBounds()
        {
            if (cells.Count == 0) return new Bounds();
            Vector3Int min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue), max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            foreach (var k in cells.Keys) { min = Vector3Int.Min(min, k); max = Vector3Int.Max(max, k); }
            Vector3 a = (Vector3)min * size, b = ((Vector3)max + Vector3.one) * size;
            return new Bounds((a + b) * 0.5f, b - a);
        }

        static readonly Vector3Int[] Dirs =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        /// <summary>
        /// Crea la mesh. 'pivot' (in metri, nello spazio del modello) diventa l'origine: es. il centro della base.
        /// Restituisce anche i colori delle sotto-mesh, nello stesso ordine.
        /// </summary>
        public Mesh Build(string name, Vector3 pivot, out Color[] submeshColors)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<List<int>>();
            var colorToSub = new Dictionary<int, int>();
            var subColors = new List<Color>();

            foreach (var kv in cells)
            {
                Vector3Int p = kv.Key;
                if (!colorToSub.TryGetValue(kv.Value, out int sub))
                {
                    sub = tris.Count;
                    colorToSub[kv.Value] = sub;
                    tris.Add(new List<int>());
                    subColors.Add(palette[kv.Value]);
                }
                Vector3 center = ((Vector3)p + Vector3.one * 0.5f) * size - pivot;
                foreach (Vector3Int d in Dirs)
                {
                    if (cells.ContainsKey(p + d)) continue; // faccia nascosta
                    Vector3 n = d;
                    Vector3 v = Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up;
                    Vector3 u = Vector3.Cross(n, v);
                    float h = size * 0.5f;
                    Vector3 c = center + n * h;
                    int s = verts.Count;
                    verts.Add(c - u * h - v * h);
                    verts.Add(c - u * h + v * h);
                    verts.Add(c + u * h + v * h);
                    verts.Add(c + u * h - v * h);
                    for (int i = 0; i < 4; i++) norms.Add(n);
                    uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));
                    var t = tris[sub];
                    t.Add(s); t.Add(s + 1); t.Add(s + 2);
                    t.Add(s); t.Add(s + 2); t.Add(s + 3);
                }
            }

            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = tris.Count;
            for (int i = 0; i < tris.Count; i++) mesh.SetTriangles(tris[i], i);
            mesh.RecalculateBounds();
            submeshColors = subColors.ToArray();
            return mesh;
        }
    }
}
