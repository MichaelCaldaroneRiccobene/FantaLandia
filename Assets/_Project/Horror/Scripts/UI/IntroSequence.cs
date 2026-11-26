using UnityEngine;

namespace FantaLandia.Horror.UI
{
    /// <summary>
    /// L'avvio del gioco: schermo nero, il logo 641 compare (1 s), resta (2 s), sparisce (0.5 s),
    /// poi dal nero si passa al gioco in 5 s. Finché non è finita non ci si muove e non si apre il menu.
    /// Ricominciando da un salvataggio il logo si salta e resta solo una dissolvenza breve.
    /// </summary>
    public class IntroSequence : MonoBehaviour
    {
        [Tooltip("Immagine del logo (se vuota viene scritto \"641\")")]
        public Texture2D logo;
        public string logoText = "641";
        public float blackStart = 0.5f, logoIn = 1f, logoHold = 2f, logoOut = 0.5f, fadeToGame = 5f;

        /// <summary>True finché l'intro è in corso (giocatore e menu bloccati).</summary>
        public static bool Active { get; private set; }
        /// <summary>Si salta il logo (ricominciando da un salvataggio).</summary>
        public static bool SkipLogo;

        float start;
        GUIStyle logoStyle, lineStyle;

        void Awake()
        {
            Active = true;
            start = Time.unscaledTime;
            if (SkipLogo) { blackStart = 0.3f; logoIn = logoHold = logoOut = 0f; fadeToGame = 1.5f; SkipLogo = false; }
        }

        void OnDestroy() => Active = false;

        float T => Time.unscaledTime - start;

        void Update()
        {
            if (Active && T > blackStart + logoIn + logoHold + logoOut + fadeToGame)
            {
                Active = false;
                enabled = false;
                TutorialHint.Show("Comandi", "<b>WASD</b>  muoviti   <b>Shift</b>  corri   <b>C</b>  abbassati\n<b>E</b>  interagisci   <b>Spazio</b>  salta una battuta\n<b>Esc</b>  il taccuino (menu e impostazioni)", 10f);
            }
        }

        void OnGUI()
        {
            float t = T;
            float logoEnd = blackStart + logoIn + logoHold + logoOut;
            float black = t < logoEnd ? 1f : 1f - Mathf.Clamp01((t - logoEnd) / Mathf.Max(0.01f, fadeToGame));
            if (black <= 0f) return;
            GUI.depth = -100; // sopra a tutto
            GUI.color = new Color(0f, 0f, 0f, black * black * (3f - 2f * black));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

            float a = 0f;
            if (t > blackStart && t < logoEnd)
            {
                float lt = t - blackStart;
                a = lt < logoIn ? lt / logoIn : lt < logoIn + logoHold ? 1f : 1f - (lt - logoIn - logoHold) / Mathf.Max(0.01f, logoOut);
            }
            if (a <= 0f) { GUI.color = Color.white; return; }

            if (logo != null)
            {
                float h = Screen.height * 0.35f, w = h * logo.width / Mathf.Max(1f, logo.height);
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.DrawTexture(new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h), logo, ScaleMode.ScaleToFit);
            }
            else
            {
                if (logoStyle == null)
                {
                    logoStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                    lineStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                }
                logoStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.2f);
                var r = new Rect(0f, Screen.height * 0.5f - Screen.height * 0.16f, Screen.width, Screen.height * 0.3f);
                // un filo di bagliore dietro alle cifre
                GUI.color = new Color(1f, 0.85f, 0.55f, a * 0.18f);
                for (int i = 0; i < 4; i++) GUI.Label(new Rect(r.x + (i % 2 == 0 ? -3 : 3), r.y + (i < 2 ? -3 : 3), r.width, r.height), logoText, logoStyle);
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.Label(r, logoText, logoStyle);
                GUI.color = new Color(1f, 1f, 1f, a * 0.6f);
                GUI.DrawTexture(new Rect(Screen.width * 0.42f, r.yMax + 4f, Screen.width * 0.16f, 2f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
    }
}
