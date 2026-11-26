using System.Collections.Generic;
using FantaLandia.EditorTools;
using UnityEditor;
using UnityEngine;

namespace FantaLandia.Horror.EditorTools
{
    /// <summary>
    /// Texture generate dal codice per lo shader FantaLandia/WorldTextured. Sono "moltiplicatori" del colore del
    /// materiale: 0.5 = colore invariato, più scuro nelle fughe e nelle ombre, più chiaro sui bordi e sulle venature.
    /// Ogni texture copre un riquadro di '_TileSize' metri ed è a pixel netti (stile cubettoso).
    /// Le righe della texture (asse v) sono l'altezza sui muri: perline, mattoni e tegole restano orizzontali.
    /// </summary>
    public static class ProceduralTextures
    {
        public enum Kind { Clapboard, Plaster, Wallpaper, Shingles, Grass, Asphalt, Slabs, Boards, VerticalBoards, CinderBlock, Brick, WoodGrain, Concrete }

        const string Folder = "Assets/_Project/Horror/Generated/Texture";
        static readonly Dictionary<Kind, Texture2D> cache = new Dictionary<Kind, Texture2D>();

        public static Texture2D Get(Kind kind)
        {
            if (cache.TryGetValue(kind, out Texture2D t) && t != null) return t;
            EditorAssetUtil.EnsureFolder(Folder);
            string path = $"{Folder}/{kind}.asset";
            int size = kind == Kind.Grass || kind == Kind.Asphalt ? 128 : 128;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool create = tex == null || tex.width != size;
            if (create)
            {
                if (tex != null) AssetDatabase.DeleteAsset(path);
                tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = kind.ToString() };
            }
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.anisoLevel = 4;
            var px = new Color[size * size];
            var rng = new System.Random((int)kind * 7919 + 13);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    px[y * size + x] = Sample(kind, u, v, x, y, size, rng);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            if (create) AssetDatabase.CreateAsset(tex, path);
            else EditorUtility.SetDirty(tex);
            cache[kind] = tex;
            return tex;
        }

