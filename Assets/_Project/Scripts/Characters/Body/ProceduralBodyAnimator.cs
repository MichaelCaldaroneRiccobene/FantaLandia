using UnityEngine;

namespace FantaLandia.Characters.Body
{
    /// <summary>
    /// Animazione procedurale "fisica" del corpo a segmenti.
    ///
    /// A terra: i piedi si appoggiano davvero (raggi verso il suolo), restano fermi mentre il corpo avanza
    /// e fanno un passo quando restano indietro: niente scivolamento, e sulle scale si appoggiano sui gradini.
    /// Il bacino si abbassa sulla gamba più bassa, le gambe si piegano con l'IK, braccia e busto
    /// controbilanciano i passi. Atterraggi su una molla: più si cade veloci, più il corpo affonda.
    ///
    /// Attraversamenti: legge le fasi e la geometria misurata dal CharacterMotor e mette mani e piedi
    /// sul bordo / sulla faccia del muro con l'IK.
    ///
    /// Convenzioni FK (asse X locale dei giunti): coscia/braccio X- = avanti; ginocchio X+ = piega;
    /// gomito X- = piega; schiena/testa X+ = avanti/giù; braccio sinistro Z- = fuori, destro Z+.
    /// </summary>
    [RequireComponent(typeof(BodyRig))]
    public class ProceduralBodyAnimator : MonoBehaviour
    {
        [Header("Respiro")]
        public float breathRate = 0.28f;      // respiri al secondo, a riposo
        public float breathAmplitude = 2.6f;  // gradi di torace

        [Header("Atterraggio (molla del bacino)")]
        public float crouchStiffness = 110f;
        public float crouchDamping = 13f;
        public float landingImpulse = 0.25f;

        [Header("Pose")]
        public float poseSharpness = 14f;
        public float hipsSharpness = 14f;

        [Header("Senza CharacterMotor (es. giocatore in prima persona)")]
        [Tooltip("Andatura usata quando il personaggio non ha un CharacterMotor")]
        public Gait gaitOverride = Gait.Walk;
        [Tooltip("Velocità di riferimento dell'andatura quando non c'è un CharacterMotor")]
        public float speedOverride = 1.8f;
        [Tooltip("Accovacciamento imposto dall'esterno (m): il bacino scende, le gambe si piegano")]
        public float externalCrouch;
        [Tooltip("Layer ignorati dai raggi dei piedi (di default: Ignore Raycast, dove sta il giocatore)")]
        public LayerMask footIgnoreLayers = 1 << 2;

        /// <summary>Un piede ha appena toccato terra (lato -1/+1, punto d'appoggio): per i suoni dei passi.</summary>
        public event System.Action<int, Vector3> FootPlanted;

        [Tooltip("Inclinazione in avanti del busto imposta dall'esterno (gradi), es. appoggiati a una ringhiera")]
        public float externalLean;

        [Tooltip("Porta qualcosa nella mano sinistra (braccio teso lungo il fianco, niente oscillazione)")]
        public bool carryLeft;

        // mani guidate dall'esterno (bussare, posare oggetti...): valgono per un solo frame
        readonly Vector3[] extTarget = new Vector3[2];
        readonly Vector3[] extPole = new Vector3[2];
        readonly float[] extWeight = new float[2];
        readonly Quaternion[] extRot = new Quaternion[2];
        readonly bool[] extHasRot = new bool[2];
        readonly bool[] extActive = new bool[4];

        /// <summary>
        /// Guida una mano (lato -1 sinistra, +1 destra) verso un punto per questo frame.
        /// Va chiamata ogni frame (es. da Update) finché serve; peso applicato senza ritardo.
        /// </summary>
        public void SetExternalHandIK(int side, Vector3 wristTarget, Vector3 elbowPole, float weight, Quaternion? handRotation = null)
        {
            int k = side < 0 ? 0 : 1;
            extTarget[k] = wristTarget;
            extPole[k] = elbowPole;
            extWeight[k] = Mathf.Clamp01(weight);
            extHasRot[k] = handRotation.HasValue;
            if (handRotation.HasValue) extRot[k] = handRotation.Value;
        }

        /// <summary>Posizione attuale del polso (per partire da lì con un movimento guidato).</summary>
        public Vector3 HandPosition(int side) => side < 0 ? handL.position : handR.position;

        struct GaitParams
        {
            public float stride, swingFrac, stepHeight, arm, elbow, lean, bob, stance;

            public static GaitParams Lerp(GaitParams a, GaitParams b, float t) => new GaitParams
            {
                stride = Mathf.Lerp(a.stride, b.stride, t), swingFrac = Mathf.Lerp(a.swingFrac, b.swingFrac, t),
                stepHeight = Mathf.Lerp(a.stepHeight, b.stepHeight, t),
                arm = Mathf.Lerp(a.arm, b.arm, t), elbow = Mathf.Lerp(a.elbow, b.elbow, t),
                lean = Mathf.Lerp(a.lean, b.lean, t), bob = Mathf.Lerp(a.bob, b.bob, t), stance = Mathf.Lerp(a.stance, b.stance, t),
            };
        }

        static GaitParams ParamsFor(Gait g)
        {
            switch (g)
            {
                case Gait.Walk:
                    return new GaitParams { stride = 1.35f, swingFrac = 0.40f, stepHeight = 0.07f, arm = 22f, elbow = -15f, lean = 3f, bob = 0.02f, stance = 0.10f };
                case Gait.Sprint:
                    return new GaitParams { stride = 3.0f, swingFrac = 0.62f, stepHeight = 0.20f, arm = 55f, elbow = -95f, lean = 15f, bob = 0.035f, stance = 0.075f };
                default:
                    return new GaitParams { stride = 2.1f, swingFrac = 0.55f, stepHeight = 0.13f, arm = 35f, elbow = -75f, lean = 8f, bob = 0.03f, stance = 0.085f };
            }
        }

        class Foot
        {
            public int side;                       // -1 sinistra, +1 destra
            public Vector3 plant, from, to, pos;
            public Vector3 normal = Vector3.up, toNormal = Vector3.up;
            public bool stepping;
            public float t, duration, lift;
            public float GroundY => stepping ? to.y : plant.y;
        }

        const int N = BodyRig.PartCount;

        BodyRig rig;
        BodyHealth health;
        float koBlend;
        bool lying;
        CharacterMotor motor;
        Transform hips, thighL, shinL, footL, thighR, shinR, footR, armL, foreL, handL, armR, foreR, handR;
        Vector3 hipsRest, armLRest, armRRest;
        float ankleH, legLen, hipDrop, hipHalfWidth;

        readonly Vector3[] target = new Vector3[N];
        readonly Vector3[] pose = new Vector3[N];
        readonly Foot[] feet = { new Foot { side = -1 }, new Foot { side = 1 } };
        int lastStepped = 1;
        bool feetInitialized;

        GaitParams gait = ParamsFor(Gait.Walk);
        Vector3 lastPos, flatVel, prevFlatVel, accel;
        float vy, airVy, exertion, breathPhase;
        float crouch, crouchVel;
        bool wasTraversing;
        float settleTimer;

