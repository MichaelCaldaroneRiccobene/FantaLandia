using UnityEngine;

namespace FantaLandia.Horror.UI
{
    /// <summary>
    /// Riquadro di tutorial in alto a sinistra (es. i tasti della torcia): compare, resta qualche secondo e svanisce.
    /// Si crea da solo alla prima chiamata.
    /// </summary>
    public class TutorialHint : MonoBehaviour
    {
        static TutorialHint instance;
        string title, body;
        float shownAt = -100f, duration;
        GUIStyle titleStyle, bodyStyle;

        public static void Show(string title, string body, float seconds = 9f)
        {
            if (instance == null) instance = new GameObject("Tutorial").AddComponent<TutorialHint>();
            instance.title = title;
            instance.body = body;
            instance.duration = seconds;
            instance.shownAt = Time.time;
        }

        void OnGUI()
        {
            float t = Time.time - shownAt;
            if (t > duration || string.IsNullOrEmpty(body)) return;
            float a = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((duration - t) / 0.8f);
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, richText = true };
                bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
            }
            int lines = body.Split('\n').Length;
            var box = new Rect(20f, 20f, 360f, 40f + lines * 22f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f * a);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(0.91f, 0.85f, 0.66f, a);
            GUI.DrawTexture(new Rect(box.x, box.y, 3f, box.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(box.x + 14f, box.y + 8f, box.width - 20f, 24f), title, titleStyle);
            GUI.Label(new Rect(box.x + 14f, box.y + 32f, box.width - 20f, lines * 22f + 4f), body, bodyStyle);
            GUI.color = Color.white;
        }
    }
}
