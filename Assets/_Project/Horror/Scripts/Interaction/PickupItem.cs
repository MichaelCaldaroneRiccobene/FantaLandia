using System;
using FantaLandia.Horror.Audio;
using UnityEngine;

namespace FantaLandia.Horror.Interaction
{
    /// <summary>Oggetto che si può prendere in mano (uno alla volta) e posare in un PlaceSpot.</summary>
    public class PickupItem : Interactable
    {
        public string itemId = "oggetto";
        public string displayName = "oggetto";
        [Tooltip("Posizione e rotazione quando è in mano (rispetto al punto di presa)")]
        public Vector3 holdOffset = Vector3.zero;
        public Vector3 holdEuler = Vector3.zero;
        [Tooltip("Portato nella mano sinistra del corpo (es. la confezione per il manico) invece che davanti alla camera")]
        public bool carryInLeftHand;
        [Tooltip("Posizione e rotazione rispetto al polso sinistro quando è portato a mano")]
        public Vector3 handOffset = new Vector3(0f, -0.25f, 0f);
        public Vector3 handEuler = new Vector3(0f, 90f, 0f);
        [Tooltip("Altezza del manico sopra la base (m): serve per posarlo con la mano")]
        public float handleHeight = 0.19f;
        [Tooltip("Se falso non si può più raccogliere (es. dopo che è stato posato per la storia)")]
        public bool canPickUp = true;

        public bool IsHeld { get; private set; }
        public event Action<PickupItem> PickedUp;

        Transform moveTarget;
        Vector3 targetLocalPos;
        Quaternion targetLocalRot;
        float moveT = 1f;
        Vector3 fromLocalPos;
        Quaternion fromLocalRot;

        public override string Prompt(Interactor who) => $"Prendi {displayName}";

        public override bool CanInteract(Interactor who) => enabled && canPickUp && !IsHeld && who.Held == null;

        public override void Interact(Interactor who)
        {
            HorrorAudio.PlayAt(ProceduralAudio.PickUp(), transform.position, 0.6f);
            who.Hold(this);
            PickedUp?.Invoke(this);
        }

        /// <summary>Attacca l'oggetto alla mano (o a un punto d'appoggio) con un breve movimento morbido.</summary>
        public void AttachTo(Transform parent, bool instant)
        {
            IsHeld = true;
            SetColliders(false);
            MoveTo(parent, holdOffset, Quaternion.Euler(holdEuler), instant);
        }

        /// <summary>Attacca l'oggetto al polso sinistro (lo si porta per il manico, lungo il fianco).</summary>
        public void AttachToHand(Transform wrist, bool instant)
        {
            IsHeld = true;
            SetColliders(false);
            MoveTo(wrist, handOffset, Quaternion.Euler(handEuler), instant);
        }

        /// <summary>Posa l'oggetto in un punto del mondo.</summary>
        public void PlaceAt(Transform spot)
        {
            IsHeld = false;
            MoveTo(spot, Vector3.zero, Quaternion.identity, false);
            SetColliders(true);
        }

        void MoveTo(Transform parent, Vector3 localPos, Quaternion localRot, bool instant)
        {
            transform.SetParent(parent, true);
            moveTarget = parent;
            targetLocalPos = localPos;
            targetLocalRot = localRot;
            fromLocalPos = transform.localPosition;
            fromLocalRot = transform.localRotation;
            moveT = instant ? 1f : 0f;
            if (instant) { transform.localPosition = localPos; transform.localRotation = localRot; }
        }

        void LateUpdate()
        {
            if (moveT >= 1f || moveTarget == null) return;
            moveT = Mathf.Min(1f, moveT + Time.deltaTime / 0.25f);
            float e = moveT * moveT * (3f - 2f * moveT);
            transform.localPosition = Vector3.Lerp(fromLocalPos, targetLocalPos, e);
            transform.localRotation = Quaternion.Slerp(fromLocalRot, targetLocalRot, e);
        }

        void SetColliders(bool on)
        {
            foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = on;
        }
    }
}