        // limbi IK: 0 = gamba sx, 1 = gamba dx, 2 = braccio sx, 3 = braccio dx
        readonly float[] ikWeight = new float[4];
        readonly float[] ikTargetWeight = new float[4];
        readonly Vector3[] ikTarget = new Vector3[4];
        readonly Vector3[] ikPole = new Vector3[4];
        readonly Quaternion[] endRotation = new Quaternion[4];
        readonly bool[] hasEndRotation = new bool[4];

        Vector3 desiredHipsLocal;

        // posa idle
        int supportSide = 1;
        float idleSwapTimer = 9f;

        void Awake()
        {
            rig = GetComponent<BodyRig>();
            motor = GetComponentInParent<CharacterMotor>();
            health = GetComponentInParent<BodyHealth>();

            hips = rig.Joint(BodyPart.Pelvis);
            thighL = rig.Joint(BodyPart.ThighL); shinL = rig.Joint(BodyPart.ShinL); footL = rig.Joint(BodyPart.FootL);
            thighR = rig.Joint(BodyPart.ThighR); shinR = rig.Joint(BodyPart.ShinR); footR = rig.Joint(BodyPart.FootR);
            armL = rig.Joint(BodyPart.UpperArmL); foreL = rig.Joint(BodyPart.ForearmL); handL = rig.Joint(BodyPart.HandL);
            armR = rig.Joint(BodyPart.UpperArmR); foreR = rig.Joint(BodyPart.ForearmR); handR = rig.Joint(BodyPart.HandR);

            hipsRest = hips.localPosition;
            armLRest = armL.localPosition;
            armRRest = armR.localPosition;
            // Misure dalla posa di riposo del prefab.
            legLen = shinL.localPosition.magnitude + footL.localPosition.magnitude;
            hipDrop = -thighL.localPosition.y;
            hipHalfWidth = Mathf.Abs(thighL.localPosition.x);
            ankleH = Mathf.Max(0.03f, hipsRest.y - hipDrop - legLen);

            desiredHipsLocal = hipsRest;
            lastPos = transform.position;
            breathPhase = Random.value * 10f;
            idleSwapTimer = Random.Range(6f, 12f);
            supportSide = Random.value < 0.5f ? -1 : 1;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateKinematics(dt);

            // svenuto: si accascia a terra; al risveglio si rialza
            bool ko = health != null && health.IsUnconscious;
            koBlend = Mathf.MoveTowards(koBlend, ko ? 1f : 0f, dt * (ko ? 1.8f : 0.8f));
            if (ko || koBlend > 0.001f)
            {
                LyingUpdate(dt);
                return;
            }
            if (lying)
            {
                lying = false;
                transform.localRotation = Quaternion.identity;
                transform.localPosition = Vector3.zero;
                feetInitialized = false;
            }

            // seduto: posa seduta, niente passi
            if (motor != null && motor.IsSeated)
            {
                SeatedUpdate(dt);
                return;
            }
            seatHandW = 0f;

            bool traversing = motor != null && motor.IsTraversing && !motor.IsStunned;
            if (wasTraversing && !traversing) OnTraversalEnded();
            if (traversing) airVy = vy;
            wasTraversing = traversing;

            // respiro: più veloce e profondo dopo uno sforzo
            breathPhase += dt * Mathf.PI * 2f * (breathRate + exertion * 0.55f);
            float breath = Mathf.Sin(breathPhase);

            System.Array.Clear(target, 0, N);
            for (int i = 0; i < 4; i++) { ikTargetWeight[i] = 0f; hasEndRotation[i] = false; }

            if (traversing) BuildTraversal(motor.Traversal, breath);
            else BuildGround(dt, breath);
            ApplyDisabledArms();
            ApplyCarryAndExternal();
            if (Mathf.Abs(externalLean) > 0.01f && !traversing)
            {
                Add(BodyPart.Abdomen, externalLean * 0.55f, 0f, 0f);
                Add(BodyPart.Chest, externalLean * 0.45f, 0f, 0f);
                Add(BodyPart.Head, -externalLean * 0.6f, 0f, 0f);
            }

            // FK con transizioni morbide
            float k = 1f - Mathf.Exp(-poseSharpness * dt);
            for (int i = 0; i < N; i++)
            {
                pose[i] = Vector3.Lerp(pose[i], target[i], k);
                Transform j = rig.joints[i];
                if (j != null) j.localRotation = Quaternion.Euler(pose[i]);
            }

            // bacino + molla dell'atterraggio
            float crouchTarget = motor != null && motor.IsStunned ? 0.42f : 0f;
            crouchVel += (-crouchStiffness * (crouch - crouchTarget) - crouchDamping * crouchVel) * dt;
            crouch = Mathf.Clamp(crouch + crouchVel * dt, -0.05f, 0.5f);
            float hk = 1f - Mathf.Exp(-(traversing ? 30f : hipsSharpness) * dt);
            Vector3 hp = Vector3.Lerp(hips.localPosition + Vector3.up * lastCrouch, desiredHipsLocal, hk);
            hips.localPosition = hp + Vector3.down * crouch;
            lastCrouch = crouch;

            // spalle che salgono con il respiro
            float lift = breath * 0.005f * (1f + exertion);
            armL.localPosition = armLRest + Vector3.up * lift;
            armR.localPosition = armRRest + Vector3.up * lift;

            SolveIK(dt);
        }

        float lastCrouch;

        // ------------------------------------------------------------------ movimento misurato

        void UpdateKinematics(float dt)
        {
            Vector3 pos = transform.position;
            Vector3 v = (pos - lastPos) / dt;
            lastPos = pos;
            vy = v.y;
            Vector3 vf = new Vector3(v.x, 0f, v.z);
            if (vf.sqrMagnitude > 400f) vf = Vector3.zero; // teletrasporti
            flatVel = Vector3.Lerp(flatVel, vf, 1f - Mathf.Exp(-12f * dt));
            accel = Vector3.Lerp(accel, (flatVel - prevFlatVel) / dt, 1f - Mathf.Exp(-6f * dt));
            prevFlatVel = flatVel;

            float rate;
            float speed = flatVel.magnitude;
            Gait g = motor != null ? motor.CurrentGait : gaitOverride;
            if (speed < 0.3f) rate = -0.08f;
            else if (g == Gait.Sprint) rate = 0.12f;
            else if (g == Gait.Jog) rate = 0.03f;
            else rate = -0.05f;
            exertion = Mathf.Clamp01(exertion + rate * dt);
        }

        void OnTraversalEnded()
        {
            // I piedi ripartono da dove sono, appoggiati al suolo.
            PlantFootAt(feet[0], footL.position);
            PlantFootAt(feet[1], footR.position);
            crouchVel += Mathf.Max(0f, -airVy) * landingImpulse;
        }

        void PlantFootAt(Foot f, Vector3 p)
        {
            Ground(p, out Vector3 g, out Vector3 n);
            f.plant = f.pos = f.to = g;
            f.normal = f.toNormal = n;
            f.stepping = false;
        }

