using System.Collections;
using FantaLandia.Characters.Body;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Interaction;
using UnityEngine;

namespace FantaLandia.Horror.Player
{
    /// <summary>
    /// Azioni "guidate" di Muark: il giocatore resta fermo, il personaggio si porta con un lerp nella
    /// posizione e rotazione giuste e poi esegue il gesto con le mani (bussare, posare un oggetto).
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public class PlayerActions : MonoBehaviour
    {
        public bool Busy { get; private set; }

        FirstPersonController fpc;
        CharacterController cc;
        Interactor interactor;
        ProceduralBodyAnimator anim;

        void Awake()
        {
            fpc = GetComponent<FirstPersonController>();
            cc = GetComponent<CharacterController>();
            interactor = GetComponent<Interactor>();
        }

        void Start()
        {
            if (fpc.body != null) anim = fpc.body.GetComponent<ProceduralBodyAnimator>();
        }

        // ------------------------------------------------------------------ posizionamento

        /// <summary>Porta il giocatore (posizione, direzione dello sguardo e inclinazione) con un movimento morbido.</summary>
        public IEnumerator MoveToPose(Vector3 position, float yaw, float pitch, float duration)
        {
            Vector3 p0 = transform.position;
            position.y = p0.y; // resta sul suo pavimento
            float yaw0 = fpc.Yaw, pitch0 = fpc.Pitch;
            float dist = Vector3.Distance(p0, position);
            duration = Mathf.Max(duration, dist / 2.2f); // se è lontano ci mette un po' di più
            cc.enabled = false;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.01f, duration));
                float e = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(p0, position, e);
                fpc.SetLook(Mathf.LerpAngle(yaw0, yaw, e), Mathf.Lerp(pitch0, pitch, e));
                yield return null;
            }
            cc.enabled = true;
        }

        IEnumerator HandTo(int side, Vector3 from, Vector3 to, float duration, float w0, float w1, Vector3 pole, Quaternion? rot)
        {
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.01f, duration));
                float e = t * t * (3f - 2f * t);
                anim?.SetExternalHandIK(side, Vector3.Lerp(from, to, e), pole, Mathf.Lerp(w0, w1, e), rot);
                yield return null;
            }
        }

        IEnumerator HoldHand(int side, Vector3 at, float seconds, Vector3 pole, Quaternion? rot)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                anim?.SetExternalHandIK(side, at, pole, 1f, rot);
                yield return null;
            }
        }

        // ------------------------------------------------------------------ bussare

        public void Knock(HorrorDoor door)
        {
            if (!Busy) StartCoroutine(KnockRoutine(door));
        }

        IEnumerator KnockRoutine(HorrorDoor door)
        {
            Busy = true;
            fpc.inputLocked = true;
            interactor.interactionEnabled = false;

            Vector3 surface = door.KnockPointWorld;
            Vector3 outward = door.OutwardWorld;
            Vector3 stand = surface + outward * 0.62f + Vector3.Cross(Vector3.up, outward) * -0.1f;
            float yaw = Quaternion.LookRotation(-outward).eulerAngles.y;
            yield return MoveToPose(stand, yaw, 4f, 0.45f);

            // mano destra: nocche verso la porta
            Quaternion fist = ProceduralBodyAnimator.PalmDown(1, -outward);
            Vector3 right = Vector3.Cross(Vector3.up, -outward);
            Vector3 pole = Vector3.down * 0.6f + right * 0.8f + outward * 0.3f;
            Vector3 hit = surface + outward * 0.1f;     // il polso: le dita arrivano a toccare la porta
            Vector3 back = surface + outward * 0.27f;
            Vector3 start = anim != null ? anim.HandPosition(1) : back;

            yield return HandTo(1, start, back, 0.28f, 0f, 1f, pole, fist);
            for (int i = 0; i < 3; i++)
            {
                yield return HandTo(1, back, hit, 0.07f, 1f, 1f, pole, fist);
                HorrorAudio.PlayAt(ProceduralAudio.KnockOnce(i), surface, 0.9f, Random.Range(0.95f, 1.05f), 30f);
                yield return HandTo(1, hit, back, 0.12f, 1f, 1f, pole, fist);
                yield return HoldHand(1, back, 0.05f, pole, fist);
            }
            Vector3 rest = anim != null ? anim.HandPosition(1) + Vector3.down * 0.3f : back;
            yield return HandTo(1, back, rest, 0.3f, 1f, 0f, pole, fist);

            interactor.interactionEnabled = true;
            fpc.inputLocked = false;
            Busy = false;
            door.NotifyKnocked();
        }

        // ------------------------------------------------------------------ posare un oggetto

        public void Place(PlaceSpot spot)
        {
            if (!Busy) StartCoroutine(PlaceRoutine(spot));
        }

        IEnumerator PlaceRoutine(PlaceSpot spot)
        {
            Busy = true;
            fpc.inputLocked = true;
            interactor.interactionEnabled = false;

            Transform target = spot.placePoint != null ? spot.placePoint : spot.transform;
            Vector3 stand = spot.StandPositionWorld;
            Vector3 toTarget = target.position - stand;
            toTarget.y = 0f;
            float yaw = Quaternion.LookRotation(toTarget.sqrMagnitude > 0.001f ? toTarget : transform.forward).eulerAngles.y;
            // la mano sinistra porta l'oggetto sul piano: ci si mette un po' a destra del punto
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            yield return MoveToPose(stand + right * 0.2f, yaw, 32f, 0.45f);

            PickupItem item = interactor.Held;
            float handleHeight = item != null ? item.handleHeight : 0.2f;
            Vector3 wristOnSpot = target.position + Vector3.up * (handleHeight + 0.04f);
            Vector3 above = wristOnSpot + Vector3.up * 0.12f;
            Vector3 pole = Vector3.down * 0.5f - right * 0.8f;
            Vector3 start = anim != null ? anim.HandPosition(-1) : above;

            if (anim != null) anim.carryLeft = false;
            yield return HandTo(-1, start, above, 0.4f, 0f, 1f, pole, null);
            yield return HandTo(-1, above, wristOnSpot, 0.18f, 1f, 1f, pole, null);
            spot.CompletePlace(interactor);
            yield return HandTo(-1, wristOnSpot, above, 0.15f, 1f, 1f, pole, null);
            Vector3 rest = anim != null ? transform.position + Vector3.up * 0.8f - right * 0.25f : above;
            yield return HandTo(-1, above, rest, 0.3f, 1f, 0f, pole, null);

            interactor.interactionEnabled = true;
            fpc.inputLocked = false;
            Busy = false;
        }
    }
}
