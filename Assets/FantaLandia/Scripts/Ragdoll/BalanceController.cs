using UnityEngine;

namespace FantaLandia.Ragdoll
{
    /// <summary>
    /// Controllo dell'equilibrio basato su coppie muscolari interne, più un aiuto all'equilibrio
    /// opzionale (balanceAssist) che si satura oltre una certa inclinazione: spinte forti lo fanno
    /// comunque barcollare, fare passi o cadere.
    ///
    /// Strategie, come nell'equilibrio umano:
    ///  1. Caviglie: confronta il capture point (dove il centro di massa "andrà a fermarsi")
    ///     con la base d'appoggio dei piedi e inclina le caviglie per riportarlo dentro.
    ///  2. Anche: tengono il bacino dritto rispetto al mondo (coppia virtuale sul busto via anca d'appoggio).
    ///  3. Passo di recupero: se il capture point esce dalla base d'appoggio, una gamba fa un passo
    ///     verso di esso (IK analitica a due segmenti → target dei muscoli di anca, ginocchio e caviglia).
    ///  4. Riflessi: braccia che si allargano quando perde l'equilibrio, testa stabilizzata.
    ///  5. Caduta: se il bacino scende troppo o il busto si inclina oltre il limite i muscoli cedono;
    ///     dopo qualche secondo si rialza (questa è l'unica fase con una forza esterna di aiuto).
    /// </summary>
    [RequireComponent(typeof(ActiveRagdoll))]
    public partial class BalanceController : MonoBehaviour
    {
        public enum State { Calibrating, Standing, Stepping, Fallen, GettingUp }

        static readonly int[] Sides = { -1, 1 };

        [Header("Muscoli")]
        [Range(0f, 2f)] public float muscleStrength = 1f;
        [Range(0f, 1f)] public float limpStrength = 0.12f;

        [Header("Equilibrio con le caviglie")]
        [Tooltip("Gradi di correzione per metro di errore del capture point (avanti/indietro).")]
        public float anklePitchGain = 60f;
        [Tooltip("Gradi di correzione per metro di errore del capture point (laterale).")]
        public float ankleRollGain = 50f;
        public float maxAnklePitch = 25f;
        public float maxAnkleRoll = 15f;

        [Header("Busto e postura")]
        [Tooltip("0 = le gambe restano allineate al bacino, 1 = le anche pensano solo a raddrizzare il bacino.")]
        [Range(0f, 1f)] public float torsoUprightWeight = 0.4f;
        [Tooltip("Inclinazione in avanti del bacino (gradi).")]
        public float torsoLean = 2f;
        [Range(0f, 20f)] public float standKneeBend = 4f;
        [Range(0f, 1f)] public float headStabilization = 0.6f;

        [Header("Braccia")]
        public float armRestAbduction = 8f;
        [Tooltip("Quanto si allargano le braccia quando perde l'equilibrio (gradi).")]
        public float armReactionAngle = 70f;

        [Header("Passo di recupero")]
        public bool enableStepping = true;
        [Tooltip("Raggio della pianta del piede usato per la base d'appoggio (m).")]
        public float footSupportRadius = 0.09f;
        public float stepTriggerMargin = 0.04f;
        public float stepDuration = 0.3f;
        public float stepHeight = 0.10f;
        public float maxStepLength = 0.55f;
        public float minStepWidth = 0.14f;
        public float stepOvershoot = 0.05f;
        public float stepCooldown = 0.06f;

        [Header("Caduta e rialzo")]
        public float fallPelvisHeight = 0.55f;
        public float fallTiltAngle = 60f;
        public float stayDownTime = 2.5f;
        public bool autoGetUp = true;

        [Header("Aiuto all'equilibrio (NON fisico)")]
        [Tooltip("0 = completamente fisico. Coppia esterna su bacino e torace che li tiene dritti, " +
                 "come fanno quasi tutti i giochi con active ragdoll (i joint di Unity da soli non reggono bene il peso).")]
        [Range(0f, 1f)] public float balanceAssist = 0.8f;
        [Tooltip("Rigidità dell'aiuto (1/s²). Sopra ~10 regge il corpo anche senza caviglie.")]
        public float assistStiffness = 25f;
        public float assistDamping = 8f;
        [Tooltip("Oltre questa inclinazione l'aiuto non cresce più: le spinte forti lo fanno cadere.")]
        public float assistMaxTilt = 20f;

