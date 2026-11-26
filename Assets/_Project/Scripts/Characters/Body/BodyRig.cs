using System.Collections.Generic;
using UnityEngine;

namespace FantaLandia.Characters.Body
{
    /// <summary>Le parti del corpo: ognuna è un segmento separato, staccabile e sostituibile (protesi).</summary>
    public enum BodyPart
    {
        Pelvis, Abdomen, Chest, Neck, Head,
        UpperArmL, ForearmL, HandL,
        UpperArmR, ForearmR, HandR,
        ThighL, ShinL, FootL,
        ThighR, ShinR, FootR,
    }

    /// <summary>
    /// Scheletro a segmenti rigidi. Ogni giunto ha come figli diretti i suoi strati visivi
    /// ("Pelle", "Intimo", in futuro "Abbigliamento", "Protesi") e i giunti successivi della catena.
    /// Staccare un arto = nascondere il sottoalbero del giunto e generare un pezzo fisico.
    /// </summary>
    public class BodyRig : MonoBehaviour
    {
        public const int PartCount = 17;

        [Tooltip("Giunti indicizzati per BodyPart (li compila il generatore di modelli)")]
        public Transform[] joints = new Transform[PartCount];
        public float height = 1.8f;
        public bool female;

        readonly HashSet<BodyPart> severed = new HashSet<BodyPart>();
        readonly List<GameObject> pieces = new List<GameObject>();

        public Transform Joint(BodyPart part) => joints[(int)part];
        public bool IsSevered(BodyPart part) => severed.Contains(part);

        public static string DisplayName(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Pelvis: return "Bacino";
                case BodyPart.Abdomen: return "Addome";
                case BodyPart.Chest: return "Torace";
                case BodyPart.Neck: return "Collo";
                case BodyPart.Head: return "Testa";
                case BodyPart.UpperArmL: return "Braccio sinistro";
                case BodyPart.ForearmL: return "Avambraccio sinistro";
                case BodyPart.HandL: return "Mano sinistra";
                case BodyPart.UpperArmR: return "Braccio destro";
                case BodyPart.ForearmR: return "Avambraccio destro";
                case BodyPart.HandR: return "Mano destra";
                case BodyPart.ThighL: return "Coscia sinistra";
                case BodyPart.ShinL: return "Stinco sinistro";
                case BodyPart.FootL: return "Piede sinistro";
                case BodyPart.ThighR: return "Coscia destra";
                case BodyPart.ShinR: return "Stinco destro";
                default: return "Piede destro";
            }
        }

        /// <summary>
        /// Stacca una parte (e tutto ciò che ci è attaccato sotto): il pezzo cade a terra con la fisica.
        /// Prima versione per il prototipo: più avanti si aggancerà al sistema salute/arti.
        /// </summary>
        public GameObject Sever(BodyPart part, Vector3 impulse)
        {
            Transform joint = Joint(part);
            if (joint == null || severed.Contains(part)) return null;

            // Copia visiva del sottoalbero (contiene solo transform e mesh).
            GameObject piece = Instantiate(joint.gameObject, joint.position, joint.rotation);
            piece.name = "Parte staccata: " + DisplayName(part);
            piece.transform.localScale = joint.lossyScale;

            Bounds b = default;
            bool first = true;
            foreach (Renderer r in piece.GetComponentsInChildren<Renderer>())
            {
                r.enabled = true;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            var col = piece.AddComponent<BoxCollider>();
            col.center = piece.transform.InverseTransformPoint(b.center);
            col.size = piece.transform.InverseTransformVector(b.size);
            col.size = new Vector3(Mathf.Abs(col.size.x), Mathf.Abs(col.size.y), Mathf.Abs(col.size.z));
            var rb = piece.AddComponent<Rigidbody>();
            rb.mass = 3f;
            rb.AddForce(impulse, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
            pieces.Add(piece);

            // Nasconde l'originale e segna come staccate tutte le parti a valle.
            foreach (Renderer r in joint.GetComponentsInChildren<Renderer>()) r.enabled = false;
            for (int i = 0; i < PartCount; i++)
                if (joints[i] != null && joints[i].IsChildOf(joint)) severed.Add((BodyPart)i);
            return piece;
        }

        /// <summary>Debug: stacca un arto a caso tra quelli ancora attaccati.</summary>
        public GameObject SeverRandomLimb()
        {
            BodyPart[] limbs =
            {
                BodyPart.UpperArmL, BodyPart.ForearmL, BodyPart.HandL,
                BodyPart.UpperArmR, BodyPart.ForearmR, BodyPart.HandR,
                BodyPart.ShinL, BodyPart.FootL, BodyPart.ShinR, BodyPart.FootR,
            };
            var candidates = new List<BodyPart>();
            foreach (var p in limbs) if (!severed.Contains(p)) candidates.Add(p);
            if (candidates.Count == 0) return null;
            BodyPart part = candidates[Random.Range(0, candidates.Count)];
            Vector3 impulse = (Random.insideUnitSphere + Vector3.up) * 1.5f;
            return Sever(part, impulse);
        }

        /// <summary>Debug: riattacca tutto e rimuove i pezzi caduti.</summary>
        public void RestoreAll()
        {
            foreach (var p in pieces) if (p != null) Destroy(p);
            pieces.Clear();
            severed.Clear();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        }
    }
}
