using System;
using System.Collections.Generic;
using FantaLandia.Characters.Body;
using FantaLandia.Core;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Dialogue;
using FantaLandia.Horror.Interaction;
using FantaLandia.Horror.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace FantaLandia.Horror.UI
{
    /// <summary>
    /// Il menu è il taccuino di Muark: con Esc (o Tab) lo tira fuori con la mano destra, la visuale si abbassa e lo segue,
    /// e sulle pagine scritte a mano ci sono le voci del menu e le impostazioni. Il mondo intanto va avanti (niente pausa).
    /// W/S scegli · A/D cambia un valore · E conferma · Esc chiude (o torna indietro).
    /// Anche col mouse: passandoci sopra si sceglie, clic conferma, [-] [+] o la rotellina cambiano i valori.
    /// Le pagine sono scritte con TextMeshPro e un font a mano (Kalam, licenza OFL).
    /// </summary>
    public class NotebookMenu : MonoBehaviour
    {
        [Tooltip("Il modello del taccuino aperto (le pagine guardano +Y, il lato lontano è +Z)")]
        public GameObject notebookPrefab;
        [Tooltip("Font scritto a mano per le pagine (TextMeshPro ne crea il font asset all'avvio)")]
        public Font handFont;
        TMP_FontAsset handFontAsset;

        public static bool IsOpen { get; private set; }

        FirstPersonController fpc;
        Interactor interactor;
        ProceduralBodyAnimator anim;
        Transform notebook;
        TextMeshProUGUI leftText, rightText;
        Vector2 lastMouse;
        float openT;            // 0 chiuso .. 1 aperto
        bool wantOpen;
        float savedPitch, menuYaw;
        [Tooltip("Il taccuino si apre da solo appena finisce l'intro (fa da menu iniziale)")]
        public bool openOnStart = true;
        /// <summary>Ricominciando da un salvataggio non si riapre da solo.</summary>
        public static bool SuppressAutoOpen;
        bool autoOpened, firstOpen = true;

        enum Page { Main, Settings }
        Page page = Page.Main;
        int cursor;
        int confirmIndex = -1;  // la voce che chiede "sicuro?"

        static readonly Color Ink = new Color(0.12f, 0.16f, 0.42f), RedInk = new Color(0.62f, 0.1f, 0.1f), Pencil = new Color(0.35f, 0.33f, 0.3f);

        void Start()
        {
            GameSettings.Load();
            fpc = GetComponent<FirstPersonController>();
            interactor = GetComponent<Interactor>();
            if (fpc != null && fpc.body != null) anim = fpc.body.GetComponent<ProceduralBodyAnimator>();
            Build();
        }

        // ------------------------------------------------------------------ il taccuino nel mondo

        void Build()
        {
            if (notebookPrefab != null) notebook = Instantiate(notebookPrefab).transform;
            else
            {
                notebook = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                Destroy(notebook.GetComponent<Collider>());
                notebook.localScale = new Vector3(0.28f, 0.015f, 0.2f);
            }
            notebook.name = "Taccuino di Muark";
            foreach (var c in notebook.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var r in notebook.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var t in notebook.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;

            // la lucina da lettura agganciata in cima al taccuino: illumina le pagine
            var lamp = new GameObject("Lucina del taccuino").AddComponent<Light>();
            lamp.transform.SetParent(notebook, false);
            // la luce vera sta più in alto (25 cm sopra le pagine): vicinissima alla carta abbaglierebbe,
            // così invece illumina le due pagine in modo uniforme e tenue
            lamp.transform.localPosition = new Vector3(0.03f, 0.25f, 0.02f);
            lamp.type = LightType.Point;
            lamp.range = 0.6f;
            lamp.intensity = 0.07f;
            lamp.color = new Color(1f, 0.86f, 0.62f);
            lamp.shadows = LightShadows.None;
            leftText = PageText(new Vector3(-0.07f, 0.0215f, 0f));
            rightText = PageText(new Vector3(0.07f, 0.0215f, 0f));
            notebook.gameObject.SetActive(false);
        }

        /// <summary>Una pagina scritta: canvas nel mondo appoggiato sulla carta (12 x 18 cm), testo TextMeshPro a mano.</summary>
        TextMeshProUGUI PageText(Vector3 localPos)
        {
            if (handFontAsset == null && handFont != null) handFontAsset = TMP_FontAsset.CreateFontAsset(handFont);
            var go = new GameObject("Pagina");
            go.layer = 2;
            go.transform.SetParent(notebook, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 175f);
            go.transform.localScale = Vector3.one * 0.001f;

            var textGo = new GameObject("Scritta");
            textGo.layer = 2;
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 6f); trt.offsetMax = new Vector2(-6f, -8f);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (handFontAsset != null) text.font = handFontAsset;
            else text.fontStyle = FontStyles.Italic;
            text.fontSize = 10;
            text.lineSpacing = -14f;
            text.color = Ink;
            text.richText = true;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        // ------------------------------------------------------------------ apertura e chiusura

        bool CanOpen => !IntroSequence.Active && fpc != null && !fpc.inputLocked && !DialogueSystem.IsBlocking;

        void Open()
        {
            wantOpen = true;
            IsOpen = true;
            page = Page.Main;
            cursor = 0;
            confirmIndex = -1;
            savedPitch = fpc.Pitch;
            menuYaw = fpc.Yaw; // il taccuino resta davanti a dove si guardava: la visuale lo raggiunge senza inseguirlo
            fpc.inputLocked = true;
            if (interactor != null) interactor.interactionEnabled = false;
            Cursor.lockState = CursorLockMode.None; // si può usare il mouse sulle pagine
            Cursor.visible = true;
            lastMouse = GameInput.MousePosition;
            notebook.gameObject.SetActive(true);
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
            if (fpc == null || notebook == null) return;

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

            openT = Mathf.MoveTowards(openT, wantOpen ? 1f : 0f, Time.unscaledDeltaTime * 2.2f);
            if (!wantOpen && openT <= 0f)
            {
                IsOpen = false;
                notebook.gameObject.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                fpc.inputLocked = false;
                if (interactor != null) interactor.interactionEnabled = true;
                return;
            }
            if (wantOpen && openT > 0.6f) HandleInput();
        }

        void LateUpdate()
        {
            if (!IsOpen || fpc == null || fpc.cam == null) return;
            float e = openT * openT * (3f - 2f * openT);
            Transform root = fpc.transform;
            Quaternion basis = Quaternion.Euler(0f, menuYaw, 0f);
            Vector3 fwd = basis * Vector3.forward, right = basis * Vector3.right;
            // dalla tasca (fianco destro, in basso) a davanti al petto
            Vector3 pocket = root.position + Vector3.up * 0.9f + right * 0.25f + fwd * 0.1f;
            Vector3 hold = root.position + Vector3.up * 1.38f + fwd * 0.5f;
            notebook.position = Vector3.Lerp(pocket, hold, e);
            notebook.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(Mathf.Lerp(-80f, -58f, e), 0f, Mathf.Lerp(-30f, 0f, e));
            notebook.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, e);

            // la mano destra tiene il taccuino dal bordo destro
            if (anim != null)
            {
                // la mano stringe il bordo destro da fuori (pollice sul bordo): niente braccio attraverso le pagine
                Vector3 grip = notebook.TransformPoint(new Vector3(0.175f, -0.005f, -0.035f));
                Vector3 pole = right * 1f + Vector3.down * 0.7f;
                anim.SetExternalHandIK(1, grip, pole, Mathf.Clamp01(openT * 1.5f), null);
            }

            // la visuale segue il taccuino
            Vector3 dir = notebook.position - fpc.cam.transform.position;
            float wantYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float wantPitch = -Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
            float k = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
            fpc.SetLook(Mathf.LerpAngle(fpc.Yaw, wantYaw, k * e), Mathf.Lerp(fpc.Pitch, wantOpen ? wantPitch : savedPitch, k));
        }

        // ------------------------------------------------------------------ voci del menu

        struct Item { public string label; public Func<string> value; public Action<int> change; public Action confirm; public bool ask; }

        List<Item> Items()
        {
            var list = new List<Item>();
            if (page == Page.Main)
            {
                list.Add(new Item { label = firstOpen ? "Gioca" : "Riprendi", confirm = Close });
                list.Add(new Item { label = "Impostazioni", confirm = () => { page = Page.Settings; cursor = 0; } });
                if (GameSettings.Checkpoint > 0)
                    list.Add(new Item { label = "Ricomincia dall'ultimo punto", ask = true, confirm = () => Restart(GameSettings.Checkpoint) });
                list.Add(new Item { label = "Ricomincia dall'inizio", ask = true, confirm = () => { GameSettings.Checkpoint = 0; Restart(0); } });
                list.Add(new Item { label = "Esci dal gioco", ask = true, confirm = Quit });
            }
            else
            {
                list.Add(new Item { label = "Lingua", value = () => GameSettings.Language == "en" ? "English" : "Italiano",
                                    change = d => GameSettings.Language = GameSettings.Language == "en" ? "it" : "en" });
                list.Add(Percent("Volume generale", () => GameSettings.Master, v => GameSettings.Master = v));
                list.Add(Percent("Voci", () => GameSettings.Voices, v => GameSettings.Voices = v));
                list.Add(Percent("Musica e TV", () => GameSettings.Music, v => GameSettings.Music = v));
                list.Add(Percent("Effetti", () => GameSettings.Effects, v => GameSettings.Effects = v));
                list.Add(new Item { label = "Sensibilità mouse", value = () => $"{GameSettings.Sensitivity:0.0}x",
                                    change = d => GameSettings.Sensitivity = Mathf.Clamp(Mathf.Round((GameSettings.Sensitivity + d * 0.1f) * 10f) / 10f, 0.3f, 2f) });
                list.Add(new Item { label = "Luminosità", value = () => (GameSettings.Brightness > 0 ? "+" : "") + GameSettings.Brightness,
                                    change = d => GameSettings.Brightness = Mathf.Clamp(GameSettings.Brightness + d, -5, 5) });
                list.Add(new Item { label = "Qualità grafica", value = () => new[] { "bassa", "media", "alta" }[GameSettings.Quality],
                                    change = d => GameSettings.Quality = Mathf.Clamp(GameSettings.Quality + d, 0, 2) });
                list.Add(new Item { label = "Sottotitoli", value = () => GameSettings.Subtitles ? "sì" : "no", change = d => GameSettings.Subtitles = !GameSettings.Subtitles });
                list.Add(new Item { label = "Testo", value = () => new[] { "piccolo", "medio", "grande" }[GameSettings.TextSize],
                                    change = d => GameSettings.TextSize = Mathf.Clamp(GameSettings.TextSize + d, 0, 2) });
                list.Add(new Item { label = "Indietro", confirm = () => { page = Page.Main; cursor = 1; } });
            }
            return list;
        }

        static Item Percent(string label, Func<float> get, Action<float> set) => new Item
        {
            label = label,
            value = () => Mathf.RoundToInt(get() * 100f) + "%",
            change = d => set(Mathf.Clamp01(Mathf.Round((get() + d * 0.1f) * 10f) / 10f)),
        };

        void HandleInput()
        {
            var items = Items();
            if (GameInput.MenuToggle)
            {
                if (page == Page.Settings) { page = Page.Main; cursor = 1; Redraw(); Scribble(); }
                else Close();
                return;
            }
            int before = cursor;
            if (GameInput.MenuUp) cursor = (cursor + items.Count - 1) % items.Count;
            if (GameInput.MenuDown) cursor = (cursor + 1) % items.Count;
            if (cursor != before) { confirmIndex = -1; Scribble(); }

            // mouse: quale voce (o [-] / [+]) c'è sotto il puntatore
            int mouseItem = -1, mouseStep = 0;
            Vector2 mp = GameInput.MousePosition;
            int link = TMP_TextUtilities.FindIntersectingLink(rightText, mp, fpc.cam);
            if (link >= 0 && link < rightText.textInfo.linkCount)
            {
                string id = rightText.textInfo.linkInfo[link].GetLinkID();
                mouseStep = id.EndsWith("-") ? -1 : id.EndsWith("+") ? 1 : 0;
                int.TryParse(id.TrimEnd('-', '+'), out mouseItem);
                if ((mp - lastMouse).sqrMagnitude > 1f && mouseItem != cursor && mouseItem < items.Count)
                {
                    cursor = mouseItem; confirmIndex = -1; Scribble();
                }
            }
            lastMouse = mp;

            Item it = items[Mathf.Clamp(cursor, 0, items.Count - 1)];
            int delta = GameInput.MenuLeft ? -1 : GameInput.MenuRight ? 1 : 0;
            if (mouseItem == cursor && it.change != null && Mathf.Abs(GameInput.Scroll) > 0.01f) delta = GameInput.Scroll > 0f ? 1 : -1;
            bool click = GameInput.LeftDown && mouseItem == cursor;
            if (click && mouseStep != 0 && it.change != null) { delta = mouseStep; click = false; }
            if (delta != 0 && it.change != null) { it.change(delta); GameSettings.Save(); Scribble(); }
            if (GameInput.MenuConfirm || click)
            {
                if (it.change != null && it.confirm == null) { it.change(1); GameSettings.Save(); Scribble(); }
                else if (it.ask && confirmIndex != cursor) { confirmIndex = cursor; Scribble(); }
                else if (it.confirm != null) { it.confirm(); confirmIndex = -1; Scribble(); }
            }
            Redraw();
        }

        void Scribble() => HorrorAudio.Play2D(ProceduralAudio.Click(), 0.25f, UnityEngine.Random.Range(0.5f, 0.7f));

        // ------------------------------------------------------------------ le pagine

        void Redraw()
        {
            if (leftText == null) return;
            string pencil = ColorUtility.ToHtmlStringRGB(Pencil);
            string objective = FantaLandia.Horror.Story.StoryDirector.Objective;
            leftText.text =
                "<size=15><b>" + Loc.T("Taccuino di Muark") + "</b></size>\n" +
                $"<color=#{pencil}>" + Loc.T("venerdì sera") + "</color>\n\n" +
                (string.IsNullOrEmpty(objective) ? "" : "<size=8><color=#" + pencil + ">" + Loc.T("da fare:") + "</color></size>\n- " + Loc.T(objective) + "\n") +
                $"\n\n\n\n\n<size=6><color=#{pencil}>" + Loc.T("W/S o mouse: scegli\nA/D, [-] [+], rotellina: cambia\nE o clic: conferma · Esc: chiudi") + "</color></size>";

            var items = Items();
            var sb = new System.Text.StringBuilder();
            bool main = page == Page.Main;
            sb.Append(main ? "<size=14><b>" + Loc.T("Menu") + "</b></size>\n\n" : "<size=12><b>" + Loc.T("Impostazioni") + "</b></size>\n");
            string red = ColorUtility.ToHtmlStringRGB(RedInk);
            for (int i = 0; i < items.Count; i++)
            {
                bool sel = i == cursor;
                // ogni voce è un link (per il mouse); le impostazioni su una riga sola, valore incolonnato a destra
                sb.Append(sel ? $"<color=#{red}>»</color> " : "  ");
                string lab = Loc.T(items[i].label);
                sb.Append($"<link=\"{i}\">").Append(sel ? "<b>" + lab + "</b>" : lab).Append("</link>");
                if (items[i].value != null)
                    sb.Append($"<pos=60%><link=\"{i}-\">[-]</link> <b>").Append(Loc.T(items[i].value())).Append($"</b> <link=\"{i}+\">[+]</link>");
                if (sel && confirmIndex == i) sb.Append($"\n    <color=#{red}>" + Loc.T("sicuro? conferma di nuovo") + "</color>");
                sb.Append(main ? "\n\n" : "\n");
            }
            rightText.text = sb.ToString();
            rightText.fontSize = main ? 10 : 7;
            rightText.enableWordWrapping = main;
            rightText.ForceMeshUpdate();
        }

        // ------------------------------------------------------------------ ricominciare e uscire

        void Restart(int startAt)
        {
            SuppressAutoOpen = true;
            FantaLandia.Horror.Story.StoryDirector.PendingStart = startAt;
            IntroSequence.SkipLogo = true;
            IsOpen = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0 ? SceneManager.GetActiveScene().buildIndex : 0);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnDestroy() => IsOpen = false;
    }
}
