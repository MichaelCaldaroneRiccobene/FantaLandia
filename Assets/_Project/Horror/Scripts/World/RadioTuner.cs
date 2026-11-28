using System;
using System.Collections.Generic;
using FantaLandia.Core;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Dialogue;
using FantaLandia.Horror.Interaction;
using FantaLandia.Horror.Player;
using FantaLandia.Horror.UI;
using UnityEngine;

namespace FantaLandia.Horror.World
{
    /// <summary>
    /// La vecchia radio della cantina (capitolo 2). Con E si prende in mano: la manopola si gira con il mouse
    /// (o la rotellina, o A/D) e la lancetta scorre sulla scala FM. Tra una stazione e l'altra c'è solo neve,
    /// con qualche brandello di voce; avvicinandosi a una stazione la neve cala e la voce si sente sempre meglio.
    ///
    /// Le stazioni: la radio locale (il notiziario che si ripete, poi si interrompe), un radioamatore terrorizzato,
    /// un messaggio militare in codice che si ripete, e una stazione di musica rotta che non serve a niente.
    /// La voce va avanti solo finché si resta sintonizzati: se si sposta la manopola si mette in pausa nella neve.
    /// Quando una stazione ha detto tutto conta come "ascoltata" (StationHeard).
    /// </summary>
    public class RadioTuner : Interactable
    {
        public const float MinFreq = 88f, MaxFreq = 108f;

        [Tooltip("Frequenza di partenza della lancetta")]
        public float frequency = 89.4f;
        [Tooltip("Quanto deve essere vicina la lancetta (MHz) per sentire la stazione")]
        public float bandwidth = 0.55f;
        [Range(0f, 1f)] public float volume = 0.9f;
        [Tooltip("Si può prendere in mano (la regia la blocca durante le scene)")]
        public bool usable = true;

        /// <summary>Una stazione ha finito di parlare (id: locale, radioamatore, militare).</summary>
        public event Action<string> StationHeard;

        public bool InUse { get; private set; }
        public int HeardCount => heard.Count;
        public bool Heard(string id) => heard.Contains(id);

        struct Line
        {
            public Speaker who; public string text;
            public Line(Speaker who, string text) { this.who = who; this.text = text; }
        }

        class Station
        {
            public string id;
            public float freq;
            public bool music;       // musica rotta, niente voci
            public bool cutsOff;     // dopo l'ultima battuta solo aria morta
            public Line[] lines;
            public int index;        // battuta in corso
            public float lineClock;  // da quanto va la battuta (solo quando sintonizzati)
            public float lineLength;
            public bool started, dead, done;
            public AudioSource source;
        }

        static Line L(Speaker who, string text) => new Line(who, text);

        // le battute delle stazioni (le voci le genera Tools/Voci/genera_voci.py)
        static Line[] LocalNews() => new[]
        {
            L(Speakers.Radio, "...WMGR, novantasei e sette. Edizione straordinaria."),
            L(Speakers.Radio, "Ripetiamo: le autorità chiedono a tutti i residenti di Maple Grove di restare in casa."),
            L(Speakers.Radio, "Non uscite. Non usate le auto. Le strade verso est sono chiuse."),
            L(Speakers.Radio, "Vi terremo aggiornati appena... un momento. Cos'è questo rumore? Cosa... Oh mio Dio..."),
        };

        static Line[] HamOperator() => new[]
        {
            L(Speakers.Ham, "...qualcuno mi sente? Qui radioamatore, Maple Grove nord. Qualcuno mi sente?"),
            L(Speakers.Ham, "L'ho visto dalla finestra. Vicino alla vecchia centrale. Una cosa alta come un palazzo."),
            L(Speakers.Ham, "Si muove. Cammina. Giuro su Dio, cammina."),
            L(Speakers.Ham, "E dalle fogne escono cose più piccole. Tante. Corrono a quattro zampe."),
            L(Speakers.Ham, "Se mi sentite, spegnete le luci. Non fate rumore. Non..."),
        };

        static Line[] MilitaryCode() => new[]
        {
            L(Speakers.Military, "...a tutte le unità, qui Comando Nord. Codice Martello Sette. Ripeto: Martello Sette."),
            L(Speakers.Military, "Settore Maple Grove: perimetro compromesso."),
            L(Speakers.Military, "Evacuazione dei civili verso nord, Route 9. Punto di raccolta: Fort Jackson."),
            L(Speakers.Military, "Fine messaggio. Il messaggio si ripete."),
        };

