using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Interaction;
using UnityEngine;

namespace FantaLandia.Horror.World
{
    public enum TvChannel { Off, Cartoon, Static, News }

    /// <summary>
    /// Televisore a tubo catodico anni '90: lo schermo è una texture a bassa risoluzione disegnata dal codice
    /// (cartoni animati, neve, notiziario) con linee di scansione, più una luce che illumina la stanza
    /// con il colore dello schermo e il suono del canale.
    /// </summary>
    public class CrtTv : Interactable
    {
        public Renderer screen;
        public Light screenLight;
        public TvChannel channel = TvChannel.Cartoon;
        [Tooltip("Senza corrente la TV è spenta (blackout)")]
        public bool powered = true;
        public string newsHeadline = "EDIZIONE STRAORDINARIA";
        [Tooltip("Il giocatore può accenderla e spegnerla con E (nella storia no: la TV la gestisce la regia)")]
        public bool playerCanToggle;
        [Header("Capitolo 2")]
        [Tooltip("Lo schermo è crepato (la TV di Tom sotto le macerie)")]
        public bool cracked;
        [Tooltip("Il notiziario finale: -1 = studio normale, 0..5 = le immagini del servizio")]
        public int newsScene = -1;
        [Tooltip("Volume del suono della TV (cartoni, neve)")]
        [Range(0f, 1f)] public float volume = 0.021f;
        [Tooltip("Volume delle voci del cartone (basse: è la TV accesa in sottofondo)")]
        [Range(0f, 1f)] public float cartoonVoiceVolume = 0.75f;

        // il cartone "Lucia e il buio": va avanti solo mentre è sul canale dei cartoni, poi ricomincia
        AudioSource cartoonVoice;
        float episodeClock, waitUntil, sceneStart, speakingUntil;
        int beat, scene;
        bool cartoonGlow, voicePaused;
        string speaker;

        const int W = 96, H = 72;
        Texture2D tex;
        Color32[] pixels;
        Material mat;
        AudioSource sound;
        float nextFrame;
        float lightIntensity = 1.2f;
        TvChannel lastChannel = (TvChannel)(-1);
        bool lastPowered;

        public override string Prompt(Interactor who) => IsOn ? "Spegni la TV" : "Accendi la TV";
        public bool IsOn => powered && channel != TvChannel.Off;

        void Awake()
        {
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            pixels = new Color32[W * H];
            if (screen != null)
            {
                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
                mat = new Material(unlit != null ? unlit : Shader.Find("Unlit/Texture"));
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                mat.mainTexture = tex;
                screen.sharedMaterial = mat;
            }
            if (screenLight != null) lightIntensity = screenLight.intensity;
            sound = HorrorAudio.CreateLoop(transform, null, volume, true, 12f);
            var vgo = new GameObject("Voci del cartone");
            vgo.transform.SetParent(transform, false);
            vgo.transform.localPosition = Vector3.up * 0.3f;
            cartoonVoice = vgo.AddComponent<AudioSource>();
            cartoonVoice.playOnAwake = false;
            cartoonVoice.spatialBlend = 1f;
            cartoonVoice.rolloffMode = AudioRolloffMode.Linear;
            cartoonVoice.minDistance = 1f;
            cartoonVoice.maxDistance = 10f;
        }

        public override bool CanInteract(Interactor who) => enabled && playerCanToggle;

        public override void Interact(Interactor who)
        {
            HorrorAudio.PlayAt(ProceduralAudio.Click(), transform.position, 0.7f);
            channel = channel == TvChannel.Off ? TvChannel.Cartoon : TvChannel.Off;
        }

        public void SetChannel(TvChannel c) => channel = c;

