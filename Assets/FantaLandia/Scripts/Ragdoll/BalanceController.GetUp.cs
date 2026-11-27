using System.Collections.Generic;
using UnityEngine;

namespace FantaLandia.Ragdoll
{
    /// <summary>
    /// Rialzo con animazione procedurale, in fasi come un rialzo umano:
    ///   (sulla schiena) Rotola a pancia in giù → Spinta con le braccia → Carponi →
    ///   In ginocchio → Affondo (un piede avanti) → In piedi.
    ///
    /// Ogni fase è un "keyframe" generato da codice: pose dei muscoli + traiettoria di bacino e torace.
    /// Le pose sono interpolate dalla posa reale di partenza (niente scatti) e raggiunte dai muscoli.
    /// Una guida esterna (forze PD con compensazione parziale del peso) accompagna bacino e torace lungo
    /// la traiettoria: è la parte non fisica, e si spegne gradualmente dopo che si è rialzato.
    /// </summary>
    public partial class BalanceController
    {
        public enum GetUpPhase { None, Roll, PushUp, HandsAndKnees, Kneel, Lunge, Stand }

        [Header("Rialzo procedurale")]
        [Tooltip("Moltiplicatore della velocità dell'animazione di rialzo.")]
        [Range(0.3f, 3f)] public float getUpSpeed = 1f;
        [Tooltip("Forza dei muscoli durante il rialzo, rispetto a muscleStrength.")]
        [Range(0.5f, 2f)] public float getUpStrengthBoost = 1.3f;
        [Tooltip("Quanto la guida esterna compensa il peso del corpo (0 = per niente, 1 = tutto).")]
        [Range(0f, 1f)] public float getUpGravityCompensation = 0.6f;
        public float getUpPositionStiffness = 30f;
        public float getUpPositionDamping = 9f;
        public float getUpRotationStiffness = 40f;
        public float getUpRotationDamping = 8f;
        [Tooltip("Coppia per rotolare a pancia in giù (Nm).")]
        public float rollTorque = 90f;
        [Tooltip("Se il rialzo dura più di così, torna a terra e riprova.")]
        public float getUpTimeout = 9f;

        public GetUpPhase CurrentGetUpPhase => state == State.GettingUp ? getUpPhase : GetUpPhase.None;

        const float ChestAbovePelvis = 0.38f;
        const float RollBlendTime = 0.4f;
        const float MaxRollTime = 2.5f;

        struct GetUpKey
        {
            public float duration, pelvisHeight, pelvisForward, pelvisPitch, chestPitch;
            public GetUpKey(float duration, float pelvisHeight, float pelvisForward, float pelvisPitch, float chestPitch)
            {
                this.duration = duration;
                this.pelvisHeight = pelvisHeight;
                this.pelvisForward = pelvisForward;
                this.pelvisPitch = pelvisPitch;
                this.chestPitch = chestPitch;
            }
        }

        GetUpPhase getUpPhase;
        float phaseTime, getUpTotalTime;
        Vector3 getUpAnchor;
        int rollSign, leadSide;
        readonly Dictionary<RagdollPart, Quaternion> phaseStartPose = new Dictionary<RagdollPart, Quaternion>();
        Vector3 phaseStartPelvisPos;
        Quaternion phaseStartPelvisRot, phaseStartChestRot;

        // Altezze del bacino (centro) da terra, avanzamento lungo la direzione di rialzo, inclinazioni
        // in avanti di bacino e torace (90 = sdraiato a pancia in giù, 0 = dritto).
        GetUpKey Key(GetUpPhase phase)
        {
            switch (phase)
            {
                case GetUpPhase.PushUp: return new GetUpKey(0.7f, 0.22f, 0.00f, 85f, 50f);
                case GetUpPhase.HandsAndKnees: return new GetUpKey(0.8f, 0.55f, -0.05f, 80f, 80f);
                case GetUpPhase.Kneel: return new GetUpKey(0.8f, 0.58f, -0.10f, 5f, 0f);
                case GetUpPhase.Lunge: return new GetUpKey(0.7f, 0.60f, -0.05f, 5f, 5f);
                default: return new GetUpKey(0.9f, ActiveRagdoll.PelvisHeight, 0.20f, torsoLean, torsoLean);
            }
        }