        readonly List<Station> stations = new List<Station>();
        readonly HashSet<string> heard = new HashSet<string>();
        AudioSource noise, music;
        FirstPersonController fpc;
        Interactor user;
        int openedFrame;
        float fragmentAt;
        float savedYaw, savedPitch;

        void Awake()
        {
            stations.Add(new Station { id = "musica", freq = 91.3f, music = true, lines = new Line[0] });
            stations.Add(new Station { id = "locale", freq = 96.7f, cutsOff = true, lines = LocalNews() });
            stations.Add(new Station { id = "radioamatore", freq = 101.9f, lines = HamOperator() });
            stations.Add(new Station { id = "militare", freq = 105.5f, lines = MilitaryCode() });
            foreach (var st in stations)
            {
                st.source = gameObject.AddComponent<AudioSource>();
                Setup3D(st.source);
            }
            noise = HorrorAudio.CreateLoop(transform, ProceduralAudio.TvStatic(), 0f, true, 9f);
            music = HorrorAudio.CreateLoop(transform, ProceduralAudio.CartoonMusic(), 0f, true, 9f);
        }

        static void Setup3D(AudioSource s)
        {
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 1f;
            s.maxDistance = 10f;
            // un altoparlante piccolo e vecchio: niente bassi, niente alti
            var hp = s.gameObject.GetComponent<AudioHighPassFilter>();
            if (hp == null) { hp = s.gameObject.AddComponent<AudioHighPassFilter>(); hp.cutoffFrequency = 380f; }
            var lp = s.gameObject.GetComponent<AudioLowPassFilter>();
            if (lp == null) { lp = s.gameObject.AddComponent<AudioLowPassFilter>(); lp.cutoffFrequency = 3600f; }
        }

        /// <summary>Accende la radio (neve) anche senza prenderla in mano: per esempio quando la sintonizza Tom.</summary>
        public bool PoweredOn { get; set; } = true;

        // ------------------------------------------------------------------ interazione

        public override string Prompt(Interactor who) => "Prendi la radio e sintonizzala";

        public override bool CanInteract(Interactor who) => enabled && usable && !InUse && !DialogueSystem.IsBlocking && Time.frameCount > closedFrame + 2;

        int closedFrame = -10;

        public override void Interact(Interactor who)
        {
            user = who;
            fpc = who.GetComponent<FirstPersonController>();
            InUse = true;
            openedFrame = Time.frameCount;
            if (fpc != null) { fpc.inputLocked = true; savedYaw = fpc.Yaw; savedPitch = fpc.Pitch; }
            who.interactionEnabled = false;
            HorrorAudio.PlayAt(ProceduralAudio.Click(), transform.position, 0.6f, 0.9f);
        }

        /// <summary>Si posa la radio (E, Esc, o la regia che interrompe).</summary>
        public void PutDown()
        {
            if (!InUse) return;
            InUse = false;
            closedFrame = Time.frameCount;
            if (fpc != null) fpc.inputLocked = false;
            if (user != null) user.interactionEnabled = true;
            user = null;
        }

        /// <summary>Muove la lancetta da programma (Tom che gira la manopola), in 'seconds' secondi.</summary>
        public System.Collections.IEnumerator TuneTo(float freq, float seconds)
        {
            float f0 = frequency;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                // a scatti, come una mano vera
                float e = t * t * (3f - 2f * t);
                frequency = Mathf.Lerp(f0, freq, e) + Mathf.Sin(t * 37f) * 0.08f * (1f - t);
                yield return null;
            }
            frequency = freq;
        }

        /// <summary>Sostituisce le battute di una stazione (Tom trova un nuovo messaggio militare).</summary>
        public void SetMilitaryMessage(params (Speaker who, string text)[] lines)
        {
            foreach (var st in stations)
            {
                // le altre stazioni ormai tacciono (girando la manopola si sente solo neve)
                if (st.id != "militare") { if (!st.music) { st.dead = true; st.source.Stop(); } continue; }
                st.lines = new Line[lines.Length];
                for (int i = 0; i < lines.Length; i++) st.lines[i] = new Line(lines[i].who, lines[i].text);
                st.index = 0; st.lineClock = 0f; st.started = false; st.dead = false; st.done = false; st.cutsOff = true;
                st.source.Stop();
            }
        }

        /// <summary>La stazione 'id' ha finito tutte le battute (anche ripetuta).</summary>
        public bool Finished(string id)
        {
            foreach (var st in stations) if (st.id == id) return st.dead || st.done;
            return false;
        }

