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

        /// <summary>Debug: riattacca tutto, rimuove protesi e pezzi caduti.</summary>
        public void RestoreAll()
        {
            foreach (var p in pieces) if (p != null) Destroy(p);
            pieces.Clear();
            severed.Clear();
            foreach (Transform j in joints)
            {
                if (j == null) continue;
                Transform pr = j.Find(ProstheticLayer);
                if (pr != null) Destroy(pr.gameObject);
            }
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        }

        // ------------------------------------------------------------------ protesi

        public const string ProstheticLayer = "Protesi";
        static Material metal, darkMetal, brass;

        /// <summary>
        /// Monta una protesi meccanica sulla parte staccata e su quelle sotto di lei (es. avambraccio + mano).
        /// La forma segue le misure dell'arto originale (dalla mesh "Pelle" del segmento).
        /// </summary>
        public void AttachProsthetic(BodyPart part)
        {
            Transform root = Joint(part);
            if (root == null) return;
            for (int i = 0; i < PartCount; i++)
            {
                Transform j = joints[i];
                if (j == null || !j.IsChildOf(root) || !severed.Contains((BodyPart)i)) continue;
                severed.Remove((BodyPart)i);
                BuildProsthetic((BodyPart)i, j);
            }
        }

        void BuildProsthetic(BodyPart part, Transform joint)
        {
            EnsureMaterials();
            Transform old = joint.Find(ProstheticLayer);
            if (old != null) Destroy(old.gameObject);

            var layer = new GameObject(ProstheticLayer).transform;
            layer.SetParent(joint, false);

            // misure dell'arto originale
            Bounds b = new Bounds(new Vector3(0f, -0.15f, 0f), new Vector3(0.1f, 0.3f, 0.1f));
            Transform skin = joint.Find("Pelle");
            if (skin != null && skin.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
            {
                b = mf.sharedMesh.bounds;
                b.center = Vector3.Scale(b.center, skin.localScale);
                b.size = Vector3.Scale(b.size, skin.localScale);
            }
            Vector3 c = b.center, sz = b.size;
            float w = Mathf.Min(sz.x, sz.z);

            bool hand = part == BodyPart.HandL || part == BodyPart.HandR;
            bool foot = part == BodyPart.FootL || part == BodyPart.FootR;

            if (hand)
            {
                Piece(layer, new Vector3(0f, -sz.y * 0.25f, 0f), new Vector3(sz.x * 0.9f, sz.y * 0.45f, sz.z * 0.9f), darkMetal); // palmo
                for (int k = 0; k < 3; k++)
                {
                    float z = (k - 1) * sz.z * 0.3f;
                    Piece(layer, new Vector3(0f, -sz.y * 0.68f, z), new Vector3(sz.x * 0.5f, sz.y * 0.45f, sz.z * 0.16f), metal);    // dita
                    Piece(layer, new Vector3(0f, -sz.y * 0.5f, z), new Vector3(sz.x * 0.6f, sz.y * 0.07f, sz.z * 0.2f), brass);      // nocche
                }
                Piece(layer, new Vector3(0f, -sz.y * 0.3f, sz.z * 0.55f), new Vector3(sz.x * 0.5f, sz.y * 0.35f, sz.z * 0.18f), metal); // pollice
                Piece(layer, Vector3.zero, Vector3.one * w * 0.6f, brass);                                                             // polso
                return;
            }

            if (foot)
            {
                Piece(layer, new Vector3(c.x, b.min.y + sz.y * 0.15f, c.z), new Vector3(sz.x * 0.95f, sz.y * 0.3f, sz.z), darkMetal); // suola
                Piece(layer, new Vector3(0f, (b.min.y + 0f) * 0.5f, 0f), new Vector3(w * 0.35f, Mathf.Abs(b.min.y) * 0.8f, w * 0.35f), metal); // caviglia
                Piece(layer, new Vector3(c.x, b.min.y + sz.y * 0.4f, b.max.z - sz.z * 0.15f), new Vector3(sz.x * 0.8f, sz.y * 0.25f, sz.z * 0.28f), metal); // punta
                Piece(layer, Vector3.zero, Vector3.one * w * 0.5f, brass);
                return;
            }

            // arto lungo: asta centrale, piastre laterali, giunto in ottone, rivetti
            Piece(layer, c, new Vector3(w * 0.38f, sz.y * 0.95f, w * 0.38f), metal);
            Piece(layer, c + new Vector3(sz.x * 0.3f, 0f, 0f), new Vector3(sz.x * 0.12f, sz.y * 0.6f, sz.z * 0.7f), darkMetal);
            Piece(layer, c - new Vector3(sz.x * 0.3f, 0f, 0f), new Vector3(sz.x * 0.12f, sz.y * 0.6f, sz.z * 0.7f), darkMetal);
            Piece(layer, new Vector3(0f, b.max.y - w * 0.3f, 0f), Vector3.one * w * 0.75f, brass);                   // giunto
            Piece(layer, new Vector3(0f, b.min.y + w * 0.15f, 0f), new Vector3(w * 0.6f, w * 0.3f, w * 0.6f), darkMetal); // attacco
            for (int k = -1; k <= 1; k += 2)
                Piece(layer, c + new Vector3(k * sz.x * 0.37f, sz.y * 0.2f, 0f), Vector3.one * w * 0.12f, brass);     // rivetti
        }

        static void Piece(Transform parent, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Pezzo";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void EnsureMaterials()
        {
            if (metal != null) return;
            metal = MakeMetal(new Color(0.55f, 0.57f, 0.6f), 0.7f, 0.9f);
            darkMetal = MakeMetal(new Color(0.22f, 0.23f, 0.25f), 0.5f, 0.8f);
            brass = MakeMetal(new Color(0.78f, 0.6f, 0.28f), 0.75f, 1f);
        }

        static Material MakeMetal(Color color, float smoothness, float metallic)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var m = new Material(sh) { color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            return m;
        }
    }
}
