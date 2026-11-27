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
            instance.title = Loc.T(title);
            instance.body = Loc.T(body);
            instance.duration = seconds;
            instance.shownAt = Time.time;
        }

        void OnGUI()
        {
            float t = Time.time - shownAt;
            if (t > duration || string.IsNullOrEmpty(body)) return;
            float a = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((duration - t) / 0.8f);
            GUI.depth = -20;
            // un post-it giallo appiccicato in alto a sinistra, un po' storto, scritto a penna
            var titleSt = UiStyle.Text(24, TextAnchor.UpperLeft, false);
            var bodySt = UiStyle.Text(19, TextAnchor.UpperLeft, true);
            float w = 430f;
            float bh = bodySt.CalcHeight(new GUIContent(body), w - 40f);
            var box = new Rect(26f, 24f - (1f - a) * 20f, w, 58f + bh);
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(-1.5f, box.center);
            UiStyle.PanelWithShadow(box, new Color(0.98f, 0.92f, 0.6f, a), 6);
            UiStyle.Panel(new Rect(box.x, box.y, box.width, 14f), new Color(0.93f, 0.86f, 0.5f, a), 6);     // striscia adesiva
            var ink = new Color(UiStyle.Ink.r, UiStyle.Ink.g, UiStyle.Ink.b, a);
            GUI.color = ink;
            GUI.Label(new Rect(box.x + 20f, box.y + 16f, box.width - 40f, 30f), title, titleSt);
            GUI.Label(new Rect(box.x + 20f, box.y + 48f, box.width - 40f, bh + 4f), body, bodySt);
            GUI.color = Color.white;
            GUI.matrix = old;
        }
    }
}