        // rumore ripetibile (si ripete esattamente ogni 'period' celle: la texture non ha giunture)
        static float H(int x, int y, int s) { unchecked { int h = x * 374761393 + y * 668265263 + s * 982451653; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }
        static float VNoise(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int Wx(int a) => ((a % period) + period) % period;
            float a00 = H(Wx(x0), Wx(y0), seed), a10 = H(Wx(x0 + 1), Wx(y0), seed), a01 = H(Wx(x0), Wx(y0 + 1), seed), a11 = H(Wx(x0 + 1), Wx(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a00, a10, fx), Mathf.Lerp(a01, a11, fx), fy);
        }
        static float Fbm(float u, float v, int period, int seed) => VNoise(u, v, period, seed) * 0.55f + VNoise(u, v, period * 2, seed + 1) * 0.3f + VNoise(u, v, period * 4, seed + 2) * 0.15f;

        static Color G(float m) { m = Mathf.Clamp01(m); return new Color(m, m, m, 1f); }
        static Color Tint(float m, float r, float g, float b) => new Color(Mathf.Clamp01(m * r), Mathf.Clamp01(m * g), Mathf.Clamp01(m * b), 1f);

        static Color Sample(Kind kind, float u, float v, int x, int y, int size, System.Random rng)
        {
            float fine = H(x, y, (int)kind) - 0.5f; // granello per pixel
            switch (kind)
            {
                case Kind.Clapboard:
                {
                    // perline orizzontali alte 1/5 del riquadro: ombra sotto ogni asse, bordo chiaro sopra, venature
                    float rows = 5f, r = v * rows, fr = r - Mathf.Floor(r);
                    int row = Mathf.FloorToInt(r);
                    float shade = fr < 0.12f ? 0.28f : fr > 0.9f ? 0.6f : 0.5f - (0.9f - fr) * 0.06f;
                    float seg = Mathf.Floor(u * 2f + H(row, 0, 3) * 2f);                         // giunte delle assi
                    float board = (H(row, (int)seg, 5) - 0.5f) * 0.08f;
                    float joint = Mathf.Abs((u * 2f + H(row, 0, 3) * 2f) - Mathf.Round(u * 2f + H(row, 0, 3) * 2f)) < 0.01f ? -0.15f : 0f;
                    float grain = (VNoise(u * 0.3f, v, 64, 9) - 0.5f) * 0.06f;
                    return G(shade + board + joint + grain + fine * 0.03f);
                }
                case Kind.Plaster:
                {
                    float n = (Fbm(u, v, 8, 21) - 0.5f) * 0.08f + fine * 0.04f;
                    if (H(x, y, 77) > 0.995f) n -= 0.08f; // puntini
                    return G(0.5f + n);
                }
                case Kind.Wallpaper:
                {
                    // carta da parati anni '90: righe verticali e un motivo a rombi leggero
                    float stripe = Mathf.Repeat(u * 6f, 1f) < 0.5f ? 0.03f : -0.02f;
                    float du = Mathf.Abs(Mathf.Repeat(u * 6f, 1f) - 0.75f), dv = Mathf.Abs(Mathf.Repeat(v * 6f, 1f) - 0.5f);
                    float diamond = du + dv < 0.12f ? 0.05f : 0f;
                    return G(0.5f + stripe + diamond + (Fbm(u, v, 8, 31) - 0.5f) * 0.05f + fine * 0.02f);
                }
                case Kind.Shingles:
                {
                    // tegole bituminose: file sfalsate, linguette con luminosità diverse, fuga scura in basso
                    float rows = 4f, r = v * rows, fr = r - Mathf.Floor(r);
                    int row = Mathf.FloorToInt(r);
                    float off = (row % 2) * 0.5f;
                    float cu = u * 3f + off;
                    int tab = Mathf.FloorToInt(cu);
                    float edge = Mathf.Abs(cu - Mathf.Round(cu)) < 0.025f ? -0.2f : 0f;
                    float gap = fr < 0.1f ? -0.22f : 0f;
                    float t = (H(row, ((tab % 3) + 3) % 3, 11) - 0.5f) * 0.18f;
                    float grit = fine * 0.12f;
                    return G(0.5f + t + edge + gap + grit + (fr - 0.5f) * 0.06f);
                }
                case Kind.Grass:
                {
                    // prato: chiazze più chiare e più scure, zone secche giallastre, ciuffi
                    float blot = Fbm(u, v, 4, 41);
                    float dry = Mathf.Clamp01((Fbm(u, v, 3, 43) - 0.58f) * 4f);
                    float m = 0.42f + blot * 0.18f + fine * 0.12f;
                    var c = Tint(m, 1f, 1f, 1f);
                    return Color.Lerp(c, Tint(m * 1.1f, 1.35f, 1.1f, 0.6f), dry * 0.7f);
                }
                case Kind.Asphalt:
                {
                    float n = (Fbm(u, v, 8, 51) - 0.5f) * 0.1f + fine * 0.18f;
                    if (H(x, y, 53) > 0.985f) n += 0.18f;                                          // sassolini
                    float crack = Mathf.Abs(VNoise(u, v, 6, 55) - 0.5f) < 0.012f ? -0.18f : 0f;    // crepe
                    float patch = Fbm(u, v, 2, 57) > 0.68f ? -0.05f : 0f;                          // rattoppi
                    return G(0.5f + n + crack + patch);
                }
                case Kind.Slabs:
                {
                    // lastre di cemento da 1/2 riquadro con fughe, macchie e piccole crepe
                    float su = Mathf.Repeat(u * 2f, 1f), sv = Mathf.Repeat(v * 2f, 1f);
                    float joint = su < 0.025f || sv < 0.025f ? -0.2f : 0f;
                    int slab = Mathf.FloorToInt(u * 2f) + Mathf.FloorToInt(v * 2f) * 7;
                    float tone = (H(slab, 0, 61) - 0.5f) * 0.08f;
                    float stain = Fbm(u, v, 4, 63) > 0.66f ? -0.06f : 0f;
                    float crack = Mathf.Abs(VNoise(u, v, 5, 65) - 0.5f) < 0.008f ? -0.12f : 0f;
                    return G(0.5f + joint + tone + stain + crack + fine * 0.07f);
                }
                case Kind.Concrete:
                {
                    float n = (Fbm(u, v, 6, 71) - 0.5f) * 0.14f + fine * 0.08f;
                    if (Fbm(u, v, 3, 73) > 0.7f) n -= 0.07f;
                    return G(0.5f + n);
                }
                case Kind.Boards:
                case Kind.VerticalBoards:
                {
                    // assi di legno (righe lungo u; per la staccionata ruotate in verticale), fughe, venature, chiodi
                    float a = kind == Kind.Boards ? u : v, b = kind == Kind.Boards ? v : u;
                    float planks = kind == Kind.Boards ? 6f : 7f, r = b * planks, fr = r - Mathf.Floor(r);
                    int plank = Mathf.FloorToInt(r);
                    float gap = fr < 0.08f ? -0.25f : 0f;
                    float tone = (H(plank, 0, 81) - 0.5f) * 0.12f;
                    float grain = (VNoise(a * 0.25f, b + plank * 0.37f, 48, 83) - 0.5f) * 0.12f + Mathf.Sin((a * 40f + VNoise(a, b, 8, 85) * 6f)) * 0.015f;
                    float nail = (Mathf.Abs(Mathf.Repeat(a * 2f, 1f) - 0.05f) < 0.012f && Mathf.Abs(fr - 0.5f) < 0.09f) ? -0.18f : 0f;
                    return G(0.5f + gap + tone + grain + nail + fine * 0.03f);
                }
                case Kind.CinderBlock:
                {
                    // blocchi di cemento della cantina (40 x 20 cm su un riquadro di 0.8 m), malta sporgente, macchie
                    float rows = 4f, r = v * rows, fr = r - Mathf.Floor(r);
                    int row = Mathf.FloorToInt(r);
                    float cu = u * 2f + (row % 2) * 0.5f, fu = cu - Mathf.Floor(cu);
                    float mortar = fr < 0.06f || fu < 0.03f ? 0.08f : 0f;
                    float tone = (H(Mathf.FloorToInt(cu), row, 91) - 0.5f) * 0.1f;
                    float pits = H(x, y, 93) > 0.97f ? -0.12f : 0f;
                    float damp = Mathf.Clamp01((0.35f - v) * 3f) * (Fbm(u, v, 3, 95) - 0.3f) * -0.2f; // umidità in basso
                    return G(0.5f + mortar + tone + pits + damp + fine * 0.08f);
                }
                case Kind.Brick:
                {
                    float rows = 12f, r = v * rows, fr = r - Mathf.Floor(r);
                    int row = Mathf.FloorToInt(r);
                    float cu = u * 4f + (row % 2) * 0.5f, fu = cu - Mathf.Floor(cu);
                    float mortar = fr < 0.15f || fu < 0.05f ? 0.18f : 0f;
                    float tone = (H(Mathf.FloorToInt(cu), row, 97) - 0.5f) * 0.16f;
                    return Tint(0.5f + mortar + tone + fine * 0.06f, 1f, mortar > 0f ? 1.1f : 1f, mortar > 0f ? 1.15f : 1f);
                }
                case Kind.WoodGrain:
                default:
                {
                    float grain = Mathf.Sin(v * 60f + VNoise(u, v, 6, 101) * 9f) * 0.04f + (VNoise(u * 0.2f, v, 32, 103) - 0.5f) * 0.1f;
                    return G(0.5f + grain + fine * 0.03f);
                }
            }
        }
    }
}