        bool Ground(Vector3 p, out Vector3 point, out Vector3 normal)
        {
            Vector3 origin = new Vector3(p.x, p.y + 0.8f, p.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.8f, ~footIgnoreLayers.value, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                normal = Vector3.Angle(hit.normal, Vector3.up) < 40f ? hit.normal : Vector3.up;
                return true;
            }
            point = new Vector3(p.x, transform.position.y, p.z);
            normal = Vector3.up;
            return false;
        }

        // ------------------------------------------------------------------ a terra

        void BuildGround(float dt, float breath)
        {
            Gait g = motor != null ? motor.CurrentGait : gaitOverride;
            gait = GaitParams.Lerp(gait, ParamsFor(g), 1f - Mathf.Exp(-6f * dt));

            Vector3 fwd = transform.forward, right = transform.right, root = transform.position;
            float speed = flatVel.magnitude;
            Vector3 velDir = speed > 0.05f ? flatVel / speed : fwd;
            float gaitSpeed = motor != null ? motor.SpeedFor(g) : speedOverride;
            float sf = Mathf.Clamp01(speed / Mathf.Max(0.5f, gaitSpeed * 0.7f));
            float idle = 1f - Mathf.Clamp01(speed / 0.6f);
            bool stunned = motor != null && motor.IsStunned;
            bool female = rig.female;

            // posa idle: la donna alterna la gamba d'appoggio (contrapposto)
            if (idle > 0.9f)
            {
                idleSwapTimer -= dt;
                if (idleSwapTimer <= 0f) { supportSide = -supportSide; idleSwapTimer = Random.Range(7f, 13f); }
            }

            if (!feetInitialized)
            {
                PlantFootAt(feet[0], root - right * 0.1f);
                PlantFootAt(feet[1], root + right * 0.1f);
                feetInitialized = true;
            }

            // --- piedi
            Vector3[] home = new Vector3[2];
            float[] footYaw = new float[2];
            for (int i = 0; i < 2; i++)
            {
                Foot f = feet[i];
                float idleHalf = female ? 0.075f : 0.13f;
                Vector3 idleOffset = Vector3.zero;
                footYaw[i] = female ? 6f : 9f;
                if (female && f.side != supportSide)
                {
                    idleOffset = fwd * 0.08f + right * (f.side * 0.03f); // gamba libera avanti e un po' fuori
                    footYaw[i] = 22f;
                }
                else if (female)
                {
                    idleOffset = right * (-f.side * 0.02f); // gamba d'appoggio sotto al corpo
                }
                float half = Mathf.Lerp(idleHalf, gait.stance, 1f - idle);
                home[i] = root + right * (f.side * half) + idleOffset * idle;
                footYaw[i] *= idle;
            }
            UpdateFeet(dt, home, velDir, speed, sf);

            // --- bacino: si abbassa sul piede più basso, sobbalza a ogni passo
            float yLow = Mathf.Min(feet[0].GroundY, feet[1].GroundY);
            float reach = yLow + ankleH + legLen * 0.975f + hipDrop;
            float restY = root.y + hipsRest.y;
            float stepLift = 0f;
            foreach (Foot f in feet) if (f.stepping) stepLift = Mathf.Max(stepLift, Mathf.Sin(Mathf.PI * Mathf.Clamp01(f.t)));
            float hipY = Mathf.Min(restY, reach) + gait.bob * sf * stepLift - externalCrouch;
            float hipsX = 0f;
            if (female) { hipsX = 0.035f * supportSide * idle; hipY -= 0.012f * idle; }
            else hipsX = Mathf.Sin(Time.time * 0.4f) * 0.01f * idle;
            // zoppia: il bacino cede quando il peso è sulla gamba ferita
            int limpSide = health != null ? health.LimpSide : 0;
            float limp = health != null ? health.LimpAmount : 0f;
            if (limpSide != 0 && limp > 0.05f && cycleMoving)
            {
                Foot inj = feet[limpSide < 0 ? 0 : 1], oth = feet[limpSide < 0 ? 1 : 0];
                if (!inj.stepping && oth.stepping)
                {
                    float dip = Mathf.Sin(Mathf.PI * Mathf.Clamp01(oth.t));
                    hipY -= 0.07f * limp * dip;
                    Add(BodyPart.Pelvis, 0f, 0f, -limpSide * 7f * limp * dip);
                    Add(BodyPart.Chest, 0f, 0f, limpSide * 4f * limp * dip);
                }
            }
            desiredHipsLocal = new Vector3(hipsX, hipY - root.y, 0f);

            // --- busto e braccia che controbilanciano i passi
            Vector3 hipsW = hips.position;
            float swingNorm = 0.3f + 0.3f * sf;
            float zL = Vector3.Dot(feet[0].pos - hipsW, fwd) / swingNorm;
            float zR = Vector3.Dot(feet[1].pos - hipsW, fwd) / swingNorm;
            zL = Mathf.Clamp(zL, -1f, 1f) * sf;
            zR = Mathf.Clamp(zR, -1f, 1f) * sf;
            float twist = zL - zR;

            float accelF = Mathf.Clamp(Vector3.Dot(accel, fwd) * 1.5f, -8f, 10f);
            float lean = gait.lean * sf + accelF * (1f - idle);
            float amp = breathAmplitude * (1f + exertion * 1.5f);

            Rot(BodyPart.Pelvis, 0f, -twist * 8f, 0f);
            Rot(BodyPart.Abdomen, lean * 0.5f + breath * amp * 0.35f, twist * 5f, 0f);
            Rot(BodyPart.Chest, lean * 0.5f - breath * amp, twist * 7f, 0f);
            Rot(BodyPart.Head, -lean * 0.8f + breath * amp * 0.3f, -twist * 6f, 0f);
            Rot(BodyPart.UpperArmL, -zR * gait.arm, 0f, -4f - 3f * sf);
            Rot(BodyPart.UpperArmR, -zL * gait.arm, 0f, 4f + 3f * sf);
            Rot(BodyPart.ForearmL, gait.elbow * sf - 12f * idle, 0f, 0f);
            Rot(BodyPart.ForearmR, gait.elbow * sf - 12f * idle, 0f, 0f);
            Rot(BodyPart.HandL, 0f, 0f, 4f * idle);
            Rot(BodyPart.HandR, 0f, 0f, -4f * idle);

            // --- pose idle caratteristiche
            float t = Time.time;
            if (female)
            {
                int s = supportSide;
                Add(BodyPart.Pelvis, 0f, 0f, 5f * s * idle);
                Add(BodyPart.Abdomen, 0f, 0f, -2f * s * idle);
                Add(BodyPart.Chest, -1.5f * idle, 0f, -3.5f * s * idle);
                Add(BodyPart.Head, 0f, Mathf.Sin(t * 0.21f) * 12f * idle, 5f * s * idle);
                // mano sul fianco dal lato d'appoggio, l'altra rilassata
                int arm = s < 0 ? 2 : 3;
                Vector3 hipSide = hipsW + right * (s * (hipHalfWidth * 1.9f + 0.05f)) + Vector3.up * 0.06f - fwd * 0.01f;
                SetIK(arm, hipSide, right * s - fwd * 0.4f + Vector3.down * 0.2f, idle * idle);
                Add(s < 0 ? BodyPart.UpperArmR : BodyPart.UpperArmL, -3f * idle, 0f, 0f);
            }
            else
            {
                Add(BodyPart.Chest, -3f * idle, 0f, 0f); // petto in fuori
                Add(BodyPart.Head, -1f * idle, Mathf.Sin(t * 0.23f) * 10f * idle + Mathf.Sin(t * 0.61f) * 3f * idle, 0f);
                Add(BodyPart.UpperArmL, 0f, 0f, -5f * idle);
                Add(BodyPart.UpperArmR, 0f, 0f, 5f * idle);
                Add(BodyPart.Pelvis, 0f, Mathf.Sin(t * 0.4f) * 2f * idle, Mathf.Sin(t * 0.4f) * 1.5f * idle);
            }

            if (stunned)
            {
                // atterraggio duro: busto giù, una mano a terra
                Add(BodyPart.Abdomen, 35f, 0f, 0f);
                Add(BodyPart.Chest, 12f, 0f, 0f);
                Add(BodyPart.Head, -30f, 0f, 0f);
                Ground(root + fwd * 0.4f + right * 0.18f, out Vector3 handGround, out _);
                SetIK(3, handGround + Vector3.up * 0.05f, -fwd + right, 1f);
                SetEnd(3, PalmDown(1, fwd));
                SetIK(2, root + fwd * 0.15f - right * 0.25f + Vector3.up * 0.55f, -fwd - right, 0.6f);
            }

            // --- gambe sui piedi
            for (int i = 0; i < 2; i++)
            {
                Foot f = feet[i];
                Vector3 kneePole = fwd + right * (f.side * 0.15f);
                SetIK(i, f.pos + f.normal * ankleH, kneePole, 1f);
                SetEnd(i, FootRotation(f, fwd, footYaw[i]));
            }
        }

