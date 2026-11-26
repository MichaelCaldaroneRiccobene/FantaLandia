using System.Collections.Generic;
using FantaLandia.Horror.Audio;
using UnityEngine;

namespace FantaLandia.Horror.World
{
    /// <summary>
    /// Prato d'erba generato all'avvio: ciuffi (due rettangoli incrociati) sparsi solo sulle superfici
    /// marcate come erba (FootstepSurface = Grass), divisi a blocchi per non appesantire il disegno.
    /// L'ondeggiamento e la spinta dei personaggi li fa lo shader FantaLandia/WindVegetation.
    /// </summary>
    public class GrassField : MonoBehaviour
    {
        public Material material;
        public Vector2 areaMin = new Vector2(-40f, -9f);
        public Vector2 areaMax = new Vector2(40f, 22f);
        [Tooltip("Ciuffi per metro quadro")]
        public float density = 10f;
        public float chunkSize = 10f;
        public Vector2 heightRange = new Vector2(0.18f, 0.42f);
        public float tuftWidth = 0.14f;
        public int seed = 12345;

        static readonly Color32[] Greens =
        {
            new Color32(80, 122, 55, 255), new Color32(95, 135, 62, 255), new Color32(70, 106, 48, 255),
            new Color32(108, 142, 68, 255), new Color32(88, 126, 52, 255),
        };

        void Start() => Generate();

        int tufts;

        public void Generate()
        {
            if (material == null) { Debug.LogWarning("[Erba] Nessun materiale assegnato", this); return; }
            if (!material.shader.isSupported)
                Debug.LogError($"[Erba] Lo shader '{material.shader.name}' non è supportato su questa piattaforma", this);
            var rng = new System.Random(seed);
            tufts = 0;
            for (float cx = areaMin.x; cx < areaMax.x; cx += chunkSize)
                for (float cz = areaMin.y; cz < areaMax.y; cz += chunkSize)
                    BuildChunk(rng, cx, cz, Mathf.Min(cx + chunkSize, areaMax.x), Mathf.Min(cz + chunkSize, areaMax.y));
            Debug.Log($"[Erba] {name}: {tufts} ciuffi generati (shader {material.shader.name})", this);
        }

        void BuildChunk(System.Random rng, float x0, float z0, float x1, float z1)
        {
            var verts = new List<Vector3>();
            var cols = new List<Color32>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            int count = Mathf.RoundToInt((x1 - x0) * (z1 - z0) * density);
            Vector3 origin = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);

            for (int i = 0; i < count; i++)
            {
                float x = x0 + (float)rng.NextDouble() * (x1 - x0);
                float z = z0 + (float)rng.NextDouble() * (z1 - z0);
                float h = Mathf.Lerp(heightRange.x, heightRange.y, (float)rng.NextDouble());
                float yaw = (float)rng.NextDouble() * Mathf.PI;
                Color32 c = Greens[rng.Next(Greens.Length)];
                // il raggio parte da sotto le chiome degli alberi (che iniziano a ~1.8 m), così l'erba cresce anche lì
                if (!Physics.Raycast(new Vector3(x, 1.35f, z), Vector3.down, out RaycastHit hit, 4f, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                var surf = hit.collider.GetComponentInParent<FootstepSurface>();
                if (surf == null || surf.surface != Surface.Grass) continue;  // niente erba su vialetti, pedane, sotto auto e staccionate

                Vector3 basePos = hit.point - origin;
                tufts++;
                for (int q = 0; q < 2; q++)
                {
                    float a = yaw + q * Mathf.PI * 0.5f;
                    Vector3 side = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (tuftWidth * 0.5f);
                    int s = verts.Count;
                    verts.Add(basePos - side);
                    verts.Add(basePos - side * 0.6f + Vector3.up * h);
                    verts.Add(basePos + side * 0.6f + Vector3.up * h);
                    verts.Add(basePos + side);
                    Color32 bottom = new Color32((byte)(c.r * 0.7f), (byte)(c.g * 0.7f), (byte)(c.b * 0.7f), 0);
                    Color32 top = new Color32(c.r, c.g, c.b, 255);
                    cols.Add(bottom); cols.Add(top); cols.Add(top); cols.Add(bottom);
                    for (int k = 0; k < 4; k++) norms.Add(Vector3.up);
                    tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
                    tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
                }
            }
            if (verts.Count == 0) return;

            var mesh = new Mesh { name = "Erba" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var b = mesh.bounds;
            b.Expand(new Vector3(1f, 0.5f, 1f)); // il vento sposta i vertici: niente sparizioni ai bordi
            mesh.bounds = b;

            var go = new GameObject("Erba (blocco)");
            go.transform.SetParent(transform, false);
            go.transform.position = origin;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