        [Header("Debug")]
        public bool drawGizmos = true;

        public State CurrentState => state;
        public ActiveRagdoll Ragdoll => rd;
        public float CurrentStrength => strength;
        public Vector3 CenterOfMass { get; private set; }
        public Vector3 CenterOfMassVelocity { get; private set; }
        public Vector3 CapturePoint { get; private set; }
        public Vector3 SupportCenter { get; private set; }
        /// <summary>Distanza del capture point dalla base d'appoggio (&gt; 0 = fuori).</summary>
        public float SupportDistance { get; private set; }

        ActiveRagdoll rd;
        State state = State.Standing;
        float stateTime, strength, fallTimer, assistFade, groundY;
        float lastStepEndTime = -10f;
        bool groundL, groundR;
        Quaternion heading = Quaternion.identity;

        int swingSide; // -1 sinistra, +1 destra
        Vector3 stepStart, stepTarget; // posizioni della caviglia, appiattite (y = 0)

        void Awake()
        {
            rd = GetComponent<ActiveRagdoll>();
            strength = muscleStrength;
            groundY = transform.position.y;
            heading = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            BeginCalibrationOrStand();
        }

        /// <summary>Fa svenire il manichino (si rialzerà da solo se autoGetUp è attivo).</summary>
        public void Knockout() => EnterState(State.Fallen);

        void FixedUpdate()
        {
            if (rd == null || rd.Parts.Count == 0) return;
            float dt = Time.fixedDeltaTime;
            stateTime += dt;

            Sense();

            float targetStrength = muscleStrength;
            switch (state)
            {
                case State.Standing: UpdateStanding(); break;
                case State.Stepping: UpdateStepping(); break;
                case State.Fallen: UpdateFallen(); targetStrength = limpStrength; break;
                case State.Calibrating: UpdateCalibration(); break;
                case State.GettingUp: UpdateGetUp(dt); targetStrength = muscleStrength * getUpStrengthBoost; break;
            }

            if (state == State.Standing || state == State.Stepping)
            {
                CheckFall(dt);
                if (assistFade > 0f)
                {
                    ApplyStandAssist(assistFade);
                    assistFade = Mathf.Max(0f, assistFade - dt / 0.8f);
                }
                ApplyBalanceAssist();
            }

            // I muscoli cedono in fretta e recuperano più lentamente.
            float rate = targetStrength < strength ? 6f : 2f;
            strength = Mathf.MoveTowards(strength, targetStrength, rate * dt);
            foreach (var part in rd.Parts) part.ApplyDrive(strength);
        }

        // ------------------------------------------------------------------ percezione

        void Sense()
        {
            CenterOfMass = rd.GetCenterOfMass(out var velocity);
            CenterOfMassVelocity = velocity;

            groundL = rd.FootL.IsGrounded;
            groundR = rd.FootR.IsGrounded;
            if (groundL || groundR)
            {
                float y = float.MaxValue;
                if (groundL) y = Mathf.Min(y, rd.FootL.Body.worldCenterOfMass.y - ActiveRagdoll.FootHalfHeight);
                if (groundR) y = Mathf.Min(y, rd.FootR.Body.worldCenterOfMass.y - ActiveRagdoll.FootHalfHeight);
                groundY = y;
            }

            if (state != State.GettingUp && state != State.Calibrating) heading = ComputeHeading();

            // Capture point del pendolo inverso lineare: x + v / ω, con ω = sqrt(g / h).
            float g = Mathf.Max(0.1f, -Physics.gravity.y);
            float h = Mathf.Max(0.3f, CenterOfMass.y - groundY);
            CapturePoint = Flat(CenterOfMass) + Flat(velocity) * Mathf.Sqrt(h / g);

            Vector3 a = FootGround(rd.FootL), b = FootGround(rd.FootR);
            if (groundL && groundR)
            {
                SupportCenter = (a + b) * 0.5f;
                SupportDistance = DistanceToSegment(CapturePoint, a, b) - footSupportRadius;
            }
            else if (groundL || groundR)
            {
                SupportCenter = groundL ? a : b;
                SupportDistance = Vector3.Distance(CapturePoint, SupportCenter) - footSupportRadius;
            }
            else
            {
                SupportCenter = Flat(CenterOfMass);
                SupportDistance = 0f;
            }
        }

