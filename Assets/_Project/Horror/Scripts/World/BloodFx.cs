using UnityEngine;

namespace FantaLandia.Horror.World
{
    /// <summary>
    /// Il sangue sulle superfici (capitolo 2): da un punto partono tanti "schizzi" in un cono; dove ogni schizzo
    /// colpisce qualcosa (muri, pavimento, stipiti, la porta) resta una macchia piatta appoggiata alla superficie,
    /// allungata se ci è arrivata di striscio. Più si è lontani, più le macchie sono piccole.
    /// Il materiale (con le quattro macchie diverse in una texture 2x2) lo prepara il generatore della scena.
    /// </summary>
    public static class BloodFx
    {
        static Mesh[] quads;
        static Transform root;

        /// <summary>Spruzza il sangue da 'origin' verso 'dir' (cono di 'spreadDeg' gradi).</summary>
        public static void Spray(Material mat, Vector3 origin, Vector3 dir, int count, float range, float spreadDeg, float sizeScale = 1f)
        {
            if (mat == null) return;
            int mask = ~((1 << 2) | (1 << 7)); // né il giocatore né i personaggi
            for (int i = 0; i < count; i++)
            {
                Vector3 d = Quaternion.AngleAxis(Random.Range(-spreadDeg, spreadDeg), Random.onUnitSphere) * dir.normalized;
                if (Physics.Raycast(origin, d, out RaycastHit hit, range * Random.Range(0.4f, 1f), mask, QueryTriggerInteraction.Ignore))
                {
                    float near = 1f - hit.distance / range;
                    float size = Mathf.Lerp(0.12f, 0.75f, near * near) * Random.Range(0.6f, 1.3f) * sizeScale;
                    Splat(mat, hit, d, size);
                }
            }
        }

        /// <summary>Una pozza sotto un punto (il sangue che cola a terra).</summary>
        public static void Pool(Material mat, Vector3 above, float size)
        {
            if (mat == null) return;
            int mask = ~((1 << 2) | (1 << 7));
            if (Physics.Raycast(above + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 4f, mask, QueryTriggerInteraction.Ignore))
                Splat(mat, hit, Vector3.down, size);
        }

        static void Splat(Material mat, RaycastHit hit, Vector3 incoming, float size)
        {
            if (quads == null) quads = new[] { Quad(0, 0), Quad(1, 0), Quad(0, 1), Quad(1, 1) };
            if (root == null) root = new GameObject("Sangue").transform;
            var go = new GameObject("Macchia di sangue");
            go.transform.SetParent(root, false);
            // la macchia si allunga nella direzione in cui è arrivato lo schizzo
            Vector3 along = Vector3.ProjectOnPlane(incoming, hit.normal);
            if (along.sqrMagnitude < 0.0001f) along = Vector3.ProjectOnPlane(Random.onUnitSphere, hit.normal);
            float graze = Mathf.Clamp(1f / Mathf.Max(0.3f, Vector3.Dot(-incoming.normalized, hit.normal)), 1f, 3f);
            go.transform.position = hit.point + hit.normal * (0.004f + Random.value * 0.004f);
            go.transform.rotation = Quaternion.LookRotation(-hit.normal, along.normalized) * Quaternion.Euler(0f, 0f, Random.Range(-15f, 15f));
            go.transform.localScale = new Vector3(size, size * graze, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = quads[Random.Range(0, quads.Length)];
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Un quadrato 1x1 con le UV di una delle quattro celle della texture.</summary>
        static Mesh Quad(int cx, int cy)
        {
            var m = new Mesh { name = "Macchia" };
            float u0 = cx * 0.5f, v0 = cy * 0.5f;
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            m.uv = new[] { new Vector2(u0, v0), new Vector2(u0 + 0.5f, v0), new Vector2(u0, v0 + 0.5f), new Vector2(u0 + 0.5f, v0 + 0.5f) };
            // la faccia guarda verso -Z (cioè fuori dalla superficie, visto che +Z entra nel muro)
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            m.tangents = new[] { new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1) };
            m.RecalculateBounds();
            return m;
        }
    }
}
