using UnityEngine;

namespace FantaLandia.Horror.World
{
    /// <summary>
    /// Disegna "Lucia e il buio" sullo schermo della TV (immagine piccola, y = 0 in basso).
    /// Scene: 0 titolo, 1 prato al tramonto, 2 bosco all'imbrunire, 3 bosco di notte, 4 albero del gufo,
    /// 5 stagno di notte, 6 in cammino nel bosco, 7 a casa con le lucciole, 8 morale e "fine".
    /// Chi parla saltella e muove la bocca; di notte si vede solo dove arriva la luce di Lucia.
    /// </summary>
    public static class CartoonRenderer
    {
        static Color32[] px;
        static int W, H;
        static float[] light;

        public static void Draw(Color32[] pixels, int w, int h, int scene, float t, string speaker, bool glow)
        {
            px = pixels; W = w; H = h;
            if (light == null || light.Length != w * h) light = new float[w * h];
            bool night = scene == 3 || scene == 5 || scene == 6;
            System.Array.Clear(light, 0, light.Length);

            switch (scene)
            {
                case 0: Title(t); break;
                case 1: Meadow(t, speaker); break;
                case 2: Dusk(t, speaker); break;
                case 3: NightForest(t, speaker, glow); break;
                case 4: OwlTree(t, speaker, glow); break;
                case 5: Pond(t, speaker, glow); break;
                case 6: Walk(t, speaker, glow); break;
                case 7: Home(t, speaker); break;
                default: Moral(t); break;
            }

            if (night)
            {
                // buio: resta visibile solo un filo di luna e il cerchio di luce di Lucia
                for (int i = 0; i < px.Length; i++)
                {
                    float k = Mathf.Clamp01(0.13f + light[i]);
                    Color32 c = px[i];
                    px[i] = new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), 255);
                }
            }
        }

        // ------------------------------------------------------------------ scene

        static void Title(float t)
        {
            Sky(new Color32(10, 14, 40, 255), new Color32(30, 30, 80, 255));
            Stars(t, 40);
            bool en = FantaLandia.Horror.UI.Loc.English;
            Text("LUCIA", 48, 50, 2, new Color32(255, 230, 120, 255));
            Text(en ? "AND THE DARK" : "E IL BUIO", 48, 34, 2, new Color32(200, 210, 255, 255));
            Lucia(48 + Mathf.Sin(t * 1.3f) * 30f, 20 + Mathf.Sin(t * 2.1f) * 4f, t, false, true, false);
        }

        static void Meadow(float t, string sp)
        {
            Sky(new Color32(255, 150, 80, 255), new Color32(120, 70, 140, 255));
            Circle(70, 22, 9, new Color32(255, 200, 90, 255));                       // sole che tramonta
            Ground(20, new Color32(80, 150, 60, 255), new Color32(60, 120, 50, 255));
            Hills(new Color32(70, 130, 60, 255), 20, 3f, 0.15f, t * 0.2f);
            ForestEdge(78, new Color32(30, 60, 35, 255));
            for (int i = 0; i < 9; i++) Flower(4 + i * 8, 6 + (i * 5) % 9, i % 3);
            Lucia(32, 36, t, sp == "lucia", false, false);
            Tasso(52, 16, t, sp == "tasso", false);
        }

        static void Dusk(float t, string sp)
        {
            Sky(new Color32(120, 90, 150, 255), new Color32(40, 40, 90, 255));
            Ground(20, new Color32(40, 80, 45, 255), new Color32(30, 60, 35, 255));
            for (int i = 0; i < 6; i++) Tree(8 + i * 16, 20, 30 + (i * 7) % 12, new Color32(45, 30, 25, 255), new Color32(30, 60, 40, 255));
            Lucia(26, 34, t, sp == "lucia", false, false);
            Tasso(40, 14, t, sp == "tasso", false);
            Rana(80, 12, t, sp == "rana", true);
        }

        static void NightForest(float t, string sp, bool glow)
        {
            Sky(new Color32(10, 12, 30, 255), new Color32(5, 6, 15, 255));
            Stars(t, 15);
            Ground(18, new Color32(30, 60, 35, 255), new Color32(25, 50, 30, 255));
            for (int i = 0; i < 7; i++) Tree(4 + i * 14, 18, 34 + (i * 5) % 10, new Color32(60, 40, 30, 255), new Color32(30, 70, 40, 255));
            // occhi nel buio (non hanno bisogno di luce)
            float lx = 40 + Mathf.Sin(t * 0.7f) * 6f, ly = 36 + Mathf.Sin(t * 1.9f) * 3f;
            if (glow) Light(lx, ly, 26f, 1f);
            Lucia(lx, ly, t, sp == "lucia", glow, false);
            Tasso(52, 12, t, sp == "tasso", false);
            if (!glow) for (int k = 0; k < 3; k++) if (Mathf.Repeat(t + k * 1.7f, 4f) < 3.6f) { Glow(14 + k * 30, 30 + k * 6, new Color32(255, 230, 80, 255)); Glow(17 + k * 30, 30 + k * 6, new Color32(255, 230, 80, 255)); }
        }

        static void OwlTree(float t, string sp, bool glow)
        {
            Sky(new Color32(20, 26, 60, 255), new Color32(8, 10, 30, 255));
            Stars(t, 30);
            Circle(80, 60, 6, new Color32(235, 235, 210, 255));                     // luna
            Ground(14, new Color32(30, 60, 35, 255), new Color32(25, 50, 30, 255));
            FillRect(58, 14, 12, 50, new Color32(70, 48, 32, 255));                 // tronco del grande albero
            FillRect(40, 48, 24, 3, new Color32(70, 48, 32, 255));                  // ramo
            Circle(64, 66, 16, new Color32(35, 75, 45, 255));
            Circle(62, 32, 3, new Color32(20, 12, 8, 255));                         // buco nel tronco
            Gufo(46, 56, t, sp == "gufo");
            float lx = 24, ly = 34 + Mathf.Sin(t * 2f) * 2f;
            Lucia(lx, ly, t, sp == "lucia", glow, false);
            Tasso(26, 8, t, sp == "tasso", false);
        }

        static void Pond(float t, string sp, bool glow)
        {
            Sky(new Color32(15, 20, 50, 255), new Color32(6, 8, 22, 255));
            Stars(t, 30);
            Circle(16, 62, 5, new Color32(235, 235, 210, 255));
            FillRect(0, 0, W, 22, new Color32(20, 40, 80, 255));                    // acqua
            for (int x = 0; x < W; x += 3) Set(x + (int)(Mathf.Sin(t + x) * 1.5f), 10 + (x * 7) % 9, new Color32(60, 90, 140, 255));
            FillRect(14, 4, 4, 1, new Color32(200, 200, 170, 255));                 // riflesso della luna
            for (int i = 0; i < 8; i++) FillRect(2 + i * 12, 16, 1, 12 + (i * 5) % 8, new Color32(60, 100, 50, 255)); // giunchi
            Ellipse(64, 12, 9, 2, new Color32(50, 120, 60, 255));                   // ninfea
            Rana(64, 15, t, sp == "rana", !glow || sp == "rana");
            float lx = 40 + Mathf.Sin(t * 0.9f) * 4f, ly = 34 + Mathf.Sin(t * 2.2f) * 2f;
            if (glow) Light(lx, ly, 30f, 1f);
            Lucia(lx, ly, t, sp == "lucia", glow, false);
            Tasso(24, 20, t, sp == "tasso", false);
        }

        static void Walk(float t, string sp, bool glow)
        {
            Sky(new Color32(10, 12, 30, 255), new Color32(5, 6, 15, 255));
            Stars(t, 15);
            Ground(16, new Color32(30, 60, 35, 255), new Color32(25, 50, 30, 255));
            float scroll = t * 8f;
            for (int i = -1; i < 8; i++)
            {
                float x = Mathf.Repeat(i * 16 - scroll, 16 * 8) - 16;
                Tree((int)x, 16, 34 + (i * 7 & 7), new Color32(60, 40, 30, 255), new Color32(30, 70, 40, 255));
            }
            float lx = 70, ly = 30 + Mathf.Sin(t * 2.4f) * 2f;
            if (glow) Light(lx, ly, 34f, 1f);
            Lucia(lx, ly, t, sp == "lucia", glow, false);
            Rana(48, 10 + Mathf.Abs(Mathf.Sin(t * 4f)) * 3f, t, sp == "rana", false);
            Tasso(24, 10, t + 0.3f, sp == "tasso", true);
        }

        static void Home(float t, string sp)
        {
            Sky(new Color32(25, 30, 70, 255), new Color32(10, 12, 35, 255));
            Stars(t, 35);
            FillRect(0, 0, W, 20, new Color32(30, 60, 100, 255));
            Ellipse(30, 14, 10, 2, new Color32(60, 140, 70, 255));
            Ellipse(70, 12, 10, 2, new Color32(60, 140, 70, 255));
            Rana(26, 17, t, sp == "rana", false);
            Rana(34, 17, t + 1f, false, false, 1.2f);                                // mamma e papà rana
            Rana(74, 15, t + 2f, false, false, 1.2f);
            for (int i = 0; i < 26; i++)                                             // tante lucciole
            {
                float fx = Mathf.Repeat(i * 37 + t * (3 + i % 4), W), fy = 26 + (i * 13) % 40 + Mathf.Sin(t * 2f + i) * 2f;
                if (Mathf.Repeat(t * 0.8f + i * 0.37f, 2f) < 1.4f) Glow(fx, fy, new Color32(255, 235, 120, 255));
            }
            Lucia(50, 40, t, sp == "lucia", true, false);
            Tasso(52, 4, t, sp == "tasso", false);
        }

        static void Moral(float t)
        {
            Sky(new Color32(8, 10, 30, 255), new Color32(4, 5, 15, 255));
            Stars(t, 25);
            Color32 c = new Color32(255, 235, 150, 255);
            bool en = FantaLandia.Horror.UI.Loc.English;
            Text(en ? "EVEN THE" : "ANCHE LA LUCE", 48, 58, 1, c);
            Text(en ? "SMALLEST LIGHT" : "PIU PICCOLA", 48, 50, 1, c);
            Text(en ? "HELPS THOSE" : "AIUTA CHI SI E", 48, 42, 1, c);
            Text(en ? "LOST IN THE DARK" : "PERSO NEL BUIO", 48, 34, 1, c);
            Text(en ? "THE END" : "FINE", 48, 14, 2, new Color32(255, 255, 255, 255));
            Lucia(48 + Mathf.Sin(t) * 36f, 26, t, false, true, false);
        }

        // ------------------------------------------------------------------ personaggi

        static void Lucia(float cx, float cy, float t, bool talking, bool glowing, bool unused)
        {
            float bob = Mathf.Sin(t * 3f) * 1.2f + (talking ? Mathf.Abs(Mathf.Sin(t * 12f)) * 1.5f : 0f);
            int x = (int)cx, y = (int)(cy + bob);
            if (glowing) { Light(x - 3, y - 1, 16f, 0.9f); Halo(x - 3, y - 1, 6, new Color32(255, 230, 90, 255)); }
            Circle(x - 3, y - 1, 2, glowing ? new Color32(255, 245, 140, 255) : new Color32(150, 140, 70, 255)); // codina
            Ellipse(x, y, 3, 2, new Color32(120, 80, 40, 255));                      // corpo
            Circle(x + 3, y + 1, 2, new Color32(70, 45, 30, 255));                   // testa
            Set(x + 4, y + 2, new Color32(255, 255, 255, 255)); Set(x + 5, y + 1, talking && Mathf.Sin(t * 20f) > 0 ? new Color32(220, 80, 80, 255) : new Color32(40, 25, 15, 255));
            bool up = Mathf.Sin(t * 30f) > 0f;                                       // ali che battono
            Color32 wing = new Color32(200, 230, 255, 255);
            Set(x - 1, y + (up ? 3 : 2), wing); Set(x, y + (up ? 4 : 3), wing); Set(x + 1, y + (up ? 3 : 2), wing);
            Set(x + 4, y + 3, new Color32(40, 25, 15, 255)); Set(x + 5, y + 4, new Color32(40, 25, 15, 255)); // antenne
        }

        static void Tasso(float cx, float cy, float t, bool talking, bool walking)
        {
            float bob = walking ? Mathf.Abs(Mathf.Sin(t * 6f)) * 1.5f : 0f;
            int x = (int)cx, y = (int)(cy + bob);
            Color32 grey = new Color32(120, 120, 128, 255), dark = new Color32(40, 40, 45, 255), white = new Color32(235, 235, 235, 255);
            Ellipse(x, y + 4, 9, 4, grey);
            int legPhase = walking ? (int)(t * 8f) % 2 : 0;
            FillRect(x - 6, y, 2, 2 + legPhase, dark); FillRect(x + 4, y, 2, 3 - legPhase, dark);
            int hy = y + 6 + (talking ? (int)(Mathf.Abs(Mathf.Sin(t * 10f)) * 1.5f) : 0);
            Circle(x + 9, hy, 4, white);
            FillRect(x + 6, hy - 1, 8, 2, dark);                                     // banda nera sugli occhi
            Set(x + 11, hy, white); Set(x + 13, hy - 2, new Color32(20, 20, 20, 255)); // occhio, naso
            FillRect(x + 7, hy + 3, 4, 1, dark);                                     // striscia sulla testa
            if (talking && Mathf.Sin(t * 18f) > 0f) Set(x + 12, hy - 3, new Color32(200, 80, 90, 255));
            Set(x - 9, y + 5, dark); Set(x - 10, y + 6, dark);                       // coda
        }

        static void Gufo(float cx, float cy, float t, bool talking)
        {
            int x = (int)cx, y = (int)cy;
            Ellipse(x, y, 5, 7, new Color32(120, 80, 40, 255));
            Ellipse(x, y - 2, 3, 4, new Color32(190, 150, 100, 255));
            bool blink = Mathf.Repeat(t, 4f) > 3.85f;
            foreach (int ex in new[] { -2, 2 })
            {
                Circle(x + ex, y + 4, 2, new Color32(255, 255, 230, 255));
                if (!blink) Set(x + ex, y + 4, new Color32(20, 20, 20, 255)); else FillRect(x + ex - 2, y + 4, 5, 1, new Color32(120, 80, 40, 255));
            }
            Set(x - 4, y + 7, new Color32(90, 60, 30, 255)); Set(x + 4, y + 7, new Color32(90, 60, 30, 255)); // ciuffi
            Set(x, y + 2, new Color32(240, 160, 40, 255));
            if (talking && Mathf.Sin(t * 16f) > 0f) Set(x, y + 1, new Color32(240, 160, 40, 255));
        }

        static void Rana(float cx, float cy, float t, bool talking, bool crying, float scale = 1f)
        {
            int x = (int)cx, y = (int)(cy + (talking ? Mathf.Abs(Mathf.Sin(t * 9f)) * 2f : 0f));
            int r = Mathf.RoundToInt(4 * scale);
            Ellipse(x, y, r + 1, Mathf.Max(2, r - 1), new Color32(80, 170, 70, 255));
            foreach (int ex in new[] { -2, 2 })
            {
                Circle(x + ex, y + r - 1, 1, new Color32(255, 255, 255, 255));
                Set(x + ex, y + r - 1, new Color32(10, 10, 10, 255));
            }
            FillRect(x - 2, y - 1, 5, 1, talking && Mathf.Sin(t * 16f) > 0f ? new Color32(200, 70, 80, 255) : new Color32(40, 90, 40, 255));
            if (crying)
            {
                int d = (int)Mathf.Repeat(t * 6f, 4f);
                Set(x - 3, y + 1 - d, new Color32(120, 180, 255, 255)); Set(x + 3, y + 1 - d, new Color32(120, 180, 255, 255));
            }
        }

        // ------------------------------------------------------------------ fondali

        static void Sky(Color32 top, Color32 bottom)
        {
            for (int y = 0; y < H; y++)
            {
                Color32 c = Color32.Lerp(bottom, top, y / (float)H);
                for (int x = 0; x < W; x++) px[y * W + x] = c;
            }
        }

        static void Stars(float t, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int x = (i * 53 + 7) % W, y = 30 + (i * 29) % (H - 30);
                if (Mathf.Repeat(t * 0.7f + i * 0.31f, 3f) < 2.6f) Set(x, y, new Color32(220, 220, 255, 255));
            }
        }

        static void Ground(int top, Color32 a, Color32 b)
        {
            for (int y = 0; y < top; y++)
                for (int x = 0; x < W; x++) px[y * W + x] = ((x / 3 + y / 2) % 5 == 0) ? b : a;
        }

        static void Hills(Color32 c, int baseY, float amp, float freq, float phase)
        {
            for (int x = 0; x < W; x++)
            {
                int h = baseY + (int)(amp * (1f + Mathf.Sin(x * freq + phase)));
                for (int y = baseY; y < h; y++) Set(x, y, c);
            }
        }

        static void ForestEdge(int x0, Color32 c)
        {
            for (int x = x0; x < W; x++)
            {
                int h = 34 + (int)(4 * Mathf.Sin(x * 0.9f)) + (x - x0) / 2;
                for (int y = 18; y < h; y++) Set(x, y, c);
            }
        }

        static void Tree(int x, int groundY, int height, Color32 trunk, Color32 leaves)
        {
            FillRect(x - 1, groundY, 3, height - 8, trunk);
            Circle(x, groundY + height - 6, 6, leaves);
            Circle(x - 3, groundY + height - 10, 4, leaves);
            Circle(x + 3, groundY + height - 9, 4, leaves);
        }

        static void Flower(int x, int y, int kind)
        {
            Color32 petal = kind == 0 ? new Color32(255, 230, 80, 255) : kind == 1 ? new Color32(240, 110, 160, 255) : new Color32(250, 250, 250, 255);
            Set(x, y - 1, new Color32(40, 100, 40, 255));
            Set(x, y, new Color32(250, 200, 60, 255));
            Set(x - 1, y, petal); Set(x + 1, y, petal); Set(x, y + 1, petal);
        }

        // ------------------------------------------------------------------ primitive

        static void Set(float fx, float fy, Color32 c)
        {
            int x = (int)fx, y = (int)fy;
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            px[y * W + x] = c;
        }

        static void FillRect(int x0, int y0, int w, int h, Color32 c)
        {
            for (int y = y0; y < y0 + h; y++) for (int x = x0; x < x0 + w; x++) Set(x, y, c);
        }

        static void Circle(float cx, float cy, float r, Color32 c) => Ellipse(cx, cy, r, r, c);

        static void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            for (int y = (int)(cy - ry); y <= (int)(cy + ry); y++)
                for (int x = (int)(cx - rx); x <= (int)(cx + rx); x++)
                {
                    float dx = (x - cx) / Mathf.Max(0.5f, rx), dy = (y - cy) / Mathf.Max(0.5f, ry);
                    if (dx * dx + dy * dy <= 1.05f) Set(x, y, c);
                }
        }

        /// <summary>Un puntino luminoso (lucciole, occhi nel buio): non viene oscurato dalla notte.</summary>
        static void Glow(float x, float y, Color32 c)
        {
            Set(x, y, c);
            int i = (int)y * W + (int)x;
            if (i >= 0 && i < light.Length && x >= 0 && x < W) light[i] = 1f;
        }

        /// <summary>Alone attorno alla codina di Lucia.</summary>
        static void Halo(int cx, int cy, int r, Color32 c)
        {
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                    if (d > 1f) continue;
                    float k = (1f - d) * 0.45f;
                    Color32 p = px[y * W + x];
                    px[y * W + x] = new Color32((byte)Mathf.Min(255, p.r + c.r * k), (byte)Mathf.Min(255, p.g + c.g * k), (byte)Mathf.Min(255, p.b + c.b * k), 255);
                }
        }

        /// <summary>Luce che rischiara il buio attorno a un punto (cerchio morbido).</summary>
        static void Light(float cx, float cy, float r, float strength)
        {
            for (int y = (int)(cy - r); y <= (int)(cy + r); y++)
                for (int x = (int)(cx - r); x <= (int)(cx + r); x++)
                {
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                    if (d >= 1f) continue;
                    float k = (1f - d * d) * strength;
                    int i = y * W + x;
                    light[i] = Mathf.Max(light[i], k);
                }
        }

        // ------------------------------------------------------------------ scritte (font 3x5)

        static readonly string Letters = "ABCEFHILNOPRSTUDGKMV";
        static readonly string[] Glyphs =
        {
            "010101111101101", "110101110101110", "011100100100011", "111100110100111", "111100110100100",
            "101101111101101", "111010010010111", "100100100100111", "110101101101101", "010101101101010",
            "110101110100100", "110101110101101", "011100010001110", "111010010010010", "101101101101111",
            "110101101101110", "011100101101011", "101101110101101", "101111111101101", "101101101101010",
        };

        static void Text(string s, int centerX, int y, int scale, Color32 c)
        {
            int width = s.Length * 4 * scale - scale;
            int x = centerX - width / 2;
            foreach (char ch in s)
            {
                int g = Letters.IndexOf(ch);
                if (g >= 0)
                    for (int row = 0; row < 5; row++)
                        for (int col = 0; col < 3; col++)
                            if (Glyphs[g][row * 3 + col] == '1')
                                for (int sy = 0; sy < scale; sy++)
                                    for (int sx = 0; sx < scale; sx++)
                                        Glow(x + col * scale + sx, y + (4 - row) * scale + sy, c);
                x += 4 * scale;
            }
        }
    }
}