        Quaternion ComputeHeading()
        {
            Transform pelvis = rd.Pelvis.transform;
            Vector3 f = Vector3.ProjectOnPlane(pelvis.forward, Vector3.up);
            if (f.sqrMagnitude < 0.04f)
            {
                // Sdraiato: si rialzerà guardando verso i piedi (sulla schiena) o verso la testa (a pancia in giù).
                Vector3 alongBody = Vector3.ProjectOnPlane(pelvis.up, Vector3.up);
                f = pelvis.forward.y > 0f ? -alongBody : alongBody;
            }
            if (f.sqrMagnitude < 1e-4f) return heading;
            return Quaternion.LookRotation(f.normalized, Vector3.up);
        }

        void CheckFall(float dt)
        {
            float pelvisHeight = rd.Pelvis.transform.position.y - groundY;
            float tilt = Vector3.Angle(rd.Chest.transform.up, Vector3.up);
            bool falling = pelvisHeight < fallPelvisHeight || tilt > fallTiltAngle;
            fallTimer = falling ? fallTimer + dt : 0f;
            if (fallTimer > 0.12f) EnterState(State.Fallen);
        }

        // ------------------------------------------------------------------ stati

        void EnterState(State next)
        {
            state = next;
            stateTime = 0f;
            fallTimer = 0f;
            if (next == State.Fallen) assistFade = 0f;
            if (next == State.GettingUp) BeginGetUp();
        }

        void UpdateStanding()
        {
            PoseUpperBody();
            LegControl(-1, groundL, groundR);
            LegControl(+1, groundR, groundL);

            if (enableStepping && (groundL || groundR) &&
                SupportDistance > stepTriggerMargin &&
                Time.fixedTime - lastStepEndTime > stepCooldown)
            {
                BeginStep(ChooseSwingSide());
            }
        }

        void LegControl(int side, bool grounded, bool otherGrounded)
        {
            var (thigh, shin, foot) = Leg(side);
            if (grounded || !otherGrounded)
            {
                StanceLeg(thigh, shin, foot);
            }
            else
            {
                // Piede sollevato mentre l'altro appoggia: lo riporta a terra sotto l'anca.
                Vector3 target = Flat(rd.JointPosition(thigh)) + heading * Vector3.right * (side * 0.02f);
                target.y = groundY + ActiveRagdoll.AnkleHeight - 0.02f;
                SwingLegIK(thigh, shin, foot, target, 0f);
            }
        }

        int ChooseSwingSide()
        {
            if (groundL && !groundR) return +1;
            if (groundR && !groundL) return -1;

            Vector3 e = CapturePoint - SupportCenter;
            float lateral = Vector3.Dot(e, heading * Vector3.right);
            if (Mathf.Abs(lateral) > 0.6f * e.magnitude) return lateral > 0f ? +1 : -1;

            // Avanti/indietro: muove il piede più lontano dal capture point.
            float dl = (CapturePoint - FootGround(rd.FootL)).sqrMagnitude;
            float dr = (CapturePoint - FootGround(rd.FootR)).sqrMagnitude;
            return dl > dr ? -1 : +1;
        }

        void BeginStep(int side)
        {
            swingSide = side;
            var foot = Leg(side).foot;
            stepStart = Flat(rd.JointPosition(foot));
            stepTarget = ComputeStepTarget(side);
            EnterState(State.Stepping);
        }

