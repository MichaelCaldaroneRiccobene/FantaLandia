using System;
using System.Collections.Generic;
using FantaLandia.EditorTools;
using FantaLandia.Horror.Voxel;
using FantaLandia.Horror.World;
using UnityEditor;
using UnityEngine;
using static FantaLandia.Horror.EditorTools.HouseKit;
using Random = System.Random;

namespace FantaLandia.Horror.EditorTools
{
    /// <summary>
    /// I dettagli che rendono "vivi" casa e quartiere: cielo notturno (stelle, Via Lattea, luna, nuvole voxel),
    /// nebbia esterna, pavimenti a motivi (parquet, scacchi, piastrelline, moquette, cemento macchiato)
    /// e l'esterno delle case (grondaie, pluviali, persiane, camino, antenna, crepe, sporco alla base).
    /// </summary>
    public static class HouseDressing
    {
        // ------------------------------------------------------------------ cielo e nebbia

        public static void BuildSky(Transform world, Light moon)
        {
            Transform sky = Group("Cielo notturno", world);

            Shader skyShader = Shader.Find("FantaLandia/NightSky");
            var domeMat = AssetMat("Cielo notturno", skyShader);
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "Volta celeste";
            UnityEngine.Object.DestroyImmediate(dome.GetComponent<Collider>());
            dome.transform.SetParent(sky, false);
            dome.transform.localScale = Vector3.one * 500f;
            var r = dome.GetComponent<MeshRenderer>();
            r.sharedMaterial = domeMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            // le nuvole sono volumetriche, calcolate nello shader del cielo
            domeMat.SetFloat("_MoonSize", 0.024f);
            domeMat.SetFloat("_CloudCoverage", 0.5f);
            EditorUtility.SetDirty(domeMat);

            var ns = sky.gameObject.AddComponent<NightSky>();
            ns.dome = r;
            ns.moonLight = moon;
        }

        static Material AssetMat(string name, Shader shader)
        {
            string path = $"{EditorAssetUtil.MaterialFolder}/{name}.mat";
            EditorAssetUtil.EnsureFolder(EditorAssetUtil.MaterialFolder);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }

        // ------------------------------------------------------------------ pavimenti a motivi

        static readonly string[] Parquet = { "#6b4a30", "#5e4129", "#77543a", "#654530" };

        /// <summary>
        /// Pavimento a cubetti da 'cell' metri: 'pattern(ix, iz, x, z)' dà il colore di ogni cella (x, z = centro in coordinate
        /// del genitore) oppure null per lasciarla vuota (buchi delle scale, altre stanze). Il piano superiore è a quota y (+3 mm).
        /// </summary>
        public static void PatternFloor(Transform parent, string name, float x0, float x1, float z0, float z1, float y, float cell,
                                        Func<int, int, float, float, string> pattern)
        {
            var v = new VoxelBuilder(cell);
            int nx = Mathf.CeilToInt((x1 - x0) / cell - 0.01f), nz = Mathf.CeilToInt((z1 - z0) / cell - 0.01f);
            var cache = new Dictionary<string, int>();
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    float cx = Mathf.Min(x0 + (ix + 0.5f) * cell, x1), cz = Mathf.Min(z0 + (iz + 0.5f) * cell, z1);
                    string hex = pattern(ix, iz, cx, cz);
                    if (hex == null) continue;
                    if (!cache.TryGetValue(hex, out int ci)) { ci = v.C(hex); cache[hex] = ci; }
                    v.Set(ix, 0, iz, ci);
                }
            GameObject prefab = VoxelAssetLibrary.Save("Pavimento " + name, v, new Vector3(0f, cell, 0f), false);
            var go = Place(prefab, parent, new Vector3(x0, y + 0.003f, z0), 0f);
            if (go != null) go.name = "Pavimento " + name;
        }

        static int H(int a, int b, int s) { unchecked { int h = a * 73856093 ^ b * 19349663 ^ s * 83492791; h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return h & 0x7fffffff; } }

        /// <summary>Parquet a listoni sfalsati (8 cubetti per listone), quattro toni di legno.</summary>
        public static string ParquetAt(int ix, int iz)
        {
            int offset = (iz % 2) * 4;
            int plank = (ix + offset) / 8;
            return Parquet[H(plank, iz, 3) % Parquet.Length];
        }

        public static Func<int, int, string> Checker(string a, string b, int size) => (ix, iz) => ((ix / size + iz / size) % 2 == 0) ? a : b;

        /// <summary>Moquette: colore base con puntini più chiari e più scuri.</summary>
        public static Func<int, int, string> Carpet(string baseHex, string light, string dark, int seed) => (ix, iz) =>
        {
            int h = H(ix, iz, seed) % 100;
            return h < 8 ? light : h < 16 ? dark : baseHex;
        };