        float cyclePhase;
        bool cycleMoving;

        /// <summary>
        /// In movimento: ciclo del passo legato alla velocità (i due piedi sono sfasati di mezzo ciclo).
        /// Il punto d'atterraggio del piede in volo viene ricalcolato ogni frame, così segue curve e inversioni.
        /// Da fermo: i piedi tornano uno alla volta nella posa di riposo.
        /// </summary>
        void UpdateFeet(float dt, Vector3[] home, Vector3 velDir, float speed, float sf)
        {
            bool wasMoving = cycleMoving;
            cycleMoving = cycleMoving ? speed > 0.12f : speed > 0.25f;

            if (cycleMoving)
            {
                if (!wasMoving)
                {
                    // parte per primo il piede più indietro
                    float bL = -Vector3.Dot(feet[0].plant - home[0], velDir);
                    float bR = -Vector3.Dot(feet[1].plant - home[1], velDir);
                    cyclePhase = bL >= bR ? 0f : 0.5f;
                    foreach (Foot f in feet)
                        if (f.stepping) { f.stepping = false; PlantFootAt(f, f.pos); }
                }

                float cycle = Mathf.Clamp(gait.stride / Mathf.Max(0.1f, speed), 0.3f, 1.6f);
                cyclePhase = Mathf.Repeat(cyclePhase + dt / cycle, 1f);
                float lead = speed * cycle * (1f - gait.swingFrac) * 0.5f;
                float remain = DistanceToDestination();

                for (int i = 0; i < 2; i++)
                {
                    Foot f = feet[i];
                    float p = Mathf.Repeat(cyclePhase - (i == 0 ? 0f : 0.5f), 1f);
                    if (p < gait.swingFrac)
                    {
                        float u = p / gait.swingFrac;
                        if (!f.stepping)
                        {
                            f.stepping = true;
                            f.from = f.plant;
                        }
                        float remainingTime = (gait.swingFrac - p) * cycle;
                        Vector3 ahead = flatVel * remainingTime + velDir * lead;
                        if (ahead.magnitude > remain) ahead = ahead.normalized * remain; // non oltrepassare la meta
                        Ground(home[i] + ahead, out Vector3 to, out Vector3 n);
                        f.to = to;
                        f.toNormal = n;
                        f.t = u;
                        f.duration = gait.swingFrac * cycle;
                        f.lift = gait.stepHeight * Mathf.Lerp(0.6f, 1f, sf) * LimpLift(f.side) + (Mathf.Abs(to.y - f.from.y) > 0.05f ? 0.06f : 0f);
                        f.pos = SwingPosition(f, u);
                    }
                    else
                    {
                        if (f.stepping)
                        {
                            f.stepping = false;
                            f.plant = f.to;
                            f.normal = f.toNormal;
                            FootPlanted?.Invoke(f.side, f.plant);
                        }
                        f.pos = f.plant;
                    }
                }
                settleTimer = 0f;
                return;
            }

            // --- da fermo: completa i passi in corso, poi riallinea i piedi uno alla volta
            foreach (Foot f in feet)
            {
                if (!f.stepping) { f.pos = f.plant; continue; }
                f.t += dt / Mathf.Max(0.05f, f.duration);
                float u = Mathf.Clamp01(f.t);
                f.pos = SwingPosition(f, u);
                if (u >= 1f)
                {
                    f.stepping = false;
                    f.plant = f.pos = f.to;
                    f.normal = f.toNormal;
                    FootPlanted?.Invoke(f.side, f.plant);
                }
            }
            if (feet[0].stepping || feet[1].stepping) return;

            settleTimer += dt;
            if (settleTimer < 0.12f) return;
            int best = -1;
            float bestErr = 0.06f;
            for (int i = 0; i < 2; i++)
            {
                Vector3 off = home[i] - feet[i].plant;
                off.y = 0f;
                float err = off.magnitude * (i == lastStepped ? 0.8f : 1f);
                if (err > bestErr) { bestErr = err; best = i; }
            }
            if (best < 0) return;

            Foot s = feet[best];
            Ground(home[best], out Vector3 dest, out Vector3 dn);
            s.from = s.plant;
            s.to = dest;
            s.toNormal = dn;
            s.duration = 0.28f;
            s.lift = 0.05f + (Mathf.Abs(dest.y - s.from.y) > 0.05f ? 0.06f : 0f);
            s.t = 0f;
            s.stepping = true;
            lastStepped = best;
        }

        Vector3 SwingPosition(Foot f, float u)
        {
            float h = u * u * (3f - 2f * u);
            Vector3 p = Vector3.Lerp(f.from, f.to, h);
            float dy = f.to.y - f.from.y;
            float yu = dy > 0.02f ? Smooth01(u / 0.6f) : dy < -0.02f ? Smooth01((u - 0.4f) / 0.6f) : h;
            p.y = f.from.y + dy * yu + f.lift * Mathf.Sin(Mathf.PI * u);
            return p;
        }