        void Update()
        {
            bool on = IsOn;
            if (channel != lastChannel || powered != lastPowered)
            {
                lastChannel = channel;
                lastPowered = powered;
                AudioClip clip = !on ? null : channel == TvChannel.Cartoon ? ProceduralAudio.CartoonMusic()
                               : channel == TvChannel.Static ? ProceduralAudio.TvStatic() : null;
                sound.Stop();
                sound.clip = clip;
                if (clip != null) sound.Play();
            }

            // il cartone avanza solo quando lo si guarda (canale dei cartoni, TV accesa)
            bool cartoon = on && channel == TvChannel.Cartoon;
            if (cartoon) StepEpisode(Time.deltaTime);
            else if (cartoonVoice != null && cartoonVoice.isPlaying) { cartoonVoice.Pause(); voicePaused = true; }
            // la musica si abbassa quando parlano
            if (sound != null && sound.isPlaying)
                sound.volume = Mathf.MoveTowards(sound.volume, (cartoon && cartoonVoice != null && cartoonVoice.isPlaying ? volume * 0.4f : volume) * FantaLandia.Horror.UI.GameSettings.Music, Time.deltaTime * 0.5f);

            if (Time.time < nextFrame) return;
            nextFrame = Time.time + 1f / 12f;

            if (!on) Fill(new Color32(6, 8, 8, 255));
            else if (channel == TvChannel.Cartoon) CartoonRenderer.Draw(pixels, W, H, scene, episodeClock - sceneStart, episodeClock < speakingUntil ? speaker : null, cartoonGlow);
            else if (channel == TvChannel.Static) DrawStatic();
            else if (newsScene >= 0) DrawReport(newsScene, Time.time);
            else DrawNews(Time.time);

            if (on) Scanlines();
            if (cracked) DrawCracks(on);
            tex.SetPixels32(pixels);
            tex.Apply(false);

            if (screenLight != null)
            {
                screenLight.enabled = on;
                if (on)
                {
                    Color avg = Average();
                    screenLight.color = Color.Lerp(avg, Color.white, 0.25f);
                    screenLight.intensity = lightIntensity * Random.Range(0.85f, 1.05f);
                }
            }
        }

        /// <summary>Fa scorrere la sceneggiatura del cartone: scene, pause, luce di Lucia e battute con la voce.</summary>
        void StepEpisode(float dt)
        {
            episodeClock += dt;
            if (voicePaused) { cartoonVoice.UnPause(); voicePaused = false; }
            if (episodeClock < waitUntil) return;
            string[] script = CartoonEpisode.Script;
            for (int guard = 0; guard < 64; guard++)
            {
                if (beat >= script.Length)
                {
                    // fine: si ricomincia dal titolo
                    beat = 0; scene = 0; cartoonGlow = false; sceneStart = episodeClock;
                }
                string b = script[beat++];
                if (b.StartsWith("@")) { int.TryParse(b.Substring(1), out scene); sceneStart = episodeClock; continue; }
                if (b.StartsWith("~"))
                {
                    float.TryParse(b.Substring(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float pause);
                    waitUntil = episodeClock + pause;
                    return;
                }
                if (b == "!luce") { cartoonGlow = true; continue; }
                int bar = b.IndexOf('|');
                if (bar <= 0) continue;
                speaker = b.Substring(0, bar);
                string text = FantaLandia.Horror.UI.Loc.T(b.Substring(bar + 1));
                AudioClip clip = FantaLandia.Horror.Dialogue.DialogueSystem.VoiceFor(FantaLandia.Horror.Dialogue.Speakers.Find(speaker), text);
                float len = clip != null ? clip.length : 1.5f + text.Length * 0.06f;
                if (clip != null && cartoonVoice != null)
                {
                    cartoonVoice.clip = clip;
                    cartoonVoice.volume = cartoonVoiceVolume * FantaLandia.Horror.UI.GameSettings.Music;
                    cartoonVoice.Play();
                }
                speakingUntil = episodeClock + len;
                waitUntil = episodeClock + len + 1.0f; // un respiro tra una battuta e l'altra, come nei cartoni
                return;
            }
        }

        // ------------------------------------------------------------------ disegno dello schermo

        void Fill(Color32 c)
        {
            for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
        }

        void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            pixels[y * W + x] = c;
        }

