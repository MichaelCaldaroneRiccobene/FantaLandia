using FantaLandia.Characters;
using FantaLandia.Characters.Body;
using FantaLandia.Core;
using FantaLandia.Horror.Audio;
using FantaLandia.Horror.Dialogue;
using FantaLandia.Horror.Interaction;
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

        [Header("Colpi e stordimento (capitolo 2)")]
        [Tooltip("0..1: si barcolla (il passo sbanda, la visuale ondeggia)")]
        [Range(0f, 1f)] public float stagger;
        [Tooltip("0..1: a terra (1 = sdraiato sul pavimento, la camera bassa e girata)")]
        [Range(0f, 1f)] public float knockdown;
        [Tooltip("Rollio della camera in gradi (si somma all'ondeggiare)")]
        public float roll;
        [Tooltip("La regia vieta la corsa (es. seguendo Tom al buio)")]
        public bool noRun;
        [Tooltip("Da seduti ci si può alzare (la regia può impedirlo, es. all'inizio del capitolo 2)")]
        public bool canStandUp = true;

        /// <summary>Una spinta improvvisa (un'esplosione, un colpo): si vola all'indietro e la spinta si smorza da sola.</summary>
        public void Push(Vector3 impulse)
        {
            velocity += impulse;
            pushT = 1f;
        }

        float pushT;

        public bool IsCrouching { get; private set; }
        /// <summary>Seduto su un divano o una poltrona.</summary>
        public bool IsSeated { get; private set; }
        public Seat CurrentSeat => seat;
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
            bool locked = inputLocked || DialogueSystem.IsBlocking || sitting || FantaLandia.Horror.UI.IntroSequence.Active;
            // mentre si parla si può camminare e guardarsi intorno, ma non correre né abbassarsi
            bool talking = DialogueSystem.IsTalking;
            if (IsSeated) locked = true;
            if (IsSeated && !sitting && !inputLocked && !DialogueSystem.IsBlocking && !FantaLandia.Horror.UI.IntroSequence.Active)
            {
                Vector2 md = GameInput.MouseDelta * mouseSensitivity * FantaLandia.Horror.UI.GameSettings.Sensitivity;
                yaw = seatYaw + Mathf.Clamp(Mathf.DeltaAngle(seatYaw, yaw + md.x), -110f, 110f);
                pitch = Mathf.Clamp(pitch - md.y, -70f, 75f);
                if (GameInput.StandUp && canStandUp) StandUp();
            }

            // --- guardarsi intorno
            if (!locked)
            {
                Vector2 md = GameInput.MouseDelta * mouseSensitivity * FantaLandia.Horror.UI.GameSettings.Sensitivity;
                yaw += md.x;
                pitch = Mathf.Clamp(pitch - md.y, -85f, 85f);
            }
            else if (DialogueSystem.IsBlocking) LookAtSpeaker(dt);
            // da seduti il corpo resta rivolto come il sedile: gira solo la testa (la camera)
            float bodyYaw = IsSeated && !sitting ? seatYaw : yaw;
            transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);

            // --- movimento
            Vector2 input = locked ? Vector2.zero : GameInput.PanAxis;
            IsCrouching = !locked && !talking && GameInput.CrouchHeld;
            IsRunning = !locked && !talking && !noRun && !IsCrouching && GameInput.RunHeld && input.y > 0.1f;
            float speed = IsCrouching ? crouchSpeed : IsRunning ? runSpeed : walkSpeed;
            Vector3 wish = (transform.forward * input.y + transform.right * input.x);
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            if (stagger > 0.001f)
            {
                // stordito: il passo sbanda di lato e rallenta, niente corsa
                IsRunning = false;
                speed = walkSpeed * (1f - 0.45f * stagger);
                if (wish.sqrMagnitude > 0.01f)
                    wish = (wish + transform.right * (Mathf.PerlinNoise(Time.time * 0.6f, 3.1f) - 0.5f) * 1.6f * stagger).normalized;
            }
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            // dopo una spinta si scivola via (si frena piano), poi si torna a camminare normalmente
            pushT = Mathf.MoveTowards(pushT, 0f, dt * 0.9f);
            horizontal = Vector3.MoveTowards(horizontal, wish * speed, acceleration * dt * (1f - 0.75f * pushT));
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
            seatT = Mathf.MoveTowards(seatT, IsSeated ? 1f : 0f, dt * 1.4f);
            if (seat != null || seatT > 0f) eye = Mathf.Lerp(eye, seatEyeHeight, seatT * seatT * (3f - 2f * seatT));
            // guardando in basso la camera avanza un po' (come la testa che si sporge): non entra nelle spalle e nei vestiti
            float down = Mathf.Clamp01(pitch / 70f);
            // ...ma non dentro quello che c'è davanti (porte, muri): la testa si ferma prima di toccarlo
            float forward = eyeForward + down * 0.2f;
            Vector3 head = transform.TransformPoint(new Vector3(0f, eye + bob - down * 0.05f, 0f));
            if (Physics.SphereCast(head, 0.1f, transform.forward, out RaycastHit block, forward + 0.06f, ~((1 << 2) | (1 << 7)), QueryTriggerInteraction.Ignore))
                forward = Mathf.Max(0f, block.distance - 0.06f);
            float kd = knockdown * knockdown * (3f - 2f * knockdown);
            float sway = stagger > 0.001f ? (Mathf.PerlinNoise(Time.time * 0.45f, 7.7f) - 0.5f) * 14f * stagger : 0f;
            cam.transform.localPosition = new Vector3(0f, Mathf.Lerp(eye + bob - down * 0.05f, 0.3f, kd), forward * (1f - kd));
            cam.transform.localRotation = Quaternion.Euler(pitch + sway * 0.3f, Mathf.DeltaAngle(bodyYaw, yaw), roll + sway + kd * 62f);

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

        // ------------------------------------------------------------------ sedersi

        Seat seat;
        float seatYaw, seatT, seatEyeHeight = 1.2f;
        bool sitting; // in movimento verso il sedile o per alzarsi

        public void SitOn(Seat s)
        {
            if (IsSeated || sitting || s == null || !s.Claim(this)) return;
            StartCoroutine(SitRoutine(s));
        }

        System.Collections.IEnumerator SitRoutine(Seat s)
        {
            sitting = true;
            seat = s;
            controller.enabled = false;
            velocity = Vector3.zero;
            seatYaw = s.transform.eulerAngles.y;
            seatEyeHeight = s.height + 0.86f; // all'altezza degli occhi degli altri seduti
            Vector3 p0 = transform.position, p1 = s.transform.position;
            float y0 = yaw, pi0 = pitch;
            IsSeated = true;
            if (animator != null) { animator.externalSeated = true; animator.externalSeatHeight = s.height; }
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / 1.1f);
                float e = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(p0, p1, e);
                yaw = Mathf.LerpAngle(y0, seatYaw, e);
                pitch = Mathf.Lerp(pi0, 8f, e);
                yield return null;
            }
            sitting = false;
        }

        public void StandUp()
        {
            if (!IsSeated || sitting) return;
            StartCoroutine(StandRoutine());
        }

        System.Collections.IEnumerator StandRoutine()
        {
            sitting = true;
            Vector3 p0 = transform.position, p1 = seat.FrontPosition;
            p1.y = p0.y;
            if (animator != null) animator.externalSeated = false;
            IsSeated = false;
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / 0.7f);
                transform.position = Vector3.Lerp(p0, p1, t * t * (3f - 2f * t));
                yield return null;
            }
            controller.enabled = true;
            Seat s = seat;
            seat = null;
            sitting = false;
            s.Release(this);
        }

        void OnGUI()
        {
            GUI.depth = -5; // l'aiuto per alzarsi (sempre sopra all'immagine dell'effetto retrò, che sta a 100)
            if (!IsSeated || sitting || inputLocked || !canStandUp || DialogueSystem.IsTalking) return;
            var st = FantaLandia.Horror.UI.UiStyle.Text(18, TextAnchor.MiddleLeft, false);
            float x = Screen.width * 0.5f - 90f, y = Screen.height - 46f;
            float kw = FantaLandia.Horror.UI.UiStyle.KeyCap(x, y, "WASD", 24f, 0.7f);
            FantaLandia.Horror.UI.UiStyle.Shadowed(new Rect(x + kw + 8f, y - 2f, 160f, 26f), FantaLandia.Horror.UI.Loc.T("per alzarti"), st, new Color(1f, 1f, 1f, 0.7f));
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
            float vol = IsCrouching ? 0.06f : IsRunning ? 0.26f : 0.14f;
            HorrorAudio.PlayAt(ProceduralAudio.Footstep(surface, Random.Range(0, 4)), point, vol, Random.Range(0.92f, 1.08f), 12f);
        }
    }
}
