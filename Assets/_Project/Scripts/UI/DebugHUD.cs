using FantaLandia.CameraSystem;
using FantaLandia.Characters;
using FantaLandia.Characters.Body;
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
            var focus = SelectionController.Instance != null ? SelectionController.Instance.Focus : null;
            var body = focus != null ? focus.GetComponentInChildren<BodyRig>() : null;
            if (body != null && GameInput.DebugSever) body.SeverRandomLimb();
            if (body != null && GameInput.DebugRestore) body.RestoreAll();

            if (GameInput.WallModeCycle)
                BuildingVisibility.Mode = (BuildingVisibility.WallMode)(((int)BuildingVisibility.Mode + 1) % 2);
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
                string cap = m.SpeedCap > 0f ? $"  (gruppo: max {m.SpeedCap:0.0})" : "";
                GUILayout.Label($"Andatura: {GaitName(m.CurrentGait)}  —  {m.CurrentSpeed:0.0} m/s{cap}", style);
                if (m.IsConspicuous) GUILayout.Label("<color=#ffb040>Scatto: dà nell'occhio in città</color>", style);
                GUILayout.Label($"Azione: {ActionName(m)}", style);
                if (Time.time - m.LastLandingTime < 4f)
                {
                    string hard = m.LastFallHeight >= m.hardLandingHeight ? "  <color=#ff8040>DURO</color>" : "";
                    GUILayout.Label($"Atterraggio da {m.LastFallHeight:0.0} m{hard}", style);
                }
            }

            if (sel != null) GUILayout.Label($"Movimento di gruppo: {(sel.GroupPace ? "sì, alla velocità del più lento" : "no")}", style);

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
                    "Clic destro: muovi (tienilo premuto per guidare)\n" +
                    "Alt + clic destro: consenti salti pericolosi\n" +
                    "R: andatura (cammina / corsa leggera / scatto)\n" +
                    "G: movimento di gruppo on/off\n" +
                    "WASD / frecce: sposta camera\n" +
                    "Q / E o tasto centrale: ruota camera\n" +
                    "Rotellina: zoom\n" +
                    "F: segui on/off   Spazio: centra\n" +
                    "V: modalità muri\n" +
                    "K / L: (debug) stacca / riattacca un arto", style);
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

        static string WallModeName(BuildingVisibility.WallMode mode) =>
            mode == BuildingVisibility.WallMode.Full ? "pieni" : "tagliati verso la camera";

        static string GaitName(Gait gait)
        {
            switch (gait)
            {
                case Gait.Walk: return "Cammina";
                case Gait.Sprint: return "Scatto";
                default: return "Corsa leggera";
            }
        }
    }
}