        void FillRect(int x0, int y0, int w, int h, Color32 c)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++) Set(x, y, c);
        }

        void Circle(float cx, float cy, float r, Color32 c)
        {
            for (int y = (int)(cy - r); y <= (int)(cy + r); y++)
                for (int x = (int)(cx - r); x <= (int)(cx + r); x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Set(x, y, c);
        }

        void DrawStatic()
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                byte v = (byte)Random.Range(0, 230);
                pixels[i] = new Color32(v, v, v, 255);
            }
        }

        /// <summary>Notiziario: studio blu, giornalista stilizzato, banda rossa in basso che scorre.</summary>
        void DrawNews(float t)
        {
            Fill(new Color32(20, 40, 110, 255));
            FillRect(0, 0, W, 14, new Color32(170, 20, 20, 255));
            for (int x = 0; x < W; x++)
                if (((x + (int)(t * 20f)) / 3) % 4 == 0) FillRect(x, 5, 1, 4, new Color32(255, 255, 255, 255)); // "testo" che scorre
            FillRect(36, 14, 24, 26, new Color32(40, 40, 50, 255));       // giacca
            FillRect(46, 30, 4, 10, new Color32(230, 230, 230, 255));     // camicia
            Circle(48, 47, 7, new Color32(230, 190, 160, 255));       // testa
            FillRect(42, 52, 13, 3, new Color32(60, 40, 30, 255));        // capelli
            FillRect(6, 50, 22, 16, new Color32(200, 200, 220, 255));     // riquadro con la mappa
            Circle(17, 58, 3, new Color32(220, 40, 40, 255));
            if (Mathf.Repeat(t, 1f) < 0.5f) FillRect(W - 14, H - 8, 10, 4, new Color32(230, 30, 30, 255)); // "LIVE"
        }

        // ------------------------------------------------------------------ capitolo 2: il servizio del notiziario e lo schermo crepato

        static Color32 Rgb(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

        /// <summary>
        /// Le immagini del servizio finale: 0 studio d'emergenza, 1 la città in fiamme vista dall'elicottero con la sagoma
        /// gigante, 2 la mappa della contea con la zona rossa e le frecce verso nord, 3 mezzi militari ed elicottero,
        /// 4 la colonna di auto in fuga, 5 segnale perso (barre colorate).
        /// </summary>
        void DrawReport(int sceneIndex, float t)
        {
            switch (sceneIndex)
            {
                case 0:
                    DrawNews(t);
                    FillRect(0, 0, W, 14, Rgb(200, 10, 10));
                    for (int x = 0; x < W; x++)
                        if (((x + (int)(t * 30f)) / 2) % 5 < 2) FillRect(x, 4, 1, 6, Rgb(255, 230, 60));
                    break;
                case 1:
                {
                    // cielo nero-arancio, case in fiamme in basso, la sagoma gigante in piedi in mezzo al fumo
                    for (int y = 0; y < H; y++)
                    {
                        float k = y / (float)H;
                        Color32 c = Color32.Lerp(Rgb(150, 60, 20), Rgb(12, 10, 16), Mathf.Clamp01(k * 1.6f));
                        FillRect(0, y, W, 1, c);
                    }
                    float pan = t * 2f;
                    for (int i = 0; i < 14; i++)
                    {
                        int bx = (int)Mathf.Repeat(i * 9 - pan, W + 12) - 6, bh = 6 + (i * 7) % 9;
                        FillRect(bx, 0, 7, bh, Rgb(20, 16, 18));
                        if ((i + (int)(t * 6f)) % 3 == 0) FillRect(bx + 2, bh - 2, 3, 2, Rgb(255, 140, 40));
                    }
                    int gx = 58 - (int)(pan * 0.3f) % 6;
                    FillRect(gx, 6, 4, 26, Rgb(8, 6, 8));          // gamba
                    FillRect(gx + 7, 6, 4, 24, Rgb(8, 6, 8));      // gamba
                    FillRect(gx - 1, 28, 13, 22, Rgb(8, 6, 8));    // corpo
                    FillRect(gx - 7, 30, 6, 3, Rgb(8, 6, 8));      // braccio lungo
                    FillRect(gx - 9, 18, 3, 14, Rgb(8, 6, 8));
                    FillRect(gx + 12, 34, 7, 3, Rgb(8, 6, 8));
                    FillRect(gx + 17, 20, 3, 16, Rgb(8, 6, 8));
                    FillRect(gx + 3, 50, 6, 6, Rgb(8, 6, 8));      // testa piccola
                    for (int i = 0; i < 40; i++) // fumo che passa
                    {
                        int sx = (int)Mathf.Repeat(i * 13 + t * 9f, W), sy = 20 + (i * 17) % 40;
                        Circle(sx, sy, 2 + i % 3, Rgb(60, 50, 50));
                    }
                    if (Mathf.Repeat(t, 1f) < 0.5f) FillRect(4, H - 8, 10, 4, Rgb(230, 30, 30)); // REC
                    break;
                }
                case 2:
                {
                    // la mappa: contea verde, strade, la zona rossa che pulsa, frecce verso nord (in alto)
                    Fill(Rgb(40, 70, 45));
                    for (int x = 0; x < W; x += 12) FillRect(x, 0, 1, H, Rgb(70, 95, 70));
                    for (int y = 0; y < H; y += 12) FillRect(0, y, W, 1, Rgb(70, 95, 70));
                    FillRect(44, 0, 2, H, Rgb(220, 210, 160));      // Route 9
                    FillRect(0, 30, W, 2, Rgb(220, 210, 160));
                    float pulse = 14f + Mathf.Sin(t * 4f) * 2f;
                    Circle(52, 26, pulse, Rgb(190, 30, 30));
                    Circle(52, 26, pulse - 4f, Rgb(230, 60, 50));
                    for (int a = 0; a < 3; a++)
                    {
                        int ax = 30 + a * 16, ay = 44 + (int)Mathf.Repeat(t * 10f + a * 4f, 16f);
                        FillRect(ax, ay, 3, 8, Rgb(255, 255, 255));
                        FillRect(ax - 2, ay + 7, 7, 2, Rgb(255, 255, 255));
                        FillRect(ax - 1, ay + 9, 5, 1, Rgb(255, 255, 255));
                    }
                    FillRect(0, 0, W, 9, Rgb(170, 20, 20));
                    break;
                }
                case 3:
                {
                    // camion militari con i fari nella notte, un elicottero che passa
                    Fill(Rgb(14, 18, 20));
                    FillRect(0, 0, W, 16, Rgb(30, 30, 28));
                    for (int i = 0; i < 3; i++)
                    {
                        int tx = (int)Mathf.Repeat(i * 34 + t * 6f, W + 30) - 30;
                        FillRect(tx, 10, 26, 12, Rgb(50, 62, 40));      // cassone
                        FillRect(tx + 26, 10, 8, 9, Rgb(44, 56, 36));   // cabina
                        Circle(tx + 5, 9, 3, Rgb(15, 15, 15));
                        Circle(tx + 22, 9, 3, Rgb(15, 15, 15));
                        FillRect(tx + 33, 13, 2, 2, Rgb(255, 240, 180)); // faro
                    }
                    int hx = (int)Mathf.Repeat(t * 14f, W + 30) - 15;
                    FillRect(hx, 52, 14, 6, Rgb(40, 46, 40));
                    FillRect(hx + 14, 55, 10, 2, Rgb(40, 46, 40));
                    if (((int)(t * 20f)) % 2 == 0) FillRect(hx - 8, 59, 30, 1, Rgb(90, 90, 90)); // pale
                    if (Mathf.Repeat(t, 0.8f) < 0.2f) FillRect(hx + 6, 51, 2, 1, Rgb(255, 40, 40));
                    // fascio del faro dell'elicottero
                    for (int y = 16; y < 52; y++) FillRect(hx + 7 - (52 - y) / 4, y, 1 + (52 - y) / 2, 1, Rgb(60, 64, 58));
                    break;
                }
                case 4:
                {
                    // la colonna di auto in fuga verso nord: fari bianchi e luci rosse su una strada buia
                    Fill(Rgb(10, 10, 14));
                    for (int y = 0; y < 40; y++) FillRect(48 - (40 - y), y, (40 - y) * 2, 1, Rgb(26, 26, 30)); // strada in prospettiva
                    for (int i = 0; i < 16; i++)
                    {
                        float d = Mathf.Repeat(i * 0.13f + t * 0.05f, 1f); // 0 vicino, 1 lontano
                        int y = (int)(d * 40f), half = (int)((1f - d) * 16f) + 1;
                        Set(48 - half, y, Rgb(255, 40, 30));
                        Set(48 - half + 2, y, Rgb(255, 40, 30));
                        Set(48 + half - 3, y, Rgb(250, 250, 220));
                        Set(48 + half - 1, y, Rgb(250, 250, 220));
                    }
                    FillRect(0, 40, W, H - 40, Rgb(22, 18, 26));
                    for (int x = 0; x < W; x += 5) FillRect(x, 40, 3, 2 + (x * 7) % 5, Rgb(14, 12, 16)); // alberi
                    break;
                }
                default:
                {
                    // segnale perso: barre colorate che tremano e neve
                    Color32[] bars = { Rgb(200, 200, 200), Rgb(200, 200, 0), Rgb(0, 200, 200), Rgb(0, 200, 0), Rgb(200, 0, 200), Rgb(200, 0, 0), Rgb(0, 0, 200) };
                    int jitter = Random.Range(-2, 3);
                    for (int i = 0; i < bars.Length; i++) FillRect(i * W / bars.Length + jitter, 18, W / bars.Length + 1, H - 18, bars[i]);
                    FillRect(0, 0, W, 18, Rgb(20, 20, 20));
                    for (int i = 0; i < 400; i++) { byte v = (byte)Random.Range(0, 255); Set(Random.Range(0, W), Random.Range(0, H), new Color32(v, v, v, 255)); }
                    break;
                }
            }
        }

        Vector2Int[] crackPixels;

        /// <summary>Crepe a ragnatela dal punto dove ha preso il colpo, con un angolo dello schermo morto.</summary>
        void DrawCracks(bool on)
        {
            if (crackPixels == null)
            {
                var list = new System.Collections.Generic.List<Vector2Int>();
                var rng = new System.Random(641);
                Vector2 hit = new Vector2(W * 0.68f, H * 0.62f);
                for (int arm = 0; arm < 11; arm++)
                {
                    float ang = arm / 11f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.4f;
                    Vector2 p = hit;
                    int len = 25 + rng.Next(50);
                    for (int i = 0; i < len; i++)
                    {
                        ang += ((float)rng.NextDouble() - 0.5f) * 0.5f;
                        p += new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        list.Add(new Vector2Int((int)p.x, (int)p.y));
                    }
                }
                // anelli spezzati attorno al punto d'urto
                foreach (float r in new[] { 5f, 11f })
                    for (float a = 0f; a < Mathf.PI * 2f; a += 0.08f)
                        if (rng.NextDouble() < 0.7) list.Add(new Vector2Int((int)(hit.x + Mathf.Cos(a) * r), (int)(hit.y + Mathf.Sin(a) * r)));
                crackPixels = list.ToArray();
            }
            // il centro dell'urto: vetro sbriciolato, scuro
            Circle(W * 0.68f, H * 0.62f, 3f, Rgb(30, 30, 30));
            foreach (var c in crackPixels)
            {
                Set(c.x, c.y, Rgb(18, 18, 18));
                if (on) Set(c.x + 1, c.y + 1, Rgb(200, 200, 200)); // il bordo della crepa riflette lo schermo
            }
            // un angolo morto (i fosfori non si accendono più)
            for (int y = H - 16; y < H; y++)
                for (int x = W - 24 + (H - y); x < W; x++) Set(x, y, Rgb(8, 8, 10));
        }

        void Scanlines()
        {
            for (int y = 0; y < H; y += 2)
                for (int x = 0; x < W; x++)
                {
                    Color32 c = pixels[y * W + x];
                    pixels[y * W + x] = new Color32((byte)(c.r * 0.7f), (byte)(c.g * 0.7f), (byte)(c.b * 0.7f), 255);
                }
        }

        Color Average()
        {
            long r = 0, g = 0, b = 0;
            for (int i = 0; i < pixels.Length; i += 7) { r += pixels[i].r; g += pixels[i].g; b += pixels[i].b; }
            float n = pixels.Length / 7f * 255f;
            return new Color(r / n, g / n, b / n);
        }
    }
}
