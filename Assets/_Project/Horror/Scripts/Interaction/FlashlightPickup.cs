using System;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Dialogue;
using FantaLandia.Horror.Player;
using UnityEngine;

namespace FantaLandia.Horror.Interaction
{
    /// <summary>La torcia: raccogliendola il giocatore la può accendere con F.</summary>
    public class FlashlightPickup : Interactable
    {
        public override string Prompt(Interactor who) => "Prendi la torcia";

        public override void Interact(Interactor who)
        {
            var fl = who.GetComponent<PlayerFlashlight>();
            if (fl != null) fl.Give();
            HorrorAudio.PlayAt(ProceduralAudio.PickUp(), transform.position, 0.6f);
            DialogueSystem.Bark(Speakers.Muark, "Una torcia. Si accende con F.");
            Destroy(gameObject);
        }
    }
}
