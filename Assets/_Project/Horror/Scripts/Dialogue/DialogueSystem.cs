using System;
using System.Collections.Generic;
using FantaLandia.Core;
using FantaLandia.Horror.Audio;
using UnityEngine;

namespace FantaLandia.Horror.Dialogue
{
    /// <summary>Chi parla: nome, colore del nome e tono della voce borbottata.</summary>
    [Serializable]
    public class Speaker
    {
        public string id;
        public string name;
        public Color color = Color.white;
        [Tooltip("Tono della voce (1 = normale, più alto = più acuto)")]
        public float pitch = 1f;

        public Speaker(string id, string name, Color color, float pitch)
        {
            this.id = id; this.name = name; this.color = color; this.pitch = pitch;
        }
    }

    /// <summary>I personaggi della storia.</summary>
    public static class Speakers
    {
        public static readonly Speaker Muark = new Speaker("muark", "Muark", new Color(0.55f, 0.85f, 1f), 1.0f);
        public static readonly Speaker Tom = new Speaker("tom", "Tom", new Color(1f, 0.75f, 0.35f), 0.82f);
        public static readonly Speaker Jef = new Speaker("jef", "Jef", new Color(0.7f, 1f, 0.6f), 0.7f);
        public static readonly Speaker Roy = new Speaker("roy", "Roy", new Color(1f, 0.5f, 0.5f), 1.18f);
        public static readonly Speaker NeighborMan = new Speaker("vicino", "Vicino", new Color(0.8f, 0.8f, 0.8f), 0.75f);
        public static readonly Speaker NeighborWoman = new Speaker("vicina", "Vicina", new Color(0.95f, 0.8f, 0.95f), 1.45f);
        public static readonly Speaker Tv = new Speaker("tv", "TV", new Color(0.7f, 0.8f, 1f), 0.9f);
        // vicini: a sinistra i Miller (anziani), a destra i Kowalski
        public static readonly Speaker Frank = new Speaker("frank", "Frank", new Color(0.8f, 0.75f, 0.6f), 0.72f);
        public static readonly Speaker Martha = new Speaker("martha", "Martha", new Color(0.95f, 0.75f, 0.85f), 1.35f);
        public static readonly Speaker Gary = new Speaker("gary", "Gary", new Color(0.75f, 0.85f, 0.7f), 0.66f);
        public static readonly Speaker Linda = new Speaker("linda", "Linda", new Color(0.6f, 0.9f, 0.9f), 1.5f);

        // capitolo 2: le voci della radio e il fattorino della pizza
        public static readonly Speaker Radio = new Speaker("radio", "Radio WMGR", new Color(0.85f, 0.85f, 0.7f), 0.9f);
        public static readonly Speaker Ham = new Speaker("radioamatore", "Radioamatore", new Color(0.8f, 0.7f, 0.55f), 0.8f);
        public static readonly Speaker Military = new Speaker("militare", "Militare", new Color(0.6f, 0.75f, 0.55f), 0.75f);
        public static readonly Speaker PizzaBoy = new Speaker("fattorino", "Fattorino", new Color(1f, 0.85f, 0.55f), 1.1f);

        static readonly Speaker[] All = { Muark, Tom, Jef, Roy, NeighborMan, NeighborWoman, Tv, Frank, Martha, Gary, Linda, Radio, Ham, Military, PizzaBoy };

        public static Speaker Find(string id)
        {
            foreach (var s in All) if (string.Equals(s.id, id, StringComparison.OrdinalIgnoreCase)) return s;
            return new Speaker(id, id, Color.white, 1f);
        }
    }

    /// <summary>
    /// Dialoghi con sottotitoli: testo che compare lettera per lettera con la voce borbottata,
    /// scelte multiple e frasi "di sottofondo" che non bloccano il gioco.
    ///
    /// Le conversazioni normali si fanno "camminando": il giocatore può muoversi e guardarsi intorno (ma non correre
    /// né abbassarsi), le battute vanno avanti da sole (Spazio per saltarle), le risposte si scelgono con 1/2/3.
    /// Se ci si allontana troppo da chi parla il discorso si interrompe ("Ehi, dove vai?") e riprende al ritorno.
    /// Le scene importanti possono essere "bloccanti" (.Blocking()): il giocatore sta fermo e guarda chi parla.
    ///
    /// Uso:
    ///   DialogueSystem.Conversation()
    ///       .Near(tom.transform, Speakers.Tom)
    ///       .Say(Speakers.Tom, "Ehi Muark!")
    ///       .Choice(new[] { "Ciao!", "Ho portato la birra." }, i => ..., 8f)   // 8 s per rispondere, poi Muark tace
    ///       .Do(() => ...)
    ///       .Start();
    /// </summary>
    public class DialogueSystem : MonoBehaviour
    {
        // ------------------------------------------------------------------ costruzione delle conversazioni