        Vector3 ComputeStepTarget(int side)
        {
            var stanceFoot = side < 0 ? rd.FootR : rd.FootL;
            Vector3 stance = FootGround(stanceFoot);
            Vector3 fwd = heading * Vector3.forward;
            Vector3 right = heading * Vector3.right;

            Vector3 e = CapturePoint - SupportCenter;
            Vector3 dir = e.sqrMagnitude > 1e-6f ? e.normalized : fwd;
            Vector3 target = CapturePoint + dir * stepOvershoot;

            // Mai incrociare le gambe: mantiene una larghezza minima dal piede d'appoggio.
            float lateral = Vector3.Dot(target - stance, right) * side;
            if (lateral < minStepWidth) target += right * (side * (minStepWidth - lateral));

            Vector3 offset = target - stance;
            if (offset.magnitude > maxStepLength) target = stance + offset.normalized * maxStepLength;

            // Da centro del piede a caviglia.
            return target - fwd * ActiveRagdoll.FootForwardOffset;
        }

        void UpdateStepping()
        {
            PoseUpperBody();
            var (stanceThigh, stanceShin, stanceFoot) = Leg(-swingSide);
            var (swingThigh, swingShin, swingFoot) = Leg(swingSide);

            float p = Mathf.Clamp01(stateTime / stepDuration);
            if (p < 0.7f) stepTarget = ComputeStepTarget(swingSide); // ripianifica finché può

            Vector3 ankle = Vector3.Lerp(stepStart, stepTarget, Mathf.SmoothStep(0f, 1f, p));
            ankle.y = groundY + ActiveRagdoll.AnkleHeight + Mathf.Sin(p * Mathf.PI) * stepHeight - (p >= 1f ? 0.02f : 0f);

            SwingLegIK(swingThigh, swingShin, swingFoot, ankle, p < 0.5f ? -8f : 0f);
            StanceLeg(stanceThigh, stanceShin, stanceFoot);

            bool swingGrounded = swingSide < 0 ? groundL : groundR;
            if ((swingGrounded && p > 0.6f) || stateTime > stepDuration * 2f)
            {
                lastStepEndTime = Time.fixedTime;
                EnterState(State.Standing);
            }
        }

        void UpdateFallen()
        {
            // Posa protettiva: braccia in avanti, gambe un po' raccolte.
            rd.Belly.TargetLocal = Quaternion.identity;
            rd.Chest.TargetLocal = Quaternion.identity;
            rd.Head.TargetLocal = Quaternion.AngleAxis(15f, Vector3.right);
            foreach (int side in Sides)
            {
                var (thigh, shin, foot) = Leg(side);
                thigh.TargetLocal = Quaternion.AngleAxis(-20f, Vector3.right);
                shin.TargetLocal = Quaternion.AngleAxis(25f, Vector3.right);
                foot.TargetLocal = Quaternion.identity;
            }
            ArmPose(rd.UpperArmL, rd.ForearmL, -1, 0f, -50f);
            ArmPose(rd.UpperArmR, rd.ForearmR, +1, 0f, -50f);

            if (autoGetUp && stateTime > stayDownTime && CenterOfMassVelocity.magnitude < 0.5f)
                EnterState(State.GettingUp);
        }

        // ------------------------------------------------------------------ pose e muscoli

        void StanceLeg(RagdollPart thigh, RagdollPart shin, RagdollPart foot)
        {
            // Anca: coppia virtuale sul busto. Il target è "la coscia dov'è ora, con il bacino dritto".
            Quaternion desiredPelvis = heading * Quaternion.AngleAxis(torsoLean, Vector3.right);
            Quaternion hipForUpright = Quaternion.Inverse(desiredPelvis) * thigh.transform.rotation;
            thigh.TargetLocal = Quaternion.Slerp(Quaternion.identity, hipForUpright, torsoUprightWeight);

            shin.TargetLocal = Quaternion.AngleAxis(standKneeBend, Vector3.right);

            // Caviglia: riporta il capture point verso il centro della base d'appoggio.
            Vector3 e = CapturePoint - SupportCenter;
            float forwardError = Vector3.Dot(e, heading * Vector3.forward);
            float lateralError = Vector3.Dot(e, heading * Vector3.right);
            float pitch = Mathf.Clamp(anklePitchGain * forwardError, -maxAnklePitch, maxAnklePitch);
            float roll = Mathf.Clamp(-ankleRollGain * lateralError, -maxAnkleRoll, maxAnkleRoll);
            foot.TargetLocal = Quaternion.AngleAxis(pitch, Vector3.right) * Quaternion.AngleAxis(roll, Vector3.forward);
        }

