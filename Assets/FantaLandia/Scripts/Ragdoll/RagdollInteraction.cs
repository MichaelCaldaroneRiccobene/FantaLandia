using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FantaLandia.Ragdoll
{
    /// <summary>
    /// Strumenti per mettere alla prova l'equilibrio: spinte, palle lanciate, afferrare e trascinare.
    /// Tutto passa da forze fisiche reali (impulsi e forze applicate ai Rigidbody).
    /// </summary>
    public class RagdollInteraction : MonoBehaviour
    {
        public BalanceController target;
        public Camera cam;
        public System.Action ResetRequested;

        [Header("Spinte (sul torace)")]
        [Tooltip("Impulso in N·s. Con Shift viene moltiplicato per strongMultiplier.")]
        public float pushImpulse = 45f;
        public float strongMultiplier = 2.5f;

        [Header("Palle (click sinistro)")]
        public float ballMass = 3f;
        public float ballSpeed = 14f;
        public float ballRadius = 0.12f;
        public float ballLifetime = 10f;

        [Header("Afferra (tieni premuto il tasto destro)")]
        public float grabStiffness = 800f;
        public float grabDamping = 60f;
        public float grabMaxForce = 2000f;

        public bool showHud = true;

        Vector3 pendingPush;
        bool hasPush;
        Rigidbody grabbed;
        Vector3 grabLocalPoint, grabTargetPoint;
        float grabDistance;

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null || target == null || cam == null) return;

            if (kb.rKey.wasPressedThisFrame)
            {
                grabbed = null;
                hasPush = false;
                ResetRequested?.Invoke();
                return;
            }
            if (kb.lKey.wasPressedThisFrame) target.Knockout();
            if (kb.hKey.wasPressedThisFrame) showHud = !showHud;

            Vector3 camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            Vector3 dir = Vector3.zero;
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) dir += camForward;
            if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) dir -= camForward;
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) dir += camRight;
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) dir -= camRight;
            if (kb.spaceKey.wasPressedThisFrame)
            {
                Vector2 r = Random.insideUnitCircle.normalized;
                dir = new Vector3(r.x, 0f, r.y);
            }
            if (dir.sqrMagnitude > 0f)
            {
                float multiplier = kb.shiftKey.isPressed ? strongMultiplier : 1f;
                pendingPush = dir.normalized * (pushImpulse * multiplier);
                hasPush = true;
            }

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (mouse.leftButton.wasPressedThisFrame) ThrowBall(ray);

            if (mouse.rightButton.wasPressedThisFrame &&
                Physics.Raycast(ray, out var hit, 100f) &&
                hit.rigidbody != null && !hit.rigidbody.isKinematic)
            {
                grabbed = hit.rigidbody;
                grabLocalPoint = grabbed.transform.InverseTransformPoint(hit.point);
                grabDistance = hit.distance;
            }
            if (mouse.rightButton.wasReleasedThisFrame) grabbed = null;
            if (grabbed != null) grabTargetPoint = ray.GetPoint(grabDistance);
#endif
        }

        void FixedUpdate()
        {
            if (hasPush && target != null)
            {
                target.Ragdoll.Chest.Body.AddForce(pendingPush, ForceMode.Impulse);
                hasPush = false;
            }

            if (grabbed != null)
            {
                Vector3 point = grabbed.transform.TransformPoint(grabLocalPoint);
                Vector3 velocity = grabbed.GetPointVelocity(point);
                Vector3 force = (grabTargetPoint - point) * grabStiffness - velocity * grabDamping;
                grabbed.AddForceAtPosition(Vector3.ClampMagnitude(force, grabMaxForce), point);
            }
        }

        void ThrowBall(Ray ray)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Palla";
            ball.transform.localScale = Vector3.one * (ballRadius * 2f);
            ball.transform.position = ray.origin + ray.direction * 0.6f;
            ball.GetComponent<Renderer>().material.color = new Color(0.9f, 0.2f, 0.2f);

            var rb = ball.AddComponent<Rigidbody>();
            rb.mass = ballMass;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = ray.direction * ballSpeed;
            Destroy(ball, ballLifetime);
        }

        void OnGUI()
        {
            if (!showHud || target == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 380, 260), GUI.skin.box);
            string stateText = StateName(target.CurrentState);
            if (target.CurrentState == BalanceController.State.GettingUp)
                stateText += $" ({PhaseName(target.CurrentGetUpPhase)})";
            GUILayout.Label($"Stato: {stateText}    Muscoli: {target.CurrentStrength:0.00}");
            if (BalanceController.IsCalibrated)
                GUILayout.Label($"Calibrazione: drive {(BalanceController.CalibrationDriveInverted ? "invertiti" : "normali")}, " +
                                $"limiti {(BalanceController.CalibrationLimitsFlipped ? "corretti" : "ok")}");
            GUILayout.Label($"Capture point fuori dalla base: {Mathf.Max(0f, target.SupportDistance) * 100f:0} cm");
            GUILayout.Space(6);
            GUILayout.Label("WASD / frecce: spinta  (Shift = forte)");
            GUILayout.Label("Spazio: spinta in direzione casuale");
            GUILayout.Label("Click sinistro: lancia una palla");
            GUILayout.Label("Tasto destro (tieni premuto): afferra e trascina");
            GUILayout.Label("L: sviene    R: reset    H: nascondi pannello");
            GUILayout.Label("Q / E o rotella premuta: ruota camera    Rotella: zoom");
            GUILayout.EndArea();
        }

        static string StateName(BalanceController.State s) => s switch
        {
            BalanceController.State.Calibrating => "Calibrazione",
            BalanceController.State.Standing => "In piedi",
            BalanceController.State.Stepping => "Passo di recupero",
            BalanceController.State.Fallen => "A terra",
            BalanceController.State.GettingUp => "Si rialza",
            _ => s.ToString()
        };

        static string PhaseName(BalanceController.GetUpPhase p) => p switch
        {
            BalanceController.GetUpPhase.Roll => "rotola",
            BalanceController.GetUpPhase.PushUp => "spinta con le braccia",
            BalanceController.GetUpPhase.HandsAndKnees => "carponi",
            BalanceController.GetUpPhase.Kneel => "in ginocchio",
            BalanceController.GetUpPhase.Lunge => "affondo",
            BalanceController.GetUpPhase.Stand => "si alza",
            _ => ""
        };
    }
}
