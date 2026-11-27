using System.Collections.Generic;
using UnityEngine;

namespace FantaLandia.Ragdoll
{
    /// <summary>
    /// Costruisce un manichino umanoide di primitive (≈73 kg, ≈1.73 m) completamente fisico:
    /// 14 Rigidbody collegati da ConfigurableJoint con limiti articolari realistici.
    /// Nessuna animazione cinematica: ogni movimento nasce dalle coppie dei muscoli (joint drive).
    ///
    /// Convenzioni: il personaggio guarda +Z, la destra è +X, i piedi poggiano a y = 0 (locale).
    /// Rotazione positiva attorno a X = la parte alta del segmento va in avanti
    /// (es. ginocchio piegato = +X, coscia in avanti = -X, gomito piegato = -X).
    /// </summary>
    public class ActiveRagdoll : MonoBehaviour
    {
        // Misure principali (metri, spazio locale del manichino)
        public const float PelvisHeight = 0.98f;
        public const float HipHeight = 0.92f;
        public const float KneeHeight = 0.50f;
        public const float AnkleHeight = 0.08f;
        public const float HipHalfWidth = 0.10f;
        public const float FootForwardOffset = 0.05f;
        public const float FootHalfHeight = 0.03f;

        [Header("Costruzione")]
        public bool buildOnAwake = true;

        [Tooltip("Unity misura i limiti sull'asse X dei joint con segno opposto alla rotazione. " +
                 "Se ginocchia e gomiti si piegano al contrario, disattiva questa opzione.")]
        public bool invertXLimits = true;

        [Tooltip("Convenzione del targetRotation dei joint. Viene verificata e corretta in automatico " +
                 "dalla calibrazione iniziale del BalanceController.")]
        public bool invertDriveTargets = false;

        [Range(4, 60)] public int solverIterations = 25;
        [Range(1, 30)] public int solverVelocityIterations = 12;

        [Header("Colori")]
        public Color torsoColor = new Color(0.85f, 0.55f, 0.25f);
        public Color limbColor = new Color(0.92f, 0.82f, 0.68f);
        public Color footColor = new Color(0.25f, 0.25f, 0.3f);

        [Header("Parti (generate)")]
        public RagdollPart Pelvis, Belly, Chest, Head;
        public RagdollPart UpperArmL, ForearmL, UpperArmR, ForearmR;
        public RagdollPart ThighL, ShinL, FootL, ThighR, ShinR, FootR;

        public readonly List<RagdollPart> Parts = new List<RagdollPart>();
        public float TotalMass { get; private set; }
        public float ThighLength => HipHeight - KneeHeight;
        public float ShinLength => KneeHeight - AnkleHeight;

        PhysicsMaterial bodyMaterial;
        PhysicsMaterial footMaterial;

        void Awake()
        {
            if (buildOnAwake && Parts.Count == 0) Build();
        }