        /// <summary>IK analitica a due segmenti: porta la caviglia in ankleTarget (mondo).</summary>
        void SwingLegIK(RagdollPart thigh, RagdollPart shin, RagdollPart foot, Vector3 ankleTarget, float toePitch)
        {
            float l1 = rd.ThighLength, l2 = rd.ShinLength;
            Vector3 hip = rd.JointPosition(thigh);
            Vector3 d = ankleTarget - hip;
            float len = Mathf.Clamp(d.magnitude, 0.15f, l1 + l2 - 0.005f);
            Vector3 dDir = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.down;

            float hipAngle = Mathf.Acos(Mathf.Clamp((l1 * l1 + len * len - l2 * l2) / (2f * l1 * len), -1f, 1f)) * Mathf.Rad2Deg;
            float kneeInner = Mathf.Acos(Mathf.Clamp((l1 * l1 + l2 * l2 - len * len) / (2f * l1 * l2), -1f, 1f)) * Mathf.Rad2Deg;
            float kneeBend = 180f - kneeInner;

            // La coscia ruota "in avanti" rispetto alla linea anca-caviglia, così il ginocchio punta avanti.
            Vector3 fwd = heading * Vector3.forward;
            Vector3 axis = Vector3.Cross(dDir, fwd);
            if (axis.sqrMagnitude < 1e-4f) axis = -(heading * Vector3.right);
            Vector3 thighDir = Quaternion.AngleAxis(hipAngle, axis.normalized) * dDir;

            Quaternion thighWorld = Quaternion.FromToRotation(Vector3.down, thighDir) * heading;
            thigh.TargetLocal = Quaternion.Inverse(rd.Pelvis.transform.rotation) * thighWorld;
            shin.TargetLocal = Quaternion.AngleAxis(kneeBend, Vector3.right);
            foot.TargetLocal = Quaternion.Inverse(shin.transform.rotation) * heading * Quaternion.AngleAxis(toePitch, Vector3.right);
        }

        void PoseUpperBody()
        {
            rd.Belly.TargetLocal = Quaternion.identity;
            rd.Chest.TargetLocal = Quaternion.identity;

            // Riflesso: la testa cerca di restare dritta rispetto al mondo.
            Quaternion headUpright = Quaternion.Inverse(rd.Chest.transform.rotation) * heading;
            rd.Head.TargetLocal = Quaternion.Slerp(Quaternion.identity, headUpright, headStabilization);

            // Riflesso: braccia larghe in proporzione allo squilibrio.
            float imbalance = Mathf.Clamp01((CapturePoint - SupportCenter).magnitude / 0.25f);
            float extra = imbalance * armReactionAngle;
            ArmPose(rd.UpperArmL, rd.ForearmL, -1, extra, -8f);
            ArmPose(rd.UpperArmR, rd.ForearmR, +1, extra, -8f);
        }

        void ArmPose(RagdollPart upper, RagdollPart fore, int side, float extraAbduction, float flex)
        {
            upper.TargetLocal = Quaternion.AngleAxis(side * (armRestAbduction + extraAbduction), Vector3.forward) *
                                Quaternion.AngleAxis(flex, Vector3.right);
            fore.TargetLocal = Quaternion.AngleAxis(-15f - extraAbduction * 0.3f, Vector3.right);
        }

        // ------------------------------------------------------------------ aiuti esterni (non fisici)

        void ApplyBalanceAssist()
        {
            if (balanceAssist <= 0f) return;
            float massShare = 0.5f * rd.TotalMass * balanceAssist;
            float maxAngle = assistMaxTilt * Mathf.Deg2Rad;
            Vector3 pelvisUp = heading * Quaternion.AngleAxis(torsoLean, Vector3.right) * Vector3.up;
            TiltTorque(rd.Pelvis.Body, pelvisUp, massShare, maxAngle);
            TiltTorque(rd.Chest.Body, Vector3.up, massShare, maxAngle);
        }

