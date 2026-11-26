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
            var health = focus != null ? focus.GetComponent<BodyHealth>() : null;
            if (health != null)
            {
                if (GameInput.DebugHit) DebugHit(health);
                if (GameInput.DebugSever) DebugCut(health);
                if (GameInput.DebugHeal) health.HealMagic(60f);
                if (GameInput.DebugProsthetic) health.AttachAllProsthetics();
                if (GameInput.DebugRestore) health.RestoreAll();
            }

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
                    "Clic sinistro su un personaggio: seleziona (Shift aggiunge)\n" +
                    "Ctrl + trascina: selezione a riquadro   Esc: deseleziona\n" +
                    "1-9: seleziona un membro del party\n" +
                    "Clic sinistro sul terreno: muovi (tienilo premuto per guidare)\n" +
                    "Alt + clic sinistro: consenti salti pericolosi\n" +
                    "R: andatura (cammina / corsa leggera / scatto)\n" +
                    "G: movimento di gruppo on/off\n" +
                    "WASD / frecce: sposta camera\n" +
                    "Q / E o tasto centrale: ruota camera\n" +
                    "Rotellina: zoom\n" +
                    "F: segui on/off   Spazio: centra\n" +
                    "V: modalità muri\n" +
                    "J: (debug) colpo contundente a caso\n" +
                    "K: (debug) fendente che stacca un arto\n" +
                    "H: cura magica   P: protesi sugli arti staccati\n" +
                    "L: (debug) tutto come nuovo", style);
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();

            if (focus != null)
            {
                var health = focus.GetComponent<BodyHealth>();
                if (health != null) DrawBody(focus.displayName, health);
            }
        }

        // ------------------------------------------------------------------ corpo

        static readonly BodyPart[] DisplayOrder =
        {
            BodyPart.Head, BodyPart.Neck, BodyPart.Chest, BodyPart.Abdomen, BodyPart.Pelvis,
            BodyPart.UpperArmL, BodyPart.ForearmL, BodyPart.HandL,
            BodyPart.UpperArmR, BodyPart.ForearmR, BodyPart.HandR,
            BodyPart.ThighL, BodyPart.ShinL, BodyPart.FootL,
            BodyPart.ThighR, BodyPart.ShinR, BodyPart.FootR,
        };

        void DrawBody(string name, BodyHealth h)
        {
            GUILayout.BeginArea(new Rect(Screen.width - 290, 10, 280, Screen.height - 20));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"<b>Corpo di {name}</b>", style);
            if (h.IsUnconscious) GUILayout.Label("<color=#ff5050><b>SVENUTO</b></color>", style);
            if (h.LimpSide != 0) GUILayout.Label($"<color=#ffb040>Zoppica ({(h.LimpSide < 0 ? "gamba sinistra" : "gamba destra")})</color>", style);

            foreach (BodyPart p in DisplayOrder)
            {
                var ph = h.Get(p);
                PartState st = h.State(p);
                GUILayout.BeginHorizontal();
                GUILayout.Label(BodyRig.DisplayName(p), style, GUILayout.Width(140));
                Rect r = GUILayoutUtility.GetRect(80, 14, GUILayout.Width(80));
                r.y += 4f;
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                float frac = st == PartState.Severed ? 0f : Mathf.Clamp01(ph.hp / ph.max);
                GUI.color = StateColor(st, frac);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * frac, r.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUILayout.Label(StateName(st, ph), style);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        static Color StateColor(PartState st, float frac)
        {
            if (st == PartState.Prosthetic) return new Color(0.6f, 0.65f, 0.75f);
            return Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.3f, 0.85f, 0.3f), frac);
        }

        static string StateName(PartState st, BodyHealth.PartHealth ph)
        {
            switch (st)
            {
                case PartState.Severed: return "<color=#ff5050>staccato</color>";
                case PartState.Disabled: return "<color=#ff8040>inutilizzabile</color>";
                case PartState.Prosthetic: return "<color=#a0b0d0>protesi</color>";
                case PartState.Injured: return $"<color=#ffd040>{Mathf.CeilToInt(ph.hp)}</color>";
                default: return Mathf.CeilToInt(ph.hp).ToString();
            }
        }

        static void DebugHit(BodyHealth h)
        {
            var p = (BodyPart)Random.Range(0, BodyRig.PartCount);
            h.Damage(p, Random.Range(15f, 30f), DamageType.Blunt);
        }

        static void DebugCut(BodyHealth h)
        {
            BodyPart[] limbs =
            {
                BodyPart.UpperArmL, BodyPart.ForearmL, BodyPart.HandL, BodyPart.UpperArmR, BodyPart.ForearmR, BodyPart.HandR,
                BodyPart.ThighL, BodyPart.ShinL, BodyPart.FootL, BodyPart.ThighR, BodyPart.ShinR, BodyPart.FootR,
            };
            for (int tries = 0; tries < 20; tries++)
            {
                BodyPart p = limbs[Random.Range(0, limbs.Length)];
                if (h.Get(p).severed) continue;
                h.Damage(p, h.Get(p).max * 2f, DamageType.Cut);
                return;
            }
        }

        static string ActionName(CharacterMotor m)
        {
            if (m.IsStunned) return "Stordito dall'atterraggio";
            switch (m.CurrentTraversal)
            {
                case TraversalKind.Vault: return "Scavalca il muretto";
                case TraversalKind.Climb: return "Si arrampica oltre";
                case TraversalKind.ClimbHigh: return "Arrampicata alta";
                case TraversalKind.ClimbUp: return "Si arrampica su";
                case TraversalKind.Drop: return "Salta giù";
                case TraversalKind.GapJump: return "Salta il varco";
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
