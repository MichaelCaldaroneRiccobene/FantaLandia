using System;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Dialogue;
using FantaLandia.Horror.Player;
using UnityEngine;

namespace FantaLandia.Horror.Interaction
{
    /// <summary>Interruttore: accende e spegne un gruppo di luci (e i materiali luminosi delle lampade).</summary>
    public class LightSwitch : Interactable
    {
        public Light[] lights;
        [Tooltip("Lampadine/paralumi da far 'brillare' quando la luce è accesa")]
        public Renderer[] glowRenderers;
        public bool isOn = true;
        [Tooltip("Senza corrente l'interruttore non fa nulla (blackout)")]
        public bool powered = true;
        public Transform lever;

        public event Action<bool> Toggled;

        public override string Prompt(Interactor who) => isOn ? "Spegni la luce" : "Accendi la luce";

        void Start() => Apply();

        public override void Interact(Interactor who)
        {
            isOn = !isOn;
            HorrorAudio.PlayAt(ProceduralAudio.Click(), transform.position, 0.7f);
            Apply();
            Toggled?.Invoke(isOn);
        }

        public void SetPowered(bool value)
        {
            powered = value;
            Apply();
        }

        void Apply()
        {
            bool lit = isOn && powered;
            foreach (Light l in lights) if (l != null) l.enabled = lit;
            foreach (Renderer r in glowRenderers) if (r != null) r.enabled = lit;
            if (lever != null) lever.localRotation = Quaternion.Euler(isOn ? -20f : 20f, 0f, 0f);
        }
    }
}
