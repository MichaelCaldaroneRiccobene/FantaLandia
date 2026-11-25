using Unity.AI.Navigation;
using UnityEngine;

namespace FantaLandia.Characters
{
    /// <summary>
    /// Informazioni extra su un NavMeshLink: quanto è alto l'ostacolo da superare.
    /// Il tipo di movimento (scavalcare, arrampicarsi, saltare giù) viene scelto dal CharacterMotor
    /// in base al dislivello e a questa altezza.
    /// </summary>
    [RequireComponent(typeof(NavMeshLink))]
    public class TraversalLink : MonoBehaviour
    {
        [Tooltip("Altezza dell'ostacolo rispetto al punto di partenza (muretto, ringhiera...). 0 = rilevamento automatico.")]
        public float obstacleHeight;
    }
}