        float DistanceToDestination()
        {
            if (motor == null || motor.Agent == null || !motor.Agent.enabled || !motor.Agent.hasPath) return float.MaxValue;
            Vector3 d = motor.Agent.destination - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        void ApplyCarryAndExternal()
        {
            if (carryLeft)
            {
                // braccio un po' in avanti (la confezione si vede guardando in basso) e staccato dalla gamba
                Rot(BodyPart.UpperArmL, -28f, 0f, -9f);
                Rot(BodyPart.ForearmL, -22f, 0f, 0f);
                Rot(BodyPart.HandL, 0f, 0f, 0f);
                if (!extActive[2]) ikTargetWeight[2] = 0f;
            }
            for (int k = 0; k < 2; k++)
            {
                int limb = 2 + k;
                extActive[limb] = extWeight[k] > 0.001f;
                if (!extActive[limb]) continue;
                SetIK(limb, extTarget[k], extPole[k], extWeight[k]);
                if (extHasRot[k]) SetEnd(limb, extRot[k]);
                extWeight[k] = 0f; // vale solo per questo frame
            }
        }

        /// <summary>La gamba ferita si solleva poco: il piede quasi striscia.</summary>
        float LimpLift(int side)
        {
            if (health == null || health.LimpSide != side) return 1f;
            return 1f - 0.6f * health.LimpAmount;
        }

        /// <summary>Braccio inutilizzabile: pende lungo il fianco, niente oscillazioni né IK.</summary>
        void ApplyDisabledArms()
        {
            if (health == null) return;
            for (int k = 0; k < 2; k++)
            {
                int side = k == 0 ? -1 : 1;
                if (!health.ArmDisabled(side)) continue;
                Rot(side < 0 ? BodyPart.UpperArmL : BodyPart.UpperArmR, 4f, 0f, side * 2f);
                Rot(side < 0 ? BodyPart.ForearmL : BodyPart.ForearmR, -4f, 0f, 0f);
                Rot(side < 0 ? BodyPart.HandL : BodyPart.HandR, 0f, 0f, 0f);
                ikTargetWeight[side < 0 ? 2 : 3] = 0f;
            }
        }

        /// <summary>
        /// Seduto: bacino sul cuscino, schiena appoggiata e un po' accasciata, cosce in avanti leggermente aperte,
        /// piedi a terra, mani appoggiate sulle cosce (IK).
        /// </summary>
        void SeatedUpdate(float dt)
        {
            feetInitialized = false;
            breathPhase += dt * Mathf.PI * 2f * (breathRate + exertion * 0.55f);
            float breath = Mathf.Sin(breathPhase);
            System.Array.Clear(target, 0, N);
            Rot(BodyPart.Pelvis, -8f, 0f, 0f);                                    // bacino che scivola un po' in avanti
            Rot(BodyPart.Abdomen, -10f + breath * breathAmplitude * 0.3f, 0f, 0f);  // schiena contro lo schienale
            Rot(BodyPart.Chest, 6f - breath * breathAmplitude, 0f, 0f);            // spalle un po' curve
            Rot(BodyPart.Neck, 8f, 0f, 0f);
            Rot(BodyPart.Head, 4f, 0f, 0f);
            // cosce orizzontali (contando il bacino inclinato), stinchi verticali, piedi piatti
            Rot(BodyPart.ThighL, -82f, -5f, -4f);
            Rot(BodyPart.ThighR, -82f, 5f, 4f);
            Rot(BodyPart.ShinL, 86f, 0f, 0f);
            Rot(BodyPart.ShinR, 90f, 0f, 0f);
            Rot(BodyPart.FootL, 4f, 0f, 0f);
            Rot(BodyPart.FootR, 0f, 0f, 0f);
            Rot(BodyPart.UpperArmL, -20f, 0f, -12f);
            Rot(BodyPart.UpperArmR, -20f, 0f, 12f);
            Rot(BodyPart.ForearmL, -45f, 0f, 0f);
            Rot(BodyPart.ForearmR, -45f, 0f, 0f);
            ApplyDisabledArms();

            float k = 1f - Mathf.Exp(-2.5f * dt); // ci si siede con calma
            for (int i = 0; i < N; i++)
            {
                pose[i] = Vector3.Lerp(pose[i], target[i], k);
                if (rig.joints[i] != null) rig.joints[i].localRotation = Quaternion.Euler(pose[i]);
            }
            // il bacino appoggiato sul cuscino (anca sopra il sedile + spessore della coscia), un po' indietro
            Vector3 seated = new Vector3(0f, motor.SeatHeight + hipDrop + 0.06f, -0.08f);
            hips.localPosition = Vector3.Lerp(hips.localPosition, seated, k);
            desiredHipsLocal = seated;
            crouch = crouchVel = lastCrouch = 0f;

            // mani appoggiate sulle cosce, a metà tra anca e ginocchio
            seatHandW = Mathf.MoveTowards(seatHandW, 1f, dt * 0.8f);
            for (int side = -1; side <= 1; side += 2)
            {
                if (health != null && health.ArmDisabled(side)) continue;
                Transform thigh = side < 0 ? thighL : thighR, knee = side < 0 ? shinL : shinR;
                Vector3 onThigh = Vector3.Lerp(thigh.position, knee.position, 0.6f) + Vector3.up * 0.1f - transform.right * side * 0.02f;
                Vector3 pole = transform.right * side * 0.8f - transform.forward * 0.6f + Vector3.down * 0.2f;
                Transform a = side < 0 ? armL : armR, b = side < 0 ? foreL : foreR, c = side < 0 ? handL : handR;
                TwoBoneIK.Solve(a, b, c, onThigh, pole, seatHandW, true);
                // mano distesa sulla coscia: dita in avanti, palmo in giù
                c.rotation = Quaternion.Slerp(c.rotation, PalmDown(side, transform.forward), seatHandW);
            }
            for (int i = 0; i < 4; i++) ikWeight[i] = 0f;
        }

        float seatHandW;

        /// <summary>Svenuto: steso sulla schiena, braccia e gambe abbandonate, respiro lento.</summary>
        void LyingUpdate(float dt)
        {
            lying = true;
            float e = Smooth01(koBlend);
            transform.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(-90f, 0f, 0f), e);
            transform.localPosition = new Vector3(0f, 0.12f * e, 0f);

            breathPhase += dt * Mathf.PI * 2f * 0.2f;
            float breath = Mathf.Sin(breathPhase);
            System.Array.Clear(target, 0, N);
            Rot(BodyPart.Head, 0f, 35f, 8f);
            Rot(BodyPart.Chest, -breath * 1.5f, 0f, 0f);
            Rot(BodyPart.UpperArmL, -10f, 0f, -40f);
            Rot(BodyPart.UpperArmR, -5f, 0f, 30f);
            Rot(BodyPart.ForearmL, -25f, 0f, 0f);
            Rot(BodyPart.ForearmR, -10f, 0f, 0f);
            Rot(BodyPart.ThighL, -8f, 0f, -4f);
            Rot(BodyPart.ThighR, -2f, 0f, 6f);
            Rot(BodyPart.ShinL, 15f, 0f, 0f);
            Rot(BodyPart.ShinR, 5f, 0f, 0f);
            Rot(BodyPart.FootL, 35f, 0f, 0f);
            Rot(BodyPart.FootR, 35f, 0f, 0f);

            float k = 1f - Mathf.Exp(-8f * dt);
            for (int i = 0; i < N; i++)
            {
                pose[i] = Vector3.Lerp(pose[i], target[i], k);
                if (rig.joints[i] != null) rig.joints[i].localRotation = Quaternion.Euler(pose[i]);
            }
            hips.localPosition = Vector3.Lerp(hips.localPosition, hipsRest, k);
            desiredHipsLocal = hipsRest;
            crouch = crouchVel = lastCrouch = 0f;
            for (int i = 0; i < 4; i++) ikWeight[i] = 0f;
        }