        /// <summary>Taglia di colpo il segnale: un ronzio forte e poi solo neve (fine del messaggio militare).</summary>
        public void KillSignal()
        {
            foreach (var st in stations) { st.source.Stop(); st.dead = true; }
            HorrorAudio.PlayAt(ProceduralAudio.Buzz(), transform.position, 0.8f, 1f, 12f);
        }

        // ------------------------------------------------------------------ aggiornamento

        void Update()
        {
            if (InUse)
            {
                float turn = GameInput.MouseDelta.x * 0.012f * GameSettings.Sensitivity + GameInput.Scroll * 0.2f;
                if (GameInput.PanAxis.x != 0f) turn += GameInput.PanAxis.x * Time.deltaTime * 1.2f;
                frequency = Mathf.Clamp(frequency + turn, MinFreq, MaxFreq);
                if (fpc != null) fpc.SetLook(savedYaw, Mathf.Lerp(fpc.Pitch, Mathf.Max(savedPitch, 25f), Time.deltaTime * 4f));
                if (Time.frameCount > openedFrame + 1 && (GameInput.Interact || GameInput.MenuToggle)) PutDown();
                if (NotebookMenu.IsOpen) PutDown();
            }

            float best = 0f;
            float fx = GameSettings.Effects;
            foreach (var st in stations)
            {
                float q = Quality(st);
                best = Mathf.Max(best, q);
                if (st.music)
                {
                    // musica che va e viene, a pezzi
                    float drop = Mathf.PerlinNoise(Time.time * 1.7f, 0.3f) > 0.35f ? 1f : 0.1f;
                    music.volume = PoweredOn ? q * drop * volume * 0.5f * GameSettings.Music : 0f;
                    music.pitch = 0.82f + Mathf.PerlinNoise(Time.time * 0.4f, 5f) * 0.05f;
                    if (music.volume > 0.001f && !music.isPlaying) music.Play();
                    if (music.volume <= 0.001f && music.isPlaying) music.Pause();
                    continue;
                }
                StepStation(st, q);
            }

            // la neve: piena tra le stazioni, quasi sparita quando si è sintonizzati
            float wantNoise = PoweredOn ? volume * (0.55f - 0.48f * best) * 0.3f * fx : 0f; // la neve: -70%
            noise.volume = Mathf.MoveTowards(noise.volume, wantNoise, Time.deltaTime * 2f);
            if (noise.volume > 0.001f && !noise.isPlaying) noise.Play();
            if (noise.volume <= 0.001f && noise.isPlaying) noise.Stop();

            // brandelli di voce nella neve, quando si gira la manopola lontano dalle stazioni
            if (PoweredOn && InUse && best < 0.2f && Time.time > fragmentAt)
            {
                fragmentAt = Time.time + UnityEngine.Random.Range(1.2f, 3f);
                int n = UnityEngine.Random.Range(2, 5);
                for (int i = 0; i < n; i++)
                    HorrorAudio.PlayAt(ProceduralAudio.Syllable(UnityEngine.Random.Range(0, ProceduralAudio.SyllableCount)),
                                       transform.position, 0.18f, UnityEngine.Random.Range(0.7f, 1.1f), 8f);
            }
        }

        float Quality(Station st)
        {
            float d = Mathf.Abs(frequency - st.freq);
            return Mathf.Clamp01(1f - d / bandwidth);
        }

        void StepStation(Station st, float q)
        {
            AudioSource src = st.source;
            if (!PoweredOn || st.dead || st.lines.Length == 0) { if (src.isPlaying) src.Pause(); return; }
            bool tuned = q > 0.3f;
            src.volume = q * q * volume * GameSettings.Voices;
            if (!tuned)
            {
                if (src.isPlaying) src.Pause();
                return;
            }
            if (!st.started)
            {
                st.started = true;
                StartLine(st);
                return;
            }
            if (st.source.clip != null && !src.isPlaying && st.lineClock < st.lineLength - 0.05f) src.UnPause();
            st.lineClock += Time.deltaTime;
            if (st.lineClock < st.lineLength + 0.6f) return;

            // battuta finita: la prossima
            st.index++;
            if (st.index >= st.lines.Length)
            {
                if (!heard.Contains(st.id)) { heard.Add(st.id); StationHeard?.Invoke(st.id); }
                st.done = true;
                if (st.cutsOff)
                {
                    // la radio locale si interrompe: un clic, poi aria morta
                    st.dead = true;
                    HorrorAudio.PlayAt(ProceduralAudio.Click(), transform.position, 0.5f, 0.6f, 8f);
                    return;
                }
                st.index = 0; // il messaggio si ripete
            }
            StartLine(st);
        }

