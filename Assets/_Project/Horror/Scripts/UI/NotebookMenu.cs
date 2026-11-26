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
using UnityEngine.UI;

namespace FantaLandia.Horror.UI
{
    /// <summary>
    /// Il menu è il taccuino di Muark: con Esc (o Tab) lo tira fuori con la mano destra, la visuale si abbassa e lo segue,
    /// e sulle pagine scritte a mano ci sono le voci del menu e le impostazioni. Il mondo intanto va avanti (niente pausa).
    /// W/S scegli · A/D cambia un valore · E conferma · Esc chiude (o torna indietro).
    /// </summary>
    public class NotebookMenu : MonoBehaviour
    {
        [Tooltip("Il modello del taccuino aperto (le pagine guardano +Y, il lato lontano è +Z)")]
        public GameObject notebookPrefab;

        public static bool IsOpen { get; private set; }

        FirstPersonController fpc;
        Interactor interactor;
        ProceduralBodyAnimator anim;
        Transform notebook;
        Text leftText, rightText;
        float openT;            // 0 chiuso .. 1 aperto
        bool wantOpen;
        float savedPitch;

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

            leftText = PageText(new Vector3(-0.07f, 0.0215f, 0f));
            rightText = PageText(new Vector3(0.07f, 0.0215f, 0f));
            notebook.gameObject.SetActive(false);
        }

        /// <summary>Una pagina scritta: canvas nel mondo appoggiato sulla carta (12 x 18 cm).</summary>
        Text PageText(Vector3 localPos)
        {
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
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 9;
            text.fontStyle = FontStyle.Italic;      // "scritto a mano"
            text.lineSpacing = 1.12f;
            text.color = Ink;
            text.supportRichText = true;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
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
            fpc.inputLocked = true;
            if (interactor != null) interactor.interactionEnabled = false;
            notebook.gameObject.SetActive(true);
            HorrorAudio.Play2D(ProceduralAudio.PickUp(), 0.5f, 1.2f);
            Redraw();
        }

        void Close()
        {
            wantOpen = false;
            confirmIndex = -1;
            HorrorAudio.Play2D(ProceduralAudio.PickUp(), 0.4f, 0.9f);
        }

        void Update()
        {
            if (fpc == null || notebook == null) return;

            if (!IsOpen)
            {
                if (GameInput.MenuToggle && CanOpen) Open();
                return;
            }

            openT = Mathf.MoveTowards(openT, wantOpen ? 1f : 0f, Time.unscaledDeltaTime * 2.2f);
            if (!wantOpen && openT <= 0f)
            {
                IsOpen = false;
                notebook.gameObject.SetActive(false);
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
            Vector3 fwd = root.forward, right = root.right;
            // dalla tasca (fianco destro, in basso) a davanti al petto
            Vector3 pocket = root.position + Vector3.up * 0.9f + right * 0.25f + fwd * 0.1f;
            Vector3 hold = root.position + Vector3.up * 1.18f + fwd * 0.4f + right * 0.03f;
            notebook.position = Vector3.Lerp(pocket, hold, e);
            notebook.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(Mathf.Lerp(-80f, -42f, e), 0f, Mathf.Lerp(-30f, 0f, e));
            notebook.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, e);

            // la mano destra tiene il taccuino dal bordo destro
            if (anim != null)
            {
                Vector3 grip = notebook.TransformPoint(new Vector3(0.12f, -0.025f, -0.03f));
                Vector3 pole = right * 0.8f + Vector3.down * 0.6f - fwd * 0.2f;
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
                list.Add(new Item { label = "Riprendi", confirm = Close });
                list.Add(new Item { label = "Impostazioni", confirm = () => { page = Page.Settings; cursor = 0; } });
                if (GameSettings.Checkpoint > 0)
                    list.Add(new Item { label = "Ricomincia dall'ultimo punto", ask = true, confirm = () => Restart(GameSettings.Checkpoint) });
                list.Add(new Item { label = "Ricomincia dall'inizio", ask = true, confirm = () => { GameSettings.Checkpoint = 0; Restart(0); } });
                list.Add(new Item { label = "Esci dal gioco", ask = true, confirm = Quit });
            }
            else
            {
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

            Item it = items[Mathf.Clamp(cursor, 0, items.Count - 1)];
            int delta = GameInput.MenuLeft ? -1 : GameInput.MenuRight ? 1 : 0;
            if (delta != 0 && it.change != null) { it.change(delta); GameSettings.Save(); Scribble(); }
            if (GameInput.MenuConfirm)
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
            leftText.text =
                $"<size=13><b>Taccuino di Muark</b></size>\n" +
                $"<color=#{ColorUtility.ToHtmlStringRGB(Pencil)}>venerdì sera</color>\n\n" +
                "- portare la birra da Tom\n" +
                "- <color=#" + ColorUtility.ToHtmlStringRGB(RedInk) + ">NON</color> farsi fregare da Roy\n" +
                "- chiedere a Jef se sta bene\n\n" +
                (page == Page.Settings ? "<size=11><b>Impostazioni</b></size>\n" : "") +
                $"\n\n\n\n<size=7><color=#{ColorUtility.ToHtmlStringRGB(Pencil)}>W/S scegli · A/D cambia\nE conferma · Esc chiudi</color></size>";

            var items = Items();
            var sb = new System.Text.StringBuilder();
            sb.Append(page == Page.Main ? "<size=12><b>Menu</b></size>\n\n" : "<size=12><b>Impostazioni</b></size>\n\n");
            for (int i = 0; i < items.Count; i++)
            {
                bool sel = i == cursor;
                string label = items[i].label;
                if (items[i].value != null) label += ":  <b>" + items[i].value() + "</b>";
                if (sel && confirmIndex == i) label += "  <color=#" + ColorUtility.ToHtmlStringRGB(RedInk) + ">sicuro? E di nuovo</color>";
                if (sel) sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(RedInk)).Append(">» </color>").Append("<b>").Append(label).Append("</b>");
                else sb.Append("   ").Append(label);
                sb.Append(page == Page.Main ? "\n\n" : "\n");
            }
            rightText.text = sb.ToString();
            rightText.fontSize = page == Page.Main ? 10 : 8;
        }

        // ------------------------------------------------------------------ ricominciare e uscire

        void Restart(int startAt)
        {
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