        Quaternion FootRotation(Foot f, Vector3 fwd, float yawOut)
        {
            float u = Mathf.Clamp01(f.t);
            Vector3 n = f.stepping ? Vector3.Slerp(f.normal, f.toNormal, u) : f.normal;
            Vector3 dir = Quaternion.AngleAxis(yawOut * f.side, Vector3.up) * fwd;
            dir = Vector3.ProjectOnPlane(dir, n);
            if (dir.sqrMagnitude < 1e-6f) dir = fwd;
            Quaternion r = Quaternion.LookRotation(dir.normalized, n);
            if (f.stepping) r *= Quaternion.Euler(Mathf.Sin(Mathf.PI * u) * Mathf.Lerp(25f, -20f, u), 0f, 0f); // punta giù allo stacco, su all'appoggio
            return r;
        }

        // ------------------------------------------------------------------ attraversamenti

        void BuildTraversal(TraversalState t, float breath)
        {
            Vector3 dir = t.dir, right = t.right, root = transform.position;
            Vector3 edge = t.Edge;
            float u = t.phaseT;
            float time = Time.time;
            desiredHipsLocal = hipsRest;

            Rot(BodyPart.Chest, -breath * breathAmplitude * 0.5f, 0f, 0f);

            switch (t.phase)
            {
                case TraversalPhase.Approach:
                {
                    // corsetta verso l'ostacolo (gambe FK)
                    float w = time * 13f;
                    Rot(BodyPart.ThighL, -35f * Mathf.Sin(w), 0f, 0f);
                    Rot(BodyPart.ThighR, 35f * Mathf.Sin(w), 0f, 0f);
                    Rot(BodyPart.ShinL, 20f + 45f * Mathf.Max(0f, Mathf.Cos(w)), 0f, 0f);
                    Rot(BodyPart.ShinR, 20f + 45f * Mathf.Max(0f, -Mathf.Cos(w)), 0f, 0f);
                    Rot(BodyPart.UpperArmL, 30f * Mathf.Sin(w), 0f, -6f);
                    Rot(BodyPart.UpperArmR, -30f * Mathf.Sin(w), 0f, 6f);
                    Rot(BodyPart.ForearmL, -70f, 0f, 0f);
                    Rot(BodyPart.ForearmR, -70f, 0f, 0f);
                    Rot(BodyPart.Abdomen, 8f, 0f, 0f);
                    break;
                }

                case TraversalPhase.Vault when t.kind == TraversalKind.Climb:
                {
                    // Volteggio laterale (parkour): mano destra sul bordo, gambe insieme di lato a sinistra.
                    float bell = Mathf.Sin(Mathf.PI * u);
                    Rot(BodyPart.Pelvis, 0f, -30f * bell, 15f * bell);
                    Rot(BodyPart.Abdomen, 15f, 0f, -8f * bell);
                    Rot(BodyPart.Chest, 12f, 20f * bell, -6f * bell);
                    Rot(BodyPart.Head, -10f, 25f * bell, 0f);
                    Rot(BodyPart.UpperArmL, -25f, 0f, -85f * bell - 10f); // braccio libero aperto per l'equilibrio
                    Rot(BodyPart.ForearmL, -15f, 0f, 0f);
                    Rot(BodyPart.ThighL, -75f, 0f, 0f); Rot(BodyPart.ShinL, 95f, 0f, 0f);
                    Rot(BodyPart.ThighR, -70f, 0f, 0f); Rot(BodyPart.ShinR, 85f, 0f, 0f);

                    Vector3 hand = edge + dir * 0.1f + right * 0.05f;
                    float handW = Ramp(u, 0f, 0.12f) * (1f - Ramp(u, 0.6f, 0.8f));
                    SetIK(3, hand + Vector3.up * 0.03f - dir * 0.04f, -dir + right, handW);
                    SetEnd(3, PalmDown(1, dir));

                    Vector3 hipsW = hips.position;
                    float legW = Ramp(u, 0.1f, 0.3f) * (1f - Ramp(u, 0.78f, 0.95f));
                    for (int i = 0; i < 2; i++)
                    {
                        Vector3 p = hipsW - right * (0.38f + 0.06f * i) + Vector3.down * 0.3f + dir * (0.08f * (1 - i));
                        p.y = Mathf.Max(p.y, t.topY + 0.08f + ankleH);
                        SetIK(i, p, dir - right * 0.5f, legW);
                    }
                    break;
                }

                case TraversalPhase.Vault when t.stepOver:
                    StepOverPose(t, u);
                    break;

                case TraversalPhase.Vault:
                    LowVaultPose(t, u);
                    break;

                case TraversalPhase.Jump when t.kind == TraversalKind.GapJump:
                    LongJumpPose(u);
                    break;

                case TraversalPhase.Jump when t.kind == TraversalKind.Drop:
                    TakeOffPose();
                    break;

                case TraversalPhase.Jump when t.kind == TraversalKind.ClimbHigh && t.topY < root.y + 0.5f:
                    // slancio dalla cima del muro alto verso il basso
                    TakeOffPose();
                    break;

                case TraversalPhase.Jump:
                case TraversalPhase.Scramble:
                case TraversalPhase.PullUp:
                case TraversalPhase.Mantle:
                    ClimbPose(t, u, time);
                    break;

                case TraversalPhase.Fall:
                    FallPose();
                    break;
            }
        }

        /// <summary>Arrampicata sul muro alto: mani sul bordo, piedi che cercano appoggio sulla parete, trazione, scavalco.</summary>
        void ClimbPose(TraversalState t, float u, float time)
        {
            Vector3 dir = t.dir, right = t.right, root = transform.position;
            Vector3 edge = t.Edge;
            Vector3 wall = t.nearFace - dir * 0.03f; // faccia del muro, a terra

            // mani sul bordo
            float handW = 1f;
            if (t.phase == TraversalPhase.Jump) handW = Ramp(u, 0f, 0.8f);
            if (t.phase == TraversalPhase.Mantle) handW = 1f - Ramp(u, 0.65f, 0.9f);
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                Vector3 h = edge + right * (side * 0.22f) - dir * 0.05f + Vector3.up * 0.03f;
                SetIK(2 + i, h, -dir * 0.6f + right * side + Vector3.down * 0.3f, handW);
                SetEnd(2 + i, PalmDown(side, dir));
            }
            Rot(BodyPart.UpperArmL, -165f, 0f, -10f);
            Rot(BodyPart.UpperArmR, -165f, 0f, 10f);
            Rot(BodyPart.ForearmL, -20f, 0f, 0f);
            Rot(BodyPart.ForearmR, -20f, 0f, 0f);

