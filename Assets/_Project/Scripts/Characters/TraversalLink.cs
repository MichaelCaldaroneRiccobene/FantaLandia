using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

namespace FantaLandia.Characters
{
    /// <summary>
    /// Informazioni extra su un NavMeshLink: quanto è alto l'ostacolo da superare, chi lo sta usando
    /// e quali sono le corsie sorelle (gli altri link dello stesso passaggio largo).
    /// Il tipo di movimento (scavalcare, arrampicarsi, saltare giù) viene scelto dal CharacterMotor
    /// in base al dislivello e a questa altezza.
    /// </summary>
    [RequireComponent(typeof(NavMeshLink))]
    public class TraversalLink : MonoBehaviour
    {
        [Tooltip("Altezza dell'ostacolo rispetto al punto di partenza (muretto, ringhiera...). 0 = rilevamento automatico.")]
        public float obstacleHeight;

        [Tooltip("Varco da saltare (fosso, ponte rotto): se è troppo largo il salto fallisce e si cade")]
        public bool isGap;

        /// <summary>Il personaggio che sta attraversando questa corsia (null = libera).</summary>
        public CharacterMotor Occupant { get; set; }
        public bool IsFree => Occupant == null;

        /// <summary>Tutte le corsie dello stesso passaggio, questa compresa.</summary>
        public TraversalLink[] Lanes { get; private set; }

        /// <summary>Prefisso del nome dell'oggetto che raggruppa le corsie di uno stesso passaggio.</summary>
        public const string LaneGroupPrefix = "Passaggio";

        public static readonly List<TraversalLink> All = new List<TraversalLink>();

        NavMeshLink link;

        void OnEnable() => All.Add(this);
        void OnDisable() { All.Remove(this); Occupant = null; }

        void Awake()
        {
            link = GetComponent<NavMeshLink>();
            // Le corsie di un passaggio sono figlie dirette di un oggetto "Passaggio" (vedi PrototypeSceneBuilder.Link).
            var lanes = new List<TraversalLink>();
            Transform group = transform.parent;
            if (group != null && group.name.StartsWith(LaneGroupPrefix))
            {
                foreach (Transform child in group)
                    if (child.TryGetComponent(out TraversalLink t)) lanes.Add(t);
            }
            if (!lanes.Contains(this)) { lanes.Clear(); lanes.Add(this); }
            Lanes = lanes.ToArray();
        }

        public bool Bidirectional => link.bidirectional;
        public Vector3 StartWorld => transform.TransformPoint(link.startPoint);
        public Vector3 EndWorld => transform.TransformPoint(link.endPoint);

        /// <summary>Punto d'ingresso dal lato indicato (0 = inizio, 1 = fine).</summary>
        public Vector3 Entry(int side) => side == 0 ? StartWorld : EndWorld;

        /// <summary>Lato d'ingresso più vicino a una posizione, e la sua distanza orizzontale. -1 se non accessibile da lì.</summary>
        public int NearestEntrySide(Vector3 position, out float distance)
        {
            float ds = FlatDistance(position, StartWorld);
            float de = Bidirectional ? FlatDistance(position, EndWorld) : float.MaxValue;
            if (ds <= de) { distance = ds; return 0; }
            distance = de;
            return 1;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