        void StartLine(Station st)
        {
            Line line = st.lines[st.index];
            string text = Loc.T(line.text);
            AudioClip clip = DialogueSystem.VoiceFor(line.who, text);
            st.lineClock = 0f;
            st.lineLength = clip != null ? clip.length : 1.4f + text.Length * 0.055f;
            st.source.Stop();
            st.source.clip = clip;
            if (clip != null) st.source.Play();
            // il sottotitolo solo se si sente abbastanza bene (e se Muark è lì vicino)
            if (Camera.main == null || Vector3.Distance(Camera.main.transform.position, transform.position) < 7f)
                DialogueSystem.Caption(line.who, line.text, st.lineLength + 0.8f);
        }

        // ------------------------------------------------------------------ la manopola (a schermo, mentre la si tiene in mano)

        void OnGUI()
        {
            GUI.depth = -15; // la manopola: sopra la scena e sotto ai sottotitoli (sempre sopra all'immagine dell'effetto retrò, che sta a 100)
            if (!InUse) return;
            float s = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f);
            float w = 620f * s, h = 150f * s;
            var r = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 150f * s, w, h);
            UiStyle.PanelWithShadow(r, new Color(0.24f, 0.15f, 0.09f, 0.96f), 14);          // il legno della radio
            var face = new Rect(r.x + 18f * s, r.y + 16f * s, r.width - 36f * s, 74f * s);
            UiStyle.Panel(face, new Color(0.93f, 0.84f, 0.6f, 1f), 8);                       // la scala illuminata
            var tick = UiStyle.Text(Mathf.RoundToInt(15 * s), TextAnchor.UpperCenter, false);
            var old = GUI.color;
            for (float f = MinFreq; f <= MaxFreq + 0.01f; f += 0.5f)
            {
                float x = face.x + 14f * s + (f - MinFreq) / (MaxFreq - MinFreq) * (face.width - 28f * s);
                bool major = Mathf.Abs(f - Mathf.Round(f / 2f) * 2f) < 0.01f;
                GUI.color = new Color(0.25f, 0.18f, 0.12f, 1f);
                GUI.DrawTexture(new Rect(x - 0.5f * s, face.y + 6f * s, 1.2f * s, (major ? 16f : 8f) * s), Texture2D.whiteTexture);
                if (major) UiStyle.Shadowed(new Rect(x - 20f * s, face.y + 24f * s, 40f * s, 20f * s), ((int)f).ToString(), tick, new Color(0.25f, 0.18f, 0.12f), 0f);
            }
            // la lancetta rossa
            float nx = face.x + 14f * s + (frequency - MinFreq) / (MaxFreq - MinFreq) * (face.width - 28f * s);
            GUI.color = new Color(0.8f, 0.1f, 0.08f, 1f);
            GUI.DrawTexture(new Rect(nx - 1.5f * s, face.y + 2f * s, 3f * s, face.height - 4f * s), Texture2D.whiteTexture);
            GUI.color = old;
            // lucina del segnale: più è accesa, meglio si sente
            float best = 0f;
            foreach (var st in stations) best = Mathf.Max(best, Quality(st));
            UiStyle.Dot(new Vector2(r.xMax - 40f * s, r.yMax - 30f * s), 16f * s, Color.Lerp(new Color(0.25f, 0.1f, 0.05f), new Color(1f, 0.75f, 0.2f), best));
            var label = UiStyle.Text(Mathf.RoundToInt(20 * s), TextAnchor.MiddleLeft, false);
            UiStyle.Shadowed(new Rect(r.x + 22f * s, r.yMax - 48f * s, 200f * s, 36f * s), "FM " + frequency.ToString("0.0"), label, UiStyle.Accent);
            var help = UiStyle.Text(Mathf.RoundToInt(16 * s), TextAnchor.MiddleRight, false);
            UiStyle.Shadowed(new Rect(r.x, r.yMax - 48f * s, r.width - 70f * s, 36f * s),
                             Loc.T("Muovi il mouse per girare la manopola · E per posare la radio"), help, new Color(1f, 1f, 1f, 0.85f));
        }
    }
}