        void BeginGetUp()
        {
            getUpTotalTime = 0f;
            leadSide = Random.value < 0.5f ? -1 : 1;

            bool faceDown = rd.Chest.transform.forward.y < -0.5f;
            if (faceDown)
            {
                StartPhase(GetUpPhase.PushUp);
            }
            else
            {
                // Ruotando di +θ attorno all'asse del corpo, "avanti" va verso cross(asse, avanti):
                // sceglie il verso che porta la pancia verso terra.
                Vector3 axis = rd.Pelvis.transform.up;
                Vector3 front = rd.Chest.transform.forward;
                rollSign = Vector3.Dot(Vector3.Cross(axis, front), Vector3.down) >= 0f ? 1 : -1;
                StartPhase(GetUpPhase.Roll);
            }
        }

        void StartPhase(GetUpPhase phase)
        {
            getUpPhase = phase;
            phaseTime = 0f;

            foreach (var part in rd.Parts)
                phaseStartPose[part] = ActiveRagdoll.LocalRotation(part);
            phaseStartPelvisPos = rd.Pelvis.Body.worldCenterOfMass;
            phaseStartPelvisRot = rd.Pelvis.transform.rotation;
            phaseStartChestRot = rd.Chest.transform.rotation;

            if (phase == GetUpPhase.PushUp)
            {
                // A pancia in giù la testa indica la direzione in cui si rialzerà.
                Vector3 h = Vector3.ProjectOnPlane(rd.Pelvis.transform.up, Vector3.up);
                if (h.sqrMagnitude > 1e-3f) heading = Quaternion.LookRotation(h.normalized, Vector3.up);
                getUpAnchor = Flat(rd.Pelvis.Body.worldCenterOfMass);
            }
        }

        void UpdateGetUp(float dt)
        {
            getUpTotalTime += dt;
            phaseTime += dt * getUpSpeed;
            if (getUpTotalTime > getUpTimeout)
            {
                EnterState(State.Fallen);
                return;
            }

            if (getUpPhase == GetUpPhase.Roll)
            {
                UpdateRoll();
                return;
            }

            GetUpKey key = Key(getUpPhase);
            float s = Mathf.SmoothStep(0f, 1f, phaseTime / key.duration);

            // Posa procedurale dei muscoli.
            foreach (var part in rd.Parts)
            {
                if (part.Joint == null) continue;
                part.TargetLocal = Quaternion.Slerp(phaseStartPose[part], PoseFor(getUpPhase, part), s);
            }

            // Traiettoria guidata di bacino e torace.
            Vector3 pelvisGoal = getUpAnchor + heading * Vector3.forward * key.pelvisForward +
                                 Vector3.up * (groundY + key.pelvisHeight);
            Quaternion pelvisGoalRot = heading * Quaternion.AngleAxis(key.pelvisPitch, Vector3.right);
            Quaternion chestGoalRot = heading * Quaternion.AngleAxis(key.chestPitch, Vector3.right);

            Vector3 pelvisTarget = Vector3.Lerp(phaseStartPelvisPos, pelvisGoal, s);
            Quaternion pelvisRot = Quaternion.Slerp(phaseStartPelvisRot, pelvisGoalRot, s);
            Quaternion chestRot = Quaternion.Slerp(phaseStartChestRot, chestGoalRot, s);
            Vector3 chestTarget = pelvisTarget + chestRot * Vector3.up * ChestAbovePelvis;

            Lift(rd.Pelvis.Body, pelvisTarget, 0.5f, getUpPositionStiffness, getUpPositionDamping, getUpGravityCompensation);
            Lift(rd.Chest.Body, chestTarget, 0.5f, getUpPositionStiffness, getUpPositionDamping, getUpGravityCompensation);
            AlignTorque(rd.Pelvis.Body, pelvisRot, getUpRotationStiffness, getUpRotationDamping);
            AlignTorque(rd.Chest.Body, chestRot, getUpRotationStiffness, getUpRotationDamping);

            if (phaseTime >= key.duration + 0.15f)
            {
                if (getUpPhase == GetUpPhase.Stand)
                {
                    getUpPhase = GetUpPhase.None;
                    EnterState(State.Standing);
                    assistFade = 1f; // la guida si spegne gradualmente
                }
                else
                {
                    StartPhase(getUpPhase + 1);
                }
            }
        }