        public class Builder
        {
            internal readonly List<Step> steps = new List<Step>();
            internal Action onEnd;
            internal bool blocking;
            internal Transform anchor;
            internal Speaker anchorSpeaker;
            internal float radius = 6f;

            public Builder Say(Speaker who, string text) { steps.Add(new Step { speaker = who, text = FantaLandia.Horror.UI.Loc.T(text) }); return this; }

            /// <summary>
            /// Il giocatore sceglie una risposta (tasti 1/2/3); la callback riceve l'indice scelto.
            /// Con 'timeout' > 0, se non risponde entro quei secondi Muark resta zitto e la callback non viene chiamata.
            /// </summary>
            public Builder Choice(string[] options, Action<int> onChosen, float timeout = 0f, Action onSilence = null)
            {
                steps.Add(new Step { options = System.Array.ConvertAll(options, FantaLandia.Horror.UI.Loc.T), onChosen = onChosen, timeout = timeout, onSilence = onSilence });
                return this;
            }

            /// <summary>Esegue del codice a questo punto della conversazione.</summary>
            public Builder Do(Action action) { steps.Add(new Step { action = action }); return this; }

            public Builder OnEnd(Action action) { onEnd = action; return this; }

            /// <summary>Scena importante: il giocatore resta fermo e lo sguardo va su chi parla.</summary>
            public Builder Blocking() { blocking = true; return this; }

            /// <summary>Se il giocatore si allontana più di 'radius' da 'who' il discorso si ferma; 'speaker' lo richiama.</summary>
            public Builder Near(Transform who, Speaker speaker, float radius = 6f)
            {
                anchor = who; anchorSpeaker = speaker; this.radius = radius;
                return this;
            }

            public void Start() => Instance.Begin(this);
        }

        internal class Step
        {
            public Speaker speaker;
            public string text;
            public string[] options;
            public Action<int> onChosen;
            public float timeout;
            public Action onSilence; // se il tempo scade: di solito qualcuno risponde al silenzio di Muark
            public Action action;
        }

        public static Builder Conversation() => new Builder();

        /// <summary>Inserisce delle battute subito dopo quella corrente (es. le risposte a una scelta).</summary>
        public static void Insert(params (Speaker who, string text)[] lines)
        {
            var self = Instance;
            foreach (var l in lines) self.steps.Insert(self.insertCursor++, new Step { speaker = l.who, text = FantaLandia.Horror.UI.Loc.T(l.text) });
        }

        /// <summary>Una sillaba della voce di qualcuno (per muovere la bocca).</summary>
        public static event Action<Speaker> SyllableSpoken;
        /// <summary>Qualcuno inizia una battuta (anche di sottofondo): chi ascolta si gira a guardarlo.</summary>
        public static event Action<Speaker, string> LineStarted;
        public static event Action ConversationEnded;

        /// <summary>Chi sta parlando adesso nella conversazione (null se nessuno).</summary>
        public static Speaker CurrentSpeaker =>
            instance != null && instance.active && !instance.paused && instance.index >= 0 && instance.index < instance.steps.Count ? instance.steps[instance.index].speaker : null;

        /// <summary>True mentre una conversazione blocca il giocatore (scene importanti).</summary>
        public static bool IsBlocking => instance != null && instance.active && instance.blocking;

        /// <summary>True durante qualsiasi conversazione (anche camminando): niente corsa, niente accovacciarsi.</summary>
        public static bool IsTalking => instance != null && instance.active;

