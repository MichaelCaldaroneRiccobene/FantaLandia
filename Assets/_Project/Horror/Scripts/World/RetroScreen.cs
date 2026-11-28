using FantaLandia.Core;
using UnityEngine;

namespace FantaLandia.Horror.World
{
    /// <summary>
    /// Look retro stile PS1: la camera disegna su un'immagine a bassa risoluzione che viene ingrandita
    /// a pixel netti. F2 attiva/disattiva l'effetto.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class RetroScreen : MonoBehaviour
    {
        [Tooltip("Altezza in pixel dell'immagine (240 = PS1)")]
        public int verticalResolution = 300;
        [Tooltip("Acceso di serie; si toglie dalle impostazioni del taccuino (o con F2)")]
        public bool retroEnabled = true;

        Camera cam;
        RenderTexture rt;
        Camera clearCam;

        void Awake()
        {
            cam = GetComponent<Camera>();
            // una camera vuota che pulisce lo schermo (la camera vera disegna nell'immagine piccola)
            var go = new GameObject("Pulizia schermo");
            go.transform.SetParent(transform, false);
            clearCam = go.AddComponent<Camera>();
            clearCam.cullingMask = 0;
            clearCam.clearFlags = CameraClearFlags.SolidColor;
            clearCam.backgroundColor = Color.black;
            clearCam.depth = cam.depth - 10;
        }

        void Update()
        {
            if (GameInput.RetroToggle) { FantaLandia.Horror.UI.GameSettings.Retro = !FantaLandia.Horror.UI.GameSettings.Retro; FantaLandia.Horror.UI.GameSettings.Save(); }
            FantaLandia.Horror.UI.GameSettings.Load();
            // con il taccuino aperto l'immagine è piena: si legge bene e il mouse clicca nel punto giusto
            retroEnabled = FantaLandia.Horror.UI.GameSettings.Retro && !FantaLandia.Horror.UI.NotebookMenu.IsOpen;

            if (!retroEnabled)
            {
                cam.targetTexture = null;
                return;
            }
            int h = Mathf.Max(120, verticalResolution);
            int w = Mathf.Max(160, Mathf.RoundToInt(h * Screen.width / (float)Mathf.Max(1, Screen.height)));
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) { cam.targetTexture = null; rt.Release(); Destroy(rt); }
                rt = new RenderTexture(w, h, 24) { filterMode = FilterMode.Point, name = "Retro" };
            }
            cam.targetTexture = rt;
        }

        void OnGUI()
        {
            if (!retroEnabled || rt == null || Event.current.type != EventType.Repaint) return;
            GUI.depth = 100; // sotto a tutta l'interfaccia
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), rt, ScaleMode.StretchToFill, false);
        }

        void OnDisable()
        {
            if (cam != null) cam.targetTexture = null;
        }
    }
}