        /// <summary>Cemento della cantina con macchie (a chiazze grandi) e qualche crepa scura.</summary>
        public static string ConcreteAt(int ix, int iz)
        {
            int blob = H(ix / 4, iz / 4, 17) % 100;
            int fine = H(ix, iz, 29) % 100;
            if (fine < 3) return "#3e3c38";                 // crepe/sporco
            if (blob < 18) return fine < 50 ? "#55524c" : "#5c5953"; // macchie
            return fine < 50 ? "#6e6c68" : "#73716c";
        }

        // ------------------------------------------------------------------ esterno delle case

        public struct WindowSpec
        {
            public float center, width, bottom, top;
            public WindowSpec(float center, float width, float bottom, float top) { this.center = center; this.width = width; this.bottom = bottom; this.top = top; }
        }

        /// <summary>
        /// Grondaie lungo i bordi del tetto, pluviali agli angoli, persiane alle finestre del fronte, sporco e muschio
        /// alla base, crepe sui muri, camino e antenna sul tetto. Coordinate locali alla casa: il fronte guarda -Z.
        /// </summary>
        public static void DecorateHouse(Transform parent, float x0, float x1, float frontFace, float backFace, float eaveY,
                                         float roofEdgeFront, float roofEdgeBack, float roofEdgeX, float ridgeY,
                                         WindowSpec[] frontWindows, int seed, Vector2 chimneyXZ, bool antenna, float porchHalfWidth)
        {
            Transform g = Group("Dettagli esterni", parent);
            Material gutter = M("Grondaia", "#b9bab4", 0.4f);
            Material shutter = M("Persiane " + seed, seed % 2 == 0 ? "#2f4a3a" : "#2e3a52");
            Material slat = M("Persiane lamelle " + seed, seed % 2 == 0 ? "#253c2f" : "#252f44");
            Material dirt = M("Sporco", "#3b3a2c");
            Material moss = M("Muschio", "#33402a");
            Material crack = M("Crepa", "#2a2824");
            Material brick = MT("Mattoni", "#7a3e2e", ProceduralTextures.Kind.Brick, 0.9f, 0.1f);
            Material brickDark = MT("Mattoni scuri", "#5e2e22", ProceduralTextures.Kind.Brick, 0.9f, 0.1f);
            Material metal = M("Antenna", "#8a8c90", 0.5f);
            var rng = new Random(seed);

            // grondaie lungo la gronda e pluviali agli angoli, attaccati al muro con un gomito sotto la grondaia
            foreach (bool front in new[] { true, false })
            {
                float z = front ? roofEdgeFront - 0.06f : roofEdgeBack + 0.06f;
                float wz = front ? frontFace - 0.07f : backFace + 0.07f;   // davanti al muro
                float o = front ? -1f : 1f;                                 // verso l'esterno
                Box("Grondaia", g, new Vector3(-roofEdgeX - 0.05f, eaveY - 0.05f, z - 0.07f), new Vector3(roofEdgeX + 0.05f, eaveY + 0.1f, z + 0.07f), gutter);
                foreach (float x in new[] { x0 + 0.12f, x1 - 0.12f })
                {
                    Box("Pluviale", g, new Vector3(x - 0.05f, 0.08f, wz - 0.05f), new Vector3(x + 0.05f, eaveY - 0.35f, wz + 0.05f), gutter);
                    SlopedBox("Gomito pluviale", g, new Vector3(x, eaveY - 0.4f, wz), new Vector3(x, eaveY - 0.02f, z), 0.1f, 0.1f, gutter);
                    Box("Staffa", g, new Vector3(x - 0.07f, 1.5f, wz - 0.06f), new Vector3(x + 0.07f, 1.54f, wz + 0.06f), gutter);
                    Box("Scarico", g, new Vector3(x - 0.06f, 0f, Mathf.Min(wz, wz + o * 0.35f)), new Vector3(x + 0.06f, 0.08f, Mathf.Max(wz, wz + o * 0.35f)), gutter);
                }
            }

            // persiane ai lati delle finestre del fronte, con lamelle
            foreach (var w in frontWindows)
            {
                float sw = Mathf.Clamp(w.width * 0.45f, 0.3f, 0.5f);
                foreach (int side in new[] { -1, 1 })
                {
                    float cx = w.center + side * (w.width * 0.5f + 0.1f + sw * 0.5f);
                    Box("Persiana", g, new Vector3(cx - sw * 0.5f, w.bottom, frontFace - 0.045f), new Vector3(cx + sw * 0.5f, w.top, frontFace - 0.005f), shutter);
                    for (float y = w.bottom + 0.08f; y < w.top - 0.05f; y += 0.12f)
                        Box("Lamella", g, new Vector3(cx - sw * 0.42f, y, frontFace - 0.055f), new Vector3(cx + sw * 0.42f, y + 0.03f, frontFace - 0.04f), slat);
                }
            }

            // sporco e muschio alla base del fronte (bordo irregolare)
            for (float x = x0; x < x1; x += 0.3f)
            {
                if (Mathf.Abs(x + 0.15f) < porchHalfWidth + 0.15f) continue; // non davanti alla veranda e alla porta
                float h = 0.3f + (float)rng.NextDouble() * 0.25f;
                Box("Sporco", g, new Vector3(x, 0f, frontFace - 0.024f), new Vector3(Mathf.Min(x + 0.3f, x1), h, frontFace - 0.02f), rng.NextDouble() < 0.35 ? moss : dirt);
            }

            // crepe sul fronte e sui lati
            for (int i = 0; i < 10; i++)
            {
                float x = Mathf.Lerp(x0 + 0.5f, x1 - 0.5f, (float)rng.NextDouble());
                float y = 0.6f + (float)rng.NextDouble() * (eaveY - 1.2f);
                if (OnWindow(frontWindows, x, y) || Mathf.Abs(x) < 0.8f) continue; // non sui vetri né sulla porta
                Crack(g, new Vector3(x, y, frontFace - 0.005f), Vector3.back, Vector3.right, rng, crack);
            }
            for (int i = 0; i < 4; i++)
            {
                bool left = i % 2 == 0;
                float z = Mathf.Lerp(frontFace + 0.6f, backFace - 0.6f, (float)rng.NextDouble());
                float y = 0.5f + (float)rng.NextDouble() * (eaveY - 1.2f);
                float x = left ? x0 - 0.005f : x1 + 0.005f;
                Crack(g, new Vector3(x, y, z), left ? Vector3.left : Vector3.right, Vector3.forward, rng, crack);
            }

            // camino di mattoni (a filari alternati) e antenna della TV
            float cx0 = chimneyXZ.x, cz0 = chimneyXZ.y;
            for (float y = eaveY; y < ridgeY + 0.9f; y += 0.2f)
                Box("Mattoni", g, new Vector3(cx0 - 0.45f, y, cz0 - 0.45f), new Vector3(cx0 + 0.45f, y + 0.2f, cz0 + 0.45f), Mathf.RoundToInt(y * 5f) % 2 == 0 ? brick : brickDark);
            Box("Cappello camino", g, new Vector3(cx0 - 0.52f, ridgeY + 0.9f, cz0 - 0.52f), new Vector3(cx0 + 0.52f, ridgeY + 1.0f, cz0 + 0.52f), brickDark);
            if (antenna)
            {
                float ax = -cx0 * 0.6f, az = (roofEdgeFront + roofEdgeBack) * 0.5f;
                Box("Palo antenna", g, new Vector3(ax - 0.03f, ridgeY - 0.2f, az - 0.03f), new Vector3(ax + 0.03f, ridgeY + 1.6f, az + 0.03f), metal);
                for (int k = 0; k < 4; k++)
                {
                    float y = ridgeY + 0.8f + k * 0.22f, half = 0.6f - k * 0.1f;
                    Box("Elemento antenna", g, new Vector3(ax - half, y, az - 0.015f), new Vector3(ax + half, y + 0.03f, az + 0.015f), metal);
                }
                Box("Traversa antenna", g, new Vector3(ax - 0.015f, ridgeY + 0.75f, az - 0.5f), new Vector3(ax + 0.015f, ridgeY + 0.78f, az + 0.5f), metal);
            }

            // sono solo decorazioni: niente collider (non devono intralciare il giocatore né la NavMesh)
            foreach (var col in g.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(col);
        }

        static bool OnWindow(WindowSpec[] ws, float x, float y)
        {
            foreach (var w in ws)
                if (Mathf.Abs(x - w.center) < w.width * 0.5f + 0.75f && y > w.bottom - 0.6f && y < w.top + 0.15f) return true;
            return false;
        }

        /// <summary>Crepa a zig-zag sul muro: piccoli segmenti scuri che scendono, ognuno un po' in un'altra direzione.</summary>
        static void Crack(Transform parent, Vector3 start, Vector3 outward, Vector3 tangent, Random rng, Material mat)
        {
            Vector3 p = start;
            int segs = 3 + rng.Next(4);
            for (int i = 0; i < segs; i++)
            {
                float ang = Mathf.Lerp(-150f, -30f, (float)rng.NextDouble()) * Mathf.Deg2Rad;
                float len = 0.1f + (float)rng.NextDouble() * 0.18f;
                Vector3 q = p + (tangent * Mathf.Cos(ang) + Vector3.up * Mathf.Sin(ang)) * len;
                SlopedBox("Crepa", parent, p + outward * 0.002f, q + outward * 0.002f, 0.014f, 0.018f, mat);
                p = q;
            }
        }
    }
}