        /// <summary>
        /// Frase di sottofondo (non blocca): compare per qualche secondo nei sottotitoli.
        /// Con 'overhead' (chiacchiere dei personaggi, non rivolte a Muark) non va nei sottotitoli in basso:
        /// appare sopra la testa di chi parla, solo se è nell'inquadratura e abbastanza vicino.
        /// </summary>
        public static void Bark(Speaker who, string text, Vector3? position = null, bool overhead = false)
        {
            text = FantaLandia.Horror.UI.Loc.T(text);
            var self = Instance;
            AudioClip voice = self.realVoices ? VoiceFor(who, text) : null;
            float shownFor = Mathf.Max(2.5f + text.Length * 0.05f, voice != null ? voice.length + 1f : 0f);
            self.barks.Add(new BarkLine { speaker = who, text = text, until = Time.time + shownFor,
                                          overhead = overhead && position.HasValue, position = position ?? Vector3.zero });
            LineStarted?.Invoke(who, text);
            self.StartCoroutine(self.BarkVoice(who, text, position));
        }

        /// <summary>
        /// Solo il sottotitolo, senza voce (la voce la fa chi la chiama: per esempio la radio, con il suo altoparlante).
        /// Il testo si traduce come le altre battute.
        /// </summary>
        public static void Caption(Speaker who, string text, float seconds)
        {
            text = FantaLandia.Horror.UI.Loc.T(text);
            Instance.barks.Add(new BarkLine { speaker = who, text = text, until = Time.time + seconds, overhead = false, position = Vector3.zero });
            LineStarted?.Invoke(who, text);
        }

        // ------------------------------------------------------------------ stato

        static DialogueSystem instance;

