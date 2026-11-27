using UnityEngine;

namespace FantaLandia.Ragdoll
{
    /// <summary>
    /// Un segmento rigido del manichino: Rigidbody + ConfigurableJoint verso il segmento genitore.
    ///
    /// Il "muscolo" è la molla angolare (slerp drive) del joint: applica coppie uguali e opposte
    /// ai due segmenti collegati, quindi è una forza interna e fisicamente corretta.
    /// TargetLocal è la rotazione desiderata di questo segmento relativa al genitore
    /// (identità = posa di costruzione: in piedi, braccia lungo i fianchi).
    /// </summary>
    [DisallowMultipleComponent]
    public class RagdollPart : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public ConfigurableJoint Joint { get; private set; }
        public RagdollPart Parent { get; private set; }
        public ActiveRagdoll Owner { get; private set; }

        [Tooltip("Rigidità del muscolo (Nm/rad).")]
        public float spring;
        [Tooltip("Smorzamento del muscolo (Nm·s/rad).")]
        public float damper;
        [Tooltip("Coppia massima che il muscolo può esercitare (Nm).")]
        public float maxTorque;

        /// <summary>Rotazione desiderata relativa al genitore.</summary>
        [System.NonSerialized] public Quaternion TargetLocal = Quaternion.identity;

        float lastGroundContactTime = -10f;

        public Vector3 GroundNormal { get; private set; } = Vector3.up;

        /// <summary>True se nell'ultimo step fisico il segmento toccava una superficie "calpestabile".</summary>
        public bool IsGrounded => Time.fixedTime - lastGroundContactTime <= Time.fixedDeltaTime * 1.5f;

        public void Init(ActiveRagdoll owner, Rigidbody body, RagdollPart parent, ConfigurableJoint joint,
                         float spring, float damper, float maxTorque)
        {
            Owner = owner;
            Body = body;
            Parent = parent;
            Joint = joint;
            this.spring = spring;
            this.damper = damper;
            this.maxTorque = maxTorque;
        }

        /// <summary>Aggiorna il muscolo. strength: 0 = floscio, 1 = normale.</summary>
        public void ApplyDrive(float strength)
        {
            if (Joint == null) return;

            var drive = Joint.slerpDrive;
            drive.positionSpring = spring * strength;
            // Un minimo di tono muscolare resta anche da svenuto.
            drive.positionDamper = damper * Mathf.Max(strength, 0.25f);
            drive.maximumForce = maxTorque * Mathf.Max(strength, 0.05f);
            Joint.slerpDrive = drive;

            // Con axis=(1,0,0), secondaryAxis=(0,1,0) e rotazione iniziale relativa = identità,
            // lo spazio del joint coincide con lo spazio locale: il target è l'inversa della rotazione voluta.
            Joint.targetRotation = Owner != null && Owner.invertDriveTargets ? TargetLocal : Quaternion.Inverse(TargetLocal);
        }

        void OnCollisionEnter(Collision collision) => RegisterContact(collision);
        void OnCollisionStay(Collision collision) => RegisterContact(collision);

        void RegisterContact(Collision collision)
        {
            // Il contatto con altre parti dello stesso manichino non conta come terreno.
            if (collision.rigidbody != null &&
                collision.rigidbody.TryGetComponent(out RagdollPart other) &&
                other.Owner == Owner)
                return;

            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector3 normal = collision.GetContact(i).normal;
                if (normal.y > 0.55f)
                {
                    lastGroundContactTime = Time.fixedTime;
                    GroundNormal = normal;
                    return;
                }
            }
        }
    }
}
