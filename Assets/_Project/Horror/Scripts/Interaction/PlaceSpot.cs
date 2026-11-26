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
        [Tooltip("Dopo averlo posato non si può più riprendere")]
        public bool lockItem = true;

        public PickupItem Placed { get; private set; }
        public event Action<PickupItem> ItemPlaced;

        public override string Prompt(Interactor who) => $"{verb} {(who.Held != null ? who.Held.displayName : "")} {where}";

        public override bool CanInteract(Interactor who) =>
            enabled && Placed == null && who.Held != null && who.Held.itemId == acceptsItemId;

        public override void Interact(Interactor who)
        {
            PickupItem item = who.Release();
            item.PlaceAt(placePoint != null ? placePoint : transform);
            if (lockItem) item.canPickUp = false;
            Placed = item;
            HorrorAudio.PlayAt(item.itemId == "birra" ? ProceduralAudio.CansClink() : ProceduralAudio.PickUp(), transform.position, 0.8f);
            ItemPlaced?.Invoke(item);
        }
    }
}