            switch (t.phase)
            {
                case TraversalPhase.Jump:
                    Rot(BodyPart.Abdomen, 8f, 0f, 0f);
                    Rot(BodyPart.Head, -25f, 0f, 0f);
                    Rot(BodyPart.ThighL, -30f, 0f, 0f); Rot(BodyPart.ShinL, 50f, 0f, 0f);
                    Rot(BodyPart.ThighR, -15f, 0f, 0f); Rot(BodyPart.ShinR, 30f, 0f, 0f);
                    break;

                case TraversalPhase.Scramble:
                {
                    // i piedi spingono a turno contro la parete cercando appoggio
                    Rot(BodyPart.Abdomen, 10f, 0f, 0f);
                    Rot(BodyPart.Chest, 6f, 0f, 0f);
                    Rot(BodyPart.Head, -20f, 0f, 0f);
                    for (int i = 0; i < 2; i++)
                    {
                        int side = i == 0 ? -1 : 1;
                        float push = Mathf.Max(0f, Mathf.Sin(time * 14f + i * Mathf.PI));
                        Vector3 p = wall + right * (side * 0.12f);
                        p.y = root.y + 0.3f + 0.35f * push;
                        SetIK(i, p - dir * 0.06f * (1f - push), dir + Vector3.down * 0.2f, 1f);
                        SetEnd(i, Quaternion.LookRotation(Vector3.up, -dir)); // pianta del piede contro il muro
                    }
                    break;
                }

                case TraversalPhase.PullUp:
                {
                    Rot(BodyPart.Abdomen, 15f, 0f, 0f);
                    Rot(BodyPart.Chest, 25f, 0f, 0f);
                    Rot(BodyPart.Head, -10f, 0f, 0f);
                    Vector3 pl = wall - right * 0.12f; pl.y = root.y + 0.55f;
                    Vector3 pr = wall + right * 0.12f; pr.y = root.y + 0.2f;
                    SetIK(0, pl, dir + Vector3.down * 0.2f, 1f);
                    SetEnd(0, Quaternion.LookRotation(Vector3.up, -dir));
                    SetIK(1, pr, dir, 0.5f);
                    Rot(BodyPart.ThighR, -20f, 0f, 0f); Rot(BodyPart.ShinR, 40f, 0f, 0f);
                    break;
                }

                case TraversalPhase.Mantle:
                {
                    // una gamba sale sul bordo, poi l'altra; il corpo si piega sopra il muro
                    float crouchM = Mathf.Sin(Mathf.PI * u);
                    Rot(BodyPart.Abdomen, Mathf.Lerp(40f, 10f, u), 0f, 0f);
                    Rot(BodyPart.Chest, Mathf.Lerp(20f, 5f, u), 0f, 0f);
                    Rot(BodyPart.Head, -10f, 0f, 0f);
                    desiredHipsLocal = hipsRest + Vector3.down * (0.32f * crouchM);

                    Vector3 onEdgeL = edge + dir * 0.12f - right * 0.1f + Vector3.up * ankleH;
                    Vector3 onEdgeR = edge + dir * 0.12f + right * 0.1f + Vector3.up * ankleH;
                    Vector3 faceL = wall - right * 0.12f; faceL.y = Mathf.Min(root.y + 0.6f, t.topY);
                    Vector3 faceR = wall + right * 0.12f; faceR.y = Mathf.Min(root.y + 0.25f, t.topY);
                    SetIK(0, Vector3.Lerp(faceL, onEdgeL, Ramp(u, 0f, 0.35f)) + Vector3.up * 0.15f * Mathf.Sin(Mathf.PI * Ramp(u, 0f, 0.35f)),
                          dir, 1f);
                    SetIK(1, Vector3.Lerp(faceR, onEdgeR, Ramp(u, 0.45f, 0.85f)) + Vector3.up * 0.2f * Mathf.Sin(Mathf.PI * Ramp(u, 0.45f, 0.85f)),
                          dir, 1f);
                    break;
                }
            }
        }

        /// <summary>
        /// Scavalco a passo del muretto: piede sinistro sopra il bordo, la destra scavalca e scende dall'altra parte,
        /// poi scende anche la sinistra. La mano destra si appoggia per un attimo sul muretto.
        /// </summary>
        void StepOverPose(TraversalState t, float u)
        {
            Vector3 dir = t.dir, right = t.right;
            Vector3 edge = t.Edge;
            Vector3 nearBase = t.nearFace - dir * 0.35f;
            Vector3 nearL = nearBase - right * 0.1f, nearR = nearBase + right * 0.1f;
            Vector3 farL = t.end - right * 0.1f, farR = t.end + right * 0.1f;
            Vector3 topL = edge + dir * 0.1f - right * 0.08f;

            // piede sinistro: sale sul bordo, resta, poi scende
            Vector3 pl;
            if (u < 0.3f) pl = Arc(nearL, topL, Smooth01(u / 0.3f), 0.15f);
            else if (u < 0.78f) pl = topL;
            else pl = Arc(topL, farL, Smooth01((u - 0.78f) / 0.22f), 0.08f);

            // piede destro: fermo, poi scavalca alto oltre il muretto
            Vector3 pr;
            if (u < 0.45f) pr = nearR;
            else if (u < 0.82f)
            {
                float v = Smooth01((u - 0.45f) / 0.37f);
                pr = Vector3.Lerp(nearR, farR, v);
                float clear = t.topY + 0.18f - pr.y;
                pr.y += Mathf.Max(0f, clear) * Mathf.Sin(Mathf.PI * v) + 0.05f * Mathf.Sin(Mathf.PI * v);
            }
            else pr = farR;

            SetIK(0, pl + Vector3.up * ankleH, dir + right * -0.2f, 1f);
            SetIK(1, pr + Vector3.up * ankleH, dir + right * 0.3f, 1f);
            SetEnd(0, Quaternion.LookRotation(dir, Vector3.up));
            SetEnd(1, Quaternion.LookRotation(dir, Vector3.up));

            // il bacino si abbassa mentre la gamba scavalca
            float bump = Smooth01((u - 0.3f) / 0.2f) * (1f - Smooth01((u - 0.7f) / 0.25f));
            desiredHipsLocal = hipsRest + Vector3.down * (0.14f * bump);

            float handW = Ramp(u, 0.15f, 0.3f) * (1f - Ramp(u, 0.6f, 0.75f));
            Vector3 hand = edge + dir * 0.05f + right * 0.22f + Vector3.up * 0.03f;
            SetIK(3, hand, -dir + right, handW);
            SetEnd(3, PalmDown(1, dir));

            Rot(BodyPart.Abdomen, 10f + 8f * bump, 0f, 0f);
            Rot(BodyPart.Chest, 6f, 0f, 0f);
            Rot(BodyPart.Head, -8f, 0f, 0f);
            Rot(BodyPart.UpperArmL, -20f, 0f, -25f);
            Rot(BodyPart.ForearmL, -25f, 0f, 0f);
            Rot(BodyPart.UpperArmR, -30f, 0f, 10f);
        }

