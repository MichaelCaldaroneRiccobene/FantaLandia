using FantaLandia.Characters.Body;
using FantaLandia.Core;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Dialogue;
using FantaLandia.Horror.Interaction;
using UnityEngine;

namespace FantaLandia.Horror.Player
{
    /// <summary>
    /// Torcia nella mano sinistra, tenuta in avanti come nei videogiochi: il braccio la punta dove guardi
    /// (con un leggero ritardo, per dare peso). F accende/spegne; Q la lascia cadere; Q tenuto più di
    /// mezzo secondo la lancia. Accesa resta accesa anche a terra o in volo, e si può raccogliere di nuovo.
    /// </summary>
    [DefaultExecutionOrder(300)] // dopo l'animatore: la torcia segue la mano nella posa finale
    public class PlayerFlashlight : MonoBehaviour
    {
        [Tooltip("La luce della torcia (diventa figlia della torcia quando la si prende)")]
        public Light spot;
        public bool isOn;
        [Tooltip("Ritardo con cui la torcia segue lo sguardo")]
        public float follow = 12f;
        public float throwSpeed = 9f;
        public float holdToThrow = 0.5f;

        [Header("Posizione in mano (rispetto alla camera)")]
        public Vector3 wristFromCamera = new Vector3(-0.2f, -0.24f, 0.38f);
        [Tooltip("Posizione della torcia rispetto al polso, lungo la direzione della mano")]
        public float gripDown = 0.05f;
        [Tooltip("Distanza tra il centro della torcia e la lente (m)")]
        public float lensForward = 0.13f;

        public bool HasFlashlight => torch != null;

        GameObject torch;
        Camera cam;
        FirstPersonController fpc;
        Interactor interactor;
        PlayerActions actions;
        ProceduralBodyAnimator anim;
        Transform wrist;
        Vector3 aim;
        float qDownTime = -1f;
        float flickerUntil;
        /// <summary>La torcia sfarfalla per qualche istante (spaventi della storia).</summary>
        public void Flicker(float seconds) => flickerUntil = Time.time + seconds;
        float baseIntensity = 3f;

        void Start()
        {
            cam = GetComponentInChildren<Camera>();
            fpc = GetComponent<FirstPersonController>();
            interactor = GetComponent<Interactor>();
            actions = GetComponent<PlayerActions>();
            if (fpc != null && fpc.body != null)
            {
                anim = fpc.body.GetComponent<ProceduralBodyAnimator>();
                wrist = fpc.body.Joint(BodyPart.HandL);
            }
            if (spot != null) { baseIntensity = spot.intensity; spot.enabled = false; }
            aim = cam != null ? cam.transform.forward : transform.forward;
        }

        /// <summary>La mano sinistra è libera? (con la birra in mano non si prende la torcia)</summary>
        public bool LeftHandFree => interactor == null || interactor.Held == null || !interactor.Held.carryInLeftHand;

        /// <summary>Prende in mano una torcia del mondo.</summary>
        public void Equip(GameObject torchObject)
        {
            if (torch != null || torchObject == null) return;
            torch = torchObject;
            foreach (Collider c in torch.GetComponentsInChildren<Collider>()) c.enabled = false;
            if (torch.TryGetComponent(out Rigidbody rb)) { rb.isKinematic = true; rb.detectCollisions = false; }
            var pickup = torch.GetComponent<FlashlightPickup>();
            if (pickup != null) pickup.enabled = false;

            if (spot != null)
            {
                spot.transform.SetParent(torch.transform, false);
                spot.transform.localPosition = new Vector3(0f, 0f, lensForward);
                spot.transform.localRotation = Quaternion.identity;
            }
            isOn = true;
            aim = cam.transform.forward;
            HorrorAudio.Play2D(ProceduralAudio.Click(), 0.6f);
            Apply();
        }

        void Update()
        {
            if (torch == null) return;
            bool blocked = DialogueSystem.IsBlocking || (actions != null && actions.Busy) || FantaLandia.Horror.UI.NotebookMenu.IsOpen || FantaLandia.Horror.UI.IntroSequence.Active;

            if (!blocked && GameInput.FlashlightToggle)
            {
                isOn = !isOn;
                HorrorAudio.Play2D(ProceduralAudio.Click(), 0.6f);
                Apply();
            }

            // Q: tocco = lascia cadere, tenuto più di mezzo secondo = lancia
            if (!blocked && GameInput.DropDown) qDownTime = Time.time;
            if (qDownTime > 0f && !GameInput.DropHeld)
            {
                bool throwIt = Time.time - qDownTime >= holdToThrow;
                qDownTime = -1f;
                Release(throwIt);
                return;
            }

            // il braccio sinistro punta la torcia dove si guarda (se non è impegnato in un'azione guidata)
            aim = Vector3.Slerp(aim, cam.transform.forward, 1f - Mathf.Exp(-follow * Time.deltaTime));
            if (anim != null && !(actions != null && actions.Busy))
            {
                // caricando il lancio il braccio va indietro
                float windUp = qDownTime > 0f ? Mathf.Clamp01((Time.time - qDownTime) / holdToThrow) : 0f;
                Vector3 target = cam.transform.TransformPoint(wristFromCamera + new Vector3(-0.05f, 0.05f, -0.25f) * windUp);
                Vector3 pole = Vector3.down * 0.6f - cam.transform.right * 0.8f - cam.transform.forward * 0.3f;
                anim.SetExternalHandIK(-1, target, pole, 1f, Quaternion.LookRotation(aim, Vector3.up));
            }

            if (isOn && spot != null)
            {
                if (Time.time > flickerUntil + 6f && Random.value < 0.002f) flickerUntil = Time.time + Random.Range(0.1f, 0.4f);
                spot.intensity = Time.time < flickerUntil ? baseIntensity * Random.Range(0.2f, 1f) : baseIntensity;
            }
        }

        void LateUpdate()
        {
            if (torch == null || wrist == null) return;
            // la torcia nel pugno: poco sotto il polso, puntata dove punta la mano
            torch.transform.position = wrist.position - wrist.up * gripDown;
            torch.transform.rotation = Quaternion.LookRotation(aim, Vector3.up);
        }

        void Release(bool throwIt)
        {
            GameObject t = torch;
            torch = null;
            foreach (Collider c in t.GetComponentsInChildren<Collider>()) c.enabled = true;
            var rb = t.GetComponent<Rigidbody>();
            if (rb == null) rb = t.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.isKinematic = false;
            rb.detectCollisions = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Vector3 playerVel = GetComponent<CharacterController>() != null ? GetComponent<CharacterController>().velocity : Vector3.zero;
            if (throwIt)
            {
                rb.linearVelocity = cam.transform.forward * throwSpeed + Vector3.up * 2f + playerVel;
                rb.angularVelocity = new Vector3(Random.Range(-8f, 8f), Random.Range(-4f, 4f), Random.Range(-8f, 8f));
            }
            else
            {
                rb.linearVelocity = playerVel + cam.transform.forward * 0.5f;
                rb.angularVelocity = Random.insideUnitSphere * 2f;
            }
            var pickup = t.GetComponent<FlashlightPickup>();
            if (pickup != null) pickup.enabled = true;
            HorrorAudio.PlayAt(ProceduralAudio.PickUp(), t.transform.position, 0.5f, 0.8f);
            // la luce resta sulla torcia: se era accesa illumina da terra
        }

        void Apply()
        {
            if (spot != null) spot.enabled = torch != null && isOn;
        }
    }
}