        /// <summary>
        /// Coppia che porta l'asse "su" del corpo verso targetUp, senza toccare la rotazione attorno alla
        /// verticale. Scalata sulla massa del corpo (braccio di leva ~1 m), quindi indipendente dal peso.
        /// </summary>
        void TiltTorque(Rigidbody rb, Vector3 targetUp, float massShare, float maxAngle)
        {
            Vector3 up = rb.transform.up;
            Vector3 axis = Vector3.Cross(up, targetUp);
            float sin = axis.magnitude;
            float angle = Mathf.Atan2(sin, Vector3.Dot(up, targetUp));
            Vector3 dir = sin > 1e-5f ? axis / sin : Vector3.zero;

            Vector3 spin = rb.angularVelocity;
            spin -= Vector3.Project(spin, Vector3.up);

            Vector3 torque = (dir * (Mathf.Min(angle, maxAngle) * assistStiffness) - spin * assistDamping) * massShare;
            rb.AddTorque(torque);
        }

        /// <summary>Aiuto che si spegne gradualmente appena finito il rialzo.</summary>
        void ApplyStandAssist(float weight)
        {
            if (weight <= 0f) return;
            Vector3 feet = (FootGround(rd.FootL) + FootGround(rd.FootR)) * 0.5f;
            Vector3 pelvisTarget = feet + Vector3.up * (groundY + ActiveRagdoll.PelvisHeight);
            Vector3 chestTarget = pelvisTarget + Vector3.up * ChestAbovePelvis;
            Lift(rd.Pelvis.Body, pelvisTarget, 0.5f * weight, getUpPositionStiffness, getUpPositionDamping, getUpGravityCompensation);
            Lift(rd.Chest.Body, chestTarget, 0.5f * weight, getUpPositionStiffness, getUpPositionDamping, getUpGravityCompensation);
            AlignTorque(rd.Pelvis.Body, heading, getUpRotationStiffness * weight, getUpRotationDamping * weight);
            AlignTorque(rd.Chest.Body, heading, getUpRotationStiffness * weight, getUpRotationDamping * weight);
        }

        /// <summary>Forza PD verso target, con compensazione (parziale) della gravità di tutto il corpo.</summary>
        void Lift(Rigidbody rb, Vector3 target, float share, float stiffness, float damping, float gravityCompensation)
        {
            Vector3 acc = (target - rb.worldCenterOfMass) * stiffness - rb.linearVelocity * damping
                          - Physics.gravity * gravityCompensation;
            rb.AddForce(acc * (rd.TotalMass * share));
        }

        static void AlignTorque(Rigidbody rb, Quaternion target, float stiffness, float damping)
        {
            Quaternion delta = target * Quaternion.Inverse(rb.rotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (float.IsNaN(axis.x) || float.IsInfinity(axis.x) || Mathf.Abs(angle) < 0.01f)
            {
                axis = Vector3.zero;
                angle = 0f;
            }
            Vector3 acc = axis * (angle * Mathf.Deg2Rad * stiffness) - rb.angularVelocity * damping;
            rb.AddTorque(acc, ForceMode.Acceleration);
        }

        // ------------------------------------------------------------------ utilità

        (RagdollPart thigh, RagdollPart shin, RagdollPart foot) Leg(int side) =>
            side < 0 ? (rd.ThighL, rd.ShinL, rd.FootL) : (rd.ThighR, rd.ShinR, rd.FootR);

        static Vector3 FootGround(RagdollPart foot) => Flat(foot.Body.worldCenterOfMass);

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        void OnDrawGizmos()
        {
            if (!drawGizmos || !Application.isPlaying || rd == null || rd.Parts.Count == 0) return;
            Vector3 up = Vector3.up * (groundY + 0.01f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(CenterOfMass, 0.04f);
            Gizmos.DrawLine(CenterOfMass, Flat(CenterOfMass) + up);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(FootGround(rd.FootL) + up, FootGround(rd.FootR) + up);

            Gizmos.color = SupportDistance > 0f ? Color.red : Color.green;
            Gizmos.DrawSphere(CapturePoint + up, 0.03f);

            if (state == State.Stepping)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(stepTarget + up, 0.05f);
            }
        }
    }
}
