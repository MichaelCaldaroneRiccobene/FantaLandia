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

        static readonly Speaker[] All = { Muark, Tom, Jef, Roy, NeighborMan, NeighborWoman, Tv, Frank, Martha, Gary, Linda };

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
    /// Uso:
    ///   DialogueSystem.Conversation()
    ///       .Say(Speakers.Tom, "Ehi Muark!")
    ///       .Choice(new[] { "Ciao!", "Ho portato la birra." }, i => ...)
    ///       .Do(() => ...)
    ///       .Start();
    /// </summary>
    public class DialogueSystem : MonoBehaviour
    {
        // ------------------------------------------------------------------ costruzione delle conversazioni

        public class Builder
        {
            internal readonly List<Step> steps = new List<Step>();
            Action onEnd;

            public Builder Say(Speaker who, string text) { steps.Add(new Step { speaker = who, text = text }); return this; }

            /// <summary>Il giocatore sceglie una risposta; la callback riceve l'indice scelto.</summary>
            public Builder Choice(string[] options, Action<int> onChosen)
            {
                steps.Add(new Step { options = options, onChosen = onChosen });
                return this;
            }

            /// <summary>Esegue del codice a questo punto della conversazione.</summary>
            public Builder Do(Action action) { steps.Add(new Step { action = action }); return this; }

            public Builder OnEnd(Action action) { onEnd = action; return this; }

            public void Start() => Instance.Begin(steps, onEnd);
        }

        internal class Step
        {
            public Speaker speaker;
            public string text;
            public string[] options;
            public Action<int> onChosen;
            public Action action;
        }

        public static Builder Conversation() => new Builder();

        /// <summary>Inserisce delle battute subito dopo quella corrente (es. le risposte a una scelta).</summary>
        public static void Insert(params (Speaker who, string text)[] lines)
        {
            var self = Instance;
            foreach (var l in lines) self.steps.Insert(self.insertCursor++, new Step { speaker = l.who, text = l.text });
        }

        /// <summary>Una sillaba della voce di qualcuno (per muovere la bocca).</summary>
        public static event Action<Speaker> SyllableSpoken;
        /// <summary>Qualcuno inizia una battuta (anche di sottofondo): chi ascolta si gira a guardarlo.</summary>
        public static event Action<Speaker, string> LineStarted;
        public static event Action ConversationEnded;

        /// <summary>Chi sta parlando adesso nella conversazione (null se nessuno).</summary>
        public static Speaker CurrentSpeaker =>
            instance != null && instance.active && instance.index >= 0 && instance.index < instance.steps.Count ? instance.steps[instance.index].speaker : null;

        /// <summary>True mentre una conversazione blocca il giocatore.</summary>
        public static bool IsBlocking => instance != null && instance.active;

        /// <summary>
        /// Frase di sottofondo (non blocca): compare per qualche secondo nei sottotitoli.
        /// Con 'overhead' (chiacchiere dei personaggi, non rivolte a Muark) non va nei sottotitoli in basso:
        /// appare sopra la testa di chi parla, solo se è nell'inquadratura e abbastanza vicino.
        /// </summary>
        public static void Bark(Speaker who, string text, Vector3? position = null, bool overhead = false)
        {
            var self = Instance;
            self.barks.Add(new BarkLine { speaker = who, text = text, until = Time.time + 2.5f + text.Length * 0.05f,
                                          overhead = overhead && position.HasValue, position = position ?? Vector3.zero });
            LineStarted?.Invoke(who, text);
            self.StartCoroutine(self.BarkVoice(who, text, position));
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

        List<Step> steps = new List<Step>();
        int index = -1;
        bool active;
        Action onEnd;
        float visibleChars;
        int lastVoiceChar;
        int hoveredChoice = -1;
        int insertCursor;
        int startFrame;

        class BarkLine { public Speaker speaker; public string text; public float until; public bool overhead; public Vector3 position; }
        readonly List<BarkLine> barks = new List<BarkLine>();

        GUIStyle nameStyle, textStyle, choiceStyle, barkStyle;

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        void Begin(List<Step> newSteps, Action end)
        {
            steps = newSteps;
            onEnd = end;
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
                visibleChars = 0f;
                lastVoiceChar = 0;
                insertCursor = index + 1;
                hoveredChoice = -1;
                Cursor.lockState = s.options != null ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = s.options != null;
                if (s.options == null) LineStarted?.Invoke(s.speaker, s.text);
                return;
            }
        }

        void Finish()
        {
            active = false;
            steps = new List<Step>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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

        void Update()
        {
            barks.RemoveAll(b => Time.time > b.until);
            if (!active || index < 0 || index >= steps.Count) return;
            if (Time.frameCount == startFrame) return;

            Step s = steps[index];
            if (s.options != null)
            {
                int digit = GameInput.DigitPressed();
                if (digit > 0 && digit <= s.options.Length) Choose(digit - 1);
                return;
            }

            int len = s.text.Length;
            if (visibleChars < len)
            {
                visibleChars = Mathf.Min(len, visibleChars + typingSpeed * Time.deltaTime);
                int shown = (int)visibleChars;
                // una sillaba ogni 2 lettere (spazi e punteggiatura esclusi)
                for (; lastVoiceChar < shown; lastVoiceChar++)
                {
                    char c = s.text[lastVoiceChar];
                    if (char.IsLetter(c) && lastVoiceChar % 2 == 0) PlayVoiceSyllable(s.speaker, null);
                }
                if (GameInput.AdvanceDialogue) visibleChars = len; // completa subito
                return;
            }

            if (GameInput.AdvanceDialogue) Next();
        }

        System.Collections.IEnumerator BarkVoice(Speaker who, string text, Vector3? position)
        {
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
            if (position.HasValue) HorrorAudio.PlayAt(clip, position.Value, voiceVolume, pitch, 15f);
            else HorrorAudio.Play2D(clip, voiceVolume, pitch);
        }

        // ------------------------------------------------------------------ interfaccia

        void EnsureStyles()
        {
            if (textStyle != null) return;
            nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, richText = true };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true, richText = true };
            choiceStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(16, 16, 8, 8) };
            barkStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, richText = true };
        }

        void OnGUI()
        {
            GUI.depth = -10; // sopra al resto
            EnsureStyles();
            float w = Mathf.Min(900f, Screen.width - 80f);
            float x = (Screen.width - w) * 0.5f;

            // frasi di sottofondo
            float by = Screen.height - (active ? 260f : 90f);
            DrawOverheadBarks();
            for (int i = barks.Count - 1; i >= 0; i--)
            {
                var b = barks[i];
                if (b.overhead) continue;
                string hex = ColorUtility.ToHtmlStringRGB(b.speaker.color);
                Rect r = new Rect(x, by, w, 28f);
                DrawShadowed(r, $"<color=#{hex}>{b.speaker.name}:</color> {b.text}", barkStyle);
                by -= 28f;
            }

            if (!active || index < 0 || index >= steps.Count) return;
            Step s = steps[index];

            if (s.options != null)
            {
                float h = 48f;
                float total = s.options.Length * (h + 8f);
                float y = Screen.height - 40f - total;
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(x - 16f, y - 16f, w + 32f, total + 24f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                for (int i = 0; i < s.options.Length; i++)
                {
                    if (GUI.Button(new Rect(x, y + i * (h + 8f), w, h), $"{i + 1}.  {s.options[i]}", choiceStyle)) Choose(i);
                }
                return;
            }

            float boxH = 130f;
            Rect box = new Rect(x - 20f, Screen.height - boxH - 30f, w + 40f, boxH);
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (s.speaker != null)
            {
                string hex = ColorUtility.ToHtmlStringRGB(s.speaker.color);
                GUI.Label(new Rect(x, box.y + 10f, w, 30f), $"<color=#{hex}>{s.speaker.name}</color>", nameStyle);
            }
            string shown = s.text.Substring(0, Mathf.Clamp((int)visibleChars, 0, s.text.Length));
            GUI.Label(new Rect(x, box.y + 42f, w, boxH - 50f), shown, textStyle);

            if (visibleChars >= s.text.Length && Mathf.Repeat(Time.time, 1f) < 0.6f)
                GUI.Label(new Rect(box.xMax - 60f, box.yMax - 34f, 50f, 30f), "▼", textStyle);
        }

        GUIStyle overheadStyle;

        void DrawOverheadBarks()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            if (overheadStyle == null) overheadStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
            foreach (var b in barks)
            {
                if (!b.overhead) continue;
                Vector3 world = b.position + Vector3.up * 0.45f;
                Vector3 sp = cam.WorldToScreenPoint(world);
                float dist = Vector3.Distance(cam.transform.position, world);
                if (sp.z <= 0f || dist > 30f) continue;
                float alpha = Mathf.Clamp01((30f - dist) / 10f) * Mathf.Clamp01((b.until - Time.time) * 2f);
                string hex = ColorUtility.ToHtmlStringRGB(b.speaker.color);
                Rect r = new Rect(sp.x - 160f, Screen.height - sp.y - 20f, 320f, 40f);
                var c = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                DrawShadowed(r, $"<color=#{hex}>{b.speaker.name}:</color> {b.text}", overheadStyle);
                GUI.color = c;
            }
        }

        static void DrawShadowed(Rect r, string text, GUIStyle style)
        {
            var c = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), StripColors(text), style);
            GUI.color = c;
            GUI.Label(r, text, style);
        }

        static string StripColors(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");
    }
}
