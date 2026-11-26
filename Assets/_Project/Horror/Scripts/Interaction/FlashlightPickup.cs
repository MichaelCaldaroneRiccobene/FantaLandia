using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Dialogue;
using FantaLandia.Horror.Player;
using UnityEngine;

namespace FantaLandia.Horror.Interaction
{
    /// <summary>La torcia nel mondo: raccolta va nella mano sinistra (F accende, Q lascia, Q tenuto lancia).</summary>
    public class FlashlightPickup : Interactable
    {
        bool firstTime = true;

        public override string Prompt(Interactor who) => "Prendi la torcia";

        public override bool CanInteract(Interactor who)
        {
            if (!enabled) return false;
            var fl = who.GetComponent<PlayerFlashlight>();
            return fl != null && !fl.HasFlashlight && fl.LeftHandFree;
        }

        public override void Interact(Interactor who)
        {
            var fl = who.GetComponent<PlayerFlashlight>();
            if (fl == null) return;
            HorrorAudio.PlayAt(ProceduralAudio.PickUp(), transform.position, 0.6f);
            fl.Equip(gameObject);
            if (firstTime)
            {
                firstTime = false;
                DialogueSystem.Bark(Speakers.Muark, "Una torcia. F accende, Q la lascia, Q tenuto la lancia.");
            }
        }
    }
}
