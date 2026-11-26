using UnityEngine;

namespace FantaLandia.Horror.Interaction
{
    /// <summary>
    /// Qualsiasi cosa con cui il giocatore può interagire guardandola e premendo E.
    /// Il collider può stare sull'oggetto o su un suo figlio.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        /// <summary>Scritta mostrata al centro dello schermo, es. "Apri la porta".</summary>
        public abstract string Prompt(Interactor who);

        public virtual bool CanInteract(Interactor who) => enabled;

        public abstract void Interact(Interactor who);
    }
}