        void UpdateRoll()
        {
            float s = Mathf.SmoothStep(0f, 1f, phaseTime / RollBlendTime);
            foreach (var part in rd.Parts)
            {
                if (part.Joint == null) continue;
                part.TargetLocal = Quaternion.Slerp(phaseStartPose[part], PoseFor(GetUpPhase.Roll, part), s);
            }

            Vector3 torque = rd.Pelvis.transform.up * (rollSign * rollTorque);
            rd.Pelvis.Body.AddTorque(torque);
            rd.Chest.Body.AddTorque(torque);

            if (rd.Chest.transform.forward.y < -0.6f || phaseTime > MaxRollTime)
                StartPhase(GetUpPhase.PushUp);
        }

        /// <summary>Posa (rotazione relativa al genitore) di ogni parte per ogni fase.</summary>
        Quaternion PoseFor(GetUpPhase phase, RagdollPart part)
        {
            int side = part == rd.UpperArmL || part == rd.ForearmL || part == rd.ThighL || part == rd.ShinL || part == rd.FootL ? -1 : 1;
            bool lead = side == leadSide;
            bool spine = part == rd.Belly || part == rd.Chest;
            bool upperArm = part == rd.UpperArmL || part == rd.UpperArmR;
            bool forearm = part == rd.ForearmL || part == rd.ForearmR;
            bool thigh = part == rd.ThighL || part == rd.ThighR;
            bool shin = part == rd.ShinL || part == rd.ShinR;
            // altrimenti: testa o piede

            switch (phase)
            {
                case GetUpPhase.Roll: // raccolto su un fianco: braccia avanti, una gamba piegata per dare slancio
                    if (spine) return Quaternion.identity;
                    if (part == rd.Head) return X(20f);
                    if (upperArm) return X(-60f);
                    if (forearm) return X(-30f);
                    if (thigh) return lead ? X(-60f) : Quaternion.identity;
                    if (shin) return lead ? X(80f) : X(10f);
                    return Quaternion.identity;

                case GetUpPhase.PushUp: // mani sotto le spalle, schiena inarcata, testa su
                    if (spine) return X(-12f);
                    if (part == rd.Head) return X(-25f);
                    if (upperArm) return Z(side * 15f) * X(-80f);
                    if (forearm) return X(-15f);
                    if (thigh) return Quaternion.identity;
                    if (shin) return X(20f);
                    return X(20f);

                case GetUpPhase.HandsAndKnees: // carponi
                    if (spine) return Quaternion.identity;
                    if (part == rd.Head) return X(-30f);
                    if (upperArm) return Z(side * 10f) * X(-85f);
                    if (forearm) return X(-5f);
                    if (thigh) return X(-95f);
                    if (shin) return X(110f);
                    return X(30f);

                case GetUpPhase.Kneel: // in ginocchio, busto dritto
                    if (spine) return Quaternion.identity;
                    if (part == rd.Head) return Quaternion.identity;
                    if (upperArm) return Z(side * 10f) * X(-10f);
                    if (forearm) return X(-20f);
                    if (thigh) return X(-5f);
                    if (shin) return X(105f);
                    return X(30f);

                case GetUpPhase.Lunge: // un piede avanti, l'altro ginocchio a terra
                    if (spine) return X(5f);
                    if (part == rd.Head) return Quaternion.identity;
                    if (upperArm) return Z(side * 12f) * X(lead ? 20f : -30f);
                    if (forearm) return X(-20f);
                    if (thigh) return lead ? X(-85f) : X(-5f);
                    if (shin) return lead ? X(85f) : X(100f);
                    return lead ? Quaternion.identity : X(30f);

                default: // Stand
                    if (spine || part == rd.Head) return Quaternion.identity;
                    if (upperArm) return Z(side * armRestAbduction) * X(-8f);
                    if (forearm) return X(-15f);
                    if (thigh) return Quaternion.identity;
                    if (shin) return X(standKneeBend);
                    return Quaternion.identity;
            }
        }

        static Quaternion X(float degrees) => Quaternion.AngleAxis(degrees, Vector3.right);
        static Quaternion Z(float degrees) => Quaternion.AngleAxis(degrees, Vector3.forward);
    }
}
