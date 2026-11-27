using UnityEngine;

namespace FantaLandia.Ragdoll
{
    /// <summary>
    /// Calibrazione automatica all'avvio (circa 1,4 s, solo la prima volta):
    /// il bacino resta fermo in aria e il manichino fa due piccoli movimenti di prova per verificare
    ///  1. la convenzione del targetRotation dei ConfigurableJoint (alza le braccia lateralmente:
    ///     i limiti laterali sono simmetrici, quindi il test non dipende dal punto 2);
    ///  2. il segno dei limiti sull'asse X (piega le ginocchia: se restano dritte, i limiti sono al contrario).
    /// Se qualcosa non torna lo corregge da solo e lo scrive nella Console. Poi scende a terra.
    /// </summary>
    public partial class BalanceController
    {
        // Validi per tutti i manichini della sessione (dipendono dalla versione di Unity, non dal manichino).
        static bool s_calibrated, s_driveInverted, s_limitsFlipped;

        const float CalibrationLift = 0.25f;
        const float CalibrationArmTest = 0.4f;
        const float CalibrationKneeTest = 0.9f;
        const float CalibrationEnd = 1.4f;

        Vector3 calibrationStandPos, calibrationRaisedPos;
        bool calibrationDriveChecked, calibrationLimitsChecked;

        [Header("Calibrazione")]
        [Tooltip("All'avvio verifica e corregge da solo le convenzioni dei joint (circa 1,4 s sospeso in aria).")]
        public bool autoCalibrate = true;

        public static bool IsCalibrated => s_calibrated;
        public static bool CalibrationDriveInverted => s_driveInverted;
        public static bool CalibrationLimitsFlipped => s_limitsFlipped;

        void BeginCalibrationOrStand()
        {
            if (s_calibrated || !autoCalibrate)
            {
                rd.invertDriveTargets = s_driveInverted;
                if (s_limitsFlipped) rd.FlipXLimits();
                EnterState(State.Standing);
                return;
            }

            calibrationStandPos = rd.Pelvis.transform.position;
            calibrationRaisedPos = calibrationStandPos + Vector3.up * CalibrationLift;
            rd.transform.position += Vector3.up * CalibrationLift;
            rd.Pelvis.Body.isKinematic = true;
            calibrationDriveChecked = false;
            calibrationLimitsChecked = false;
            EnterState(State.Calibrating);
        }

        void UpdateCalibration()
        {
            foreach (var part in rd.Parts) part.TargetLocal = Quaternion.identity;
            float t = stateTime;

            // 1) Braccia aperte di 45°.
            Quaternion armL = Quaternion.AngleAxis(-45f, Vector3.forward);
            Quaternion armR = Quaternion.AngleAxis(45f, Vector3.forward);
            if (t < CalibrationArmTest)
            {
                rd.UpperArmL.TargetLocal = armL;
                rd.UpperArmR.TargetLocal = armR;
                return;
            }
            if (!calibrationDriveChecked)
            {
                calibrationDriveChecked = true;
                Quaternion l = ActiveRagdoll.LocalRotation(rd.UpperArmL);
                Quaternion r = ActiveRagdoll.LocalRotation(rd.UpperArmR);
                float asExpected = Quaternion.Angle(l, armL) + Quaternion.Angle(r, armR);
                float mirrored = Quaternion.Angle(l, Quaternion.Inverse(armL)) + Quaternion.Angle(r, Quaternion.Inverse(armR));
                if (mirrored < asExpected)
                {
                    rd.invertDriveTargets = !rd.invertDriveTargets;
                    Debug.Log($"[Ragdoll] Calibrazione: convenzione dei joint drive invertita, corretta (invertDriveTargets = {rd.invertDriveTargets}).");
                }
                else
                {
                    Debug.Log("[Ragdoll] Calibrazione: convenzione dei joint drive corretta.");
                }
                s_driveInverted = rd.invertDriveTargets;
            }

            // 2) Ginocchia piegate di 60°.
            if (t < CalibrationKneeTest)
            {
                rd.ShinL.TargetLocal = Quaternion.AngleAxis(60f, Vector3.right);
                rd.ShinR.TargetLocal = Quaternion.AngleAxis(60f, Vector3.right);
                return;
            }
            if (!calibrationLimitsChecked)
            {
                calibrationLimitsChecked = true;
                float bend = 0.5f * (Quaternion.Angle(ActiveRagdoll.LocalRotation(rd.ShinL), Quaternion.identity) +
                                     Quaternion.Angle(ActiveRagdoll.LocalRotation(rd.ShinR), Quaternion.identity));
                if (bend < 25f)
                {
                    rd.FlipXLimits();
                    s_limitsFlipped = !s_limitsFlipped;
                    Debug.Log($"[Ragdoll] Calibrazione: limiti articolari al contrario (ginocchio a {bend:0}°), corretti.");
                }
                else
                {
                    Debug.Log($"[Ragdoll] Calibrazione: limiti articolari corretti (ginocchio a {bend:0}°).");
                }
            }

            // 3) Scende dolcemente fino all'altezza in piedi, poi lo lascia libero.
            foreach (int side in Sides) Leg(side).shin.TargetLocal = Quaternion.AngleAxis(standKneeBend, Vector3.right);
            if (t < CalibrationEnd)
            {
                float p = Mathf.SmoothStep(0f, 1f, (t - CalibrationKneeTest) / (CalibrationEnd - CalibrationKneeTest));
                rd.Pelvis.Body.MovePosition(Vector3.Lerp(calibrationRaisedPos, calibrationStandPos, p));
                rd.Pelvis.Body.MoveRotation(heading);
                return;
            }

            rd.Pelvis.Body.isKinematic = false;
            s_calibrated = true;
            EnterState(State.Standing);
        }
    }
}
