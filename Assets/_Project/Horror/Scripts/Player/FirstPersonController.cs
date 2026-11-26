using FantaLandia.Characters;
using FantaLandia.Characters.Body;
using FantaLandia.Core;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Dialogue;
using FantaLandia.Horror.World;
using UnityEngine;

namespace FantaLandia.Horror.Player
{
    /// <summary>
    /// Muark in prima persona: WASD, mouse per guardarsi intorno, Shift corre, C/Ctrl si abbassa.
    /// Il corpo a segmenti è visibile (guardando in basso si vedono gambe e piedi che si appoggiano davvero)
    /// tranne testa e collo, che fanno solo ombra. I passi suonano quando un piede tocca terra.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public Camera cam;
        [Tooltip("Il modello del corpo (con BodyRig e ProceduralBodyAnimator)")]
        public BodyRig body;

        [Header("Movimento (m/s)")]
        public float walkSpeed = 1.7f;
        public float runSpeed = 3.6f;
        public float crouchSpeed = 0.9f;
        public float acceleration = 10f;
        public float gravity = 18f;

        [Header("Visuale")]
        public float mouseSensitivity = 0.08f;
        public float eyeHeight = 1.68f;
        public float crouchEyeHeight = 1.15f;
        [Tooltip("La camera sta un po' avanti rispetto al centro del corpo, così non si vede l'interno della testa")]
        public float eyeForward = 0.12f;
        public float headBob = 0.015f;

        /// <summary>Bloccato da scene e dialoghi.</summary>
        public bool inputLocked;

        public bool IsCrouching { get; private set; }
        public bool IsRunning { get; private set; }
        public float Yaw => yaw;
        public float Pitch => pitch;

        CharacterController controller;
        ProceduralBodyAnimator animator;
        Vector3 velocity;
        float yaw, pitch;
        float crouchT;
        float bobPhase;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (body != null) animator = body.GetComponent<ProceduralBodyAnimator>();
            yaw = transform.eulerAngles.y;
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (body != null)
            {
                // testa e collo fanno solo ombra: la camera è "dentro" la testa
                foreach (BodyPart p in new[] { BodyPart.Head, BodyPart.Neck })
                {
                    Transform j = body.Joint(p);
                    if (j == null) continue;
                    foreach (Transform child in j)
                    {
                        if (child.name == "Pelle" || child.name == "Intimo" || child.name == "Abbigliamento")
                            foreach (Renderer r in child.GetComponentsInChildren<Renderer>())
                                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    }
                }
                foreach (Transform t in body.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            }
            if (animator != null) animator.FootPlanted += OnFootPlanted;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool locked = inputLocked || DialogueSystem.IsBlocking;

            // --- guardarsi intorno
            if (!locked)
            {
                Vector2 md = GameInput.MouseDelta * mouseSensitivity;
                yaw += md.x;
                pitch = Mathf.Clamp(pitch - md.y, -85f, 85f);
            }
            else if (DialogueSystem.IsBlocking) LookAtSpeaker(dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // --- movimento
            Vector2 input = locked ? Vector2.zero : GameInput.PanAxis;
            IsCrouching = !locked && GameInput.CrouchHeld;
            IsRunning = !locked && !IsCrouching && GameInput.RunHeld && input.y > 0.1f;
            float speed = IsCrouching ? crouchSpeed : IsRunning ? runSpeed : walkSpeed;
            Vector3 wish = (transform.forward * input.y + transform.right * input.x);
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            horizontal = Vector3.MoveTowards(horizontal, wish * speed, acceleration * dt);
            velocity.x = horizontal.x;
            velocity.z = horizontal.z;

            if (controller.enabled)
            {
                if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
                velocity.y -= gravity * dt;
                controller.Move(velocity * dt);
            }
            else velocity = Vector3.zero; // posizionato da un'azione guidata

            // --- abbassarsi: capsula più bassa e camera più giù
            crouchT = Mathf.MoveTowards(crouchT, IsCrouching ? 1f : 0f, dt * 4f);
            float height = Mathf.Lerp(1.8f, 1.25f, crouchT);
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);

            // --- camera
            float hs = horizontal.magnitude;
            bobPhase += hs * dt * 2.2f;
            float bob = Mathf.Sin(bobPhase * Mathf.PI) * headBob * Mathf.Clamp01(hs / walkSpeed);
            float eye = Mathf.Lerp(eyeHeight, crouchEyeHeight, crouchT);
            cam.transform.localPosition = new Vector3(0f, eye + bob, eyeForward);
            cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            // --- il corpo segue
            if (animator != null)
            {
                animator.gaitOverride = IsRunning ? Gait.Jog : Gait.Walk;
                animator.speedOverride = IsRunning ? runSpeed : walkSpeed;
                animator.externalCrouch = crouchT * 0.5f;
            }
        }

        Dialogue.NpcSpeaker dialogueFocus;

        /// <summary>Durante i dialoghi lo sguardo si porta da solo su chi parla (o sull'ultimo che ha parlato).</summary>
        void LookAtSpeaker(float dt)
        {
            Speaker sp = DialogueSystem.CurrentSpeaker;
            if (sp != null && sp.id != Speakers.Muark.id)
            {
                var npc = Dialogue.NpcSpeaker.Find(sp.id);
                if (npc != null) dialogueFocus = npc;
            }
            if (dialogueFocus == null || cam == null) return;
            Vector3 dir = dialogueFocus.EyePoint - cam.transform.position;
            if (dir.sqrMagnitude < 0.01f) return;
            float wantYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float wantPitch = -Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
            float k = 1f - Mathf.Exp(-4f * dt);
            yaw = Mathf.LerpAngle(yaw, wantYaw, k);
            pitch = Mathf.Lerp(pitch, Mathf.Clamp(wantPitch, -60f, 60f), k);
        }

        public void SetLook(float newYaw, float newPitch)
        {
            yaw = newYaw;
            pitch = newPitch;
        }

        void OnFootPlanted(int side, Vector3 point)
        {
            Surface surface = Surface.Concrete;
            if (Physics.Raycast(point + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 0.6f, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                var s = hit.collider.GetComponentInParent<FootstepSurface>();
                if (s != null) surface = s.surface;
            }
            float vol = IsCrouching ? 0.15f : IsRunning ? 0.6f : 0.35f;
            HorrorAudio.PlayAt(ProceduralAudio.Footstep(surface, Random.Range(0, 4)), point, vol, Random.Range(0.92f, 1.08f), 12f);
        }
    }
}
