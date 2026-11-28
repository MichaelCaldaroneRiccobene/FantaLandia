using System;
using System.Collections.Generic;
using FantaLandia.Core;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Dialogue;
using FantaLandia.Horror.Interaction;
using FantaLandia.Horror.Player;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FantaLandia.Horror.UI
{
    /// <summary>
    /// Il menu è il taccuino di Muark: con Esc (o Tab) lo tira fuori e lo tiene davanti a sé per l'angolo in basso a destra.
    /// Il mondo intanto va avanti (niente pausa).
    ///
    /// È disegnato sopra la scena (non prende luci, nebbia, effetto retrò o sfocatura: si legge sempre uguale):
    /// copertina di cuoio, spirale, carta a righe con il margine rosso, segnalibri colorati sul bordo.
    ///  - pagina sinistra: il diario (titolo, sera e capitolo, cosa fare, le cose fatte barrate, uno scarabocchio)
    ///  - pagina destra: il menu, oppure le impostazioni divise in schede (Gioco, Audio, Video, Comandi)
    /// I valori sono scritti a mano: la scelta cerchiata, caselle spuntate, file di caselle riempite per i volumi.
    ///
    /// Tastiera: W/S scegli · A/D cambia (sul titolo della scheda: cambia scheda) · E conferma · Esc indietro/chiudi.
    /// Mouse: passandoci sopra si sceglie, clic sulle opzioni, sulle caselle, sui segnalibri; rotellina cambia.
    /// </summary>
    public class NotebookMenu : MonoBehaviour
    {
        [Tooltip("Non più usato (era il taccuino 3D in mano): resta per le scene già create")]
        public GameObject notebookPrefab;
        [Tooltip("Font scritto a mano per le pagine (TextMeshPro ne crea il font asset all'avvio)")]
        public Font handFont;
        TMP_FontAsset handFontAsset;

        public static bool IsOpen { get; private set; }

        [Tooltip("Il taccuino si apre da solo appena finisce l'intro (fa da menu iniziale)")]
        public bool openOnStart = true;
        /// <summary>Ricominciando da un salvataggio non si riapre da solo.</summary>
        public static bool SuppressAutoOpen;

        FirstPersonController fpc;
        Interactor interactor;
        float openT;            // 0 chiuso .. 1 aperto
        bool wantOpen;
        bool autoOpened, firstOpen = true;
        float savedPitch;
        Vector2 lastMouse;

        enum Page { Main, Gioco, Audio, Video, Comandi }
        Page page = Page.Main;
        int cursor;
        int confirmIndex = -1;  // la voce che chiede "sicuro?"
        bool dirty = true;

        // il diario: l'obiettivo di adesso e quelli già fatti
        string lastObjective = "";
        readonly List<string> done = new List<string>();

        static readonly Color Ink = new Color32(38, 44, 110, 255), InkFaded = new Color32(110, 112, 140, 255),
                              Pencil = new Color32(105, 100, 92, 255), RedInk = new Color32(176, 48, 44, 255),
                              PaperColor = new Color32(243, 234, 212, 255), Leather = new Color32(64, 38, 26, 255);
        static readonly Color[] TabColors = { new Color32(196, 74, 60, 255), new Color32(214, 160, 60, 255), new Color32(80, 132, 96, 255), new Color32(76, 102, 160, 255) };
        static readonly string[] TabNames = { "Gioco", "Audio", "Video", "Comandi" };

        // misure in "pixel del taccuino" (schermo di riferimento 1600 x 1000)
        const float BookW = 1140f, BookH = 810f;
        const float LeftX = 26f, RightX = 580f, PageW = 534f, PageY = 24f, PageH = 762f;
        const float TextLeft = LeftX + 95f, TextRight = RightX + 95f;

        // ------------------------------------------------------------------ interfaccia

        Canvas canvas;
        RectTransform book, leftContent, rightContent;
        Image backdrop;
        readonly RectTransform[] tabs = new RectTransform[4];
        Sprite rounded, soft, box, check, fill, circle, line, doodle;
        bool doodleIsRadio;

        /// <summary>Una zona cliccabile: voce 'item'; 'option' = -1 la riga, >= 0 un'opzione/casella, -2/-3 frecce ‹ ›.</summary>
        struct Zone { public RectTransform rt; public int item; public int option; }
        readonly List<Zone> zones = new List<Zone>();

        void Start()
        {
            GameSettings.Load();
            fpc = GetComponent<FirstPersonController>();
            interactor = GetComponent<Interactor>();
            if (handFont != null) handFontAsset = TMP_FontAsset.CreateFontAsset(handFont);
            MakeSprites();
            BuildBook();
            canvas.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ il libro (una volta sola)

        void BuildBook()
        {
            var cgo = new GameObject("Taccuino di Muark (menu)", typeof(RectTransform));
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 1000f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            // il mondo dietro si scurisce un po'
            backdrop = Img(cgo.transform, null, new Color(0f, 0f, 0f, 0f));
            Stretch(backdrop.rectTransform);

            book = (RectTransform)new GameObject("Libro", typeof(RectTransform)).transform;
            book.SetParent(cgo.transform, false);
            book.anchorMin = book.anchorMax = new Vector2(0.5f, 0.5f);
            book.pivot = new Vector2(0.5f, 0.5f);
            book.sizeDelta = new Vector2(BookW, BookH);

            // ombra, copertina di cuoio con la cucitura
            Place(Img(book, soft, new Color(0f, 0f, 0f, 0.75f)).rectTransform, -42f, -10f, BookW + 140f, BookH + 140f);
            Sliced(Img(book, rounded, Leather), 0f, 0f, BookW, BookH);
            Sliced(Img(book, rounded, new Color32(90, 56, 38, 255)), 8f, 8f, BookW - 16f, BookH - 16f);
            Sliced(Img(book, rounded, Leather), 11f, 11f, BookW - 22f, BookH - 22f);

            // i segnalibri sul bordo destro (sotto le pagine)
            for (int i = 0; i < 4; i++)
            {
                var tab = Img(book, rounded, TabColors[i]);
                tab.type = Image.Type.Sliced;
                tabs[i] = tab.rectTransform;
                var label = Text(tabs[i], TabNames[i], 25f, new Color32(255, 248, 230, 255), 0f, 0f, 104f, 54f, TextAlignmentOptions.Center);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.62f, 0.5f);
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                label.rectTransform.anchoredPosition = Vector2.zero;
                label.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                label.name = "Nome scheda";
            }

            // le due pagine di carta a righe
            for (int p = 0; p < 2; p++)
            {
                var pageImg = new GameObject(p == 0 ? "Pagina sinistra" : "Pagina destra", typeof(RectTransform)).AddComponent<RawImage>();
                pageImg.texture = PaperTexture((int)PageW, (int)PageH, 7 + p);
                pageImg.raycastTarget = false;
                pageImg.rectTransform.SetParent(book, false);
                Place(pageImg.rectTransform, p == 0 ? LeftX : RightX, PageY, PageW, PageH);
            }
            // l'ombra della piega e la spirale
            Place(Img(book, soft, new Color(0.2f, 0.12f, 0.05f, 0.35f)).rectTransform, LeftX + PageW - 40f, PageY, 100f, PageH);
            for (float y = PageY + 20f; y < PageY + PageH - 20f; y += 44f)
            {
                Sliced(Img(book, rounded, new Color32(90, 92, 98, 255)), LeftX + PageW - 14f, y, 46f, 18f);
                Sliced(Img(book, rounded, new Color32(176, 178, 184, 255)), LeftX + PageW - 12f, y + 2f, 42f, 14f);
                Place(Img(book, null, new Color32(232, 234, 238, 255)).rectTransform, LeftX + PageW - 6f, y + 5f, 30f, 2.5f);
            }
            // luce morbida dall'alto a sinistra
            Place(Img(book, soft, new Color(1f, 0.86f, 0.6f, 0.1f)).rectTransform, -100f, -300f, 900f, 800f);

            leftContent = Group("Diario");
            rightContent = Group("Menu");

            // la mano di Muark: tiene l'angolo in basso a destra, fuori dalla pagina (manica di flanella verde)
            var sleeve = Img(book, null, new Color32(42, 78, 52, 255));
            Place(sleeve.rectTransform, BookW + 10f, BookH - 30f, 420f, 230f);
            sleeve.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 28f);
            var cuff = Img(book, null, new Color32(30, 56, 38, 255));
            Place(cuff.rectTransform, BookW - 2f, BookH - 40f, 40f, 230f);
            cuff.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 28f);
            var hand = Img(book, null, new Color32(196, 150, 112, 255));
            Place(hand.rectTransform, BookW - 95f, BookH - 55f, 150f, 105f);
            hand.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 22f);
            var thumb = Img(book, null, new Color32(214, 170, 132, 255));
            Place(thumb.rectTransform, BookW - 110f, BookH - 62f, 62f, 34f);
            thumb.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 22f);

            LayoutTabs();
        }

        RectTransform Group(string name)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(book, false);
            Place(rt, 0f, 0f, BookW, BookH);
            return rt;
        }

        void LayoutTabs()
        {
            for (int i = 0; i < 4; i++)
            {
                bool sel = page != Page.Main && (int)page - 1 == i;
                Place(tabs[i], BookW - 30f, 80f + i * 128f, sel ? 108f : 84f, 104f);
                tabs[i].SetSiblingIndex(4 + i); // sopra la copertina, sotto le pagine
                var label = tabs[i].GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = Loc.T(TabNames[i]); // la lingua può essere cambiata
            }
        }

        // ------------------------------------------------------------------ apertura e chiusura

        bool CanOpen => !IntroSequence.Active && fpc != null && !fpc.inputLocked && !DialogueSystem.IsBlocking;

        void Open()
        {
            wantOpen = true;
            IsOpen = true;
            page = Page.Main;
            LayoutTabs();
            cursor = 0;
            confirmIndex = -1;
            savedPitch = fpc.Pitch;
            fpc.inputLocked = true;
            if (interactor != null) interactor.interactionEnabled = false;
            Cursor.lockState = CursorLockMode.None; // si può usare il mouse sulle pagine
            Cursor.visible = true;
            lastMouse = GameInput.MousePosition;
            canvas.gameObject.SetActive(true);
            HorrorAudio.Play2D(ProceduralAudio.PickUp(), 0.5f, 1.2f);
            Redraw();
        }

        void Close()
        {
            firstOpen = false;
            wantOpen = false;
            confirmIndex = -1;
            HorrorAudio.Play2D(ProceduralAudio.PickUp(), 0.4f, 0.9f);
        }

        void Update()
        {
            if (fpc == null || canvas == null) return;
            TrackObjective();

            if (!IsOpen)
            {
                // all'avvio (finita l'intro) il taccuino è già aperto: è il menu iniziale
                if (openOnStart && !autoOpened && !IntroSequence.Active && CanOpen)
                {
                    autoOpened = true;
                    if (SuppressAutoOpen) SuppressAutoOpen = false;
                    else { Open(); return; }
                }
                if (GameInput.MenuToggle && CanOpen) Open();
                return;
            }

            openT = Mathf.MoveTowards(openT, wantOpen ? 1f : 0f, Time.unscaledDeltaTime * 2.6f);
            if (!wantOpen && openT <= 0f)
            {
                IsOpen = false;
                canvas.gameObject.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                fpc.inputLocked = false;
                if (interactor != null) interactor.interactionEnabled = true;
                return;
            }
            if (wantOpen && openT > 0.6f) HandleInput();
            if (dirty) Redraw();
        }

        void LateUpdate()
        {
            if (!IsOpen || book == null) return;
            // sale da in basso a destra (dalla tasca), girandosi dritto; poi oscilla appena, come tenuto in mano
            float e = openT * openT * (3f - 2f * openT);
            float sway = Mathf.Sin(Time.unscaledTime * 1.3f) * 1.5f;
            book.anchoredPosition = Vector2.Lerp(new Vector2(420f, -900f), new Vector2(0f, -10f + sway), e);
            book.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-18f, Mathf.Sin(Time.unscaledTime * 0.9f) * 0.4f, e));
            book.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, e);
            backdrop.color = new Color(0f, 0f, 0f, 0.45f * e);
            // lo sguardo si abbassa un po' verso le mani
            float k = 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime);
            fpc.SetLook(fpc.Yaw, Mathf.Lerp(fpc.Pitch, wantOpen ? Mathf.Max(savedPitch, 18f) : savedPitch, k));
        }

        void TrackObjective()
        {
            string now = FantaLandia.Horror.Story.StoryDirector.Objective ?? "";
            if (now == lastObjective) return;
            if (!string.IsNullOrEmpty(lastObjective)) { done.Remove(lastObjective); done.Insert(0, lastObjective); }
            lastObjective = now;
            dirty = true;
        }

        // ------------------------------------------------------------------ voci del menu

        enum Kind { Action, Choice, Toggle, Scale, Stepper, Info, TabTitle }

        class Item
        {
            public string label;
            public Kind kind;
            public string[] options;                 // Choice
            public Func<int> get;                    // Choice: indice; Scale: 0..10
            public Action<int> set;
            public Func<bool> getBool;               // Toggle
            public Action<bool> setBool;
            public Func<string> show;                // Stepper
            public Action<int> step;
            public Action confirm;
            public bool ask;
            public bool Selectable => kind != Kind.Info;
        }

        List<Item> Items()
        {
            var list = new List<Item>();
            switch (page)
            {
                case Page.Main:
                    list.Add(new Item { label = firstOpen ? "Gioca" : "Riprendi", kind = Kind.Action, confirm = Close });
                    list.Add(new Item { label = "Impostazioni", kind = Kind.Action, confirm = () => GoTo(Page.Gioco) });
                    if (GameSettings.Checkpoint > 0)
                        list.Add(new Item { label = "Torna all'ultimo punto", kind = Kind.Action, ask = true, confirm = () => Restart(GameSettings.Checkpoint) });
                    list.Add(new Item { label = "Ricomincia da capo", kind = Kind.Action, ask = true, confirm = () => { GameSettings.Checkpoint = 0; Restart(0); } });
                    list.Add(new Item { label = "Esci dal gioco", kind = Kind.Action, ask = true, confirm = Quit });
                    return list;
                case Page.Gioco:
                    list.Add(Title());
                    list.Add(new Item { label = "Lingua", kind = Kind.Choice, options = new[] { "Italiano", "English" },
                                        get = () => GameSettings.Language == "en" ? 1 : 0, set = i => GameSettings.Language = i == 1 ? "en" : "it" });
                    list.Add(Toggle("Voci parlate", () => GameSettings.SpokenVoices, v => GameSettings.SpokenVoices = v));
                    list.Add(Toggle("Sottotitoli", () => GameSettings.Subtitles, v => GameSettings.Subtitles = v));
                    list.Add(new Item { label = "Testo", kind = Kind.Choice, options = new[] { "piccolo", "medio", "grande" },
                                        get = () => GameSettings.TextSize, set = i => GameSettings.TextSize = i });
                    list.Add(new Item { label = "Sensibilità mouse", kind = Kind.Scale,
                                        get = () => Mathf.Clamp(Mathf.RoundToInt(GameSettings.Sensitivity * 5f), 1, 10),
                                        set = i => GameSettings.Sensitivity = Mathf.Max(1, i) * 0.2f });
                    break;
                case Page.Audio:
                    list.Add(Title());
                    list.Add(Volume("Volume generale", () => GameSettings.Master, v => GameSettings.Master = v));
                    list.Add(Volume("Voci", () => GameSettings.Voices, v => GameSettings.Voices = v));
                    list.Add(Volume("Musica e TV", () => GameSettings.Music, v => GameSettings.Music = v));
                    list.Add(Volume("Effetti", () => GameSettings.Effects, v => GameSettings.Effects = v));
                    break;
                case Page.Video:
                    list.Add(Title());
                    list.Add(new Item { label = "Luminosità", kind = Kind.Stepper, show = () => (GameSettings.Brightness > 0 ? "+" : "") + GameSettings.Brightness,
                                        step = d => GameSettings.Brightness = Mathf.Clamp(GameSettings.Brightness + d, -5, 5) });
                    list.Add(new Item { label = "Qualità grafica", kind = Kind.Choice, options = new[] { "bassa", "media", "alta" },
                                        get = () => GameSettings.Quality, set = i => GameSettings.Quality = i });
                    list.Add(Toggle("Effetto retrò", () => GameSettings.Retro, v => GameSettings.Retro = v));
                    break;
                case Page.Comandi:
                    list.Add(Title());
                    foreach (string c in Controls) list.Add(new Item { label = c, kind = Kind.Info });
                    break;
            }
            list.Add(new Item { label = "Indietro", kind = Kind.Action, confirm = () => GoTo(Page.Main) });
            return list;
        }

        static readonly string[] Controls =
        {
            "WASD|muoviti", "Shift|corri", "C|abbassati", "E|interagisci", "F|torcia", "Spazio|salta una battuta", "Esc|il taccuino",
        };

        Item Title() => new Item { label = TabNames[(int)page - 1], kind = Kind.TabTitle };

        static Item Toggle(string label, Func<bool> get, Action<bool> set) =>
            new Item { label = label, kind = Kind.Toggle, getBool = get, setBool = set };

        static Item Volume(string label, Func<float> get, Action<float> set) => new Item
        {
            label = label, kind = Kind.Scale,
            get = () => Mathf.Clamp(Mathf.RoundToInt(get() * 10f), 0, 10),
            set = i => set(Mathf.Clamp(i, 0, 10) / 10f),
        };

        void GoTo(Page p)
        {
            Page from = page;
            page = p;
            if (p != Page.Main) firstOpen = false; // dopo le impostazioni si "riprende"
            cursor = p == Page.Main ? (from == Page.Main ? 0 : 1) : 1;
            confirmIndex = -1;
            LayoutTabs();
            HorrorAudio.Play2D(ProceduralAudio.PickUp(), 0.35f, 1.5f); // la pagina che si gira
            dirty = true;
        }

        void SwitchTab(int d)
        {
            int t = page == Page.Main ? 0 : (int)page - 1;
            t = (t + d + 4) % 4;
            GoTo((Page)(t + 1));
            cursor = 0;
        }

        /// <summary>Cambia il valore di una voce di 'd' passi (A/D, rotellina).</summary>
        void Change(Item it, int d)
        {
            switch (it.kind)
            {
                case Kind.Choice:
                    int n = it.options.Length, i = it.get();
                    it.set(it.label == "Lingua" ? (i + d + n) % n : Mathf.Clamp(i + d, 0, n - 1));
                    break;
                case Kind.Toggle: it.setBool(!it.getBool()); break;
                case Kind.Scale: it.set(Mathf.Clamp(it.get() + d, 0, 10)); break;
                case Kind.Stepper: it.step(d); break;
                case Kind.TabTitle: SwitchTab(d); return;
                default: return;
            }
            GameSettings.Save();
            Scribble();
            dirty = true;
        }

        /// <summary>Clic su un'opzione precisa (una parola, una casella, una freccia).</summary>
        void Pick(Item it, int option)
        {
            switch (it.kind)
            {
                case Kind.Choice: it.set(option); break;
                case Kind.Toggle: it.setBool(option == 0); break;
                case Kind.Scale: it.set(option == 0 && it.get() == 1 ? 0 : option + 1); break;
                case Kind.Stepper: it.step(option == -2 ? -1 : 1); break;
                default: return;
            }
            GameSettings.Save();
            Scribble();
            dirty = true;
        }

        void Confirm(Item it)
        {
            if (it.kind == Kind.Action)
            {
                if (it.ask && confirmIndex != cursor) { confirmIndex = cursor; Scribble(); dirty = true; return; }
                confirmIndex = -1;
                Scribble();
                dirty = true;
                it.confirm?.Invoke();
            }
            else if (it.kind == Kind.TabTitle) SwitchTab(1);
            else if (it.kind != Kind.Info) Change(it, 1);
        }

        void HandleInput()
        {
            var items = Items();
            if (GameInput.MenuToggle)
            {
                if (page != Page.Main) GoTo(Page.Main);
                else Close();
                return;
            }
            int before = cursor;
            if (GameInput.MenuUp) cursor = Step(items, cursor, -1);
            if (GameInput.MenuDown) cursor = Step(items, cursor, 1);

            // mouse: sopra cosa sta il puntatore? (le opzioni vincono sulla riga intera)
            Vector2 mp = GameInput.MousePosition;
            bool moved = (mp - lastMouse).sqrMagnitude > 1f;
            lastMouse = mp;
            bool hovering = false;
            Zone hover = default;
            foreach (var z in zones)
            {
                if (z.rt == null || !RectTransformUtility.RectangleContainsScreenPoint(z.rt, mp, null)) continue;
                if (!hovering || z.option != -1) { hover = z; hovering = true; }
                if (z.option != -1) break;
            }
            if (hovering && moved && hover.item >= 0 && hover.item < items.Count && items[hover.item].Selectable)
                cursor = hover.item;
            if (cursor != before) { confirmIndex = -1; Scribble(); dirty = true; }

            // clic sui segnalibri
            if (GameInput.LeftDown)
                for (int t = 0; t < 4; t++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(tabs[t], mp, null)) { GoTo((Page)(t + 1)); return; }

            cursor = Mathf.Clamp(cursor, 0, items.Count - 1);
            Item it = items[cursor];
            int delta = GameInput.MenuLeft ? -1 : GameInput.MenuRight ? 1 : 0;
            if (delta == 0 && hovering && hover.item == cursor && Mathf.Abs(GameInput.Scroll) > 0.01f && it.kind != Kind.Action)
                delta = GameInput.Scroll > 0f ? 1 : -1;
            if (delta != 0) { Change(it, delta); return; }

            if (GameInput.LeftDown && hovering && hover.item == cursor)
            {
                if (hover.option != -1) Pick(it, hover.option);
                else Confirm(it);
            }
            else if (GameInput.MenuConfirm) Confirm(it);
        }

        static int Step(List<Item> items, int from, int d)
        {
            int i = from;
            for (int n = 0; n < items.Count; n++)
            {
                i = (i + d + items.Count) % items.Count;
                if (items[i].Selectable) return i;
            }
            return from;
        }

        void Scribble() => HorrorAudio.Play2D(ProceduralAudio.Click(), 0.25f, UnityEngine.Random.Range(0.5f, 0.7f));

        // ------------------------------------------------------------------ le pagine scritte

        void Redraw()
        {
            dirty = false;
            if (leftContent == null) return;
            Clear(leftContent);
            Clear(rightContent);
            zones.Clear();
            DrawDiary();
            if (page == Page.Main) DrawMain();
            else DrawSettings();
        }

        static void Clear(RectTransform rt)
        {
            for (int i = rt.childCount - 1; i >= 0; i--)
            {
                var child = rt.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        bool Chapter2 => FindFirstObjectByType<FantaLandia.Horror.Story.Chapter2Director>() != null;

        void DrawDiary()
        {
            var L = leftContent;
            bool ch2 = Chapter2;
            Text(L, Loc.T("Taccuino di Muark"), 50f, Ink, TextLeft, 30f, 420f, 70f);
            Text(L, Loc.T(ch2 ? "Venerdì notte · Capitolo 2" : "Venerdì sera · Capitolo 1"), 25f, Pencil, TextLeft, 104f, 420f, 40f);
            Text(L, Loc.T("da fare:"), 25f, Pencil, TextLeft, 186f, 300f, 40f);
            float y = 230f;
            if (!string.IsNullOrEmpty(lastObjective))
            {
                Split(Loc.T(lastObjective), out string main, out string note);
                Place(Img(L, box, Ink).rectTransform, TextLeft + 8f, y + 14f, 26f, 26f);
                var t = Text(L, main, 31f, Ink, TextLeft + 50f, y, 360f, 46f);
                t.enableAutoSizing = true; t.fontSizeMin = 18f; t.fontSizeMax = 31f;
                if (!string.IsNullOrEmpty(note))
                {
                    var n = Text(L, note, 24f, Pencil, TextLeft + 50f, y + 44f, 360f, 40f);
                    n.enableAutoSizing = true; n.fontSizeMin = 14f; n.fontSizeMax = 24f;
                }
                y += 130f;
            }
            else y += 60f;
            // le cose già fatte: spuntate e barrate
            for (int i = 0; i < Mathf.Min(3, done.Count); i++)
            {
                Split(Loc.T(done[i]), out string main, out _);
                Place(Img(L, box, InkFaded).rectTransform, TextLeft + 8f, y + 12f, 24f, 24f);
                Place(Img(L, check, RedInk).rectTransform, TextLeft + 6f, y + 2f, 34f, 34f);
                var t = Text(L, main, 26f, InkFaded, TextLeft + 50f, y, 360f, 42f);
                float w = Mathf.Min(360f, t.GetPreferredValues(main).x);
                if (w >= 360f) { t.enableAutoSizing = true; t.fontSizeMin = 14f; t.fontSizeMax = 26f; }
                Place(Img(L, line, RedInk).rectTransform, TextLeft + 46f, y + 16f, w + 10f, 10f);
                y += 46f;
            }
            // lo scarabocchio del capitolo
            if (doodle == null || doodleIsRadio != ch2) { doodleIsRadio = ch2; doodle = Doodle(ch2); }
            Place(Img(L, doodle, Ink).rectTransform, TextLeft + 90f, 520f, 220f, 170f);
            // i comandi del taccuino, in piccolo in fondo alla pagina
            Text(L, Loc.T("W/S o mouse: scegli  ·  A/D: cambia"), 20f, Pencil, TextLeft, PageY + PageH - 96f, 430f, 30f);
            Text(L, Loc.T("E o clic: conferma  ·  Esc: chiudi"), 20f, Pencil, TextLeft, PageY + PageH - 66f, 430f, 30f);
        }

        static void Split(string objective, out string main, out string note)
        {
            int p = objective.IndexOf(" (", StringComparison.Ordinal);
            if (p > 0) { main = objective.Substring(0, p); note = objective.Substring(p + 1); }
            else { main = objective; note = ""; }
        }

        void DrawMain()
        {
            var R = rightContent;
            Text(R, Loc.T("Menu"), 52f, Ink, TextRight, 30f, 400f, 72f);
            var items = Items();
            for (int i = 0; i < items.Count; i++)
            {
                float y = 150f + i * 96f;
                bool sel = i == cursor;
                string lab = Loc.T(items[i].label);
                if (sel) Text(R, "»", 40f, RedInk, TextRight - 40f, y, 40f, 56f);
                var t = Text(R, lab, sel ? 40f : 36f, sel ? Ink : (Color)new Color32(60, 66, 120, 255), TextRight, y, 410f, 56f);
                float w = t.GetPreferredValues(lab).x;
                if (w > 410f) { t.enableAutoSizing = true; t.fontSizeMin = 20f; t.fontSizeMax = t.fontSize; w = 410f; }
                Row(i, TextRight - 40f, y, 460f, 60f);
                if (sel) Place(Img(R, line, RedInk).rectTransform, TextRight, y + 50f, w + 8f, 10f);
                if (sel && confirmIndex == i) Text(R, Loc.T("sicuro? conferma di nuovo"), 22f, RedInk, TextRight + 20f, y + 58f, 400f, 30f);
            }
        }

        void DrawSettings()
        {
            var R = rightContent;
            var items = Items();
            float y = 150f;
            for (int i = 0; i < items.Count; i++)
            {
                Item it = items[i];
                bool sel = i == cursor;
                if (it.kind == Kind.TabTitle)
                {
                    if (sel) Text(R, "»", 44f, RedInk, TextRight - 42f, 34f, 40f, 66f);
                    Text(R, Loc.T(it.label), 52f, Ink, TextRight, 30f, 260f, 72f);
                    if (sel) Text(R, Loc.T("A/D: cambia scheda"), 20f, Pencil, TextRight + 240f, 54f, 200f, 30f);
                    Row(i, TextRight - 40f, 30f, 440f, 72f);
                    continue;
                }
                if (it.kind == Kind.Info)
                {
                    string[] kv = it.label.Split('|');
                    var key = Text(R, Loc.T(kv[0]), 28f, Ink, TextRight, y, 130f, 42f);
                    key.fontStyle = FontStyles.Bold;
                    Text(R, Loc.T(kv[1]), 28f, Ink, TextRight + 140f, y, 290f, 42f);
                    y += 54f;
                    continue;
                }
                if (it.label == "Indietro")
                {
                    float by = Mathf.Max(y + 10f, PageY + PageH - 90f);
                    string back = Loc.T("Indietro");
                    if (sel) Text(R, "»", 34f, RedInk, TextRight - 36f, by, 40f, 46f);
                    var b = Text(R, back, sel ? 34f : 30f, sel ? Ink : InkFaded, TextRight, by, 300f, 46f);
                    if (sel) Place(Img(R, line, RedInk).rectTransform, TextRight, by + 42f, b.GetPreferredValues(back).x + 6f, 9f);
                    Row(i, TextRight - 40f, by, 300f, 50f);
                    continue;
                }
                if (sel) Text(R, "»", 38f, RedInk, TextRight - 40f, y, 40f, 50f);
                Text(R, Loc.T(it.label), 34f, Ink, TextRight, y, 420f, 48f);
                Row(i, TextRight - 40f, y, 460f, 100f);
                DrawValue(R, it, i, TextRight + 10f, y + 48f);
                y += 106f;
            }
        }

        /// <summary>Il valore scritto a mano sotto la voce: scelta cerchiata, caselle sì/no, file di caselle, frecce.</summary>
        void DrawValue(RectTransform R, Item it, int index, float x, float y)
        {
            switch (it.kind)
            {
                case Kind.Choice:
                {
                    int sel = it.get();
                    float cx = x;
                    for (int o = 0; o < it.options.Length; o++)
                    {
                        string s = Loc.T(it.options[o]);
                        var t = Text(R, s, 30f, o == sel ? Ink : InkFaded, cx, y, 200f, 44f);
                        float w = t.GetPreferredValues(s).x;
                        t.rectTransform.sizeDelta = new Vector2(w + 4f, 44f);
                        if (o == sel) Place(Img(R, circle, RedInk).rectTransform, cx - 16f, y - 4f, w + 32f, 52f);
                        Option(index, o, cx - 8f, y, w + 16f, 46f);
                        cx += w + 46f;
                    }
                    break;
                }
                case Kind.Toggle:
                {
                    bool on = it.getBool();
                    for (int o = 0; o < 2; o++)
                    {
                        float bx = x + o * 130f;
                        Place(Img(R, box, Ink).rectTransform, bx, y + 8f, 30f, 30f);
                        if ((o == 0) == on) Place(Img(R, check, RedInk).rectTransform, bx - 2f, y - 6f, 44f, 44f);
                        Text(R, Loc.T(o == 0 ? "sì" : "no"), 30f, Ink, bx + 42f, y, 80f, 44f);
                        Option(index, o, bx - 4f, y, 110f, 46f);
                    }
                    break;
                }
                case Kind.Scale:
                {
                    int v = it.get();
                    for (int k = 0; k < 10; k++)
                    {
                        float bx = x + k * 34f;
                        Place(Img(R, box, Ink).rectTransform, bx, y + 8f, 26f, 28f);
                        if (k < v) Place(Img(R, fill, Ink).rectTransform, bx + 2f, y + 10f, 22f, 24f);
                        Option(index, k, bx - 4f, y + 2f, 34f, 40f);
                    }
                    string pct = it.label == "Sensibilità mouse" ? (v * 0.2f).ToString("0.0") + "x" : v * 10 + "%";
                    Text(R, pct, 28f, Pencil, x + 352f, y + 2f, 90f, 40f);
                    break;
                }
                case Kind.Stepper:
                {
                    Text(R, "«", 38f, Ink, x, y - 4f, 30f, 46f);
                    Text(R, it.show(), 30f, Ink, x + 40f, y, 60f, 44f, TextAlignmentOptions.Top);
                    Text(R, "»", 38f, Ink, x + 110f, y - 4f, 30f, 46f);
                    Option(index, -2, x - 8f, y, 44f, 46f);
                    Option(index, -3, x + 102f, y, 44f, 46f);
                    break;
                }
            }
        }

        void Row(int item, float x, float y, float w, float h) => zones.Add(new Zone { rt = Hit(x, y, w, h), item = item, option = -1 });
        void Option(int item, int option, float x, float y, float w, float h) => zones.Add(new Zone { rt = Hit(x, y, w, h), item = item, option = option });

        RectTransform Hit(float x, float y, float w, float h)
        {
            var rt = (RectTransform)new GameObject("Zona", typeof(RectTransform)).transform;
            rt.SetParent(rightContent, false);
            Place(rt, x, y, w, h);
            return rt;
        }

        // ------------------------------------------------------------------ mattoncini dell'interfaccia

        static Image Img(Transform parent, Sprite sprite, Color color)
        {
            var img = new GameObject("Immagine", typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static void Sliced(Image img, float x, float y, float w, float h)
        {
            img.type = Image.Type.Sliced;
            Place(img.rectTransform, x, y, w, h);
        }

        /// <summary>Posizione dall'angolo in alto a sinistra del libro (y verso il basso), come nei disegni di prova.</summary>
        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        TextMeshProUGUI Text(Transform parent, string s, float size, Color color, float x, float y, float w, float h,
                             TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var t = new GameObject("Scritta", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            if (handFontAsset != null) t.font = handFontAsset;
            t.text = s;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = true;
            Place(t.rectTransform, x, y, w, h);
            return t;
        }

        // ------------------------------------------------------------------ texture disegnate a mano (create all'avvio)

        void MakeSprites()
        {
            rounded = Sprite.Create(RoundedTex(64, 24), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
            soft = Make(SoftTex(128));
            box = Make(Draw(48, 48, (px, w, h) => { Stroke(px, w, h, 5, 6, 43, 5, 3f); Stroke(px, w, h, 43, 5, 42, 43, 3f); Stroke(px, w, h, 42, 43, 6, 42, 3f); Stroke(px, w, h, 6, 42, 5, 6, 3f); }));
            check = Make(Draw(48, 48, (px, w, h) => { Stroke(px, w, h, 6, 24, 17, 10, 4f); Stroke(px, w, h, 17, 10, 44, 44, 4f); }));
            fill = Make(Draw(48, 48, (px, w, h) => { for (int k = 0; k < 7; k++) Stroke(px, w, h, (k % 2 == 0) ? 6 : 40, 42 - k * 6, (k % 2 == 0) ? 40 : 6, 39 - k * 6, 4f); }));
            circle = Make(Draw(256, 96, (px, w, h) =>
            {
                float x0 = 0f, y0 = 0f;
                for (int i = 0; i <= 80; i++)
                {
                    float a = i / 70f * Mathf.PI * 2f + 0.4f, k = 1f + 0.04f * Mathf.Sin(i * 0.7f);
                    float x = 128f + Mathf.Cos(a) * 118f * k, y = 48f + Mathf.Sin(a) * 40f * k;
                    if (i > 0) Stroke(px, w, h, x0, y0, x, y, 3.2f);
                    x0 = x; y0 = y;
                }
            }));
            line = Make(Draw(256, 16, (px, w, h) =>
            {
                float x0 = 3f, y0 = 8f;
                for (int i = 1; i <= 12; i++) { float x = 3f + i * 20.8f, y = 8f + Mathf.Sin(i * 1.7f) * 2f; Stroke(px, w, h, x0, y0, x, y, 2.6f); x0 = x; y0 = y; }
            }));
        }

        static Sprite Make(Texture2D t) => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);

        static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        static Texture2D RoundedTex(int s, int r)
        {
            var t = NewTex(s, s);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(r - x - 0.5f, x + 0.5f - (s - r))), dy = Mathf.Max(0f, Mathf.Max(r - y - 0.5f, y + 0.5f - (s - r)));
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    if (dx <= 0f && dy <= 0f) a = 1f;
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply(false);
            return t;
        }

        static Texture2D SoftTex(int s)
        {
            var t = NewTex(s, s);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            t.SetPixels32(px);
            t.Apply(false);
            return t;
        }

        /// <summary>Una texture trasparente su cui si disegna a penna (bianco: il colore lo dà l'immagine).</summary>
        static Texture2D Draw(int w, int h, Action<Color32[], int, int> paint)
        {
            var t = NewTex(w, h);
            var px = new Color32[w * h];
            paint(px, w, h);
            t.SetPixels32(px);
            t.Apply(false);
            return t;
        }

        /// <summary>Un tratto di penna da (x0,y0) a (x1,y1) (y dal basso), con i bordi morbidi.</summary>
        static void Stroke(Color32[] px, int w, int h, float x0, float y0, float x1, float y1, float width)
        {
            float r = width * 0.5f;
            int minX = Mathf.Max(0, (int)(Mathf.Min(x0, x1) - r - 2)), maxX = Mathf.Min(w - 1, (int)(Mathf.Max(x0, x1) + r + 2));
            int minY = Mathf.Max(0, (int)(Mathf.Min(y0, y1) - r - 2)), maxY = Mathf.Min(h - 1, (int)(Mathf.Max(y0, y1) + r + 2));
            Vector2 a = new Vector2(x0, y0), ab = new Vector2(x1, y1) - a;
            float len2 = Mathf.Max(0.0001f, ab.sqrMagnitude);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float d = Vector2.Distance(p, a + ab * t);
                    byte alpha = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255);
                    int i = y * w + x;
                    if (alpha > px[i].a) px[i] = new Color32(255, 255, 255, alpha);
                }
        }

        /// <summary>
        /// La carta: crema con un po' di grana, qualche macchia, bordi appena ingialliti, righe azzurrine e il margine rosso.
        /// </summary>
        static Texture2D PaperTexture(int w, int h, int seed)
        {
            var t = NewTex(w, h);
            var px = new Color32[w * h];
            var rng = new System.Random(seed);
            var stains = new List<Vector3>();
            for (int i = 0; i < 6; i++) stains.Add(new Vector3(rng.Next(w), rng.Next(h), 40 + rng.Next(80)));
            for (int y = 0; y < h; y++)
            {
                int top = h - 1 - y; // righe contate dall'alto
                bool ruled = top >= 86 && (top - 86) % 46 < 2;
                for (int x = 0; x < w; x++)
                {
                    Color c = PaperColor;
                    float n = (float)(rng.NextDouble() - 0.5) * 0.045f;
                    c.r += n; c.g += n; c.b += n * 1.1f;
                    foreach (var s in stains)
                    {
                        float dx = x - s.x, dy = y - s.y;
                        float d2 = dx * dx + dy * dy;
                        if (d2 < s.z * s.z) c = Color.Lerp(c, new Color(0.78f, 0.68f, 0.47f), 0.06f * (1f - Mathf.Sqrt(d2) / s.z));
                    }
                    float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    if (edge < 18f) c = Color.Lerp(c, new Color(0.75f, 0.65f, 0.45f), 0.12f * (1f - edge / 18f));
                    if (ruled) c = Color.Lerp(c, new Color(0.47f, 0.63f, 0.78f), 0.4f);
                    if (x >= 69 && x <= 71) c = Color.Lerp(c, new Color(0.8f, 0.35f, 0.35f), 0.6f);
                    px[y * w + x] = c;
                }
            }
            t.SetPixels32(px);
            t.Apply(false);
            return t;
        }

        /// <summary>Lo scarabocchio della pagina: una fetta di pizza (capitolo 1) o la radio con l'antenna (capitolo 2).</summary>
        static Sprite Doodle(bool radio)
        {
            return Make(Draw(220, 170, (px, w, h) =>
            {
                if (radio)
                {
                    // la radio: corpo, altoparlante, griglia, antenna e onde
                    Stroke(px, w, h, 20, 20, 160, 20, 3f); Stroke(px, w, h, 160, 20, 160, 100, 3f);
                    Stroke(px, w, h, 160, 100, 20, 100, 3f); Stroke(px, w, h, 20, 100, 20, 20, 3f);
                    float cx0 = 0f, cy0 = 0f;
                    for (int i = 0; i <= 24; i++)
                    {
                        float a = i / 24f * Mathf.PI * 2f, x = 58 + Mathf.Cos(a) * 24, y = 60 + Mathf.Sin(a) * 24;
                        if (i > 0) Stroke(px, w, h, cx0, cy0, x, y, 3f);
                        cx0 = x; cy0 = y;
                    }
                    for (int k = 0; k < 4; k++) Stroke(px, w, h, 96, 38 + k * 14, 146, 38 + k * 14, 2.4f);
                    Stroke(px, w, h, 140, 100, 186, 150, 3f);
                    for (int k = 0; k < 3; k++)
                    {
                        float r = 14 + k * 10, ax = 0f, ay = 0f;
                        for (int i = 0; i <= 8; i++)
                        {
                            float a = Mathf.Lerp(-0.2f, 1.2f, i / 8f), x = 186 + Mathf.Cos(a) * r, y = 150 + Mathf.Sin(a) * r - 10;
                            if (i > 0) Stroke(px, w, h, ax, ay, x, y, 2.2f);
                            ax = x; ay = y;
                        }
                    }
                }
                else
                {
                    // la fetta di pizza: triangolo, crosta, salamini
                    Stroke(px, w, h, 40, 140, 180, 140, 3f); Stroke(px, w, h, 180, 140, 110, 15, 3f); Stroke(px, w, h, 110, 15, 40, 140, 3f);
                    Stroke(px, w, h, 36, 152, 184, 152, 3f); Stroke(px, w, h, 36, 152, 40, 140, 3f); Stroke(px, w, h, 184, 152, 180, 140, 3f);
                    foreach (var c in new[] { new Vector2(95, 110), new Vector2(135, 115), new Vector2(112, 72) })
                    {
                        float ax = 0f, ay = 0f;
                        for (int i = 0; i <= 14; i++)
                        {
                            float a = i / 14f * Mathf.PI * 2f, x = c.x + Mathf.Cos(a) * 10, y = c.y + Mathf.Sin(a) * 10;
                            if (i > 0) Stroke(px, w, h, ax, ay, x, y, 2.4f);
                            ax = x; ay = y;
                        }
                    }
                }
            }));
        }

        // ------------------------------------------------------------------ ricominciare e uscire

        void Restart(int startAt)
        {
            SuppressAutoOpen = true;
            FantaLandia.Horror.Story.StoryDirector.PendingStart = startAt;
            IntroSequence.SkipLogo = true;
            IsOpen = false;
            // i punti dal 100 in su sono del capitolo 2 (un'altra scena); gli altri del capitolo 1
            string scene = startAt >= FantaLandia.Horror.Story.Chapter2Director.CheckpointBase ? FantaLandia.Horror.Story.Chapter2Director.SceneName : "Casa_Tom";
            if (Application.CanStreamedLevelBeLoaded(scene)) SceneManager.LoadScene(scene);
            else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0 ? SceneManager.GetActiveScene().buildIndex : 0);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnDestroy()
        {
            IsOpen = false;
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }
}
