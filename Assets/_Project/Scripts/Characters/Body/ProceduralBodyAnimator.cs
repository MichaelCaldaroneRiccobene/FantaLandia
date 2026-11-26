using UnityEngine;

namespace FantaLandia.Characters.Body
{
    /// <summary>
    /// Animazione procedurale del corpo a segmenti, guidata dal CharacterMotor:
    /// fermo (respiro), camminata / corsa leggera / scatto, scavalcare, arrampicarsi, salto, atterraggio.
    ///
    /// Convenzioni di rotazione (asse X locale dei giunti):
    ///  - coscia e braccio: X negativo = in avanti
    ///  - ginocchio: X positivo = piega (stinco indietro); gomito: X negativo = piega (avambraccio avanti)
    ///  - schiena e testa: X positivo = in avanti / guarda giù
    ///  - braccio sinistro: Z negativo = verso l'esterno; destro: Z positivo
    /// </summary>
    [RequireComponent(typeof(BodyRig))]
    public class ProceduralBodyAnimator : MonoBehaviour
    {
        [Header("Lunghezza del passo (m) per un personaggio alto 1.8 m")]
        public float walkStride = 0.75f;
        public float jogStride = 1.15f;
        public float sprintStride = 1.6f;

        [Header("Fluidità")]
        public float gaitBlendSharpness = 6f;
        public float actionInSharpness = 18f;
        public float actionOutSharpness = 8f;
        public float landingDuration = 0.3f;

        struct GaitParams
        {
            public float thigh, knee, kneeBase, arm, elbow, lean, bob, stride;

            public static GaitParams Lerp(GaitParams a, GaitParams b, float t) => new GaitParams
            {
                thigh = Mathf.Lerp(a.thigh, b.thigh, t),
                knee = Mathf.Lerp(a.knee, b.knee, t),
                kneeBase = Mathf.Lerp(a.kneeBase, b.kneeBase, t),
                arm = Mathf.Lerp(a.arm, b.arm, t),
                elbow = Mathf.Lerp(a.elbow, b.elbow, t),
                lean = Mathf.Lerp(a.lean, b.lean, t),
                bob = Mathf.Lerp(a.bob, b.bob, t),
                stride = Mathf.Lerp(a.stride, b.stride, t),
            };
        }

        const int N = BodyRig.PartCount;

        BodyRig rig;
        CharacterMotor motor;
        Transform hips;
        Vector3 hipsRest;

        readonly Vector3[] loco = new Vector3[N];
        readonly Vector3[] action = new Vector3[N];
        float locoHipsDrop, actionHipsDrop;

        GaitParams gait;
        float phase, smoothSpeed, verticalSpeed, actionWeight;
        Vector3 lastPos;
        bool wasTraversing;
        float traversalEndTime = -10f;

        void Awake()
        {
            rig = GetComponent<BodyRig>();
            motor = GetComponentInParent<CharacterMotor>();
            hips = rig.Joint(BodyPart.Pelvis);
            if (hips != null) hipsRest = hips.localPosition;
            lastPos = transform.position;
            gait = ParamsFor(Gait.Jog);
        }