        /// <summary>Balzo sul muretto (di corsa): mano destra sul bordo, gambe raccolte che passano sopra.</summary>
        void LowVaultPose(TraversalState t, float u)
        {
            Vector3 dir = t.dir, right = t.right;
            Vector3 edge = t.Edge;
            Rot(BodyPart.Abdomen, 18f, 0f, 0f);
            Rot(BodyPart.Chest, 10f, 0f, 0f);
            Rot(BodyPart.Head, -15f, 0f, 0f);
            Rot(BodyPart.UpperArmL, -60f, 0f, -35f);
            Rot(BodyPart.ForearmL, -20f, 0f, 0f);
            Rot(BodyPart.ThighL, -85f, 0f, 0f); Rot(BodyPart.ShinL, 105f, 0f, 0f);
            Rot(BodyPart.ThighR, -80f, 0f, 0f); Rot(BodyPart.ShinR, 100f, 0f, 0f);

            Vector3 hand = edge + dir * 0.08f + right * 0.14f;
            float handW = Ramp(u, 0f, 0.15f) * (1f - Ramp(u, 0.55f, 0.75f));
            SetIK(3, hand + Vector3.up * 0.03f - dir * 0.04f, -dir + right, handW);
            SetEnd(3, PalmDown(1, dir));

            Vector3 hipsW = hips.position;
            float legW = Ramp(u, 0.1f, 0.3f) * (1f - Ramp(u, 0.8f, 1f));
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = hipsW + Vector3.down * 0.42f + dir * 0.12f + right * (feet[i].side * 0.1f);
                p.y = Mathf.Max(p.y, t.topY + 0.06f + ankleH);
                SetIK(i, p, dir, legW);
            }
        }

        /// <summary>Salto in lungo: stacco con una gamba avanti, braccia in alto, poi gambe avanti per l'atterraggio.</summary>
        void LongJumpPose(float u)
        {
            float land = Smooth01((u - 0.45f) / 0.45f);
            Rot(BodyPart.ThighL, Mathf.Lerp(-75f, -65f, land), 0f, 0f);
            Rot(BodyPart.ShinL, Mathf.Lerp(70f, 45f, land), 0f, 0f);
            Rot(BodyPart.ThighR, Mathf.Lerp(25f, -60f, land), 0f, 0f);
            Rot(BodyPart.ShinR, Mathf.Lerp(80f, 50f, land), 0f, 0f);
            Rot(BodyPart.UpperArmL, Mathf.Lerp(-140f, -60f, land), 0f, -20f);
            Rot(BodyPart.UpperArmR, Mathf.Lerp(-140f, -60f, land), 0f, 20f);
            Rot(BodyPart.ForearmL, -20f, 0f, 0f);
            Rot(BodyPart.ForearmR, -20f, 0f, 0f);
            Rot(BodyPart.Abdomen, Mathf.Lerp(-5f, 15f, land), 0f, 0f);
            Rot(BodyPart.Head, Mathf.Lerp(-10f, 10f, land), 0f, 0f);
        }

        static Vector3 Arc(Vector3 a, Vector3 b, float t, float height)
        {
            Vector3 p = Vector3.Lerp(a, b, t);
            p.y += height * Mathf.Sin(Mathf.PI * t);
            return p;
        }

        void TakeOffPose()
        {
            Rot(BodyPart.UpperArmL, -120f, 0f, -30f);
            Rot(BodyPart.UpperArmR, -120f, 0f, 30f);
            Rot(BodyPart.ForearmL, -15f, 0f, 0f); Rot(BodyPart.ForearmR, -15f, 0f, 0f);
            Rot(BodyPart.ThighL, -40f, 0f, 0f); Rot(BodyPart.ShinL, 60f, 0f, 0f);
            Rot(BodyPart.ThighR, -10f, 0f, 0f); Rot(BodyPart.ShinR, 25f, 0f, 0f);
            Rot(BodyPart.Abdomen, -5f, 0f, 0f);
            Rot(BodyPart.Head, -10f, 0f, 0f);
        }

        void FallPose()
        {
            float flail = Mathf.Sin(Time.time * 9f);
            Rot(BodyPart.UpperArmL, -100f + 10f * flail, 0f, -60f);
            Rot(BodyPart.UpperArmR, -100f - 10f * flail, 0f, 60f);
            Rot(BodyPart.ForearmL, -20f, 0f, 0f); Rot(BodyPart.ForearmR, -20f, 0f, 0f);
            Rot(BodyPart.ThighL, -35f, 0f, 0f); Rot(BodyPart.ShinL, 55f, 0f, 0f);
            Rot(BodyPart.ThighR, -20f, 0f, 0f); Rot(BodyPart.ShinR, 40f, 0f, 0f);
            Rot(BodyPart.FootL, -15f, 0f, 0f); Rot(BodyPart.FootR, -15f, 0f, 0f);
            Rot(BodyPart.Abdomen, 5f, 0f, 0f);
            Rot(BodyPart.Head, 15f, 0f, 0f); // guarda dove atterra
        }

        // ------------------------------------------------------------------ IK

        void SolveIK(float dt)
        {
            bool fast = motor != null && motor.IsTraversing;
            float k = 1f - Mathf.Exp(-(fast ? 40f : 20f) * dt);
            for (int i = 0; i < 4; i++)
            {
                ikWeight[i] = extActive[i] ? ikTargetWeight[i] : Mathf.Lerp(ikWeight[i], ikTargetWeight[i], k);
                float w = ikWeight[i];
                if (w <= 0.001f) continue;
                switch (i)
                {
                    case 0: TwoBoneIK.Solve(thighL, shinL, footL, ikTarget[i], ikPole[i], w, false); break;
                    case 1: TwoBoneIK.Solve(thighR, shinR, footR, ikTarget[i], ikPole[i], w, false); break;
                    case 2: TwoBoneIK.Solve(armL, foreL, handL, ikTarget[i], ikPole[i], w, true); break;
                    case 3: TwoBoneIK.Solve(armR, foreR, handR, ikTarget[i], ikPole[i], w, true); break;
                }
                if (hasEndRotation[i])
                {
                    Transform end = i == 0 ? footL : i == 1 ? footR : i == 2 ? handL : handR;
                    end.rotation = Quaternion.Slerp(end.rotation, endRotation[i], w);
                }
            }
        }

        void SetIK(int limb, Vector3 targetPos, Vector3 pole, float weight)
        {
            ikTarget[limb] = targetPos;
            ikPole[limb] = pole;
            ikTargetWeight[limb] = Mathf.Clamp01(weight);
        }

        void SetEnd(int limb, Quaternion rotation)
        {
            endRotation[limb] = rotation;
            hasEndRotation[limb] = true;
        }

        /// <summary>Mano appoggiata: palmo verso il basso, dita in avanti.</summary>
        public static Quaternion PalmDown(int side, Vector3 dir)
        {
            Vector3 y = -dir;                                   // le dita puntano lungo il -Y locale
            Vector3 x = side < 0 ? Vector3.down : Vector3.up;   // il palmo è sul lato interno della mano
            Vector3 z = Vector3.Cross(x, y);
            return Quaternion.LookRotation(z, y);
        }

        // ------------------------------------------------------------------ utilità

        void Rot(BodyPart p, float x, float y, float z) => target[(int)p] = new Vector3(x, y, z);
        void Add(BodyPart p, float x, float y, float z) => target[(int)p] += new Vector3(x, y, z);

        static float Ramp(float u, float a, float b) => Mathf.Clamp01((u - a) / (b - a));
        static float Smooth01(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }
    }
}