        static DialogueSystem Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("Dialoghi").AddComponent<DialogueSystem>();
                return instance;
            }
        }

        [Tooltip("Lettere al secondo")]
        public float typingSpeed = 38f;
        public float voiceVolume = 0.55f;
        [Tooltip("Pausa dopo una battuta prima della successiva (secondi + per lettera)")]
        public float holdBase = 1.1f;
        public float holdPerChar = 0.035f;
        [Header("Voci vere")]
        [Tooltip("Usa le voci registrate in Resources/Voci (generate da Tools/Voci/genera_voci.py); senza, il borbottio")]
        public bool realVoices = true;
        public float realVoiceVolume = 1f;

        AudioSource voiceSource;
        AudioClip lineVoice;          // la voce della battuta in corso (null = borbottio)
        Speaker lineSpeaker;
        float mouthClock;
        readonly float[] rmsBuffer = new float[256];
        static readonly Dictionary<string, AudioClip> voiceCache = new Dictionary<string, AudioClip>();

        /// <summary>La voce registrata per questa battuta (Resources/Voci/&lt;id&gt;_&lt;md5 del testo&gt;), o null.</summary>
        public static AudioClip VoiceFor(Speaker who, string text)
        {
            if (who == null || string.IsNullOrEmpty(text)) return null;
            if (!FantaLandia.Horror.UI.GameSettings.SpokenVoices) return null; // voci spente: borbottio
            string key;
            string folder = FantaLandia.Horror.UI.Loc.VoiceFolder;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] h = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
                var sb = new System.Text.StringBuilder(who.id).Append('_');
                for (int i = 0; i < 5; i++) sb.Append(h[i].ToString("x2"));
                key = sb.ToString();
            }
            if (voiceCache.TryGetValue(folder + key, out AudioClip c)) return c;
            c = Resources.Load<AudioClip>(folder + key);
            voiceCache[folder + key] = c;
            return c;
        }

        List<Step> steps = new List<Step>();
        int index = -1;
        bool active, blocking, paused;
        Transform anchor;
        Speaker anchorSpeaker;
        float radius;
        Action onEnd;
        float visibleChars;
        int lastVoiceChar;
        int insertCursor;
        int startFrame;
        float lineDoneAt;   // quando la battuta è finita di scrivere
        float choiceStart;
        float pausedNagAt;

        class BarkLine { public Speaker speaker; public string text; public float until; public bool overhead; public Vector3 position; }
        readonly List<BarkLine> barks = new List<BarkLine>();

        GUIStyle nameStyle, textStyle, choiceStyle, barkStyle, hintStyle;

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            var go = new GameObject("Voce");
            go.transform.SetParent(transform, false);
            voiceSource = go.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.rolloffMode = AudioRolloffMode.Linear;
            voiceSource.minDistance = 2f;
            voiceSource.maxDistance = 30f;
            voiceSource.dopplerLevel = 0f;
        }

        /// <summary>Fa partire la voce vera della battuta (se c'è), dal punto in cui si trova chi parla.</summary>
        void PlayLineVoice(Step s)
        {
            if (voiceSource != null) voiceSource.Stop();
            lineSpeaker = s.speaker;
            lineVoice = realVoices && s.options == null ? VoiceFor(s.speaker, s.text) : null;
            if (lineVoice == null) return;
            Vector3? pos = SpeakerPosition(s.speaker);
            voiceSource.spatialBlend = pos.HasValue ? 0.85f : 0f; // un po' "in testa" anche da lontano: si capisce sempre
            if (pos.HasValue) voiceSource.transform.position = pos.Value;
            voiceSource.clip = lineVoice;
            voiceSource.volume = realVoiceVolume * FantaLandia.Horror.UI.GameSettings.Voices;
            voiceSource.pitch = 1f;
            voiceSource.Play();
        }

        /// <summary>La bocca segue il volume della voce vera (una "sillaba" quando la voce è forte).</summary>
        void UpdateVoiceMouth()
        {
            if (voiceSource == null || !voiceSource.isPlaying || lineSpeaker == null) return;
            Vector3? pos = SpeakerPosition(lineSpeaker);
            if (pos.HasValue) voiceSource.transform.position = pos.Value;
            if (Time.time < mouthClock) return;
            voiceSource.GetOutputData(rmsBuffer, 0);
            float sum = 0f;
            foreach (float x in rmsBuffer) sum += x * x;
            if (Mathf.Sqrt(sum / rmsBuffer.Length) > 0.02f)
            {
                SyllableSpoken?.Invoke(lineSpeaker);
                mouthClock = Time.time + UnityEngine.Random.Range(0.1f, 0.16f);
            }
        }

        void Begin(Builder b)
        {
            steps = b.steps;
            onEnd = b.onEnd;
            blocking = b.blocking;
            anchor = b.anchor;
            anchorSpeaker = b.anchorSpeaker;
            radius = b.radius;
            paused = false;
            active = true;
            index = -1;
            startFrame = Time.frameCount; // il tasto che ha aperto il dialogo non deve anche farlo avanzare
            Next();
        }

        void Next()
        {
            while (true)
            {
                index++;
                if (index >= steps.Count) { Finish(); return; }
                Step s = steps[index];
                if (s.action != null) { s.action(); continue; } // azioni: subito
                StartStep(s);
                return;
            }
        }

        void StartStep(Step s)
        {
            visibleChars = 0f;
            lastVoiceChar = 0;
            lineDoneAt = -1f;
            insertCursor = index + 1;
            choiceStart = Time.time;
            PlayLineVoice(s);
            if (s.options == null) LineStarted?.Invoke(s.speaker, s.text);
        }

        void Finish()
        {
            if (voiceSource != null) voiceSource.Stop();
            active = false;
            blocking = false;
            paused = false;
            steps = new List<Step>();
            Action end = onEnd;
            onEnd = null;
            ConversationEnded?.Invoke();
            end?.Invoke();
        }

        void Choose(int i)
        {
            Step s = steps[index];
            if (i < 0 || i >= s.options.Length) return;
            HorrorAudio.Play2D(ProceduralAudio.Click(), 0.4f);
            // la scelta viene detta da Muark
            steps.Insert(index + 1, new Step { speaker = Speakers.Muark, text = s.options[i] });
            insertCursor = index + 2; // le risposte vanno dopo la battuta di Muark
            s.onChosen?.Invoke(i);
            Next();
        }

        bool PlayerTooFar()
        {
            if (anchor == null || blocking) return false;
            Camera cam = Camera.main;
            if (cam == null) return false;
            Vector3 d = cam.transform.position - anchor.position;
            d.y *= 0.5f; // un piano di differenza conta, ma meno
            return d.magnitude > radius;
        }

        void Update()
        {
            barks.RemoveAll(b => Time.time > b.until);
            if (!active || index < 0 || index >= steps.Count) return;
            if (Time.frameCount == startFrame) return;

            // ci si è allontanati: il discorso si ferma e chi parlava richiama Muark; si riprende dalla stessa battuta
            if (!paused && PlayerTooFar())
            {
                paused = true;
                pausedNagAt = Time.time + 0.4f;
            }
            if (paused)
            {
                if (voiceSource != null && voiceSource.isPlaying) voiceSource.Stop();
                if (Time.time > pausedNagAt && anchorSpeaker != null)
                {
                    pausedNagAt = Time.time + 12f;
                    Bark(anchorSpeaker, Nag(), anchor != null ? anchor.position + Vector3.up * 1.6f : (Vector3?)null);
                }
                if (PlayerTooFar()) return;
                paused = false;
                StartStep(steps[index]); // ricomincia la battuta interrotta
                return;
            }

            Step s = steps[index];
            if (s.options != null)
            {
                int digit = GameInput.DigitPressed();
                if (digit > 0 && digit <= s.options.Length) { Choose(digit - 1); return; }
                if (s.timeout > 0f && Time.time - choiceStart > s.timeout)
                {
                    // nessuna risposta: Muark tace (le battute inserite qui vanno subito dopo)
                    insertCursor = index + 1;
                    s.onSilence?.Invoke();
                    Next();
                }
                return;
            }

            int len = s.text.Length;
            UpdateVoiceMouth();
            if (visibleChars < len)
            {
                if (lineVoice != null)
                {
                    // con la voce vera il testo scorre alla stessa velocità del parlato
                    float speed = len / Mathf.Max(0.4f, lineVoice.length * 0.85f);
                    visibleChars = Mathf.Min(len, visibleChars + speed * Time.deltaTime);
                }
                else
                {
                    visibleChars = Mathf.Min(len, visibleChars + typingSpeed * Time.deltaTime);
                    int shown = (int)visibleChars;
                    // una sillaba ogni 2 lettere (spazi e punteggiatura esclusi)
                    for (; lastVoiceChar < shown; lastVoiceChar++)
                    {
                        char c = s.text[lastVoiceChar];
                        if (char.IsLetter(c) && lastVoiceChar % 2 == 0) PlayVoiceSyllable(s.speaker, SpeakerPosition(s.speaker));
                    }
                }
                if (Skip()) { visibleChars = len; if (voiceSource != null) voiceSource.Stop(); } // completa subito
                return;
            }

            if (lineVoice != null && voiceSource != null && voiceSource.isPlaying)
            {
                if (Skip()) voiceSource.Stop();
                return; // si aspetta che finisca di parlare
            }
            if (lineDoneAt < 0f) lineDoneAt = Time.time;
            float hold = lineVoice != null ? 0.3f : holdBase + len * holdPerChar;
            if (Skip() || Time.time - lineDoneAt > hold) Next();
        }

        bool Skip() => blocking ? GameInput.AdvanceDialogue : GameInput.SkipLine;

        static readonly string[] Nags = { "Ehi, Muark! Dove vai?", "Muark? Ti sto parlando!", "Ehi! Torna qui, dai." };
        int nagIndex;
        string Nag() => Nags[nagIndex++ % Nags.Length];

        /// <summary>Le voci dei personaggi vengono da dove sono (Muark e la TV restano "in testa" se non si trovano).</summary>
        static Vector3? SpeakerPosition(Speaker who)
        {
            if (who == null || who.id == Speakers.Muark.id) return null;
            var npc = NpcSpeaker.Find(who.id);
            return npc != null ? npc.EyePoint : (Vector3?)null;
        }

        System.Collections.IEnumerator BarkVoice(Speaker who, string text, Vector3? position)
        {
            AudioClip clip = realVoices ? VoiceFor(who, text) : null;
            if (clip != null)
            {
                if (position.HasValue) HorrorAudio.PlayAt(clip, position.Value, realVoiceVolume, 1f, 30f, true);
                else HorrorAudio.Play2D(clip, realVoiceVolume, 1f, true);
                // la bocca si muove per tutta la frase
                for (float t = 0f; t < clip.length; t += 0.13f)
                {
                    SyllableSpoken?.Invoke(who);
                    yield return new WaitForSeconds(0.13f);
                }
                yield break;
            }
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsLetter(text[i]) && i % 2 == 0) PlayVoiceSyllable(who, position);
                yield return new WaitForSeconds(1f / typingSpeed);
            }
        }

        void PlayVoiceSyllable(Speaker who, Vector3? position)
        {
            if (who == null) return;
            SyllableSpoken?.Invoke(who);
            var clip = ProceduralAudio.Syllable(UnityEngine.Random.Range(0, ProceduralAudio.SyllableCount));
            float pitch = who.pitch * UnityEngine.Random.Range(0.94f, 1.08f);
            if (position.HasValue) HorrorAudio.PlayAt(clip, position.Value, voiceVolume, pitch, 30f, true);
            else HorrorAudio.Play2D(clip, voiceVolume, pitch, true);
        }

        // ------------------------------------------------------------------ interfaccia

        float styleScale = -1f;

        void EnsureStyles()
        {
            float sc = FantaLandia.Horror.UI.GameSettings.TextScale;
            if (textStyle != null && Mathf.Approximately(sc, styleScale)) return;
            styleScale = sc;
            Font hand = FantaLandia.Horror.UI.UiStyle.Hand;
            nameStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(23 * sc), richText = true, font = hand };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * sc), wordWrap = true, richText = true, font = hand };
            choiceStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(23 * sc), alignment = TextAnchor.MiddleLeft, richText = true, font = hand };
            barkStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(21 * sc), alignment = TextAnchor.MiddleCenter, richText = true, font = hand };
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, richText = true, font = hand };
        }

        void OnGUI()
        {
            GUI.depth = -10; // sopra al resto
            EnsureStyles();
            float sc = FantaLandia.Horror.UI.GameSettings.TextScale;
            float w = Mathf.Min(860f, Screen.width - 80f);
            float x = (Screen.width - w) * 0.5f;
            bool showing = active && !paused && index >= 0 && index < steps.Count;

            // frasi di sottofondo (sopra i sottotitoli, con un'ombra morbida)
            DrawOverheadBarks();
            float by = Screen.height - (showing ? 190f * sc : 80f);
            for (int i = barks.Count - 1; i >= 0; i--)
            {
                var b = barks[i];
                if (b.overhead || !FantaLandia.Horror.UI.GameSettings.Subtitles) continue;
                float a = Mathf.Clamp01((b.until - Time.time) * 1.5f);
                string bhex = ColorUtility.ToHtmlStringRGB(b.speaker.color);
                Rect r = new Rect(x, by, w, 30f * sc);
                FantaLandia.Horror.UI.UiStyle.Shadowed(r, $"<color=#{bhex}>{FantaLandia.Horror.UI.Loc.T(b.speaker.name)}</color>  {b.text}", barkStyle, new Color(1f, 1f, 1f, a));
                by -= 30f * sc;
            }

            if (!showing || FantaLandia.Horror.UI.NotebookMenu.IsOpen) return;
            Step s = steps[index];

            if (s.options != null)
            {
                // scelte: una "carta" scura per risposta, con il tasto numerico disegnato
                float h = 44f * sc, gap = 8f;
                float cw = Mathf.Min(w, 720f);
                float cx = (Screen.width - cw) * 0.5f;
                float total = s.options.Length * (h + gap);
                float y = Screen.height - 40f - total;
                if (s.timeout > 0f)
                {
                    float left = 1f - Mathf.Clamp01((Time.time - choiceStart) / s.timeout);
                    FantaLandia.Horror.UI.UiStyle.Panel(new Rect(cx + 8f, y - 12f, cw - 16f, 4f), new Color(1f, 1f, 1f, 0.12f), 2);
                    FantaLandia.Horror.UI.UiStyle.Panel(new Rect(cx + 8f, y - 12f, (cw - 16f) * left, 4f), new Color(0.93f, 0.8f, 0.52f, 0.85f), 2);
                }
                for (int i = 0; i < s.options.Length; i++)
                {
                    Rect card = new Rect(cx, y + i * (h + gap), cw, h);
                    FantaLandia.Horror.UI.UiStyle.Panel(card, new Color(0.04f, 0.05f, 0.08f, 0.72f), 12);
                    float kw = FantaLandia.Horror.UI.UiStyle.KeyCap(card.x + 12f, card.y + (h - 28f) * 0.5f, (i + 1).ToString(), 28f);
                    FantaLandia.Horror.UI.UiStyle.Shadowed(new Rect(card.x + 24f + kw, card.y, card.width - kw - 36f, h), s.options[i], choiceStyle, new Color(0.97f, 0.95f, 0.9f));
                }
                return;
            }

            if (!FantaLandia.Horror.UI.GameSettings.Subtitles) return; // senza sottotitoli restano solo le scelte
            // sottotitolo: pannello scuro arrotondato, barretta del colore di chi parla, nome e testo a mano
            float boxH = 112f * sc;
            Rect box = new Rect(x - 20f, Screen.height - boxH - 28f, w + 40f, boxH);
            FantaLandia.Horror.UI.UiStyle.Panel(box, new Color(0.03f, 0.035f, 0.06f, 0.66f), 16);
            Color sp = s.speaker != null ? s.speaker.color : Color.white;
            FantaLandia.Horror.UI.UiStyle.Panel(new Rect(box.x + 12f, box.y + 14f, 4f, box.height - 28f), new Color(sp.r, sp.g, sp.b, 0.9f), 2);
            if (s.speaker != null)
                FantaLandia.Horror.UI.UiStyle.Shadowed(new Rect(box.x + 28f, box.y + 8f, w, 32f * sc), $"<color=#{ColorUtility.ToHtmlStringRGB(sp)}>{FantaLandia.Horror.UI.Loc.T(s.speaker.name)}</color>", nameStyle, Color.white);
            string shown = s.text.Substring(0, Mathf.Clamp((int)visibleChars, 0, s.text.Length));
            FantaLandia.Horror.UI.UiStyle.Shadowed(new Rect(box.x + 28f, box.y + 8f + 30f * sc, w - 20f, boxH - 40f * sc), shown, textStyle, new Color(0.97f, 0.95f, 0.9f));
            if (!blocking)
            {
                float kx = box.xMax - 112f, ky = box.yMax - 30f;
                float kw = FantaLandia.Horror.UI.UiStyle.KeyCap(kx, ky, FantaLandia.Horror.UI.Loc.T("Spazio"), 20f, 0.55f);
                var c = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                GUI.Label(new Rect(kx + kw + 6f, ky - 1f, 60f, 22f), FantaLandia.Horror.UI.Loc.T("salta"), hintStyle);
                GUI.color = c;
            }
            else if (visibleChars >= s.text.Length && Mathf.Repeat(Time.time, 1f) < 0.6f)
                FantaLandia.Horror.UI.UiStyle.Panel(new Rect(box.xMax - 30f, box.yMax - 24f, 10f, 10f), new Color(0.93f, 0.8f, 0.52f, 0.9f), 5);
        }

        GUIStyle overheadStyle;
        const float MaxOverheadDistance = 20f;

        void DrawOverheadBarks()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            if (overheadStyle == null) overheadStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true, font = FantaLandia.Horror.UI.UiStyle.Hand };
            foreach (var b in barks)
            {
                if (!b.overhead) continue;
                Vector3 world = b.position + Vector3.up * 0.45f;
                Vector3 sp = cam.WorldToScreenPoint(world);
                float dist = Vector3.Distance(cam.transform.position, world);
                if (sp.z <= 0f || dist > MaxOverheadDistance) continue;
                if (sp.x < 0f || sp.x > Screen.width || sp.y < 0f || sp.y > Screen.height) continue;
                // solo se chi parla si vede davvero: niente scritte attraverso muri, porte e mobili
                // (il raggio ignora il giocatore e i personaggi, layer 2 e 7)
                if (Physics.Linecast(cam.transform.position, b.position, ~((1 << 2) | (1 << 7)), QueryTriggerInteraction.Ignore)) continue;
                float alpha = Mathf.Clamp01((MaxOverheadDistance - dist) / 5f) * Mathf.Clamp01((b.until - Time.time) * 2f);
                string hex = ColorUtility.ToHtmlStringRGB(b.speaker.color);
                string line = $"<color=#{hex}>{FantaLandia.Horror.UI.Loc.T(b.speaker.name)}</color>  {b.text}";
                Vector2 size = overheadStyle.CalcSize(new GUIContent(System.Text.RegularExpressions.Regex.Replace(line, "<.*?>", "")));
                float bw = Mathf.Min(size.x + 24f, 340f);
                float bh = overheadStyle.CalcHeight(new GUIContent(b.text), bw - 24f) + 10f;
                Rect r = new Rect(sp.x - bw * 0.5f, Screen.height - sp.y - bh, bw, bh);
                FantaLandia.Horror.UI.UiStyle.Panel(r, new Color(0.03f, 0.035f, 0.06f, 0.5f * alpha), 10);
                FantaLandia.Horror.UI.UiStyle.Shadowed(new Rect(r.x + 12f, r.y, r.width - 24f, r.height), line, overheadStyle, new Color(1f, 1f, 1f, alpha));
            }
        }

        static void DrawShadowed(Rect r, string text, GUIStyle style)
        {
            var c = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f * c.a);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), StripColors(text), style);
            GUI.color = c;
            GUI.Label(r, text, style);
        }

        static string StripColors(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");
    }
}
