using Unity.AI.Navigation;
using UnityEngine;

namespace FantaLandia.World
{
    /// <summary>
    /// Costruisce la NavMesh all'avvio della scena. Per il prototipo è più semplice che salvarla:
    /// se modifichi la scena, basta premere Play.
    /// Più avanti, con il mondo a zone, ogni zona avrà la sua NavMesh già calcolata.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(NavMeshSurface))]
    public class RuntimeNavMeshBaker : MonoBehaviour
    {
        void Awake()
        {
            GetComponent<NavMeshSurface>().BuildNavMesh();

            // Riaggancia i link alla NavMesh appena costruita.
            foreach (var link in FindObjectsByType<NavMeshLink>(FindObjectsSortMode.None))
                link.UpdateLink();
        }
    }
}
