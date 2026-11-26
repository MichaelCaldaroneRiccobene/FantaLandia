using System;
using FantaLandia.Horror.Audio;
using UnityEngine;

namespace FantaLandia.Horror.Interaction
{
    /// <summary>Punto dove posare un oggetto preciso (es. la birra sul bancone della cucina).</summary>
    public class PlaceSpot : Interactable
    {
        [Tooltip("itemId dell'oggetto accettato")]
        public string acceptsItemId = "birra";
        public string verb = "Posa";
        public string where = "sul bancone";
        [Tooltip("Dove finisce l'oggetto (se vuoto, questo oggetto)")]
        public Transform placePoint;
        [Tooltip("Dove si mette il giocatore per posare (se vuoto: 0.55 m davanti al punto di posa)")]
        public Transform standPoint;
        [Tooltip("Dopo averlo posato non si può più riprendere")]
        public bool lockItem = true;

        public PickupItem Placed { get; private set; }
        public event Action<PickupItem> ItemPlaced;

        public override string Prompt(Interactor who) => $"{verb} {(who.Held != null ? who.Held.displayName : "")} {where}";

        public override bool CanInteract(Interactor who) =>
            enabled && Placed == null && who.Held != null && who.Held.itemId == acceptsItemId;

        public Vector3 StandPositionWorld
        {
            get
            {
                if (standPoint != null) return standPoint.position;
                Transform p = placePoint != null ? placePoint : transform;
                Vector3 fwd = transform.forward;
                return p.position + fwd * 0.55f;
            }
        }

        public override void Interact(Interactor who)
        {
            var actions = who.GetComponent<FantaLandia.Horror.Player.PlayerActions>();
            if (actions != null) actions.Place(this);
            else CompletePlace(who);
        }

        /// <summary>Lascia l'oggetto nel punto di posa (chiamato a fine gesto).</summary>
        public void CompletePlace(Interactor who)
        {
            if (who.Held == null) return;
            PickupItem item = who.Release();
            item.PlaceAt(placePoint != null ? placePoint : transform);
            if (lockItem) item.canPickUp = false;
            Placed = item;
            HorrorAudio.PlayAt(item.itemId == "birra" ? ProceduralAudio.CansClink() : ProceduralAudio.PickUp(), transform.position, 0.8f);
            ItemPlaced?.Invoke(item);
        }
    }
}