        GaitParams ParamsFor(Gait g)
        {
            switch (g)
            {
                case Gait.Walk:
                    return new GaitParams { thigh = 24f, knee = 35f, kneeBase = 4f, arm = 16f, elbow = -12f, lean = 2f, bob = 0.025f, stride = walkStride };
                case Gait.Sprint:
                    return new GaitParams { thigh = 55f, knee = 105f, kneeBase = 18f, arm = 55f, elbow = -95f, lean = 16f, bob = 0.05f, stride = sprintStride };
                default:
                    return new GaitParams { thigh = 38f, knee = 70f, kneeBase = 12f, arm = 30f, elbow = -75f, lean = 8f, bob = 0.04f, stride = jogStride };
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float scale = rig.height / 1.8f;

            // Velocità reale misurata dallo spostamento (vale anche durante salti e arrampicate).
            Vector3 pos = transform.position;
            Vector3 d = pos - lastPos;
            lastPos = pos;
            float horizontal = new Vector2(d.x, d.z).magnitude / dt;
            verticalSpeed = Mathf.Lerp(verticalSpeed, d.y / dt, 1f - Mathf.Exp(-20f * dt));

            bool traversing = motor != null && motor.IsTraversing;
            if (wasTraversing && !traversing) traversalEndTime = Time.time;
            wasTraversing = traversing;
            smoothSpeed = Mathf.Lerp(smoothSpeed, traversing ? 0f : horizontal, 1f - Mathf.Exp(-10f * dt));

            Gait g = motor != null ? motor.CurrentGait : Gait.Walk;
            gait = GaitParams.Lerp(gait, ParamsFor(g), 1f - Mathf.Exp(-gaitBlendSharpness * dt));
            float refSpeed = motor != null ? motor.SpeedFor(g) : 2f;
            float move = Mathf.Clamp01(smoothSpeed / Mathf.Max(0.5f, refSpeed * 0.5f));
            phase = Mathf.Repeat(phase + smoothSpeed * Mathf.PI / Mathf.Max(0.1f, gait.stride * scale) * dt, Mathf.PI * 2f);

            BuildLocomotion(move);
            bool acting = BuildAction();
            float sharp = acting ? actionInSharpness : actionOutSharpness;
            actionWeight = Mathf.Lerp(actionWeight, acting ? 1f : 0f, 1f - Mathf.Exp(-sharp * dt));

            for (int i = 0; i < N; i++)
            {
                Transform j = rig.joints[i];
                if (j != null) j.localRotation = Quaternion.Euler(Vector3.Lerp(loco[i], action[i], actionWeight));
            }
            if (hips != null)
                hips.localPosition = hipsRest + Vector3.down * (Mathf.Lerp(locoHipsDrop, actionHipsDrop, actionWeight) * scale);
        }

        // ------------------------------------------------------------------ locomozione

        void BuildLocomotion(float move)
        {
            System.Array.Clear(loco, 0, N);
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            float idle = 1f - move;
            float t = Time.time;

            Set(loco, BodyPart.ThighL, -gait.thigh * s * move - idle * 1.5f);
            Set(loco, BodyPart.ThighR, gait.thigh * s * move - idle * 1.5f);
            Set(loco, BodyPart.ShinL, (gait.kneeBase + gait.knee * Mathf.Max(0f, c)) * move + idle * 3f);
            Set(loco, BodyPart.ShinR, (gait.kneeBase + gait.knee * Mathf.Max(0f, -c)) * move + idle * 3f);
            // Il piede resta circa parallelo al terreno.
            Set(loco, BodyPart.FootL, -(X(loco, BodyPart.ThighL) + X(loco, BodyPart.ShinL)) * 0.8f);
            Set(loco, BodyPart.FootR, -(X(loco, BodyPart.ThighR) + X(loco, BodyPart.ShinR)) * 0.8f);

            loco[(int)BodyPart.UpperArmL] = new Vector3(gait.arm * s * move, 0f, -4f - 2f * move);
            loco[(int)BodyPart.UpperArmR] = new Vector3(-gait.arm * s * move, 0f, 4f + 2f * move);
            Set(loco, BodyPart.ForearmL, gait.elbow * move - idle * 10f);
            Set(loco, BodyPart.ForearmR, gait.elbow * move - idle * 10f);

            float breath = Mathf.Sin(t * 1.7f);
            float twist = gait.arm * s * move;
            loco[(int)BodyPart.Pelvis] = new Vector3(0f, -twist * 0.12f, 0f);
            loco[(int)BodyPart.Abdomen] = new Vector3(gait.lean * move * 0.5f, twist * 0.12f, 0f);
            loco[(int)BodyPart.Chest] = new Vector3(gait.lean * move * 0.5f + breath * 1.2f * idle, twist * 0.18f, 0f);
            loco[(int)BodyPart.Head] = new Vector3(-gait.lean * move * 0.7f, -twist * 0.18f + Mathf.Sin(t * 0.37f) * 6f * idle, 0f);

            locoHipsDrop = gait.bob * Mathf.Abs(s) * move + idle * 0.01f;
        }

        // ------------------------------------------------------------------ azioni

        bool BuildAction()
        {
            System.Array.Clear(action, 0, N);
            actionHipsDrop = 0f;
            if (motor == null) return false;

            if (motor.IsStunned) { HardLandingPose(); return true; }

            switch (motor.CurrentTraversal)
            {
                case TraversalKind.Vault:
                    VaultPose();
                    return true;
                case TraversalKind.Climb:
                case TraversalKind.ClimbHigh:
                case TraversalKind.ClimbUp:
                    if (verticalSpeed > 0.3f) ClimbPose(Time.time); else VaultPose();
                    return true;
                case TraversalKind.Drop:
                    if (verticalSpeed > 0.2f) TakeOffPose(); else FallPose();
                    return true;
            }

            float since = Time.time - Mathf.Max(motor.LastLandingTime, traversalEndTime);
            if (since < landingDuration) { SoftLandingPose(); return true; }
            return false;
        }

        void VaultPose()
        {
            Set(action, BodyPart.ThighL, -95f); Set(action, BodyPart.ShinL, 110f); Set(action, BodyPart.FootL, -20f);
            Set(action, BodyPart.ThighR, -40f); Set(action, BodyPart.ShinR, 80f); Set(action, BodyPart.FootR, -30f);
            Set(action, BodyPart.Abdomen, 25f); Set(action, BodyPart.Chest, 10f); Set(action, BodyPart.Head, -25f);
            action[(int)BodyPart.UpperArmL] = new Vector3(-75f, 0f, -10f); Set(action, BodyPart.ForearmL, -5f);
            action[(int)BodyPart.UpperArmR] = new Vector3(-45f, 0f, 35f); Set(action, BodyPart.ForearmR, -30f);
            actionHipsDrop = -0.03f;
        }

        void ClimbPose(float t)
        {
            float w = Mathf.Sin(t * 9f);
            action[(int)BodyPart.UpperArmL] = new Vector3(-165f + 18f * w, 0f, -8f);
            action[(int)BodyPart.UpperArmR] = new Vector3(-165f - 18f * w, 0f, 8f);
            Set(action, BodyPart.ForearmL, -30f + 20f * w);
            Set(action, BodyPart.ForearmR, -30f - 20f * w);
            Set(action, BodyPart.ThighL, -55f + 30f * w); Set(action, BodyPart.ShinL, 75f - 30f * w);
            Set(action, BodyPart.ThighR, -55f - 30f * w); Set(action, BodyPart.ShinR, 75f + 30f * w);
            Set(action, BodyPart.FootL, -10f); Set(action, BodyPart.FootR, -10f);
            Set(action, BodyPart.Abdomen, 8f); Set(action, BodyPart.Head, -25f);
        }

        void TakeOffPose()
        {
            action[(int)BodyPart.UpperArmL] = new Vector3(-120f, 0f, -30f);
            action[(int)BodyPart.UpperArmR] = new Vector3(-120f, 0f, 30f);
            Set(action, BodyPart.ForearmL, -15f); Set(action, BodyPart.ForearmR, -15f);
            Set(action, BodyPart.ThighL, -40f); Set(action, BodyPart.ShinL, 60f);
            Set(action, BodyPart.ThighR, -10f); Set(action, BodyPart.ShinR, 25f);
            Set(action, BodyPart.Abdomen, -5f); Set(action, BodyPart.Head, -10f);
        }

        void FallPose()
        {
            action[(int)BodyPart.UpperArmL] = new Vector3(-100f, 0f, -60f);
            action[(int)BodyPart.UpperArmR] = new Vector3(-100f, 0f, 60f);
            Set(action, BodyPart.ForearmL, -20f); Set(action, BodyPart.ForearmR, -20f);
            Set(action, BodyPart.ThighL, -35f); Set(action, BodyPart.ShinL, 55f);
            Set(action, BodyPart.ThighR, -20f); Set(action, BodyPart.ShinR, 40f);
            Set(action, BodyPart.FootL, -15f); Set(action, BodyPart.FootR, -15f);
            Set(action, BodyPart.Abdomen, 5f); Set(action, BodyPart.Head, 15f);
        }

        void SoftLandingPose()
        {
            Set(action, BodyPart.ThighL, -65f); Set(action, BodyPart.ThighR, -65f);
            Set(action, BodyPart.ShinL, 100f); Set(action, BodyPart.ShinR, 100f);
            Set(action, BodyPart.FootL, -35f); Set(action, BodyPart.FootR, -35f);
            Set(action, BodyPart.Abdomen, 25f); Set(action, BodyPart.Chest, 8f); Set(action, BodyPart.Head, -15f);
            action[(int)BodyPart.UpperArmL] = new Vector3(-35f, 0f, -15f);
            action[(int)BodyPart.UpperArmR] = new Vector3(-35f, 0f, 15f);
            Set(action, BodyPart.ForearmL, -30f); Set(action, BodyPart.ForearmR, -30f);
            actionHipsDrop = 0.28f;
        }

        void HardLandingPose()
        {
            Set(action, BodyPart.ThighL, -95f); Set(action, BodyPart.ThighR, -80f);
            Set(action, BodyPart.ShinL, 130f); Set(action, BodyPart.ShinR, 115f);
            Set(action, BodyPart.FootL, -35f); Set(action, BodyPart.FootR, -35f);
            Set(action, BodyPart.Abdomen, 40f); Set(action, BodyPart.Chest, 15f); Set(action, BodyPart.Head, -35f);
            action[(int)BodyPart.UpperArmR] = new Vector3(-75f, 0f, 10f); Set(action, BodyPart.ForearmR, -5f);   // mano a terra
            action[(int)BodyPart.UpperArmL] = new Vector3(-40f, 0f, -20f); Set(action, BodyPart.ForearmL, -40f);
            actionHipsDrop = 0.45f;
        }

        static void Set(Vector3[] pose, BodyPart part, float x) => pose[(int)part].x = x;
        static float X(Vector3[] pose, BodyPart part) => pose[(int)part].x;
    }
}