        public void Build()
        {
            bodyMaterial = new PhysicsMaterial("Ragdoll Corpo")
            {
                dynamicFriction = 0.6f,
                staticFriction = 0.7f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            footMaterial = new PhysicsMaterial("Ragdoll Piede")
            {
                dynamicFriction = 1.0f,
                staticFriction = 1.2f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            //                       nome        genitore  forma                 centro                       dimensioni                    kg    colore      pos. joint               limiti X(min,max)       Y    Z   rigidità smorz. coppia max
            Pelvis = AddPart("Bacino", null, PrimitiveType.Cube, new Vector3(0, PelvisHeight, 0), new Vector3(0.30f, 0.18f, 0.20f), 11f, torsoColor, Vector3.zero, Vector2.zero, 0, 0, 0, 0, 0, bodyMaterial);
            Belly = AddPart("Addome", Pelvis, PrimitiveType.Cube, new Vector3(0, 1.15f, 0), new Vector3(0.27f, 0.14f, 0.17f), 8f, torsoColor, new Vector3(0, 1.07f, 0), new Vector2(-25, 35), 25, 20, 1200, 50, 500, bodyMaterial);
            Chest = AddPart("Torace", Belly, PrimitiveType.Cube, new Vector3(0, 1.36f, 0), new Vector3(0.34f, 0.26f, 0.21f), 15f, torsoColor, new Vector3(0, 1.22f, 0), new Vector2(-20, 30), 25, 20, 1200, 50, 500, bodyMaterial);
            Head = AddPart("Testa", Chest, PrimitiveType.Sphere, new Vector3(0, 1.62f, 0), new Vector3(0.22f, 0.22f, 0.22f), 5f, limbColor, new Vector3(0, 1.50f, 0), new Vector2(-40, 50), 60, 30, 250, 6, 60, bodyMaterial);

            for (int s = -1; s <= 1; s += 2)
            {
                string suffix = s < 0 ? "_L" : "_R";
                float armX = 0.23f * s;
                float legX = HipHalfWidth * s;

                var upperArm = AddPart("Braccio" + suffix, Chest, PrimitiveType.Capsule, new Vector3(armX, 1.31f, 0), new Vector3(0.10f, 0.30f, 0.10f), 2.2f, limbColor, new Vector3(armX, 1.45f, 0), new Vector2(-150, 50), 70, 90, 200, 12, 80, bodyMaterial);
                var forearm = AddPart("Avambraccio" + suffix, upperArm, PrimitiveType.Capsule, new Vector3(armX, 1.02f, 0), new Vector3(0.09f, 0.28f, 0.09f), 1.6f, limbColor, new Vector3(armX, 1.16f, 0), new Vector2(-140, 5), 10, 10, 100, 3, 40, bodyMaterial);
                var thigh = AddPart("Coscia" + suffix, Pelvis, PrimitiveType.Capsule, new Vector3(legX, 0.71f, 0), new Vector3(0.15f, 0.42f, 0.15f), 8.5f, limbColor, new Vector3(legX, HipHeight, 0), new Vector2(-110, 30), 35, 45, 1500, 100, 600, bodyMaterial);
                var shin = AddPart("Stinco" + suffix, thigh, PrimitiveType.Capsule, new Vector3(legX, 0.295f, 0), new Vector3(0.11f, 0.41f, 0.11f), 3.7f, limbColor, new Vector3(legX, KneeHeight, 0), new Vector2(-5, 140), 3, 3, 1500, 100, 600, bodyMaterial);
                var foot = AddPart("Piede" + suffix, shin, PrimitiveType.Cube, new Vector3(legX, FootHalfHeight, FootForwardOffset), new Vector3(0.10f, FootHalfHeight * 2f, 0.25f), 1.1f, footColor, new Vector3(legX, AnkleHeight, 0), new Vector2(-25, 40), 15, 25, 1200, 80, 350, footMaterial);

                if (s < 0) { UpperArmL = upperArm; ForearmL = forearm; ThighL = thigh; ShinL = shin; FootL = foot; }
                else { UpperArmR = upperArm; ForearmR = forearm; ThighR = thigh; ShinR = shin; FootR = foot; }
            }

            IgnoreAdjacentCollisions();

            TotalMass = 0f;
            foreach (var p in Parts) TotalMass += p.Body.mass;
        }

        RagdollPart AddPart(string partName, RagdollPart parent, PrimitiveType shape, Vector3 center, Vector3 size,
                            float mass, Color color, Vector3 jointPos, Vector2 xRange, float yLimit, float zLimit,
                            float spring, float damper, float maxTorque, PhysicsMaterial material)
        {
            var go = new GameObject(partName);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.identity;

            // Collider sull'oggetto non scalato (così gli anchor dei joint restano in metri).
            Collider col;
            switch (shape)
            {
                case PrimitiveType.Capsule:
                    var capsule = go.AddComponent<CapsuleCollider>();
                    capsule.direction = 1;
                    capsule.radius = size.x * 0.5f;
                    capsule.height = size.y;
                    col = capsule;
                    break;
                case PrimitiveType.Sphere:
                    var sphere = go.AddComponent<SphereCollider>();
                    sphere.radius = size.x * 0.5f;
                    col = sphere;
                    break;
                default:
                    var box = go.AddComponent<BoxCollider>();
                    box.size = size;
                    col = box;
                    break;
            }
            col.sharedMaterial = material;

            // Mesh visiva in un figlio scalato, senza collider.
            var visual = GameObject.CreatePrimitive(shape);
            visual.name = "Mesh";
            DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = shape == PrimitiveType.Capsule
                ? new Vector3(size.x, size.y * 0.5f, size.z) // la capsula primitiva è alta 2 m
                : size;
            visual.GetComponent<Renderer>().material.color = color;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = 0.02f;
            rb.angularDamping = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.solverIterations = solverIterations;
            rb.solverVelocityIterations = solverVelocityIterations;
            rb.maxAngularVelocity = 30f;

            ConfigurableJoint joint = null;
            if (parent != null)
            {
                joint = go.AddComponent<ConfigurableJoint>();
                joint.connectedBody = parent.Body;
                joint.autoConfigureConnectedAnchor = true;
                joint.anchor = go.transform.InverseTransformPoint(transform.TransformPoint(jointPos));
                joint.axis = Vector3.right;
                joint.secondaryAxis = Vector3.up;

                joint.xMotion = ConfigurableJointMotion.Locked;
                joint.yMotion = ConfigurableJointMotion.Locked;
                joint.zMotion = ConfigurableJointMotion.Locked;
                joint.angularXMotion = ConfigurableJointMotion.Limited;
                joint.angularYMotion = ConfigurableJointMotion.Limited;
                joint.angularZMotion = ConfigurableJointMotion.Limited;

                float low = xRange.x, high = xRange.y;
                if (invertXLimits) { low = -xRange.y; high = -xRange.x; }
                joint.lowAngularXLimit = new SoftJointLimit { limit = low };
                joint.highAngularXLimit = new SoftJointLimit { limit = high };
                joint.angularYLimit = new SoftJointLimit { limit = yLimit };
                joint.angularZLimit = new SoftJointLimit { limit = zLimit };

                joint.rotationDriveMode = RotationDriveMode.Slerp;
                joint.enableCollision = false;
                joint.enablePreprocessing = false; // più stabile nelle catene di ragdoll
            }

            var part = go.AddComponent<RagdollPart>();
            part.Init(this, rb, parent, joint, spring, damper, maxTorque);
            Parts.Add(part);
            return part;
        }

        void IgnoreAdjacentCollisions()
        {
            // Ignora solo genitore e nonno: le altre parti (es. gamba contro gamba) collidono davvero.
            foreach (var part in Parts)
            {
                var col = part.GetComponent<Collider>();
                var parent = part.Parent;
                if (parent == null) continue;
                Physics.IgnoreCollision(col, parent.GetComponent<Collider>(), true);
                if (parent.Parent != null)
                    Physics.IgnoreCollision(col, parent.Parent.GetComponent<Collider>(), true);
            }
        }

        /// <summary>Centro di massa dell'intero corpo e la sua velocità.</summary>
        public Vector3 GetCenterOfMass(out Vector3 velocity)
        {
            Vector3 position = Vector3.zero;
            Vector3 momentum = Vector3.zero;
            float mass = 0f;
            foreach (var part in Parts)
            {
                var rb = part.Body;
                position += rb.worldCenterOfMass * rb.mass;
                momentum += rb.linearVelocity * rb.mass;
                mass += rb.mass;
            }
            if (mass <= 0f) { velocity = Vector3.zero; return transform.position; }
            velocity = momentum / mass;
            return position / mass;
        }

        /// <summary>Inverte i limiti sull'asse X di tutti i joint (usato dalla calibrazione).</summary>
        public void FlipXLimits()
        {
            invertXLimits = !invertXLimits;
            foreach (var part in Parts)
            {
                var joint = part.Joint;
                if (joint == null) continue;
                var low = joint.lowAngularXLimit;
                var high = joint.highAngularXLimit;
                float newLow = -high.limit, newHigh = -low.limit;
                low.limit = newLow;
                high.limit = newHigh;
                joint.lowAngularXLimit = low;
                joint.highAngularXLimit = high;
            }
        }

        /// <summary>Rotazione attuale della parte relativa al genitore (stessa convenzione di TargetLocal).</summary>
        public static Quaternion LocalRotation(RagdollPart part) =>
            part.Parent != null
                ? Quaternion.Inverse(part.Parent.transform.rotation) * part.transform.rotation
                : part.transform.rotation;

        /// <summary>Posizione mondo del joint che collega la parte al genitore.</summary>
        public Vector3 JointPosition(RagdollPart part) =>
            part.Joint != null ? part.transform.TransformPoint(part.Joint.anchor) : part.transform.position;
    }
}
