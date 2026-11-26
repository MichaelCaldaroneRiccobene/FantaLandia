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
        [Tooltip("Volume del suono della TV (cartoni, neve)")]
        [Range(0f, 1f)] public float volume = 0.042f;
        [Tooltip("Volume delle voci del cartone (basse: è la TV accesa in sottofondo)")]
        [Range(0f, 1f)] public float cartoonVoiceVolume = 0.3f;

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
                sound.volume = Mathf.MoveTowards(sound.volume, cartoon && cartoonVoice != null && cartoonVoice.isPlaying ? volume * 0.4f : volume, Time.deltaTime * 0.5f);

            if (Time.time < nextFrame) return;
            nextFrame = Time.time + 1f / 12f;

            if (!on) Fill(new Color32(6, 8, 8, 255));
            else if (channel == TvChannel.Cartoon) CartoonRenderer.Draw(pixels, W, H, scene, episodeClock - sceneStart, episodeClock < speakingUntil ? speaker : null, cartoonGlow);
            else if (channel == TvChannel.Static) DrawStatic();
            else DrawNews(Time.time);

            if (on) Scanlines();
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
                string text = b.Substring(bar + 1);
                AudioClip clip = FantaLandia.Horror.Dialogue.DialogueSystem.VoiceFor(FantaLandia.Horror.Dialogue.Speakers.Find(speaker), text);
                float len = clip != null ? clip.length : 1.5f + text.Length * 0.06f;
                if (clip != null && cartoonVoice != null)
                {
                    cartoonVoice.clip = clip;
                    cartoonVoice.volume = cartoonVoiceVolume;
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
