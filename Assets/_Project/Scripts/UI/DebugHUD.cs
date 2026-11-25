using FantaLandia.CameraSystem;
using FantaLandia.Characters;
using FantaLandia.Core;
using FantaLandia.Player;
using FantaLandia.World;
using UnityEngine;

namespace FantaLandia.UI
{
    /// <summary>
    /// Pannello di debug del prototipo (solo italiano: la localizzazione vera arriva con l'MVP).
    /// Gestisce anche i tasti di debug: V = modalità muri, F1 = aiuto.
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        bool showHelp = true;
        RTSCameraRig rig;
        GUIStyle style;

        void Start() => rig = FindFirstObjectByType<RTSCameraRig>();

        void Update()
        {
            if (GameInput.HelpToggle) showHelp = !showHelp;
            if (GameInput.WallModeCycle)
                BuildingVisibility.Mode = (BuildingVisibility.WallMode)(((int)BuildingVisibility.Mode + 1) % 3);
        }

        void OnGUI()
        {
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };

            var sel = SelectionController.Instance;
            var focus = sel != null ? sel.Focus : null;

            GUILayout.BeginArea(new Rect(10, 10, 380, Screen.height - 20));
            GUILayout.BeginVertical(GUI.skin.box);

            if (focus != null)
            {
                var m = focus.Motor;
                GUILayout.Label($"<b>{focus.displayName}</b>  ({sel.Selected.Count} selezionati)", style);
                GUILayout.Label($"Andatura: {(m.IsRunning ? "Corsa" : "Camminata")}  —  {m.CurrentSpeed:0.0} m/s", style);
                GUILayout.Label($"Azione: {ActionName(m)}", style);
                if (Time.time - m.LastLandingTime < 4f)
                {
                    string hard = m.LastFallHeight >= m.hardLandingHeight ? "  <color=#ff8040>DURO</color>" : "";
                    GUILayout.Label($"Atterraggio da {m.LastFallHeight:0.0} m{hard}", style);
                }
            }

            var b = BuildingVisibility.Current;
            GUILayout.Label($"Posizione: {(b != null ? b.CurrentFloorName : "All'aperto")}", style);
            GUILayout.Label($"Muri: {WallModeName(BuildingVisibility.Mode)}", style);
            if (rig != null) GUILayout.Label($"Camera: {(rig.IsFollowing ? "segue il personaggio" : "libera")}", style);

            if (showHelp)
            {
                GUILayout.Space(6);
                GUILayout.Label(
                    "<b>Comandi</b> (F1 nasconde)\n" +
                    "Clic sinistro / trascina: seleziona  (Shift aggiunge)\n" +
                    "1-9: seleziona un membro del party\n" +
                    "Clic destro: muovi\n" +
                    "Alt + clic destro: consenti salti pericolosi\n" +
                    "R: camminata / corsa\n" +
                    "WASD / frecce: sposta camera\n" +
                    "Q / E o tasto centrale: ruota camera\n" +
                    "Rotellina: zoom\n" +
                    "F: segui on/off   Spazio: centra\n" +
                    "V: modalità muri", style);
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        static string ActionName(CharacterMotor m)
        {
            if (m.IsStunned) return "Stordito dall'atterraggio";
            switch (m.CurrentTraversal)
            {
                case TraversalKind.Vault: return "Scavalca";
                case TraversalKind.Climb: return "Si arrampica oltre";
                case TraversalKind.ClimbHigh: return "Arrampicata alta";
                case TraversalKind.ClimbUp: return "Si arrampica su";
                case TraversalKind.Drop: return "Salta giù";
            }
            return m.CurrentSpeed > 0.1f ? "In movimento" : "Fermo";
        }

        static string WallModeName(BuildingVisibility.WallMode mode)
        {
            switch (mode)
            {
                case BuildingVisibility.WallMode.Full: return "pieni";
                case BuildingVisibility.WallMode.FrontCutaway: return "tagliati verso la camera";
                default: return "abbassati (solo pavimenti)";
            }
        }
    }
}
