using System.Collections.Generic;
using UnityEngine;

namespace FantaLandia.Horror.UI
{
    /// <summary>
    /// Lo stile comune dell'interfaccia a schermo (sottotitoli, scelte, scritte di interazione, tutorial, titoli):
    /// font scritto a mano ma leggibile (Patrick Hand, licenza OFL), pannelli scuri con angoli arrotondati e bordi
    /// morbidi, tasti disegnati come tasti veri, post-it per i tutorial. Le texture si generano dal codice.
    /// </summary>
    public static class UiStyle
    {
        static Font hand;
        public static Font Hand
        {
            get
            {
                if (hand == null) hand = Resources.Load<Font>("Fonts/PatrickHand-Regular");
                return hand;
            }
        }

        public static readonly Color Paper = new Color(0.96f, 0.93f, 0.84f);
        public static readonly Color Ink = new Color(0.14f, 0.16f, 0.32f);
        public static readonly Color Accent = new Color(0.93f, 0.8f, 0.52f);

        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, GUIStyle> styles = new Dictionary<string, GUIStyle>();

        /// <summary>Rettangolo arrotondato bianco con bordo morbido (si colora con GUI.color), 9-slice.</summary>
        static Texture2D Rounded(int radius, float softness)
        {
            string key = "r" + radius + "_" + softness;
            if (textures.TryGetValue(key, out Texture2D t) && t != null) return t;
            int size = radius * 2 + 4;
            t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[size * size];
            float c = size * 0.5f - 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x - c) - (c - radius)), dy = Mathf.Max(0f, Mathf.Abs(y - c) - (c - radius));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((radius - d) / Mathf.Max(1f, softness));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px);
            t.Apply();
            textures[key] = t;
            return t;
        }

        static GUIStyle PanelStyle(int radius, float softness)
        {
            string key = "p" + radius + "_" + softness;
            if (styles.TryGetValue(key, out GUIStyle st)) return st;
            st = new GUIStyle { border = new RectOffset(radius + 2, radius + 2, radius + 2, radius + 2) };
            st.normal.background = Rounded(radius, softness);
            styles[key] = st;
            return st;
        }

        /// <summary>Pannello arrotondato del colore dato.</summary>
        public static void Panel(Rect r, Color c, int radius = 12, float softness = 1.5f)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = c;
            PanelStyle(radius, softness).Draw(r, false, false, false, false);
            GUI.color = old;
        }

        /// <summary>Pannello con un'ombra morbida sotto (per post-it e riquadri importanti).</summary>
        public static void PanelWithShadow(Rect r, Color c, int radius = 10)
        {
            Panel(new Rect(r.x + 3f, r.y + 5f, r.width, r.height), new Color(0f, 0f, 0f, 0.35f * c.a), radius + 6, 10f);
            Panel(r, c, radius);
        }

        /// <summary>Stile di testo col font a mano (in cache).</summary>
        public static GUIStyle Text(int size, TextAnchor anchor = TextAnchor.UpperLeft, bool wrap = true)
        {
            string key = "t" + size + "_" + anchor + "_" + wrap;
            if (styles.TryGetValue(key, out GUIStyle st)) return st;
            st = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, wordWrap = wrap, richText = true, clipping = TextClipping.Overflow };
            if (Hand != null) st.font = Hand;
            st.normal.textColor = Color.white;
            styles[key] = st;
            return st;
        }

        /// <summary>Testo con un'ombra morbida dietro (leggibile su qualsiasi sfondo).</summary>
        public static void Shadowed(Rect r, string text, GUIStyle style, Color color, float shadow = 0.85f)
        {
            var old = GUI.color;
            string plain = System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", "");
            GUI.color = new Color(0f, 0f, 0f, shadow * color.a * 0.6f);
            GUI.Label(new Rect(r.x + 1f, r.y + 2f, r.width, r.height), plain, style);
            GUI.Label(new Rect(r.x - 1f, r.y + 1f, r.width, r.height), plain, style);
            GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = old;
        }

        /// <summary>Un tasto della tastiera disegnato (es. "E"): restituisce la larghezza usata.</summary>
        public static float KeyCap(float x, float y, string key, float height = 26f, float alpha = 1f)
        {
            var st = Text(Mathf.RoundToInt(height * 0.62f), TextAnchor.MiddleCenter, false);
            float w = Mathf.Max(height, st.CalcSize(new GUIContent(key)).x + height * 0.5f);
            Panel(new Rect(x, y + 2f, w, height), new Color(0f, 0f, 0f, 0.45f * alpha), 6);       // spessore del tasto
            Panel(new Rect(x, y, w, height), new Color(0.93f, 0.9f, 0.82f, alpha), 6);
            Panel(new Rect(x + 2f, y + 2f, w - 4f, height - 6f), new Color(1f, 0.98f, 0.93f, alpha), 5);
            var old = GUI.color;
            GUI.color = new Color(Ink.r, Ink.g, Ink.b, alpha);
            GUI.Label(new Rect(x, y - 1f, w, height), key, st);
            GUI.color = old;
            return w;
        }

        /// <summary>Puntino morbido (mirino).</summary>
        public static void Dot(Vector2 center, float size, Color c)
        {
            Panel(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), c, Mathf.Max(2, Mathf.RoundToInt(size * 0.5f)), 1.2f);
        }
    }
}
